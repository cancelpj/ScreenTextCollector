using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using PluginInterface;

namespace LabelTool
{
    public partial class FormMain : Form
    {
        private const string Title = "截屏采集标注工具 V1.3";

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
        private bool _isVerificationAreaMode = true; // true=检测区域, false=采集区域

        // 匹配阈值
        private float _matchThreshold = 0.8f;

        // 当前屏幕编号
        private int _screenNumber;

        // 是否有未保存的更改
        private bool _isModify;

        // 颜色定义（红绿色盲友好）
        private Color VERIFICATION_COLOR = Color.FromArgb(0, 0, 255); // 蓝色
        private Color COLLECTION_COLOR = Color.FromArgb(255, 128, 0); // 橙色

        // 显示模式
        private bool _isAutoZoom = true;           // 是否自动缩放模式
        private float _zoomLevel = 1.0f;            // 缩放级别 (0.1 ~ 5.0)
        private Point _lastMousePos;                // 上次鼠标位置（用于平移）
        private Point _scrollOffset = Point.Empty;   // 滚动偏移量（直接追踪，避免抖动）
        private bool _isPanning;                   // 是否正在平移
        private const float MIN_ZOOM = 0.5f;        // 最小缩放
        private const float MAX_ZOOM = 1.5f;       // 最大缩放
        private const float ZOOM_STEP = 0.1f;      // 每次缩放步进

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
        private RadioButton _radioVerificationArea;
        private RadioButton _radioCollectionArea;
        private Label _lblTabHint;
        private ToolTip _toolTip;
        private ToolStripButton _btnCapture;
        private ToolStripButton _btnSave;
        private ToolStripButton _btnOpenConfig;
        private ToolStripButton _btnAbout;
        private ToolStripLabel _ocrEngineLabel;
        private ToolStripComboBox _ocrEngineComboBox;
        private ToolStripButton _btnOcrTest;
        private ToolStripComboBox _cmbZoomMode;
        private ToolStripComboBox _cmbZoomLevel;
        private ToolStripButton _btnZoomOut;
        private TrackBar _zoomTrackBar;
        private ToolStripControlHost _zoomTrackBarHost;
        private ToolStripButton _btnZoomIn;
        private Panel _scrollContainer;

