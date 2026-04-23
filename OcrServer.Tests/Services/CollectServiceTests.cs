using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using OcrServer.Configuration;
using OcrServer.Serialization;
using OcrServer.Services;
using Xunit;

namespace OcrServer.Tests.Services
{
    /// <summary>
    /// CollectService 测试：验证多设备并发采集和结果缓存隔离
    /// 新版：CaptureScreen 返回每屏幕完整截图
    /// </summary>
    public class CollectServiceTests
    {
        /// <summary>
        /// 用例：多设备并发采集，结果按 DeviceCode 隔离
        /// 模拟 DEVICE-001 和 DEVICE-002 同时采集，验证结果互不干扰
        /// </summary>
        [Fact]
        public async Task CollectAsync_MultiDevice_ResultsAreIsolated()
        {
            // Arrange：两台设备的 Mock HTTP 响应（每屏幕一张完整截图）
            var device001Called = new ConcurrentBag<string>();
            var device002Called = new ConcurrentBag<string>();

            var mock001Handler = CreateMockHandler((req, _) =>
            {
                device001Called.Add(req.RequestUri.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = Convert.ToBase64String(new byte[50]) }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                });
            });

            var mock002Handler = CreateMockHandler((req, _) =>
            {
                device002Called.Add(req.RequestUri.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = Convert.ToBase64String(new byte[50]) }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                });
            });

            // 使用真实 ResultCache
            var cache = new ResultCache();

            var client001 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.50:8080", mock001Handler);
            var client002 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.51:8080", mock002Handler);

            // Act：并发调用两台设备的截图接口
            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    var screenshots = await client001.CaptureFullScreenAsync(
                        new List<int> { 0 },
                        CancellationToken.None);
                    cache.Set("DEVICE-001", "V001", "12.5");
                }),
                Task.Run(async () =>
                {
                    var screenshots = await client002.CaptureFullScreenAsync(
                        new List<int> { 0 },
                        CancellationToken.None);
                    cache.Set("DEVICE-002", "V001", "8.1");
                })
            };

            await Task.WhenAll(tasks);

            // Assert：两台设备的结果独立存储，互不覆盖
            Assert.Equal("12.5", cache.Get("DEVICE-001", "V001"));
            Assert.Equal("8.1", cache.Get("DEVICE-002", "V001"));
            Assert.NotEqual(cache.Get("DEVICE-001", "V001"), cache.Get("DEVICE-002", "V001"));

            client001.Dispose();
            client002.Dispose();
        }

        /// <summary>
        /// 用例：DEVICE-001 HTTP 错误，DEVICE-002 正常返回
        /// 验证：单设备失败不影响其他设备采集
        /// </summary>
        [Fact]
        public async Task CollectAsync_Device001Fails_Device002StillWorks()
        {
            // Arrange：DEVICE-001 返回 500，DEVICE-002 正常
            var device002Completed = new TaskCompletionSource<bool>();

            var errorHandler = CreateMockHandler((_, __) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server Error")
                }));

            var successHandler = CreateMockHandler((_, __) =>
            {
                device002Completed.TrySetResult(true);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = Convert.ToBase64String(new byte[50]) }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                });
            });

            var cache = new ResultCache();
            var device001Failed = false;

            var client001 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.50:8080", errorHandler);
            var client002 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.51:8080", successHandler);

            // Act：并发采集两个设备，DEVICE-001 捕获异常
            var device001Task = Task.Run(async () =>
            {
                try
                {
                    await client001.CaptureFullScreenAsync(new List<int> { 0 }, CancellationToken.None);
                }
                catch (HttpRequestException)
                {
                    device001Failed = true;
                }
            });

            var device002Task = Task.Run(async () =>
            {
                var screenshots = await client002.CaptureFullScreenAsync(new List<int> { 0 }, CancellationToken.None);
                Assert.True(screenshots.ContainsKey(0));
                Assert.NotEmpty(screenshots[0]);
                cache.Set("DEVICE-002", "V001", "OCR_RESULT_FROM_DEVICE002");
            });

            await Task.WhenAll(device001Task, device002Task);

            // Assert：DEVICE-001 失败，DEVICE-002 成功，缓存结果正确
            Assert.True(device001Failed, "DEVICE-001 的 HTTP 错误应被触发");
            Assert.True(device002Completed.Task.IsCompleted, "DEVICE-002 应成功完成");
            Assert.Equal("OCR_RESULT_FROM_DEVICE002", cache.Get("DEVICE-002", "V001"));

            client001.Dispose();
            client002.Dispose();
        }

        /// <summary>
        /// 验证：多设备并发时，DEVICE-001 的 HTTP 错误不影响 DEVICE-002
        /// </summary>
        [Fact]
        public async Task CollectAsync_Device001HttpError_Device002StillSucceeds()
        {
            // Arrange
            var errorHandler = CreateMockHandler((_, __) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server Error")
                }));

            var successHandler = CreateMockHandler((_, __) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = Convert.ToBase64String(new byte[50]) }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                }));

            var cache = new ResultCache();
            var errorOccurred = false;

            var client001 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.50:8080", errorHandler);
            var client002 = CreateCaptureScreenClientWithMockHttp("http://192.168.1.51:8080", successHandler);

            // Act：并发采集两个设备
            var tasks = new Task[2];

            tasks[0] = Task.Run(async () =>
            {
                try
                {
                    await client001.CaptureFullScreenAsync(new List<int> { 0 }, CancellationToken.None);
                }
                catch (HttpRequestException)
                {
                    errorOccurred = true;
                }
            });

            tasks[1] = Task.Run(async () =>
            {
                var screenshots = await client002.CaptureFullScreenAsync(new List<int> { 0 }, CancellationToken.None);
                Assert.True(screenshots.ContainsKey(0));
                Assert.NotEmpty(screenshots[0]);
                cache.Set("DEVICE-002", "V003", "OCR_RESULT_FROM_DEVICE002");
            });

            await Task.WhenAll(tasks);

            // Assert
            Assert.True(errorOccurred, "DEVICE-001 的 HTTP 错误应被触发");
            Assert.Equal("OCR_RESULT_FROM_DEVICE002", cache.Get("DEVICE-002", "V003"));

            client001.Dispose();
            client002.Dispose();
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private static HttpMessageHandler CreateMockHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            var mock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            mock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(responseFactory);
            return mock.Object;
        }

        private static CaptureScreenClient CreateCaptureScreenClientWithMockHttp(
            string captureScreenUrl, HttpMessageHandler mockHandler)
        {
            var deviceConfig = new DeviceConfig
            {
                DeviceCode = "TEST",
                CaptureScreenUrl = captureScreenUrl,
                TimeoutSeconds = 10
            };

            var client = new CaptureScreenClient(deviceConfig);
            client.SetHttpClient(new HttpClient(mockHandler) { BaseAddress = new Uri(captureScreenUrl) });
            return client;
        }
    }
}
