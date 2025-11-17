using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PluginInterface
{
    public static class Tool
    {
        public static readonly Settings Settings =
            JsonConvert.DeserializeObject<Settings>(File.ReadAllText("appsettings.json"));

        public static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 保存设置
        /// </summary>
        /// <returns></returns>
        public static MethodResult SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                //复制文件备份
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var bakFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"appsettings-{DateTime.Now:yyyyMMddHHmmssfff}.json");
                if (File.Exists(filePath))
                    File.Copy(filePath, bakFilePath, true);
                File.WriteAllText("appsettings.json", json);
            }
            catch (Exception ex)
            {
                var result = new MethodResult("保存设置失败", ex);
                OutputMessage(result);
            }

            return new MethodResult();
        }

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

                if (Screen.AllScreens.Length - 1 < Settings.ScreenNumber)
                {
                    return new MethodResult("屏幕编号超出范围");
                }

                Screen screen = Screen.AllScreens[Settings.ScreenNumber];
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

        public static MethodResult ProcessScreenCapture(string screenShotPath, Func<string, List<ImageVerificationArea>, bool> verifyImage,
            Func<string, ImageCollectionArea, string> performOcr)
        {
            try
            {
                // 图像校验
                if (!verifyImage(screenShotPath, Settings.ImageVerificationAreas))
                {
                    File.Delete(screenShotPath);
                    return new MethodResult("未监测到程序画面", MethodResultType.Warning);
                }

                // 图像采集
                var data = new Dictionary<string, string>();
                Parallel.ForEach(Settings.ImageCollectionAreas, area =>
                {
                    var text = performOcr(screenShotPath, area);
                    data[area.Name] = text;
                });

                // 保存本地日志
                if (Settings.CsvRecord)
                {
                    SaveToCsv(data);
                }

                // 汇总结果
                File.Delete(screenShotPath);
                var result = JsonConvert.SerializeObject(data);
                Log.Info("识别结果：{0}", result);
                return new MethodResult(result, MethodResultType.Success);
            }
            catch (Exception ex)
            {
                //File.Delete(screenShotPath);
                Log.Error(ex, "处理截屏失败");
                return new MethodResult("处理截屏失败", ex);
            }
        }

        /// <summary>
        /// 保存结果到 CSV 文件
        /// 每个采集项保存在一行（名称, 值）
        /// </summary>
        /// <param name="results">采集结果</param>
        public static void SaveToCsv(Dictionary<string, string> results)
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
    }
}