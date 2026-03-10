using OpenCvSharp;
using OpenCvSharp.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                                    text = CleanOcrResult(text, area);

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
        /// 清理 OCR 识别结果
        /// </summary>
        private string CleanOcrResult(string text, ImageCollectionArea area)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // 移除常见 OCR 错误字符
            text = text.Replace("|", "")
                       .Replace("_", "")
                       .Replace("—", "-")
                       .Replace("'", "")
                       .Replace("`", "")
                       .Replace("°", "")
                       .Replace("(", "")
                       .Replace(")", "")
                       .Replace("[", "")
                       .Replace("]", "")
                       .Replace("{", "")
                       .Replace("}", "");

            // 尝试匹配有效的数字格式
            // 1. 首先尝试匹配带小数的数字 (优先)
            var match = Regex.Match(text, @"(-?\d+\.\d+)");
            if (match.Success)
            {
                string result = match.Groups[1].Value;
                // 规范化小数：移除尾随的0，但保留 .0
                result = NormalizeDecimal(result);
                return result;
            }

            // 2. 然后尝试匹配整数
            match = Regex.Match(text, @"(-?\d+)");
            if (match.Success)
            {
                string numStr = match.Groups[1].Value;

                // 特殊处理：如果文本中有负号，但识别成 "01" 格式
                // 如 "-0.1" 被识别成 "01"
                if (text.Contains("-") && numStr.StartsWith("0") && numStr.Length <= 2)
                {
                    // 可能是 -0.1 被识别成 01
                    return "-" + numStr;
                }

                // 处理 "180701" 这种 - 截取合理长度的数字
                if (numStr.Length > 4)
                {
                    // 尝试分段匹配
                    var parts = Regex.Split(text, @"\s+");
                    foreach (var part in parts)
                    {
                        var partMatch = Regex.Match(part, @"(-?\d+\.?\d*)");
                        if (partMatch.Success && !string.IsNullOrEmpty(partMatch.Value) && partMatch.Value.Length <= 5)
                        {
                            return NormalizeDecimal(partMatch.Value);
                        }
                    }

                    // 如果没找到合适的，截取前4位
                    return numStr.Substring(0, Math.Min(4, numStr.Length));
                }

                return numStr;
            }

            // 如果不是纯数字，检查是否包含有效字符
            if (text.Length > 0)
            {
                // 只保留字母、数字和常见符号
                var cleaned = Regex.Replace(text, @"[^a-zA-Z0-9\-.\s]", "");
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return cleaned.Trim();
            }

            return text;
        }

        /// <summary>
        /// 规范化小数：移除尾随的0
        /// </summary>
        private string NormalizeDecimal(string numStr)
        {
            if (string.IsNullOrEmpty(numStr))
                return numStr;

            // 如果是负数，先处理
            bool isNegative = numStr.StartsWith("-");
            string absNum = isNegative ? numStr.Substring(1) : numStr;

            // 如果有小数点
            if (absNum.Contains("."))
            {
                // 移除尾随的0
                absNum = absNum.TrimEnd('0');
                // 如果最后是小数点，保留一个0
                if (absNum.EndsWith("."))
                    absNum += "0";
            }

            return isNegative ? "-" + absNum : absNum;
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
