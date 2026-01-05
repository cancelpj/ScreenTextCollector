using Newtonsoft.Json;
using PluginInterface;
using ScreenTextCollector.OpenCvSharp;
using SimpleMqttClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace ScreenTextCollector
{
    internal static class Program
    {
        private static bool _isRunning = true;
        private static readonly IOcrService OcrService = new OcrService();
        private static MqttClient _mqttClient = null;  // 保持全局引用便于清理
        private static HttpListener _listener = null;  // 保持全局引用便于清理

        static void Main(string[] args)
        {
            // 参数处理：支持任意顺序的参数
            bool isDebugMode = args.Contains("-debug", StringComparer.OrdinalIgnoreCase);

            if (isDebugMode)
            {
                Console.WriteLine("按任意键继续...");
                Console.ReadKey();
            }

            #region 启动时清空temp文件夹

            Tool.Log.Info("启动时清空 temp 文件夹");
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true); // 删除文件夹及其内容
            }

            #endregion
            #region MQTT推送功能

            var mqttBrokerConfig = Tool.Settings.MqttBroker;
            if (mqttBrokerConfig.EnableMqttPush)
            {
                if (!string.IsNullOrEmpty(mqttBrokerConfig.Ip) && !string.IsNullOrEmpty(mqttBrokerConfig.Topic))
                {
                    #region 用一个独立线程定时检查进程并推送 MQTT
                    new Thread(() =>
                        {
                            Tool.Log.Info($"{DateTime.Now} MQTT 推送服务已启动，服务器: {mqttBrokerConfig.Ip}, 主题: {mqttBrokerConfig.Topic}\n");
                            try
                            {
                                #region MQTT 连接配置
                                _mqttClient = new MqttClient(mqttBrokerConfig.ClientId);
                                if (!string.IsNullOrEmpty(mqttBrokerConfig.Username) && !string.IsNullOrEmpty(mqttBrokerConfig.Password))
                                    _mqttClient.SetCredentials(mqttBrokerConfig.Username, mqttBrokerConfig.Password);
                                
                                _mqttClient.Connected += () =>
                                {
                                    Tool.Log.Info($"{DateTime.Now} MQTT 服务器已连接！\n");
                                    //mqttClient.Subscribe("command/topic");
                                };

                                _mqttClient.Disconnected += () =>
                                {
                                    Tool.Log.Info($"{DateTime.Now} MQTT 服务器已断开！\n");
                                };
                                #endregion MQTT 连接配置

                                while (_isRunning)
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
                                            { "GROUPCODE", "collection" },
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

                                    MqttConnect(_mqttClient, mqttBrokerConfig.Ip, mqttBrokerConfig.Port);
                                    _mqttClient.Publish(mqttBrokerConfig.Topic, payloadJson);
                                    Tool.Log.Info($"{DateTime.Now} 发布 MQTT 消息: {payloadJson}\n");

                                    Thread.Sleep(mqttBrokerConfig.CaptureFrequency * 1000);
                                }
                            }
                            catch (Exception ex)
                            {
                                Tool.Log.Info($"{DateTime.Now} MQTT 推送服务异常: {ex}\n");
                            }
                            finally
                            {
                                // 确保清理 MQTT 客户端
                                _mqttClient?.Disconnect();
                                _mqttClient?.Dispose();
                                _mqttClient = null;
                            }

                        })
                    { IsBackground = true }.Start();
                    #endregion
                }
                else
                {
                    Tool.Log.Info("配置文件中缺少必要的 MQTT 配置项。");
                }
            }

            #endregion

            var httpConfig = Tool.Settings.Http;
            if (httpConfig.EnableHttp)
            {
                #region 启动 HTTP 服务

                //启动一个 HTTP 服务监听 HTTP GET 请求
                _listener = new HttpListener();
                var uri = $"http://{httpConfig.Ip}:{httpConfig.Port}/";
                _listener.Prefixes.Add(uri);
                _listener.Start();

                // 创建一个异步回调来处理请求
                _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);

                Tool.Log.Info($"{DateTime.Now} HTTP服务已启动，服务器: {uri}\n");

                #endregion 启动 HTTP 服务
            }

            Tool.Log.Info("按 Ctrl+C 键停止服务...");

            // 设置控制台事件处理
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _isRunning = false;
                
                // 立即停止监听
                if (_listener != null)
                {
                    try
                    {
                        _listener.Stop();
                        _listener.Close();
                    }
                    catch { }
                    finally
                    {
                        _listener = null;
                    }
                }
                
                // 断开 MQTT 连接
                if (_mqttClient != null)
                {
                    try
                    {
                        _mqttClient.Disconnect();
                    }
                    catch { }
                    finally
                    {
                        _mqttClient?.Dispose();
                        _mqttClient = null;
                    }
                }
                
                Tool.Log.Info("\n服务已停止");
            };

            // 保持服务运行
            while (_isRunning)
            {
                Thread.Sleep(100);
            }
        }

        static void MqttConnect(MqttClient mqttClient, string mqttServerIp, int mqttServerPort)
        {
            mqttClient.Connect(mqttServerIp, mqttServerPort);
            while (!mqttClient.IsConnected && _isRunning)
            {
                Thread.Sleep(10000);
                if (_isRunning)  // 检查是否还在运行
                {
                    mqttClient.Connect(mqttServerIp, mqttServerPort);
                }
            }
        }

        static void ListenerCallback(IAsyncResult result)
        {
            if (!_isRunning) return;

            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                
                response.StatusCode = 200;

                try
                {
                    // 只处理 GET 请求
                    if (request.HttpMethod == "GET")
                    {
                        // 根据请求的URL路径返回不同的响应
                        string responseString;
                        if (request.Url.AbsolutePath == "/health")
                        {
                            responseString = "ScreenTextCollector is alive.";
                        }
                        else if (request.Url.AbsolutePath.StartsWith("/process/"))
                        {
                            var processName = request.Url.AbsolutePath.Replace("/process/", "");
                            //按 processName 检查进程状态
                            responseString = CheckProcess(processName);
                        }
                        else if (request.Url.AbsolutePath == "/stc")
                        {
                            var ret = ScreenTextCollect();
                            responseString = ret.Message;
                            response.StatusCode = ret.ResultType == MethodResultType.Success ? 200 : 500;
                        }
                        else
                        {
                            responseString = "404 Not Found";
                            response.StatusCode = 404;
                        }

                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                }
                catch (Exception e)
                {
                    Tool.Log.Error($"{DateTime.Now} {e}\n");
                    response.StatusCode = 500;
                }
                finally
                {
                    // 手动关闭响应流
                    try
                    {
                        response.OutputStream.Close();
                        response.Close();
                    }
                    catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                // 监听器已被关闭，正常退出
                return;
            }
            catch (Exception e)
            {
                Tool.Log.Error($"{DateTime.Now} ListenerCallback 异常: {e}\n");
            }
            finally
            {
                // 继续监听下一个请求（如果仍在运行）
                if (_isRunning && _listener != null)
                {
                    try
                    {
                        _listener.BeginGetContext(ListenerCallback, _listener);
                    }
                    catch { }
                }
            }
        }

        private static MethodResult ScreenTextCollect()
        {
            var ret = Tool.GetScreenCapture();
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
                return ret;
            }

            ret = Tool.ProcessScreenCapture(ret.Message, OcrService.VerifyImage, OcrService.PerformOcr);
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
            }

            return ret;
        }

        /// <summary>
        /// 按 processName 检查进程状态
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static string CheckProcess(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0 ? "Running" : "Standby";
            }
            finally
            {
                // 释放进程句柄
                foreach (var p in processes)
                {
                    p?.Dispose();
                }
            }
        }
    }
}
