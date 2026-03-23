using Newtonsoft.Json;
using PluginInterface;
using SimpleMqttClient;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        #region MQTT推送服务

        private static void StartMqttPush(MqttBrokerConfig mqttBrokerConfig, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(mqttBrokerConfig.Ip) && !string.IsNullOrEmpty(mqttBrokerConfig.Topic))
            {
                Tool.Log.Info(
                    $"{DateTime.Now} MQTT 推送服务已启动，服务器: {mqttBrokerConfig.Ip}, 主题: {mqttBrokerConfig.Topic}\n");
                try
                {
                    #region MQTT 连接配置

                    _mqttClient = new MqttClient(mqttBrokerConfig.ClientId);
                    if (!string.IsNullOrEmpty(mqttBrokerConfig.Username) &&
                        !string.IsNullOrEmpty(mqttBrokerConfig.Password))
                        _mqttClient.SetCredentials(mqttBrokerConfig.Username, mqttBrokerConfig.Password);

                    _mqttClient.Connected += () =>
                    {
                        Tool.Log.Info($"{DateTime.Now} MQTT 服务器已连接！\n");
                        //mqttClient.Subscribe("command/topic");
                    };

                    _mqttClient.Disconnected += () => { Tool.Log.Info($"{DateTime.Now} MQTT 服务器已断开！\n"); };

                    #endregion MQTT 连接配置

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string payloadJson;
                        var ret = ScreenTextCollect();
                        if (ret.ResultType == MethodResultType.Success)
                        {
                            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(ret.Message);
                            var telemetry = new Dictionary<string, object>
                            {
                                { "CLIENT", mqttBrokerConfig.ClientId },
                                { "DEVICECODE", Tool.Settings.DeviceName },
                                { "EQUIPMENT", Tool.Settings.DeviceName },
                                { "GROUPCODE", mqttBrokerConfig.GroupCode },
                                { "TIMESTAMP", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                            };
                            foreach (var item in data)
                            {
                                telemetry.Add(item.Key, item.Value);
                            }

                            payloadJson = JsonConvert.SerializeObject(telemetry);
                        }
                        else
                        {
                            payloadJson = ret.Message;
                        }

                        MqttConnect(_mqttClient, mqttBrokerConfig.Ip, mqttBrokerConfig.Port, cancellationToken);
                        //_mqttClient.Connect(mqttBrokerConfig.Ip, mqttBrokerConfig.Port, 30, true);
                        _mqttClient.Publish(mqttBrokerConfig.Topic, payloadJson);
                        Tool.Log.Info($"{DateTime.Now} 发布 MQTT 消息: {payloadJson}\n");

                        // 使用 WaitOne 实现可中断的等待
                        // 每秒检查一次取消信号，允许快速响应退出请求
                        int sleepIntervalMs = mqttBrokerConfig.CaptureFrequency * 1000;
                        int checkIntervalMs = 1000;
                        int elapsedMs = 0;

                        while (elapsedMs < sleepIntervalMs && !cancellationToken.IsCancellationRequested)
                        {
                            Thread.Sleep(checkIntervalMs);
                            elapsedMs += checkIntervalMs;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 线程被正常取消，忽略异常
                    Tool.Log.Info("MQTT 推送线程已收到取消信号");
                }
                catch (Exception ex)
                {
                    Tool.Log.Error($"{DateTime.Now} MQTT 推送服务异常: {ex}\n");
                }
                finally
                {
                    // 确保清理 MQTT 客户端
                    _mqttClient?.Disconnect();
                    _mqttClient?.Dispose();
                    _mqttClient = null;
                }
            }
            else
            {
                Tool.Log.Warn("配置文件中缺少必要的 MQTT 配置项。");
            }
        }

        private static void MqttConnect(MqttClient mqttClient, string mqttServerIp, int mqttServerPort, CancellationToken cancellationToken)
        {
            mqttClient.Connect(mqttServerIp, mqttServerPort);
            while (!mqttClient.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(10000);
                if (!cancellationToken.IsCancellationRequested) // 检查是否还在运行
                {
                    Tool.Log.Info("尝试重连MQTT ...");
                    mqttClient.Connect(mqttServerIp, mqttServerPort);
                }
            }
        }

        #endregion
    }
}