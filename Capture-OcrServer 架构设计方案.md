# ScreenTextCollector C/S 架构设计方案

## 目标

将现有的单机版 ScreenTextCollector 改造为 C/S 架构：
- **服务端 CaptureScreen**：部署在 Windows XP/2000 32位老旧系统，仅负责截取完整屏幕截图（被动响应）
- **客户端 OcrServer**：跨平台运行，主动调用 CaptureScreen 获取完整屏幕截图，执行图像验证和 OCR，以 MQTT/HTTP 方式对外提供结果

**关键设计**：CaptureScreen 只接收屏幕编号，返回该屏幕的**完整截图**（每屏幕一张 Base64 JPEG）；OcrServer 收到完整截图后，在本地完成图像验证、按区域裁剪、OCR 识别、MQTT 推送的全流程。

---

## 一、技术选型

| 项目 | 技术 | 说明 |
|------|------|------|
| CaptureScreen | Go 1.10.8，静态编译 | 最后支持 Windows XP 的 Go 版本；零第三方依赖，用标准库 `syscall` 调用 Win32 API，配置文件改为 JSON |
| OcrServer | ASP.NET Core 10，Native AOT | 跨平台，启动快，内存占用低 |
| MQTT 库 | MQTTnet 5.1 | 支持 .NET 10，AOT 兼容 |
| JSON 序列化 | System.Text.Json + Source Generator | AOT 兼容，替换原 Newtonsoft.Json |
| 日志 | Serilog.Sinks.File | 滚动文件日志 |
| 测试 | xunit.v3 | OcrServer 单元/集成测试 |

---

## 二、架构概览

**部署模型：多台 CaptureScreen 对应 1 台 OcrServer**

```
┌──────────────────────────┐
│  CaptureScreen (Go)      │  DeviceCode: DEVICE-001
│  192.168.1.50:8080       │  Windows XP/2000 32位
│  只返回完整屏幕截图       │  每屏幕一张 Base64 JPEG
└──────────┬───────────────┘
           │ 返回完整屏幕截图（Base64 JPEG）
           ▼
┌──────────────────────────┐   MQTT / HTTP     ┌──────────────────────────┐
│   OcrServer (.NET 10)    │ ───────────────▶ │   调用者 / MQTT Broker    │
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

**OcrServer 内部处理流程（收到完整屏幕截图后）：**

```
完整屏幕截图（Base64 JPEG）
        │
        ▼
┌───────────────────┐
│  1. 图像验证       │ ← 对照模板图，用像素级相似度比较
│     VerifyImage() │   验证目标画面是否存在
└────────┬──────────┘
         │ 通过
         ▼
