# ScreenTextCollector.OpenCVsharp
基于 OpenCvSharp4 实现的图像文字采集类库。先采用 OpenCV 进行简单的图像灰度处理，然后使用 Tesseract 进行 OCR 识别。

## 安装与配置

### 安装依赖
- .NET Framework 4.8 / .NET 6 / .NET Standard 2.0
- (Windows) Visual C++ 2022 Redistributable Package
- (Windows Server) Media Foundation

### 训练文件
Tesseract 语言数据文件的官方存储位置是其 GitHub 仓库，地址为 https://github.com/tesseract-ocr/tessdata ，本项目中已包含 eng.traineddata 文件，如需其他语言数据文件，请自行下载并放置在 data 文件夹中。

### 功能配置
暂无。