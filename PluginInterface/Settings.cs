using System.Collections.Generic;

namespace PluginInterface
{
    public class Settings
    {
        /// <summary>
        /// 采集点名称
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 本地 csv 文件记录
        /// </summary>
        public bool CsvRecord { get; set; }

        /// <summary>
        /// 是否启用HTTP服务
        /// </summary>
        public HttpConfig Http { get; set; }

        public MqttBrokerConfig MqttBroker { get; set; }

        /// <summary>
        /// 屏幕编号，从 0 开始
        /// </summary>
        public int ScreenNumber { get; set; } = 0;

        public List<ImageVerificationArea> ImageVerificationAreas { get; set; }
        public List<ImageCollectionArea> ImageCollectionAreas { get; set; }
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
    }

    /// <summary>
    /// 图像检测区域配置项
    /// </summary>
    public class ImageVerificationArea
    {
        public int TopLeftX { get; set; }
        public int TopLeftY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// 检测图片的文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 匹配度
        /// </summary>
        public float MatchThreshold { get; set; }
    }

    /// <summary>
    /// 图像采集区域配置项
    /// </summary>
    public class ImageCollectionArea
    {
        /// <summary>
        /// 采集项名称
        /// </summary>
        public string Name { get; set; }

        public int TopLeftX { get; set; }
        public int TopLeftY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}