using PluginInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 区域对话框

        private void ShowAreaDialog(Rectangle rect)
        {
            // 转换面板坐标到图片坐标
            var topLeft = PanelToImage(new Point(rect.X, rect.Y));
            var bottomRight = PanelToImage(new Point(rect.Right, rect.Bottom));

            int imgX = Math.Min(topLeft.X, bottomRight.X);
            int imgY = Math.Min(topLeft.Y, bottomRight.Y);
            int imgWidth = Math.Abs(bottomRight.X - topLeft.X);
            int imgHeight = Math.Abs(bottomRight.Y - topLeft.Y);

            if (_isVerificationAreaMode)
            {
                // 检测区域
                string defaultName = $"检测区域{_verificationAreas.Count + 1}";
                var dialog = new FormAreaDialog(true, _matchThreshold, defaultName, imgX, imgY, imgWidth, imgHeight);
                dialog.ValidateName = name =>
                {
                    if (IsVerificationNameDuplicate(name))
                        return $"检测区域名称 \"{name}\" 已存在，请使用其他名称。";
                    return null;
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var area = new ImageVerificationArea
                    {
                        TopLeftX = imgX,
                        TopLeftY = imgY,
                        Width = imgWidth,
                        Height = imgHeight,
                        FileName = dialog.AreaName + ".png",
                        MatchThreshold = dialog.MatchThreshold
                    };
                    _verificationAreas.Add(area);
                    _isModify = true;

                    // 保存检测区域截图
                    SaveVerificationImage(area);

                    RefreshVerificationList();
                    _imagePanel.Invalidate();
                    _statusLabel.Text = "已添加检测区域";
                }
            }
            else
            {
                // 采集区域
                string defaultName = $"采集区域{_collectionAreas.Count + 1}";
                var dialog = new FormAreaDialog(false, _matchThreshold, defaultName, imgX, imgY, imgWidth, imgHeight, "", _availableTopics);
                dialog.ValidateName = name =>
                {
                    if (IsCollectionNameDuplicate(name))
                        return $"采集区域名称 \"{name}\" 已存在，请使用其他名称。";
                    return null;
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var area = new ImageCollectionArea
                    {
                        Name = dialog.AreaName,
                        TopLeftX = imgX,
                        TopLeftY = imgY,
                        Width = imgWidth,
                        Height = imgHeight,
                        Topic = dialog.Topic
                    };
                    _collectionAreas.Add(area);
                    _isModify = true;

                    RefreshCollectionList();
                    _imagePanel.Invalidate();
                    _statusLabel.Text = "已添加采集区域";
                }
            }
        }

        private void SaveVerificationImage(ImageVerificationArea area)
        {
            try
            {
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                var filePath = Path.Combine(dataDir, area.FileName);
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                using (var cropped = _screenshot.Clone(rect, _screenshot.PixelFormat))
                {
                    cropped.Save(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存检测图片失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 切换选择区域单选按钮

        private void RadioVerificationArea_CheckedChanged(object sender, EventArgs e)
        {
            if (_radioVerificationArea.Checked)
            {
                _isVerificationAreaMode = true;
            }
        }

        private void RadioCollectionArea_CheckedChanged(object sender, EventArgs e)
        {
            if (_radioCollectionArea.Checked)
            {
                _isVerificationAreaMode = false;
            }
        }

        #endregion

        #region 配置保存/加载

        private string GetDataDir()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        }

        private string GetConfigPath()
        {
            return Path.Combine(GetDataDir(), "CaptureSettings.json");
        }

        private Image LoadIconFromResources(string resourcesDir, string fileName)
        {
            var path = Path.Combine(resourcesDir, fileName);
            if (File.Exists(path))
            {
                byte[] data = File.ReadAllBytes(path);
                return Image.FromStream(new MemoryStream(data));
            }
            return null;
        }

        private string GetScreenshotPath()
        {
            return Path.Combine(GetDataDir(), "screenshot.png");
        }

        private void DeleteOldConfig()
        {
            try
            {
                var configPath = GetConfigPath();
                var screenshotPath = GetScreenshotPath();
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

                // 删除配置文件
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                // 删除截图文件
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }

                // 删除 data 目录中的模板图片
                if (Directory.Exists(dataDir))
                {
                    Directory.Delete(dataDir, true);
                }

                _statusLabel.Text = "已清除旧配置";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除旧配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadScreenshot(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    _screenshot?.Dispose();
                    // 先从文件读取，然后用克隆创建新Bitmap以释放文件锁
                    using (var tempBmp = new Bitmap(path))
                    {
                        _screenshot = new Bitmap(tempBmp);
                    }
                    UpdateScrollSize();
                    _imagePanel.Invalidate();
                    _statusLabel.Text = $"截图已加载: {_screenshot.Width}x{_screenshot.Height}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载截图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadConfig()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<CaptureSettings>(json);
                if (config != null)
                {
                    _verificationAreas = config.VerificationAreas ?? new List<ImageVerificationArea>();
                    _collectionAreas = config.CollectionAreas ?? new List<ImageCollectionArea>();
                    _screenNumber = config.ScreenNumber;
                    _ocrEngineComboBox.Text = config.OcrEngine ?? "PaddleOCR";

                    // 检查屏幕编号是否越界
                    var screens = Screen.AllScreens;
                    if (_screenNumber >= screens.Length)
                    {
                        _screenNumber = 0;
                    }

                    // 加载截图
                    var screenshotPath = GetScreenshotPath();
                    if (File.Exists(screenshotPath))
                    {
                        LoadScreenshot(screenshotPath);
                    }

                    // 更新阈值下拉框
                    for (int i = 0; i < _thresholdComboBox.Items.Count; i++)
                    {
                        if (_thresholdComboBox.Items[i].ToString() == _matchThreshold.ToString())
                        {
                            _thresholdComboBox.SelectedIndex = i;
                            break;
                        }
                    }

                    RefreshVerificationList();
                    RefreshCollectionList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 从 appsettings.json 加载 MQTT Topic 列表
        /// </summary>
        private void LoadAvailableTopics()
        {
            try
            {
                var topics = new List<string>();

                // 包含 DefaultTopic
                if (Tool.Settings?.MqttBroker?.DefaultTopic != null
                    && !string.IsNullOrEmpty(Tool.Settings.MqttBroker.DefaultTopic.Name))
                {
                    topics.Add(Tool.Settings.MqttBroker.DefaultTopic.Name);
                }

                // 包含 Topics 列表
                if (Tool.Settings?.MqttBroker?.Topics != null)
                {
                    topics.AddRange(Tool.Settings.MqttBroker.Topics
                        .Where(t => !string.IsNullOrEmpty(t.Name))
                        .Select(t => t.Name));
                }

                _availableTopics = topics.Distinct().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载 Topic 列表失败: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                // 确保 data 目录存在
                Directory.CreateDirectory(GetDataDir());

                var screenshotPath = GetScreenshotPath();

                // 保存截图
                _screenshot?.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);

                var config = new CaptureSettings
                {
                    VerificationAreas = _verificationAreas,
                    CollectionAreas = _collectionAreas,
                    ScreenNumber = _screenNumber,
                    OcrEngine = _ocrEngineComboBox.Text
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(GetConfigPath(), json);
                _statusLabel.Text = "配置已保存";
                _isModify = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 删除和绘制事件

        private void VerificationListView_MouseClick(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            var info = listView.HitTest(e.X, e.Y);
            if (info.Item != null && info.SubItem != null)
            {
                // 点击删除列（第3列）
                int deleteColumnIndex = 2;
                int columnIndex = info.Item.SubItems.IndexOf(info.SubItem);
                if (columnIndex == deleteColumnIndex)
                {
                    int index = (int)info.Item.Tag;
                    DeleteVerificationArea(index);
                }
            }
        }

        private void CollectionListView_MouseClick(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            var info = listView.HitTest(e.X, e.Y);
            if (info.Item != null && info.SubItem != null)
            {
                // 点击删除列（第3列）
                int deleteColumnIndex = 2;
                int columnIndex = info.Item.SubItems.IndexOf(info.SubItem);
                if (columnIndex == deleteColumnIndex)
                {
                    int index = (int)info.Item.Tag;
                    DeleteCollectionArea(index);
                }
            }
        }

        private void VerificationListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null || listView.SelectedItems.Count == 0) return;

            int index = (int)listView.SelectedItems[0].Tag;
            if (index >= 0 && index < _verificationAreas.Count)
            {
                EditVerificationArea(index);
            }
        }

        private void CollectionListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null || listView.SelectedItems.Count == 0) return;

            int index = (int)listView.SelectedItems[0].Tag;
            if (index >= 0 && index < _collectionAreas.Count)
            {
                EditCollectionArea(index);
            }
        }

        private void DeleteVerificationArea(int index)
        {
            if (index >= 0 && index < _verificationAreas.Count)
            {
                var area = _verificationAreas[index];
                var result = MessageBox.Show($"确定要删除检测区域 \"{Path.GetFileNameWithoutExtension(area.FileName)}\" 吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _verificationAreas.RemoveAt(index);
                    _isModify = true;
                    RefreshVerificationList();

                    // 删除后重置选中索引
                    if (_selectedVerificationIndex >= _verificationAreas.Count)
                    {
                        _selectedVerificationIndex = _verificationAreas.Count > 0 ? _verificationAreas.Count - 1 : -1;
                    }

                    _imagePanel.Invalidate();
                    _statusLabel.Text = "检测区域已删除";
                }
            }
        }

        private void DeleteCollectionArea(int index)
        {
            if (index >= 0 && index < _collectionAreas.Count)
            {
                var area = _collectionAreas[index];
                var result = MessageBox.Show($"确定要删除采集区域 \"{area.Name}\" 吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _collectionAreas.RemoveAt(index);
                    _isModify = true;
                    RefreshCollectionList();

                    // 删除后重置选中索引
                    if (_selectedCollectionIndex >= _collectionAreas.Count)
                    {
                        _selectedCollectionIndex = _collectionAreas.Count > 0 ? _collectionAreas.Count - 1 : -1;
                    }

                    _imagePanel.Invalidate();
                    _statusLabel.Text = "采集区域已删除";
                }
            }
        }

        private void ListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        /// <summary>
        /// 绘制 ListView 删除按钮（通用方法）
        /// </summary>
        private void ListView_DrawDeleteButton(object sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex == 2)
            {
                // 绘制删除按钮
                e.DrawDefault = false;
                var bounds = e.SubItem.Bounds;
                using (var btnBrush = new SolidBrush(Color.FromArgb(220, 80, 80)))
                {
                    var btnRect = new Rectangle(bounds.Left + 2, bounds.Top + 2, bounds.Width - 4, bounds.Height - 4);
                    e.Graphics.FillRectangle(btnBrush, btnRect);
                    using (var pen = new Pen(Color.White, 1))
                    {
                        e.Graphics.DrawString("×", new Font("Microsoft Sans Serif", 9, FontStyle.Bold),
                            Brushes.White, bounds.Left + 8, bounds.Top + 2);
                    }
                }
            }
            else
            {
                e.DrawDefault = true;
            }
        }

        private void VerificationListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
            => ListView_DrawDeleteButton(sender, e);

        private void CollectionListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
            => ListView_DrawDeleteButton(sender, e);

        /// <summary>
        /// 捕获键盘事件 - 处理 Tab 键切换检测/采集区域
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab)
            {
                // 切换检测/采集模式
                _isVerificationAreaMode = !_isVerificationAreaMode;

                // 更新RadioButton状态
                _radioVerificationArea.Checked = _isVerificationAreaMode;
                _radioCollectionArea.Checked = !_isVerificationAreaMode;

                // 切换选中区域
                if (_isVerificationAreaMode)
                {
                    if (_selectedVerificationIndex < 0 && _verificationAreas.Count > 0)
                    {
                        _selectedVerificationIndex = 0;
                    }
                    _selectedCollectionIndex = -1;
                }
                else
                {
                    if (_selectedCollectionIndex < 0 && _collectionAreas.Count > 0)
                    {
                        _selectedCollectionIndex = 0;
                    }
                    _selectedVerificationIndex = -1;
                }

                _imagePanel.Invalidate();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion
    }
}
