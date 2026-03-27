using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
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
            // Ctrl+T: OCR测试
            else if (e.Control && e.KeyCode == Keys.T)
            {
                BtnOcrTest_Click(this, EventArgs.Empty);
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
            _toolTip.SetToolTip(_toolStrip, "工具栏：重新截屏 | 保存配置 | 用记事本打开配置 | OCR测试");

            // 加载可用的 MQTT Topic 列表
            LoadAvailableTopics();

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
            _radioVerificationArea.Checked = true;
            _isVerificationAreaMode = true;

            // 更新状态栏
            _statusLabel.Text = "就绪";
        }

        #region DataGridView 事件处理

        // 点击"..."按钮打开详情弹窗
        private void CollectionDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 3 && e.RowIndex >= 0) // 展开按钮列（第4列）
            {
                string fullText = _collectionDataGridView.Rows[e.RowIndex].Cells[2].Value?.ToString() ?? ""; // 识别结果列（第3列）
                ShowOcrResultDialog(fullText);
            }
        }

        // 鼠标进入单元格时设置 Tooltip
        private void CollectionDataGridView_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 2 && e.RowIndex >= 0) // 识别结果列（第3列）
            {
                string text = _collectionDataGridView.Rows[e.RowIndex].Cells[2].Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    _collectionDataGridView.Rows[e.RowIndex].Cells[2].ToolTipText = text;
                }
            }
        }

        // 自定义绘制删除按钮（红色背景）
        private void CollectionDataGridView_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // 仅处理删除列（第5列，索引4）
            if (e.ColumnIndex == 4 && e.RowIndex >= 0)
            {
                e.PaintBackground(e.CellBounds, true);
                using (var brush = new SolidBrush(Color.FromArgb(220, 60, 60)))  // 红色背景
                {
                    var rect = e.CellBounds;
                    rect.Inflate(-2, -2);  // 按钮与单元格边缘留点间距
                    e.Graphics.FillRectangle(brush, rect);
                }
                using (var pen = new Pen(Color.FromArgb(180, 40, 40), 1))  // 深红色边框
                {
                    var rect = e.CellBounds;
                    rect.Inflate(-2, -2);
                    e.Graphics.DrawRectangle(pen, rect);
                }
                // 绘制"×"文字（白色）
                using (var whiteBrush = new SolidBrush(Color.White))
                using (var font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString("×", font, whiteBrush, e.CellBounds, sf);
                }
                e.Handled = true;
            }
        }

        // 点击删除按钮或行选择
        private void CollectionDataGridView_MouseClick(object sender, MouseEventArgs e)
        {
            var hit = _collectionDataGridView.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0)
            {
                // 点击删除列（第5列，索引4）
                if (hit.ColumnIndex == 4)
                {
                    int index = hit.RowIndex;
                    if (index >= 0 && index < _collectionAreas.Count)
                    {
                        _collectionAreas.RemoveAt(index);
                        RefreshCollectionList();
                        _isModify = true;
                    }
                }
                // 点击行时选中
                else if (hit.ColumnIndex >= 0 && hit.ColumnIndex <= 3)
                {
                    _selectedCollectionIndex = (int)_collectionDataGridView.Rows[hit.RowIndex].Tag;
                    _selectedVerificationIndex = -1;
                    _verificationListView.SelectedItems.Clear();
                    _imagePanel.Invalidate();
                }
            }
        }

        // 显示 OCR 结果详情弹窗
        private void ShowOcrResultDialog(string text)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "OCR 识别结果";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(500, 300);
                dialog.MinimumSize = new Size(300, 200);

                var textBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Text = text,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10)
                };

                var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
                var copyButton = new Button { Text = "复制到剪贴板", Top = 5, Left = 10, AutoSize = true };
                copyButton.Click += (s, ev) =>
                {
                    Clipboard.SetText(text);
                    copyButton.Text = "已复制!";
                    System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
                        BeginInvoke(new Action(() => copyButton.Text = "复制到剪贴板")));
                };
                buttonPanel.Controls.Add(copyButton);

                dialog.Controls.Add(textBox);
                dialog.Controls.Add(buttonPanel);
                dialog.ShowDialog();
            }
        }

        #endregion
    }
}
