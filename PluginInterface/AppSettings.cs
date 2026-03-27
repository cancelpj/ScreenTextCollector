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

        public string Ip { get; set; } = "127.0.0.1";
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

        /// <summary>
        /// 全局默认 Topic
        /// </summary>
        public MqttTopicConfig DefaultTopic { get; set; }

        /// <summary>
        /// Topic 列表（优先级高于 DefaultTopic）
        /// </summary>
        public List<MqttTopicConfig> Topics { get; set; }

        /// <summary>
        /// 根据 Topic 名称查找对应的配置
        /// </summary>
        /// <param name="topicName">Topic 名称（Name）</param>
        /// <returns>找到的配置，未找到返回 null</returns>
        public MqttTopicConfig FindTopicConfig(string topicName)
        {
            if (Topics == null || string.IsNullOrEmpty(topicName))
                return null;

            foreach (var tc in Topics)
            {
                if (tc.Name == topicName)
                    return tc;
            }
            return null;
        }
    }

    /// <summary>
    /// MQTT Topic 配置
    /// </summary>
    public class MqttTopicConfig
    {
        /// <summary>
        /// Topic 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 该 Topic 的 ExtendPayload（可选，不填则使用全局 ExtendPayload）
        /// </summary>
        public Dictionary<string, string> ExtendPayload { get; set; }
    }
}
