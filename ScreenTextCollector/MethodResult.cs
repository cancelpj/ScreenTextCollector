using System;

namespace ScreenTextCollector
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

    public enum MethodResultType
    {
        Success,
        Error,
        Warning
    }
}