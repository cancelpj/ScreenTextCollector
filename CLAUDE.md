# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个基于 .NET Framework 4.8 的屏幕文本采集（OCR）应用程序，用于从指定屏幕区域定时采集文本并通过 MQTT 推送或 HTTP 接口提供数据。

## 项目结构

```
ScreenTextCollector/
├── ScreenTextCollector/          # 主程序（WinForms应用）
│   ├── Program.cs               # 入口点，单实例运行检查
│   ├── Form1.cs                 # 主窗体，NLog日志UI展示
│   ├── ServiceMqttPush.cs       # MQTT推送服务
│   ├── ServiceWebApi.cs         # HTTP服务（健康检查、主动触发采集）
│   ├── SimpleMqttClient.cs      # 自定义MQTT客户端实现
│   ├── FunctionCall.cs          # 核心业务逻辑
│   └── MemoryLeakDetector.cs    # 内存泄露检测工具
├── PluginInterface/              # 共享接口和工具类
│   ├── Tool.cs                  # 工具类（日志广播、截屏、CSV保存）
│   ├── Settings.cs              # 配置模型（从appsettings.json加载）
│   ├── IOcrService.cs           # OCR服务接口
│   ├── MethodResult.cs          # 操作结果封装
│   └── NLogGuiTarget.cs         # NLog自定义目标（日志转发到UI）
├── ScreenTextCollector.OpenCvSharp/  # OpenCvSharp OCR实现
│   └── OcrService.cs            # 图像验证+OCR识别
├── ScreenTextCollector.PaddleOCR/    # PaddleOCR OCR实现（可选）
└── Setup/                        # 安装程序项目
```

## 常用命令

```bash
# 构建项目（使用 MSBuild）
msbuild ScreenTextCollector.sln /p:Configuration=Release

# 或使用 dotnet build（需要 .NET SDK）
dotnet build ScreenTextCollector/ScreenTextCollector.csproj -c Release

# 发布主程序
dotnet publish ScreenTextCollector/ScreenTextCollector.csproj -c Release -o bin/publish
```

## 核心功能

1. **定时采集**: 根据配置的 CaptureFrequency 定时截取指定屏幕区域
2. **图像验证**: 使用模板匹配验证屏幕是否显示目标程序画面
3. **OCR识别**: 从指定区域识别文字（支持 OpenCvSharp / PaddleOCR）
4. **MQTT推送**: 将识别结果以 JSON 格式推送到 MQTT Broker
5. **HTTP服务**:
   - `/health` - 健康检查
   - `/stc` - 手动触发一次采集
   - `/process/{name}` - 检查进程状态

## 配置说明

配置文件 `appsettings.json` 位于主程序输出目录，关键配置项：

- `DeviceName`: 采集点名称
- `ScreenNumber`: 屏幕编号（从0开始）
- `CaptureFrequency`: 采集频率（秒）
- `CsvRecord`: 是否保存CSV记录
- `ImageVerificationAreas`: 图像验证区域列表
- `ImageCollectionAreas`: 图像采集区域列表
- `MqttBroker`: MQTT推送配置
- `Http`: HTTP服务配置

## 架构要点

- **单实例运行**: 使用全局 Mutex 确保只有一个实例运行
- **日志广播**: Tool 类通过静态事件将 NLog 日志广播到 UI（Form1 订阅 LogReceived 事件）
- **资源清理**: 注意及时释放 Bitmap、Mat、网络连接等资源避免内存泄露
- **OCR可插拔**: 通过 IOcrService 接口支持不同 OCR 引擎
