using PaddleOCRSharp;
using PluginInterface;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace ScreenTextCollector.PaddleOCR
{
    public class OcrService : IOcrService
    {
        /// <summary>
        /// 验证图像：调用 Tool 静态方法
        /// </summary>
        public bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            return Tool.VerifyImage(screenShotPath, imageVerificationAreas);
        }

        /// <summary>
        /// 执行 OCR
        /// </summary>
        public string PerformOcr(string screenShotPath, ImageCollectionArea area)
        {
            using (var screenShot = new Bitmap(screenShotPath))
            {
                using (Bitmap image = screenShot.Clone(
                           new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height),
                           PixelFormat.Format24bppRgb))
                {
                    var engine = new PaddleOCREngine();
                    OCRResult result = engine.DetectText(image);
                    // 后处理：清理和规范化 OCR 结果
                    return PostProcessText(result.Text.Trim(), area);
                }
            }
        }

        /// <summary>
        /// 后处理 OCR 识别结果：调用 Tool 静态方法
        /// </summary>
        public string PostProcessText(string text, ImageCollectionArea area)
        {
            return Tool.PostProcessText(text, area);
        }
    }
}
