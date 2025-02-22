using System.Collections.Generic;
using OpenCvSharp;

namespace ScreenCaptureAgent
{
    public class Config
    {
        /// <summary>
        /// 采集点名称
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 采集频率，单位：毫秒
        /// </summary>
        public int CollectionFrequency { get; set; }

        /// <summary>
        /// 本地 csv 文件记录
        /// </summary>
        public bool CsvRecord { get; set; }


        public MqttBrokerConfig MqttBroker { get; set; }
        public ImageVerificationArea ImageVerificationArea { get; set; }
        public List<ImageCollectionArea> ImageCollectionAreas { get; set; }
    }

    public class MqttBrokerConfig
    {
        public string Ip { get; set; }
        public string ClientId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int CacheDays { get; set; }
    }

    /// <summary>
    /// 图像检测区域配置项
    /// </summary>
    public class ImageVerificationArea
    {
        /// <summary>
        /// 坐标
        /// </summary>
        public List<Point> Vertices { get; set; }

        /// <summary>
        /// 检测图片的文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 匹配度
        /// </summary>
        public double MatchDegree { get; set; }
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

        /// <summary>
        /// 坐标
        /// </summary>
        public List<Point> Vertices { get; set; }
    }
}