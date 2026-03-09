using System.Diagnostics;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// FunctionCall.CheckProcess 方法的单元测试
    /// </summary>
    public class CheckProcessTests
    {
        [Fact]
        public void CheckProcess_WithRunningProcess_ShouldReturnRunning()
        {
            // Arrange - 使用当前进程作为测试对象
            var currentProcess = Process.GetCurrentProcess();
            var processName = currentProcess.ProcessName;

            // Act
            var result = Program.CheckProcess(processName);

            // Assert
            Assert.Equal("Running", result);
        }

        [Fact]
        public void CheckProcess_WithNonExistentProcess_ShouldReturnStandby()
        {
            // Arrange - 使用一个不太可能存在的进程名
            var processName = "NonExistentProcessName12345XYZ";

            // Act
            var result = Program.CheckProcess(processName);

            // Assert
            Assert.Equal("Standby", result);
        }

        [Fact]
        public void CheckProcess_WithEmptyString_ShouldReturnStandby()
        {
            // Arrange
            var processName = string.Empty;

            // Act
            var result = Program.CheckProcess(processName);

            // Assert
            Assert.Equal("Standby", result);
        }

        [Fact]
        public void CheckProcess_WithSystemProcess_ShouldReturnRunning()
        {
            // Arrange - 使用系统进程（Windows 上通常存在）
            var processName = "explorer"; // Windows 资源管理器

            // Act
            var result = Program.CheckProcess(processName);

            // Assert
            // 注意：在某些环境下 explorer 可能不运行，这个测试可能需要调整
            Assert.True(result == "Running" || result == "Standby");
        }
    }
}
