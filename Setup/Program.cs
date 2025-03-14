using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PluginInterface;
using ScreenTextCollector;

namespace Setup
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\n欢迎使用屏幕文字采集工具配置程序！");
            Console.WriteLine("\n本程序将对目标屏幕截屏，用于配置屏幕检测区域和采集区域。请确保在运行本程序时目标区域上没有其他窗口遮挡。\n");
            int screenNumber = GetCoordinate("目标屏幕编号");
            Tool.Settings.ScreenNumber = screenNumber;
            Console.WriteLine("\n按回车键开始截屏...");
            Console.ReadLine();
            var screenShotQueue = new BlockingCollection<string>();
            var result = Tool.CaptureScreenShot(screenShotQueue, screenNumber);
            if (result.Exception != null)
            {
                Console.WriteLine($"截屏失败：{result.Exception.Message}");
                throw result.Exception;
            }

            var screenShotPath = screenShotQueue.Take();
            //用 windows 画图工具打开 screenShotPath
            Process.Start("mspaint.exe", screenShotPath);

            #region 引导配置检测区域

            Console.WriteLine("截屏画面已用画图工具打开，接下来请跟随我完成采集配置。");
            Console.Write("\n首先，是否要清空已配置的检测区域？(y/n)：");
            if (Console.ReadKey().Key == ConsoleKey.Y) Tool.Settings.ImageVerificationAreas.Clear();
            Console.WriteLine("\n请在画图工具中确定检测区域，然后依次输入检测区域的左上角坐标和宽度、高度，输入后按回车键确认。");
            while (true)
            {
                var (topLeftX, topLeftY, width, height) = GetScreenSize();

                var fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}.png";
                using (var screenShot = new Bitmap(screenShotPath))
                {
                    using (Bitmap validationArea = screenShot.Clone(
                               new Rectangle(topLeftX, topLeftY, width, height),
                               PixelFormat.Format24bppRgb))
                    {
                        validationArea.Save(Path.Combine("data", fileName));
                    }
                }

                Tool.Settings.ImageVerificationAreas.Add(new ImageVerificationArea
                {
                    TopLeftX = topLeftX,
                    TopLeftY = topLeftY,
                    Width = width,
                    Height = height,
                    FileName = fileName,
                    MatchThreshold = 0.8f
                });

                SaveSettings();

                Console.Write("\n是否继续输入下一个检测区域？(y/n)：");
                if (Console.ReadKey().Key != ConsoleKey.Y) break;
            }

            #endregion

            #region 引导配置采集区域

            Console.WriteLine("\n检测区域配置已完成，接下来我们进行采集区域配置。");
            Console.Write("\n首先，是否要清空已配置的采集区域？(y/n)：");
            if (Console.ReadKey().Key == ConsoleKey.Y) Tool.Settings.ImageCollectionAreas.Clear();
            Console.WriteLine("\n接下来请在画图工具中确定采集区域，然后依次输入采集区域的左上角坐标和宽度、高度，输入后按回车键确认。");
            while (true)
            {
                Console.Write("请输入采集项名称：");
                var name = Console.ReadLine();

                var (topLeftX, topLeftY, width, height) = GetScreenSize();

                Tool.Settings.ImageCollectionAreas.Add(new ImageCollectionArea()
                {
                    Name = name,
                    TopLeftX = topLeftX,
                    TopLeftY = topLeftY,
                    Width = width,
                    Height = height
                });

                SaveSettings();

                Console.Write("\n是否继续输入下一个采集区域？(y/n)：");
                if (Console.ReadKey().Key != ConsoleKey.Y) break;
            }

            #endregion

            Console.Write("\n配置已全部保存，请用记事本打开 appsettings.json 文件，手动编辑其它配置项。");
            //Console.Write("配置已全部保存，是否立即启动采集？(y/n)：");
            //if (Console.ReadKey().Key != ConsoleKey.Y) return;
            //if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stc.exe")))
            //    Process.Start("stc.exe");
            //else
            //    Console.WriteLine("未找到 stc.exe，请手动执行。");
        }

        private static void SaveSettings()
        {
            var result = Tool.SaveSettings();
            if (result.Exception != null)
            {
                Console.WriteLine($"\n保存配置失败：{result.Exception.Message}");
                throw result.Exception;
            }
        }

        static (int, int, int, int) GetScreenSize()
        {
            int topLeftX = GetCoordinate("左上角 X 坐标");
            int topLeftY = GetCoordinate("左上角 Y 坐标");
            int width = GetCoordinate("宽度");
            int height = GetCoordinate("高度");

            return (topLeftX, topLeftY, width, height);
        }

        static int GetCoordinate(string prompt)
        {
            int value;
            while (true)
            {
                Console.Write($"请输入{prompt}：");
                if (int.TryParse(Console.ReadLine(), out value))
                {
                    break;
                }
                Console.WriteLine("请输入一个整数。");
            }
            return value;
        }
    }
}