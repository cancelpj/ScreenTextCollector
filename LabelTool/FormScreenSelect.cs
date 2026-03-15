using System;
using System.Drawing;
using System.Windows.Forms;

namespace LabelTool
{
    /// <summary>
    /// 屏幕选择对话框
    /// </summary>
    public class FormScreenSelect : Form
    {
        private Label _lblTitle;
        private ListBox _screenListBox;
        private Button _btnOk;
        private Button _btnCancel;

        /// <summary>
        /// 用户选择的屏幕编号
        /// </summary>
        public int SelectedScreenNumber { get; private set; }

        public FormScreenSelect()
        {
            InitializeComponent();
            LoadScreens();
        }

        private void InitializeComponent()
        {
            this._lblTitle = new System.Windows.Forms.Label();
            this._screenListBox = new System.Windows.Forms.ListBox();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();

            // FormScreenSelect
            this.Text = "选择屏幕";
            this.Size = new Size(400, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            // _lblTitle
            this._lblTitle.Text = "请选择要采集的屏幕：";
            this._lblTitle.Location = new Point(20, 20);
            this._lblTitle.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            this._lblTitle.AutoSize = true;
            this.Controls.Add(this._lblTitle);

            // _screenListBox
            this._screenListBox.Location = new Point(20, 50);
            this._screenListBox.Size = new Size(340, 220);
            this._screenListBox.Font = new Font("Microsoft Sans Serif", 9F);
            this._screenListBox.DoubleClick += ScreenListBox_DoubleClick;
            this.Controls.Add(this._screenListBox);

            // _btnOk
            this._btnOk.Text = "确定";
            this._btnOk.Location = new Point(200, 285);
            this._btnOk.Size = new Size(75, 30);
            this._btnOk.DialogResult = DialogResult.OK;
            this._btnOk.Click += BtnOk_Click;
            this.Controls.Add(this._btnOk);

            // _btnCancel
            this._btnCancel.Text = "取消";
            this._btnCancel.Location = new Point(285, 285);
            this._btnCancel.Size = new Size(75, 30);
            this._btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(this._btnCancel);
        }

        private void LoadScreens()
        {
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                string displayName;

                // 为主屏幕添加"主屏幕"标识
                if (screen.Primary)
                {
                    displayName = $"屏幕 {i}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height}) - 主屏幕";
                }
                else
                {
                    displayName = $"屏幕 {i}: {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
                }

                _screenListBox.Items.Add(displayName);
            }

            // 默认选择主屏幕（索引0）
            if (_screenListBox.Items.Count > 0)
            {
                _screenListBox.SelectedIndex = 0;
            }
        }

        private void ScreenListBox_DoubleClick(object sender, EventArgs e)
        {
            // 双击选择后确定
            BtnOk_Click(sender, e);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (_screenListBox.SelectedIndex >= 0)
            {
                SelectedScreenNumber = _screenListBox.SelectedIndex;
            }
            else
            {
                SelectedScreenNumber = 0; // 默认使用主屏幕
            }
        }
    }
}
