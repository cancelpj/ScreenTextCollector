using PluginInterface;
using System;
using System.IO;

namespace LabelTool
{
    public partial class FormMain
    {
        #region OCR功能

        /// <summary>
        /// 根据选中的索引创建对应的 OCR 服务实例
        /// </summary>
        /// <param name="engineIndex">0=PaddleOCR, 1=OpenCvSharp</param>
        private IOcrService CreateOcrService(int engineIndex)
        {
            return engineIndex == 0
                ? (IOcrService)new ScreenTextCollector.PaddleOCR.OcrService()
                : (IOcrService)new ScreenTextCollector.OpenCvSharp.OcrService();
        }

        /// <summary>
        /// 刷新采集列表中所有区域的 OCR 识别结果
        /// </summary>
        private bool RefreshCollectionListOcrResults()
        {
            if (_screenshot == null || _collectionAreas.Count == 0)
            {
                _statusLabel.Text = "没有截图或采集区域，无法执行 OCR 识别";
                return false;
            }

            try
            {
                // 保存临时截图用于 OCR
                var tempPath = Path.Combine(Path.GetTempPath(), "labeltool_ocr_temp.png");
                _screenshot.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                // 根据选中的引擎创建对应的 OcrService
                IOcrService ocrService = CreateOcrService(_ocrEngineComboBox.SelectedIndex);

                // 对每个采集区域执行 OCR
                for (int i = 0; i < _collectionAreas.Count; i++)
                {
                    var area = _collectionAreas[i];
                    string result = ocrService.PerformOcr(tempPath, area);

                    // 更新 ListView OCR结果列（第4列，索引3）
                    if (i < _collectionListView.Items.Count)
                    {
                        _collectionListView.Items[i].SubItems[3].Text = result;
                    }
                }

                // 清理临时文件
                try { File.Delete(tempPath); } catch { }

                // 释放 OCR 服务资源
                if (ocrService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"OCR识别失败: {ex.Message}";
                return false;
            }

            return true;
        }

        #endregion
    }
}
