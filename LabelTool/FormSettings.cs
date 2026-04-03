using PluginInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LabelTool
{
    /// <summary>
    /// 配置设置窗口 - 用于编辑 appsettings.json
    /// </summary>
    public class FormSettings : Form
    {
        private TabControl _tabControl;
        private TabPage _tabGeneral;
        private TabPage _tabMqtt;

        // 常规配置选项卡控件
        private CheckBox _chkCsvRecord;
        private CheckBox _chkEnableHttp;
        private TextBox _txtHttpIp;
        private NumericUpDown _numHttpPort;

        // MQTT配置选项卡控件
        private CheckBox _chkEnableMqtt;
        private TextBox _txtMqttIp;
        private NumericUpDown _numMqttPort;
        private TextBox _txtClientId;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private NumericUpDown _numCaptureFrequency;

        // 默认Topic
        private TextBox _txtDefaultTopic;
        private ListView _lvDefaultExtendPayload;
        private Button _btnAddDefaultExtend;
        private Button _btnEditDefaultExtend;
        private Button _btnDeleteDefaultExtend;

        // Topic列表
        private ListView _lvTopics;
        private Button _btnAddTopic;
        private Button _btnEditTopic;
        private Button _btnDeleteTopic;

        // 按钮
        private Button _btnCancel;
        private Button _btnSave;

        // 数据
        private AppSettings _settings;
        private List<Dictionary<string, string>> _topicsList;

        public FormSettings()
        {
            _settings = CloneSettings(Tool.Settings);
            _topicsList = new List<Dictionary<string, string>>();

            if (_settings.MqttBroker?.Topics != null)
            {
                foreach (var topic in _settings.MqttBroker.Topics)
                {
                    var dict = new Dictionary<string, string> { { "Name", topic.Name ?? "" } };
                    if (topic.ExtendPayload != null)
                    {
                        foreach (var kvp in topic.ExtendPayload)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }
                    _topicsList.Add(dict);
                }
            }

            InitializeComponent();
            LoadSettings();
        }

        private AppSettings CloneSettings(AppSettings source)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);
        }

        private void InitializeComponent()
        {
            #region 创建所有控件对象（放在 SuspendLayout 之前）
            // 选项卡控件
            _tabControl = new System.Windows.Forms.TabControl();
            _tabGeneral = new System.Windows.Forms.TabPage();
            _tabMqtt = new System.Windows.Forms.TabPage();

            // 常规配置选项卡控件
            _chkCsvRecord = new System.Windows.Forms.CheckBox();
            _chkEnableHttp = new System.Windows.Forms.CheckBox();
            _txtHttpIp = new System.Windows.Forms.TextBox();
            _numHttpPort = new System.Windows.Forms.NumericUpDown();

            // MQTT配置选项卡控件
            _chkEnableMqtt = new System.Windows.Forms.CheckBox();
            _txtMqttIp = new System.Windows.Forms.TextBox();
            _numMqttPort = new System.Windows.Forms.NumericUpDown();
            _txtClientId = new System.Windows.Forms.TextBox();
            _txtUsername = new System.Windows.Forms.TextBox();
            _txtPassword = new System.Windows.Forms.TextBox();
            _numCaptureFrequency = new System.Windows.Forms.NumericUpDown();

            // 默认Topic控件
            _txtDefaultTopic = new System.Windows.Forms.TextBox();
            _lvDefaultExtendPayload = new System.Windows.Forms.ListView();
            _btnAddDefaultExtend = new System.Windows.Forms.Button();
            _btnEditDefaultExtend = new System.Windows.Forms.Button();
            _btnDeleteDefaultExtend = new System.Windows.Forms.Button();

            // Topic列表控件
            _lvTopics = new System.Windows.Forms.ListView();
            _btnAddTopic = new System.Windows.Forms.Button();
            _btnEditTopic = new System.Windows.Forms.Button();
            _btnDeleteTopic = new System.Windows.Forms.Button();

            // 底部按钮
            _btnCancel = new System.Windows.Forms.Button();
            _btnSave = new System.Windows.Forms.Button();

            // 常规配置内部控件
            System.Windows.Forms.GroupBox grpCsv = new System.Windows.Forms.GroupBox();
            System.Windows.Forms.Label lblCsvDesc = new System.Windows.Forms.Label();
            System.Windows.Forms.GroupBox grpHttp = new System.Windows.Forms.GroupBox();
            System.Windows.Forms.Label lblHttpIp = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblHttpPort = new System.Windows.Forms.Label();

            // MQTT内部控件
            System.Windows.Forms.GroupBox grpMqtt = new System.Windows.Forms.GroupBox();
            System.Windows.Forms.Label lblMqttIp = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblMqttPort = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblClientId = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblUsername = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblPassword = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblFreq = new System.Windows.Forms.Label();
            System.Windows.Forms.GroupBox grpDefaultTopic = new System.Windows.Forms.GroupBox();
            System.Windows.Forms.Label lblTopicName = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblExtend = new System.Windows.Forms.Label();
            System.Windows.Forms.GroupBox grpTopics = new System.Windows.Forms.GroupBox();
            #endregion

            #region 窗体基本属性
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Text = "配置设置";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(520, 480);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = System.Drawing.Color.White;
            #endregion

            #region 选项卡控件
            _tabControl.Location = new System.Drawing.Point(10, 10);
            _tabControl.Size = new System.Drawing.Size(500, 380);
            _tabControl.Padding = new System.Drawing.Point(10, 5);

            _tabGeneral.Text = "常规配置";
            _tabGeneral.Padding = new System.Windows.Forms.Padding(10);
            _tabMqtt.Text = "MQTT配置";
            _tabMqtt.Padding = new System.Windows.Forms.Padding(10);

            _tabControl.TabPages.Add(_tabGeneral);
            _tabControl.TabPages.Add(_tabMqtt);
            #endregion

            #region 常规配置选项卡
            // CSV记录
            grpCsv.Text = "CSV记录";
            grpCsv.Location = new System.Drawing.Point(10, 15);
            grpCsv.Size = new System.Drawing.Size(455, 70);

            _chkCsvRecord.Text = "启用CSV记录";
            _chkCsvRecord.Location = new System.Drawing.Point(15, 25);
            _chkCsvRecord.AutoSize = true;

            lblCsvDesc.Text = "将采集数据保存为CSV文件";
            lblCsvDesc.Location = new System.Drawing.Point(35, 48);
            lblCsvDesc.ForeColor = System.Drawing.Color.Gray;
            lblCsvDesc.Font = new System.Drawing.Font("微软雅黑", 8F);

            grpCsv.Controls.Add(_chkCsvRecord);
            grpCsv.Controls.Add(lblCsvDesc);
            _tabGeneral.Controls.Add(grpCsv);

            // HTTP服务
            grpHttp.Text = "HTTP服务";
            grpHttp.Location = new System.Drawing.Point(10, 100);
            grpHttp.Size = new System.Drawing.Size(455, 120);

            _chkEnableHttp.Text = "启用HTTP服务";
            _chkEnableHttp.Location = new System.Drawing.Point(15, 25);
            _chkEnableHttp.AutoSize = true;

            lblHttpIp.Text = "IP地址:";
            lblHttpIp.Location = new System.Drawing.Point(15, 55);
            lblHttpIp.AutoSize = true;

            _txtHttpIp.Location = new System.Drawing.Point(90, 52);
            _txtHttpIp.Size = new System.Drawing.Size(150, 25);

            lblHttpPort.Text = "端口:";
            lblHttpPort.Location = new System.Drawing.Point(260, 55);
            lblHttpPort.AutoSize = true;

            _numHttpPort.Location = new System.Drawing.Point(310, 52);
            _numHttpPort.Size = new System.Drawing.Size(80, 25);
            _numHttpPort.Minimum = 1;
            _numHttpPort.Maximum = 65535;
            _numHttpPort.Value = 8004;

            grpHttp.Controls.Add(_chkEnableHttp);
            grpHttp.Controls.Add(lblHttpIp);
            grpHttp.Controls.Add(_txtHttpIp);
            grpHttp.Controls.Add(lblHttpPort);
            grpHttp.Controls.Add(_numHttpPort);
            _tabGeneral.Controls.Add(grpHttp);
            #endregion

            #region MQTT配置选项卡
            // MQTT推送
            grpMqtt.Text = "MQTT推送";
            grpMqtt.Location = new System.Drawing.Point(10, 15);
            grpMqtt.Size = new System.Drawing.Size(455, 165);

            _chkEnableMqtt.Text = "启用MQTT推送";
            _chkEnableMqtt.Location = new System.Drawing.Point(15, 22);
            _chkEnableMqtt.AutoSize = true;

            lblMqttIp.Text = "服务器:";
            lblMqttIp.Location = new System.Drawing.Point(15, 52);
            lblMqttIp.AutoSize = true;

            _txtMqttIp.Location = new System.Drawing.Point(75, 49);
            _txtMqttIp.Size = new System.Drawing.Size(130, 25);

            lblMqttPort.Text = "端口:";
            lblMqttPort.Location = new System.Drawing.Point(215, 52);
            lblMqttPort.AutoSize = true;

            _numMqttPort.Location = new System.Drawing.Point(255, 49);
            _numMqttPort.Size = new System.Drawing.Size(70, 25);
            _numMqttPort.Minimum = 1;
            _numMqttPort.Maximum = 65535;
            _numMqttPort.Value = 1883;

            lblClientId.Text = "客户端ID:";
            lblClientId.Location = new System.Drawing.Point(15, 82);
            lblClientId.AutoSize = true;

            _txtClientId.Location = new System.Drawing.Point(85, 79);
            _txtClientId.Size = new System.Drawing.Size(240, 25);

            lblUsername.Text = "用户名:";
            lblUsername.Location = new System.Drawing.Point(15, 112);
            lblUsername.AutoSize = true;

            _txtUsername.Location = new System.Drawing.Point(85, 109);
            _txtUsername.Size = new System.Drawing.Size(120, 25);

            lblPassword.Text = "密码:";
            lblPassword.Location = new System.Drawing.Point(215, 112);
            lblPassword.AutoSize = true;

            _txtPassword.Location = new System.Drawing.Point(255, 109);
            _txtPassword.Size = new System.Drawing.Size(120, 25);
            _txtPassword.UseSystemPasswordChar = true;

            lblFreq.Text = "采集频率(秒):";
            lblFreq.Location = new System.Drawing.Point(15, 138);
            lblFreq.AutoSize = true;

            _numCaptureFrequency.Location = new System.Drawing.Point(115, 136);
            _numCaptureFrequency.Size = new System.Drawing.Size(70, 25);
            _numCaptureFrequency.Minimum = 1;
            _numCaptureFrequency.Maximum = 3600;
            _numCaptureFrequency.Value = 2;

            grpMqtt.Controls.Add(_chkEnableMqtt);
            grpMqtt.Controls.Add(lblMqttIp);
            grpMqtt.Controls.Add(_txtMqttIp);
            grpMqtt.Controls.Add(lblMqttPort);
            grpMqtt.Controls.Add(_numMqttPort);
            grpMqtt.Controls.Add(lblClientId);
            grpMqtt.Controls.Add(_txtClientId);
            grpMqtt.Controls.Add(lblUsername);
            grpMqtt.Controls.Add(_txtUsername);
            grpMqtt.Controls.Add(lblPassword);
            grpMqtt.Controls.Add(_txtPassword);
            grpMqtt.Controls.Add(lblFreq);
            grpMqtt.Controls.Add(_numCaptureFrequency);
            _tabMqtt.Controls.Add(grpMqtt);

            // 默认Topic
            grpDefaultTopic.Text = "默认Topic";
            grpDefaultTopic.Location = new System.Drawing.Point(10, 190);
            grpDefaultTopic.Size = new System.Drawing.Size(455, 130);

            lblTopicName.Text = "Topic名称:";
            lblTopicName.Location = new System.Drawing.Point(15, 22);
            lblTopicName.AutoSize = true;

            _txtDefaultTopic.Location = new System.Drawing.Point(85, 19);
            _txtDefaultTopic.Size = new System.Drawing.Size(350, 25);

            lblExtend.Text = "额外字段:";
            lblExtend.Location = new System.Drawing.Point(15, 52);
            lblExtend.AutoSize = true;

            _lvDefaultExtendPayload.Location = new System.Drawing.Point(15, 72);
            _lvDefaultExtendPayload.Size = new System.Drawing.Size(280, 50);
            _lvDefaultExtendPayload.View = System.Windows.Forms.View.List;
            _lvDefaultExtendPayload.MultiSelect = false;

            _btnAddDefaultExtend.Text = "+ 添加";
            _btnAddDefaultExtend.Location = new System.Drawing.Point(305, 72);
            _btnAddDefaultExtend.Size = new System.Drawing.Size(65, 22);
            _btnAddDefaultExtend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnAddDefaultExtend.Click += new System.EventHandler(this.BtnAddDefaultExtend_Click);

            _btnEditDefaultExtend.Text = "修改";
            _btnEditDefaultExtend.Location = new System.Drawing.Point(305, 98);
            _btnEditDefaultExtend.Size = new System.Drawing.Size(65, 22);
            _btnEditDefaultExtend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnEditDefaultExtend.Click += new System.EventHandler(this.BtnEditDefaultExtend_Click);

            _btnDeleteDefaultExtend.Text = "删除";
            _btnDeleteDefaultExtend.Location = new System.Drawing.Point(380, 72);
            _btnDeleteDefaultExtend.Size = new System.Drawing.Size(60, 48);
            _btnDeleteDefaultExtend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnDeleteDefaultExtend.Click += new System.EventHandler(this.BtnDeleteDefaultExtend_Click);

            grpDefaultTopic.Controls.Add(lblTopicName);
            grpDefaultTopic.Controls.Add(_txtDefaultTopic);
            grpDefaultTopic.Controls.Add(lblExtend);
            grpDefaultTopic.Controls.Add(_lvDefaultExtendPayload);
            grpDefaultTopic.Controls.Add(_btnAddDefaultExtend);
            grpDefaultTopic.Controls.Add(_btnEditDefaultExtend);
            grpDefaultTopic.Controls.Add(_btnDeleteDefaultExtend);
            _tabMqtt.Controls.Add(grpDefaultTopic);

            // Topic列表
            grpTopics.Text = "Topic列表";
            grpTopics.Location = new System.Drawing.Point(10, 330);
            grpTopics.Size = new System.Drawing.Size(455, 130);

            _lvTopics.Location = new System.Drawing.Point(15, 20);
            _lvTopics.Size = new System.Drawing.Size(275, 95);
            _lvTopics.View = System.Windows.Forms.View.List;
            _lvTopics.MultiSelect = false;

            _btnAddTopic.Text = "+ 添加";
            _btnAddTopic.Location = new System.Drawing.Point(305, 20);
            _btnAddTopic.Size = new System.Drawing.Size(65, 22);
            _btnAddTopic.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnAddTopic.Click += new System.EventHandler(this.BtnAddTopic_Click);

            _btnEditTopic.Text = "修改";
            _btnEditTopic.Location = new System.Drawing.Point(305, 46);
            _btnEditTopic.Size = new System.Drawing.Size(65, 22);
            _btnEditTopic.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnEditTopic.Click += new System.EventHandler(this.BtnEditTopic_Click);

            _btnDeleteTopic.Text = "删除";
            _btnDeleteTopic.Location = new System.Drawing.Point(305, 72);
            _btnDeleteTopic.Size = new System.Drawing.Size(65, 22);
            _btnDeleteTopic.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnDeleteTopic.Click += new System.EventHandler(this.BtnDeleteTopic_Click);

            grpTopics.Controls.Add(_lvTopics);
            grpTopics.Controls.Add(_btnAddTopic);
            grpTopics.Controls.Add(_btnEditTopic);
            grpTopics.Controls.Add(_btnDeleteTopic);
            _tabMqtt.Controls.Add(grpTopics);
            #endregion

            #region 底部按钮
            _btnCancel.Text = "取消";
            _btnCancel.Location = new System.Drawing.Point(280, 405);
            _btnCancel.Size = new System.Drawing.Size(90, 30);
            _btnCancel.TabIndex = 100;
            _btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);

            _btnSave.Text = "保存(&S)";
            _btnSave.Location = new System.Drawing.Point(385, 405);
            _btnSave.Size = new System.Drawing.Size(90, 30);
            _btnSave.TabIndex = 101;
            _btnSave.ForeColor = System.Drawing.Color.White;
            _btnSave.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
            _btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            _btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            #endregion

            #region 添加控件到窗体
            Controls.Add(_tabControl);
            Controls.Add(_btnCancel);
            Controls.Add(_btnSave);
            #endregion
        }

        #region 数据加载
        private void LoadSettings()
        {
            // 常规配置
            _chkCsvRecord.Checked = _settings.CsvRecord;
            _chkEnableHttp.Checked = _settings.Http?.EnableHttp ?? true;
            _txtHttpIp.Text = _settings.Http?.Ip ?? "127.0.0.1";
            _numHttpPort.Value = _settings.Http?.Port ?? 8004;

            // MQTT配置
            _chkEnableMqtt.Checked = _settings.MqttBroker?.EnableMqttPush ?? false;
            _txtMqttIp.Text = _settings.MqttBroker?.Ip ?? "127.0.0.1";
            _numMqttPort.Value = _settings.MqttBroker?.Port ?? 1883;
            _txtClientId.Text = _settings.MqttBroker?.ClientId ?? "";
            _txtUsername.Text = _settings.MqttBroker?.Username ?? "";
            _txtPassword.Text = _settings.MqttBroker?.Password ?? "";
            _numCaptureFrequency.Value = _settings.MqttBroker?.CaptureFrequency ?? 2;

            // 默认Topic
            _txtDefaultTopic.Text = _settings.MqttBroker?.DefaultTopic?.Name ?? "";
            LoadDefaultExtendPayload();

            // Topic列表
            LoadTopicsList();
        }

        private void LoadDefaultExtendPayload()
        {
            _lvDefaultExtendPayload.Items.Clear();
            if (_settings.MqttBroker?.DefaultTopic?.ExtendPayload != null)
            {
                foreach (var kvp in _settings.MqttBroker.DefaultTopic.ExtendPayload)
                {
                    _lvDefaultExtendPayload.Items.Add($"{kvp.Key}: {kvp.Value}");
                }
            }
        }

        private void LoadTopicsList()
        {
            _lvTopics.Items.Clear();
            foreach (var topic in _topicsList)
            {
                var name = topic.ContainsKey("Name") ? topic["Name"] : "";
                _lvTopics.Items.Add(name);
            }
        }
        #endregion

        #region 事件处理
        private void BtnCancel_Click(object sender, System.EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }

        private void BtnAddDefaultExtend_Click(object sender, System.EventArgs e)
        {
            AddDefaultExtendPayload();
        }

        private void BtnEditDefaultExtend_Click(object sender, System.EventArgs e)
        {
            EditDefaultExtendPayload();
        }

        private void BtnDeleteDefaultExtend_Click(object sender, System.EventArgs e)
        {
            DeleteDefaultExtendPayload();
        }

        private void BtnAddTopic_Click(object sender, System.EventArgs e)
        {
            AddTopic();
        }

        private void BtnEditTopic_Click(object sender, System.EventArgs e)
        {
            EditTopic();
        }

        private void BtnDeleteTopic_Click(object sender, System.EventArgs e)
        {
            DeleteTopic();
        }
        #endregion

        #region ExtendPayload 操作
        private void AddDefaultExtendPayload()
        {
            using (var dialog = FormKeyValueDialog.Create("", ""))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (_settings.MqttBroker.DefaultTopic.ExtendPayload == null)
                    {
                        _settings.MqttBroker.DefaultTopic.ExtendPayload = new Dictionary<string, string>();
                    }

                    if (_settings.MqttBroker.DefaultTopic.ExtendPayload.ContainsKey(dialog.Key))
                    {
                        MessageBox.Show($"键 \"{dialog.Key}\" 已存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _settings.MqttBroker.DefaultTopic.ExtendPayload[dialog.Key] = dialog.Value;
                    LoadDefaultExtendPayload();
                }
            }
        }

        private void EditDefaultExtendPayload()
        {
            if (_lvDefaultExtendPayload.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要修改的字段", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedText = _lvDefaultExtendPayload.SelectedItems[0].Text;
            var parts = selectedText.Split(new[] { ':' }, 2);
            if (parts.Length < 2) return;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            using (var dialog = FormKeyValueDialog.Create(key, value))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _settings.MqttBroker.DefaultTopic.ExtendPayload[key] = dialog.Value;
                    LoadDefaultExtendPayload();
                }
            }
        }

        private void DeleteDefaultExtendPayload()
        {
            if (_lvDefaultExtendPayload.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要删除的字段", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedText = _lvDefaultExtendPayload.SelectedItems[0].Text;
            var parts = selectedText.Split(new[] { ':' }, 2);
            if (parts.Length < 2) return;

            var key = parts[0].Trim();

            if (MessageBox.Show($"确定要删除字段 \"{key}\" 吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _settings.MqttBroker.DefaultTopic.ExtendPayload.Remove(key);
                LoadDefaultExtendPayload();
            }
        }
        #endregion

        #region Topic列表 操作
        private void AddTopic()
        {
            using (var dialog = FormKeyValueDialog.Create("", ""))
            {
                dialog.Text = "添加Topic";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var dict = new Dictionary<string, string> { { "Name", dialog.Key } };
                    dict[dialog.Value] = "";
                    _topicsList.Add(dict);
                    LoadTopicsList();
                }
            }
        }

        private void EditTopic()
        {
            if (_lvTopics.SelectedIndices.Count == 0)
            {
                MessageBox.Show("请先选择要修改的Topic", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var index = _lvTopics.SelectedIndices[0];
            var topic = _topicsList[index];
            var name = topic.ContainsKey("Name") ? topic["Name"] : "";

            using (var dialog = FormKeyValueDialog.Create("Topic名称", name))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    topic["Name"] = dialog.Value;
                    LoadTopicsList();
                }
            }
        }

        private void DeleteTopic()
        {
            if (_lvTopics.SelectedIndices.Count == 0)
            {
                MessageBox.Show("请先选择要删除的Topic", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var index = _lvTopics.SelectedIndices[0];
            var name = _topicsList[index].ContainsKey("Name") ? _topicsList[index]["Name"] : "";

            if (MessageBox.Show($"确定要删除Topic \"{name}\" 吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _topicsList.RemoveAt(index);
                LoadTopicsList();
            }
        }
        #endregion

        #region 保存操作
        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // 验证必填项
                if (_chkEnableMqtt.Checked)
                {
                    if (string.IsNullOrWhiteSpace(_txtMqttIp.Text))
                    {
                        MessageBox.Show("请输入MQTT服务器地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _tabControl.SelectedTab = _tabMqtt;
                        _txtMqttIp.Focus();
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_txtClientId.Text))
                    {
                        MessageBox.Show("请输入MQTT客户端ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _tabControl.SelectedTab = _tabMqtt;
                        _txtClientId.Focus();
                        return;
                    }
                }

                // 保存常规配置
                _settings.CsvRecord = _chkCsvRecord.Checked;
                _settings.Http = new HttpConfig
                {
                    EnableHttp = _chkEnableHttp.Checked,
                    Ip = _txtHttpIp.Text.Trim(),
                    Port = (int)_numHttpPort.Value
                };

                // 保存MQTT配置
                _settings.MqttBroker = new MqttBrokerConfig
                {
                    EnableMqttPush = _chkEnableMqtt.Checked,
                    Ip = _txtMqttIp.Text.Trim(),
                    Port = (int)_numMqttPort.Value,
                    ClientId = _txtClientId.Text.Trim(),
                    Username = _txtUsername.Text.Trim(),
                    Password = _txtPassword.Text,
                    CaptureFrequency = (int)_numCaptureFrequency.Value,
                    DefaultTopic = new MqttTopicConfig
                    {
                        Name = _txtDefaultTopic.Text.Trim(),
                        ExtendPayload = _settings.MqttBroker?.DefaultTopic?.ExtendPayload ?? new Dictionary<string, string>()
                    },
                    Topics = _topicsList.Select(t => new MqttTopicConfig
                    {
                        Name = t.ContainsKey("Name") ? t["Name"] : "",
                        ExtendPayload = t.Where(kv => kv.Key != "Name").ToDictionary(kv => kv.Key, kv => kv.Value)
                    }).ToList()
                };

                // 保存到文件
                Tool.SaveSettings(_settings);

                DialogResult = DialogResult.OK;
                MessageBox.Show("配置已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
    }
}
