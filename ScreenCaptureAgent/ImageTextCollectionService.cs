using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Text;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Point = OpenCvSharp.Point;
using Timer = System.Timers.Timer;

namespace ScreenCaptureAgent
{
    public partial class ImageTextCollectionService : ServiceBase
    {
        private Timer _captureTimer;
        private readonly BlockingCollection<string> _screenShotQueue = new BlockingCollection<string>();
        private IManagedMqttClient _mqttClient;
        private Config _config;
        private bool _isRunning = true;

        // 在构造函数中初始化 Serilog
        public ImageTextCollectionService()
        {
            InitializeComponent();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine("logs", "log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        protected override void OnStart(string[] args)
        {
            // 加载配置文件
            _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("appsettings.json"));

            // 启动 MQTT 客户端
            StartMqttClient();

            // 启动截屏线程和处理线程
            StartScreenshotThread();
            StartProcessingThread();
        }

        public void Start(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStop()
        {
            _isRunning = false;
            _captureTimer.Stop();
            _screenShotQueue.CompleteAdding(); // 标记队列不再接受新项
            _mqttClient.StopAsync().Wait();
        }

        private void StartScreenshotThread()
        {
            #region 启动时清空temp文件夹
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true); // 删除文件夹及其内容
            }
            #endregion

            _captureTimer = new Timer(_config.CollectionFrequency);
            _captureTimer.Elapsed += CaptureScreenShot;
            _captureTimer.Start();
        }

        private void StartProcessingThread()
        {
            new Thread(ProcessScreenshots).Start();
        }

        private void CaptureScreenShot(object sender, ElapsedEventArgs e)
        {
            try
            {
                // 截屏
                Bitmap screenShot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                Graphics g = Graphics.FromImage(screenShot);
                g.CopyFromScreen(0, 0, 0, 0, screenShot.Size);

                // 保存图片
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                string path = Path.Combine(tempDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");
                screenShot.Save(path);

                // 发送消息到队列
                _screenShotQueue.Add(path);
            }
            catch (Exception ex)
            {
                OutputMessage("截屏失败", ex);
            }
        }

        private void ProcessScreenshots()
        {
            while (_isRunning)
            {
                try
                {
                    // 阻塞等待队列中的新项
                    string screenShotPath = _screenShotQueue.Take();

                    // 图像校验
                    if (!VerifyImage(screenShotPath))
                    {
                        OutputMessage("未监测到程序画面");
                        File.Delete(screenShotPath);
                        continue;
                    }

                    // 图像采集
                    var results = new Dictionary<string, string>();
                    var tasks = new List<Task>();
                    foreach (var area in _config.ImageCollectionAreas)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            string text = PerformOcr(screenShotPath, area.Vertices);
                            results[area.Name] = text;
                        }));
                    }
                    Task.WaitAll(tasks.ToArray());

                    // 汇总结果并发送 MQTT 消息
                    var mqttMessage = JsonConvert.SerializeObject(results);
                    UploadIsTelemetryData(mqttMessage);

                    // 保存本地日志
                    if (_config.CsvRecord)
                    {
                        SaveToCsv(results);
                    }

                    // 删除截屏图片
                    File.Delete(screenShotPath);
                }
                catch (InvalidOperationException)
                {
                    // 队列已完成添加且为空时抛出此异常，忽略即可
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "处理截屏失败");
                }
            }
        }

        private bool VerifyImage(string screenShotPath)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath, ImreadModes.Color))
            {
                var vertices = _config.ImageVerificationArea.Vertices;
                var roi = new Rect(vertices[0].X, vertices[0].Y, vertices[1].X - vertices[0].X, vertices[1].Y - vertices[0].Y);

                var path = Path.Combine("data", _config.ImageVerificationArea.FileName);
                using (Mat verificationImage = Cv2.ImRead(path, ImreadModes.Color))
                {
                    Mat roiImage = screenShot[roi];
                    Mat result = new Mat();
                    Cv2.MatchTemplate(roiImage, verificationImage, result, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                    return maxVal >= _config.ImageVerificationArea.MatchDegree;
                }
            }
        }

        private string PerformOcr(string screenShotPath, List<Point> vertices)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath, ImreadModes.Color))
            {
                var roi = new Rect(vertices[0].X, vertices[0].Y, vertices[1].X - vertices[0].X, vertices[1].Y - vertices[0].Y);

                // 使用 OpenCvSharp 进行简单的 OCR 示例，实际应用中可以替换为 PaddleOCRSharp
                var trainedDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data/");
                using (var ocr = OCRTesseract.Create(
                           datapath: trainedDataPath,
                           language: "eng",
                           //charWhitelist: "0123456789", // 只允许识别数字
                           oem: 3,
                           psmode: 3))
                {
                    Mat roiImage = screenShot[roi];
                    Mat gray = new Mat();
                    Cv2.CvtColor(roiImage, gray, ColorConversionCodes.BGR2GRAY);
                    ocr.Run(gray, out string text, out _, out _, out _);
                    return text.Trim();
                }
            }
        }

        private void SaveToCsv(Dictionary<string, string> results)
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string csvPath = Path.Combine("output", $"{date}.csv");
            bool isNewFile = !File.Exists(csvPath);

            using (var writer = new StreamWriter(csvPath, true, Encoding.UTF8))
            {
                if (isNewFile)
                {
                    writer.WriteLine(string.Join(",", results.Keys));
                }
                writer.WriteLine(string.Join(",", results.Values));
            }
        }

        private void OutputMessage(string msg, Exception ex = null)
        {
            if (ex == null)
            {
                Log.Warning(msg);
            }
            else
            {
                Log.Error(ex, msg);
            }
            UploadIsTelemetryData(msg);
        }

        private void UploadIsTelemetryData(string message)
        {
            var mqttMessage = new MqttApplicationMessageBuilder()
               .WithTopic($"devices/{_config.DeviceName}/telemetry")
               .WithPayload(message)
               .Build();

            _mqttClient.EnqueueAsync(mqttMessage);
        }

        private async void StartMqttClient()
        {
            var options = new ManagedMqttClientOptionsBuilder()
               .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
               .WithClientOptions(new MqttClientOptionsBuilder()
                   .WithClientId(_config.MqttBroker.ClientId)
                   .WithTcpServer(_config.MqttBroker.Ip)
                   .WithCredentials(_config.MqttBroker.Username, _config.MqttBroker.Password)
                   .Build())
               .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            await _mqttClient.StartAsync(options);
        }
    }

}