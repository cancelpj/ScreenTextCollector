using System.Collections.Generic;

namespace PluginInterface
{
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
        public string GroupCode { get; set; } = "collection";
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

        // ========== 图像预处理配置 ==========

        /// <summary>
        /// 是否启用 CLAHE 对比度增强（推荐，用于提高 OCR 识别率）
        /// </summary>
        public bool EnableClahe { get; set; } = true;

        /// <summary>
        /// CLAHE 对比度增强强度，值越大对比度增强越明显，推荐 2.0
        /// </summary>
        public double ClaheClipLimit { get; set; } = 2.0;

        /// <summary>
        /// CLAHE 局部块大小，推荐 8
        /// </summary>
        public int ClaheTileSize { get; set; } = 8;

        /// <summary>
        /// 是否启用自适应阈值二值化（用于处理不均匀光照）
        /// </summary>
        public bool EnableAdaptiveThreshold { get; set; } = true;

        /// <summary>
        /// 自适应阈值块大小，必须是奇数，推荐 11
        /// </summary>
        public int AdaptiveThresholdBlockSize { get; set; } = 11;

        /// <summary>
        /// 自适应阈值偏移量，推荐 2
        /// </summary>
        public int AdaptiveThresholdC { get; set; } = 2;

        /// <summary>
        /// 是否启用高斯模糊降噪
        /// </summary>
        public bool EnableGaussianBlur { get; set; } = false;

        /// <summary>
        /// 高斯模糊核大小，必须是奇数，推荐 3
        /// </summary>
        public int GaussianBlurKernelSize { get; set; } = 3;
    }
}
