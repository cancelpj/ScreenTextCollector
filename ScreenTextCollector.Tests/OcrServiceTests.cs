using System.Collections.Generic;
using System.IO;
using PluginInterface;
using ScreenTextCollector.OpenCvSharp;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// OcrService 的单元测试
    /// 注意：这些测试需要准备测试图片文件
    /// </summary>
    public class OcrServiceTests
    {
        private readonly OcrService _ocrService;

        public OcrServiceTests()
        {
            _ocrService = new OcrService();
        }

        [Fact]
        public void OcrService_ImplementsIOcrService()
        {
            // Assert
            Assert.IsAssignableFrom<IOcrService>(_ocrService);
        }

        [Fact(Skip = "需要准备测试图片文件")]
        public void VerifyImage_WithValidImage_ShouldReturnTrue()
        {
            // Arrange
            var testImagePath = Path.Combine("TestImages", "test_screenshot.png");
            var verificationAreas = new List<ImageVerificationArea>
            {
                new ImageVerificationArea
                {
                    TopLeftX = 0,
                    TopLeftY = 0,
                    Width = 100,
                    Height = 100,
                    FileName = "test_template.png",
                    MatchThreshold = 0.8f
                }
            };

            // Act
            var result = _ocrService.VerifyImage(testImagePath, verificationAreas);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "需要准备测试图片文件")]
        public void VerifyImage_WithMismatchedImage_ShouldReturnFalse()
        {
            // Arrange
            var testImagePath = Path.Combine("TestImages", "test_screenshot.png");
            var verificationAreas = new List<ImageVerificationArea>
            {
                new ImageVerificationArea
                {
                    TopLeftX = 0,
                    TopLeftY = 0,
                    Width = 100,
                    Height = 100,
                    FileName = "different_template.png",
                    MatchThreshold = 0.95f // 高阈值，不太可能匹配
                }
            };

            // Act
            var result = _ocrService.VerifyImage(testImagePath, verificationAreas);

            // Assert
            Assert.False(result);
        }

        [Fact(Skip = "需要准备测试图片文件和 Tesseract 数据")]
        public void PerformOcr_WithValidImage_ShouldReturnText()
        {
            // Arrange
            var testImagePath = Path.Combine("TestImages", "test_text_image.png");
            var collectionArea = new ImageCollectionArea
            {
                Name = "测试区域",
                TopLeftX = 0,
                TopLeftY = 0,
                Width = 200,
                Height = 50
            };

            // Act
            var result = _ocrService.PerformOcr(testImagePath, collectionArea);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact(Skip = "需要准备测试图片文件")]
        public void PerformOcr_WithEmptyArea_ShouldReturnEmptyOrWhitespace()
        {
            // Arrange
            var testImagePath = Path.Combine("TestImages", "blank_image.png");
            var collectionArea = new ImageCollectionArea
            {
                Name = "空白区域",
                TopLeftX = 0,
                TopLeftY = 0,
                Width = 100,
                Height = 100
            };

            // Act
            var result = _ocrService.PerformOcr(testImagePath, collectionArea);

            // Assert
            Assert.True(string.IsNullOrWhiteSpace(result));
        }
    }
}
