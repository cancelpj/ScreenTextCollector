# Screen Text Collector
一个基于屏幕截图 OCR 的数据采集程序

## 简介
定时截屏，先检测是否程序画面，然后识别指定图像区域的文字，本地保存 CSV 或者转发 MQTT 。

## 功能特性
- 定时截取指定屏幕的图像（通过 ScreenNumber 指定显示器编号）。
- 支持根据多个检测区域（ImageVerificationAreas）来验证截图的有效性。
- 对图像中的多个采集区域（ImageCollectionAreas）进行文本识别。
- 将识别结果通过 MQTT 推送到指定的 broker。
- 可选择将识别结果保存为本地 CSV 文件。

## 使用方法

### 安装依赖
确保已安装 .NET Framework 4.8 。

### 引导配置
双击运行`setup.exe`，根据提示一步一步完成检测区域和采集区域配置。

### 画图工具操作
用画图程序的矩形选择功能框选区域，先移动到区域左上角记录顶点坐标，再完成框选记录宽高。

### 手动运行
双击运行`stc.exe`即可开始采集，关闭窗口即可停止采集。运行期间要保持目标屏幕画面不被遮挡。

## 其它

### 配置文件说明
编辑`appsettings.json`文件以适应您的需求：
- `DeviceName`: 采集对象名称。
- `CaptureFrequency`: 截图频率（秒）。
- `CsvRecord`: 是否保存本地记录。
- `MQTTBroker`: MQTT Broker 配置。
- `ScreenNumber`: 要捕获的屏幕编号。
- `ImageVerificationAreas`: 检测区域列表。（**自动配置**）
- `ImageCollectionAreas`: 采集区域列表。（**自动配置**）
