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
                // 逐个对比图像检测区域
                foreach (var area in imageVerificationAreas)
                {
                    var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                    var path = Path.Combine("data", area.FileName);
                    using (Mat verificationImage = Cv2.ImRead(path))
                    {
                        Mat roiImage = screenShot[roi];
                        Mat matResult = new Mat();
                        Cv2.MatchTemplate(roiImage, verificationImage, matResult, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(matResult, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                        if (maxVal < area.MatchThreshold) return false; // 只要有区域图像对比不通过，就认为未监测到程序画面
                    }
                }
            }

            return true;
        }

        public string PerformOcr(string screenShotPath, ImageCollectionArea area)
        {
            using (Mat screenShot = Cv2.ImRead(screenShotPath, ImreadModes.Color))
            {
                var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                var trainedDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data/");
                using (var ocr = OCRTesseract.Create(
                           datapath: trainedDataPath,
                           language: "eng",
                           //charWhitelist: "0123456789", // 只允许识别数字
                           oem: 3,
                           psmode: 3))
                {
                    Mat roiImage = screenShot[roi];
                    Mat gray = new Mat();
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