# 屏幕文本采集系统

## 项目简介

定时检查目标屏幕是否显示，然后识别指定区域的文字，支持保存 CSV 记录或转发 MQTT，也提供 HTTP API 模式调用。

## 核心功能

- 定时抓取指定屏幕画面（通过 ScreenNumber 指定序号）
- 支持多区域模板匹配（VerificationAreas）验证画面有效性
- 图像中的文字可采集（CollectionAreas）进行识别
- 识别结果通过 MQTT 发送到指定的 broker
- 在 HTTP API 模式下，通过外部调用触发截图和识别
- 可选识别结果保存为本地 CSV 文件

---

## 软件截图

### 屏幕选择功能

![屏幕选择](doc/select-screen.png)

### 主程序界面

![主程序界面](doc/main.png)

---

## 安装与配置

### 安装要求
- .NET Framework 4.8
- Windows 64 位操作系统（32 位很慢）

### 配置文件

编辑 `appsettings.json` 文件配置相应参数
- `DeviceName`: 采集点名称
- `CaptureFrequency`: 截图频率（秒）
- `CsvRecord`: 是否保存关键记录
- `MQTTBroker`: MQTT Broker 配置

## 使用方法
- 运行 `LabelTool.exe`（标注工具）来图形化配置检测和采集区域。
- 运行`ScreenTextCollector.exe`即可开始采集，关闭窗口即可停止采集。运行期间要保持目标屏幕画面不被遮挡。

