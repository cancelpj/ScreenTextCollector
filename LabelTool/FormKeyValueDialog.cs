using System;
using System.Drawing;
using System.Windows.Forms;

namespace LabelTool
{
    /// <summary>
    /// Key-Value 键值对编辑对话框
    /// </summary>
    public class FormKeyValueDialog : Form
    {
        private TextBox txtKey;
        private TextBox txtValue;
        private Button btnOK;
        private Button btnCancel;
        private Label lblKey;
        private Label lblValue;

        /// <summary>编辑的键</summary>
        public string Key { get; private set; }

        /// <summary>编辑的值</summary>
        public string Value { get; private set; }

        /// <summary>是否为新增模式</summary>
        public bool IsNewEntry { get; private set; }

        public FormKeyValueDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 创建对话框实例
        /// </summary>
        /// <param name="key">键（空字符串表示新增模式）</param>
        /// <param name="value">值</param>
        public static FormKeyValueDialog Create(string key = "", string value = "")
        {
            var dialog = new FormKeyValueDialog();
            dialog.Key = key ?? "";
            dialog.Value = value ?? "";
            dialog.IsNewEntry = string.IsNullOrEmpty(key);
            dialog.txtKey.Text = dialog.Key;
            dialog.txtValue.Text = dialog.Value;
            dialog.txtKey.ReadOnly = !dialog.IsNewEntry;
            dialog.Text = dialog.IsNewEntry ? "添加新字段" : "编辑字段";
            return dialog;
        }

        private void InitializeComponent()
        {
            lblKey = new Label();
            txtKey = new TextBox();
            lblValue = new Label();
            txtValue = new TextBox();
            btnOK = new Button();
            btnCancel = new Button();
            SuspendLayout();
            //
            // lblKey
            //
            lblKey.AutoSize = true;
            lblKey.Location = new Point(20, 20);
            lblKey.Name = "lblKey";
            lblKey.Size = new Size(53, 12);
            lblKey.TabIndex = 0;
            lblKey.Text = "键 (Key):";
            //
            // txtKey
            //
            txtKey.Location = new Point(20, 45);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(320, 21);
            txtKey.TabIndex = 1;
            //
            // lblValue
            //
            lblValue.AutoSize = true;
            lblValue.Location = new Point(20, 80);
            lblValue.Name = "lblValue";
            lblValue.Size = new Size(65, 12);
            lblValue.TabIndex = 2;
            lblValue.Text = "值 (Value):";
            //
            // txtValue
            //
            txtValue.Location = new Point(20, 105);
            txtValue.Name = "txtValue";
            txtValue.Size = new Size(320, 21);
            txtValue.TabIndex = 3;
            //
            // btnOK
            //
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new Point(155, 150);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(85, 30);
            btnOK.TabIndex = 4;
            btnOK.Text = "确定";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            //
            // btnCancel
            //
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(255, 150);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(85, 30);
            btnCancel.TabIndex = 5;
            btnCancel.Text = "取消";
            btnCancel.UseVisualStyleBackColor = true;
            //
            // FormKeyValueDialog
            //
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            CancelButton = btnCancel;
            ClientSize = new Size(360, 195);
            Controls.AddRange(new Control[] { btnCancel, btnOK, txtValue, lblValue, txtKey, lblKey });
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormKeyValueDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "编辑额外字段";
            ResumeLayout(false);
            PerformLayout();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (IsNewEntry && string.IsNullOrWhiteSpace(txtKey.Text))
            {
                MessageBox.Show("键不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtKey.Focus();
                DialogResult = DialogResult.None;
                return;
            }

            Key = txtKey.Text.Trim();
            Value = txtValue.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
