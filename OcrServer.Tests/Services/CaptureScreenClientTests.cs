using System;
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
    /// CaptureScreenClient 测试：验证 HTTP 请求构造和响应解析
    /// 新版：请求只含屏幕编号，响应返回每屏幕完整截图
    /// </summary>
    public class CaptureScreenClientTests
    {
        /// <summary>
        /// 用例：正常截图请求（只传屏幕编号），响应解析正确
        /// </summary>
        [Fact]
        public async Task CaptureFullScreenAsync_ValidResponse_ReturnsCorrectDictionary()
        {
            // Arrange：Mock HTTP 返回有效响应（每屏幕一张完整截图）
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = Convert.ToBase64String(new byte[100]) },
                            new ScreenResult { ScreenIndex = 1, Image = Convert.ToBase64String(new byte[200]) }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                });

            var deviceConfig = new DeviceConfig
            {
                DeviceCode = "TEST-001",
                CaptureScreenUrl = "http://192.168.1.50:8080",
                TimeoutSeconds = 10
            };

            var client = new CaptureScreenClient(deviceConfig);
            client.SetHttpClient(new HttpClient(mockHandler.Object) { BaseAddress = new Uri("http://192.168.1.50:8080") });

            var screenIndices = new List<int> { 0, 1 };

            // Act
            var response = await client.CaptureFullScreenAsync(screenIndices, CancellationToken.None);

            // Assert 1：返回字典包含所有屏幕
            Assert.NotNull(response);
            Assert.Equal(2, response.Count);
            Assert.True(response.ContainsKey(0));
            Assert.True(response.ContainsKey(1));
            Assert.NotEmpty(response[0]);
            Assert.NotEmpty(response[1]);

            // Assert 2：SendAsync 被调用
            mockHandler.Protected().Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.PathAndQuery.Contains("/api/screenshot")),
                ItExpr.IsAny<CancellationToken>());

            client.Dispose();
        }

        /// <summary>
        /// 用例：服务端返回 5xx，抛出 HttpRequestException
        /// </summary>
        [Fact]
        public async Task CaptureFullScreenAsync_ServerUnavailable_ThrowsException()
        {
            // Arrange：Mock HTTP 返回 500
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                });

            var deviceConfig = new DeviceConfig
            {
                DeviceCode = "TEST-001",
                CaptureScreenUrl = "http://192.168.1.50:8080",
                TimeoutSeconds = 10
            };

            var client = new CaptureScreenClient(deviceConfig);
            client.SetHttpClient(new HttpClient(mockHandler.Object) { BaseAddress = new Uri("http://192.168.1.50:8080") });

            var screenIndices = new List<int> { 0 };

            // Act & Assert：5xx 抛出 HttpRequestException
            var ex = await Assert.ThrowsAsync<HttpRequestException>(
                () => client.CaptureFullScreenAsync(screenIndices, CancellationToken.None));
            Assert.Contains("TEST-001", ex.Message);
            Assert.Contains("500", ex.Message);

            client.Dispose();
        }

        /// <summary>
        /// 用例：多屏幕请求
        /// </summary>
        [Fact]
        public async Task CaptureFullScreenAsync_MultiScreen_ReturnsAllScreens()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new CaptureScreenResponse
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Screens = new List<ScreenResult>
                        {
                            new ScreenResult { ScreenIndex = 0, Image = "SCREEN0_DATA" },
                            new ScreenResult { ScreenIndex = 1, Image = "SCREEN1_DATA" }
                        }
                    }, JsonContext.Default.CaptureScreenResponse))
                });

            var client = new CaptureScreenClient(new DeviceConfig
            {
                DeviceCode = "TEST",
                CaptureScreenUrl = "http://localhost:8080",
                TimeoutSeconds = 10
            });
            client.SetHttpClient(new HttpClient(mockHandler.Object) { BaseAddress = new Uri("http://localhost:8080") });

            var screenIndices = new List<int> { 0, 1 };

            // Act
            var response = await client.CaptureFullScreenAsync(screenIndices, CancellationToken.None);

            // Assert
            Assert.Equal(2, response.Count);
            Assert.Equal("SCREEN0_DATA", response[0]);
            Assert.Equal("SCREEN1_DATA", response[1]);

            client.Dispose();
        }
    }
}
