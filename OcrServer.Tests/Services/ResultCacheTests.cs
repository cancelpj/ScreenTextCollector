using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OcrServer.Services;
using Xunit;

namespace OcrServer.Tests.Services
{
    /// <summary>
    /// ResultCache 测试：验证按 DeviceCode 隔离的缓存读写
    /// </summary>
    public class ResultCacheTests
    {
        /// <summary>
        /// 用例：DeviceCode 隔离
        /// DEVICE-001 的 V001 不覆盖 DEVICE-002 的 V001
        /// </summary>
        [Fact]
        public void Set_SameAreaNameDifferentDevice_AreIsolated()
        {
            // Arrange
            var cache = new ResultCache();

            // Act：两个设备写入相同区域名 V001
            cache.Set("DEVICE-001", "V001", "12.5");
            cache.Set("DEVICE-002", "V001", "8.1");

            // Assert：相同区域名，但设备不同，结果独立
            Assert.Equal("12.5", cache.Get("DEVICE-001", "V001"));
            Assert.Equal("8.1", cache.Get("DEVICE-002", "V001"));
            Assert.NotEqual(cache.Get("DEVICE-001", "V001"), cache.Get("DEVICE-002", "V001"));
        }

        /// <summary>
        /// 用例：同一设备内多区域存储
        /// </summary>
        [Fact]
        public void Set_SingleDeviceMultipleAreas_StoredSeparately()
        {
            var cache = new ResultCache();

            cache.Set("DEVICE-001", "V001", "12.5");
            cache.Set("DEVICE-001", "V002", "13.2");
            cache.Set("DEVICE-001", "V003", "-0.9");

            var all = cache.GetDevice("DEVICE-001");

            Assert.Equal(3, all.Count);
            Assert.Equal("12.5", all["V001"]);
            Assert.Equal("13.2", all["V002"]);
            Assert.Equal("-0.9", all["V003"]);
        }

        /// <summary>
        /// 验证：获取不存在的设备返回空字符串
        /// </summary>
        [Fact]
        public void Get_NonExistentDevice_ReturnsEmpty()
        {
            var cache = new ResultCache();
            cache.Set("DEVICE-001", "V001", "12.5");

            Assert.Equal("", cache.Get("DEVICE-002", "V001"));
            Assert.Equal("", cache.Get("DEVICE-001", "V002")); // 设备存在，但区域不存在
        }

        /// <summary>
        /// 用例：线程安全并发写入
        /// 多个线程同时向同一设备写入不同区域，结果应全部保留
        /// </summary>
        [Fact]
        public void Set_ConcurrentWrites_AllValuesPreserved()
        {
            var cache = new ResultCache();
            var tasks = new List<Task>();
            var expectedCount = 100;

            // Act：100 个线程同时写入同一设备的不同区域
            for (int i = 0; i < expectedCount; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    cache.Set("DEVICE-001", $"V{index:D3}", $"{index}.0");
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert：100 个区域全部写入成功
            var all = cache.GetDevice("DEVICE-001");
            Assert.Equal(expectedCount, all.Count);

            // 验证部分值
            Assert.Equal("0.0", all["V000"]);
            Assert.Equal("50.0", all["V050"]);
            Assert.Equal("99.0", all["V099"]);
        }

        /// <summary>
        /// 验证：多设备并发写入，结果互不干扰
        /// </summary>
        [Fact]
        public void Set_ConcurrentMultiDeviceWrites_AllDevicesPreserved()
        {
            var cache = new ResultCache();
            var tasks = new List<Task>();
            var deviceCount = 10;
            var areasPerDevice = 10;

            for (int d = 0; d < deviceCount; d++)
            {
                var deviceCode = $"DEVICE-{d:D3}";
                for (int a = 0; a < areasPerDevice; a++)
                {
                    var areaName = $"V{a:D3}";
                    var value = $"{d}.{a}";
                    tasks.Add(Task.Run(() => cache.Set(deviceCode, areaName, value)));
                }
            }

            Task.WaitAll(tasks.ToArray());

            // Assert：每个设备都有完整的 10 个区域
            for (int d = 0; d < deviceCount; d++)
            {
                var deviceCode = $"DEVICE-{d:D3}";
                var all = cache.GetDevice(deviceCode);
                Assert.Equal(areasPerDevice, all.Count);

                for (int a = 0; a < areasPerDevice; a++)
                {
                    Assert.Equal($"{d}.{a}", all[$"V{a:D3}"]);
                }
            }
        }

        /// <summary>
        /// 验证：Clear 清空所有设备数据
        /// </summary>
        [Fact]
        public void Clear_RemovesAllData()
        {
            var cache = new ResultCache();
            cache.Set("DEVICE-001", "V001", "12.5");
            cache.Set("DEVICE-002", "V001", "8.1");

            cache.Clear();

            Assert.Equal("", cache.Get("DEVICE-001", "V001"));
            Assert.Equal("", cache.Get("DEVICE-002", "V001"));
        }

        /// <summary>
        /// 验证：覆盖已有值
        /// </summary>
        [Fact]
        public void Set_OverwritesExistingValue()
        {
            var cache = new ResultCache();
            cache.Set("DEVICE-001", "V001", "12.5");
            cache.Set("DEVICE-001", "V001", "13.0"); // 覆盖

            Assert.Equal("13.0", cache.Get("DEVICE-001", "V001"));
            Assert.Single(cache.GetDevice("DEVICE-001"));
        }

        /// <summary>
        /// 验证：空区域名场景
        /// </summary>
        [Fact]
        public void Set_EmptyAreaName_StoredCorrectly()
        {
            var cache = new ResultCache();
            cache.Set("DEVICE-001", "", "empty_area");

            Assert.Equal("empty_area", cache.Get("DEVICE-001", ""));
        }
    }
}
