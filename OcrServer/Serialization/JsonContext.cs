using System.Text.Json;
using System.Text.Json.Serialization;
using OcrServer.Configuration;

// DTO 类型必须在 JsonContext 之前定义（因为 JsonSerializable 属性需要引用这些类型）
namespace OcrServer.Serialization;

#region CaptureScreen HTTP 请求/响应 DTO

/// <summary>
/// POST /api/screenshot 请求体（新版：只传屏幕编号，返回完整屏幕截图）
/// </summary>
public sealed class CaptureScreenRequest
{
    public List<ScreenInfo> Screens { get; set; } = new();

    public sealed class ScreenInfo
    {
        public int ScreenIndex { get; set; }
    }
}

/// <summary>
/// POST /api/screenshot 响应体（新版：每屏幕返回一张完整截图）
/// </summary>
public sealed class CaptureScreenResponse
{
    public long Timestamp { get; set; }
    public List<ScreenResult> Screens { get; set; } = new();
}

/// <summary>
/// 单个屏幕的截图结果（完整屏幕，不含区域）
/// </summary>
public sealed class ScreenResult
{
    public int ScreenIndex { get; set; }
    public string Image { get; set; } = ""; // Base64 编码的完整屏幕 JPEG
}

#endregion

/// <summary>
/// MQTT 推送 Payload 结构（Data 和 ExtendPayload 合并到根层级，TIMESTAMP 全大写）
/// </summary>
public sealed class MqttPayload
{
    /// <summary>
    /// 根层级字段：TIMESTAMP、采集结果、扩展字段全部扁平化
    /// </summary>
    public Dictionary<string, string> Root { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 健康检查响应
/// </summary>
public sealed class HealthResponse
{
    public string Status { get; set; } = "ok";
    public long Timestamp { get; set; }
}

/// <summary>
/// System.Text.Json Source Generator 上下文
/// 为所有需要 JSON 序列化/反序列化的类型生成优化的序列化代码，
/// 兼容 Native AOT 编译
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HttpConfig))]
[JsonSerializable(typeof(MqttBrokerConfig))]
[JsonSerializable(typeof(MqttTopicConfig))]
[JsonSerializable(typeof(MqttReconnectConfig))]
[JsonSerializable(typeof(DeviceConfig))]
[JsonSerializable(typeof(CaptureSettings))]
[JsonSerializable(typeof(ImageVerificationArea))]
[JsonSerializable(typeof(ImageCollectionArea))]
[JsonSerializable(typeof(CaptureScreenRequest))]
[JsonSerializable(typeof(CaptureScreenResponse))]
[JsonSerializable(typeof(ScreenResult))]
[JsonSerializable(typeof(MqttPayload))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
public partial class JsonContext : JsonSerializerContext
{
}
