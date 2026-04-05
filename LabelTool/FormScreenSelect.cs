using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LabelTool
{
    /// <summary>
    /// 屏幕选择对话框（支持多选）
    /// </summary>
    public class FormScreenSelect : Form
    {
        private Label _lblTitle;
        private CheckedListBox _screenCheckedListBox;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Button _btnOk;
        private Button _btnCancel;

        /// <summary>
        /// 用户选择的屏幕编号列表
        /// </summary>
        public List<int> SelectedScreenNumbers { get; private set; } = new List<int>();

        public FormScreenSelect()
        {
            InitializeComponent();
            LoadScreens();
        }

        private void InitializeComponent()
        {
            this._lblTitle = new System.Windows.Forms.Label();
            this._screenCheckedListBox = new System.Windows.Forms.CheckedListBox();
            this._btnSelectAll = new System.Windows.Forms.Button();
            this._btnDeselectAll = new System.Windows.Forms.Button();
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
            this._lblTitle.Size = new System.Drawing.Size(188, 17);
            this._lblTitle.TabIndex = 0;
            this._lblTitle.Text = "请选择要截取的屏幕（可多选）：";
            //
            // _screenCheckedListBox
            //
            this._screenCheckedListBox.CheckOnClick = true;
            this._screenCheckedListBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this._screenCheckedListBox.Location = new System.Drawing.Point(20, 50);
            this._screenCheckedListBox.Name = "_screenCheckedListBox";
            this._screenCheckedListBox.Size = new System.Drawing.Size(340, 154);
            this._screenCheckedListBox.TabIndex = 1;
            //
            // _btnSelectAll
            //
            this._btnSelectAll.Location = new System.Drawing.Point(20, 215);
            this._btnSelectAll.Name = "_btnSelectAll";
            this._btnSelectAll.Size = new System.Drawing.Size(80, 25);
            this._btnSelectAll.TabIndex = 2;
            this._btnSelectAll.Text = "全选";
            this._btnSelectAll.UseVisualStyleBackColor = true;
            this._btnSelectAll.Click += BtnSelectAll_Click;
            //
            // _btnDeselectAll
            //
            this._btnDeselectAll.Location = new System.Drawing.Point(110, 215);
            this._btnDeselectAll.Name = "_btnDeselectAll";
            this._btnDeselectAll.Size = new System.Drawing.Size(80, 25);
            this._btnDeselectAll.TabIndex = 3;
            this._btnDeselectAll.Text = "取消全选";
            this._btnDeselectAll.UseVisualStyleBackColor = true;
            this._btnDeselectAll.Click += BtnDeselectAll_Click;
            //
            // _btnOk
            //
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location = new System.Drawing.Point(200, 251);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 30);
            this._btnOk.TabIndex = 4;
            this._btnOk.Text = "确定";
            this._btnOk.Click += BtnOk_Click;
            //
            // _btnCancel
            //
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(285, 251);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 30);
            this._btnCancel.TabIndex = 5;
            this._btnCancel.Text = "取消";
            this._btnCancel.Click += BtnCancel_Click;
            //
            // FormScreenSelect
            //
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(420, 320);
            this.Controls.Add(this._lblTitle);
            this.Controls.Add(this._screenCheckedListBox);
            this.Controls.Add(this._btnSelectAll);
            this.Controls.Add(this._btnDeselectAll);
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

                _screenCheckedListBox.Items.Add(displayName, true); // 默认全选
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _screenCheckedListBox.Items.Count; i++)
            {
                _screenCheckedListBox.SetItemChecked(i, true);
            }
        }

        private void BtnDeselectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _screenCheckedListBox.Items.Count; i++)
            {
                _screenCheckedListBox.SetItemChecked(i, false);
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            SelectedScreenNumbers.Clear();
            for (int i = 0; i < _screenCheckedListBox.Items.Count; i++)
            {
                if (_screenCheckedListBox.GetItemChecked(i))
                {
                    SelectedScreenNumbers.Add(i);
                }
            }

            // 如果没有选择任何屏幕，默认选择主屏幕（索引0）
            if (SelectedScreenNumbers.Count == 0 && _screenCheckedListBox.Items.Count > 0)
            {
                SelectedScreenNumbers.Add(0);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // 取消
        }

        private void FormScreenSelect_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // ESC键取消
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.Close();
            }
        }
    }
}