        // 手柄偏移因子（相对于 rect.X/Y 的比例）：左上、上、右上、右、右下、下、左下、左
        // 每个元素的 (X,Y) 表示 rect.X + rect.Width * X, rect.Y + rect.Height * Y
        private static readonly float[] _handleFX = { 0f, 0.5f, 1f, 1f, 1f, 0.5f, 0f, 0f };
        private static readonly float[] _handleFY = { 0f, 0f, 0f, 0.5f, 1f, 1f, 1f, 0.5f };

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
                    Icon = Icon.FromHandle(bmp.GetHicon());
                }
            }
            // 启用键盘快捷键捕获
            KeyPreview = true;
            KeyDown += FormMain_KeyDown;
            // 启动时最大化窗口
            WindowState = FormWindowState.Maximized;
            Load += FormMain_Load;
            FormClosing += FormMain_FormClosing;
        }

        private void InitializeComponent()
        {
            _toolStrip = new ToolStrip();
            _scrollContainer = new Panel();
            _imagePanel = new Panel();
            // 启用双缓冲，减少闪烁
            _imagePanel.GetType().GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_imagePanel, true, null);
            _listPanel = new Panel();
            _verificationGroup = new GroupBox();
            _collectionGroup = new GroupBox();
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel();
            _toolStripSeparator1 = new ToolStripSeparator();
            _toolStripLabel1 = new ToolStripLabel();
            _thresholdComboBox = new ToolStripComboBox();
            _radioVerificationArea = new RadioButton();
            _radioCollectionArea = new RadioButton();

            #region 工具栏 ToolStrip

            // 加载图标资源
            var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            var screenshotIcon = LoadIconFromResources(resourcesDir, "Screenshot.png");
            var saveIcon = LoadIconFromResources(resourcesDir, "Save.png");
            var openIcon = LoadIconFromResources(resourcesDir, "Open.png");
            var ocrIcon = LoadIconFromResources(resourcesDir, "ocr.png");
            var aboutIcon = SystemIcons.Information.ToBitmap();

            // ToolStrip
            _btnCapture = new ToolStripButton("重新截屏 Ctrl+R", screenshotIcon, BtnCapture_Click);
            _btnSave = new ToolStripButton("保存配置 Ctrl+S", saveIcon, BtnSave_Click);
            _btnOpenConfig = new ToolStripButton("用记事本打开配置 Ctrl+O", openIcon, BtnOpenConfig_Click);
            _toolStrip.Items.Add(_btnCapture);
            _toolStrip.Items.Add(_btnSave);
            _toolStrip.Items.Add(_btnOpenConfig);

            // 使用 Spring 属性将关于按钮推到右边
            _toolStrip.Items.Add(new ToolStripSeparator());
            _btnAbout = new ToolStripButton("关于", aboutIcon, BtnAbout_Click);
            _btnAbout.Alignment = ToolStripItemAlignment.Right;
            _toolStrip.Items.Add(_btnAbout);
            _toolStrip.Location = new Point(0, 0);
            _toolStrip.Name = "_toolStrip";
            _toolStrip.Size = new Size(1024, 25);
            _toolStrip.TabIndex = 1;

            // 匹配阈值下拉框
            _toolStrip.Items.Add(_toolStripSeparator1);
            _toolStripLabel1.Text = "匹配阈值:";
            _thresholdComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _thresholdComboBox.Items.AddRange(new object[] { "0.7", "0.8", "0.85", "0.9", "0.95" });
            _thresholdComboBox.SelectedIndex = 1; // 默认0.8
            _thresholdComboBox.SelectedIndexChanged += ThresholdComboBox_SelectedIndexChanged;
            _toolStrip.Items.Add(_toolStripLabel1);
            _toolStrip.Items.Add(_thresholdComboBox);

            // OCR引擎下拉框
            var toolStripSeparator2 = new ToolStripSeparator();
            _ocrEngineLabel = new ToolStripLabel();
            _ocrEngineLabel.Text = "OCR引擎:";
            _ocrEngineComboBox = new ToolStripComboBox();
            _ocrEngineComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _ocrEngineComboBox.Items.AddRange(new object[] { "PaddleOCR", "OpenCvSharp" });
            _ocrEngineComboBox.SelectedIndex = 0; // 默认PaddleOCR
            _ocrEngineComboBox.SelectedIndexChanged += OcrEngineComboBox_SelectedIndexChanged;
            _toolStrip.Items.Add(toolStripSeparator2);
            _toolStrip.Items.Add(_ocrEngineLabel);
            _toolStrip.Items.Add(_ocrEngineComboBox);

            // OCR测试按钮
            var toolStripSeparator3 = new ToolStripSeparator();
            _btnOcrTest = new ToolStripButton("OCR测试 Ctrl+T", ocrIcon, BtnOcrTest_Click);
            _toolStrip.Items.Add(toolStripSeparator3);
            _toolStrip.Items.Add(_btnOcrTest);

            // 缩放控制
            var toolStripSeparator4 = new ToolStripSeparator();
            _cmbZoomMode = new ToolStripComboBox();
            _cmbZoomMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbZoomMode.Items.AddRange(new object[] { "自动缩放", "手动缩放" });
            _cmbZoomMode.SelectedIndex = 0; // 默认自动缩放
            _cmbZoomMode.SelectedIndexChanged += CmbZoomMode_SelectedIndexChanged;
            _cmbZoomLevel = new ToolStripComboBox();
            _cmbZoomLevel.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbZoomLevel.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%" });
            _cmbZoomLevel.SelectedIndex = 2; // 默认100%
            _cmbZoomLevel.TextChanged += CmbZoomLevel_TextChanged;
            _cmbZoomLevel.KeyDown += CmbZoomLevel_KeyDown;
            _btnZoomOut = new ToolStripButton("-", null, BtnZoomOut_Click);
            _btnZoomOut.ToolTipText = "缩小";
            _zoomTrackBar = new TrackBar
            {
                TickStyle = TickStyle.None,
                AutoSize = false,
                Height = 20,
                Width = 100,
                Minimum = (int)(MIN_ZOOM * 100),
                Maximum = (int)(MAX_ZOOM * 100),
                Value = 100,
                Enabled = false
            };
            // TrackBar 垂直居中（ToolStrip 高度约25）
            _zoomTrackBar.Top = (_toolStrip.Height - _zoomTrackBar.Height) / 2;
            _zoomTrackBar.Scroll += ZoomTrackBar_Scroll;
            _zoomTrackBarHost = new ToolStripControlHost(_zoomTrackBar);
            _zoomTrackBarHost.Padding = Padding.Empty;
            _zoomTrackBarHost.Margin = new Padding(2, 0, 2, 0);
            _btnZoomIn = new ToolStripButton("+", null, BtnZoomIn_Click);
            _btnZoomIn.ToolTipText = "放大";

            _toolStrip.Items.Add(toolStripSeparator4);
            _toolStrip.Items.Add(_cmbZoomMode);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_cmbZoomLevel);
            _toolStrip.Items.Add(_btnZoomOut);
            _toolStrip.Items.Add(_zoomTrackBarHost);
            _toolStrip.Items.Add(_btnZoomIn);

            #endregion

            // 垂直SplitContainer（左侧图片，右侧列表）
            _verticalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700,
                BorderStyle = BorderStyle.FixedSingle
            };

            // ImagePanel 和滚动容器
            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMinSize = new Size(1, 1),
                BackColor = Color.Black
            };
            _imagePanel.BackColor = Color.Black;
            _imagePanel.Name = "_imagePanel";
            _imagePanel.Size = new Size(698, 398);
            _imagePanel.TabIndex = 0;
            _imagePanel.Paint += ImagePanel_Paint;
            _imagePanel.MouseDown += ImagePanel_MouseDown;
            _imagePanel.MouseMove += ImagePanel_MouseMove;
            _imagePanel.MouseUp += ImagePanel_MouseUp;
            _imagePanel.MouseClick += ImagePanel_MouseClick;
            _imagePanel.MouseDoubleClick += ImagePanel_MouseDoubleClick;
            _imagePanel.MouseEnter += ImagePanel_MouseEnter;
            _imagePanel.MouseLeave += ImagePanel_MouseLeave;
            _imagePanel.MouseWheel += ImagePanel_MouseWheel;
            _imagePanel.KeyDown += ImagePanel_KeyDown;
            _imagePanel.TabIndex = 0;
            _imagePanel.TabStop = true;

            // ListPanel
            _listPanel.Dock = DockStyle.Fill;
            _listPanel.Name = "_listPanel";

            _scrollContainer.Controls.Add(_imagePanel);
            _verticalSplit.Panel1.Controls.Add(_scrollContainer);
            _verticalSplit.Panel2.Controls.Add(_listPanel);

            // VerificationGroup
            _verificationGroup.Dock = DockStyle.Top;
            _verificationGroup.Name = "_verificationGroup";
            _verificationGroup.Size = new Size(318, 200);
            _verificationGroup.TabIndex = 0;
            _verificationGroup.TabStop = false;
            _verificationGroup.Text = "检测区域";
            _verificationGroup.Height = 200;

            // Verification ListView
            _verificationListView = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                MultiSelect = false,
                OwnerDraw = true
            };
            _verificationListView.Columns.Add("名称", 80);
            _verificationListView.Columns.Add("位置", 150);
            _verificationListView.Columns.Add("", 30);
            _verificationListView.SelectedIndexChanged += VerificationListView_SelectedIndexChanged;
            _verificationListView.MouseClick += VerificationListView_MouseClick;
            _verificationListView.MouseDoubleClick += VerificationListView_MouseDoubleClick;
            _verificationListView.DrawColumnHeader += ListView_DrawColumnHeader;
            _verificationListView.DrawSubItem += VerificationListView_DrawSubItem;
            _verificationGroup.Controls.Add(_verificationListView);

            // CollectionGroup
            _collectionGroup.Dock = DockStyle.Fill;
            _collectionGroup.Name = "_collectionGroup";
            _collectionGroup.Size = new Size(318, 110);
            _collectionGroup.TabIndex = 1;
            _collectionGroup.TabStop = false;
            _collectionGroup.Text = "采集区域";
            _collectionGroup.Height = 300;

            // Collection ListView
            _collectionListView = new ListView
            {
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                GridLines = true,
                View = View.Details,
                MultiSelect = false,
                OwnerDraw = true
            };
            _collectionListView.Columns.Add("名称", 80);
            _collectionListView.Columns.Add("位置", 150);
            _collectionListView.Columns.Add("", 30);
            _collectionListView.Columns.Add("识别结果", 200);
            _collectionListView.SelectedIndexChanged += CollectionListView_SelectedIndexChanged;
            _collectionListView.MouseClick += CollectionListView_MouseClick;
            _collectionListView.MouseDoubleClick += CollectionListView_MouseDoubleClick;
            _collectionListView.DrawColumnHeader += ListView_DrawColumnHeader;
            _collectionListView.DrawSubItem += CollectionListView_DrawSubItem;
            _collectionGroup.Controls.Add(_collectionListView);

            // Radio buttons
            _radioVerificationArea.AutoSize = true;
            _radioVerificationArea.Name = "_radioVerificationArea";
            _radioVerificationArea.Size = new Size(83, 16);
            _radioVerificationArea.TabIndex = 3;
            _radioVerificationArea.TabStop = true;
            _radioVerificationArea.Text = "检测区域";
            _radioVerificationArea.UseVisualStyleBackColor = true;
            _radioVerificationArea.CheckedChanged += RadioVerificationArea_CheckedChanged;

            _radioCollectionArea.AutoSize = true;
            _radioCollectionArea.Name = "_radioCollectionArea";
            _radioCollectionArea.Size = new Size(83, 16);
            _radioCollectionArea.TabIndex = 4;
            _radioCollectionArea.TabStop = true;
            _radioCollectionArea.Text = "采集区域";
            _radioCollectionArea.UseVisualStyleBackColor = true;
            _radioCollectionArea.CheckedChanged += RadioCollectionArea_CheckedChanged;

            // StatusStrip
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
            _statusStrip.Location = new Point(0, 571);
            _statusStrip.Name = "_statusStrip";
            _statusStrip.Size = new Size(1024, 22);
            _statusStrip.TabIndex = 5;

            // StatusLabel
            _statusLabel.Name = "_statusLabel";
            _statusLabel.Size = new Size(50, 17);
            _statusLabel.Text = "就绪";

            // ListPanel容器 - 包含GroupBox和RadioButton
            var listContainer = new Panel { Dock = DockStyle.Fill };

            // RadioButton Panel
            var radioPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35
            };
            _lblTabHint = new Label
            {
                Text = "Tab键切换",
                Location = new Point(210, 6),
                AutoSize = true,
                ForeColor = Color.Fuchsia,
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            radioPanel.Controls.Add(_lblTabHint);
            radioPanel.Controls.Add(_radioVerificationArea);
            radioPanel.Controls.Add(_radioCollectionArea);
            _radioVerificationArea.Location = new Point(10, 8);
            _radioCollectionArea.Location = new Point(120, 8);

            listContainer.Controls.Add(_collectionGroup);
            listContainer.Controls.Add(_verificationGroup);
            listContainer.Controls.Add(radioPanel);

            _verificationGroup.Location = new Point(0, 0);
            _collectionGroup.Location = new Point(0, 200);
            _listPanel.Controls.Add(listContainer);
            listContainer.Dock = DockStyle.Fill;

            // FormMain
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1024, 593);
            Controls.Add(_verticalSplit);
            Controls.Add(_toolStrip);
            Controls.Add(_statusStrip);
            Name = "FormMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = Title;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _screenshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region 区域边界类

    /// <summary>
    /// 区域边界抽象基类，用于统一 ImageVerificationArea 和 ImageCollectionArea 的边界操作
    /// </summary>
    internal abstract class AreaBounds
    {
        public abstract int TopLeftX { get; set; }
        public abstract int TopLeftY { get; set; }
        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
    }

    internal sealed class VerificationAreaAdapter : AreaBounds
    {
        private readonly ImageVerificationArea _area;
        public VerificationAreaAdapter(ImageVerificationArea area) => _area = area;
        public override int TopLeftX { get => _area.TopLeftX; set => _area.TopLeftX = value; }
        public override int TopLeftY { get => _area.TopLeftY; set => _area.TopLeftY = value; }
        public override int Width { get => _area.Width; set => _area.Width = value; }
        public override int Height { get => _area.Height; set => _area.Height = value; }
    }

    internal sealed class CollectionAreaAdapter : AreaBounds
    {
        private readonly ImageCollectionArea _area;
        public CollectionAreaAdapter(ImageCollectionArea area) => _area = area;
        public override int TopLeftX { get => _area.TopLeftX; set => _area.TopLeftX = value; }
        public override int TopLeftY { get => _area.TopLeftY; set => _area.TopLeftY = value; }
        public override int Width { get => _area.Width; set => _area.Width = value; }
        public override int Height { get => _area.Height; set => _area.Height = value; }
    }

    #endregion
}
