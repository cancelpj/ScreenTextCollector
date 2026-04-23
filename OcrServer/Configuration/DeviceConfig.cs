namespace OcrServer.Configuration;

/// <summary>
/// 单台设备配置（对应一台 CaptureScreen 实例）
/// </summary>
public class DeviceConfig
{
    /// <summary>
    /// 设备编码，用于结果隔离和日志标识
    /// </summary>
    public string DeviceCode { get; set; } = "";

    /// <summary>
    /// CaptureScreen 服务地址（例：http://192.168.8.128:2333）
    /// </summary>
    public string CaptureScreenUrl { get; set; } = "";

    /// <summary>
    /// HTTP 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 采集频率（秒），两次采集之间的间隔
    /// </summary>
    public int CaptureFrequency { get; set; } = 5;

    /// <summary>
    /// 设备级默认 Payload，MQTT 推送时会与 Topic.ExtendPayload 合并
    /// </summary>
    public Dictionary<string, string> DefaultExtendPayload { get; set; } = new();

    /// <summary>
    /// 该设备可用的 Topic 列表，Topics[0] 为默认值
    /// </summary>
    public List<MqttTopicConfig> Topics { get; set; } = new();
}
