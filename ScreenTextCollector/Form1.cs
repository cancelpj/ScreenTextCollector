using NLog;
using PluginInterface;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ScreenTextCollector
{
    public partial class Form1 : Form
    {
        // 订阅处理器引用，用于取消订阅
        private Action<LogLevel, string> _logReceivedHandler;

        private delegate void InvokeUiEventCallback(object sender, EventArgs e);
        private delegate void InvokeLogWriteCallback(LogLevel logLevel, string msg);

        /// <summary>
        /// 统一日志输入方法
        /// </summary>
        /// <param name="logLevel">日志文件级别</param>
        /// <param name="msg">日志内容</param>
        private void LogWrite(LogLevel logLevel, string msg)
        {
            if (dataGridView_log.InvokeRequired) // 如果不是创建控件的线程调用本方法，就委托给它调用
            {
                InvokeLogWriteCallback msgCallback = LogWrite;
                dataGridView_log.BeginInvoke(msgCallback, logLevel, msg);
            }
            else // 实际的方法逻辑
            {
                // 将日志写入 NLog，同时打上标记表示来自 UI，避免自定义 Target 再次转发回 UI 导致循环
                // 这是个给日志事件添加属性过滤的例子，实际使用中请根据需要添加
                //var logEvent = new LogEventInfo(logLevel, Tool.Log.Name, msg);
                //logEvent.Properties["FromUi"] = true;
                //Tool.Log.Log(logEvent);

                //保证dataGridView最多显示100行，超过时删除最旧的行
                if (dataGridView_log.Rows.Count >= 100)
                {
                    dataGridView_log.Rows.RemoveAt(0);
                }

                var index = dataGridView_log.Rows.Add();
                dataGridView_log.Rows[index].Cells["Message"].Value =
                    $"{DateTimeOffset.Now:yyyy-M-d HH:mm:ss}  {msg} \r\n";
                dataGridView_log.Rows[index].Cells["LogLevel"].Value = logLevel;

                //每新增一行，滚动条自动滚动至最后一行
                dataGridView_log.FirstDisplayedScrollingRowIndex = dataGridView_log.Rows.Count - 1;
            }
        }

        /// <summary>
        /// 获取程序集版本号
        /// </summary>
        /// <returns>版本号字符串</returns>
        private string GetApplicationVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version.ToString();
        }

        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        public Form1()
        {
            InitializeComponent();

            // 窗口标题显示版本号，格式为 yyyy.M.D.HHMM,如 2026.1.6.1003
            Text = $@"SPMES 任务客户端 V{GetApplicationVersion()}";

            #region 控件样式

            // 单元格换行样式
            dataGridView_log.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView_log.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            dataGridView_log.RowsDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // 单元格颜色
            dataGridView_log.RowsDefaultCellStyle.BackColor = Color.Black;
            dataGridView_log.BackgroundColor = Color.Black;
            dataGridView_log.CellBorderStyle = DataGridViewCellBorderStyle.None;

            #endregion

            // 订阅来自 Tool 的日志广播
            _logReceivedHandler = (logLevel, message) => LogWrite(logLevel, message);
            Tool.LogReceived += _logReceivedHandler;

            //todo:向服务器发出在线状态

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 用户点击关闭按钮时，隐藏窗体而不退出程序
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                WindowState = FormWindowState.Normal;
                notifyIcon1.ShowBalloonTip(2000, "提示", "程序持续运行", ToolTipIcon.Info);
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            // 判断只有最小化时，隐藏窗体
            if (WindowState == FormWindowState.Minimized) Hide();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            if (InvokeRequired) //如果不是创建控件的线程调用本方法，就委托给它调用
            {
                InvokeUiEventCallback callback = new InvokeUiEventCallback(notifyIcon1_DoubleClick);
                callback.Invoke(sender, e);
            }
            else  //实际的方法
            {
                // 正常显示窗体
                Visible = true;
                WindowState = FormWindowState.Normal;
                Activate();
            }
        }

        private void toolStripMenuItem_exit_Click(object sender, EventArgs e)
        {
            if (_logReceivedHandler != null)
            {
                Tool.LogReceived -= _logReceivedHandler;
                _logReceivedHandler = null;
            }

            //todo:向服务器发出离线状态

            //Close();
            Program.Exit();
        }

        private void dataGridView_log_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            switch (dataGridView_log.Rows[e.RowIndex].Cells["LogLevel"].Value)
            {
                case 1:
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Red;
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.Red;
                    break;
                case 2:
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Yellow;
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.Yellow;
                    break;
                case 3:
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.SpringGreen;
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.SpringGreen;
                    break;
                case 4:
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Bisque;
                    dataGridView_log.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = Color.Bisque;
                    break;
            }
        }
    }
}