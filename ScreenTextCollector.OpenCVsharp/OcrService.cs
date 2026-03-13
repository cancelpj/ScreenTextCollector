using OpenCvSharp;
using OpenCvSharp.Text;
using PluginInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ScreenTextCollector.OpenCvSharp
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

                // 尝试多种 PSM 模式
                int[] psmodes = { 6, 3, 11 };

                foreach (int psmode in psmodes)
                {
                    // 尝试多种语言模型
                    string[] languages = { "eng+chi_sim", "chi_sim", "eng" };

                    foreach (var lang in languages)
                    {
                        try
                        {
                            using (var ocr = OCRTesseract.Create(
                                       datapath: trainedDataPath,
                                       language: lang,
                                       oem: 3,
                                       psmode: psmode))
                            using (Mat roiImage = screenShot[roi])
                            using (Mat gray = new Mat())
                            {
                                // 将彩色图像转换为灰度图像
                                Cv2.CvtColor(roiImage, gray, ColorConversionCodes.BGR2GRAY);

                                // 图像预处理
                                using (Mat preprocessed = PreprocessImage(gray, area))
                                {
                                    // 对于小区域，放大图像以提高识别率
                                    Mat imageForOcr = preprocessed;
                                    bool needDispose = false;

                                    if (area.Width < 50 || area.Height < 15)
                                    {
                                        // 放大 2 倍
                                        Mat scaled = new Mat();
                                        Cv2.Resize(preprocessed, scaled, new Size(0, 0), 2.0, 2.0, InterpolationFlags.Cubic);
                                        imageForOcr = scaled;
                                        needDispose = true;
                                    }

                                    // OCR 识别
                                    ocr.Run(imageForOcr, out string text, out _, out _, out _);

                                    if (needDispose)
                                    {
                                        imageForOcr.Dispose();
                                    }

                                    // 后处理：清理和规范化 OCR 结果
                                    text = PostProcessText(text, area);

                                    // 如果有有效结果，返回它
                                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 0)
                                    {
                                        return text.Trim();
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // 如果这个组合失败，尝试下一个
                            continue;
                        }
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// 后处理 OCR 识别结果：调用 Tool 静态方法
        /// </summary>
        public string PostProcessText(string text, ImageCollectionArea area)
        {
            return Tool.PostProcessText(text, area);
        }

        /// <summary>
        /// 图像预处理：增强对比度、二值化、降噪
        /// </summary>
        private Mat PreprocessImage(Mat grayImage, ImageCollectionArea area)
        {
            Mat result = grayImage.Clone();

            // 1. CLAHE 对比度增强
            if (area.EnableClahe)
            {
                using (var clahe = Cv2.CreateCLAHE(area.ClaheClipLimit,
                    new Size(area.ClaheTileSize, area.ClaheTileSize)))
                {
                    Mat enhanced = new Mat();
                    clahe.Apply(result, enhanced);
                    result.Dispose();
                    result = enhanced;
                }
            }

            // 2. 高斯模糊降噪
            if (area.EnableGaussianBlur)
            {
                int kernelSize = area.GaussianBlurKernelSize;
                if (kernelSize % 2 == 0) kernelSize += 1;
                if (kernelSize < 3) kernelSize = 3;

                Mat blurred = new Mat();
                Cv2.GaussianBlur(result, blurred, new Size(kernelSize, kernelSize), 0);
                result.Dispose();
                result = blurred;
            }

            // 3. 自适应阈值二值化
            if (area.EnableAdaptiveThreshold)
            {
                int blockSize = area.AdaptiveThresholdBlockSize;
                if (blockSize % 2 == 0) blockSize += 1;
                if (blockSize < 3) blockSize = 3;

                Mat binary = new Mat();
                Cv2.AdaptiveThreshold(result, binary, 255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary,
                    blockSize,
                    area.AdaptiveThresholdC);
                result.Dispose();
                result = binary;
            }

            return result;
        }
    }
}
