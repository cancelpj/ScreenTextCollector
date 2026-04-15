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
                string defaultName = $"{_currentScreenNumber}_检测区域{GetCurrentVerificationAreas().Count + 1}";
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
                        ScreenNumber = _currentScreenNumber, // 关联当前屏幕
                        TopLeftX = imgX,
                        TopLeftY = imgY,
                        Width = imgWidth,
                        Height = imgHeight,
                        FileName = $"{dialog.AreaName}.png",
                        MatchThreshold = dialog.MatchThreshold
                    };
                    GetCurrentVerificationAreas().Add(area);
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
                string defaultName = $"{_currentScreenNumber}_采集区域{GetCurrentCollectionAreas().Count + 1}";
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
                        ScreenNumber = _currentScreenNumber, // 关联当前屏幕
                        Name = dialog.AreaName,
                        TopLeftX = imgX,
                        TopLeftY = imgY,
                        Width = imgWidth,
                        Height = imgHeight,
                        Topic = dialog.Topic
                    };
                    GetCurrentCollectionAreas().Add(area);
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
                var screenshot = GetCurrentScreenshot();
                if (screenshot == null)
                {
                    MessageBox.Show("当前没有截图，无法保存检测图片。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                var filePath = Path.Combine(dataDir, area.FileName);
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                using (var cropped = screenshot.Clone(rect, screenshot.PixelFormat))
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

        #region 辅助方法：截图和区域数据清理

        /// <summary>
        /// 释放并清空所有屏幕截图
        /// </summary>
        private void ClearAllScreenshots()
        {
            foreach (var screenshot in _screenScreenshots.Values)
            {
                screenshot?.Dispose();
            }
            _screenScreenshots.Clear();
        }

        /// <summary>
        /// 清空所有数据（截图+区域+选中状态）
        /// </summary>
        private void ClearAllData()
        {
            ClearAllScreenshots();
            _screenVerificationAreas.Clear();
            _screenCollectionAreas.Clear();
            _selectedVerificationIndex = -1;
            _selectedCollectionIndex = -1;
        }

        /// <summary>
        /// 获取当前屏幕的验证区域列表
        /// </summary>
        private List<ImageVerificationArea> GetCurrentVerificationAreas()
        {
            if (!_screenVerificationAreas.ContainsKey(_currentScreenNumber))
            {
                _screenVerificationAreas[_currentScreenNumber] = new List<ImageVerificationArea>();
            }
            return _screenVerificationAreas[_currentScreenNumber];
        }

        /// <summary>
        /// 获取当前屏幕的采集区域列表
        /// </summary>
        private List<ImageCollectionArea> GetCurrentCollectionAreas()
        {
            if (!_screenCollectionAreas.ContainsKey(_currentScreenNumber))
            {
                _screenCollectionAreas[_currentScreenNumber] = new List<ImageCollectionArea>();
            }
            return _screenCollectionAreas[_currentScreenNumber];
        }

        /// <summary>
        /// 检查验证区域名称是否重复
        /// </summary>
        private bool IsVerificationNameDuplicate(string name)
        {
            foreach (var areas in _screenVerificationAreas.Values)
            {
                if (areas.Any(a => Path.GetFileNameWithoutExtension(a.FileName) == name))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查采集区域名称是否重复
        /// </summary>
        private bool IsCollectionNameDuplicate(string name)
        {
            foreach (var areas in _screenCollectionAreas.Values)
            {
                if (areas.Any(a => a.Name == name))
                    return true;
            }
            return false;
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

        private string GetScreenshotPath(int screenNumber)
        {
            return Path.Combine(GetDataDir(), $"screenshot_{screenNumber}.png");
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

        private void DeleteOldConfig()
        {
            try
            {
                var configPath = GetConfigPath();
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

                // 删除配置文件
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                // 删除所有截图文件
                var screenshotFiles = Directory.GetFiles(dataDir, "*.png");
                foreach (var file in screenshotFiles)
                {
                    File.Delete(file);
                }

                ClearAllData();

                _statusLabel.Text = "已清除旧配置";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除旧配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadScreenshot(int screenNumber)
        {
            try
            {
                var screenshotPath = GetScreenshotPath(screenNumber);
                if (File.Exists(screenshotPath))
                {
                    if (_screenScreenshots.ContainsKey(screenNumber))
                    {
                        _screenScreenshots[screenNumber]?.Dispose();
                    }
                    // 先从文件读取，然后用克隆创建新Bitmap以释放文件锁
                    using (var tempBmp = new Bitmap(screenshotPath))
                    {
                        _screenScreenshots[screenNumber] = new Bitmap(tempBmp);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载屏幕 {screenNumber} 截图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    // 按屏幕分组加载验证区域
                    _screenVerificationAreas.Clear();
                    if (config.VerificationAreas != null)
                    {
                        foreach (var area in config.VerificationAreas)
                        {
                            int screenNumber = area.ScreenNumber;
                            if (!_screenVerificationAreas.ContainsKey(screenNumber))
                            {
                                _screenVerificationAreas[screenNumber] = new List<ImageVerificationArea>();
                            }
                            _screenVerificationAreas[screenNumber].Add(area);
                        }
                    }

                    // 按屏幕分组加载采集区域
                    _screenCollectionAreas.Clear();
                    if (config.CollectionAreas != null)
                    {
                        foreach (var area in config.CollectionAreas)
                        {
                            int screenNumber = area.ScreenNumber;
                            if (!_screenCollectionAreas.ContainsKey(screenNumber))
                            {
                                _screenCollectionAreas[screenNumber] = new List<ImageCollectionArea>();
                            }
                            _screenCollectionAreas[screenNumber].Add(area);
                        }
                    }

                    _ocrEngineComboBox.Text = config.OcrEngine ?? "PaddleOCR";

                    // 加载所有有区域的屏幕的截图
                    foreach (var screenNumber in _screenVerificationAreas.Keys.Concat(_screenCollectionAreas.Keys).Distinct())
                    {
                        LoadScreenshot(screenNumber);
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

                    // 构建所有出现过的屏幕编号（截图 + 区域配置的并集）
                    var allScreenNumbers = _screenScreenshots.Keys
                        .Concat(_screenVerificationAreas.Keys)
                        .Concat(_screenCollectionAreas.Keys)
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();

                    // 遍历每个屏幕，验证截图有效性，分辨率不匹配时给出警告
                    var warnings = new List<string>();
                    var validScreenNumbers = new List<int>();
                    var currentScreens = Screen.AllScreens;

                    foreach (var screenNumber in allScreenNumbers)
                    {
                        // 1. 检查截图是否存在（通过 _screenScreenshots 字典判断，文件不存在时字典不包含该键）
                        if (!_screenScreenshots.ContainsKey(screenNumber))
                        {
                            warnings.Add($"屏幕 {screenNumber}：截图文件不存在");
                            continue;
                        }

                        // 2. 检查屏幕编号是否越界
                        if (screenNumber < 0 || screenNumber >= currentScreens.Length)
                        {
                            warnings.Add($"屏幕 {screenNumber}：系统屏幕数量不足（当前 {currentScreens.Length} 个）");
                            continue;
                        }

                        // 3. 检查分辨率是否匹配（直接使用内存中的 Bitmap，避免重复 I/O）
                        try
                        {
                            var screenshot = _screenScreenshots[screenNumber];
                            var currentScreenBounds = currentScreens[screenNumber].Bounds;
                            if (screenshot.Width != currentScreenBounds.Width
                                || screenshot.Height != currentScreenBounds.Height)
                            {
                                warnings.Add($"屏幕 {screenNumber}：分辨率不匹配"
                                    + $"（配置: {screenshot.Width}×{screenshot.Height}，当前: {currentScreenBounds.Width}×{currentScreenBounds.Height}）");
                            }
                            validScreenNumbers.Add(screenNumber);
                        }
                        catch (ArgumentException ex)
                        {
                            warnings.Add($"屏幕 {screenNumber}：截图文件损坏，无法加载（{ex.Message}）");
                            continue;
                        }
                    }

                    // 设置下拉框依赖的数据源
                    _capturedScreenNumbers = validScreenNumbers;

                    // 汇总警告
                    if (warnings.Count > 0)
                    {
                        MessageBox.Show(
                            "加载配置时发现以下问题：\n\n" + string.Join("\n", warnings),
                            "配置兼容性警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // 刷新屏幕下拉框
                    RefreshScreenComboBox();

                    // 切换到第一个有数据的屏幕
                    if (_capturedScreenNumbers.Count > 0)
                    {
                        _currentScreenNumber = _capturedScreenNumbers[0];
                        SwitchScreen(_currentScreenNumber);
                    }
                    else if (_screenScreenshots.Count > 0)
                    {
                        // 兜底：没有任何有效屏幕时，尝试切换到最小编号的截图（即使校验失败）
                        _currentScreenNumber = _screenScreenshots.Keys.Min();
                        SwitchScreen(_currentScreenNumber);
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

                // 保存所有屏幕的截图
                foreach (var kvp in _screenScreenshots)
                {
                    var screenshotPath = GetScreenshotPath(kvp.Key);
                    kvp.Value?.Save(screenshotPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                // 收集所有区域
                var allVerificationAreas = _screenVerificationAreas.Values.SelectMany(list => list).ToList();
                var allCollectionAreas = _screenCollectionAreas.Values.SelectMany(list => list).ToList();

                var config = new CaptureSettings
                {
                    VerificationAreas = allVerificationAreas,
                    CollectionAreas = allCollectionAreas,
                    OcrEngine = _ocrEngineComboBox.Text
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);

                // 停止监测，避免保存操作触发变更通知
                var wasWatching = _configFileWatcher != null;
                if (wasWatching) StopConfigFileWatcher();

                File.WriteAllText(GetConfigPath(), json);

                // 恢复监测
                if (wasWatching) StartConfigFileWatcher();

                _statusLabel.Text = "配置已保存";
                _isModify = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 配置文件监测

        private void StartConfigFileWatcher()
        {
            // 每次启动前先停止旧的 watcher
            StopConfigFileWatcher();

            var configPath = GetConfigPath();
            var dataDir = GetDataDir();
            var fileName = Path.GetFileName(configPath);

            _configFileWatcher = new FileSystemWatcher(dataDir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _configFileWatcher.Changed += OnConfigFileChanged;
        }

        private void StopConfigFileWatcher()
        {
            if (_configFileWatcher != null)
            {
                _configFileWatcher.EnableRaisingEvents = false;
                _configFileWatcher.Changed -= OnConfigFileChanged;
                _configFileWatcher.Dispose();
                _configFileWatcher = null;
            }
        }

        // FileSystemWatcher 在后台线程触发，需 Invoke 切回 UI 线程
        private void OnConfigFileChanged(object _, FileSystemEventArgs __)
        {
            // 第一个回调会 Dispose watcher，后续入队的回调看到 watcher == null 就会直接返回
            if (_configFileWatcher == null) return;

            BeginInvoke(new Action(() =>
            {
                if (_configFileWatcher == null) return;
                StopConfigFileWatcher();

                var result = MessageBox.Show(
                    "检测到配置文件已变更，是否重新加载？",
                    "配置文件已修改", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    LoadConfig();

                    _statusLabel.Text = "配置已重新加载";
                }
            }));
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
            EditVerificationArea(index);
        }

        private void CollectionListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null || listView.SelectedItems.Count == 0) return;

            int index = (int)listView.SelectedItems[0].Tag;
            EditCollectionArea(index);
        }

        private void DeleteVerificationArea(int index)
        {
            var currentAreas = GetCurrentVerificationAreas();
            if (index >= 0 && index < currentAreas.Count)
            {
                var area = currentAreas[index];
                var result = MessageBox.Show($"确定要删除检测区域 \"{Path.GetFileNameWithoutExtension(area.FileName)}\" 吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    currentAreas.RemoveAt(index);
                    _isModify = true;
                    RefreshVerificationList();

                    // 删除后重置选中索引
                    if (_selectedVerificationIndex >= currentAreas.Count)
                    {
                        _selectedVerificationIndex = currentAreas.Count > 0 ? currentAreas.Count - 1 : -1;
                    }

                    _imagePanel.Invalidate();
                    _statusLabel.Text = "检测区域已删除";
                }
            }
        }

        private void DeleteCollectionArea(int index)
        {
            var currentAreas = GetCurrentCollectionAreas();
            if (index >= 0 && index < currentAreas.Count)
            {
                var area = currentAreas[index];
                var result = MessageBox.Show($"确定要删除采集区域 \"{area.Name}\" 吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    currentAreas.RemoveAt(index);
                    _isModify = true;
                    RefreshCollectionList();

                    // 删除后重置选中索引
                    if (_selectedCollectionIndex >= currentAreas.Count)
                    {
                        _selectedCollectionIndex = currentAreas.Count > 0 ? currentAreas.Count - 1 : -1;
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
                    var currentVerificationAreas = GetCurrentVerificationAreas();
                    if (_selectedVerificationIndex < 0 && currentVerificationAreas.Count > 0)
                    {
                        _selectedVerificationIndex = 0;
                    }
                    _selectedCollectionIndex = -1;
                }
                else
                {
                    var currentCollectionAreas = GetCurrentCollectionAreas();
                    if (_selectedCollectionIndex < 0 && currentCollectionAreas.Count > 0)
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
