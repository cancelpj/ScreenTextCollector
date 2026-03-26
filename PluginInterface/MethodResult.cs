using System;

namespace PluginInterface
{
    public class MethodResult
    {
        public MethodResultType ResultType { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }

        public MethodResult()
        {
            ResultType = MethodResultType.Success;
            Message = string.Empty;
            Exception = null;
        }

        public MethodResult(string message)
        {
            Message = message;
            ResultType = MethodResultType.Warning;
        }

        public MethodResult(string message, MethodResultType resultType) : this(message)
        {
            ResultType = resultType;
        }

        public MethodResult(string message, Exception exception) : this(message)
        {
            Exception = exception;
            ResultType = MethodResultType.Error;
        }
    }

    /// <summary>
    /// 泛型方法结果封装
    /// </summary>
    /// <typeparam name="T">结果数据类型</typeparam>
    public class MethodResult<T> : MethodResult
    {
        public T Data { get; set; }

        public MethodResult()
        {
            Data = default;
        }

        public MethodResult(T data)
        {
            Data = data;
            ResultType = MethodResultType.Success;
            Message = string.Empty;
        }

        public MethodResult(string message, MethodResultType resultType) : base(message, resultType)
        {
            Data = default;
        }

        public MethodResult(string message, Exception exception) : base(message, exception)
        {
            Data = default;
        }

        public MethodResult(T data, string message, MethodResultType resultType) : base(message, resultType)
        {
            Data = data;
        }
    }

    public enum MethodResultType
    {
        Success,
        Error,
        Warning
    }
}