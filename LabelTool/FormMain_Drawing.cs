using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LabelTool
{
    public partial class FormMain
    {
        #region 绘制区域

        private void ImagePanel_Paint(object sender, PaintEventArgs e)
        {
            var screenshot = GetCurrentScreenshot();
            if (screenshot == null)
            {
                e.Graphics.Clear(Color.Black);
                using (var font = new Font("Microsoft Sans Serif", 14))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var text = "请点击\"重新截屏\"获取屏幕截图";
                    var size = e.Graphics.MeasureString(text, font);
                    var x = (_scrollContainer.ClientSize.Width - size.Width) / 2;
                    var y = (_scrollContainer.ClientSize.Height - size.Height) / 2;
                    e.Graphics.DrawString(text, font, brush, x, y);
                }
                return;
            }

            // 绘制截屏图片
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 计算缩放比例和绘制偏移
            float scaleX = 1f, scaleY = 1f;
            int areaOffsetX = 0, areaOffsetY = 0;

            if (_isAutoZoom)
            {
                // 自动缩放模式：图片适应滚动容器
                scaleX = (float)_scrollContainer.ClientSize.Width / screenshot.Width;
                scaleY = (float)_scrollContainer.ClientSize.Height / screenshot.Height;
                // 保持宽高比，取较小值
                float minScale = Math.Min(scaleX, scaleY);
                int drawW = (int)(screenshot.Width * minScale);
                int drawH = (int)(screenshot.Height * minScale);
                int ox = (_scrollContainer.ClientSize.Width - drawW) / 2;
                int oy = (_scrollContainer.ClientSize.Height - drawH) / 2;
                e.Graphics.DrawImage(screenshot, ox, oy, drawW, drawH);
                scaleX = minScale;
                scaleY = minScale;
                areaOffsetX = ox;
                areaOffsetY = oy;
            }
            else
            {
                // 手动缩放模式：绘制缩放后的图片，滚动容器负责滚动
                scaleX = _zoomLevel;
                scaleY = _zoomLevel;
                int drawWidth = (int)(screenshot.Width * _zoomLevel);
                int drawHeight = (int)(screenshot.Height * _zoomLevel);
                e.Graphics.DrawImage(screenshot, 0, 0, drawWidth, drawHeight);
            }

            // 获取当前屏幕的区域列表
            var currentVerificationAreas = GetCurrentVerificationAreas();
            var currentCollectionAreas = GetCurrentCollectionAreas();

            // 绘制检测区域（蓝色）- 只绘制当前屏幕的区域
            for (int i = 0; i < currentVerificationAreas.Count; i++)
            {
                var area = currentVerificationAreas[i];
                int x = (int)(area.TopLeftX * scaleX) + areaOffsetX;
                int y = (int)(area.TopLeftY * scaleY) + areaOffsetY;
                var rect = new Rectangle(
                    x,
                    y,
                    (int)(area.Width * scaleX),
                    (int)(area.Height * scaleY));

                var frontColor = i == _selectedVerificationIndex ? Color.Cyan : Color.LightSkyBlue;
                var backColor = Color.FromArgb(200, Color.Black);
                DrawArea(e.Graphics, rect, frontColor, backColor, "检测: " + Path.GetFileNameWithoutExtension(area.FileName), i == _selectedVerificationIndex);
            }

            // 绘制采集区域（橙色）- 只绘制当前屏幕的区域
            for (int i = 0; i < currentCollectionAreas.Count; i++)
            {
                var area = currentCollectionAreas[i];
                int x = (int)(area.TopLeftX * scaleX) + areaOffsetX;
                int y = (int)(area.TopLeftY * scaleY) + areaOffsetY;
                var rect = new Rectangle(
                    x,
                    y,
                    (int)(area.Width * scaleX),
                    (int)(area.Height * scaleY));

                var frontColor = i == _selectedCollectionIndex ? Color.Yellow : COLLECTION_COLOR;
                var backColor = Color.FromArgb(200, Color.Black);
                DrawArea(e.Graphics, rect, frontColor, backColor, area.Name, i == _selectedCollectionIndex);
            }

            // 绘制当前拖拽的矩形
            if (_isDragging && _currentRect.Width > 0 && _currentRect.Height > 0)
            {
                var color = _isVerificationAreaMode ? VERIFICATION_COLOR : COLLECTION_COLOR;
                using (var pen = new Pen(color, 2))
                using (var brush = new SolidBrush(Color.FromArgb(50, color)))
                {
                    e.Graphics.FillRectangle(brush, _currentRect);
                    e.Graphics.DrawRectangle(pen, _currentRect);
                }
            }
        }

        private void DrawArea(Graphics g, Rectangle rect, Color frontColor, Color backColor, string label, bool isSelected)
        {
            using (var pen = new Pen(frontColor, 2))
            using (var brush = new SolidBrush(Color.FromArgb(50, frontColor)))
            using (var font = new Font("Microsoft Sans Serif", 8))
            using (var textBrush = new SolidBrush(frontColor))
            {
                // 填充半透明背景
                g.FillRectangle(brush, rect);
                // 绘制边框
                g.DrawRectangle(pen, rect);
                // 绘制标签
                var size = g.MeasureString(label, font);
                g.FillRectangle(new SolidBrush(Color.FromArgb(200, backColor)),
                    rect.X, rect.Y - (int)size.Height - 2, (int)size.Width + 4, (int)size.Height + 2);
                g.DrawString(label, font, textBrush, rect.X + 2, rect.Y - (int)size.Height - 2);

                // 如果是选中状态，绘制8个缩放手柄
                if (isSelected)
                {
                    int handleSize = 8;
                    using (var handleBrush = new SolidBrush(frontColor))
                    using (var handlePen = new Pen(Color.White, 1))
                    {
                        foreach (var handleRect in GetHandleRects(rect, handleSize))
                        {
                            g.FillRectangle(handleBrush, handleRect);
                            g.DrawRectangle(handlePen, handleRect);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 根据手柄编号设置对应的鼠标光标
        /// </summary>
        private void SetCursorForHandle(int handle)
        {
            switch (handle)
            {
                case 0: // 左上
                case 4: // 右下
                    _imagePanel.Cursor = Cursors.SizeNWSE;
                    break;
                case 2: // 右上
                case 6: // 左下
                    _imagePanel.Cursor = Cursors.SizeNESW;
                    break;
                case 1: // 上
                case 5: // 下
                    _imagePanel.Cursor = Cursors.SizeNS;
                    break;
                case 3: // 右
                case 7: // 左
                    _imagePanel.Cursor = Cursors.SizeWE;
                    break;
                default:
                    _imagePanel.Cursor = Cursors.Default;
                    break;
            }
        }

        /// <summary>
        /// 检测鼠标位置是否在缩放手柄范围内，并更新鼠标光标
        /// </summary>
        private void UpdateCursorForResizeHandles(Point mousePos)
        {
            var screenshot = GetCurrentScreenshot();
            if (screenshot == null) return;

            var currentVerificationAreas = GetCurrentVerificationAreas();
            var currentCollectionAreas = GetCurrentCollectionAreas();

            // 检查检测区域
            if (_selectedVerificationIndex >= 0 && _selectedVerificationIndex < currentVerificationAreas.Count)
            {
                var area = currentVerificationAreas[_selectedVerificationIndex];
                var panelRect = ImageToPanelRect(new VerificationAreaAdapter(area));
                int handle = GetResizeHandle(panelRect, mousePos);
                SetCursorForHandle(handle);
                return;
            }

            // 检查采集区域
            if (_selectedCollectionIndex >= 0 && _selectedCollectionIndex < currentCollectionAreas.Count)
            {
                var area = currentCollectionAreas[_selectedCollectionIndex];
                var panelRect = ImageToPanelRect(new CollectionAreaAdapter(area));
                int handle = GetResizeHandle(panelRect, mousePos);
                SetCursorForHandle(handle);
                return;
            }

            _imagePanel.Cursor = Cursors.Default;
        }

        #endregion
    }
}
