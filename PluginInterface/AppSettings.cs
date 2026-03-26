using System.Collections.Generic;

namespace PluginInterface
{
    /// <summary>
    /// 应用程序配置（从 appsettings.json 加载）
    /// 包含设备名称、记录设置、HTTP服务、MQTT推送等配置
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 本地 csv 文件记录
        /// </summary>
        public bool CsvRecord { get; set; }

        /// <summary>
        /// HTTP服务配置
        /// </summary>
        public HttpConfig Http { get; set; }

        /// <summary>
        /// MQTT推送服务配置
        /// </summary>
        public MqttBrokerConfig MqttBroker { get; set; }
    }

    public class HttpConfig
    {
        /// <summary>
        /// 是否启用HTTP服务
        /// </summary>
        public bool EnableHttp { get; set; } = true;

        public string Ip { get; set; } = "+";
        public int Port { get; set; } = 80;
    }

    public class MqttBrokerConfig
    {
        /// <summary>
        /// 是否启用MQTT推送
        /// </summary>
        public bool EnableMqttPush { get; set; } = false;

        /// <summary>
        /// 采集频率，单位：毫秒
        /// </summary>
        public int CaptureFrequency { get; set; }

        public string Ip { get; set; }
        public int Port { get; set; } = 1883;
        public string ClientId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Topic { get; set; }

        /// <summary>
        /// MQTT 扩展 Payload 字段，用于向发送的 payload 添加额外的键值对
        /// </summary>
        public Dictionary<string, string> ExtendPayload { get; set; }
    }
}
