# CaptureScreen & OcrServer 安装使用说明

## 系统架构

**部署模型：多台 CaptureScreen 对应 1 台 OcrServer**

```
┌──────────────────────────┐
│  CaptureScreen (Go)      │  DeviceCode: DEVICE-001
│  192.168.1.50:8080       │  Windows XP/2000 32位
│  只返回完整屏幕截图       │  每屏幕一张 Base64 JPEG
└──────────┬───────────────┘
           │ 返回完整屏幕截图（Base64 JPEG）
           ▼
┌──────────────────────────┐   MQTT / HTTP     ┌─────────────────────────┐
│   OcrServer (.NET 10)    │ ───────────────▶ │   调用者 / MQTT Broker   │
│   跨平台                  │                  └──────────────────────────┘
│   图像验证 + OCR + 推送   │
└──────────—───────────────┘
           ▲ 返回完整屏幕截图
           |
┌──────────┴───────────────┐
│  CaptureScreen (Go)      │  DeviceCode: DEVICE-002
│  192.168.1.51:8080       │  Windows XP/2000 32位
└──────────────────────────┘
```

- **客户端 CaptureScreen**：部署在 Windows XP/2000 32位老旧系统，仅负责截取完整屏幕截图（被动响应）
- **服务端 OcrServer**：跨平台运行，主动调用 CaptureScreen 获取完整屏幕截图，执行图像验证和 OCR，以 MQTT/HTTP 方式对外提供结果

**关键设计**：CaptureScreen 只接收屏幕编号，返回该屏幕的**完整截图**（每屏幕一张 Base64 JPEG）；OcrServer 收到完整截图后，在本地完成图像验证、按区域裁剪、OCR 识别、MQTT 推送的全流程。

---

## 一、CaptureScreen 客户端安装

### 1.1 编译

在已安装 Go 1.10.8 环境的机器上执行：

```powershell
cd CaptureScreen
$env:CGO_ENABLED=0
$env:GOOS="windows"
$env:GOARCH="386"
go build -ldflags="-s -w" -o CaptureScreen32.exe
```

编译产物 `CaptureScreen32.exe` 为单一 32 位可执行文件，可在 Windows XP/2000 上执行，无需任何依赖。

### 1.2 配置

编辑 `config.json`：

```json
{
  "server": {
    "ip": "0.0.0.0",
    "port": 2333
  },
  "jpeg_quality": 85,
  "log_level": "info"
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `server.ip` | 监听地址 | `0.0.0.0` |
| `server.port` | 监听端口 | `2333` |
| `jpeg_quality` | JPEG 压缩质量（1-100） | `85` |
| `log_level` | 日志级别（debug/info/warn/error） | `info` |

### 1.3 运行

```cmd
# 使用默认配置
CaptureScreen.exe

# 指定配置文件
CaptureScreen.exe config.json
```

启动时输出检测到的显示器信息：

```
CaptureScreen v1.0.0 starting...
Listening on: 0.0.0.0:2333
JPEG Quality: 85
Log Level: info
2 monitors detected:
  - Monitor 0: 1920x1080 at (0, 0)
  - Monitor 1: 1920x1080 at (1920, 0)
Server started, waiting for requests...
```

### 1.4 验证

```cmd
# 健康检查
curl http://localhost:2333/health
# 输出：{"status":"ok","version":"1.0.0"}

# 测试截图接口
curl -X POST http://localhost:2333/api/screenshot ^
  -H "Content-Type: application/json" ^
  -d "{\"screens\":[{\"screen_index\":0}]}"
# 返回：{"timestamp":...,"screens":[{"screen_index":0,"image":"BASE64..."}]}
```

---

## 二、OcrServer 服务端安装

### 2.1 编译

需要 .NET 10 SDK：

```powershell
# Debug 编译
dotnet build OcrServer/OcrServer.csproj -c Debug

# Release 编译
dotnet build OcrServer/OcrServer.csproj -c Release

# Native AOT 发布（需要 Visual Studio C++ 桌面开发工具）
$env:PATH = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer;' + $env:PATH
dotnet publish OcrServer/OcrServer.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

AOT 发布产物约 368 MB（包含 PaddleOCR 原生依赖）。

### 2.2 目录结构

部署后 `OcrServer/` 目录应包含：

```
OcrServer/
├── OcrServer.exe              # 主程序（或 publish/OcrServer.exe）
├── data/
│   ├── appsettings.json       # 运行配置（必填）
│   ├── CaptureSettings.DEVICE-001.json  # 设备001采集区域配置
│   ├── CaptureSettings.DEVICE-002.json  # 设备002采集区域配置（按需）
│   └── *.png                  # 图像验证模板图（与 CaptureSettings 中 fileName 对应）
└── publish/                   # AOT 发布目录（可选）
    └── OcrServer.exe
```

