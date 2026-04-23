using System;
using System.Collections.Generic;
using OcrServer.Configuration;
using OcrServer.Services;
using Xunit;

namespace OcrServer.Tests.Services
{
    /// <summary>
    /// OcrService 测试：
    /// - 后处理：验证 PostProcessText 清理常见 OCR 错误字符（静态方法，无需依赖注入）
    /// - ImageVerificationArea 和 ImageCollectionArea 属性正确
    /// </summary>
    public class OcrServiceTests
    {
        // ============================================================
        // 后处理测试（PostProcessText 是静态方法，直接测试）
        // ============================================================

        /// <summary>
        /// 验证：OCR 后处理正确清理常见 OCR 错误字符
        /// </summary>
        [Theory]
        [InlineData("12.5|", "12.5")]
        [InlineData("1800.2_", "1800.2")]
        [InlineData("Aut o", "Auto")]
        [InlineData("0pen", "Open")]
        [InlineData("(12.5)", "12.5")]
        [InlineData("[test]", "test")]
        [InlineData("{value}", "value")]
        [InlineData("100.5°", "100.5")]
        public void PostProcessText_RemovesCommonOcrErrors_ReturnsCleanText(string input, string expected)
        {
            var area = new ImageCollectionArea { Name = "V001" };
            var result = OcrService.PostProcessText(input, area);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// 验证：空白文本直接返回
        /// </summary>
        [Fact]
        public void PostProcessText_EmptyOrWhitespace_ReturnsAsIs()
        {
            var area = new ImageCollectionArea { Name = "V001" };
            Assert.Equal("", OcrService.PostProcessText("", area));
            Assert.Equal("  ", OcrService.PostProcessText("  ", area));
        }

        /// <summary>
        /// 验证：原始文本无错误字符时保持不变
        /// </summary>
        [Theory]
        [InlineData("12.5", "12.5")]
        [InlineData("1800.2", "1800.2")]
        [InlineData("-0.9", "-0.9")]
        [InlineData("Open", "Open")]
        [InlineData("Auto", "Auto")]
        [InlineData("Closed", "Closed")]
        public void PostProcessText_NoErrors_ReturnsUnchanged(string input, string expected)
        {
            var area = new ImageCollectionArea { Name = "V001" };
            var result = OcrService.PostProcessText(input, area);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// 验证：多字符清理（链式替换）
        /// </summary>
        [Fact]
        public void PostProcessText_MultipleErrorChars_AllRemoved()
        {
            var area = new ImageCollectionArea { Name = "V001" };
            var result = OcrService.PostProcessText("12.5|_()[]{}°`'", area);
            Assert.Equal("12.5", result);
        }

        // ============================================================
        // 配置类型测试（验证类型属性）
        // ============================================================

        /// <summary>
        /// 验证：ImageCollectionArea 属性正确
        /// </summary>
        [Fact]
        public void ImageCollectionArea_Properties_SetCorrectly()
        {
            var area = new ImageCollectionArea
            {
                Name = "V001",
                Topic = "screen/collection/110001",
                ScreenNumber = 0,
                TopLeftX = 60,
                TopLeftY = 205,
                Width = 44,
                Height = 11
            };

            Assert.Equal("V001", area.Name);
            Assert.Equal("screen/collection/110001", area.Topic);
            Assert.Equal(0, area.ScreenNumber);
            Assert.Equal(60, area.TopLeftX);
            Assert.Equal(205, area.TopLeftY);
            Assert.Equal(44, area.Width);
            Assert.Equal(11, area.Height);
        }

        /// <summary>
        /// 验证：ImageVerificationArea 属性正确
        /// </summary>
        [Fact]
        public void ImageVerificationArea_Properties_SetCorrectly()
        {
            var area = new ImageVerificationArea
            {
                ScreenNumber = 0,
                TopLeftX = 0,
                TopLeftY = 0,
                Width = 50,
                Height = 50,
                FileName = "verify.png",
                MatchThreshold = 0.8f
            };

            Assert.Equal(0, area.ScreenNumber);
            Assert.Equal(0, area.TopLeftX);
            Assert.Equal(0, area.TopLeftY);
            Assert.Equal(50, area.Width);
            Assert.Equal(50, area.Height);
            Assert.Equal("verify.png", area.FileName);
            Assert.Equal(0.8f, area.MatchThreshold);
        }

        /// <summary>
        /// 验证：CaptureSettings 包含 CollectionAreas 和 VerificationAreas
        /// </summary>
        [Fact]
        public void CaptureSettings_ContainsAreas()
        {
            var settings = new CaptureSettings
            {
                OcrEngine = "PaddleOCR",
                CollectionAreas = new List<ImageCollectionArea>
                {
                    new ImageCollectionArea { Name = "V001", ScreenNumber = 0, TopLeftX = 60, TopLeftY = 205, Width = 44, Height = 11 },
                    new ImageCollectionArea { Name = "V002", ScreenNumber = 0, TopLeftX = 111, TopLeftY = 205, Width = 44, Height = 11 }
                },
                VerificationAreas = new List<ImageVerificationArea>
                {
                    new ImageVerificationArea { ScreenNumber = 0, TopLeftX = 0, TopLeftY = 0, Width = 50, Height = 50, FileName = "verify.png", MatchThreshold = 0.8f }
                }
            };

            Assert.Equal("PaddleOCR", settings.OcrEngine);
            Assert.Equal(2, settings.CollectionAreas.Count);
            Assert.Single(settings.VerificationAreas);
            Assert.Equal("V001", settings.CollectionAreas[0].Name);
            Assert.Equal("verify.png", settings.VerificationAreas[0].FileName);
        }
    }
}
