using OpenCvSharp;
using OpenCvSharp.Text;
using System;
using System.Collections.Generic;
using System.IO;
using PluginInterface;

namespace ScreenTextCollector.OpenCvSharp
{
    public class OcrService : IOcrService
    {
        public bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath))
            {
                // 截图文件不存在
                if (screenShot.Empty())
                {
                    return false;
                }

                // 逐个对比图像检测区域
                foreach (var area in imageVerificationAreas)
                {
                    var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                    var path = Path.Combine("data", area.FileName);
                    using (Mat verificationImage = Cv2.ImRead(path))
                    {
                        // 模板文件不存在，跳过此区域
                        if (verificationImage.Empty())
                        {
                            return false;
                        }

                        using (Mat roiImage = screenShot[roi])
                        using (Mat matResult = new Mat())
                        {
                            Cv2.MatchTemplate(roiImage, verificationImage, matResult, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(matResult, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                            if (maxVal < area.MatchThreshold) return false; // 只要有区域图像对比不通过，就认为未监测到程序画面
                        }
                    }
                }
            }

            return true;
        }

        public string PerformOcr(string screenShotPath, ImageCollectionArea area)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath, ImreadModes.Color))
            {
                if (screenShot.Empty())
                {
                    return string.Empty;
                }

                var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                var trainedDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data/");
                using (var ocr = OCRTesseract.Create(
                           datapath: trainedDataPath,
                           language: "eng",
                           //charWhitelist: "0123456789", // 只允许识别数字
                           oem: 3,
                           psmode: 3))
                using (Mat roiImage = screenShot[roi])
                using (Mat gray = new Mat())
                {
                    // 将彩色图像转换为灰度图像
                    Cv2.CvtColor(roiImage, gray, ColorConversionCodes.BGR2GRAY);
                    // OCR 识别
                    ocr.Run(gray, out string text, out _, out _, out _);
                    return text.Trim();
                }
            }
        }
    }
}
