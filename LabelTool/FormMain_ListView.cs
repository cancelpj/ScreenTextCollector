using System;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 列表操作

        private void RefreshVerificationList()
        {
            _verificationListView.Items.Clear();
            var currentAreas = GetCurrentVerificationAreas();
            for (int i = 0; i < currentAreas.Count; i++)
            {
                var area = currentAreas[i];
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(area.FileName));
                item.SubItems.Add($"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})");
                item.SubItems.Add("×");
                item.Tag = i;
                _verificationListView.Items.Add(item);
            }
        }

        private void RefreshCollectionList()
        {
            _collectionDataGridView.Rows.Clear();
            var currentAreas = GetCurrentCollectionAreas();
            for (int i = 0; i < currentAreas.Count; i++)
            {
                var area = currentAreas[i];
                int rowIndex = _collectionDataGridView.Rows.Add();
                var row = _collectionDataGridView.Rows[rowIndex];
                row.Cells[0].Value = area.Name;
                row.Cells[1].Value = $"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})";
                row.Cells[2].Value = ""; // OCR 识别结果
                row.Cells[3].Value = ""; // 展开按钮（列定义已有 Text="..."）
                row.Cells[4].Value = ""; // 删除按钮（使用自定义绘制）
                row.Tag = i;
            }
        }

        private void VerificationListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_verificationListView.SelectedItems.Count > 0)
            {
                _selectedVerificationIndex = (int)_verificationListView.SelectedItems[0].Tag;
                _selectedCollectionIndex = -1;
                _collectionDataGridView.ClearSelection();
                _imagePanel.Invalidate();
            }
        }

        private void CollectionListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            // DataGridView 使用 SelectionChanged 事件，此方法保留兼容
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
            for (int i = 0; i < _collectionDataGridView.Rows.Count; i++)
            {
                if ((int)_collectionDataGridView.Rows[i].Tag == index)
                {
                    _collectionDataGridView.Rows[i].Selected = true;
                    break;
                }
            }
        }

        #endregion
    }
}
