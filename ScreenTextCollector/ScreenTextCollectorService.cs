using Newtonsoft.Json;
using PluginInterface;
using ScreenTextCollector.OpenCVsharp;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ScreenTextCollector
{
    internal class ScreenTextCollectorService
    {
        private Timer _captureTimer;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BlockingCollection<string> _screenShotQueue = new BlockingCollection<string>();

        private readonly Settings _settings =
            JsonConvert.DeserializeObject<Settings>(File.ReadAllText("appsettings.json"));

        public void Start(string[] args)
        {
            // 启动 MQTT 客户端
            //StartMqttClient();

            #region 启动时清空temp文件夹

            Tool.Log.Info("启动时清空 temp 文件夹");
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
            _cts.Cancel();
            _captureTimer.Stop();
            _screenShotQueue.CompleteAdding(); // 标记队列不再接受新项
            //_mqttClient.StopAsync().Wait();
            Tool.Log.Info("截屏线程已停止");
        }

        private void StartScreenShotThread()
        {
            _captureTimer = new Timer(_settings.CaptureFrequency);
            _captureTimer.Elapsed += CaptureScreenShot;
            _captureTimer.Start();
            Tool.Log.Info("截屏线程已启动");
        }

        private void StartProcessingThread()
        {
            Tool.Log.Info("图像处理线程已启动");
            IOcrService ocrService = new OcrService();
            Tool.ProcessScreenshots(_settings, _screenShotQueue, _cts.Token, ocrService.VerifyImage,
                ocrService.PerformOcr);
        }

        private void CaptureScreenShot(object sender, ElapsedEventArgs e)
        {
            var result = Tool.CaptureScreenShot(_settings, _screenShotQueue);
            Tool.OutputMessage(result);
        }
    }
}