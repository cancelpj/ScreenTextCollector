using System;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        private void FormMain_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+R: 重新截屏
            if (e.Control && e.KeyCode == Keys.R)
            {
                BtnCapture_Click(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
            // Ctrl+S: 保存配置
            else if (e.Control && e.KeyCode == Keys.S)
            {
                BtnSave_Click(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
            // Ctrl+O: 用记事本打开配置
            else if (e.Control && e.KeyCode == Keys.O)
            {
                BtnOpenConfig_Click(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
            // Ctrl+T: OCR测试
            else if (e.Control && e.KeyCode == Keys.T)
            {
                BtnOcrTest_Click(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isModify)
            {
                var result = MessageBox.Show("检测到未保存的修改，是否保存？", "未保存的更改",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    SaveConfig();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // 初始化 ToolTip
            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_toolStrip, "工具栏：重新截屏 | 保存配置 | 用记事本打开配置 | OCR测试");

            // 检查是否存在旧配置文件
            var configPath = GetConfigPath();
            var screenshotPath = GetScreenshotPath();
            if (File.Exists(configPath))
            {
                // 检查截图文件是否存在
                bool screenshotExists = File.Exists(screenshotPath);

                string message = screenshotExists
                    ? "已存在旧配置，是否加载？"
                    : "已存在旧配置文件，但截图文件不存在，是否加载配置？";

                var result = MessageBox.Show(message, "加载配置",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 加载配置（包括截图）
                    LoadConfig();
                }
                else
                {
                    // 清除旧配置，弹出屏幕选择对话框
                    DeleteOldConfig();
                    SelectScreenAndCapture(sender, e);
                }
            }
            else
            {
                // 没有配置文件，弹出屏幕选择对话框
                SelectScreenAndCapture(sender, e);
            }

            // 设置默认选中检测区域
            _radioVerificationArea.Checked = true;
            _isVerificationAreaMode = true;

            // 更新状态栏
            _statusLabel.Text = "就绪";
        }
    }
}
