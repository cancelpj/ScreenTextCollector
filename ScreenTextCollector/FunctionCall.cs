using System.Diagnostics;
using PluginInterface;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        #region 业务功能

        private static MethodResult ScreenTextCollect()
        {
            var ret = Tool.GetScreenCapture();
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
                return ret;
            }

            ret = Tool.ProcessScreenCapture(ret.Message, OcrService.VerifyImage, OcrService.PerformOcr);
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
            }

            return ret;
        }

        /// <summary>
        /// 按 processName 检查进程状态
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static string CheckProcess(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0 ? "Running" : "Standby";
            }
            finally
            {
                // 释放进程句柄
                foreach (var p in processes)
                {
                    p?.Dispose();
                }
            }
        }

        #endregion
    }
}