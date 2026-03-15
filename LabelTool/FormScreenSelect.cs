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
            this.SuspendLayout();
            //
            // _lblTitle
            //
            this._lblTitle.AutoSize = true;
            this._lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._lblTitle.Location = new System.Drawing.Point(20, 20);
            this._lblTitle.Name = "_lblTitle";
            this._lblTitle.Size = new System.Drawing.Size(158, 17);
            this._lblTitle.TabIndex = 0;
            this._lblTitle.Text = "请选择要采集的屏幕：";
            //
            // _screenListBox
            //
            this._screenListBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this._screenListBox.ItemHeight = 15;
            this._screenListBox.Location = new System.Drawing.Point(20, 50);
            this._screenListBox.Name = "_screenListBox";
            this._screenListBox.Size = new System.Drawing.Size(340, 214);
            this._screenListBox.TabIndex = 1;
            this._screenListBox.DoubleClick += ScreenListBox_DoubleClick;
            this._screenListBox.KeyDown += ScreenListBox_KeyDown;
            //
            // _btnOk
            //
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location = new System.Drawing.Point(200, 276);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 30);
            this._btnOk.TabIndex = 2;
            this._btnOk.Text = "确定";
            this._btnOk.Click += BtnOk_Click;
            //
            // _btnCancel
            //
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(285, 276);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 30);
            this._btnCancel.TabIndex = 3;
            this._btnCancel.Text = "取消";
            this._btnCancel.Click += BtnCancel_Click;
            //
            // FormScreenSelect
            //
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 311);
            this.Controls.Add(this._lblTitle);
            this.Controls.Add(this._screenListBox);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormScreenSelect";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "选择屏幕";
            this.KeyDown += FormScreenSelect_KeyDown;
            this.ResumeLayout(false);
            this.PerformLayout();

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

        private void ScreenListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // 回车键确定
            if (e.KeyCode == Keys.Enter)
            {
                BtnOk_Click(sender, e);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
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

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // 取消时关闭整个程序
            Application.Exit();
        }

        private void FormScreenSelect_KeyDown(object sender, KeyEventArgs e)
        {
            // ESC键取消，关闭整个程序
            if (e.KeyCode == Keys.Escape)
            {
                Application.Exit();
            }
        }
    }
}
