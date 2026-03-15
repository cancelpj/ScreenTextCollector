using System;
using System.Windows.Forms;

namespace Configurator
{
    public class FormAreaDialog : Form
    {
        private Label _lblName;
        private Label _lblThreshold;
        private TextBox _nameTextBox;
        private NumericUpDown _thresholdNumeric;
        private Button _btnOk;
        private Button _btnCancel;

        // 坐标和尺寸输入框
        private Label _lblX;
        private Label _lblY;
        private Label _lblWidth;
        private Label _lblHeight;
        private NumericUpDown _numericX;
        private NumericUpDown _numericY;
        private NumericUpDown _numericWidth;
        private NumericUpDown _numericHeight;

        public string AreaName { get; private set; }
        public float MatchThreshold { get; private set; }
        public int AreaX { get; private set; }
        public int AreaY { get; private set; }
        public int AreaWidth { get; private set; }
        public int AreaHeight { get; private set; }

        /// <summary>
        /// 新建区域
        /// </summary>
        public FormAreaDialog(bool isVerificationMode, float defaultThreshold) :
            this(isVerificationMode, defaultThreshold, "", 0, 0, 100, 100)
        {
        }

        /// <summary>
        /// 编辑已有区域
        /// </summary>
        public FormAreaDialog(bool isVerificationMode, float defaultThreshold, string areaName, int x, int y, int width, int height)
        {
            AreaX = x;
            AreaY = y;
            AreaWidth = width;
            AreaHeight = height;

            InitializeComponent();

            // 设置名称
            _nameTextBox.Text = areaName;

            // 设置坐标和尺寸
            _numericX.Value = x;
            _numericY.Value = y;
            _numericWidth.Value = width;
            _numericHeight.Value = height;

            if (!isVerificationMode)
            {
                // 采集区域不需要匹配阈值
                _lblThreshold.Visible = false;
                _thresholdNumeric.Visible = false;
            }
            else
            {
                _thresholdNumeric.Value = (decimal)defaultThreshold;
            }

            // 更新标题
            this.Text = string.IsNullOrEmpty(areaName) ? (isVerificationMode ? "添加检测区域" : "添加采集区域") : "编辑区域";
        }

