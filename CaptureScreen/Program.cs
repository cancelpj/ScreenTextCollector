using ScreenTextCollector;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PluginInterface;

namespace CaptureScreen
{
    class Program
    {
        static void Main(string[] args)
        {
            var screenShotQueue = new BlockingCollection<string>();
            Tool.CaptureScreenShot(screenShotQueue);
            var screenShotPath = screenShotQueue.Take();
            //用 windows 画图工具打开 screenShotPath
            System.Diagnostics.Process.Start("mspaint.exe", screenShotPath);
            Console.WriteLine("请在画图工具中确定检测区域，然后依次输入检测区域的左上角坐标和宽度、高度，输入后按回车键确认。");
            while (true)
            {
                inputX:
                Console.Write("请输入左上角 X 坐标：");
                if (!int.TryParse(Console.ReadLine(), out var topLeftX))
                {
                    Console.WriteLine("请输入一个整数。");
                    goto inputX;
                    ;
                }

                inputY:
                Console.Write("请输入左上角 Y 坐标：");
                if (!int.TryParse(Console.ReadLine(), out var topLeftY))
                {
                    Console.WriteLine("请输入一个整数。");
                    goto inputY;
                }

                inputWidth:
                Console.Write("请输入宽度：");
                if (!int.TryParse(Console.ReadLine(), out var width))
                {
                    Console.WriteLine("请输入一个整数。");
                    goto inputWidth;
                }

                inputHeight:
                Console.Write("请输入高度：");
                if (!int.TryParse(Console.ReadLine(), out var height))
                {
                    Console.WriteLine("请输入一个整数。");
                    goto inputHeight;
                }

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

                Tool.SaveSettings();
                Console.Write("\n是否继续输入下一个？(y/n)：");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                {
                    break;
                }
            }
        }
    }
}