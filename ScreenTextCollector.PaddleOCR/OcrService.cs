using NLog;
using PaddleOCRSharp;
using PluginInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenTextCollector.PaddleOCR
{
    public class OcrService : IOcrService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private PaddleOCREngine _engine;

        public OcrService()
        {
            _engine = new PaddleOCREngine();
        }

        /// <summary>
        /// 验证图像：使用像素级相似度比较验证截图是否包含目标画面
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

                using (var screenShot = new Bitmap(screenShotPath))
                {
                    // 逐个对比图像检测区域
                    foreach (var area in imageVerificationAreas)
                    {
                        var templatePath = Path.Combine("data", area.FileName);
                        if (!File.Exists(templatePath))
                        {
                            Log.Warn("模板文件不存在: {0}", templatePath);
                            return false;
                        }

                        using (var template = new Bitmap(templatePath))
                        {
                            // 提取 ROI 区域
                            var roiRect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                            // 确保 ROI 区域在截图范围内
                            if (roiRect.X < 0 || roiRect.Y < 0 ||
                                roiRect.Right > screenShot.Width || roiRect.Bottom > screenShot.Height)
                            {
                                Log.Warn("ROI 区域超出截图范围: {0}", area.FileName);
                                return false;
                            }

                            using (var roiImage = screenShot.Clone(roiRect, PixelFormat.Format24bppRgb))
                            {
                                // 计算相似度
                                double similarity = CalculateSimilarity(roiImage, template);

                                // 只要有区域图像对比不通过，就认为未监测到程序画面
                                if (similarity < area.MatchThreshold)
                                {
                                    Log.Debug("区域 {0} 相似度 {1} 低于阈值 {2}", area.FileName, similarity, area.MatchThreshold);
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

        /// <summary>
        /// 计算两张图像的相似度（基于像素级比较）
        /// </summary>
        private double CalculateSimilarity(Bitmap image1, Bitmap image2)
        {
            // 调整模板大小以匹配 ROI 大小
            using (var resizedTemplate = new Bitmap(image1.Width, image1.Height))
            {
                using (Graphics g = Graphics.FromImage(resizedTemplate))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image2, 0, 0, image1.Width, image1.Height);
                }

                // 锁定位图数据以提高访问速度
                var rect1 = new Rectangle(0, 0, image1.Width, image1.Height);
                var rect2 = new Rectangle(0, 0, resizedTemplate.Width, resizedTemplate.Height);

                BitmapData data1 = image1.LockBits(rect2, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                BitmapData data2 = resizedTemplate.LockBits(rect2, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                try
                {
                    int width = image1.Width;
                    int height = image1.Height;
                    int stride1 = data1.Stride;
                    int stride2 = data2.Stride;
                    IntPtr scan0_1 = data1.Scan0;
                    IntPtr scan0_2 = data2.Scan0;

                    long totalDiff = 0;
                    long totalPixels = width * height;

                    unsafe
                    {
                        byte* p1 = (byte*)scan0_1.ToPointer();
                        byte* p2 = (byte*)scan0_2.ToPointer();

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int idx1 = y * stride1 + x * 3;
                                int idx2 = y * stride2 + x * 3;

                                // 计算 RGB 通道的差异
                                int diffB = Math.Abs(p1[idx1] - p2[idx2]);
                                int diffG = Math.Abs(p1[idx1 + 1] - p2[idx2 + 1]);
                                int diffR = Math.Abs(p1[idx1 + 2] - p2[idx2 + 2]);

                                totalDiff += diffB + diffG + diffR;
                            }
                        }
                    }

                    // 计算相似度（0-1之间）
                    // 最大可能差异为 255 * 3 * pixels
                    double maxDiff = 255.0 * 3.0 * totalPixels;
                    double similarity = 1.0 - (totalDiff / maxDiff);

                    return similarity;
                }
                finally
                {
                    image1.UnlockBits(data1);
                    resizedTemplate.UnlockBits(data2);
                }
            }
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
                    OCRResult result = _engine.DetectText(image);
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
