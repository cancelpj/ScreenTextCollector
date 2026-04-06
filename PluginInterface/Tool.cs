using Newtonsoft.Json;
using NLog;
using NLog.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private static AppSettings _settings;
        /// <summary>
        /// 应用程序配置（从 data/appsettings.json 加载）
        /// </summary>
        public static AppSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                    var configPath = Path.Combine(dataDir, "appsettings.json");

                    // 确保 data 目录存在
                    if (!Directory.Exists(dataDir))
                    {
                        Directory.CreateDirectory(dataDir);
                    }

                    // 兼容旧版本：如果新路径不存在，检查旧路径并迁移
                    if (!File.Exists(configPath))
                    {
                        var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                        if (File.Exists(oldPath))
                        {
                            // 从旧路径复制到新路径
                            File.Copy(oldPath, configPath);
                        }
                    }

                    if (File.Exists(configPath))
                    {
                        _settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(configPath));
                    }
                    else
                    {
                        _settings = new AppSettings();
                    }
                }
                return _settings;
            }
        }

        /// <summary>
        /// 重新加载配置（用于配置修改后刷新）
        /// </summary>
        public static void ReloadSettings()
        {
            _settings = null;
        }

        /// <summary>
        /// 保存配置到 data/appsettings.json
        /// </summary>
        public static void SaveSettings(AppSettings settings)
        {
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            var configPath = Path.Combine(dataDir, "appsettings.json");
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(configPath, json);
            ReloadSettings();
        }

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
        /// 校验屏幕配置的有效性（越界检查 + 分辨率检查）
        /// </summary>
        /// <param name="screenNumber">屏幕编号</param>
        /// <param name="errorMessage">校验失败时的错误信息</param>
        /// <returns>校验是否通过（越界/分辨率不匹配均返回 false）</returns>
        private static bool ValidateScreenConfig(int screenNumber, out string errorMessage)
        {
            errorMessage = null;

            // 1. 越界检查
            if (screenNumber < 0 || screenNumber >= Screen.AllScreens.Length)
            {
                errorMessage = $"屏幕 {screenNumber} 超出系统屏幕范围（0-{Screen.AllScreens.Length - 1}）";
                return false;
            }

            // 2. 分辨率检查：以 data/screenshot_{n}.png 为基准，与当前屏幕比较
            var refPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"screenshot_{screenNumber}.png");
            if (File.Exists(refPath))
            {
                try
                {
                    using (var refImg = Image.FromFile(refPath))
                    {
                        var currentBounds = Screen.AllScreens[screenNumber].Bounds;
                        if (refImg.Width != currentBounds.Width || refImg.Height != currentBounds.Height)
                        {
                            errorMessage = $"屏幕 {screenNumber} 分辨率不匹配"
                                + $"（配置: {refImg.Width}×{refImg.Height}，当前: {currentBounds.Width}×{currentBounds.Height}）";
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"屏幕 {screenNumber} 读取截图失败，分辨率检查跳过: {ex.Message}");
                }
            }

            return true;
        }

        /// <summary>
        /// 截取指定屏幕
        /// </summary>
        /// <param name="screenNumber">屏幕编号，从 0 开始</param>
        /// <returns>截图保存路径（通过 Data 属性返回）</returns>
        private static MethodResult<string> CaptureScreen(int screenNumber = 0)
        {
            try
            {
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // 文件名包含屏幕编号，便于调试
                var path = Path.Combine(tempDir, $"{DateTime.Now:yyyyMMddHHmmssfff}_S{screenNumber}.png");

                // 屏幕越界检查
                if (screenNumber < 0 || screenNumber >= Screen.AllScreens.Length)
                {
                    return new MethodResult<string>($"屏幕编号 {screenNumber} 超出范围（0-{Screen.AllScreens.Length - 1}）", MethodResultType.Error);
                }

                Screen screen = Screen.AllScreens[screenNumber];
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

                return new MethodResult<string>(path, "截屏成功", MethodResultType.Success);
            }
            catch (Exception ex)
            {
                return new MethodResult<string>("截屏失败", ex);
            }
        }

        /// <summary>
        /// 执行截屏并进行图像校验，成功时返回截图路径
        /// </summary>
        /// <param name="verifyImage">图像校验委托</param>
        /// <param name="verificationAreas">验证区域列表（将按 ScreenNumber 筛选）</param>
        /// <param name="screenNumber">屏幕编号</param>
        /// <returns>校验成功的截图路径（通过 Data 属性返回）</returns>
        private static MethodResult<string> CaptureAndVerify(
            Func<string, List<ImageVerificationArea>, bool> verifyImage,
            List<ImageVerificationArea> verificationAreas,
            int screenNumber)
        {
            // 筛选属于该屏幕的验证区域
            var screenVerificationAreas = verificationAreas?.FindAll(a => a.ScreenNumber == screenNumber);
            if (screenVerificationAreas == null || screenVerificationAreas.Count == 0)
            {
                return new MethodResult<string>($"未找到图像校验区域配置", MethodResultType.Error);
            }

            var ret = CaptureScreen(screenNumber);
            if (ret.ResultType != MethodResultType.Success)
                return ret;

            if (!verifyImage(ret.Data, screenVerificationAreas))
            {
                File.Delete(ret.Data);
                return new MethodResult<string>("未监测到程序画面", MethodResultType.Warning);
            }

            return ret;
        }

        /// <summary>
        /// 处理屏幕截图，返回包含 Topic 映射的采集结果
        /// </summary>
        /// <param name="screenShotPath">已验证的屏幕截图文件路径</param>
        /// <param name="performOcr">OCR识别委托</param>
        /// <param name="screenNumber">屏幕编号（用于筛选对应屏幕的区域）</param>
        /// <returns>采集结果，包含数据字典和 Topic 映射</returns>
        private static MethodResult<CollectionResult> ProcessScreenCaptureWithTopic(string screenShotPath,
            Func<string, ImageCollectionArea, string> performOcr, List<ImageCollectionArea> collectionAreas, int screenNumber = 0)
        {
            try
            {
                // 筛选属于该屏幕的采集区域
                var screenCollectionAreas = collectionAreas?.FindAll(a => a.ScreenNumber == screenNumber);
                if (screenCollectionAreas == null || screenCollectionAreas.Count == 0)
                {
                    return new MethodResult<CollectionResult>($"未找到图像采集区域配置", MethodResultType.Error);
                }

                // 图像采集
                var data = new ConcurrentDictionary<string, string>();
                var topicMap = new ConcurrentDictionary<string, string>();
                Parallel.ForEach(screenCollectionAreas, area =>
                {
                    data[area.Name] = performOcr(screenShotPath, area);
                    if (!string.IsNullOrEmpty(area.Topic))
                        topicMap[area.Name] = area.Topic;
                });

                // 保存本地日志
                if (Settings.CsvRecord)
                    SaveToCsv(data);

                // 汇总结果
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
            finally
            {
                // 确保截图文件被清理
                try { File.Delete(screenShotPath); } catch { }
            }
        }

        /// <summary>
        /// 处理多屏幕截图，批量截取所有屏幕并处理 OCR
        /// </summary>
        /// <param name="performOcr">OCR识别委托</param>
        /// <param name="verifyImage">图像校验委托（必须提供）</param>
        /// <returns>所有屏幕的采集结果汇总</returns>
        public static MethodResult<CollectionResult> ProcessMultiScreenCapture(
            Func<string, ImageCollectionArea, string> performOcr,
            Func<string, List<ImageVerificationArea>, bool> verifyImage)
        {
            if (verifyImage == null)
                throw new ArgumentNullException(nameof(verifyImage));

            try
            {
                var allData = new ConcurrentDictionary<string, string>();
                var allTopicMap = new ConcurrentDictionary<string, string>();

                // 获取所有验证区域和采集区域
                var verificationAreas = CaptureSettings.VerificationAreas ?? new List<ImageVerificationArea>();
                var collectionAreas = CaptureSettings.CollectionAreas ?? new List<ImageCollectionArea>();

                // 获取所有需要处理的屏幕编号（验证区域和采集区域都可能分布在不同屏幕）
                var allScreenNumbers = verificationAreas.Select(a => a.ScreenNumber)
                    .Concat(collectionAreas.Select(a => a.ScreenNumber))
                    .Distinct();

                foreach (var screenNumber in allScreenNumbers)
                {
                    // 越界和分辨率校验
                    if (!ValidateScreenConfig(screenNumber, out string errorMsg))
                    {
                        Log.Error(errorMsg + "，跳过");
                        continue;
                    }

                    // 调用 CaptureAndVerify 进行截屏和图像校验
                    var captureResult = CaptureAndVerify(verifyImage, verificationAreas, screenNumber);
                    if (captureResult.ResultType != MethodResultType.Success)
                    {
                        Log.Warn($"屏幕 {screenNumber}: {captureResult.Message}");
                        continue;
                    }
                    string screenshotPath = captureResult.Data;

                    // 调用 ProcessScreenCaptureWithTopic 执行 OCR（会在 finally 中删除截图）
                    var ocrResult = ProcessScreenCaptureWithTopic(screenshotPath, performOcr, collectionAreas, screenNumber);
                    if (ocrResult.ResultType == MethodResultType.Success)
                    {
                        // 合并结果
                        foreach (var kvp in ocrResult.Data.Data)
                        {
                            allData[kvp.Key] = kvp.Value;
                        }
                        foreach (var kvp in ocrResult.Data.TopicMap)
                        {
                            allTopicMap[kvp.Key] = kvp.Value;
                        }
                    }
                }

                // 汇总结果
                var multiScreenResult = new CollectionResult
                {
                    Data = new Dictionary<string, string>(allData),
                    TopicMap = new Dictionary<string, string>(allTopicMap)
                };

                Log.Info("多屏幕识别结果：{0}", JsonConvert.SerializeObject(multiScreenResult.Data));
                return new MethodResult<CollectionResult>(multiScreenResult, MethodResultType.Success.ToString(), MethodResultType.Success);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理多屏幕截屏失败");
                return new MethodResult<CollectionResult>("处理多屏幕截屏失败", ex);
            }
        }

        /// <summary>
        /// 处理单个采集区域的 OCR
        /// </summary>
        /// <param name="screenShotPath">屏幕截图路径</param>
        /// <param name="areaName">采集区域名称</param>
        /// <param name="performOcr">OCR 识别委托</param>
        /// <returns>MethodResult，单区域采集结果</returns>
        public static MethodResult ProcessScreenCaptureSingle(Func<string, ImageCollectionArea, string> performOcr,
            Func<string, List<ImageVerificationArea>, bool> verifyImage, string areaName)
        {
            var area = CaptureSettings.CollectionAreas?.Find(a => a.Name == areaName);
            if (area == null)
            {
                return new MethodResult($"未找到采集区域: {areaName}", MethodResultType.Error);
            }

            // 越界和分辨率校验
            if (!ValidateScreenConfig(area.ScreenNumber, out string errorMsg))
            {
                return new MethodResult(errorMsg, MethodResultType.Error);
            }

            var captureResult = CaptureAndVerify(verifyImage, CaptureSettings.VerificationAreas, area.ScreenNumber);
            if (captureResult.ResultType != MethodResultType.Success)
            {
                Tool.OutputMessage(captureResult);
                return captureResult;
            }

            string screenshotPath = captureResult.Data;
            try
            {
                string result = performOcr(screenshotPath, area);
                Log.Info("单个区域识别结果 [{0}]: {1}", areaName, result);

                var singleResult = new Dictionary<string, string> { { areaName, result } };

                if (Settings.CsvRecord)
                {
                    SaveToCsv(singleResult);
                }

                return new MethodResult(result, MethodResultType.Success);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理单区域截屏失败");
                return new MethodResult("处理截屏失败", ex);
            }
            finally
            {
                // 确保截图文件被清理
                try { File.Delete(screenshotPath); } catch { }
            }
        }

        /// <summary>
        /// 保存结果到 CSV 文件
        /// 每行格式：时间戳, 采集项名称, 值
        /// </summary>
        /// <param name="results">采集结果</param>
        public static void SaveToCsv(IDictionary<string, string> results)
        {
            var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var csvPath = Path.Combine(saveDir, $"{DateTime.Now:yyyyMMdd}.csv");

            using (var writer = new StreamWriter(csvPath, true, Encoding.UTF8))
            {
                foreach (var item in results)
                {
                    writer.WriteLine($"{timestamp},{item.Key},{item.Value}");
                }
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