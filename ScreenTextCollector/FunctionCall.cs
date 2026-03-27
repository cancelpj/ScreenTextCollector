using System.Diagnostics;
using PluginInterface;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        #region 业务功能

        /// <summary>
        /// 执行屏幕文本采集
        /// </summary>
        /// <param name="areaName">采集区域名称，为空时采集所有区域</param>
        /// <returns>采集结果</returns>
        private static MethodResult ScreenTextCollect(string areaName = null)
        {
            var ret = Tool.CaptureAndVerify(OcrService.VerifyImage, Tool.CaptureSettings.VerificationAreas);
            if (ret.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(ret);
                return ret;
            }

            if (string.IsNullOrEmpty(areaName))
            {
                ret = Tool.ProcessScreenCapture(ret.Message, OcrService.PerformOcr);
            }
            else
            {
                ret = Tool.ProcessScreenCaptureSingle(ret.Message, areaName, OcrService.PerformOcr);
            }

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