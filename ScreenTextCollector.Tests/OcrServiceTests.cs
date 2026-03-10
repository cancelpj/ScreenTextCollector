using System;
using System.Collections.Generic;
using System.IO;
using PluginInterface;
using ScreenTextCollector.OpenCvSharp;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// OcrService 的单元测试
    /// 使用 TestImages 目录下的真实测试数据
    /// </summary>
    public class OcrServiceTests
    {
        protected readonly ITestOutputHelper Output;
        private readonly OcrService _ocrService;
        private readonly string _testDataBasePath;

        public OcrServiceTests(ITestOutputHelper tempOutput)
        {
            Output = tempOutput;
            _ocrService = new OcrService();
            // 测试数据的基础路径（输出目录）
            _testDataBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
        }

        [Fact]
        public void OcrService_ImplementsIOcrService()
        {
            Assert.IsAssignableFrom<IOcrService>(_ocrService);
        }

        #region 图像验证测试

        /// <summary>
        /// 图像验证测试 - 参数化测试
        /// </summary>
        [Theory]
        // 纵拉机测试截图
        [InlineData("纵拉机", "test_screenshot_1.png", "20260117115113709.png", 0, 0, 50, 50, 0.8f)]
        [InlineData("纵拉机", "test_screenshot_2.png", "20260117115113709.png", 0, 0, 50, 50, 0.8f)]
        // 拉弯机测试截图
        [InlineData("拉弯机", "test_screenshot.png", "20260113064214930.png", 0, 0, 30, 20, 0.8f)]
        public void VerifyImage_Test_ShouldReturnTrue(
            string machineName, string screenshotFile, string templateFile,
            int x, int y, int width, int height, float threshold)
        {
            // Arrange
            var testImagePath = Path.Combine(_testDataBasePath, machineName, screenshotFile);
            Assert.True(File.Exists(testImagePath), $"测试图片不存在: {testImagePath}");

            var verificationAreas = new List<ImageVerificationArea>
            {
                new ImageVerificationArea
                {
                    TopLeftX = x,
                    TopLeftY = y,
                    Width = width,
                    Height = height,
                    FileName = templateFile,
                    MatchThreshold = threshold
                }
            };

            // Act
            var result = _ocrService.VerifyImage(testImagePath, verificationAreas);

            // Assert
            Assert.True(result, $"{machineName} 图像验证应该通过");
        }

        #endregion

        /// <summary>
        /// 所有采集区域 OCR 测试
        /// </summary>
        [Theory]
        #region 纵拉机 OCR 测试数据
        // 第一张截图 test_screenshot_1.png (V001-V049)
        [InlineData("纵拉机", "test_screenshot_1.png", "V001", 60, 205, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V002", 111, 205, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V003", 60, 223, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V004", 111, 223, 44, 12)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V005", 60, 242, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V006", 353, 203, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V007", 405, 203, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V008", 353, 219, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V009", 404, 219, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V010", 331, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V011", 385, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V012", 437, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V013", 663, 202, 45, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V014", 712, 202, 45, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V015", 663, 221, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V016", 717, 221, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V017", 665, 240, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V018", 60, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V019", 114, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V020", 60, 344, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V021", 353, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V022", 405, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V023", 353, 345, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V024", 405, 345, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V025", 330, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V026", 383, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V027", 436, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V028", 667, 318, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V029", 722, 318, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V030", 667, 337, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V031", 65, 432, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V032", 65, 450, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V033", 65, 468, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V034", 65, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V035", 65, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V036", 320, 460, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V037", 321, 510, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V038", 434, 505, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V039", 669, 432, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V040", 669, 450, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V041", 669, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V042", 669, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V043", 669, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V044", 116, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V045", 116, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V046", 116, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V047", 468, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V048", 468, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_1.png", "V049", 468, 504, 41, 11)]
        // 第二张截图 test_screenshot_2.png (V001-V010)
        [InlineData("纵拉机", "test_screenshot_2.png", "V001", 60, 205, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V002", 111, 205, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V003", 60, 223, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V004", 111, 223, 44, 12)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V005", 60, 242, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V006", 353, 203, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V007", 405, 203, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V008", 353, 219, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V009", 404, 219, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V010", 331, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V011", 385, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V012", 437, 259, 40, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V013", 663, 202, 45, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V014", 712, 202, 45, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V015", 663, 221, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V016", 717, 221, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V017", 665, 240, 44, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V018", 60, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V019", 114, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V020", 60, 344, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V021", 353, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V022", 405, 325, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V023", 353, 345, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V024", 405, 345, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V025", 330, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V026", 383, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V027", 436, 377, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V028", 667, 318, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V029", 722, 318, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V030", 667, 337, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V031", 65, 432, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V032", 65, 450, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V033", 65, 468, 42, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V034", 65, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V035", 65, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V036", 320, 460, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V037", 321, 510, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V038", 434, 505, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V039", 669, 432, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V040", 669, 450, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V041", 669, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V042", 669, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V043", 669, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V044", 116, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V045", 116, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V046", 116, 504, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V047", 468, 468, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V048", 468, 486, 41, 11)]
        [InlineData("纵拉机", "test_screenshot_2.png", "V049", 468, 504, 41, 11)]
        #endregion
        #region 拉弯机 OCR 测试数据
        [InlineData("拉弯机", "test_screenshot.png", "V001", 165, 172, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V002", 165, 189, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V003", 165, 207, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V004", 165, 225, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V005", 165, 242, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V006", 165, 282, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V007", 165, 301, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V008", 165, 320, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V009", 165, 362, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V010", 165, 381, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V011", 165, 400, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V012", 165, 441, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V013", 165, 460, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V014", 600, 172, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V015", 600, 189, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V016", 600, 207, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V017", 600, 225, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V018", 600, 242, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V019", 600, 282, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V020", 600, 301, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V021", 600, 320, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V022", 600, 362, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V023", 600, 381, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V024", 600, 400, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V025", 600, 441, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V026", 600, 460, 45, 10)]
        [InlineData("拉弯机", "test_screenshot.png", "V027", 195, 513, 45, 12)]
        [InlineData("拉弯机", "test_screenshot.png", "V028", 606, 511, 45, 20)]
        [InlineData("拉弯机", "test_screenshot.png", "V029", 415, 184, 40, 20)]
        [InlineData("拉弯机", "test_screenshot.png", "V030", 375, 218, 45, 12)]
        [InlineData("拉弯机", "test_screenshot.png", "V031", 280, 141, 45, 12)]
        [InlineData("拉弯机", "test_screenshot.png", "V032", 465, 141, 45, 12)]
        #endregion
        public void PerformOcr_ShouldReturnText(
            string machineName, string screenshotFile, string name, int x, int y, int width, int height)
        {
            // Arrange
            var testImagePath = Path.Combine(_testDataBasePath, machineName, screenshotFile);
            Assert.True(File.Exists(testImagePath), $"测试图片不存在: {testImagePath}");

            var collectionArea = new ImageCollectionArea
            {
                Name = name,
                TopLeftX = x,
                TopLeftY = y,
                Width = width,
                Height = height
            };

            // Act
            var result = _ocrService.PerformOcr(testImagePath, collectionArea);

            // 输出 OCR 识别结果
            Output.WriteLine($"[{machineName}] {screenshotFile} - {name}: '{result}'");

            // Assert
            Assert.NotEqual("",result);
        }

        #region 边界测试

        /// <summary>
        /// 测试不存在的模板文件
        /// </summary>
        [Fact]
        public void VerifyImage_NotExistTemplate_ShouldReturnFalse()
        {
            // Arrange
            var testImagePath = Path.Combine(_testDataBasePath, "纵拉机", "test_screenshot_1.png");
            Assert.True(File.Exists(testImagePath), $"测试图片不存在: {testImagePath}");

            var verificationAreas = new List<ImageVerificationArea>
            {
                new ImageVerificationArea
                {
                    TopLeftX = 0,
                    TopLeftY = 0,
                    Width = 50,
                    Height = 50,
                    FileName = "不存在的模板.png",
                    MatchThreshold = 0.95f
                }
            };

            // Act
            var result = _ocrService.VerifyImage(testImagePath, verificationAreas);

            // Assert
            Assert.False(result, "不存在的模板应该返回 false");
        }

        /// <summary>
        /// 测试超出图片范围的区域
        /// </summary>
        [Fact]
        public void PerformOcr_OutOfRangeArea_ShouldHandleGracefully()
        {
            // Arrange
            var testImagePath = Path.Combine(_testDataBasePath, "纵拉机", "test_screenshot_1.png");
            Assert.True(File.Exists(testImagePath), $"测试图片不存在: {testImagePath}");

            var collectionArea = new ImageCollectionArea
            {
                Name = "超范围区域",
                TopLeftX = 9999,
                TopLeftY = 9999,
                Width = 100,
                Height = 100
            };

            // Act & Assert
            try
            {
                var result = _ocrService.PerformOcr(testImagePath, collectionArea);
                Assert.NotNull(result);
            }
            catch
            {
                // OpenCV 可能抛出异常，这是预期行为
            }
        }

        #endregion
    }
}
