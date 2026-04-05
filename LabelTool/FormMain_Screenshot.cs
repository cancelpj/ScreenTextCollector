using PluginInterface;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 屏幕切换

        /// <summary>
        /// 切换当前显示的屏幕
        /// </summary>
        /// <param name="screenNumber">目标屏幕编号</param>
        private void SwitchScreen(int screenNumber)
        {
            // 检查截图是否存在
            if (!_screenScreenshots.ContainsKey(screenNumber))
            {
                _statusLabel.Text = $"屏幕 {screenNumber} 尚未截图";
                return;
            }

            _currentScreenNumber = screenNumber;
            UpdateScrollSize();
            _imagePanel.Invalidate();
            RefreshVerificationList();
            RefreshCollectionList();
            _statusLabel.Text = $"已切换到屏幕 {screenNumber}";
        }

        /// <summary>
        /// 刷新屏幕下拉框（只显示已勾选并截屏的屏幕）
        /// </summary>
        private void RefreshScreenComboBox()
        {
            _screenComboBox.Items.Clear();
            var screens = Screen.AllScreens;

            // 只显示已勾选的屏幕
            foreach (var screenNumber in _capturedScreenNumbers)
            {
                if (screenNumber >= 0 && screenNumber < screens.Length)
                {
                    var screen = screens[screenNumber];
                    string displayName;
                    if (screen.Primary)
                    {
                        displayName = $"屏幕 {screenNumber}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height}) - 主屏幕";
                    }
                    else
                    {
                        displayName = $"屏幕 {screenNumber}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
                    }
                    _screenComboBox.Items.Add(displayName);
                }
            }

            // 选中当前屏幕
            int currentIndex = _capturedScreenNumbers.IndexOf(_currentScreenNumber);
            if (currentIndex >= 0)
            {
                _screenComboBox.SelectedIndex = currentIndex;
            }
        }

        #endregion

        #region 截屏功能

        private void SelectScreenAndCapture(object sender, EventArgs e)
        {
            // 获取所有屏幕
            var screens = Screen.AllScreens;

            // 如果只有一个屏幕，直接截屏不弹窗
            if (screens.Length == 1)
            {
                _currentScreenNumber = 0;
                if (CaptureScreen(_currentScreenNumber))
                {
                    RefreshScreenComboBox();
                    SwitchScreen(_currentScreenNumber);
                }
                return;
            }

            using (var screenSelectDialog = new FormScreenSelect())
            {
                if (screenSelectDialog.ShowDialog() == DialogResult.OK)
                {
                    var selectedScreens = screenSelectDialog.SelectedScreenNumbers;

                    // 保存已勾选的屏幕编号
                    _capturedScreenNumbers = new List<int>(selectedScreens);

                    // 批量截取所有选中的屏幕
                    foreach (var screenNumber in selectedScreens)
                    {
                        CaptureScreen(screenNumber);
                    }

                    // 切换到第一个屏幕
                    if (selectedScreens.Count > 0)
                    {
                        _currentScreenNumber = selectedScreens[0];
                    }

                    // 刷新屏幕下拉框并切换显示
                    RefreshScreenComboBox();
                    SwitchScreen(_currentScreenNumber);
                }
                else
                {
                    // 用户取消
                    _statusLabel.Text = "已取消截屏";
                    return;
                }
            }
        }

        /// <summary>
        /// 截取指定屏幕
        /// </summary>
        /// <param name="screenNumber">屏幕编号，从 0 开始</param>
        /// <returns>是否截屏成功</returns>
        private bool CaptureScreen(int screenNumber)
        {
            try
            {
                // 获取所有屏幕
                var screens = Screen.AllScreens;

                // 屏幕越界检查
                if (screenNumber < 0 || screenNumber >= screens.Length)
                {
                    _statusLabel.Text = $"屏幕编号 {screenNumber} 超出范围（0-{screens.Length - 1}）";
                    return false;
                }

                // 禁用 ToolTip，避免截屏时捕获到 ToolTip
                if (_toolTip != null)
                {
                    _toolTip.Active = false;
                }

                // 隐藏窗体
                this.Hide();
                System.Threading.Thread.Sleep(300);

                // 获取指定屏幕
                var screen = screens[screenNumber];
                var screenBounds = screen.Bounds;

                var screenshot = new System.Drawing.Bitmap(screenBounds.Width, screenBounds.Height);
                using (var g = System.Drawing.Graphics.FromImage(screenshot))
                {
                    // 从屏幕的实际位置开始截取（考虑多屏幕偏移）
                    g.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, screenBounds.Size);
                }

                // 保存到字典中
                if (_screenScreenshots.ContainsKey(screenNumber))
                {
                    _screenScreenshots[screenNumber]?.Dispose();
                }
                _screenScreenshots[screenNumber] = screenshot;

                // 显示窗体
                this.Show();

                // 重新启用 ToolTip
                if (_toolTip != null)
                {
                    _toolTip.Active = true;
                }

                _statusLabel.Text = $"屏幕 {screenNumber} 截屏完成: {screenshot.Width}x{screenshot.Height}";
                return true;
            }
            catch (Exception ex)
            {
                this.Show();
                // 重新启用 ToolTip
                if (_toolTip != null)
                {
                    _toolTip.Active = true;
                }
                MessageBox.Show($"截屏失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取当前屏幕的截图
        /// </summary>
        private System.Drawing.Bitmap GetCurrentScreenshot()
        {
            if (_screenScreenshots.TryGetValue(_currentScreenNumber, out var screenshot))
            {
                return screenshot;
            }
            return null;
        }

        #endregion
    }
}
