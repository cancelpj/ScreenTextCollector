# OcrServer

OcrServer 是 ScreenTextCollector C/S 架构中的客户端组件，负责从 CaptureScreen 服务获取截图并执行 OCR，以 MQTT/HTTP 方式对外提供结果。

## 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core 10 Native AOT | 跨平台，启动快，内存占用低 |
| MQTT | MQTTnet 5.1 | AOT 兼容，替换原手写 SimpleMqttClient |
| JSON | System.Text.Json + Source Generator | AOT 兼容，替换原 Newtonsoft.Json |
| 日志 | Serilog.Sinks.File + Serilog.Sinks.Console | AOT 兼容 |
| OCR | PaddleOCRSharp 6.1.0 | 支持 CPU 推理，AOT 不兼容 |

## 项目结构

```
OcrServer/
├── OcrServer.csproj              # .NET 10
├── Program.cs                    # 启动入口，DI 注册
├── Configuration/
│   ├── AppSettings.cs            # appsettings.json 结构体
│   ├── SerilogConfig.cs          # Serilog 配置（代替 Serilog.Settings.Configuration ，因为它不兼容 AOT）
│   ├── DeviceConfig.cs           # 单台设备配置
│   └── CaptureSettings.cs        # CaptureSettings.{DeviceCode}.json 结构体
├── Services/
│   ├── CaptureScreenClient.cs    # HttpClient 调用 CaptureScreen（按屏幕编号请求完整截图）
│   ├── OcrService.cs             # PaddleOCRSharp OCR + 图像校验（像素级相似度）+ 模板缓存
│   ├── CollectService.cs         # 定时采集主循环：获取整屏 → 图像验证 → 多区域并发 OCR → 推送
│   ├── ResultCache.cs            # 线程安全缓存（按 DeviceCode 隔离）
│   ├── MqttPushService.cs        # MQTTnet 推送（按 Topic 分组，支持指数退避自动重连）
│   └── HttpApiService.cs         # Minimal API（/health, /collect）
├── Serialization/
│   └── JsonContext.cs            # System.Text.Json Source Generator
└── data/
    ├── appsettings.json                      # 运行配置
    ├── CaptureSettings.{DeviceCode}.json     # 采集区域配置
    └── *.png                                 # 图像验证模板图
```

## 编译

```powershell
# Debug 编译（Framework-Dependent）
dotnet build OcrServer.csproj -c Debug

# Release 编译（Framework-Dependent）
dotnet build OcrServer.csproj -c Release

# 发布
$env:PATH = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer;' + $env:PATH
dotnet publish OcrServer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o ./bin/OcrServer-x64

# 或使用发布配置文件
dotnet publish -p:PublishProfile=.\Properties\PublishProfiles\win-x64.pubxml
```

**AOT 发布说明**：（因 PaddleOCRSharp 6.1.0 暂不支持 AOT 发布）

| 配置项 | 值 |
|--------|-----|
| 发布模式 | Native AOT (`PublishAot=true`) |
| 目标框架 | `net10.0` |
| 运行时 | `win-x64` |
| 部署方式 | Self-Contained |
| 发布目录 | `publish/` |

**前置条件**：需要安装 Visual Studio C++ 桌面开发工具。若 `vswhere.exe` 找不到，按上述命令将 VS Installer 目录加入 PATH。

**发布产物**：
- `OcrServer.exe` (18 MB) — 主程序单文件
- `*.dll` — PaddleOCR 原生依赖（OpenCV、MKL 等），需随包分发
- 总大小约 368 MB

## 配置

### appsettings.json

主配置文件，包含 MQTT、HTTP、日志、多设备列表等配置。

