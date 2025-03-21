# 基于屏幕截图 OCR 的数据采集程序

## 简介

定时截屏，先检测是否程序画面，然后识别指定图像区域的文字，本地保存 CSV 或者转发 MQTT 。

## 功能特性

- 定时截取指定屏幕的图像（通过 ScreenNumber 指定显示器编号）。
- 支持根据多个检测区域（ImageVerificationAreas）来验证截图的有效性。
- 对图像中的多个采集区域（ImageCollectionAreas）进行文本识别。
- 将识别结果通过 MQTT 推送到指定的 broker。
- 可选择将识别结果保存为本地 CSV 文件。

## 安装与配置

### 安装依赖

请查看各项目的 .NET Framework 版本要求。

### 配置文件

编辑`appsettings.json`文件以适应您的需求：
- `DeviceName`: 采集对象名称。
- `CaptureFrequency`: 截图频率（秒）。
- `CsvRecord`: 是否保存本地记录。
- `MQTTBroker`: MQTT Broker 配置。
- `ScreenNumber`: 要捕获的屏幕编号。
- `ImageVerificationAreas`: 检测区域列表。
- `ImageCollectionAreas`: 采集区域列表。

### 坐标配置

- 利用程序自带的截屏功能，先截取一张目标显示器的完整屏幕画面，然后粘贴到画图程序中操作。
- 先用画图程序的矩形选择功能框选出检测区域，记录框选时的左上角顶点坐标和宽高，然后打开一个新的图画程序，把框选内容复制到其中保存，注意保存的图片宽高要和框选时的一致。
- 用画图程序的矩形选择功能框选出采集区域，记录框选时的左上角顶点坐标和宽高

## 使用方法
双击运行 `stc.exe` 即可开始采集，关闭窗口即可停止采集。运行期间要保持目标屏幕画面不被遮挡。