namespace PluginInterface
{
    /// <summary>
    /// 应用程序配置（从 appsettings.json 加载）
    /// 包含设备名称、记录设置、HTTP服务、MQTT推送等配置
    /// </summary>
    public class AppSettings
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
        /// HTTP服务配置
        /// </summary>
        public HttpConfig Http { get; set; }

        /// <summary>
        /// MQTT推送服务配置
        /// </summary>
        public MqttBrokerConfig MqttBroker { get; set; }

        /// <summary>
        /// OCR 引擎类型：OpenCvSharp 或 PaddleOCR
        /// </summary>
        public string OcrEngine { get; set; } = "OpenCvSharp";
    }
}
