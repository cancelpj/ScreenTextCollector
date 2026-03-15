using NLog;
using OpenCvSharp;
using OpenCvSharp.Text;
using PluginInterface;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenTextCollector.OpenCvSharp
{
    public class OcrService : IOcrService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 验证图像：使用模板匹配算法验证截图是否包含目标画面
        /// </summary>
        public bool VerifyImage(string screenShotPath, List<ImageVerificationArea> imageVerificationAreas)
        {
            try
            {
                // 截图文件不存在
                if (!File.Exists(screenShotPath))
                {
                    Log.Warn("截图文件不存在: {0}", screenShotPath);
                    return false;
                }

                using (Mat screenShot = Cv2.ImRead(screenShotPath))
                {
                    if (screenShot.Empty())
                    {
                        Log.Warn("无法读取截图文件: {0}", screenShotPath);
                        return false;
                    }

                    // 逐个对比图像检测区域
                    foreach (var area in imageVerificationAreas)
                    {
                        var roi = new Rect(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                        var templatePath = Path.Combine("data", area.FileName);
                        if (!File.Exists(templatePath))
                        {
                            Log.Warn("模板文件不存在: {0}", templatePath);
                            return false;
                        }

                        using (Mat verificationImage = Cv2.ImRead(templatePath))
                        {
                            if (verificationImage.Empty())
                            {
                                Log.Warn("无法读取模板文件: {0}", templatePath);
                                return false;
                            }

                            using (Mat roiImage = screenShot[roi])
                            using (Mat matResult = new Mat())
                            {
                                Cv2.MatchTemplate(roiImage, verificationImage, matResult, TemplateMatchModes.CCoeffNormed);
                                Cv2.MinMaxLoc(matResult, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

                                // 只要有区域图像对比不通过，就认为未监测到程序画面
                                if (maxVal < area.MatchThreshold)
                                {
                                    Log.Debug("区域 {0} 匹配度 {1} 低于阈值 {2}", area.FileName, maxVal, area.MatchThreshold);
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "图像验证异常");
                return false;
            }
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
