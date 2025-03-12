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
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Common;
using Point = System.Drawing.Point;
using Timer = System.Timers.Timer;

namespace ScreenTextCollector.OpenCVsharp
{
    public partial class ScreenTextCollectorService
    {
        private Timer _captureTimer;
        private readonly BlockingCollection<string> _screenShotQueue = new BlockingCollection<string>();
        private readonly Settings _settings;
        private IManagedMqttClient _mqttClient;

        // 在构造函数中初始化 Serilog
        public ScreenTextCollectorService()
        {
            // 加载配置文件
            _settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("appsettings.json"));

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine("logs", "log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        public void Start(string[] args)
        {
            // 启动 MQTT 客户端
            StartMqttClient();

            #region 启动时清空temp文件夹

            Log.Information("启动时清空 temp 文件夹");
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true); // 删除文件夹及其内容
            }

            #endregion

            // 启动截屏线程和处理线程
            StartScreenShotThread();
            StartProcessingThread();
        }

        public void Stop()
        {
            _captureTimer.Stop();
            _screenShotQueue.CompleteAdding(); // 标记队列不再接受新项
            _mqttClient.StopAsync().Wait();
        }

        private void StartScreenShotThread()
        {
            _captureTimer = new Timer(_settings.CaptureFrequency);
            _captureTimer.Elapsed += CaptureScreenShot;
            _captureTimer.Start();
            Log.Information("截屏线程已启动");
        }

        private void StartProcessingThread()
        {
            new Thread(ProcessScreenshots).Start();
        }

        private void CaptureScreenShot(object sender, ElapsedEventArgs e)
        {
            try
            {
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var path = Path.Combine(tempDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");

                if (Screen.AllScreens.Length - 1 < _settings.ScreenNumber)
                {
                    OutputMessage("屏幕编号超出范围");
                    return;
                }
                Screen screen = Screen.AllScreens[_settings.ScreenNumber];
                Rectangle bounds = screen.Bounds;
                using (Bitmap screenShot = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(screenShot))
                    {
                        // 截屏
                        g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);

                        // 保存图片
                        screenShot.Save(path);
                    }
                }

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
            Log.Information("图像线程已启动");
            while (_captureTimer.Enabled)
            {
                try
                {
                    // 阻塞等待队列中的新项
                    string screenShotPath = _screenShotQueue.Take();

                    // 图像校验
                    if (!VerifyImage(screenShotPath, _settings.ImageVerificationAreas))
                    {
                        OutputMessage("未监测到程序画面");
                        File.Delete(screenShotPath);
                        continue;
                    }

                    // 图像采集
                    var results = new Dictionary<string, string>();
                    Parallel.ForEach(_settings.ImageCollectionAreas, area =>
                    {
                        var text = PerformOcr(screenShotPath, area);
                        results[area.Name] = text;
                    });

                    // 汇总结果并发送 MQTT 消息
                    var mqttMessage = JsonConvert.SerializeObject(results);
                    Log.Information("识别结果：{Results}", mqttMessage);
                    UploadIsTelemetryData(mqttMessage);

                    // 保存本地日志
                    if (_settings.CsvRecord)
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
                    OutputMessage("处理截屏失败", ex);
                }
            }
        }

        private static bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath))
            {
                // 逐个对比图像检测区域
                foreach (var area in imageVerificationAreas)
                {
                    var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                    var path = Path.Combine("data", area.FileName);
                    using (Mat verificationImage = Cv2.ImRead(path))
                    {
                        Mat roiImage = screenShot[roi];
                        Mat matResult = new Mat();
                        Cv2.MatchTemplate(roiImage, verificationImage, matResult, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(matResult, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                        if (maxVal < area.MatchThreshold) return false; // 只要有区域图像对比不通过，就认为未监测到程序画面
                    }
                }
            }

            return true;
        }

        private static string PerformOcr(string screenShotPath, ImageCollectionArea area)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath, ImreadModes.Color))
            {
                var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

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

        private static void SaveToCsv(Dictionary<string, string> results)
        {
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            string date = DateTime.Now.ToString("yyyyMMdd");
            string csvPath = Path.Combine(saveDir, $"{date}.csv");
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
                .WithTopic($"devices/{_settings.DeviceName}/telemetry")
                .WithPayload(message)
                .Build();

            _mqttClient.EnqueueAsync(mqttMessage);
        }

        private async void StartMqttClient()
        {
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(_settings.MqttBroker.ClientId)
                    .WithTcpServer(_settings.MqttBroker.Ip)
                    .WithCredentials(_settings.MqttBroker.Username, _settings.MqttBroker.Password)
                    .Build())
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            await _mqttClient.StartAsync(options);
            Log.Information("MQTT 客户端已连接");
        }
    }
}