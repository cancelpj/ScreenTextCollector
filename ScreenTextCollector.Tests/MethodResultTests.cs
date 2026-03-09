using System;
using PluginInterface;
using Xunit;

namespace ScreenTextCollector.Tests
{
    /// <summary>
    /// MethodResult 类的单元测试
    /// </summary>
    public class MethodResultTests
    {
        [Fact]
        public void Constructor_Default_ShouldSetSuccessType()
        {
            // Arrange & Act
            var result = new MethodResult();

            // Assert
            Assert.Equal(MethodResultType.Success, result.ResultType);
            Assert.Equal(string.Empty, result.Message);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void Constructor_WithMessage_ShouldSetWarningType()
        {
            // Arrange
            var message = "测试警告消息";

            // Act
            var result = new MethodResult(message);

            // Assert
            Assert.Equal(MethodResultType.Warning, result.ResultType);
            Assert.Equal(message, result.Message);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void Constructor_WithMessageAndType_ShouldSetSpecifiedType()
        {
            // Arrange
            var message = "测试错误消息";
            var type = MethodResultType.Error;

            // Act
            var result = new MethodResult(message, type);

            // Assert
            Assert.Equal(type, result.ResultType);
            Assert.Equal(message, result.Message);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void Constructor_WithMessageAndException_ShouldSetErrorType()
        {
            // Arrange
            var message = "发生异常";
            var exception = new InvalidOperationException("测试异常");

            // Act
            var result = new MethodResult(message, exception);

            // Assert
            Assert.Equal(MethodResultType.Error, result.ResultType);
            Assert.Equal(message, result.Message);
            Assert.Equal(exception, result.Exception);
        }

        [Fact]
        public void Properties_CanBeSetDirectly()
        {
            // Arrange
            var result = new MethodResult();
            var newMessage = "新消息";
            var newException = new Exception("新异常");

            // Act
            result.Message = newMessage;
            result.ResultType = MethodResultType.Error;
            result.Exception = newException;

            // Assert
            Assert.Equal(newMessage, result.Message);
            Assert.Equal(MethodResultType.Error, result.ResultType);
            Assert.Equal(newException, result.Exception);
        }
    }
}
