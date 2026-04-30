using OcrServer.Configuration;
using OcrServer.Serialization;
using OcrServer.Utilities;
using System.Text;
using System.Text.Json;
using ILogger = Serilog.ILogger;

namespace OcrServer.Services;

/// <summary>
/// HTTP 客户端，负责调用 CaptureScreen 服务的 /api/screenshot 接口
/// </summary>
public sealed class CaptureScreenClient : IDisposable
{
    private HttpClient _httpClient;
    private readonly DeviceConfig _deviceConfig;
    private readonly ILogger _logger;

    /// <summary>
    /// 测试用：注入 Mock HttpClient
    /// </summary>
    internal void SetHttpClient(HttpClient httpClient) => _httpClient = httpClient;

    public CaptureScreenClient(DeviceConfig deviceConfig, ILogger logger)
    {
        _deviceConfig = deviceConfig;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(deviceConfig.CaptureScreenUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(deviceConfig.TimeoutSeconds)
        };
    }

    /// <summary>
    /// 请求完整屏幕截图（每屏幕返回一张图）
    /// </summary>
    public async Task<Dictionary<int, string>> CaptureFullScreenAsync(
        List<int> screenIndices,
        CancellationToken cancellationToken = default)
    {
        var screens = screenIndices.Select(i => new CaptureScreenRequest.ScreenInfo { ScreenIndex = i }).ToList();
        var requestBody = new CaptureScreenRequest { Screens = screens };
        string json = JsonSerializer.Serialize(requestBody, JsonContext.Default.CaptureScreenRequest);
        string fullUrl = $"{_deviceConfig.CaptureScreenUrl.TrimEnd('/')}/api/screenshot";

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync(
                new Uri(fullUrl),
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.Warning("设备 {DeviceCode} HTTP 请求失败 [{StatusCode}]，Body: {Body}",
                    _deviceConfig.DeviceCode, (int)response.StatusCode, errorBody);
                throw new HttpRequestException($"设备 [{_deviceConfig.DeviceCode}] HTTP {(int)response.StatusCode}");
            }

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize(responseBody, JsonContext.Default.CaptureScreenResponse)
                ?? throw new InvalidDataException($"设备 [{_deviceConfig.DeviceCode}] 响应解析失败，返回数据为空");

            var screenshots = new Dictionary<int, string>();
            foreach (var screen in result.Screens)
                screenshots[screen.ScreenIndex] = screen.Image;
            return screenshots;
        }
        catch (Exception ex)
        {
            _logger.Warning(NetworkExceptionHelper.GetFriendlyMessage(ex, $"设备 {_deviceConfig.DeviceCode}"));
            throw;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
