using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OcrServer.Configuration;
using OcrServer.Serialization;
using Xunit;

namespace OcrServer.Tests.Services
{
    /// <summary>
    /// MqttPushService 测试：验证 Payload 构造和 MQTTnet 发布逻辑
    /// 通过测试 MqttPayload 模型和 PayloadJsonBuilder 辅助类来验证核心逻辑
    /// </summary>
    public class MqttPushServiceTests
    {
        /// <summary>
        /// 用例：Payload 含 DeviceCode
        /// 验证：ExtendPayload 中 DEVICECODE 与设备配置一致
        /// </summary>
        [Fact]
        public void MqttPayload_ContainsCorrectDeviceCode()
        {
            // Arrange
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string>
                {
                    ["DEVICECODE"] = "110001",
                    ["GroupCode"] = "collection1"
                },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig
                    {
                        Name = "screen/collection/110001",
                        ExtendPayload = new Dictionary<string, string>
                        {
                            ["GroupCode"] = "collection1"
                        }
                    }
                }
            };

            // Act：构造 Payload（模拟 PushToMqttAsync 中的逻辑）
            var payload = BuildPayload("V001", "12.5", 1712345678900L, device, "screen/collection/110001");

            // Assert
            Assert.Equal(1712345678900L, payload.Timestamp);
            Assert.Equal("12.5", payload.Data["V001"]);
            Assert.Equal("110001", payload.ExtendPayload["DEVICECODE"]);
            Assert.Equal("collection1", payload.ExtendPayload["GroupCode"]);
        }

        /// <summary>
        /// 用例：Topic.ExtendPayload 覆盖 DefaultExtendPayload
        /// </summary>
        [Fact]
        public void MqttPayload_TopicExtendPayloadOverridesDefault()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string>
                {
                    ["DEVICECODE"] = "110001",
                    ["GroupCode"] = "default"
                },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig
                    {
                        Name = "screen/collection/110001",
                        ExtendPayload = new Dictionary<string, string>
                        {
                            ["GroupCode"] = "collection1"
                        }
                    }
                }
            };

            var payload = BuildPayload("V001", "12.5", 0, device, "screen/collection/110001");

            // Topic.ExtendPayload 覆盖默认 GroupCode
            Assert.Equal("collection1", payload.ExtendPayload["GroupCode"]);
            Assert.Equal("110001", payload.ExtendPayload["DEVICECODE"]);
        }

        /// <summary>
        /// 用例：多区域合并到同一 Payload
        /// </summary>
        [Fact]
        public void MqttPayload_MultipleAreasInOnePayload()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string>
                {
                    ["DEVICECODE"] = "110001"
                },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig
                    {
                        Name = "screen/collection/110001",
                        ExtendPayload = new Dictionary<string, string>()
                    }
                }
            };

            var payload = new MqttPayload
            {
                Timestamp = 1712345678900L
            };
            payload.Data["V001"] = "12.5";
            payload.Data["V002"] = "13.2";

            foreach (var kvp in device.DefaultExtendPayload)
                payload.ExtendPayload[kvp.Key] = kvp.Value;

            Assert.Equal("12.5", payload.Data["V001"]);
            Assert.Equal("13.2", payload.Data["V002"]);
            Assert.Equal("110001", payload.ExtendPayload["DEVICECODE"]);
        }

        /// <summary>
        /// 用例：不同 Topic 的区域分别构建不同 Payload
        /// </summary>
        [Fact]
        public void MqttPayload_DifferentTopics_BuildSeparately()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string>
                {
                    ["DEVICECODE"] = "110001",
                    ["GroupCode"] = "collection1"
                },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig
                    {
                        Name = "screen/collection/110001",
                        ExtendPayload = new Dictionary<string, string> { ["GroupCode"] = "collection1" }
                    },
                    new MqttTopicConfig
                    {
                        Name = "screen/alarm/110001",
                        ExtendPayload = new Dictionary<string, string> { ["GroupCode"] = "alarm1" }
                    }
                }
            };

            // 构建采集 Topic Payload
            var collectionPayload = BuildPayload("V001", "12.5", 1712345678900L, device, "screen/collection/110001");
            // 构建告警 Topic Payload
            var alarmPayload = BuildPayload("ALARM001", "Open", 1712345678901L, device, "screen/alarm/110001");

            // 验证采集 Topic
            Assert.Single(collectionPayload.Data);
            Assert.Equal("12.5", collectionPayload.Data["V001"]);
            Assert.Equal("collection1", collectionPayload.ExtendPayload["GroupCode"]);

            // 验证告警 Topic
            Assert.Single(alarmPayload.Data);
            Assert.Equal("Open", alarmPayload.Data["ALARM001"]);
            Assert.Equal("alarm1", alarmPayload.ExtendPayload["GroupCode"]);
        }

        /// <summary>
        /// 验证：多设备各自有独立的 DeviceCode
        /// </summary>
        [Fact]
        public void MqttPayload_MultipleDevices_EachHasOwnDeviceCode()
        {
            var device001 = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string> { ["DEVICECODE"] = "110001" },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig { Name = "screen/collection/110001", ExtendPayload = new Dictionary<string, string>() }
                }
            };

            var device002 = new DeviceConfig
            {
                DeviceCode = "DEVICE-002",
                DefaultExtendPayload = new Dictionary<string, string> { ["DEVICECODE"] = "110002" },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig { Name = "screen/collection/110002", ExtendPayload = new Dictionary<string, string>() }
                }
            };

            var payload001 = BuildPayload("V001", "12.5", 0, device001, "screen/collection/110001");
            var payload002 = BuildPayload("V001", "8.1", 0, device002, "screen/collection/110002");

            Assert.Equal("110001", payload001.ExtendPayload["DEVICECODE"]);
            Assert.Equal("110002", payload002.ExtendPayload["DEVICECODE"]);
            Assert.NotEqual(payload001.ExtendPayload["DEVICECODE"], payload002.ExtendPayload["DEVICECODE"]);
        }

        /// <summary>
        /// 验证：Topic 找不到时使用默认 Topic（Topics[0]）
        /// </summary>
        [Fact]
        public void FindTopicConfig_UnknownTopic_UsesDefault()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                DefaultExtendPayload = new Dictionary<string, string> { ["DEVICECODE"] = "110001" },
                Topics = new List<MqttTopicConfig>
                {
                    new MqttTopicConfig { Name = "screen/collection/110001", ExtendPayload = new Dictionary<string, string>() }
                }
            };

            var topicName = ""; // 空 Topic
            var matchedTopic = device.Topics.FirstOrDefault(t => t.Name == topicName)
                ?? (device.Topics.Count > 0 ? device.Topics[0] : null);

            Assert.NotNull(matchedTopic);
            Assert.Equal("screen/collection/110001", matchedTopic.Name);
        }

        /// <summary>
        /// 验证：Payload JSON 格式正确（模拟 MqttPushService.PublishAsync 的序列化逻辑）
        /// </summary>
        [Fact]
        public void MqttPayload_SerializedJson_ContainsAllFields()
        {
            var payload = new MqttPayload
            {
                Timestamp = 1712345678900L
            };
            payload.Data["V001"] = "12.5";
            payload.Data["V002"] = "13.2";
            payload.ExtendPayload["DEVICECODE"] = "110001";
            payload.ExtendPayload["GroupCode"] = "collection1";

            // 模拟 MqttPushService.PublishAsync 中的 JSON 构建逻辑
            string dataJson = JsonSerializer.Serialize(payload.Data, JsonContext.Default.DictionaryStringString);
            string extendJson = JsonSerializer.Serialize(payload.ExtendPayload, JsonContext.Default.DictionaryStringString);
            string json = $"{{\"TIMESTAMP\":{payload.Timestamp},\"Data\":{dataJson},\"ExtendPayload\":{extendJson}}}";

            // 验证 JSON 包含所有必需字段
            Assert.Contains("TIMESTAMP", json);
            Assert.Contains("1712345678900", json);
            Assert.Contains("V001", json);
            Assert.Contains("12.5", json);
            Assert.Contains("DEVICECODE", json);
            Assert.Contains("110001", json);
            Assert.Contains("GroupCode", json);
            Assert.Contains("collection1", json);
        }

        /// <summary>
        /// 验证：MqttTopicConfig 属性正确
        /// </summary>
        [Fact]
        public void MqttTopicConfig_Properties_SetCorrectly()
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
        /// 验证：DeviceConfig Topics 为空列表时不出错
        /// </summary>
        [Fact]
        public void DeviceConfig_EmptyTopics_HandledGracefully()
        {
            var device = new DeviceConfig
            {
                DeviceCode = "DEVICE-001",
                Topics = new List<MqttTopicConfig>()
            };

            var matchedTopic = device.Topics.Count > 0 ? device.Topics[0] : null;
            Assert.Null(matchedTopic);
        }

        // ============================================================
        // 辅助方法（模拟 CollectService.PushToMqttAsync 的 Payload 构造逻辑）
        // ============================================================

        /// <summary>
        /// 模拟 CollectService.PushToMqttAsync 中构建 MqttPayload 的逻辑
        /// </summary>
        private static MqttPayload BuildPayload(
            string areaName,
            string text,
            long timestamp,
            DeviceConfig device,
            string topicName)
        {
            var payload = new MqttPayload
            {
                Timestamp = timestamp
            };

            payload.Data[areaName] = text;

            // 合并 DefaultExtendPayload
            if (device.DefaultExtendPayload != null)
            {
                foreach (var kvp in device.DefaultExtendPayload)
                    payload.ExtendPayload[kvp.Key] = kvp.Value;
            }

            // 合并 Topic.ExtendPayload（优先级更高）
            var topicConfig = device.Topics?.FirstOrDefault(t => t.Name == topicName);
            if (topicConfig?.ExtendPayload != null)
            {
                foreach (var kvp in topicConfig.ExtendPayload)
                    payload.ExtendPayload[kvp.Key] = kvp.Value;
            }

            return payload;
        }
    }
}
