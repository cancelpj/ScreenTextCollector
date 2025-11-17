using PluginInterface;
using ScreenTextCollector.OpenCvSharp;
using SimpleMqttClient;
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

        private readonly Settings _settings = Tool.Settings;

        public void Start(string[] args)
        {
            // 启动 MQTT 客户端
            var mqttBrokerConfig = Tool.Settings.MqttBroker;
            if (!mqttBrokerConfig.EnableMqttPush) return;

            var ret = StartMqttClient(mqttBrokerConfig, out var mqttClient);
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
                return;
            }

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

        ~ScreenTextCollectorService()
        {
            _cts.Cancel();
            _captureTimer.Stop();
            _screenShotQueue.CompleteAdding(); // 标记队列不再接受新项
            //_mqttClient.StopAsync().Wait();
            Tool.Log.Info("截屏线程已停止");
        }

        private void StartScreenShotThread()
        {
            _captureTimer = new Timer(Tool.Settings.MqttBroker.CaptureFrequency);
            _captureTimer.Elapsed += CaptureScreenShot;
            _captureTimer.Start();
            Tool.Log.Info("截屏线程已启动");
        }

        private void StartProcessingThread()
        {
            Tool.Log.Info("图像处理线程已启动");
            IOcrService ocrService = new OcrService();
            Tool.ProcessScreenshots(_screenShotQueue, ocrService.VerifyImage, ocrService.PerformOcr, PushMqtt, _cts.Token);
        }

        private void CaptureScreenShot(object sender, ElapsedEventArgs e)
        {
            var result = Tool.CaptureScreenShot(_screenShotQueue);
            Tool.OutputMessage(result);
        }

        private MethodResult StartMqttClient(MqttBrokerConfig mqttBrokerConfig, out MqttClient mqttClient)
        {
            mqttClient = null;
            if (string.IsNullOrEmpty(mqttBrokerConfig.Ip)) return new MethodResult("请配置 MQTT 服务器 IP 地址");
            if (string.IsNullOrEmpty(mqttBrokerConfig.ClientId)) return new MethodResult("请配置 MQTT 推送客户端 ID");
            if (string.IsNullOrEmpty(mqttBrokerConfig.Topic)) return new MethodResult("请配置 MQTT 推送主题");

            Tool.Log.Info($"MQTT 推送服务已启动，服务器: {mqttBrokerConfig.Ip}, 客户端ID: {mqttBrokerConfig.ClientId}");
            try
            {
                mqttClient = new MqttClient(mqttBrokerConfig.ClientId);
                if (!string.IsNullOrEmpty(mqttBrokerConfig.Username) && !string.IsNullOrEmpty(mqttBrokerConfig.Password))
                    mqttClient.SetCredentials(mqttBrokerConfig.Username, mqttBrokerConfig.Password);
                mqttClient.Connected += () =>
                {
                    Console.WriteLine($"{DateTime.Now} MQTT 服务器已连接！\n");
                    //mqttClient.Subscribe("command/topic");
                };

                mqttClient.Disconnected += () =>
                {
                    Console.WriteLine($"{DateTime.Now} MQTT 服务器已断开！\n");
                };
            
                return new MethodResult("ok", MethodResultType.Success);
            }
            catch (Exception ex)
            {
                return new MethodResult("MQTT 服务连接异常", ex);
            }
        }

        private MethodResult PushMqtt(string message)
        {

        }
    }
}