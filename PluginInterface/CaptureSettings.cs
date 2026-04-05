using System.Collections.Generic;

namespace PluginInterface
{
    /// <summary>
    /// 屏幕采集配置（从 CaptureSettings.json 加载）
    /// 由 LabelTool 生成和维护
    /// </summary>
    public class CaptureSettings
    {
        /// <summary>
        /// 图像验证区域列表
        /// </summary>
        public List<ImageVerificationArea> VerificationAreas { get; set; }

        /// <summary>
        /// 图像采集区域列表
        /// </summary>
        public List<ImageCollectionArea> CollectionAreas { get; set; }

        /// <summary>
        /// OCR 引擎类型：OpenCvSharp 或 PaddleOCR
        /// </summary>
        public string OcrEngine { get; set; } = "PaddleOCR";
    }

    /// <summary>
    /// 图像检测区域配置项
    /// </summary>
    public class ImageVerificationArea
    {
        /// <summary>
        /// 所属屏幕编号，从 0 开始
        /// </summary>
        public int ScreenNumber { get; set; }

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
        /// 所属屏幕编号，从 0 开始
        /// </summary>
        public int ScreenNumber { get; set; }

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

        /// <summary>
        /// 自定义 MQTT 推送主题（可选）
        /// 如果设置，则该采集项的结果会发布到此主题，优先级高于全局 Topic
        /// </summary>
        public string Topic { get; set; }
    }
}
