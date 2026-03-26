using System;
using System.Drawing;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 截屏功能

        private void SelectScreenAndCapture(object sender, EventArgs e)
        {
            // 获取所有屏幕
            var screens = Screen.AllScreens;

            // 如果只有一个屏幕，直接截屏不弹窗
            if (screens.Length == 1)
            {
                _screenNumber = 0;
                CaptureScreen(_screenNumber);
                return;
            }

            using (var screenSelectDialog = new FormScreenSelect())
            {
                if (screenSelectDialog.ShowDialog() == DialogResult.OK)
                {
                    _screenNumber = screenSelectDialog.SelectedScreenNumber;
                    // 使用选中的屏幕截屏
                    CaptureScreen(_screenNumber);
                }
                else
                {
                    // 用户取消，直接返回主界面，不截屏
                    _statusLabel.Text = "已取消截屏";
                    return;
                }
            }
        }

        /// <summary>
        /// 截取指定屏幕
        /// </summary>
        /// <param name="screenNumber">屏幕编号</param>
        private void CaptureScreen(int screenNumber)
        {
            try
            {
                // 禁用 ToolTip，避免截屏时捕获到 ToolTip
                if (_toolTip != null)
                {
                    _toolTip.Active = false;
                }

                // 隐藏窗体
                this.Hide();
                System.Threading.Thread.Sleep(300);

                // 获取所有屏幕
                var screens = Screen.AllScreens;

                // 检查屏幕编号是否越界
                if (screenNumber >= screens.Length)
                {
                    screenNumber = 0; // 越界时使用主屏幕
                }

                // 获取指定屏幕
                var screen = screens[screenNumber];
                var screenBounds = screen.Bounds;

                _screenshot = new Bitmap(screenBounds.Width, screenBounds.Height);
                using (var g = Graphics.FromImage(_screenshot))
                {
                    // 从屏幕的实际位置开始截取（考虑多屏幕偏移）
                    g.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, screenBounds.Size);
                }

                // 显示窗体
                this.Show();

                // 重新启用 ToolTip
                if (_toolTip != null)
                {
                    _toolTip.Active = true;
                }

                UpdateScrollSize();
                _imagePanel.Invalidate();
                _statusLabel.Text = $"截屏完成: {_screenshot.Width}x{_screenshot.Height}";
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show($"截屏失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion
    }
}
