using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PluginInterface;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// OCR 测试输入数据，继承自 ImageCollectionArea
    /// </summary>
    public class OcrTestInput : ImageCollectionArea
    {
        /// <summary>
        /// 机器名称（测试元数据）
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// 截图文件名（测试元数据）
        /// </summary>
        public string ScreenshotFile { get; set; }

        /// <summary>
        /// 期望的 OCR 识别结果
        /// </summary>
        public object Expected { get; set; }

        public OcrTestInput(string machineName, string screenshotFile, string name, int x, int y, int width, int height, object expected)
        {
            MachineName = machineName;
            ScreenshotFile = screenshotFile;
            Name = name;
            TopLeftX = x;
            TopLeftY = y;
            Width = width;
            Height = height;
            Expected = expected;
        }
    }

    /// <summary>
    /// 纵拉机测试数据
    /// </summary>
    public class ZongLaJiTestData : TheoryData<OcrTestInput[]>
    {
        public ZongLaJiTestData()
        {
            Add(new OcrTestInput[]
            {
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V001", 60, 205, 44, 11, 1800.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V002", 111, 205, 44, 11, 1800.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V003", 60, 223, 44, 11, -0.01),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V004", 111, 223, 44, 12, -0.01),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V005", 60, 242, 44, 11, -5.7),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V006", 353, 203, 44, 11, -0.9),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V007", 405, 203, 40, 11, -0.9),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V008", 353, 219, 42, 11, 5185.7),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V009", 404, 219, 42, 11, 5185.7),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V010", 331, 259, 40, 11, "0.00"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V011", 385, 259, 40, 11, "0.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V012", 437, 259, 40, 11, "0.00"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V013", 663, 202, 45, 11, "1799.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V014", 717, 202, 44, 11, "1799.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V015", 663, 221, 44, 11, -0.02),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V016", 717, 221, 44, 11, -0.02),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V017", 665, 240, 44, 11, -2.6),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V018", 60, 325, 42, 11, 399.5),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V019", 114, 325, 42, 11, 399.5),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V020", 60, 344, 42, 11, -5.6),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V021", 353, 325, 42, 11, 797.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V022", 405, 325, 42, 11, 797.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V023", 353, 345, 42, 11, "0.00"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V024", 405, 345, 42, 11, "0.00"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V025", 330, 377, 42, 11, -6.87),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V026", 383, 377, 42, 11, -14.8),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V027", 436, 377, 42, 11, -7.93),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V028", 667, 318, 42, 11, 399.9),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V029", 722, 318, 42, 12, 399.9),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V030", 667, 337, 42, 11, "-13.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V031", 65, 432, 41, 11, "Open"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V032", 65, 450, 42, 11, 0),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V033", 65, 468, 42, 11, 0.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V034", 65, 486, 41, 11, -0.1),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V035", 65, 504, 41, 11, -0.1),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V036", 320, 460, 41, 11, 71.1),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V037", 321, 510, 41, 11, "0.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V038", 434, 505, 41, 11, 36.9),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V039", 669, 432, 41, 11, "Open"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V040", 669, 450, 41, 11, 0),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V041", 669, 468, 41, 11, -0.1),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V042", 669, 486, 41, 11, "0.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V043", 669, 504, 41, 11, "0.0"),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V044", 116, 468, 41, 11, 0.2),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V045", 116, 486, 41, 11, -0.1),
                new OcrTestInput("纵拉机", "test_screenshot_1.png", "V046", 116, 504, 41, 11, -0.1),
            });
        }
    }

    /// <summary>
    /// 拉弯机测试数据
    /// </summary>
    public class LaWanJiTestData : TheoryData<OcrTestInput[]>
    {
        public LaWanJiTestData()
        {
            Add(new OcrTestInput[]
            {
                new OcrTestInput("拉弯机", "test_screenshot.png", "V001", 165, 172, 45, 10, 459.6),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V002", 165, 189, 45, 10, 459.6),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V003", 165, 207, 45, 10, -0.92),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V004", 165, 225, 45, 10, 97.69),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V005", 165, 242, 45, 10, "762.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V006", 165, 282, 45, 10, 0.1),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V007", 165, 301, 45, 10, 0.1),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V008", 165, 320, 45, 10, "180.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V009", 165, 362, 45, 10, 31.7),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V010", 165, 381, 45, 10, 31.7),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V011", 165, 400, 45, 10, "360.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V012", 165, 441, 45, 10, 1746.5),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V013", 165, 460, 45, 10, 1746.5),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V014", 600, 172, 45, 10, 386.2),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V015", 600, 189, 45, 10, 386.2),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V016", 600, 207, 45, 10, -1.15),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V017", 600, 225, 45, 10, 97.69),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V018", 600, 242, 45, 10, "762.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V019", 600, 282, 45, 10, 0.3),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V020", 600, 301, 45, 10, 0.3),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V021", 600, 320, 45, 10, "180.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V022", 600, 362, 45, 10, 78.8),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V023", 600, 381, 45, 10, 78.8),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V024", 600, 400, 45, 10, "360.0"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V025", 600, 441, 45, 10, 1746.4),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V026", 600, 460, 45, 10, 1746.4),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V027", 195, 513, 45, 12, 0),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V028", 606, 511, 45, 20, "Auto"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V029", 415, 184, 40, 20, "关"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V030", 375, 218, 45, 12, 28.5),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V031", 280, 141, 45, 12, "0.00"),
                new OcrTestInput("拉弯机", "test_screenshot.png", "V032", 465, 141, 45, 12, "0.00"),
            });
        }
    }

    /// <summary>
    /// OcrService 的单元测试
    /// 使用 TestImages 目录下的真实测试数据
    /// </summary>
    public class OcrServiceTests
    {
        protected readonly ITestOutputHelper Output;
        private readonly IOcrService _ocrService;
        private readonly string _testDataBasePath;
        private readonly Stopwatch _stopwatch;

        public OcrServiceTests(ITestOutputHelper tempOutput)
        {
            Output = tempOutput;
            _ocrService = new PaddleOCR.OcrService();
            _testDataBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
            _stopwatch = new Stopwatch();
        }

        [Fact]
        public void OcrService_ImplementsIOcrService()
        {
            Assert.IsType<IOcrService>(_ocrService, exactMatch: false);
        }

        #region 图像验证测试

        /// <summary>
        /// 图像验证测试 - 参数化测试
        /// </summary>
        [Theory]
        [InlineData("纵拉机", "test_screenshot_1.png", "20260117115113709.png", 0, 0, 50, 50, 0.8f)]
        [InlineData("纵拉机", "test_screenshot_2.png", "20260117115113709.png", 0, 0, 50, 50, 0.8f)]
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
        /// 使用 Parallel.ForEach 并行执行，与实际运行逻辑保持一致
        /// </summary>
        [Theory]
        [ClassData(typeof(ZongLaJiTestData))]
        [ClassData(typeof(LaWanJiTestData))]
        public void PerformOcr_ShouldReturnText(OcrTestInput[] testInputs)
        {
            _stopwatch.Start();

            // 获取该组的第一条数据来确定截图文件路径（同一组使用同一张截图）
            var screenshotFile = testInputs[0].ScreenshotFile;
            var machineName = testInputs[0].MachineName;
            var testImagePath = Path.Combine(_testDataBasePath, machineName, screenshotFile);

            Assert.True(File.Exists(testImagePath), $"测试图片不存在: {testImagePath}");

            Output.WriteLine($"\n## {machineName} {screenshotFile} OCR 测试");

            // 使用 ConcurrentDictionary 收集并行 OCR 结果
            var ocrResults = new ConcurrentDictionary<string, string>();

            // 使用 Parallel.ForEach 并行执行 OCR（OcrTestInput 继承自 ImageCollectionArea）
            Parallel.ForEach(testInputs, area =>
            {
                var result = _ocrService.PerformOcr(testImagePath, area);
                ocrResults[area.Name] = result;
            });

            // 验证结果
            var passedTests = 0;
            foreach (var input in testInputs)
            {
                var result = ocrResults[input.Name];
                var expected = input.Expected.ToString();

                if (result == expected)
                {
                    passedTests++;
                    Output.WriteLine($"- [PASS] {input.Name}: '{result}'");
                }
                else
                {
                    Output.WriteLine($"- [FAIL] {input.Name}: '{result}' (期望: '{expected}')");
                }
            }

            _stopwatch.Stop();

            var totalTests = testInputs.Length;
            var passRate = totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0;
            Output.WriteLine($"\n## {machineName} {screenshotFile} OCR 测试统计");
            Output.WriteLine($"- 总测试数: {totalTests}");
            Output.WriteLine($"- 通过数: {passedTests}");
            Output.WriteLine($"- 失败数: {totalTests - passedTests}");
            Output.WriteLine($"- 正确率: {passRate:F2}%");
            Output.WriteLine($"- 总耗时: {_stopwatch.ElapsedMilliseconds / 1000} s");
            Output.WriteLine("---\n");

        }

        #region 边界测试

        [Fact]
        public void VerifyImage_NotExistTemplate_ShouldReturnFalse()
        {
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

            var result = _ocrService.VerifyImage(testImagePath, verificationAreas);
            Assert.False(result, "不存在的模板应该返回 false");
        }

        [Fact]
        public void PerformOcr_OutOfRangeArea_ShouldHandleGracefully()
        {
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
