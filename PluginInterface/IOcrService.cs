using System.Collections.Generic;

namespace PluginInterface
{
    public interface IOcrService
    {
        /// <summary>
        /// 验证图像
        /// </summary>
        /// <param name="screenShotPath">截图路径</param>
        /// <param name="imageVerificationAreas">验证区域</param>
        /// <returns></returns>
        bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas);

        /// <summary>
        /// 执行 OCR
        /// </summary>
        /// <param name="screenShotPath">截图路径</param>
        /// <param name="imageCollectionArea">采集区域</param>
        /// <returns></returns>
        string PerformOcr(string screenShotPath, ImageCollectionArea imageCollectionArea);

        /// <summary>
        /// 后处理 OCR 识别结果
        /// </summary>
        /// <param name="text">OCR 原始文本</param>
        /// <param name="area">采集区域配置</param>
        /// <returns>处理后的文本</returns>
        string PostProcessText(string text, ImageCollectionArea area);
    }
}