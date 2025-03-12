using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Common;
using Newtonsoft.Json;
using NLog;
using PaddleOCRSharp;
using Timer = System.Timers.Timer;

namespace ScreenTextCollector
{
    public class ScreenTextCollectorService
    {
        private Timer _captureTimer;
        private readonly BlockingCollection<string> _screenShotQueue = new BlockingCollection<string>();

        private readonly Settings _settings =
            JsonConvert.DeserializeObject<Settings>(File.ReadAllText("appsettings.json"));

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public void Start(string[] args)
        {
            // 启动 MQTT 客户端
            StartMqttClient();

            #region 启动时清空temp文件夹

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
            //_mqttClient.StopAsync().Wait();
        }

        private void StartScreenShotThread()
        {
            _captureTimer = new Timer(_settings.CaptureFrequency);
            _captureTimer.Elapsed += SaveScreenShot;
            _captureTimer.Start();
            _logger.Info("截屏线程已启动");
        }

        private void StartProcessingThread()
        {
            new Thread(ProcessScreenshots).Start();
        }

        private void SaveScreenShot(object sender, ElapsedEventArgs e)
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
            while (_captureTimer.Enabled)
            {
                try
                {
                    // 阻塞等待队列中的新项
                    string screenShotPath = _screenShotQueue.Take();

                    // 图像校验
                    if (!VerifyScreenShot(screenShotPath, _settings.ImageVerificationAreas))
                    {
                        OutputMessage("未监测到程序画面");
                        File.Delete(screenShotPath);
                        continue;
                    }

                    // 图像采集
                    var results = new Dictionary<string, string>();
                    Parallel.ForEach(_settings.ImageCollectionAreas, area =>
                    {
                        var text = PerformOCR(screenShotPath, area);
                        results[area.Name] = text;
                    });

                    // 汇总结果并发送 MQTT 消息
                    var mqttMessage = JsonConvert.SerializeObject(results);
                    _logger.Info("识别结果：{Results}", mqttMessage);
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

        private static bool VerifyScreenShot(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            using (var screenShot = new Bitmap(screenShotPath))
            {
                // 逐个对比图像检测区域
                foreach (var area in imageVerificationAreas)
                {
                    using (Bitmap validationArea = screenShot.Clone(
                               new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height),
                               PixelFormat.Format24bppRgb))
                    {
                        var path = Path.Combine("data", area.FileName);
                        using (var expectedImage = new Bitmap(path))
                        {
                            var matchThreshold = CompareImages(validationArea, expectedImage);
                            if (matchThreshold < area.MatchThreshold) return false; // 只要有区域图像对比不通过，就认为未监测到程序画面
                        }
                    }
                }
            }

            return true;
        }

        private static float CompareImages(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
            {
                return 0;
            }

            int totalPixels = img1.Width * img1.Height;
            int matchingPixels = 0;

            for (int y = 0; y < img1.Height; y++)
            {
                for (int x = 0; x < img1.Width; x++)
                {
                    Color pixel1 = img1.GetPixel(x, y);
                    Color pixel2 = img2.GetPixel(x, y);

                    if (pixel1 == pixel2)
                    {
                        matchingPixels++;
                    }
                }
            }

            return (float)matchingPixels / totalPixels;
        }

        private static string PerformOCR(string screenShotPath, ImageCollectionArea area)
        {
            using (var screenShot = new Bitmap(screenShotPath))
            {
                using (Bitmap image = screenShot.Clone(
                           new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height),
                           PixelFormat.Format24bppRgb))
                {
                    var engine = new PaddleOCREngine();
                    OCRResult result = engine.DetectText(image);
                    return result.Text.Trim();
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
                _logger.Warn(msg);
            }
            else
            {
                _logger.Error(ex, msg);
            }

            UploadIsTelemetryData(msg);
        }

        private static void UploadIsTelemetryData(string message)
        {
            //var mqttMessage = new MqttApplicationMessageBuilder()
            //   .WithTopic($"devices/{_config.DeviceName}/telemetry")
            //   .WithPayload(message)
            //   .Build();

            //_mqttClient.EnqueueAsync(mqttMessage);
        }

        private void StartMqttClient()
        {
            //var options = new ManagedMqttClientOptionsBuilder()
            //   .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            //   .WithClientOptions(new MqttClientOptionsBuilder()
            //       .WithClientId(_config.MqttBroker.ClientId)
            //       .WithTcpServer(_config.MqttBroker.Ip)
            //       .WithCredentials(_config.MqttBroker.Username, _config.MqttBroker.Password)
            //       .Build())
            //   .Build();

            //_mqttClient = new MqttFactory().CreateManagedMqttClient();
            //await _mqttClient.StartAsync(options);
        }
    }
}