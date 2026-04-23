using OcrServer.Configuration;
using OcrServer.Serialization;
using System.Text;
using System.Text.Json;

namespace OcrServer.Services;

/// <summary>
/// HTTP 客户端，负责调用 CaptureScreen 服务的 /api/screenshot 接口
/// </summary>
public sealed class CaptureScreenClient : IDisposable
{
    private HttpClient _httpClient;

    /// <summary>
    /// 测试用：注入 Mock HttpClient
    /// </summary>
    internal void SetHttpClient(HttpClient httpClient) => _httpClient = httpClient;
    private readonly DeviceConfig _deviceConfig;

    public CaptureScreenClient(DeviceConfig deviceConfig)
    {
        _deviceConfig = deviceConfig;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(deviceConfig.CaptureScreenUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(deviceConfig.TimeoutSeconds)
        };
    }

    /// <summary>
    /// 请求完整屏幕截图（每屏幕返回一张图）
    /// </summary>
    /// <param name="screenIndices">屏幕编号列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>screenIndex -> Base64 完整屏幕截图</returns>
    public async Task<Dictionary<int, string>> CaptureFullScreenAsync(
        List<int> screenIndices,
        CancellationToken cancellationToken = default)
    {
        var screens = screenIndices.Select(i => new CaptureScreenRequest.ScreenInfo { ScreenIndex = i }).ToList();
        var requestBody = new CaptureScreenRequest { Screens = screens };
        string json = JsonSerializer.Serialize(requestBody, JsonContext.Default.CaptureScreenRequest);

        string fullUrl = _deviceConfig.CaptureScreenUrl.TrimEnd('/') + "/api/screenshot";
        HttpResponseMessage response = await _httpClient.PostAsync(
                new Uri(fullUrl),
                new StringContent(json, Encoding.UTF8, "application/json"),
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"CaptureScreen [{_deviceConfig.DeviceCode}] HTTP {(int)response.StatusCode}: {errorBody}");
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize(responseBody, JsonContext.Default.CaptureScreenResponse)
            ?? throw new InvalidDataException("CaptureScreen response deserialized to null");

        // 解析为 screenIndex -> Base64
        var screenshots = new Dictionary<int, string>();
        foreach (var screen in result.Screens)
        {
            screenshots[screen.ScreenIndex] = screen.Image;
        }

        return screenshots;
    }

    public void Dispose() => _httpClient.Dispose();
}
