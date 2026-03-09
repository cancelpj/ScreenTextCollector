# Screen Text Collector
一个基于屏幕截图 OCR 的数据采集程序

## 简介
自动截屏后，先检测是否程序画面，然后提取指定图像区域的文字（目前只支持英文和数字）。

## 功能特性
- 自动截取指定屏幕的图像（通过 ScreenNumber 指定显示器编号）。
- 支持根据多个检测区域（ImageVerificationAreas）来验证截图的有效性。
- 对图像中的多个采集区域（ImageCollectionAreas）进行文本识别。
- 在 MQTT 推送模式下定时截屏，将识别结果推送到指定的 broker。
- 在 HTTP API 模式下，通过外部调用触发截图和识别。
- 可选择将识别结果保存为本地 CSV 文件。

## 使用方法

### 安装依赖
确保已安装 .NET Framework 4.8 。

### 引导配置
双击运行`setup.exe`，根据提示一步一步完成检测区域和采集区域配置。

### 画图工具操作
用画图程序的矩形选择功能框选区域，先移动到区域左上角记录顶点坐标，再完成框选记录宽高。

### 运行
以管理员身份运行`stc.exe`即可开始采集，关闭窗口即可停止采集。运行期间要保持目标屏幕画面不被遮挡。

## 其它

### 配置文件说明
编辑`appsettings.json`文件以适应您的需求：
- `DeviceName`: 采集对象名称。
- `CsvRecord`: 是否保存本地记录。
- `Http`: HTTP 服务配置。
- `MQTTBroker`: MQTT Broker 配置。
- `ScreenNumber`: 要捕获的屏幕编号。
- `ImageVerificationAreas`: 检测区域列表。（**自动配置**）
- `ImageCollectionAreas`: 采集区域列表。（**自动配置**）
