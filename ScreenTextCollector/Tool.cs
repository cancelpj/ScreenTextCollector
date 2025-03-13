using Newtonsoft.Json;
using PluginInterface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenTextCollector
{
    public static class Tool
    {
        public static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 截屏
        /// </summary>
        /// <param name="settings">程序配置信息</param>
        /// <param name="screenShotQueue">截屏处理消息队列</param>
        /// <returns></returns>
        public static MethodResult CaptureScreenShot(Settings settings, BlockingCollection<string> screenShotQueue)
        {
            try
            {
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var path = Path.Combine(tempDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");

                if (Screen.AllScreens.Length - 1 < settings.ScreenNumber)
                {
                    return new MethodResult("屏幕编号超出范围");
                }

                Screen screen = Screen.AllScreens[settings.ScreenNumber];
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

                // 发送消息到队列
                screenShotQueue.Add(path);
            }
            catch (Exception ex)
            {
                return new MethodResult("截屏失败", ex);
            }

            return new MethodResult();
        }

        /// <summary>
        /// 循环处理截屏消息队列
        /// </summary>
        /// <param name="settings">程序配置信息</param>
        /// <param name="screenShotQueue">截屏处理消息队列</param>
        /// <param name="cancellationToken"></param>
        /// <param name="verifyImage"></param>
        /// <param name="performOcr"></param>
        public static void ProcessScreenshots(Settings settings, BlockingCollection<string> screenShotQueue,
            CancellationToken cancellationToken, Func<string, List<ImageVerificationArea>, bool> verifyImage,
            Func<string, ImageCollectionArea, string> performOcr)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    // 阻塞等待队列中的新项
                    string screenShotPath = screenShotQueue.Take();

                    // 图像校验
                    if (!verifyImage(screenShotPath, settings.ImageVerificationAreas))
                    {
                        OutputMessage(new MethodResult("未监测到程序画面"));
                        File.Delete(screenShotPath);
                        continue;
                    }

                    // 图像采集
                    var results = new Dictionary<string, string>();
                    Parallel.ForEach(settings.ImageCollectionAreas, area =>
                    {
                        var text = performOcr(screenShotPath, area);
                        results[area.Name] = text;
                    });

                    // 汇总结果并发送 MQTT 消息
                    var mqttMessage = JsonConvert.SerializeObject(results);
                    //Log.Information("识别结果：{Results}", mqttMessage);
                    OutputMessage(new MethodResult(mqttMessage, MethodResultType.Success));

                    // 保存本地日志
                    if (settings.CsvRecord)
                    {
                        SaveToCsv(results);
                    }

                    // 删除截屏图片
                    File.Delete(screenShotPath);
                }
                catch (InvalidOperationException)
                {
                    // 队列已完成添加且为空时抛出此异常，忽略即可
                }
                catch (Exception ex)
                {
                    OutputMessage(new MethodResult("处理截屏失败", ex));
                }
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
        /// <param name="result">方法结果</param>
        /// <param name="uploadAction">上传消息的委托</param>
        /// <exception cref="ArgumentOutOfRangeException">结果类型不在预期范围内</exception>
        public static void OutputMessage(MethodResult result, Action<string> uploadAction = null)
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
                    Log.Error(result.Exception, result.Message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            uploadAction?.Invoke(result.Message);
        }
    }
}