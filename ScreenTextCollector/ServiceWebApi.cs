using PluginInterface;
using System;
using System.Net;
using System.Text;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        private const string collectEndpoint = "/collect";
        #region HTTP 服务

        private static void StartHttpServer(HttpConfig httpConfig)
        {
            //启动一个 HTTP 服务监听 HTTP GET 请求
            _listener = new HttpListener();
            var uri = $"http://{httpConfig.Ip}:{httpConfig.Port}/";
            _listener.Prefixes.Add(uri);
            _listener.Start();

            // 创建一个异步回调来处理请求
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);

            Tool.Log.Info($"{DateTime.Now} HTTP服务已启动，服务器: {uri}\n");
        }

        private static void ListenerCallback(IAsyncResult result)
        {
            if (!_isRunning) return;

            try
            {
                HttpListener listener = (HttpListener)result.AsyncState;
                HttpListenerContext context = listener.EndGetContext(result);
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                response.StatusCode = 200;
                response.ContentType = "application/json; charset=utf-8";

                try
                {

                    // 只处理 GET 请求
                    if (request.HttpMethod == "GET")
                    {
                        var url = request.Url.AbsolutePath.TrimEnd();

                        // 根据请求的URL路径返回不同的响应
                        string responseString;
                        if (url == "/health" || url == "/health/")
                        {
                            responseString = "ScreenTextCollector is alive.";
                        }
                        else if (url.StartsWith("/process/"))
                        {
                            var processName = url.Replace("/process/", "");
                            //按 processName 检查进程状态
                            responseString = CheckProcess(processName);
                        }
                        else if (url == collectEndpoint || url == $"{collectEndpoint}/")
                        {
                            // 触发所有区域采集
                            var ret = ScreenTextCollect();
                            responseString = ret.Message;
                            response.StatusCode = ret.ResultType == MethodResultType.Success ? 200 : 500;
                        }
                        else if (url.StartsWith($"{collectEndpoint}/"))
                        {
                            // 触发单个区域采集
                            var areaName = Uri.UnescapeDataString(url.Replace($"{collectEndpoint}/", ""));
                            var ret = ScreenTextCollect(areaName);
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
                    catch
                    {
                    }
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
                    catch
                    {
                    }
                }
            }
        }

        #endregion HTTP 服务
    }
}