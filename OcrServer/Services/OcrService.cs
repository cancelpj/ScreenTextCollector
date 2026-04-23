using OcrServer.Configuration;
using PaddleOCRSharp;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using ILogger = Serilog.ILogger;

namespace OcrServer.Services;

/// <summary>
/// OCR 服务（PaddleOCRSharp 封装）
/// 从 ScreenTextCollector.PaddleOCR.OcrService 迁移到 .NET 10，改为支持 Base64 输入
/// </summary>
public sealed class OcrService : IDisposable
{
    private readonly PaddleOCREngine _engine;
    private readonly ILogger _logger;
    private readonly string _dataDir;

    // 模板图缓存（启动时加载，避免每次验证都读磁盘）
    private readonly ConcurrentDictionary<string, Bitmap> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    public OcrService(ILogger logger, string dataDir = "data")
    {
        _logger = logger;
        _dataDir = dataDir;
        _engine = new PaddleOCREngine();
    }

    /// <summary>
    /// 验证图像：使用像素级相似度比较验证截图是否包含目标画面
    /// </summary>
    /// <param name="screenShotBytes">截图数据（Base64 解码后的字节数组）</param>
    /// <param name="imageVerificationAreas">验证区域列表</param>
    /// <returns>验证是否通过</returns>
    public bool VerifyImage(byte[] screenShotBytes, List<ImageVerificationArea> imageVerificationAreas)
    {
        if (imageVerificationAreas == null || imageVerificationAreas.Count == 0)
            return true;

        try
        {
            using var screenShot = new Bitmap(new MemoryStream(screenShotBytes));

            foreach (var area in imageVerificationAreas)
            {
                var template = GetOrLoadTemplate(area.FileName);
                if (template == null)
                {
                    _logger.Warning("模板文件不存在: {TemplatePath}", area.FileName);
                    return false;
                }

                var roiRect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

                if (roiRect.X < 0 || roiRect.Y < 0 ||
                    roiRect.Right > screenShot.Width || roiRect.Bottom > screenShot.Height)
                {
                    _logger.Warning("ROI 区域超出截图范围: {AreaFileName}", area.FileName);
                    return false;
                }

                using var roiImage = screenShot.Clone(roiRect, PixelFormat.Format24bppRgb);
                double similarity = CalculateSimilarity(roiImage, template);

                if (similarity < area.MatchThreshold)
                {
                    _logger.Debug("区域 {AreaFileName} 相似度 {Similarity} 低于阈值 {Threshold}",
                        area.FileName, similarity, area.MatchThreshold);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "图像验证异常");
            return false;
        }
    }

    /// <summary>
    /// 获取或加载模板图（线程安全缓存）
    /// </summary>
    private Bitmap? GetOrLoadTemplate(string fileName)
    {
        if (_templateCache.TryGetValue(fileName, out var cached))
            return cached;

        var templatePath = Path.Combine(_dataDir, fileName);
        if (!File.Exists(templatePath))
            return null;

        try
        {
            var bitmap = new Bitmap(templatePath);
            _templateCache.TryAdd(fileName, bitmap);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 验证图像（重载，接受 Base64 字符串）
    /// </summary>
    public bool VerifyImage(string base64Image, List<ImageVerificationArea> imageVerificationAreas)
    {
        byte[] bytes = Convert.FromBase64String(base64Image);
        return VerifyImage(bytes, imageVerificationAreas);
    }

    /// <summary>
    /// 计算两张图像的相似度（基于像素级比较）
    /// </summary>
    private double CalculateSimilarity(Bitmap image1, Bitmap template)
    {
        // 将 template 缩放到 image1 尺寸
        using var resizedTemplate = new Bitmap(image1.Width, image1.Height);
        using (var g = Graphics.FromImage(resizedTemplate))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(template, 0, 0, image1.Width, image1.Height);
        }

        var rect = new Rectangle(0, 0, image1.Width, image1.Height);
        BitmapData data1 = image1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        BitmapData data2 = resizedTemplate.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

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
                int row1 = y * stride1;
                int row2 = y * stride2;
                for (int x = 0; x < width; x++)
                {
                    int idx1 = row1 + x * 3;
                    int idx2 = row2 + x * 3;

                    int diffB = Math.Abs(p1[idx1] - p2[idx2]);
                    int diffG = Math.Abs(p1[idx1 + 1] - p2[idx2 + 1]);
                    int diffR = Math.Abs(p1[idx1 + 2] - p2[idx2 + 2]);

                    totalDiff += diffB + diffG + diffR;
                }
            }
        }

        // BitmapData 用完自动失效（Bitmap.Dispose 时由 GDI+ 清理）
        double maxDiff = 255.0 * 3.0 * totalPixels;
        return 1.0 - (totalDiff / maxDiff);
    }

    /// <summary>
    /// 执行 OCR（从 Base64 图像数据中截取区域并识别文字）
    /// </summary>
    /// <param name="base64Image">Base64 编码的图像数据</param>
    /// <param name="area">采集区域配置</param>
    /// <returns>OCR 识别结果（已后处理）</returns>
    public string PerformOcr(string base64Image, ImageCollectionArea area)
    {
        byte[] bytes = Convert.FromBase64String(base64Image);
        using var screenShot = new Bitmap(new MemoryStream(bytes));

        var roiRect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);

        // 限制截取范围，防止越界
        if (roiRect.Right > screenShot.Width)
            roiRect.Width = screenShot.Width - roiRect.X;
        if (roiRect.Bottom > screenShot.Height)
            roiRect.Height = screenShot.Height - roiRect.Y;

        using var roiImage = screenShot.Clone(roiRect, PixelFormat.Format24bppRgb);

        OCRResult result = _engine.DetectText(roiImage);
        string text = result.Text?.Trim() ?? "";
        return PostProcessText(text, area);
    }

    /// <summary>
    /// 后处理 OCR 识别结果：清理常见 OCR 错误字符
    /// </summary>
    public static string PostProcessText(string text, ImageCollectionArea area)
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

        // 常见误识别修正
        if (text == "Aut o") text = "Auto";
        if (text == "0pen") text = "Open";

        return text;
    }

    public void Dispose() => _engine.Dispose();
}
