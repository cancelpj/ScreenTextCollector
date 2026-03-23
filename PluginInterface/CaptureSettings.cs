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
        /// 屏幕编号，从 0 开始
        /// </summary>
        public int ScreenNumber { get; set; } = 0;

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
}
