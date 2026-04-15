using PluginInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain : Form
    {
        private const string Title = "截屏采集标注工具 V1.5.3";

        // 截屏图片（按屏幕分组存储）
        private Dictionary<int, Bitmap> _screenScreenshots = new Dictionary<int, Bitmap>();

        // 当前选中的屏幕编号
        private int _currentScreenNumber = 0;

        // 已勾选并截屏的屏幕编号列表（来自 FormScreenSelect）
        private List<int> _capturedScreenNumbers = new List<int>();
        private Point _dragStart;
        private Point _dragEnd;
        private bool _isDragging;
        private Rectangle _currentRect;
        private bool _isDraggingArea; // 是否在拖拽已选中的区域
        private bool _isResizingArea; // 是否在缩放已选中的区域
        private int _resizeHandle = -1; // 缩放手柄编号（0-7）

        // 区域列表（按屏幕分组存储）
        private Dictionary<int, List<ImageVerificationArea>> _screenVerificationAreas = new Dictionary<int, List<ImageVerificationArea>>();
        private Dictionary<int, List<ImageCollectionArea>> _screenCollectionAreas = new Dictionary<int, List<ImageCollectionArea>>();

        // 当前选中的区域
        private int _selectedVerificationIndex = -1;
        private int _selectedCollectionIndex = -1;

        // 当前操作模式
        private bool _isVerificationAreaMode = true; // true=检测区域, false=采集区域

        // 匹配阈值
        private float _matchThreshold = 1;

        // 可用的 MQTT Topic 列表
        private List<string> _availableTopics = new List<string>();

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
        private const int ZOOM_TRACKBAR_MIN = 50;  // TrackBar最小值
        private const int ZOOM_TRACKBAR_MAX = 150; // TrackBar最大值
        private const float ZOOM_STEP = 0.1f;      // 每次缩放步进

        // 控件
        private ToolStrip _toolStrip;
        private Panel _imagePanel;
        private SplitContainer _verticalSplit;
        private Panel _listPanel;
        private GroupBox _verificationGroup;
        private GroupBox _collectionGroup;
        private ListView _verificationListView;
        private DataGridView _collectionDataGridView;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripLabel _toolStripLabel1;
        private ToolStripComboBox _thresholdComboBox;
        private RadioButton _radioVerificationArea;
        private RadioButton _radioCollectionArea;
        private Label _lblTabHint;
        private ToolTip _toolTip;
        private ToolStripButton _btnCapture;
        private ToolStripComboBox _screenComboBox;
        private ToolStripButton _btnSave;
        private ToolStripButton _btnOpenConfig;
        private ToolStripButton _btnAbout;
        private ToolStripButton _btnSettings;
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

        // 工具栏分隔符
        private ToolStripSeparator _toolStripSepA;
        private ToolStripSeparator _toolStripSepB;
        private ToolStripSeparator _toolStripSepC;
        private ToolStripSeparator _toolStripSepD;
        private ToolStripSeparator _toolStripSepE;
        private ToolStripSeparator _toolStripSepF;

        // ListView 列
        private ColumnHeader _colName;
        private ColumnHeader _colLocation;
        private ColumnHeader _colAction;

        // DataGridView 列
        private DataGridViewTextBoxColumn _nameColumn;
        private DataGridViewTextBoxColumn _locationColumn;
        private DataGridViewTextBoxColumn _resultColumn;
        private DataGridViewButtonColumn _expandColumn;
        private DataGridViewButtonColumn _deleteColumn;

        // 手柄偏移因子（相对于 rect.X/Y 的比例）：左上、上、右上、右、右下、下、左下、左
        // 每个元素的 (X,Y) 表示 rect.X + rect.Width * X, rect.Y + rect.Height * Y
        private static readonly float[] _handleFX = { 0f, 0.5f, 1f, 1f, 1f, 0.5f, 0f, 0f };
        private static readonly float[] _handleFY = { 0f, 0f, 0f, 0.5f, 1f, 1f, 1f, 0.5f };

        // 配置文件监测器（监测记事本编辑保存）
        private FileSystemWatcher _configFileWatcher;

        public FormMain()
        {
            InitializeComponent();

            // 启用双缓冲，减少闪烁
            _imagePanel.GetType().GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_imagePanel, true, null);

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
            KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormMain_KeyDown);
            // 启动时最大化窗口
            WindowState = FormWindowState.Maximized;
            Load += new System.EventHandler(this.FormMain_Load);
            FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
        }

        private void InitializeComponent()
        {
            // 先创建所有控件对象（在 SuspendLayout 之前）
            _toolStrip = new ToolStrip();
            _btnCapture = new ToolStripButton();
            _screenComboBox = new ToolStripComboBox();
            _btnSave = new ToolStripButton();
            _btnOpenConfig = new ToolStripButton();
            _btnSettings = new ToolStripButton();
            _btnAbout = new ToolStripButton();
            _ocrEngineLabel = new ToolStripLabel();
            _ocrEngineComboBox = new ToolStripComboBox();
            _btnOcrTest = new ToolStripButton();
            _cmbZoomMode = new ToolStripComboBox();
            _cmbZoomLevel = new ToolStripComboBox();
            _btnZoomOut = new ToolStripButton();
            _btnZoomIn = new ToolStripButton();
            _zoomTrackBar = new TrackBar();
            _zoomTrackBarHost = new ToolStripControlHost(_zoomTrackBar);
            _scrollContainer = new Panel();
            _imagePanel = new Panel();
            _listPanel = new Panel();
            _verificationGroup = new GroupBox();
            _collectionGroup = new GroupBox();
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel();
            _toolStripLabel1 = new ToolStripLabel();
            _thresholdComboBox = new ToolStripComboBox();
            _radioVerificationArea = new RadioButton();
            _radioCollectionArea = new RadioButton();
            _verificationListView = new ListView();
            _collectionDataGridView = new DataGridView();
            _lblTabHint = new Label();
            _toolTip = new ToolTip();
            _verticalSplit = new SplitContainer();

            // 工具栏分隔符
            _toolStripSepA = new ToolStripSeparator();
            _toolStripSepB = new ToolStripSeparator();
            _toolStripSepC = new ToolStripSeparator();
            _toolStripSepD = new ToolStripSeparator();
            _toolStripSepE = new ToolStripSeparator();
            _toolStripSepF = new ToolStripSeparator();

            // ListView 列
            _colName = new ColumnHeader();
            _colName.Text = "名称";
            _colName.Width = 100;

            _colLocation = new ColumnHeader();
            _colLocation.Text = "位置";
            _colLocation.Width = 150;

            _colAction = new ColumnHeader();
            _colAction.Text = "";
            _colAction.Width = 30;

            // DataGridView 列
            _nameColumn = new DataGridViewTextBoxColumn();
            _nameColumn.HeaderText = "名称";
            _nameColumn.Width = 100;
            _nameColumn.ReadOnly = true;

            _locationColumn = new DataGridViewTextBoxColumn();
            _locationColumn.HeaderText = "位置";
            _locationColumn.Width = 120;
            _locationColumn.ReadOnly = true;

            _resultColumn = new DataGridViewTextBoxColumn();
            _resultColumn.HeaderText = "识别结果";
            _resultColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _expandColumn = new DataGridViewButtonColumn();
            _expandColumn.HeaderText = "";
            _expandColumn.Width = 30;
            _expandColumn.UseColumnTextForButtonValue = true;
            _expandColumn.Text = "...";
            _expandColumn.FlatStyle = FlatStyle.Popup;
            _expandColumn.Resizable = DataGridViewTriState.False;

            _deleteColumn = new DataGridViewButtonColumn();
            _deleteColumn.HeaderText = "";
            _deleteColumn.Width = 30;
            _deleteColumn.UseColumnTextForButtonValue = true;
            _deleteColumn.Text = "×";
            _deleteColumn.FlatStyle = FlatStyle.Popup;
            _deleteColumn.Resizable = DataGridViewTriState.False;

            SuspendLayout();

            // 加载图标资源
            var resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            var screenshotIcon = LoadIconFromResources(resourcesDir, "Screenshot.png");
            var saveIcon = LoadIconFromResources(resourcesDir, "Save.png");
            var openIcon = LoadIconFromResources(resourcesDir, "Open.png");
            var ocrIcon = LoadIconFromResources(resourcesDir, "ocr.png");
            var aboutIcon = SystemIcons.Information.ToBitmap();

            #region 工具栏 ToolStrip

            // ToolStrip
            _btnCapture.Name = "_btnCapture";
            _btnCapture.Text = "新建采集 Ctrl+R";
            _btnCapture.Image = screenshotIcon;
            _btnCapture.Click += new System.EventHandler(this.BtnCapture_Click);

            // 屏幕选择下拉框
            _screenComboBox.Name = "_screenComboBox";
            _screenComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _screenComboBox.Width = 150;
            _screenComboBox.SelectedIndexChanged += new System.EventHandler(this.ScreenComboBox_SelectedIndexChanged);
            _screenComboBox.ComboBox.DropDown += new EventHandler(ScreenComboBox_DropDown);

            _btnSave.Name = "_btnSave";
            _btnSave.Text = "保存 Ctrl+S";
            _btnSave.Image = saveIcon;
            _btnSave.Click += new System.EventHandler(this.BtnSave_Click);

            _btnOpenConfig.Name = "_btnOpenConfig";
            _btnOpenConfig.Text = "用记事本打开 Ctrl+O";
            _btnOpenConfig.Image = openIcon;
            _btnOpenConfig.Click += new System.EventHandler(this.BtnOpenConfig_Click);

            // 使用 Spring 属性将配置、关于按钮推到右边
            _btnSettings.Name = "_btnSettings";
            _btnSettings.Text = "通讯配置";
            _btnSettings.Alignment = ToolStripItemAlignment.Right;
            _btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);

            _btnAbout.Name = "_btnAbout";
            _btnAbout.Text = "关于";
            _btnAbout.Image = aboutIcon;
            _btnAbout.Alignment = ToolStripItemAlignment.Right;
            _btnAbout.Click += new System.EventHandler(this.BtnAbout_Click);

            _toolStrip.Items.Add(_btnCapture);
            _toolStrip.Items.Add(_screenComboBox);
            _toolStrip.Items.Add(_toolStripSepA);
            _toolStrip.Items.Add(_btnSave);
            _toolStrip.Items.Add(_btnOpenConfig);
            _toolStrip.Items.Add(_toolStripSepB);
            _toolStrip.Items.Add(_btnAbout);
            _toolStrip.Items.Add(_btnSettings);
            _toolStrip.Location = new Point(0, 0);
            _toolStrip.Name = "_toolStrip";
            _toolStrip.Size = new Size(1024, 25);
            _toolStrip.TabIndex = 1;

            // 匹配阈值下拉框
            _toolStripLabel1.Name = "_toolStripLabel1";
            _toolStripLabel1.Text = "默认匹配度:";
            _thresholdComboBox.AutoSize = false;
            _thresholdComboBox.Width = 40;
            _thresholdComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _thresholdComboBox.Items.AddRange(new object[] { "1", "0.95", "0.9", "0.85", "0.8" });
            _thresholdComboBox.Name = "_thresholdComboBox";
            _thresholdComboBox.SelectedIndex = 0;
            _thresholdComboBox.SelectedIndexChanged += new System.EventHandler(this.ThresholdComboBox_SelectedIndexChanged);

            // OCR引擎下拉框
            _ocrEngineLabel.Name = "_ocrEngineLabel";
            _ocrEngineLabel.Text = "OCR引擎:";
            _ocrEngineComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _ocrEngineComboBox.Items.AddRange(new object[] { "PaddleOCR", "OpenCvSharp" });
            _ocrEngineComboBox.Name = "_ocrEngineComboBox";
            _ocrEngineComboBox.SelectedIndex = 0; // 默认PaddleOCR
            _ocrEngineComboBox.SelectedIndexChanged += new System.EventHandler(this.OcrEngineComboBox_SelectedIndexChanged);

            // OCR测试按钮
            _btnOcrTest.Name = "_btnOcrTest";
            _btnOcrTest.Text = "OCR测试 Ctrl+T";
            _btnOcrTest.Image = ocrIcon;
            _btnOcrTest.Click += new System.EventHandler(this.BtnOcrTest_Click);

            _toolStrip.Items.Add(_toolStripLabel1);
            _toolStrip.Items.Add(_thresholdComboBox);
            _toolStrip.Items.Add(_toolStripSepC);
            _toolStrip.Items.Add(_ocrEngineLabel);
            _toolStrip.Items.Add(_ocrEngineComboBox);
            _toolStrip.Items.Add(_btnOcrTest);

            // 缩放控制
            _cmbZoomMode.AutoSize = false;
            _cmbZoomMode.Width = 80;
            _cmbZoomMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbZoomMode.Items.AddRange(new object[] { "自动缩放", "手动缩放" });
            _cmbZoomMode.Name = "_cmbZoomMode";
            _cmbZoomMode.SelectedIndex = 0; // 默认自动缩放
            _cmbZoomMode.SelectedIndexChanged += new System.EventHandler(this.CmbZoomMode_SelectedIndexChanged);

            _cmbZoomLevel.AutoSize = false;
            _cmbZoomLevel.Width = 60;
            _cmbZoomLevel.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbZoomLevel.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%" });
            _cmbZoomLevel.Name = "_cmbZoomLevel";
            _cmbZoomLevel.SelectedIndex = 2; // 默认100%
            _cmbZoomLevel.TextChanged += new System.EventHandler(this.CmbZoomLevel_TextChanged);
            _cmbZoomLevel.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CmbZoomLevel_KeyDown);

            _btnZoomOut.Name = "_btnZoomOut";
            _btnZoomOut.Text = "-";
            _btnZoomOut.ToolTipText = "缩小";
            _btnZoomOut.Click += new System.EventHandler(this.BtnZoomOut_Click);

            _zoomTrackBar.TickStyle = TickStyle.None;
            _zoomTrackBar.AutoSize = false;
            _zoomTrackBar.Height = 20;
            _zoomTrackBar.Width = 100;
            _zoomTrackBar.Minimum = ZOOM_TRACKBAR_MIN;
            _zoomTrackBar.Maximum = ZOOM_TRACKBAR_MAX;
            _zoomTrackBar.Value = 100;
            _zoomTrackBar.Enabled = false;
            _zoomTrackBar.Name = "_zoomTrackBar";
            // TrackBar 垂直居中（ToolStrip 高度约25，TrackBar 高度20，偏移2像素）
            _zoomTrackBar.Top = 2;
            _zoomTrackBar.Scroll += new System.EventHandler(this.ZoomTrackBar_Scroll);

            _zoomTrackBarHost.Padding = new Padding(0);
            _zoomTrackBarHost.Margin = new Padding(2, 0, 2, 0);

            _btnZoomIn.Name = "_btnZoomIn";
            _btnZoomIn.Text = "+";
            _btnZoomIn.ToolTipText = "放大";
            _btnZoomIn.Click += new System.EventHandler(this.BtnZoomIn_Click);

            _toolStrip.Items.Add(_toolStripSepD);
            _toolStrip.Items.Add(_cmbZoomMode);
            _toolStrip.Items.Add(_toolStripSepE);
            _toolStrip.Items.Add(_cmbZoomLevel);
            _toolStrip.Items.Add(_btnZoomOut);
            _toolStrip.Items.Add(_zoomTrackBarHost);
            _toolStrip.Items.Add(_btnZoomIn);

            #endregion

            // 垂直SplitContainer（左侧图片，右侧列表）
            _verticalSplit.Dock = DockStyle.Fill;
            _verticalSplit.Orientation = Orientation.Vertical;
            _verticalSplit.SplitterDistance = 700;
            _verticalSplit.BorderStyle = BorderStyle.FixedSingle;
            _verticalSplit.Name = "_verticalSplit";

            // ImagePanel 和滚动容器
            _scrollContainer.Dock = DockStyle.Fill;
            _scrollContainer.AutoScroll = true;
            _scrollContainer.AutoScrollMinSize = new Size(1, 1);
            _scrollContainer.BackColor = Color.Black;
            _scrollContainer.Name = "_scrollContainer";

            _imagePanel.BackColor = Color.Black;
            _imagePanel.Name = "_imagePanel";
            _imagePanel.Size = new Size(698, 398);
            _imagePanel.TabIndex = 0;
            _imagePanel.Paint += new System.Windows.Forms.PaintEventHandler(this.ImagePanel_Paint);
            _imagePanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseDown);
            _imagePanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseMove);
            _imagePanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseUp);
            _imagePanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseClick);
            _imagePanel.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseDoubleClick);
            _imagePanel.MouseEnter += new System.EventHandler(this.ImagePanel_MouseEnter);
            _imagePanel.MouseLeave += new System.EventHandler(this.ImagePanel_MouseLeave);
            _imagePanel.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.ImagePanel_MouseWheel);
            _imagePanel.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ImagePanel_KeyDown);
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

            // Verification ListView
            _verificationListView.Dock = DockStyle.Fill;
            _verificationListView.FullRowSelect = true;
            _verificationListView.GridLines = true;
            _verificationListView.View = View.Details;
            _verificationListView.MultiSelect = false;
            _verificationListView.OwnerDraw = true;
            _verificationListView.Name = "_verificationListView";
            _verificationListView.Columns.Add(_colName);
            _verificationListView.Columns.Add(_colLocation);
            _verificationListView.Columns.Add(_colAction);
            _verificationListView.SelectedIndexChanged += new System.EventHandler(this.VerificationListView_SelectedIndexChanged);
            _verificationListView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.VerificationListView_MouseClick);
            _verificationListView.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.VerificationListView_MouseDoubleClick);
            _verificationListView.DrawColumnHeader += new System.Windows.Forms.DrawListViewColumnHeaderEventHandler(this.ListView_DrawColumnHeader);
            _verificationListView.DrawSubItem += new System.Windows.Forms.DrawListViewSubItemEventHandler(this.VerificationListView_DrawSubItem);
            _verificationGroup.Controls.Add(_verificationListView);

            // CollectionGroup
            _collectionGroup.Dock = DockStyle.Fill;
            _collectionGroup.Name = "_collectionGroup";
            _collectionGroup.Size = new Size(318, 300);
            _collectionGroup.TabIndex = 1;
            _collectionGroup.TabStop = false;
            _collectionGroup.Text = "采集区域";

            // Collection DataGridView
            _collectionDataGridView.Dock = DockStyle.Fill;
            _collectionDataGridView.AllowUserToAddRows = false;
            _collectionDataGridView.AllowUserToDeleteRows = false;
            _collectionDataGridView.ReadOnly = true;
            _collectionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _collectionDataGridView.MultiSelect = false;
            _collectionDataGridView.RowHeadersVisible = false;
            _collectionDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _collectionDataGridView.BackgroundColor = Color.White;
            _collectionDataGridView.BorderStyle = BorderStyle.None;
            _collectionDataGridView.ColumnHeadersVisible = true;
            _collectionDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _collectionDataGridView.ScrollBars = ScrollBars.Vertical;
            _collectionDataGridView.Name = "_collectionDataGridView";

            // 添加列：名称、位置、识别结果、展开按钮、删除

            _collectionDataGridView.Columns.Add(_nameColumn);
            _collectionDataGridView.Columns.Add(_locationColumn);
            _collectionDataGridView.Columns.Add(_resultColumn);
            _collectionDataGridView.Columns.Add(_expandColumn);
            _collectionDataGridView.Columns.Add(_deleteColumn);
            _collectionDataGridView.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.CollectionDataGridView_CellPainting);
            _collectionDataGridView.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.CollectionDataGridView_CellContentClick);
            _collectionDataGridView.CellMouseEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.CollectionDataGridView_CellMouseEnter);
            _collectionDataGridView.SelectionChanged += new System.EventHandler(this.CollectionDataGridView_SelectionChanged);
            _collectionDataGridView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.CollectionDataGridView_MouseClick);
            _collectionDataGridView.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.CollectionDataGridView_MouseDoubleClick);
            _collectionGroup.Controls.Add(_collectionDataGridView);

            // Radio buttons
            _radioVerificationArea.AutoSize = true;
            _radioVerificationArea.Name = "_radioVerificationArea";
            _radioVerificationArea.Size = new Size(83, 16);
            _radioVerificationArea.TabIndex = 3;
            _radioVerificationArea.TabStop = true;
            _radioVerificationArea.Text = "检测区域";
            _radioVerificationArea.UseVisualStyleBackColor = true;
            _radioVerificationArea.CheckedChanged += new System.EventHandler(this.RadioVerificationArea_CheckedChanged);

            _radioCollectionArea.AutoSize = true;
            _radioCollectionArea.Name = "_radioCollectionArea";
            _radioCollectionArea.Size = new Size(83, 16);
            _radioCollectionArea.TabIndex = 4;
            _radioCollectionArea.TabStop = true;
            _radioCollectionArea.Text = "采集区域";
            _radioCollectionArea.UseVisualStyleBackColor = true;
            _radioCollectionArea.CheckedChanged += new System.EventHandler(this.RadioCollectionArea_CheckedChanged);

            // StatusStrip
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Location = new Point(0, 571);
            _statusStrip.Name = "_statusStrip";
            _statusStrip.Size = new Size(1024, 22);
            _statusStrip.TabIndex = 5;

            // StatusLabel
            _statusLabel.Name = "_statusLabel";
            _statusLabel.Size = new Size(50, 17);
            _statusLabel.Text = "就绪";

            // ListPanel容器 - 包含GroupBox和RadioButton
            var listContainer = new Panel();
            listContainer.Dock = DockStyle.Fill;
            listContainer.Name = "listContainer";

            // RadioButton Panel
            var radioPanel = new Panel();
            radioPanel.Dock = DockStyle.Bottom;
            radioPanel.Height = 35;
            radioPanel.Name = "radioPanel";

            _lblTabHint.Text = "Tab键切换";
            _lblTabHint.Location = new Point(210, 6);
            _lblTabHint.AutoSize = true;
            _lblTabHint.ForeColor = Color.Fuchsia;
            _lblTabHint.Font = new Font("Microsoft YaHei UI", 10F);

            _radioVerificationArea.Location = new Point(10, 8);
            _radioCollectionArea.Location = new Point(120, 8);

            radioPanel.Controls.Add(_lblTabHint);
            radioPanel.Controls.Add(_radioVerificationArea);
            radioPanel.Controls.Add(_radioCollectionArea);

            listContainer.Controls.Add(_collectionGroup);
            listContainer.Controls.Add(_verificationGroup);
            listContainer.Controls.Add(radioPanel);

            _verificationGroup.Location = new Point(0, 0);
            _collectionGroup.Location = new Point(0, 200);
            _listPanel.Controls.Add(listContainer);

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

            ResumeLayout(false);
            PerformLayout();
        }

        private void CollectionDataGridView_SelectionChanged(object sender, System.EventArgs e)
        {
            _collectionDataGridView.ClearSelection();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearAllScreenshots();
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
