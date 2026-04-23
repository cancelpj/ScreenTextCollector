using System;
using System.Collections.Generic;
using System.Text.Json;
using OcrServer.Configuration;
using AppSettings = OcrServer.Configuration.AppSettings;
using Xunit;

namespace OcrServer.Tests.Configuration
{
    /// <summary>
    /// AppSettings 配置测试：验证多设备配置正确加载
    /// </summary>
    public class AppSettingsTests
    {
        /// <summary>
        /// 用例：多设备配置正确加载
        /// 验证：Devices 列表正确反序列化，每个设备 URL 和 DeviceCode 正确
        /// </summary>
        [Fact]
        public void Load_MultiDeviceConfig_AllDevicesLoaded()
        {
            // Arrange：构造多设备配置 JSON
            var json = @"{
  ""CsvRecord"": false,
  ""Http"": {
    ""EnableHttp"": true,
    ""Ip"": ""0.0.0.0"",
    ""Port"": 8081
  },
  ""MqttBroker"": {
    ""EnableMqttPush"": true,
    ""Ip"": ""192.168.1.100"",
    ""Port"": 1883,
    ""ClientId"": ""OcrServer-001"",
    ""Username"": """",
    ""Password"": """"
  },
  ""Devices"": [
    {
      ""DeviceCode"": ""DEVICE-001"",
      ""CaptureScreenUrl"": ""http://192.168.1.50:8080"",
      ""TimeoutSeconds"": 10,
      ""CaptureFrequency"": 5,
      ""DefaultExtendPayload"": {
        ""DEVICECODE"": ""110001"",
        ""GroupCode"": ""collection1""
      },
      ""Topics"": [
        {
          ""Name"": ""screen/collection/110001"",
          ""ExtendPayload"": {
            ""GroupCode"": ""collection1""
          }
        },
        {
          ""Name"": ""screen/alarm/110001"",
          ""ExtendPayload"": {
            ""GroupCode"": ""alarm1""
          }
        }
      ]
    },
    {
      ""DeviceCode"": ""DEVICE-002"",
      ""CaptureScreenUrl"": ""http://192.168.1.51:8080"",
      ""TimeoutSeconds"": 10,
      ""CaptureFrequency"": 5,
      ""DefaultExtendPayload"": {
        ""DEVICECODE"": ""110002"",
        ""GroupCode"": ""collection1""
      },
      ""Topics"": [
        {
          ""Name"": ""screen/collection/110002"",
          ""ExtendPayload"": {
            ""GroupCode"": ""collection1""
          }
        }
      ]
    }
  ]
}";

            // Act
            var settings = JsonSerializer.Deserialize<OcrServer.Configuration.AppSettings>(json);

            // Assert 1：有两个设备
            Assert.NotNull(settings);
            Assert.Equal(2, settings.Devices.Count);

            // Assert 2：DEVICE-001 配置正确
            var device001 = settings.Devices[0];
            Assert.Equal("DEVICE-001", device001.DeviceCode);
            Assert.Equal("http://192.168.1.50:8080", device001.CaptureScreenUrl);
            Assert.Equal(10, device001.TimeoutSeconds);
            Assert.Equal(5, device001.CaptureFrequency);
            Assert.Equal("110001", device001.DefaultExtendPayload["DEVICECODE"]);
            Assert.Equal(2, device001.Topics.Count);
            Assert.Equal("screen/collection/110001", device001.Topics[0].Name);
            Assert.Equal("collection1", device001.Topics[0].ExtendPayload["GroupCode"]);
            Assert.Equal("screen/alarm/110001", device001.Topics[1].Name);

            // Assert 3：DEVICE-002 配置正确
            var device002 = settings.Devices[1];
            Assert.Equal("DEVICE-002", device002.DeviceCode);
            Assert.Equal("http://192.168.1.51:8080", device002.CaptureScreenUrl);
            Assert.Equal("110002", device002.DefaultExtendPayload["DEVICECODE"]);
            Assert.Single(device002.Topics);
            Assert.Equal("screen/collection/110002", device002.Topics[0].Name);
        }

        /// <summary>
        /// 验证：空设备列表场景
        /// </summary>
        [Fact]
        public void Load_EmptyDevices_HandledGracefully()
        {
            var json = @"{
  ""CsvRecord"": false,
  ""Devices"": []
}";

            var settings = JsonSerializer.Deserialize<OcrServer.Configuration.AppSettings>(json);

