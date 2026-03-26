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
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(area.FileName));
                item.SubItems.Add($"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})");
                item.SubItems.Add("×");
                item.Tag = i;
                _verificationListView.Items.Add(item);
            }
        }

        private void RefreshCollectionList()
        {
            _collectionListView.Items.Clear();
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var item = new ListViewItem(area.Name);
                item.SubItems.Add($"({area.TopLeftX}, {area.TopLeftY}, {area.Width}x{area.Height})");
                item.SubItems.Add("×");
                item.SubItems.Add(""); // OCR识别结果列
                item.Tag = i;
                _collectionListView.Items.Add(item);
            }
        }

        private void VerificationListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_verificationListView.SelectedItems.Count > 0)
            {
                _selectedVerificationIndex = (int)_verificationListView.SelectedItems[0].Tag;
                _selectedCollectionIndex = -1;
                _collectionListView.SelectedItems.Clear();
                _imagePanel.Invalidate();
            }
        }

        private void CollectionListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_collectionListView.SelectedItems.Count > 0)
            {
                _selectedCollectionIndex = (int)_collectionListView.SelectedItems[0].Tag;
                _selectedVerificationIndex = -1;
                _verificationListView.SelectedItems.Clear();
                _imagePanel.Invalidate();
            }
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
            foreach (ListViewItem item in _collectionListView.Items)
            {
                if ((int)item.Tag == index)
                {
                    item.Selected = true;
                    break;
                }
            }
        }

        #endregion
    }
}
