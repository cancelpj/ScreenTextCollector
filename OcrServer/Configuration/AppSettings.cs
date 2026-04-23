using System.Collections.Generic;

namespace OcrServer.Configuration;

/// <summary>
/// 应用程序配置（从 appsettings.json 加载）
/// 基于 PluginInterface.AppSettings 扩展，新增多设备支持
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 本地 csv 文件记录
    /// </summary>
    public bool CsvRecord { get; set; }

    /// <summary>
    /// HTTP 服务配置
    /// </summary>
    public HttpConfig Http { get; set; } = new();

    /// <summary>
    /// MQTT 推送服务配置
    /// </summary>
    public MqttBrokerConfig MqttBroker { get; set; } = new();

    /// <summary>
    /// 设备列表（每个设备对应一台 CaptureScreen 实例）
    /// </summary>
    public List<DeviceConfig> Devices { get; set; } = new();
}

/// <summary>
/// HTTP 服务配置
/// </summary>
public class HttpConfig
{
    /// <summary>
    /// 是否启用 HTTP 服务
    /// </summary>
    public bool EnableHttp { get; set; } = true;

    /// <summary>
    /// 监听 IP
    /// </summary>
    public string Ip { get; set; } = "0.0.0.0";

    /// <summary>
    /// 监听端口
    /// </summary>
    public int Port { get; set; } = 8081;
}

/// <summary>
/// MQTT Broker 配置
/// </summary>
public class MqttBrokerConfig
{
    /// <summary>
    /// 是否启用 MQTT 推送
    /// </summary>
    public bool EnableMqttPush { get; set; } = false;

    /// <summary>
    /// Broker IP 地址
    /// </summary>
    public string Ip { get; set; } = "127.0.0.1";

    /// <summary>
    /// Broker 端口
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = "OcrServer";

    /// <summary>
    /// 用户名（可选）
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// 密码（可选）
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Topic 配置列表
    /// </summary>
    public List<MqttTopicConfig> Topics { get; set; } = new();

    /// <summary>
    /// 重连配置
    /// </summary>
    public MqttReconnectConfig Reconnect { get; set; } = new();
}

/// <summary>
/// MQTT 重连配置
/// </summary>
public class MqttReconnectConfig
{
    /// <summary>
    /// 首次重连等待时间（秒），默认 3 秒
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 3;

    /// <summary>
    /// 最大重连等待时间（秒），超过此时间后固定为该值，默认 60 秒
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 60;

    /// <summary>
    /// 重连最大重试次数，0 表示无限重试，默认 0
    /// </summary>
    public int MaxRetries { get; set; } = 0;
}

/// <summary>
/// MQTT Topic 配置（用于 MQTTnet 推送时携带额外 Payload）
/// </summary>
public class MqttTopicConfig
{
    /// <summary>
    /// Topic 名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 该 Topic 的额外 Payload 字段（与设备级 DefaultExtendPayload 合并）
    /// </summary>
    public Dictionary<string, string> ExtendPayload { get; set; } = new();
}
