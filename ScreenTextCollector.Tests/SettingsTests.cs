using System.Collections.Generic;
using PluginInterface;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// 配置类的单元测试
    /// </summary>
    public class SettingsTests
    {
        [Fact]
        public void AppSettings_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var settings = new AppSettings();

            // Assert
            Assert.Null(settings.DeviceName);
            Assert.False(settings.CsvRecord);
            Assert.Null(settings.Http);
            Assert.Null(settings.MqttBroker);
        }

        [Fact]
        public void AppSettings_Properties_CanBeSet()
        {
            // Arrange
            var settings = new AppSettings
            {
                DeviceName = "测试设备",
                CsvRecord = true,
                Http = new HttpConfig { EnableHttp = true, Ip = "127.0.0.1", Port = 8080 },
                MqttBroker = new MqttBrokerConfig { EnableMqttPush = true, Ip = "localhost", Port = 1883 }
            };

            // Assert
            Assert.Equal("测试设备", settings.DeviceName);
            Assert.True(settings.CsvRecord);
            Assert.NotNull(settings.Http);
            Assert.NotNull(settings.MqttBroker);
        }

        [Fact]
        public void CaptureSettings_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var settings = new CaptureSettings();

            // Assert
            Assert.Equal(0, settings.ScreenNumber);
            Assert.Null(settings.VerificationAreas);
            Assert.Null(settings.CollectionAreas);
            Assert.Equal("PaddleOCR", settings.OcrEngine);
        }

        [Fact]
        public void CaptureSettings_Properties_CanBeSet()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                ScreenNumber = 1,
                VerificationAreas = new List<ImageVerificationArea>(),
                CollectionAreas = new List<ImageCollectionArea>(),
                OcrEngine = "PaddleOCR"
            };

            // Assert
            Assert.Equal(1, settings.ScreenNumber);
            Assert.NotNull(settings.VerificationAreas);
            Assert.NotNull(settings.CollectionAreas);
            Assert.Equal("PaddleOCR", settings.OcrEngine);
        }

        [Fact]
        public void HttpConfig_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var httpConfig = new HttpConfig();

            // Assert
            Assert.True(httpConfig.EnableHttp);
            Assert.Equal("+", httpConfig.Ip);
            Assert.Equal(80, httpConfig.Port);
        }

        [Fact]
        public void MqttBrokerConfig_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var mqttConfig = new MqttBrokerConfig();

            // Assert
            Assert.False(mqttConfig.EnableMqttPush);
            Assert.Equal(1883, mqttConfig.Port);
            Assert.Equal("collection", mqttConfig.GroupCode);
            Assert.Equal(0, mqttConfig.CaptureFrequency);
        }

        [Fact]
        public void ImageVerificationArea_Properties_CanBeSet()
        {
            // Arrange & Act
            var area = new ImageVerificationArea
            {
                TopLeftX = 100,
                TopLeftY = 200,
                Width = 300,
                Height = 400,
                FileName = "test.png",
                MatchThreshold = 0.85f
            };

            // Assert
            Assert.Equal(100, area.TopLeftX);
            Assert.Equal(200, area.TopLeftY);
            Assert.Equal(300, area.Width);
            Assert.Equal(400, area.Height);
            Assert.Equal("test.png", area.FileName);
            Assert.Equal(0.85f, area.MatchThreshold);
        }

        [Fact]
        public void ImageCollectionArea_Properties_CanBeSet()
        {
            // Arrange & Act
            var area = new ImageCollectionArea
            {
                Name = "测试区域",
                TopLeftX = 50,
                TopLeftY = 100,
                Width = 150,
                Height = 200
            };

            // Assert
            Assert.Equal("测试区域", area.Name);
            Assert.Equal(50, area.TopLeftX);
            Assert.Equal(100, area.TopLeftY);
            Assert.Equal(150, area.Width);
            Assert.Equal(200, area.Height);
        }
    }
}