┌───────────────────┐
│  2. 按区域裁剪     │ ← 从完整截图中提取各采集区域
│     Bitmap.Clone()│
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│  3. OCR 识别      │ ← PaddleOCRSharp 逐区域识别
│     PerformOcr()  │   多区域并发处理
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│  4. 结果推送      │ ← MQTT 推送 + ResultCache 缓存
│     PushToMqtt()  │   按 Topic 分组并发发布
└───────────────────┘
```

OcrServer 维护一个设备列表，每个设备有独立的 `DeviceCode`、IP 地址和采集区域配置。定时采集时并发轮询所有设备，OCR 结果按 DeviceCode 隔离，MQTT 推送时 `ExtendPayload` 携带对应设备的 `DeviceCode`。

---

## 三、服务端 CaptureScreen（Go）

### 职责

纯粹的截图服务，**不持有任何采集配置**，仅根据屏幕编号截取**该屏幕的完整图像**并返回。**不实现图像验证和 OCR 逻辑**。

### 项目结构

```
CaptureScreen/
├── main.go          # 程序入口，启动 HTTP Server
├── config.go        # JSON 配置加载（标准库 encoding/json）
├── screenshot.go    # Win32 API 截图核心（标准库 syscall）
└── handler.go       # HTTP 路由处理
```

> 无 go.mod（Go 1.10 不支持 modules），零第三方依赖，直接 `go build` 编译。

### 配置文件 config.json

```json
{
  "server": {
    "ip": "0.0.0.0",
    "port": 8080
  },
  "jpeg_quality": 85,
  "log_level": "info"
}
```

> 无采集频率、屏幕坐标、OCR Endpoint 等配置，这些全部由 OcrServer 管理。

### HTTP 接口规范

#### `GET /health` — 健康检查

**响应**：
```json
{ "status": "ok", "version": "1.0.0" }
```

#### `POST /api/screenshot` — 截取完整屏幕截图

OcrServer 定时调用，传入屏幕编号列表，返回每块屏幕的**完整截图**（不是按区域分别截图）。

**请求体**（新版，简化版）：
```json
{
  "screens": [
    { "screen_index": 0 },
    { "screen_index": 1 }
  ]
}
```

> 不再需要传入区域坐标，仅传屏幕编号。截取整屏是为了支持 OcrServer 在本地做图像验证（验证区域可能覆盖多个采集区域，需要完整画面）。

**响应**（成功，HTTP 200）：
```json
{
  "timestamp": 1712345678900,
  "screens": [
    {
      "screen_index": 0,
      "image": "BASE64_ENCODED_FULL_SCREEN_JPEG..."
    },
    {
      "screen_index": 1,
      "image": "BASE64_ENCODED_FULL_SCREEN_JPEG..."
    }
  ]
}
```

**响应**（屏幕编号越界，HTTP 400）：
```json
{ "error": "screen index 2 out of range, available: 0-1" }
```

**编译命令**（PowerShell）：
```powershell
$env:CGO_ENABLED=0; $env:GOOS="windows"; $env:GOARCH="386"; go build -ldflags="-s -w" -o CaptureScreen.exe
```

---

## 四、客户端 OcrServer（ASP.NET Core 10 Native AOT）

### 项目结构

```
OcrServer/
├── OcrServer.csproj              # .NET 10, PublishAot=false（目前）
├── Program.cs                    # 启动入口，DI 注册，ASP.NET Core 路由
├── Configuration/
│   ├── AppSettings.cs            # appsettings.json 结构体（含 Devices 列表）
│   ├── DeviceConfig.cs           # 单台设备配置（DeviceCode、URL、Topic 等）
│   └── CaptureSettings.cs        # CaptureSettings.{DeviceCode}.json 结构体
├── Services/
│   ├── CaptureScreenClient.cs    # HTTP 客户端，调用 CaptureScreen /api/screenshot
│   ├── OcrService.cs             # OCR 处理（PaddleOCRSharp）+ 图像验证 + 区域裁剪
│   ├── CollectService.cs         # 定时采集主循环（并发轮询所有设备）
│   ├── ResultCache.cs            # 线程安全的 OCR 结果缓存（按 DeviceCode 隔离）
│   ├── MqttPushService.cs        # MQTTnet 5.1 推送
│   └── HttpApiService.cs         # ASP.NET Core 路由（/health, /collect）
├── Serialization/
│   └── JsonContext.cs            # System.Text.Json Source Generator 上下文
├── data/
│   ├── appsettings.json                    # 运行配置（含 Devices 列表）
│   ├── CaptureSettings.DEVICE-001.json     # 设备 001 采集区域配置
│   ├── CaptureSettings.DEVICE-002.json     # 设备 002 采集区域配置
│   └── *.png                              # 图像验证模板图
└── output/                                 # CSV 输出目录
```

### OcrServer 内部流程详解

#### 步骤 1：获取完整屏幕截图

```csharp
// CaptureScreenClient.cs
// 请求格式：{"screens":[{"screen_index":0}]}
// 响应格式：{"timestamp":...,"screens":[{"screen_index":0,"image":"BASE64..."}]}
public async Task<Dictionary<int, string>> CaptureFullScreenAsync(
    List<int> screenIndices,
    CancellationToken cancellationToken)
```

返回 `screenIndex -> Base64` 的字典，OcrServer 据此决定使用哪张截图进行验证和裁剪。

#### 步骤 2：图像验证（OcrService.VerifyImage）

参照 `PluginInterface/Tool.cs` 中 `CaptureAndVerify` 的逻辑，在 OCR 之前先验证目标画面是否存在：

```csharp
// OcrService.cs
public bool VerifyImage(byte[] fullScreenBytes, List<ImageVerificationArea> verificationAreas)
// 1. 从 fullScreenBytes 加载完整截图 Bitmap
// 2. 对每个 verificationArea：
//    - 加载模板图（Path.Combine(_dataDir, area.FileName)）
//    - 在截图的对应区域（area.TopLeftX, area.TopLeftY, area.Width, area.Height）裁剪 ROI
//    - 调用 CalculateSimilarity(roiImage, template) 计算像素级相似度
//    - 相似度 < 阈值则返回 false
// 3. 全部通过返回 true
```

- 模板图文件放在 `data/` 目录下（如 `0_检测区域1.png`）
- 验证在 OCR 之前执行，验证不通过则跳过本次采集

#### 步骤 3：按区域裁剪 + OCR 识别（OcrService.PerformOcr）

```csharp
// OcrService.cs
public string PerformOcr(byte[] fullScreenBytes, ImageCollectionArea area)
// 1. 从 fullScreenBytes 加载完整截图 Bitmap
// 2. 在截图的 area.TopLeftX/Y/Width/Height 区域裁剪 ROI
// 3. PaddleOCRSharp.DetectText(roiImage) 识别文字
// 4. PostProcessText() 后处理（移除 OCR 常见错误字符）
```

多区域并发处理：
```csharp
var ocrTasks = captureSettings.CollectionAreas
    .Select(async area => await Task.Run(() => _ocrService.PerformOcr(fullScreenBytes, area)))
    .ToList();