        private void InitializeComponent()
        {
            this._nameTextBox = new System.Windows.Forms.TextBox();
            this._thresholdNumeric = new System.Windows.Forms.NumericUpDown();
            this._btnOk = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._lblName = new System.Windows.Forms.Label();
            this._lblThreshold = new System.Windows.Forms.Label();

            // 坐标和尺寸标签和输入框
            this._lblX = new System.Windows.Forms.Label();
            this._lblY = new System.Windows.Forms.Label();
            this._lblWidth = new System.Windows.Forms.Label();
            this._lblHeight = new System.Windows.Forms.Label();
            this._numericX = new System.Windows.Forms.NumericUpDown();
            this._numericY = new System.Windows.Forms.NumericUpDown();
            this._numericWidth = new System.Windows.Forms.NumericUpDown();
            this._numericHeight = new System.Windows.Forms.NumericUpDown();

            ((System.ComponentModel.ISupportInitialize)(this._thresholdNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericHeight)).BeginInit();
            this.SuspendLayout();

            int labelWidth = 65;
            int inputWidth = 60;
            int startX = 20;
            int row1Y = 20;
            int row2Y = 55;
            int row3Y = 90;
            int row4Y = 125;
            int row5Y = 160;

            // 区域名称
            this._lblName.AutoSize = true;
            this._lblName.Location = new System.Drawing.Point(startX, row1Y + 3);
            this._lblName.Name = "_lblName";
            this._lblName.Size = new System.Drawing.Size(53, 12);
            this._lblName.TabIndex = 0;
            this._lblName.Text = "名称:";

            this._nameTextBox.Location = new System.Drawing.Point(startX + labelWidth, row1Y);
            this._nameTextBox.Name = "_nameTextBox";
            this._nameTextBox.Size = new System.Drawing.Size(180, 21);
            this._nameTextBox.TabIndex = 1;

            // 坐标 X
            this._lblX.AutoSize = true;
            this._lblX.Location = new System.Drawing.Point(startX, row2Y + 3);
            this._lblX.Name = "_lblX";
            this._lblX.Size = new System.Drawing.Size(53, 12);
            this._lblX.Text = "X坐标:";

            this._numericX.Location = new System.Drawing.Point(startX + labelWidth, row2Y);
            this._numericX.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._numericX.Name = "_numericX";
            this._numericX.Size = new System.Drawing.Size(inputWidth, 21);
            this._numericX.TabIndex = 2;

            // 坐标 Y
            this._lblY.AutoSize = true;
            this._lblY.Location = new System.Drawing.Point(startX + 130, row2Y + 3);
            this._lblY.Name = "_lblY";
            this._lblY.Size = new System.Drawing.Size(53, 12);
            this._lblY.Text = "Y坐标:";

            this._numericY.Location = new System.Drawing.Point(startX + 130 + labelWidth, row2Y);
            this._numericY.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._numericY.Name = "_numericY";
            this._numericY.Size = new System.Drawing.Size(inputWidth, 21);
            this._numericY.TabIndex = 3;

            // 宽度
            this._lblWidth.AutoSize = true;
            this._lblWidth.Location = new System.Drawing.Point(startX, row3Y + 3);
            this._lblWidth.Name = "_lblWidth";
            this._lblWidth.Size = new System.Drawing.Size(53, 12);
            this._lblWidth.Text = "宽度:";

            this._numericWidth.Location = new System.Drawing.Point(startX + labelWidth, row3Y);
            this._numericWidth.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this._numericWidth.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._numericWidth.Name = "_numericWidth";
            this._numericWidth.Size = new System.Drawing.Size(inputWidth, 21);
            this._numericWidth.TabIndex = 4;

            // 高度
            this._lblHeight.AutoSize = true;
            this._lblHeight.Location = new System.Drawing.Point(startX + 130, row3Y + 3);
            this._lblHeight.Name = "_lblHeight";
            this._lblHeight.Size = new System.Drawing.Size(53, 12);
            this._lblHeight.Text = "高度:";

            this._numericHeight.Location = new System.Drawing.Point(startX + 130 + labelWidth, row3Y);
            this._numericHeight.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            this._numericHeight.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._numericHeight.Name = "_numericHeight";
            this._numericHeight.Size = new System.Drawing.Size(inputWidth, 21);
            this._numericHeight.TabIndex = 5;

            // 匹配阈值
            this._lblThreshold.AutoSize = true;
            this._lblThreshold.Location = new System.Drawing.Point(startX, row4Y + 3);
            this._lblThreshold.Name = "_lblThreshold";
            this._lblThreshold.Size = new System.Drawing.Size(53, 12);
            this._lblThreshold.Text = "匹配阈值:";

            this._thresholdNumeric.DecimalPlaces = 2;
            this._thresholdNumeric.Location = new System.Drawing.Point(startX + labelWidth, row4Y);
            this._thresholdNumeric.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this._thresholdNumeric.Minimum = new decimal(new int[] { 50, 0, 0, 131072 });
            this._thresholdNumeric.Name = "_thresholdNumeric";
            this._thresholdNumeric.Size = new System.Drawing.Size(80, 21);
            this._thresholdNumeric.TabIndex = 6;
            this._thresholdNumeric.Value = new decimal(new int[] { 80, 0, 0, 131072 });

            // 按钮
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location = new System.Drawing.Point(startX + 80, row5Y);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(75, 25);
            this._btnOk.TabIndex = 7;
            this._btnOk.Text = "确定";
            this._btnOk.UseVisualStyleBackColor = true;
            this._btnOk.Click += this.BtnOK_Click;

            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(startX + 165, row5Y);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 25);
            this._btnCancel.TabIndex = 8;
            this._btnCancel.Text = "取消";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Click += this.BtnCancel_Click;

            // FormAreaDialog
            this.AcceptButton = this._btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 205);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._numericHeight);
            this.Controls.Add(this._lblHeight);
            this.Controls.Add(this._numericWidth);
            this.Controls.Add(this._lblWidth);
            this.Controls.Add(this._numericY);
            this.Controls.Add(this._lblY);
            this.Controls.Add(this._numericX);
            this.Controls.Add(this._lblX);
            this.Controls.Add(this._thresholdNumeric);
            this.Controls.Add(this._lblThreshold);
            this.Controls.Add(this._nameTextBox);
            this.Controls.Add(this._lblName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormAreaDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "添加区域";

            ((System.ComponentModel.ISupportInitialize)(this._thresholdNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numericHeight)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("请输入区域名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AreaName = _nameTextBox.Text.Trim();
            MatchThreshold = (float)_thresholdNumeric.Value;
            AreaX = (int)_numericX.Value;
            AreaY = (int)_numericY.Value;
            AreaWidth = (int)_numericWidth.Value;
            AreaHeight = (int)_numericHeight.Value;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
