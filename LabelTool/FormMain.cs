using PluginInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public class FormMain : Form
    {
        private const string Title = "截屏采集标注工具 V1.2";

        // 截屏图片
        private Bitmap _screenshot;

        // 鼠标拖拽状态
        private Point _dragStart;
        private Point _dragEnd;
        private bool _isDragging;
        private Rectangle _currentRect;
        private bool _isDraggingArea; // 是否在拖拽已选中的区域
        private bool _isResizingArea; // 是否在缩放已选中的区域
        private int _resizeHandle = -1; // 缩放手柄编号（0-7）

        // 区域列表
        private List<ImageVerificationArea> _verificationAreas = new List<ImageVerificationArea>();
        private List<ImageCollectionArea> _collectionAreas = new List<ImageCollectionArea>();

        // 当前选中的区域
        private int _selectedVerificationIndex = -1;
        private int _selectedCollectionIndex = -1;

        // 当前操作模式
        private bool _isVerificationMode = true; // true=检测区域, false=采集区域

        // 匹配阈值
        private float _matchThreshold = 0.8f;

        // 当前屏幕编号
        private int _screenNumber = 0;

        // 是否有未保存的更改
        private bool _isModify = false;

        // 颜色定义（红绿色盲友好）
        private Color VERIFICATION_COLOR = Color.FromArgb(0, 0, 255); // 蓝色
        private Color COLLECTION_COLOR = Color.FromArgb(255, 128, 0); // 橙色

        // 控件
        private ToolStrip _toolStrip;
        private Panel _imagePanel;
        private SplitContainer _verticalSplit;
        private Panel _listPanel;
        private GroupBox _verificationGroup;
        private GroupBox _collectionGroup;
        private ListView _verificationListView;
        private ListView _collectionListView;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripSeparator _toolStripSeparator1;
        private ToolStripLabel _toolStripLabel1;
        private ToolStripComboBox _thresholdComboBox;
        private RadioButton _radioVerification;
        private RadioButton _radioCollection;
        private Label _lblTabHint;
        private ToolTip _toolTip;
        private ToolStripButton _btnCapture;
        private ToolStripButton _btnSave;
        private ToolStripButton _btnOpenConfig;
        private ToolStripButton _btnAbout;
        private ToolStripLabel _ocrEngineLabel;
        private ToolStripComboBox _ocrEngineComboBox;

        public FormMain()
        {
            InitializeComponent();
            // 设置应用图标（从文件加载）
            var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            var appIconPath = Path.Combine(resourcesDir, "Application.png");
            if (File.Exists(appIconPath))
            {
                using (var bmp = new Bitmap(appIconPath))
                {
                    this.Icon = Icon.FromHandle(bmp.GetHicon());
                }
            }
            // 启用键盘快捷键捕获
            this.KeyPreview = true;
            this.KeyDown += FormMain_KeyDown;
            // 启动时最大化窗口
            WindowState = FormWindowState.Maximized;
            this.Load += FormMain_Load;
            this.FormClosing += FormMain_FormClosing;
        }

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
            _toolTip.SetToolTip(_toolStrip, "工具栏：重新截屏 | 保存配置 | 用记事本打开配置");

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
            _radioVerification.Checked = true;
            _isVerificationMode = true;

            // 更新状态栏
            _statusLabel.Text = "就绪";
        }

        /// <summary>
        /// 选择屏幕并截屏
        /// </summary>
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

        private void InitializeComponent()
        {
            this._toolStrip = new System.Windows.Forms.ToolStrip();
            this._imagePanel = new System.Windows.Forms.Panel();
            // 启用双缓冲，减少闪烁
            this._imagePanel.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(this._imagePanel, true, null);
            this._listPanel = new System.Windows.Forms.Panel();
            this._verificationGroup = new System.Windows.Forms.GroupBox();
            this._collectionGroup = new System.Windows.Forms.GroupBox();
            this._statusStrip = new System.Windows.Forms.StatusStrip();
            this._statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this._toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this._thresholdComboBox = new System.Windows.Forms.ToolStripComboBox();
            this._radioVerification = new System.Windows.Forms.RadioButton();
            this._radioCollection = new System.Windows.Forms.RadioButton();

            // ToolStrip
            // 加载图标资源
            var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            var screenshotIcon = LoadIconFromResources(resourcesDir, "Screenshot.png");
            var saveIcon = LoadIconFromResources(resourcesDir, "Save.png");
            var openIcon = LoadIconFromResources(resourcesDir, "Open.png");
            var aboutIcon = SystemIcons.Information.ToBitmap();

            // ToolStrip
            _btnCapture = new ToolStripButton("重新截屏 Ctrl+R", screenshotIcon, BtnCapture_Click);
            _btnSave = new ToolStripButton("保存配置 Ctrl+S", saveIcon, BtnSave_Click);
            _btnOpenConfig = new ToolStripButton("用记事本打开配置 Ctrl+O", openIcon, BtnOpenConfig_Click);
            _btnAbout = new ToolStripButton("关于", aboutIcon, BtnAbout_Click);
            this._toolStrip.Items.Add(_btnCapture);
            this._toolStrip.Items.Add(_btnSave);
            this._toolStrip.Items.Add(_btnOpenConfig);
            this._toolStrip.Items.Add(this._toolStripSeparator1);
            this._toolStrip.Items.Add(this._toolStripLabel1);
            this._toolStrip.Items.Add(this._thresholdComboBox);
            // 使用 Spring 属性将关于按钮推到右边
            this._toolStrip.Items.Add(new ToolStripSeparator());
            _btnAbout.Alignment = ToolStripItemAlignment.Right;
            this._toolStrip.Items.Add(_btnAbout);
            this._toolStrip.Location = new Point(0, 0);
            this._toolStrip.Name = "_toolStrip";
            this._toolStrip.Size = new Size(1024, 25);
            this._toolStrip.TabIndex = 1;

            // 设置 ToolTip（在 FormMain_Load 中初始化 _toolTip 后设置）

            // 匹配阈值下拉框
            this._toolStripLabel1.Text = "匹配阈值:";
            this._thresholdComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this._thresholdComboBox.Items.AddRange(new object[] { "0.7", "0.8", "0.85", "0.9", "0.95" });
            this._thresholdComboBox.SelectedIndex = 1; // 默认0.8
            this._thresholdComboBox.SelectedIndexChanged += ThresholdComboBox_SelectedIndexChanged;

            // OCR引擎下拉框
            var toolStripSeparator2 = new ToolStripSeparator();
            this._ocrEngineLabel = new ToolStripLabel();
            this._ocrEngineLabel.Text = "OCR引擎:";
            this._ocrEngineComboBox = new ToolStripComboBox();
            this._ocrEngineComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this._ocrEngineComboBox.Items.AddRange(new object[] { "PaddleOCR", "OpenCvSharp" });
            this._ocrEngineComboBox.SelectedIndex = 0; // 默认PaddleOCR
            this._ocrEngineComboBox.SelectedIndexChanged += OcrEngineComboBox_SelectedIndexChanged;
            this._toolStrip.Items.Add(toolStripSeparator2);
            this._toolStrip.Items.Add(this._ocrEngineLabel);
            this._toolStrip.Items.Add(this._ocrEngineComboBox);

            // 垂直SplitContainer（左侧图片，右侧列表）
            this._verticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700,
                BorderStyle = BorderStyle.FixedSingle
            };

            // ImagePanel
            this._imagePanel.BackColor = Color.Black;
            this._imagePanel.Dock = DockStyle.Fill;
            this._imagePanel.Name = "_imagePanel";
            this._imagePanel.Size = new Size(698, 398);
            this._imagePanel.TabIndex = 0;
            this._imagePanel.Paint += ImagePanel_Paint;
            this._imagePanel.MouseDown += ImagePanel_MouseDown;
            this._imagePanel.MouseMove += ImagePanel_MouseMove;
            this._imagePanel.MouseUp += ImagePanel_MouseUp;
            this._imagePanel.MouseClick += ImagePanel_MouseClick;
            this._imagePanel.MouseDoubleClick += ImagePanel_MouseDoubleClick;
            this._imagePanel.KeyDown += ImagePanel_KeyDown;
            this._imagePanel.TabIndex = 0;
            this._imagePanel.TabStop = true;

            // ListPanel
            this._listPanel.Dock = DockStyle.Fill;
            this._listPanel.Name = "_listPanel";

            this._verticalSplit.Panel1.Controls.Add(this._imagePanel);
            this._verticalSplit.Panel2.Controls.Add(this._listPanel);

            // 不再需要水平SplitContainer，直接使用垂直SplitContainer
            // SplitContainer - 水平分割（检测区域在上，采集区域在下） - 已移除

            // VerificationGroup
            this._verificationGroup.Dock = DockStyle.Top;
            this._verificationGroup.Name = "_verificationGroup";
            this._verificationGroup.Size = new Size(318, 200);
            this._verificationGroup.TabIndex = 0;
            this._verificationGroup.TabStop = false;
            this._verificationGroup.Text = "检测区域";
            this._verificationGroup.Height = 200;

            // Verification ListView
            this._verificationListView = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                MultiSelect = false,
                OwnerDraw = true
            };
            this._verificationListView.Columns.Add("名称", 80);
            this._verificationListView.Columns.Add("位置", 150);
            this._verificationListView.Columns.Add("", 30);
            this._verificationListView.SelectedIndexChanged += VerificationListView_SelectedIndexChanged;
            this._verificationListView.MouseClick += VerificationListView_MouseClick;
            this._verificationListView.MouseDoubleClick += VerificationListView_MouseDoubleClick;
            this._verificationListView.DrawColumnHeader += ListView_DrawColumnHeader;
            this._verificationListView.DrawSubItem += VerificationListView_DrawSubItem;
            this._verificationGroup.Controls.Add(this._verificationListView);

            // CollectionGroup
            this._collectionGroup.Dock = DockStyle.Fill;
            this._collectionGroup.Name = "_collectionGroup";
            this._collectionGroup.Size = new Size(318, 110);
            this._collectionGroup.TabIndex = 1;
            this._collectionGroup.TabStop = false;
            this._collectionGroup.Text = "采集区域";
            this._collectionGroup.Height = 300;

            // Collection ListView
            this._collectionListView = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                MultiSelect = false,
                OwnerDraw = true
            };
            this._collectionListView.Columns.Add("名称", 80);
            this._collectionListView.Columns.Add("位置", 150);
            this._collectionListView.Columns.Add("", 30);
            this._collectionListView.Columns.Add("识别结果", 200);
            this._collectionListView.SelectedIndexChanged += CollectionListView_SelectedIndexChanged;
            this._collectionListView.MouseClick += CollectionListView_MouseClick;
            this._collectionListView.MouseDoubleClick += CollectionListView_MouseDoubleClick;
            this._collectionListView.DrawColumnHeader += ListView_DrawColumnHeader;
            this._collectionListView.DrawSubItem += CollectionListView_DrawSubItem;
            this._collectionGroup.Controls.Add(this._collectionListView);

            // Radio buttons
            this._radioVerification.AutoSize = true;
            this._radioVerification.Name = "_radioVerification";
            this._radioVerification.Size = new Size(83, 16);
            this._radioVerification.TabIndex = 3;
            this._radioVerification.TabStop = true;
            this._radioVerification.Text = "检测区域";
            this._radioVerification.UseVisualStyleBackColor = true;
            this._radioVerification.CheckedChanged += RadioVerification_CheckedChanged;

            this._radioCollection.AutoSize = true;
            this._radioCollection.Name = "_radioCollection";
            this._radioCollection.Size = new Size(83, 16);
            this._radioCollection.TabIndex = 4;
            this._radioCollection.TabStop = true;
            this._radioCollection.Text = "采集区域";
            this._radioCollection.UseVisualStyleBackColor = true;
            this._radioCollection.CheckedChanged += RadioCollection_CheckedChanged;

            // StatusStrip
            this._statusStrip.Items.AddRange(new ToolStripItem[] { this._statusLabel });
            this._statusStrip.Location = new Point(0, 571);
            this._statusStrip.Name = "_statusStrip";
            this._statusStrip.Size = new Size(1024, 22);
            this._statusStrip.TabIndex = 5;

            // StatusLabel
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Size = new Size(50, 17);
            this._statusLabel.Text = "就绪";

            // ListPanel容器 - 包含GroupBox和RadioButton
            var listContainer = new Panel { Dock = DockStyle.Fill };

            // RadioButton Panel
            var radioPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35
            };
            this._lblTabHint = new Label
            {
                Text = "Tab键切换",
                Location = new Point(210, 6),
                AutoSize = true,
                ForeColor = Color.Fuchsia,
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            radioPanel.Controls.Add(this._lblTabHint);
            radioPanel.Controls.Add(this._radioVerification);
            radioPanel.Controls.Add(this._radioCollection);
            this._radioVerification.Location = new Point(10, 8);
            this._radioCollection.Location = new Point(120, 8);

            listContainer.Controls.Add(this._collectionGroup);
            listContainer.Controls.Add(this._verificationGroup);
            listContainer.Controls.Add(radioPanel);

            this._verificationGroup.Location = new Point(0, 0);
            this._collectionGroup.Location = new Point(0, 200);
            this._listPanel.Controls.Add(listContainer);
            listContainer.Dock = DockStyle.Fill;

            // FormMain
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1024, 593);
            this.Controls.Add(this._verticalSplit);
            this.Controls.Add(this._toolStrip);
            this.Controls.Add(this._statusStrip);
            this.Name = "FormMain";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = Title;
        }

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
            RefreshCollectionListOcrResults();
        }

        #endregion

        #region 截屏功能

        private void CaptureScreen()
        {
            CaptureScreen(_screenNumber);
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

                _imagePanel.Invalidate();
                _statusLabel.Text = $"截屏完成: {_screenshot.Width}x{_screenshot.Height}";
                RefreshCollectionListOcrResults();
            }
            catch (Exception ex)
            {
                this.Show();
                MessageBox.Show($"截屏失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 鼠标拖拽框选

        private void ImagePanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 聚焦到图片面板以接收键盘事件
            _imagePanel.Focus();

            float scaleX = (float)_imagePanel.Width / _screenshot.Width;
            float scaleY = (float)_imagePanel.Height / _screenshot.Height;

            // 确保选中索引有效
            if (_selectedVerificationIndex >= _verificationAreas.Count)
                _selectedVerificationIndex = -1;
            if (_selectedCollectionIndex >= _collectionAreas.Count)
                _selectedCollectionIndex = -1;

            // 检查是否点击了选中区域的边缘（缩放）或内部（移动）
            if (_selectedVerificationIndex >= 0)
            {
                var area = _verificationAreas[_selectedVerificationIndex];
                var rect = GetScaledRect(area, scaleX, scaleY);
                int handle = GetResizeHandle(rect, e.Location);

                if (handle >= 0)
                {
                    // 点击了缩放手柄
                    _isResizingArea = true;
                    _resizeHandle = handle;
                }
                else if (rect.Contains(e.Location))
                {
                    // 点击了区域内部，开始拖拽移动
                    _isDraggingArea = true;
                }
            }
            else if (_selectedCollectionIndex >= 0)
            {
                var area = _collectionAreas[_selectedCollectionIndex];
                var rect = GetScaledRect(area, scaleX, scaleY);
                int handle = GetResizeHandle(rect, e.Location);

                if (handle >= 0)
                {
                    _isResizingArea = true;
                    _resizeHandle = handle;
                }
                else if (rect.Contains(e.Location))
                {
                    _isDraggingArea = true;
                }
            }

            if (_isDraggingArea || _isResizingArea)
            {
                _dragStart = e.Location;
                _isDragging = true;
            }
            else
            {
                // 新建区域
                _dragStart = e.Location;
                _isDragging = true;
            }
        }

        private void ImagePanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            float scaleX = (float)_imagePanel.Width / _screenshot.Width;
            float scaleY = (float)_imagePanel.Height / _screenshot.Height;
            int deltaImgX = (int)((e.Location.X - _dragStart.X) / scaleX);
            int deltaImgY = (int)((e.Location.Y - _dragStart.Y) / scaleY);

            if (_screenshot == null || !_isDragging) return;

            if (_isDraggingArea)
            {
                // 拖拽移动选中区域
                if (_selectedVerificationIndex >= 0)
                {
                    var area = _verificationAreas[_selectedVerificationIndex];
                    area.TopLeftX += deltaImgX;
                    area.TopLeftY += deltaImgY;
                    RefreshVerificationList();
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    var area = _collectionAreas[_selectedCollectionIndex];
                    area.TopLeftX += deltaImgX;
                    area.TopLeftY += deltaImgY;
                    RefreshCollectionList();
                }
                _dragStart = e.Location;
                _imagePanel.Invalidate();
            }
            else if (_isResizingArea)
            {
                // 缩放选中区域
                if (_selectedVerificationIndex >= 0)
                {
                    var area = _verificationAreas[_selectedVerificationIndex];
                    ResizeArea(area, _resizeHandle, deltaImgX, deltaImgY);
                    RefreshVerificationList();
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    var area = _collectionAreas[_selectedCollectionIndex];
                    ResizeArea(area, _resizeHandle, deltaImgX, deltaImgY);
                    RefreshCollectionList();
                }
                _dragStart = e.Location;
                _imagePanel.Invalidate();
            }
            else
            {
                // 新建区域
                _dragEnd = e.Location;
                _currentRect = GetRectangle(_dragStart, _dragEnd);
                _imagePanel.Invalidate();
            }
        }

        private void ImagePanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_screenshot == null || !_isDragging) return;

            // 如果是拖拽或缩放区域，结束操作
            if (_isDraggingArea || _isResizingArea)
            {
                _isDraggingArea = false;
                _isResizingArea = false;
                _isDragging = false;
                _resizeHandle = -1;
                _imagePanel.Invalidate();
                return;
            }

            _dragEnd = e.Location;
            _currentRect = GetRectangle(_dragStart, _dragEnd);

            _isDragging = false;
            _dragEnd = e.Location;
            _currentRect = GetRectangle(_dragStart, _dragEnd);

            // 确保矩形有效
            if (_currentRect.Width > 10 && _currentRect.Height > 10)
            {
                // 弹出对话框确认区域属性
                ShowAreaDialog(_currentRect);
            }

            _imagePanel.Invalidate();
        }

        private void ImagePanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 检查是否点击了已有区域
            var clickPoint = e.Location;

            // 缩放比例
            float scaleX = (float)_imagePanel.Width / _screenshot.Width;
            float scaleY = (float)_imagePanel.Height / _screenshot.Height;
            int imgX = (int)(clickPoint.X / scaleX);
            int imgY = (int)(clickPoint.Y / scaleY);

            // 检查检测区域
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                if (rect.Contains(imgX, imgY))
                {
                    _selectedVerificationIndex = i;
                    _selectedCollectionIndex = -1;
                    SelectVerificationItem(i);
                    _imagePanel.Invalidate();
                    return;
                }
            }

            // 检查采集区域
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                if (rect.Contains(imgX, imgY))
                {
                    _selectedCollectionIndex = i;
                    _selectedVerificationIndex = -1;
                    SelectCollectionItem(i);
                    _imagePanel.Invalidate();
                    return;
                }
            }

            // 点击空白区域，取消选择
            _selectedVerificationIndex = -1;
            _selectedCollectionIndex = -1;
            _verificationListView.SelectedItems.Clear();
            _collectionListView.SelectedItems.Clear();
            _imagePanel.Invalidate();
        }

        private Rectangle GetRectangle(Point start, Point end)
        {
            int x = Math.Min(start.X, end.X);
            int y = Math.Min(start.Y, end.Y);
            int width = Math.Abs(end.X - start.X);
            int height = Math.Abs(end.Y - start.Y);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 获取缩放后的区域矩形
        /// </summary>
        private Rectangle GetScaledRect(ImageVerificationArea area, float scaleX, float scaleY)
        {
            return new Rectangle(
                (int)(area.TopLeftX * scaleX),
                (int)(area.TopLeftY * scaleY),
                (int)(area.Width * scaleX),
                (int)(area.Height * scaleY));
        }

        private Rectangle GetScaledRect(ImageCollectionArea area, float scaleX, float scaleY)
        {
            return new Rectangle(
                (int)(area.TopLeftX * scaleX),
                (int)(area.TopLeftY * scaleY),
                (int)(area.Width * scaleX),
                (int)(area.Height * scaleY));
        }

        /// <summary>
        /// 获取缩放手柄编号（-1表示不在手柄范围内）
        /// 手柄编号：0-左上，1-上，2-右上，3-右，4-右下，5-下，6-左下，7-左
        /// </summary>
        private int GetResizeHandle(Rectangle rect, Point point)
        {
            int handleSize = 8;
            // 八个方向的检测区域
            var handles = new Dictionary<int, Rectangle>
            {
                { 0, new Rectangle(rect.X - handleSize/2, rect.Y - handleSize/2, handleSize, handleSize) }, // 左上
                { 1, new Rectangle(rect.X + rect.Width/2 - handleSize/2, rect.Y - handleSize/2, handleSize, handleSize) }, // 上
                { 2, new Rectangle(rect.X + rect.Width - handleSize/2, rect.Y - handleSize/2, handleSize, handleSize) }, // 右上
                { 3, new Rectangle(rect.X + rect.Width - handleSize/2, rect.Y + rect.Height/2 - handleSize/2, handleSize, handleSize) }, // 右
                { 4, new Rectangle(rect.X + rect.Width - handleSize/2, rect.Y + rect.Height - handleSize/2, handleSize, handleSize) }, // 右下
                { 5, new Rectangle(rect.X + rect.Width/2 - handleSize/2, rect.Y + rect.Height - handleSize/2, handleSize, handleSize) }, // 下
                { 6, new Rectangle(rect.X - handleSize/2, rect.Y + rect.Height - handleSize/2, handleSize, handleSize) }, // 左下
                { 7, new Rectangle(rect.X - handleSize/2, rect.Y + rect.Height/2 - handleSize/2, handleSize, handleSize) }, // 左
            };

            foreach (var kvp in handles)
            {
                if (kvp.Value.Contains(point))
                    return kvp.Key;
            }
            return -1;
        }

        /// <summary>
        /// 缩放区域
        /// </summary>
        private void ResizeArea(ImageVerificationArea area, int handle, int deltaX, int deltaY)
        {
            switch (handle)
            {
                case 0: // 左上
                    area.TopLeftX += deltaX;
                    area.TopLeftY += deltaY;
                    area.Width -= deltaX;
                    area.Height -= deltaY;
                    break;
                case 1: // 上
                    area.TopLeftY += deltaY;
                    area.Height -= deltaY;
                    break;
                case 2: // 右上
                    area.TopLeftY += deltaY;
                    area.Width += deltaX;
                    area.Height -= deltaY;
                    break;
                case 3: // 右
                    area.Width += deltaX;
                    break;
                case 4: // 右下
                    area.Width += deltaX;
                    area.Height += deltaY;
                    break;
                case 5: // 下
                    area.Height += deltaY;
                    break;
                case 6: // 左下
                    area.TopLeftX += deltaX;
                    area.Width -= deltaX;
                    area.Height += deltaY;
                    break;
                case 7: // 左
                    area.TopLeftX += deltaX;
                    area.Width -= deltaX;
                    break;
            }
            // 确保尺寸有效
            if (area.Width < 10) area.Width = 10;
            if (area.Height < 10) area.Height = 10;
        }

        private void ResizeArea(ImageCollectionArea area, int handle, int deltaX, int deltaY)
        {
            switch (handle)
            {
                case 0:
                    area.TopLeftX += deltaX;
                    area.TopLeftY += deltaY;
                    area.Width -= deltaX;
                    area.Height -= deltaY;
                    break;
                case 1:
                    area.TopLeftY += deltaY;
                    area.Height -= deltaY;
                    break;
                case 2:
                    area.TopLeftY += deltaY;
                    area.Width += deltaX;
                    area.Height -= deltaY;
                    break;
                case 3:
                    area.Width += deltaX;
                    break;
                case 4:
                    area.Width += deltaX;
                    area.Height += deltaY;
                    break;
                case 5:
                    area.Height += deltaY;
                    break;
                case 6:
                    area.TopLeftX += deltaX;
                    area.Width -= deltaX;
                    area.Height += deltaY;
                    break;
                case 7:
                    area.TopLeftX += deltaX;
                    area.Width -= deltaX;
                    break;
            }
            if (area.Width < 10) area.Width = 10;
            if (area.Height < 10) area.Height = 10;
        }

        /// <summary>
        /// 双击编辑区域
        /// </summary>
        private void ImagePanel_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            float scaleX = (float)_imagePanel.Width / _screenshot.Width;
            float scaleY = (float)_imagePanel.Height / _screenshot.Height;

            // 检查是否点击了检测区域
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var rect = GetScaledRect(area, scaleX, scaleY);
                if (rect.Contains(e.Location))
                {
                    EditVerificationArea(i);
                    return;
                }
            }

            // 检查是否点击了采集区域
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var rect = GetScaledRect(area, scaleX, scaleY);
                if (rect.Contains(e.Location))
                {
                    EditCollectionArea(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 键盘事件 - Delete 删除选中区域, Tab 切换检测/采集区域
        /// </summary>
        private void ImagePanel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                // 切换检测/采集模式
                _isVerificationMode = !_isVerificationMode;

                // 更新RadioButton状态
                _radioVerification.Checked = _isVerificationMode;
                _radioCollection.Checked = !_isVerificationMode;

                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (_selectedVerificationIndex >= 0)
                {
                    DeleteVerificationArea(_selectedVerificationIndex);
                    e.Handled = true;
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    DeleteCollectionArea(_selectedCollectionIndex);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 编辑检测区域
        /// </summary>
        private void EditVerificationArea(int index)
        {
            if (index < 0 || index >= _verificationAreas.Count) return;

            var area = _verificationAreas[index];
            var dialog = new FormAreaDialog(true, area.MatchThreshold, area.FileName, area.TopLeftX, area.TopLeftY, area.Width, area.Height);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                area.TopLeftX = dialog.AreaX;
                area.TopLeftY = dialog.AreaY;
                area.Width = dialog.AreaWidth;
                area.Height = dialog.AreaHeight;
                area.FileName = dialog.AreaName + ".png";
                area.MatchThreshold = dialog.MatchThreshold;
                RefreshVerificationList();
                _imagePanel.Invalidate();
            }
        }

        /// <summary>
        /// 编辑采集区域
        /// </summary>
        private void EditCollectionArea(int index)
        {
            if (index < 0 || index >= _collectionAreas.Count) return;

            var area = _collectionAreas[index];
            var dialog = new FormAreaDialog(false, 0.8f, area.Name, area.TopLeftX, area.TopLeftY, area.Width, area.Height);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                area.TopLeftX = dialog.AreaX;
                area.TopLeftY = dialog.AreaY;
                area.Width = dialog.AreaWidth;
                area.Height = dialog.AreaHeight;
                area.Name = dialog.AreaName;
                RefreshCollectionList();
                _imagePanel.Invalidate();
            }
        }

        #endregion

        #region 区域对话框

        private void ShowAreaDialog(Rectangle rect)
        {
            // 缩放比例（图片控件坐标 -> 图片真实坐标）
            float scaleX = (float)_screenshot.Width / _imagePanel.Width;
            float scaleY = (float)_screenshot.Height / _imagePanel.Height;

            int imgX = (int)(rect.X * scaleX);
            int imgY = (int)(rect.Y * scaleY);
            int imgWidth = (int)(rect.Width * scaleX);
            int imgHeight = (int)(rect.Height * scaleY);

            if (_isVerificationMode)
            {
                // 检测区域
                string defaultName = $"检测区域{_verificationAreas.Count + 1}";
                var dialog = new FormAreaDialog(true, _matchThreshold, defaultName, imgX, imgY, imgWidth, imgHeight);
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
                var dialog = new FormAreaDialog(false, _matchThreshold, defaultName, imgX, imgY, imgWidth, imgHeight);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var area = new ImageCollectionArea
                    {
                        Name = dialog.AreaName,
                        TopLeftX = imgX,
                        TopLeftY = imgY,
                        Width = imgWidth,
                        Height = imgHeight
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

        #region 绘制区域

        private void ImagePanel_Paint(object sender, PaintEventArgs e)
        {
            if (_screenshot == null)
            {
                e.Graphics.Clear(Color.Black);
                using (var font = new Font("Microsoft Sans Serif", 14))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var text = "请点击\"重新截屏\"获取屏幕截图";
                    var size = e.Graphics.MeasureString(text, font);
                    var x = (_imagePanel.Width - size.Width) / 2;
                    var y = (_imagePanel.Height - size.Height) / 2;
                    e.Graphics.DrawString(text, font, brush, x, y);
                }
                return;
            }

            // 绘制截屏图片（缩放以适应面板）
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_screenshot, 0, 0, _imagePanel.Width, _imagePanel.Height);

            // 计算缩放比例
            float scaleX = (float)_imagePanel.Width / _screenshot.Width;
            float scaleY = (float)_imagePanel.Height / _screenshot.Height;

            // 绘制检测区域（蓝色）
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var rect = new Rectangle(
                    (int)(area.TopLeftX * scaleX),
                    (int)(area.TopLeftY * scaleY),
                    (int)(area.Width * scaleX),
                    (int)(area.Height * scaleY));

                var frontColor = i == _selectedVerificationIndex ? Color.Cyan : Color.LightSkyBlue;
                var backColor =  Color.FromArgb(200, Color.Black);
                DrawArea(e.Graphics, rect, frontColor, backColor, "检测: " + Path.GetFileNameWithoutExtension(area.FileName));
            }

            // 绘制采集区域（橙色）
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var rect = new Rectangle(
                    (int)(area.TopLeftX * scaleX),
                    (int)(area.TopLeftY * scaleY),
                    (int)(area.Width * scaleX),
                    (int)(area.Height * scaleY));

                var frontColor = i == _selectedCollectionIndex ? Color.Yellow : COLLECTION_COLOR;
                var backColor = Color.FromArgb(200, Color.Black);
                DrawArea(e.Graphics, rect, frontColor, backColor, area.Name);
            }

            // 绘制当前拖拽的矩形
            if (_isDragging && _currentRect.Width > 0 && _currentRect.Height > 0)
            {
                var color = _isVerificationMode ? VERIFICATION_COLOR : COLLECTION_COLOR;
                using (var pen = new Pen(color, 2))
                using (var brush = new SolidBrush(Color.FromArgb(50, color)))
                {
                    e.Graphics.FillRectangle(brush, _currentRect);
                    e.Graphics.DrawRectangle(pen, _currentRect);
                }
            }
        }

        private void DrawArea(Graphics g, Rectangle rect, Color frontColor, Color backColor, string label)
        {
            using (var pen = new Pen(frontColor, 2))
            using (var brush = new SolidBrush(Color.FromArgb(50, frontColor)))
            using (var font = new Font("Microsoft Sans Serif", 8))
            using (var textBrush = new SolidBrush(frontColor))
            {
                // 填充半透明背景
                g.FillRectangle(brush, rect);
                // 绘制边框
                g.DrawRectangle(pen, rect);
                // 绘制标签
                var size = g.MeasureString(label, font);
                g.FillRectangle(new SolidBrush(Color.FromArgb(200, backColor)),
                    rect.X, rect.Y - (int)size.Height - 2, (int)size.Width + 4, (int)size.Height + 2);
                g.DrawString(label, font, textBrush, rect.X + 2, rect.Y - (int)size.Height - 2);
            }
        }

        #endregion

        #region 列表操作

        private void RefreshVerificationList()
        {
            _verificationListView.Items.Clear();
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(area.FileName));
                item.SubItems.Add($"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})");
                item.SubItems.Add("×");
                item.Tag = i;
                _verificationListView.Items.Add(item);
            }
        }

        private void RefreshCollectionList()
        {
            _collectionListView.Items.Clear();
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var item = new ListViewItem(area.Name);
                item.SubItems.Add($"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})");
                item.SubItems.Add("×");
                item.SubItems.Add(""); // OCR识别结果列
                item.Tag = i;
                _collectionListView.Items.Add(item);
            }
            RefreshCollectionListOcrResults();
        }

        private void VerificationListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_verificationListView.SelectedItems.Count > 0)
            {
                _selectedVerificationIndex = (int)_verificationListView.SelectedItems[0].Tag;
                _selectedCollectionIndex = -1;
                _collectionListView.SelectedItems.Clear();
                _imagePanel.Invalidate();
            }
        }

        private void CollectionListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_collectionListView.SelectedItems.Count > 0)
            {
                _selectedCollectionIndex = (int)_collectionListView.SelectedItems[0].Tag;
                _selectedVerificationIndex = -1;
                _verificationListView.SelectedItems.Clear();
                _imagePanel.Invalidate();
            }
        }

        private void SelectVerificationItem(int index)
        {
            foreach (ListViewItem item in _verificationListView.Items)
            {
                if ((int)item.Tag == index)
                {
                    item.Selected = true;
                    break;
                }
            }
        }

        private void SelectCollectionItem(int index)
        {
            foreach (ListViewItem item in _collectionListView.Items)
            {
                if ((int)item.Tag == index)
                {
                    item.Selected = true;
                    break;
                }
            }
        }

        #endregion

        #region 单选按钮

        private void RadioVerification_CheckedChanged(object sender, EventArgs e)
        {
            if (_radioVerification.Checked)
            {
                _isVerificationMode = true;
            }
        }

        private void RadioCollection_CheckedChanged(object sender, EventArgs e)
        {
            if (_radioCollection.Checked)
            {
                _isVerificationMode = false;
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
                return Image.FromFile(path);
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
                    _imagePanel.Invalidate();
                    _statusLabel.Text = $"截图已加载: {_screenshot.Width}x{_screenshot.Height}";
                    RefreshCollectionListOcrResults();
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
                    ScreenNumber = _screenNumber
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

        private void VerificationListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
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

        private void CollectionListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
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

        /// <summary>
        /// 捕获键盘事件 - 处理 Tab 键切换检测/采集区域
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab)
            {
                // 切换检测/采集模式
                _isVerificationMode = !_isVerificationMode;

                // 更新RadioButton状态
                _radioVerification.Checked = _isVerificationMode;
                _radioCollection.Checked = !_isVerificationMode;

                // 切换选中区域
                if (_isVerificationMode)
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

        #region OCR功能

        /// <summary>
        /// 根据选中的索引创建对应的 OCR 服务实例
        /// </summary>
        /// <param name="engineIndex">0=PaddleOCR, 1=OpenCvSharp</param>
        private IOcrService CreateOcrService(int engineIndex)
        {
            return engineIndex == 0
                ? (IOcrService)new ScreenTextCollector.PaddleOCR.OcrService()
                : (IOcrService)new ScreenTextCollector.OpenCvSharp.OcrService();
        }

        /// <summary>
        /// 刷新采集列表中所有区域的 OCR 识别结果
        /// </summary>
        private void RefreshCollectionListOcrResults()
        {
            if (_screenshot == null || _collectionAreas.Count == 0) return;

            try
            {
                // 保存临时截图用于 OCR
                var tempPath = Path.Combine(Path.GetTempPath(), "labeltool_ocr_temp.png");
                _screenshot.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                // 根据选中的引擎创建对应的 OcrService
                IOcrService ocrService = CreateOcrService(_ocrEngineComboBox.SelectedIndex);

                // 对每个采集区域执行 OCR
                for (int i = 0; i < _collectionAreas.Count; i++)
                {
                    var area = _collectionAreas[i];
                    string result = ocrService.PerformOcr(tempPath, area);

                    // 更新 ListView OCR结果列（第4列，索引3）
                    if (i < _collectionListView.Items.Count)
                    {
                        _collectionListView.Items[i].SubItems[3].Text = result;
                    }
                }

                // 清理临时文件
                try { File.Delete(tempPath); } catch { }

                // 释放 OCR 服务资源
                if (ocrService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"OCR识别失败: {ex.Message}";
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _screenshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}
