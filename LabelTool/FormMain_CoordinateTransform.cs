using System;
using System.Drawing;

namespace LabelTool
{
    public partial class FormMain
    {
        /// <summary>
        /// 获取图片在面板上的绘制偏移（考虑缩放和平移）
        /// </summary>
        private (float scaleX, float scaleY, float offsetX, float offsetY) GetImageTransform()
        {
            var screenshot = GetCurrentScreenshot();
            if (screenshot == null)
                return (1, 1, 0, 0);

            if (_isAutoZoom)
            {
                float scale = Math.Min(
                    (float)_scrollContainer.ClientSize.Width / screenshot.Width,
                    (float)_scrollContainer.ClientSize.Height / screenshot.Height);
                int drawW = (int)(screenshot.Width * scale);
                int drawH = (int)(screenshot.Height * scale);
                return (scale, scale,
                    (_scrollContainer.ClientSize.Width - drawW) / 2f,
                    (_scrollContainer.ClientSize.Height - drawH) / 2f);
            }
            else
            {
                return (_zoomLevel, _zoomLevel, 0, 0);
            }
        }

        /// <summary>
        /// 将面板坐标转换为图片坐标（使用 Math.Round 减少精度损失）
        /// </summary>
        private Point PanelToImage(Point panelPoint)
        {
            var (scaleX, scaleY, offsetX, offsetY) = GetImageTransform();
            int imgX = (int)Math.Round((panelPoint.X - offsetX) / scaleX);
            int imgY = (int)Math.Round((panelPoint.Y - offsetY) / scaleY);
            return new Point(imgX, imgY);
        }

        /// <summary>
        /// 将图片区域转换为面板区域（统一逻辑，适配 ImageVerificationArea 和 ImageCollectionArea）
        /// 使用 Math.Round 减少精度损失
        /// </summary>
        private Rectangle ImageToPanelRect<T>(T adapter) where T : AreaBounds
        {
            var (scaleX, scaleY, offsetX, offsetY) = GetImageTransform();
            return new Rectangle(
                (int)Math.Round(adapter.TopLeftX * scaleX + offsetX),
                (int)Math.Round(adapter.TopLeftY * scaleY + offsetY),
                (int)Math.Round(adapter.Width * scaleX),
                (int)Math.Round(adapter.Height * scaleY));
        }

        /// <summary>
        /// 根据基准矩形和手柄大小，计算 8 个手柄的 Rectangle（左上→顺时针→左）
        /// </summary>
        private static Rectangle[] GetHandleRects(Rectangle rect, int handleSize)
        {
            var rects = new Rectangle[8];
            for (int i = 0; i < 8; i++)
            {
                int cx = (int)(rect.X + rect.Width * _handleFX[i]);
                int cy = (int)(rect.Y + rect.Height * _handleFY[i]);
                rects[i] = new Rectangle(cx - handleSize / 2, cy - handleSize / 2, handleSize, handleSize);
            }
            return rects;
        }
    }
}
