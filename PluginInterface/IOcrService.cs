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
    }
}