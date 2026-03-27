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
            if (!string.IsNullOrEmpty(mqttBrokerConfig.Ip) && !string.IsNullOrEmpty(mqttBrokerConfig.DefaultTopic?.Name))
            {
                Tool.Log.Info(
                    $"{DateTime.Now} MQTT 推送服务已启动，服务器: {mqttBrokerConfig.Ip}, 主题: {mqttBrokerConfig.DefaultTopic?.Name}\n");
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
                        var collectRet = Tool.CaptureAndVerify(
                            OcrService.VerifyImage,
                            Tool.CaptureSettings.VerificationAreas);

                        if (collectRet.ResultType != MethodResultType.Success)
                        {
                            // 验证失败，跳过本次推送
                            Tool.Log.Warn($"{DateTime.Now} 采集异常，跳过本次推送: {collectRet.Message}\n");
                        }
                        else
                        {
                            var ocrRet = Tool.ProcessScreenCaptureWithTopic(collectRet.Message, OcrService.PerformOcr);

                            if (ocrRet.ResultType == MethodResultType.Success)
                            {
                                // 建立 MQTT 连接
                                MqttConnect(_mqttClient, mqttBrokerConfig.Ip, mqttBrokerConfig.Port, cancellationToken);

                                // 按 Topic 分组
                                var groupedData = GroupByTopic(ocrRet.Data.Data, ocrRet.Data.TopicMap, mqttBrokerConfig.DefaultTopic?.Name);

                                foreach (var group in groupedData)
                                {
                                    string topic = group.Key;
                                    var data = group.Value;

                                    // 构建遥测数据
                                    var telemetry = new Dictionary<string, object>
                                    {
                                        { "TIMESTAMP", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                                    };
                                    foreach (var item in data)
                                    {
                                        telemetry[item.Key] = item.Value;
                                    }

                                    // 添加 DefaultTopic 的扩展字段（Topic 级配置会覆盖同名键）
                                    if (mqttBrokerConfig.DefaultTopic != null)
                                        MergeExtendPayload(telemetry, mqttBrokerConfig.DefaultTopic.ExtendPayload);
                                    // Topic 级扩展字段（优先级更高）
                                    var topicConfig = mqttBrokerConfig.FindTopicConfig(topic);
                                    if (topicConfig != null)
                                        MergeExtendPayload(telemetry, topicConfig.ExtendPayload);

                                    string payloadJson = JsonConvert.SerializeObject(telemetry);
                                    _mqttClient.Publish(topic, payloadJson);
                                    Tool.Log.Info($"{DateTime.Now} 发布 MQTT 消息到 [{topic}]: {payloadJson}\n");
                                }
                            }
                            else
                            {
                                // OCR 失败，跳过本次推送
                                Tool.Log.Warn($"{DateTime.Now} OCR 失败，跳过本次推送: {ocrRet.Message}\n");
                            }
                        }

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

        /// <summary>
        /// 按 Topic 分组采集数据
        /// </summary>
        /// <param name="data">采集结果数据</param>
        /// <param name="topicMap">区域名称到自定义 Topic 的映射</param>
        /// <param name="defaultTopic">默认全局 Topic</param>
        /// <returns>按 Topic 分组的结果（Topic -> 数据字典）</returns>
        private static Dictionary<string, Dictionary<string, string>> GroupByTopic(
            Dictionary<string, string> data,
            Dictionary<string, string> topicMap,
            string defaultTopic)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            foreach (var item in data)
            {
                string topic = !string.IsNullOrEmpty(item.Key) && topicMap.TryGetValue(item.Key, out var customTopic)
                    ? customTopic ?? defaultTopic
                    : defaultTopic;

                if (!result.TryGetValue(topic, out var group))
                {
                    group = new Dictionary<string, string>();
                    result[topic] = group;
                }
                group[item.Key] = item.Value;
            }

            return result;
        }

        /// <summary>
        /// 将 ExtendPayload 合并到遥测数据字典
        /// </summary>
        private static void MergeExtendPayload(Dictionary<string, object> telemetry, Dictionary<string, string> extendPayload)
        {
            if (extendPayload == null) return;
            foreach (var item in extendPayload)
            {
                telemetry[item.Key] = item.Value;
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