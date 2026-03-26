using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 鼠标拖拽框选

        private void ImagePanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 聚焦到图片面板以接收键盘事件
            _imagePanel.Focus();

            // 鼠标右键开始平移（手动缩放模式）
            if (e.Button == MouseButtons.Right && !_isAutoZoom)
            {
                _isPanning = true;
                _scrollOffset = new Point(-_scrollContainer.AutoScrollPosition.X, -_scrollContainer.AutoScrollPosition.Y);
                _lastMousePos = e.Location;
                _imagePanel.Cursor = Cursors.Hand;
                return;
            }

            var (scaleX, scaleY, offsetX, offsetY) = GetImageTransform();

            // 确保选中索引有效
            if (_selectedVerificationIndex >= _verificationAreas.Count)
                _selectedVerificationIndex = -1;
            if (_selectedCollectionIndex >= _collectionAreas.Count)
                _selectedCollectionIndex = -1;

            // 检查是否点击了选中区域的边缘（缩放）或内部（移动）
            if (_selectedVerificationIndex >= 0)
            {
                var area = _verificationAreas[_selectedVerificationIndex];
                var panelRect = ImageToPanelRect(new VerificationAreaAdapter(area));
                int handle = GetResizeHandle(panelRect, e.Location);

                if (handle >= 0)
                {
                    // 点击了缩放手柄
                    _isResizingArea = true;
                    _resizeHandle = handle;
                }
                else if (panelRect.Contains(e.Location))
                {
                    // 点击了区域内部，开始拖拽移动
                    _isDraggingArea = true;
                }
            }
            else if (_selectedCollectionIndex >= 0)
            {
                var area = _collectionAreas[_selectedCollectionIndex];
                var panelRect = ImageToPanelRect(new CollectionAreaAdapter(area));
                int handle = GetResizeHandle(panelRect, e.Location);

                if (handle >= 0)
                {
                    _isResizingArea = true;
                    _resizeHandle = handle;
                }
                else if (panelRect.Contains(e.Location))
                {
                    _isDraggingArea = true;
                }
            }

            if (_isDraggingArea || _isResizingArea)
            {
                _dragStart = e.Location;
                _isDragging = true;
                _imagePanel.Cursor = Cursors.SizeAll;
            }
            else
            {
                // 新建区域
                _dragStart = e.Location;
                _isDragging = true;
            }
        }

        private void ImagePanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 平移（使用滚动条实现）
            if (_isPanning)
            {
                int dx = e.Location.X - _lastMousePos.X;
                int dy = e.Location.Y - _lastMousePos.Y;
                // 直接累加偏移量，避免 AutoScrollPosition 读写不对称导致的抖动
                _scrollOffset.X += dx;
                _scrollOffset.Y += dy;
                _scrollContainer.AutoScrollPosition = _scrollOffset;
                _lastMousePos = e.Location;
                return;
            }

            var (scaleX, scaleY, offsetX, offsetY) = GetImageTransform();
            int deltaImgX = (int)((e.Location.X - _dragStart.X) / scaleX);
            int deltaImgY = (int)((e.Location.Y - _dragStart.Y) / scaleY);

            if (_screenshot == null || !_isDragging) return;

            if (_isDraggingArea)
            {
                // 拖拽移动选中区域
                if (_selectedVerificationIndex >= 0)
                {
                    var area = _verificationAreas[_selectedVerificationIndex];
                    area.TopLeftX += deltaImgX;
                    area.TopLeftY += deltaImgY;
                    RefreshVerificationList();
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    var area = _collectionAreas[_selectedCollectionIndex];
                    area.TopLeftX += deltaImgX;
                    area.TopLeftY += deltaImgY;
                    RefreshCollectionList();
                }
                _dragStart = e.Location;
                _imagePanel.Invalidate();
            }
            else if (_isResizingArea)
            {
                // 缩放选中区域
                if (_selectedVerificationIndex >= 0)
                {
                    var area = _verificationAreas[_selectedVerificationIndex];
                    ResizeArea(new VerificationAreaAdapter(area), _resizeHandle, deltaImgX, deltaImgY);
                    RefreshVerificationList();
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    var area = _collectionAreas[_selectedCollectionIndex];
                    ResizeArea(new CollectionAreaAdapter(area), _resizeHandle, deltaImgX, deltaImgY);
                    RefreshCollectionList();
                }
                _dragStart = e.Location;
                _imagePanel.Invalidate();
            }
            else
            {
                // 新建区域
                _dragEnd = e.Location;
                _currentRect = GetRectangle(_dragStart, _dragEnd);
                _imagePanel.Invalidate();
            }
        }

        /// <summary>
        /// 鼠标进入区域时检测并更新光标
        /// </summary>
        private void ImagePanel_MouseEnter(object sender, EventArgs e)
        {
            if (_screenshot == null) return;
            var pos = _imagePanel.PointToClient(Cursor.Position);
            UpdateCursorForResizeHandles(pos);
        }

        /// <summary>
        /// 鼠标离开区域时恢复默认光标
        /// </summary>
        private void ImagePanel_MouseLeave(object sender, EventArgs e)
        {
            _imagePanel.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 鼠标滚轮缩放（手动缩放模式下有效）
        /// </summary>
        private void ImagePanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (_screenshot == null || _isAutoZoom) return;

            // 计算缩放步进
            float oldZoom = _zoomLevel;
            if (e.Delta > 0)
                _zoomLevel = Math.Min(MAX_ZOOM, _zoomLevel + ZOOM_STEP);
            else
                _zoomLevel = Math.Max(MIN_ZOOM, _zoomLevel - ZOOM_STEP);

            if (Math.Abs(oldZoom - _zoomLevel) > 0.001f)
            {
                UpdateScrollSize();
                _imagePanel.Invalidate();
                UpdateZoomUI();
            }
        }

        private void ImagePanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 结束平移（右键）
            if (e.Button == MouseButtons.Right)
            {
                _isPanning = false;
                _imagePanel.Cursor = Cursors.Default;
                return;
            }

            if (!_isDragging) return;

            // 如果是拖拽或缩放区域，结束操作
            if (_isDraggingArea || _isResizingArea)
            {
                _isDraggingArea = false;
                _isResizingArea = false;
                _isDragging = false;
                _resizeHandle = -1;
                _imagePanel.Invalidate();
                // 恢复默认光标
                UpdateCursorForResizeHandles(e.Location);
                return;
            }

            _dragEnd = e.Location;
            _currentRect = GetRectangle(_dragStart, _dragEnd);

            _isDragging = false;

            // 确保矩形有效
            if (_currentRect.Width > 10 && _currentRect.Height > 10)
            {
                // 弹出对话框确认区域属性
                ShowAreaDialog(_currentRect);
            }

            _imagePanel.Invalidate();
        }

        private void ImagePanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 检查是否点击了已有区域
            var clickPoint = e.Location;

            // 转换面板坐标到图片坐标
            var imgPoint = PanelToImage(clickPoint);
            int imgX = imgPoint.X;
            int imgY = imgPoint.Y;

            // 检查检测区域
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                if (rect.Contains(imgX, imgY))
                {
                    _selectedVerificationIndex = i;
                    _selectedCollectionIndex = -1;
                    SelectVerificationItem(i);
                    _imagePanel.Invalidate();
                    UpdateCursorForResizeHandles(clickPoint);
                    return;
                }
            }

            // 检查采集区域
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var rect = new Rectangle(area.TopLeftX, area.TopLeftY, area.Width, area.Height);
                if (rect.Contains(imgX, imgY))
                {
                    _selectedCollectionIndex = i;
                    _selectedVerificationIndex = -1;
                    SelectCollectionItem(i);
                    _imagePanel.Invalidate();
                    UpdateCursorForResizeHandles(clickPoint);
                    return;
                }
            }

            // 点击空白区域，取消选择
            _selectedVerificationIndex = -1;
            _selectedCollectionIndex = -1;
            _verificationListView.SelectedItems.Clear();
            _collectionListView.SelectedItems.Clear();
            _imagePanel.Invalidate();
            UpdateCursorForResizeHandles(clickPoint);
        }

        private Rectangle GetRectangle(Point start, Point end)
        {
            int x = Math.Min(start.X, end.X);
            int y = Math.Min(start.Y, end.Y);
            int width = Math.Abs(end.X - start.X);
            int height = Math.Abs(end.Y - start.Y);
            return new Rectangle(x, y, width, height);
        }

        private int GetResizeHandle(Rectangle rect, Point point)
        {
            var rects = GetHandleRects(rect, 8);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].Contains(point))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 缩放区域（统一逻辑，适配 ImageVerificationArea 和 ImageCollectionArea）
        /// </summary>
        private void ResizeArea<T>(T adapter, int handle, int deltaX, int deltaY) where T : AreaBounds
        {
            switch (handle)
            {
                case 0: // 左上
                    adapter.TopLeftX += deltaX;
                    adapter.TopLeftY += deltaY;
                    adapter.Width -= deltaX;
                    adapter.Height -= deltaY;
                    break;
                case 1: // 上
                    adapter.TopLeftY += deltaY;
                    adapter.Height -= deltaY;
                    break;
                case 2: // 右上
                    adapter.TopLeftY += deltaY;
                    adapter.Width += deltaX;
                    adapter.Height -= deltaY;
                    break;
                case 3: // 右
                    adapter.Width += deltaX;
                    break;
                case 4: // 右下
                    adapter.Width += deltaX;
                    adapter.Height += deltaY;
                    break;
                case 5: // 下
                    adapter.Height += deltaY;
                    break;
                case 6: // 左下
                    adapter.TopLeftX += deltaX;
                    adapter.Width -= deltaX;
                    adapter.Height += deltaY;
                    break;
                case 7: // 左
                    adapter.TopLeftX += deltaX;
                    adapter.Width -= deltaX;
                    break;
            }
            // 确保尺寸有效
            if (adapter.Width < 10) adapter.Width = 10;
            if (adapter.Height < 10) adapter.Height = 10;
        }

        /// <summary>
        /// 双击编辑区域
        /// </summary>
        private void ImagePanel_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_screenshot == null) return;

            // 检查是否点击了检测区域
            for (int i = 0; i < _verificationAreas.Count; i++)
            {
                var area = _verificationAreas[i];
                var panelRect = ImageToPanelRect(new VerificationAreaAdapter(area));
                if (panelRect.Contains(e.Location))
                {
                    EditVerificationArea(i);
                    return;
                }
            }

            // 检查是否点击了采集区域
            for (int i = 0; i < _collectionAreas.Count; i++)
            {
                var area = _collectionAreas[i];
                var panelRect = ImageToPanelRect(new CollectionAreaAdapter(area));
                if (panelRect.Contains(e.Location))
                {
                    EditCollectionArea(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 键盘事件 - Delete 删除选中区域
        /// </summary>
        private void ImagePanel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (_selectedVerificationIndex >= 0)
                {
                    DeleteVerificationArea(_selectedVerificationIndex);
                    e.Handled = true;
                }
                else if (_selectedCollectionIndex >= 0)
                {
                    DeleteCollectionArea(_selectedCollectionIndex);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 编辑检测区域
        /// </summary>
        private void EditVerificationArea(int index)
        {
            if (index < 0 || index >= _verificationAreas.Count) return;

            var area = _verificationAreas[index];
            var dialog = new FormAreaDialog(true, area.MatchThreshold, Path.GetFileNameWithoutExtension(area.FileName), area.TopLeftX, area.TopLeftY, area.Width, area.Height);
            dialog.ValidateName = name =>
            {
                if (IsVerificationNameDuplicate(name, index))
                    return $"检测区域名称 \"{name}\" 已存在，请使用其他名称。";
                return null;
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                area.TopLeftX = dialog.AreaX;
                area.TopLeftY = dialog.AreaY;
                area.Width = dialog.AreaWidth;
                area.Height = dialog.AreaHeight;
                area.FileName = dialog.AreaName + ".png";
                area.MatchThreshold = dialog.MatchThreshold;
                RefreshVerificationList();
                _imagePanel.Invalidate();
            }
        }

        /// <summary>
        /// 编辑采集区域
        /// </summary>
        private void EditCollectionArea(int index)
        {
            if (index < 0 || index >= _collectionAreas.Count) return;

            var area = _collectionAreas[index];
            var dialog = new FormAreaDialog(false, 0.8f, area.Name, area.TopLeftX, area.TopLeftY, area.Width, area.Height, area.Topic);
            dialog.ValidateName = name =>
            {
                if (IsCollectionNameDuplicate(name, index))
                    return $"采集区域名称 \"{name}\" 已存在，请使用其他名称。";
                return null;
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                area.TopLeftX = dialog.AreaX;
                area.TopLeftY = dialog.AreaY;
                area.Width = dialog.AreaWidth;
                area.Height = dialog.AreaHeight;
                area.Name = dialog.AreaName;
                area.Topic = dialog.Topic;
                RefreshCollectionList();
                _imagePanel.Invalidate();
            }
        }

        #endregion
    }
}
