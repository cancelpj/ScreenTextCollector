using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using PluginInterface;
using PaddleOCRSharp;

namespace ScreenTextCollector.PaddleOCR
{
    public class OcrService : IOcrService
    {
        /// <summary>
        /// 验证图像
        /// </summary>
        public bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            using (var screenShot = new Bitmap(screenShotPath))
            {
                // 逐个对比图像检测区域
                foreach (var area in imageVerificationAreas)
                {
                    using (Bitmap validationArea = screenShot.Clone(
                               new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height),
                               PixelFormat.Format24bppRgb))
                    {
                        var path = Path.Combine("data", area.FileName);
                        using (var expectedImage = new Bitmap(path))
                        {
                            var matchThreshold = CompareImages(validationArea, expectedImage);
                            // 只要有区域图像对比不通过，就认为未监测到程序画面
                            if (matchThreshold < area.MatchThreshold) return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 比较两张图片的相似度
        /// </summary>
        private static float CompareImages(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
            {
                return 0;
            }

            int totalPixels = img1.Width * img1.Height;
            int matchingPixels = 0;

            for (int y = 0; y < img1.Height; y++)
            {
                for (int x = 0; x < img1.Width; x++)
                {
                    Color pixel1 = img1.GetPixel(x, y);
                    Color pixel2 = img2.GetPixel(x, y);

                    if (pixel1 == pixel2)
                    {
                        matchingPixels++;
                    }
                }
            }

            return (float)matchingPixels / totalPixels;
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
                    return result.Text.Trim();
                }
            }
        }
    }
}
