using Newtonsoft.Json;
using NLog;
using NLog.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PluginInterface
{
    /// <summary>
    /// 采集结果封装类
    /// </summary>
    public class CollectionResult
    {
        /// <summary>
        /// 采集结果数据（区域名称 -> OCR 识别结果）
        /// </summary>
        public Dictionary<string, string> Data { get; set; }

        /// <summary>
        /// Topic 映射（区域名称 -> 自定义 MQTT Topic）
        /// </summary>
        public Dictionary<string, string> TopicMap { get; set; }
    }

    public static class Tool
    {
        /// <summary>
        /// 应用程序配置（从 appsettings.json 加载）
        /// </summary>
        public static readonly AppSettings Settings =
            JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText("appsettings.json"));

        /// <summary>
        /// 屏幕采集配置（从 data/CaptureSettings.json 加载）
        /// 由 LabelTool 生成和维护
        /// </summary>
        public static readonly CaptureSettings CaptureSettings =
            File.Exists(Path.Combine("data", "CaptureSettings.json"))
                ? JsonConvert.DeserializeObject<CaptureSettings>(File.ReadAllText(Path.Combine("data", "CaptureSettings.json")))
                : new CaptureSettings();

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        #region 让Nlog写日志的同时可以广播到UI

        // 事件：外部（如 UI）可以订阅以接收日志消息
        public static event Action<LogLevel, string> LogReceived;

        // 静态构造：注册自定义 NLog 目标（GUI 目标），使所有日志都可以被广播
        static Tool()
        {
            try
            {
                var config = LogManager.Configuration ?? new LoggingConfiguration();
                // 如果已存在同名目标，先移除（避免重复添加）
                if (config.FindTargetByName("Gui") == null)
                {
                    var guiTarget = new NLogGuiTarget { Name = "Gui", Layout = "${message}" };
                    config.AddTarget("Gui", guiTarget);
                    var rule = new LoggingRule("*", LogLevel.Debug, guiTarget);
                    config.LoggingRules.Add(rule);
                    LogManager.Configuration = config;
                }
            }
            catch
            {
                // 静态初始化不应抛异常（保持容错）
            }
        }

        internal static void RaiseLog(LogLevel level, string message)
        {
            LogReceived?.Invoke(level, message);
        }

        #endregion

        /// <summary>
        /// 截屏
        /// </summary>
        /// <returns>截图保存路径</returns>
        public static MethodResult GetScreenCapture()
        {
            try
            {
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var path = Path.Combine(tempDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");

                if (Screen.AllScreens.Length - 1 < CaptureSettings.ScreenNumber)
                {
                    return new MethodResult("屏幕编号超出范围");
                }

                Screen screen = Screen.AllScreens[CaptureSettings.ScreenNumber];
                Rectangle bounds = screen.Bounds;
                using (var screenShot = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(screenShot))
                    {
                        // 截屏
                        g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);

                        // 保存图片
                        screenShot.Save(path);
                    }
                }

                return new MethodResult(path, MethodResultType.Success);
            }
            catch (Exception ex)
            {
                return new MethodResult("截屏失败", ex);
            }
        }

        /// <summary>
        /// 处理屏幕截图，执行图像校验和OCR文字采集
        /// </summary>
        /// <param name="screenShotPath">屏幕截图文件路径</param>
        /// <param name="verifyImage">图像校验委托，用于验证截图是否符合预期条件</param>
        /// <param name="performOcr">OCR识别委托，用于从图像采集区域提取文字</param>
        /// <returns>处理结果。成功时返回JSON格式的采集数据；校验失败时返回警告信息；异常时返回错误信息</returns>
        public static MethodResult ProcessScreenCapture(string screenShotPath, Func<string, List<ImageVerificationArea>, bool> verifyImage,
            Func<string, ImageCollectionArea, string> performOcr)
        {
            var ret = ProcessScreenCaptureWithTopic(screenShotPath, verifyImage, performOcr);
            if (ret.ResultType == MethodResultType.Success)
            {
                return new MethodResult(JsonConvert.SerializeObject(ret.Data.Data), MethodResultType.Success);
            }
            return new MethodResult(ret.Message, ret.ResultType);
        }

        /// <summary>
        /// 处理屏幕截图，返回包含 Topic 映射的采集结果
        /// </summary>
        /// <param name="screenShotPath">屏幕截图文件路径</param>
        /// <param name="verifyImage">图像校验委托</param>
        /// <param name="performOcr">OCR识别委托</param>
        /// <returns>采集结果，包含数据字典和 Topic 映射</returns>
        public static MethodResult<CollectionResult> ProcessScreenCaptureWithTopic(string screenShotPath,
            Func<string, List<ImageVerificationArea>, bool> verifyImage,
            Func<string, ImageCollectionArea, string> performOcr)
        {
            try
            {
                // 图像校验
                if (!verifyImage(screenShotPath, CaptureSettings.VerificationAreas))
                {
                    File.Delete(screenShotPath);
                    return new MethodResult<CollectionResult>("未监测到程序画面", MethodResultType.Warning);
                }

                // 图像采集
                var data = new ConcurrentDictionary<string, string>();
                var topicMap = new ConcurrentDictionary<string, string>();
                Parallel.ForEach(CaptureSettings.CollectionAreas, area =>
                {
                    data[area.Name] = performOcr(screenShotPath, area);
                    if (!string.IsNullOrEmpty(area.Topic))
                        topicMap[area.Name] = area.Topic;
                });

                // 保存本地日志
                if (Settings.CsvRecord)
                    SaveToCsv(data);

                // 汇总结果
                File.Delete(screenShotPath);
                var result = new CollectionResult
                {
                    Data = new Dictionary<string, string>(data),
                    TopicMap = new Dictionary<string, string>(topicMap)
                };
                Log.Info("识别结果：{0}", JsonConvert.SerializeObject(result.Data));
                return new MethodResult<CollectionResult>(result, MethodResultType.Success.ToString(), MethodResultType.Success);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理截屏失败");
                return new MethodResult<CollectionResult>("处理截屏失败", ex);
            }
        }

        /// <summary>
        /// 保存结果到 CSV 文件
        /// 每个采集项保存在一行（名称, 值）
        /// </summary>
        /// <param name="results">采集结果</param>
        public static void SaveToCsv(IDictionary<string, string> results)
        {
            var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            var date = DateTime.Now.ToString("yyyyMMdd");
            var csvPath = Path.Combine(saveDir, $"{date}.csv");
            var isNewFile = !File.Exists(csvPath);

            using (var writer = new StreamWriter(csvPath, true, Encoding.UTF8))
            {
                if (isNewFile)
                {
                    writer.WriteLine(string.Join(",", results.Keys));
                }

                writer.WriteLine(string.Join(",", results.Values));
            }
        }

        /// <summary>
        /// 输出消息
        /// </summary>
        /// <param name="result">方法结果消息</param>
        /// <param name="func">处理消息的委托</param>
        /// <exception cref="ArgumentOutOfRangeException">结果类型不在预期范围内</exception>
        public static void OutputMessage(MethodResult result, Func<string, MethodResult> func = null)
        {
            switch (result.ResultType)
            {
                case MethodResultType.Success:
                    Log.Info(result.Message);
                    break;
                case MethodResultType.Warning:
                    Log.Warn(result.Message);
                    break;
                case MethodResultType.Error:
                    if (result.Exception != null)
                        Log.Error(result.Exception, result.Message);
                    else
                        Log.Error(result.Message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            func?.Invoke(result.Message);
        }

        /// <summary>
        /// 后处理 OCR 识别结果
        /// </summary>
        /// <param name="text">OCR 原始文本</param>
        /// <param name="area">采集区域配置</param>
        /// <returns>处理后的文本</returns>
        public static string PostProcessText(string text, ImageCollectionArea area)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // 移除常见 OCR 错误字符
            text = text.Replace("|", "")
                       .Replace("_", "")
                       .Replace("—", "-")
                       .Replace("'", "")
                       .Replace("`", "")
                       .Replace("°", "")
                       .Replace("(", "")
                       .Replace(")", "")
                       .Replace("[", "")
                       .Replace("]", "")
                       .Replace("{", "")
                       .Replace("}", "");

            if (text == "Aut o") text = "Auto";
            if (text == "0pen") text = "Open";

            return text;
        }
    }
}