### 2.3 配置 appsettings.json

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
      "captureScreenUrl": "http://192.168.8.128:2333",
      "timeoutSeconds": 10,
      "captureFrequency": 3,
      "defaultExtendPayload": {
        "DEVICECODE": "110001"
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

**关键配置项说明**：

| 配置路径 | 说明 |
|---------|------|
| `mqttBroker.ip / port` | MQTT Broker 地址 |
| `mqttBroker.reconnect` | 断线重连策略（指数退避） |
| `devices[].captureScreenUrl` | CaptureScreen 服务地址 |
| `devices[].captureFrequency` | 采集频率（秒） |
| `devices[].topics[].name` | MQTT Topic，值须与 CaptureSettings 中 `topic` 字段对应 |
| `devices[].topics[].extendPayload` | 该 Topic 携带的额外字段 |

### 2.4 配置 CaptureSettings.{DeviceCode}.json

每台设备对应一个独立文件，文件名中的 `DeviceCode` 用于关联 `appsettings.json` 中的设备项。

```json
{
  "ocrEngine": "PaddleOCR",
  "verificationAreas": [
    {
      "screenNumber": 0,
      "topLeftX": 656,
      "topLeftY": 86,
      "width": 590,
      "height": 80,
      "fileName": "0_检测区域1.png",
      "matchThreshold": 0.95
    }
  ],
  "collectionAreas": [
    {
      "screenNumber": 0,
      "name": "温度",
      "topic": "screen/alarm/110001",
      "topLeftX": 434,
      "topLeftY": 433,
      "width": 91,
      "height": 52
    }
  ]
}
```

| 字段 | 说明 |
|------|------|
| `verificationAreas` | 图像验证区域（用于确认目标画面存在），可留空 |
| `verificationAreas[].fileName` | 模板图文件名，须放在 `data/` 目录下 |
| `verificationAreas[].matchThreshold` | 像素相似度阈值（0-1），默认 0.95 |
| `collectionAreas` | OCR 采集区域 |
| `collectionAreas[].name` | 区域名称（作为 MQTT Data 的键名） |
| `collectionAreas[].topic` | 对应 MQTT Topic（须在 appsettings 中存在） |

### 2.5 运行

```powershell
# Framework-Dependent 方式（需 .NET 运行时）
dotnet run --project OcrServer/OcrServer.csproj

# AOT 发布产物（无需 .NET 运行时）
OcrServer\publish\OcrServer.exe
```

启动日志示例：

```
[Information] MQTT 推送已启用，Broker: 192.168.1.100:1883
[Information] MQTT 连接成功，Broker: 192.168.1.100:1883
[Information] 设备 DEVICE-001 已加载，采集频率: 3s，区域数: 4
[Information] HTTP 服务已启动，监听 http://0.0.0.0:8081
[Information] 定时采集已启动
```

---

## 三、MQTT 消息格式

OcrServer 推送的 MQTT 消息格式（与 ScreenTextCollector 100% 兼容）：

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

## 四、HTTP 查询接口

| 接口 | 说明 |
|------|------|
| `GET /health` | 健康检查 |
| `GET /collect` | 所有设备所有区域的结果 |
| `GET /collect/{deviceCode}` | 指定设备的全部结果 |
| `GET /collect/{deviceCode}/{areaName}` | 指定设备指定区域的单个值 |

## 五、日志

日志同时输出到控制台和文件：

- 控制台：Info 及以上
- 文件（`logs/`）：Debug 及以上，滚动写入

## 六、多设备部署

新增设备时，在 `appsettings.json` 的 `devices` 数组中添加一项，并在 `data/` 目录放置对应的 `CaptureSettings.{DeviceCode}.json` 文件，无需重启服务。

## 七、注意事项

1. **CaptureScreen 部署位置**：应部署在目标机器上（直连显示器），与 OcrServer 通过 HTTP 通信
2. **MQTT 重连**：默认启用指数退避重连（3s → 6s → 12s → ...，封顶 60s），`maxRetries=0` 表示无限重试
3. **图像验证**：若 `verificationAreas` 为空，则跳过验证直接 OCR
4. **Topic 匹配**：`collectionAreas[].topic` 须在 `appsettings.json` 的设备 `topics` 列表中存在，否则使用 `Topics[0]` 作为默认值
5. **模板图**：图像验证用的模板图必须放在 `data/` 目录，与 `fileName` 字段对应
