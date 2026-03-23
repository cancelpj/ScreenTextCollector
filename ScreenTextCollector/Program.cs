using PluginInterface;
using SimpleMqttClient;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ScreenTextCollector
{
    internal static partial class Program
    {
        private static bool _isRunning = true;
        private static readonly IOcrService OcrService = CreateOcrService();
        private static Thread _mqttPushThread = null;
        private static MqttClient _mqttClient = null; // 保持全局引用便于清理
        private static HttpListener _listener = null; // 保持全局引用便于清理
        private static Mutex _mutex = null;
        private static CancellationTokenSource _cancellationTokenSource = null; // 用于优雅停止线程

        static void Main(string[] args)
        {
            // 参数处理：支持任意顺序的参数
            bool isDebugMode = args.Contains("-debug", StringComparer.OrdinalIgnoreCase);

            if (isDebugMode)
            {
                MessageBox.Show(@"按任意键继续...");
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 单一实例运行检查（使用Mutex）
            string mutexName = "Global\\ScreenTextCollector_" +
                               Assembly.GetExecutingAssembly().GetName().Name.GetHashCode();
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // 已有实例运行，激活现有实例
                ActivateExistingInstance();
                _mutex?.Dispose();
                return;
            }

            try
            {
                #region 启动时清空temp文件夹

                Tool.Log.Info("启动时清空 temp 文件夹");
                string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true); // 删除文件夹及其内容
                }

                #endregion

                #region 启动服务

                var httpConfig = Tool.Settings.Http;
                if (httpConfig.EnableHttp)
                {
                    StartHttpServer(httpConfig);
                }

                var mqttBrokerConfig = Tool.Settings.MqttBroker;
                if (mqttBrokerConfig.EnableMqttPush)
                {
                    // 创建取消令牌，用于优雅停止线程
                    _cancellationTokenSource = new CancellationTokenSource();

                    //用一个独立线程定时检查进程并推送 MQTT
                    _mqttPushThread = new Thread(() => StartMqttPush(mqttBrokerConfig, _cancellationTokenSource.Token))
                    {
                        IsBackground = true,
                        Name = "MqttPushThread"
                    };
                    _mqttPushThread.Start();
                }

                #endregion

                Application.Run(new Form1());
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;
            }
        }

        public static void Exit()
        {
            _isRunning = false;

            // 立即停止监听
            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch
                {
                }
                finally
                {
                    _listener = null;
                }
            }

            // 断开 MQTT 连接
            if (_mqttClient != null)
            {
                try
                {
                    _mqttClient?.Disconnect();
                }
                catch
                {
                }
                finally
                {
                    _mqttClient?.Dispose();
                    _mqttClient = null;
                }
            }

            // 取消线程执行信号
            if (_cancellationTokenSource != null)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                    // 等待线程自然退出（最多等待5秒）
                    if (_mqttPushThread != null && _mqttPushThread.IsAlive)
                    {
                        _mqttPushThread.Join(5000);
                    }
                }
                catch
                {
                }
                finally
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                    _mqttPushThread = null;
                }
            }

            Tool.Log.Info("\n服务已停止");
            Application.Exit();
        }

        /// <summary>
        /// 根据配置创建 OCR 服务
        /// </summary>
        private static IOcrService CreateOcrService()
        {
            var engine = Tool.Settings.OcrEngine?.ToLower() ?? "opencvsharp";
            switch (engine)
            {
                case "paddleocr":
                    return new ScreenTextCollector.PaddleOCR.OcrService();
                default:
                    return new ScreenTextCollector.OpenCvSharp.OcrService();
            }
        }

        private static void ActivateExistingInstance()
        {
            var current = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(current.ProcessName);
            try
            {
                foreach (var p in processes)
                {
                    if (p.Id == current.Id)
                        continue;

                    try
                    {
                        //给进程p发个消息

                        //if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindowAsync(p.MainWindowHandle, WindowShowStyle.ShowNormal);
                            SetForegroundWindow(p.MainWindowHandle);
                            break;
                        }
                    }
                    catch
                    {
                        // 忽略无法访问的进程
                    }
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    p?.Dispose();
                }
            }

            current.Dispose();
        }

        /// <summary>
        /// 改变窗口的显示状态
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="style">窗口显示状态常量</param>
        /// <returns>是否成功</returns>
        [DllImport("User32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, WindowShowStyle style);

        /// <summary>
        /// 将指定的窗口带到前台并激活它
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>是否成功</returns>
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}

public enum WindowShowStyle : int
{
    /// <summary>隐藏窗口并激活另一个窗口。</summary>
    Hide = 0,

    /// <summary>激活并显示窗口。如果窗口最小化或最大化，系统将其还原到其原始大小和位置。</summary>
    ShowNormal = 1,

    /// <summary>激活窗口并将其显示为最小化窗口。</summary>
    ShowMinimized = 2,

    /// <summary>激活窗口并将其显示为最大化窗口。</summary>
    ShowMaximized = 3,

    /// <summary>以最近的大小和位置显示窗口，但不激活它。</summary>
    ShowNoActivate = 4,

    /// <summary>在当前位置和大小激活并显示窗口。</summary>
    Show = 5,

    /// <summary>最小化指定的窗口并激活 Z 轴顺序中的下一个顶层窗口。</summary>
    Minimize = 6,

    /// <summary>将窗口显示为最小化窗口，但不激活它。</summary>
    ShowMinNoActive = 7,

    /// <summary>以当前状态显示窗口，但不激活它。</summary>
    ShowNA = 8,

    /// <summary>激活并显示窗口。如果窗口最小化或最大化，系统将其恢复到原始大小和位置。</summary>
    Restore = 9,

    /// <summary>依据启动应用程序的 STARTUPINFO 结构中指定的 SW_ 值设定显示状态。</summary>
    ShowDefault = 10,

    /// <summary>即使拥有窗口的线程被挂起，也最小化窗口。在跨进程操作时建议使用此标志。</summary>
    ForceMinimize = 11
}