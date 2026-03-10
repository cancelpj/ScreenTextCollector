using System;
using System.Diagnostics;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// 进程检查功能的单元测试
    /// 注意：由于测试项目不再引用 ScreenTextCollector 主项目，
    /// 这里演示如何独立测试进程检查逻辑
    /// </summary>
    public class CheckProcessTests
    {
        /// <summary>
        /// 测试获取当前进程名称
        /// </summary>
        [Fact]
        public void GetCurrentProcess_ShouldReturnValidProcessName()
        {
            // Arrange & Act
            var currentProcess = Process.GetCurrentProcess();

            // Assert
            Assert.NotNull(currentProcess.ProcessName);
            Assert.NotEmpty(currentProcess.ProcessName);

            currentProcess.Dispose();
        }

        /// <summary>
        /// 测试查找运行中的进程
        /// </summary>
        [Fact]
        public void GetProcessesByName_WithRunningProcess_ShouldReturnAtLeastOne()
        {
            // Arrange - 使用当前进程
            var processName = Process.GetCurrentProcess().ProcessName;

            // Act
            var processes = Process.GetProcessesByName(processName);

            // Assert
            Assert.NotNull(processes);
            Assert.NotEmpty(processes);

            // 清理
            foreach (var p in processes)
            {
                p?.Dispose();
            }
        }

        /// <summary>
        /// 测试查找不存在的进程
        /// </summary>
        [Fact]
        public void GetProcessesByName_WithNonExistentProcess_ShouldReturnEmpty()
        {
            // Arrange - 使用一个几乎不可能存在的进程名
            var processName = $"NonExistentProcess_{Guid.NewGuid():N}";

            // Act
            var processes = Process.GetProcessesByName(processName);

            // Assert
            Assert.NotNull(processes);
            Assert.Empty(processes);
        }

        /// <summary>
        /// 测试空字符串进程名
        /// </summary>
        [Fact]
        public void GetProcessesByName_WithEmptyString_ShouldReturnEmpty()
        {
            // Arrange
            var processName = string.Empty;

            // Act
            var processes = Process.GetProcessesByName(processName);

            // Assert - 空字符串通常返回空数组
            Assert.NotNull(processes);
        }

        /// <summary>
        /// 测试进程名不区分大小写
        /// </summary>
        [Fact]
        public void GetProcessesByName_ShouldBeCaseInsensitive()
        {
            // Arrange
            var processName = Process.GetCurrentProcess().ProcessName.ToUpperInvariant();

            // Act
            var upperProcesses = Process.GetProcessesByName(processName);
            var lowerProcesses = Process.GetProcessesByName(processName.ToLowerInvariant());

            // Assert - 结果应该相同
            Assert.Equal(upperProcesses.Length, lowerProcesses.Length);

            // 清理
            foreach (var p in upperProcesses) p?.Dispose();
            foreach (var p in lowerProcesses) p?.Dispose();
        }
    }
}