await Task.WhenAll(ocrTasks);
```

#### 步骤 4：结果推送

与原方案一致，按 Topic 分组并发发布 MQTT。

### 配置文件一：data/appsettings.json

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
          "extendPayload": {
            "GroupCode": "collection1"
          }
        },
        {
          "name": "screen/alarm/110001",
          "extendPayload": {
            "GroupCode": "alarm1"
          }
        }
      ]
    },
    {
      "deviceCode": "DEVICE-002",
      "captureScreenUrl": "http://192.168.1.51:8080",
      "timeoutSeconds": 10,
      "captureFrequency": 5,
      "defaultExtendPayload": {
        "DEVICECODE": "110002",
        "GroupCode": "collection1"
      },
      "topics": [
        {
          "name": "screen/collection/110002",
          "extendPayload": {
            "GroupCode": "collection1"
          }
        },
        {
          "name": "screen/alarm/110002",
          "extendPayload": {
            "GroupCode": "alarm1"
          }
        }
      ]
    }
  ]
}
```

> **Topic 查找规则**：推送时按 `CollectionArea.Topic` 在设备的 `Topics` 列表中查找匹配项，合并 `DefaultExtendPayload` 和 `Topic.ExtendPayload`（后者优先级更高）后发布。若找不到匹配项，则使用 `Topics[0]` 作为默认值。

### 配置文件二：data/CaptureSettings.{DeviceCode}.json

每台设备对应一个独立的配置文件，命名规则为 `CaptureSettings.{DeviceCode}.json`。

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
    },
    {
      "screenNumber": 0,
      "name": "湿度",
      "topic": "screen/collection/110001",
      "topLeftX": 851,
      "topLeftY": 434,
      "width": 61,
      "height": 51
    },
    {
      "screenNumber": 0,
      "name": "噪声",
      "topic": "screen/collection/110001",
      "topLeftX": 1260,
      "topLeftY": 402,
      "width": 81,
      "height": 51
    },
    {
      "screenNumber": 0,
      "name": "气压",
      "topic": "screen/alarm/110001",
      "topLeftX": 424,
      "topLeftY": 754,
      "width": 93,
      "height": 49
    }
  ]
}
```

> `verificationAreas` 用于图像验证（OcrServer 侧实现），`collectionAreas` 用于 OCR 识别和 MQTT 推送。`CollectionArea.Topic` 为**必填**，值必须与 `appsettings.json` 中对应设备的 `Topics[n].name` 一致。

### 对外 HTTP 接口

| 接口 | 响应 | 说明 |
|------|------|------|
| `GET /health` | `{"status":"ok","timestamp":...}` | 健康检查 |
| `GET /collect` | `{"DEVICE-001":{"温度":"25.3","湿度":"60"},"DEVICE-002":{...}}` | 所有设备所有区域 |
| `GET /collect/{deviceCode}` | `{"温度":"25.3","湿度":"60"}` | 指定设备所有区域 |
| `GET /collect/{deviceCode}/{areaName}` | `"25.3"` | 指定设备单区域 |

### MQTT 消息规范（与 ScreenTextCollector 100% 兼容）

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

---

## 五、验证方法

### CaptureScreen 编译与测试

```powershell
# 编译（32位静态）
$env:CGO_ENABLED=0; $env:GOOS="windows"; $env:GOARCH="386"; go build -ldflags="-s -w" -o CaptureScreen32.exe

# 集成测试（服务必须运行在 http://192.168.8.128:2333）
go test -v -run TestHealth
go test -v -run TestScreenshot
```

### OcrServer 编译与测试

```powershell
# 编译
dotnet build OcrServer/OcrServer.csproj -c Debug

# 单元测试
dotnet test OcrServer.Tests/OcrServer.Tests.csproj

# Native AOT 发布
$env:PATH = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer;' + $env:PATH
dotnet publish OcrServer/OcrServer.csproj -c Release -r win-x64 --self-contained true -o ./publish
```
