# ScreenTextCollector.OpenCVsharp

基于 OpenCvSharp4 实现的 OcrEngine。首先使用 OpenCV 进行图像预处理，然后使用 Tesseract 进行 OCR 识别。

## 运行要求
- .NET Framework 4.8 / .NET 6 / .NET Standard 2.0
- (Windows) Visual C++ 2022 Redistributable Package
- (Windows Server) Media Foundation

## 训练文件
Tesseract 训练数据文件的官方存储位置位于 GitHub 仓库，地址为 https://github.com/tesseract-ocr/tessdata 。请在该项目中找到 eng.traineddata 文件（英文语言包）和其他语言包，将下载的训练文件放入到 data 文件夹中。
