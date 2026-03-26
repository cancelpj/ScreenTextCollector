using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 工具栏事件

        private void BtnCapture_Click(object sender, EventArgs e)
        {
            var configPath = GetConfigPath();

            // 检查是否存在旧配置文件
            if (File.Exists(configPath))
            {
                // 弹窗警告
                var result = MessageBox.Show(
                    "重新截屏将删除现有的所有配置和图片文件，是否继续？",
                    "确认重新截屏",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return; // 用户取消
                }

                // 清理旧配置
                DeleteOldConfig();

                // 清空内存中的区域列表
                _verificationAreas.Clear();
                _collectionAreas.Clear();
                _selectedVerificationIndex = -1;
                _selectedCollectionIndex = -1;

                // 刷新列表显示
                RefreshVerificationList();
                RefreshCollectionList();
            }

            // 开始新的截屏流程
            SelectScreenAndCapture(sender, e);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }

        private void BtnOpenConfig_Click(object sender, EventArgs e)
        {
            var configPath = GetConfigPath();
            if (File.Exists(configPath))
            {
                System.Diagnostics.Process.Start("notepad.exe", configPath);
            }
            else
            {
                MessageBox.Show("配置文件不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"{Title}\n\n用于标注屏幕文字采集的检测和采集区域，生成采集配置文件。",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ThresholdComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_thresholdComboBox.SelectedItem != null)
            {
                float.TryParse(_thresholdComboBox.SelectedItem.ToString(), out _matchThreshold);
            }
        }

        private void OcrEngineComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 切换引擎不再自动刷新OCR结果，由用户手动点击"OCR测试"按钮
        }

        private void BtnOcrTest_Click(object sender, EventArgs e)
        {
            if (_screenshot == null)
            {
                MessageBox.Show("请先进行截屏。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_collectionAreas.Count == 0)
            {
                MessageBox.Show("请先添加采集区域。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _btnOcrTest.Enabled = false;
            _statusLabel.Text = "正在识别...";
            if (RefreshCollectionListOcrResults()) _statusLabel.Text = "识别完成";
            _btnOcrTest.Enabled = true;
        }

        private void CmbZoomMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            _isAutoZoom = (_cmbZoomMode.SelectedIndex == 0);
            if (!_isAutoZoom)
            {
                _zoomLevel = 1.0f;
                UpdateScrollSize();
            }
            _imagePanel.Invalidate();
            UpdateZoomUI();
        }

        private bool _isUpdatingZoomLevel; // 防止 TextChanged 回环

        private void CmbZoomLevel_TextChanged(object sender, EventArgs e)
        {
            if (_isAutoZoom || _screenshot == null || _isUpdatingZoomLevel) return;

            string text = _cmbZoomLevel.Text.TrimEnd('%', ' ');
            if (float.TryParse(text, out float percent))
            {
                float newZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, percent / 100f));
                if (Math.Abs(_zoomLevel - newZoom) > 0.001f)
                {
                    _zoomLevel = newZoom;
                    UpdateScrollSize();
                    _imagePanel.Invalidate();
                    // 同步 TrackBar（避免重新触发 TextChanged）
                    UpdateTrackBarOnly();
                }
            }
        }

        private void CmbZoomLevel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                _cmbZoomLevel.Focus(); // 触发 TextChanged
            }
        }

        private void UpdateScrollSize()
        {
            // 更新滚动容器的内容尺寸（不触发重绘，由调用方决定何时刷新）
            if (_screenshot == null) return;
            int w = (int)(_screenshot.Width * _zoomLevel);
            int h = (int)(_screenshot.Height * _zoomLevel);
            _imagePanel.Size = new Size(w, h);
            _scrollContainer.AutoScrollMinSize = new Size(w, h);
        }

        private void BtnZoomIn_Click(object sender, EventArgs e)
        {
            if (_isAutoZoom) return;
            _zoomLevel = Math.Min(MAX_ZOOM, _zoomLevel + ZOOM_STEP);
            UpdateScrollSize();
            _imagePanel.Invalidate();
            UpdateZoomUI();
        }

        private void BtnZoomOut_Click(object sender, EventArgs e)
        {
            if (_isAutoZoom) return;
            _zoomLevel = Math.Max(MIN_ZOOM, _zoomLevel - ZOOM_STEP);
            UpdateScrollSize();
            _imagePanel.Invalidate();
            UpdateZoomUI();
        }

        private void ZoomTrackBar_Scroll(object sender, EventArgs e)
        {
            if (_isAutoZoom) return;
            // 将拖动条位置转换为缩放值
            _zoomLevel = _zoomTrackBar.Value / 100f;
            UpdateScrollSize();
            _imagePanel.Invalidate();
            UpdateZoomUI();
        }

        private void UpdateTrackBarOnly()
        {
            // 将缩放值转换为拖动条位置
            _zoomTrackBar.Value = (int)(_zoomLevel * 100);
        }

        private void UpdateZoomUI()
        {
            // 更新缩放显示
            int percent = (int)(_zoomLevel * 100);
            _isUpdatingZoomLevel = true;
            _cmbZoomLevel.Text = percent + "%";
            _isUpdatingZoomLevel = false;

            // 将缩放值转换为拖动条位置 (0.1-5.0 -> 0-100)
            UpdateTrackBarOnly();

            // 启用/禁用控件
            _btnZoomIn.Enabled = !_isAutoZoom && _zoomLevel < MAX_ZOOM;
            _btnZoomOut.Enabled = !_isAutoZoom && _zoomLevel > MIN_ZOOM;
            _zoomTrackBar.Enabled = !_isAutoZoom;
            _cmbZoomLevel.Enabled = !_isAutoZoom;
        }

        #endregion
    }
}
