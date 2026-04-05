using System.Diagnostics;
using PluginInterface;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        #region 业务功能

        /// <summary>
        /// 执行屏幕文本采集（多屏幕支持）
        /// </summary>
        /// <param name="areaName">采集区域名称，为空时采集所有区域</param>
        /// <returns>采集结果</returns>
        private static MethodResult ScreenTextCollect(string areaName = null)
        {
            if (string.IsNullOrEmpty(areaName))
            {
                // 多屏幕批量采集（包含图像校验）
                var ret = Tool.ProcessMultiScreenCapture(OcrService.PerformOcr, OcrService.VerifyImage);
                if (ret.ResultType != MethodResultType.Success)
                {
                    Tool.OutputMessage(ret);
                    return ret;
                }

                // 返回所有采集结果
                return new MethodResult(
                    Newtonsoft.Json.JsonConvert.SerializeObject(ret.Data.Data),
                    MethodResultType.Success);
            }
            else
            {
                var ret = Tool.ProcessScreenCaptureSingle(OcrService.PerformOcr, OcrService.VerifyImage, areaName);
                if (ret.ResultType != MethodResultType.Success)
                {
                    Tool.OutputMessage(ret);
                }
                return ret;
            }
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