```json
{
  "csvRecord": false,
  "http": {
    "enableHttp": true,
    "ip": "0.0.0.0",
    "port": 8081
  },
  "mqttBroker": {
    "enableMqttPush": true,
    "ip": "192.168.1.100",
    "port": 1883,
    "clientId": "OcrServer-001",
    "username": "",
    "password": "",
    "reconnect": {
      "initialDelaySeconds": 3,
      "maxDelaySeconds": 60,
      "maxRetries": 0
    }
  },
  "devices": [
    {
      "deviceCode": "DEVICE-001",
      "captureScreenUrl": "http://192.168.1.50:8080",
      "timeoutSeconds": 10,
      "captureFrequency": 5,
      "defaultExtendPayload": {
        "DEVICECODE": "110001",
        "GroupCode": "collection1"
      },
      "topics": [
        {
          "name": "screen/collection/110001",
          "extendPayload": { "GroupCode": "collection1" }
        },
        {
          "name": "screen/alarm/110001",
          "extendPayload": { "GroupCode": "alarm1" }
        }
      ]
    }
  ]
}
```

**MQTT 重连配置（`mqttBroker.reconnect`）**：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `initialDelaySeconds` | int | `3` | 首次重连等待时间（秒） |
| `maxDelaySeconds` | int | `60` | 最大等待时间（秒），超过后封顶 |
| `maxRetries` | int | `0` | 最大重试次数，`0` 表示无限重试 |

重连策略为指数退避：首次 3s → 6s → 12s → 24s → 48s → 60s（封顶）。

### CaptureSettings.{DeviceCode}.json

每台设备对应一个独立的配置文件，从文件名中提取 `DeviceCode`。

```json
{
  "ocrEngine": "PaddleOCR",
  "verificationAreas": [
    {
      "screenNumber": 0,
      "topLeftX": 0,
      "topLeftY": 0,
      "width": 50,
      "height": 50,
      "fileName": "DEVICE-001_verify.png",
      "matchThreshold": 0.8
    }
  ],
  "collectionAreas": [
    {
      "screenNumber": 0,
      "name": "V001",
      "topic": "screen/collection/110001",
      "topLeftX": 60,
      "topLeftY": 205,
      "width": 44,
      "height": 11
    }
  ]
}
```

**Topic 查找规则**：按 `CollectionArea.Topic` 在设备的 `Topics` 列表中查找匹配项，合并 `DefaultExtendPayload` 和 `Topic.ExtendPayload`（后者优先级更高）。若找不到匹配项，则使用 `Topics[0]` 作为默认值。

## HTTP 接口

| 接口 | 响应 | 说明 |
|------|------|------|
| `GET /health` | `{"status":"ok","timestamp":...}` | 健康检查 |
| `GET /collect` | `{"DEVICE-001":{"温度":"25.3","湿度":"60"},"DEVICE-002":{...}}` | 所有设备所有区域 |
| `GET /collect/{deviceCode}` | `{"温度":"25.3","湿度":"60"}` | 指定设备所有区域 |
| `GET /collect/{deviceCode}/{areaName}` | `"25.3"` | 指定设备单区域 |

## MQTT 消息规范

```json
{
  "timestamp": 1712345678900,
  "Data": {
    "温度": "25.3",
    "湿度": "60"
  },
  "ExtendPayload": {
    "DEVICECODE": "110001",
    "GroupCode": "collection1"
  }
}
```

## 运行

```powershell
# 编译后运行
dotnet run --project OcrServer.csproj

# 或直接运行编译产物
OcrServer.exe
```

## 部署架构

**部署模型：多台 CaptureScreen 对应 1 台 OcrServer**

```
┌──────────────────────────┐
│  CaptureScreen (Go)      │
│  Windows XP/2000 32位    │
└──────────┬───────────────┘
           │ POST /api/screenshot
           ▼
┌──────────────────────────┐   MQTT / HTTP    ┌──────────────────────────┐
│   OcrServer (.NET 10)    │ ───────────────▶ │   调用者 / MQTT Broker    │
│   跨平台                  │                  └──────────────────────────┘
└──────────────────────────┘
```

## 注意事项

1. **PaddleOCR 模型**：确保 `publish/inference/` 目录存在，包含 OCR 模型文件
2. **设备列表**：至少配置一台设备（CaptureScreen），否则服务无法启动
3. **MQTT 连接**：支持用户名/密码认证，自动重连