            Assert.NotNull(settings);
            Assert.NotNull(settings.Devices);
            Assert.Empty(settings.Devices);
        }

        /// <summary>
        /// 验证：缺少 Devices 字段时默认为空列表
        /// </summary>
        [Fact]
        public void Load_MissingDevicesField_DefaultsToEmpty()
        {
            var json = @"{
  ""CsvRecord"": true
}";

            var settings = JsonSerializer.Deserialize<OcrServer.Configuration.AppSettings>(json);

            Assert.NotNull(settings);
            Assert.NotNull(settings.Devices);
            Assert.Empty(settings.Devices);
        }

        /// <summary>
        /// 验证：DeviceConfig 各字段完整
        /// </summary>
        [Fact]
        public void DeviceConfig_AllProperties_SetCorrectly()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "TEST-001",
                CaptureScreenUrl = "http://localhost:8080",
                TimeoutSeconds = 30,
                CaptureFrequency = 10,
                DefaultExtendPayload = new Dictionary<string, string>
                {
                    ["DEVICECODE"] = "TEST001",
                    ["CustomKey"] = "CustomValue"
                },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig
                    {
                        Name = "test/topic/1",
                        ExtendPayload = new Dictionary<string, string> { ["Key"] = "Value" }
                    }
                }
            };

            Assert.Equal("TEST-001", device.DeviceCode);
            Assert.Equal("http://localhost:8080", device.CaptureScreenUrl);
            Assert.Equal(30, device.TimeoutSeconds);
            Assert.Equal(10, device.CaptureFrequency);
            Assert.Equal("TEST001", device.DefaultExtendPayload["DEVICECODE"]);
            Assert.Equal("CustomValue", device.DefaultExtendPayload["CustomKey"]);
            Assert.Single(device.Topics);
            Assert.Equal("test/topic/1", device.Topics[0].Name);
            Assert.Equal("Value", device.Topics[0].ExtendPayload["Key"]);
        }

        /// <summary>
        /// 验证：MqttTopicConfig 扩展 Payload 正确合并
        /// </summary>
        [Fact]
        public void MqttTopicConfig_ExtendPayload_StoredCorrectly()
        {
            var topic = new MqttTopicConfig
            {
                Name = "screen/collection/110001",
                ExtendPayload = new Dictionary<string, string>
                {
                    ["GroupCode"] = "collection1",
                    ["Priority"] = "high"
                }
            };

            Assert.Equal("screen/collection/110001", topic.Name);
            Assert.Equal(2, topic.ExtendPayload.Count);
            Assert.Equal("collection1", topic.ExtendPayload["GroupCode"]);
            Assert.Equal("high", topic.ExtendPayload["Priority"]);
        }

        /// <summary>
        /// 验证：从 JSON 文件加载配置
        /// </summary>
        [Fact]
        public void Load_FromJsonFile_ParsesCorrectly()
        {
            var json = @"{
  ""CsvRecord"": false,
  ""Http"": {
    ""EnableHttp"": true,
    ""Ip"": ""0.0.0.0"",
    ""Port"": 8081
  },
  ""MqttBroker"": {
    ""EnableMqttPush"": true,
    ""Ip"": ""192.168.1.100"",
    ""Port"": 1883
  },
  ""Devices"": [
    {
      ""DeviceCode"": ""DEVICE-001"",
      ""CaptureScreenUrl"": ""http://192.168.1.50:8080"",
      ""Topics"": [
        {
          ""Name"": ""screen/collection/110001"",
          ""ExtendPayload"": { ""GroupCode"": ""collection1"" }
        }
      ]
    }
  ]
}";

            var settings = JsonSerializer.Deserialize<OcrServer.Configuration.AppSettings>(json);

            Assert.NotNull(settings);
            Assert.Single(settings.Devices);
            Assert.Equal("DEVICE-001", settings.Devices[0].DeviceCode);
        }

        /// <summary>
        /// 验证：AppSettings 基础字段完整性
        /// </summary>
        [Fact]
        public void AppSettings_AllProperties_InitializedCorrectly()
        {
            var settings = new AppSettings
            {
                CsvRecord = true,
                Http = new HttpConfig { EnableHttp = true, Ip = "0.0.0.0", Port = 8081 },
                MqttBroker = new MqttBrokerConfig
                {
                    EnableMqttPush = true,
                    Ip = "localhost",
                    Port = 1883
                },
                Devices = new List<DeviceConfig>
                {
                    new DeviceConfig { DeviceCode = "TEST", CaptureScreenUrl = "http://localhost:8080" }
                }
            };

            Assert.True(settings.CsvRecord);
            Assert.NotNull(settings.Http);
            Assert.NotNull(settings.MqttBroker);
            Assert.NotNull(settings.Devices);
            Assert.Single(settings.Devices);
        }
    }
}
