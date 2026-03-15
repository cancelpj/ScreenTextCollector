# 屏幕文本采集系统

## 项目简介

定时检查目标屏幕是否显示，然后识别指定区域的文字，支持保存 CSV 记录或转发 MQTT，也提供 HTTP API 模式调用。

## 核心功能

- 定时抓取指定屏幕画面（通过 ScreenNumber 指定序号）
- 支持多区域模板匹配（ImageVerificationAreas）验证画面有效性
- 图像中的文字可采集（ImageCollectionAreas）进行识别
- 识别结果通过 MQTT 发送到指定的 broker
- 在 HTTP API 模式下，通过外部调用触发截图和识别
- 可选识别结果保存为本地 CSV 文件

## 开发历史

### 2026年3月

#### 易用性
- **图形化标注**：抛弃旧的 Setup 命令行配置工具，重写 LabelTool 图形化标注配置工具

#### 项目结构与构建优化
- **简化项目文件结构**：使用新的 SDK 格式，重构项目配置文件
- **添加 x64 平台支持**：更新项目配置，支持 x64 平台构建
- **统一项目配置**：添加清理脚本，更新 NuGet 包引用

#### OCR 功能增强
- **新增 PaddleOCR 支持**：实现可插拔 OCR 引擎，支持 OpenCvSharp 和 PaddleOCR 两种引擎
- **图像预处理配置**：添加 OCR 引擎选项和图像预处理参数配置
- **后处理功能**：重构 OCR 服务，添加文本后处理功能
- **并行处理优化**：优化 OCR 服务并行处理逻辑

#### 测试体系建设
- **添加测试项目**：新增 ScreenTextCollector.Tests 测试项目
- **测试用例完善**：添加真实测试数据，重构测试类结构
- **性能统计**：为 OCR 测试添加性能统计功能
- **多引擎切换**：支持方便切换不同的 OcrEngine 进行测试

#### 文档与配置
- **更新 .gitignore**：优化版本控制配置
- **项目 README 完善**：更新各模块 README 文件内容

---

## 安装与配置

### 安装要求

.NET Framework 4.8
Windows 64 位操作系统（32 位很慢）

### 配置文件

编辑 `appsettings.json` 文件配置相应参数
- `DeviceName`: 采集点名称
- `CaptureFrequency`: 截图频率（秒）
- `CsvRecord`: 是否保存关键记录
- `MQTTBroker`: MQTT Broker 配置

### 区域设置

运行 `LabelTool.exe`（标注工具）来图形化配置检测和采集区域。

## 使用方法

运行`ScreenTextCollector.exe`即可开始采集，关闭窗口即可停止采集。运行期间要保持目标屏幕画面不被遮挡。
