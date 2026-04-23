# OcrServer.Tests

OcrServer 的单元/集成测试项目，使用 xunit.v3 测试框架。

## 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| 测试框架 | xunit.v3 3.2.2 | 最新版 xUnit |
| Mock | Moq 4.20.72 | 模拟接口行为 |
| 目标框架 | .NET 10.0 | 与 OcrServer 保持一致 |

## 项目结构

```
OcrServer.Tests/
├── OcrServer.Tests.csproj
├── Services/
│   ├── CaptureScreenClientTests.cs      # 截图客户端测试
│   ├── CollectServiceTests.cs           # 采集服务测试
│   ├── OcrServiceTests.cs              # OCR 服务测试
│   ├── ResultCacheTests.cs             # 结果缓存测试
│   └── MqttPushServiceTests.cs         # MQTT 推送测试
├── Configuration/
│   └── AppSettingsTests.cs             # 配置加载测试
└── TestData/                          # 测试图片
    ├── 纵拉机/
    │   ├── test_screenshot_1.png
    │   ├── test_screenshot_2.png
    │   └── 20260117115113709.png
    └── 拉弯机/
        ├── screenshot.png
        └── 20260113064214930.png
```

## 运行测试

```powershell
# 运行所有测试
dotnet test

# 运行所有测试（详细输出）
dotnet test -v n

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~CaptureScreenClientTests"

# 运行特定测试用例
dotnet test --filter "FullyQualifiedName~TestScreenshot_SingleArea"

# 生成测试报告（TRX 格式）
dotnet test --logger "trx;LogFileName=results.trx"
```

## 测试用例

### CaptureScreenClientTests

| 用例 | 验证点 |
|------|--------|
| `TestBuildRequest` | 正确构造 HTTP 请求体 |
| `TestBuildRequest_MultiScreen` | 多屏幕请求体构造 |
| `TestParseResponse_SingleArea` | 单区域响应解析 |
| `TestParseResponse_MultiArea` | 多区域响应解析 |
| `TestParseResponse_InvalidJson` | 非 JSON 响应处理 |
| `TestCapture_ErrorResponse` | 服务端返回错误时正确处理 |

### CollectServiceTests

| 用例 | 验证点 |
|------|--------|
| `TestCollect_MultiDevice` | 多设备并发采集，结果互不干扰 |
| `TestCollect_SingleDeviceFailure` | 单设备失败不影响其他设备 |
| `TestCollect_NoVerification` | 无图像校验时正常采集 |

### OcrServiceTests

| 用例 | 验证点 |
|------|--------|
| `TestOcr_EmptyImage` | 空图片返回空字符串 |
| `TestOcr_VerifyImage_TemplateExists` | 模板存在时校验逻辑正确 |
| `TestOcr_VerifyImage_TemplateNotExists` | 模板不存在时返回 false |
| `TestOcr_PerformOcr` | OCR 识别非空文本 |
| `TestOcr_PerformOcr_MultiArea` | 多区域 OCR |
| `TestOcr_PerformOcr_SmallArea` | 小区域 OCR |

### ResultCacheTests

| 用例 | 验证点 |
|------|--------|
| `TestGet_Set` | 基本存取值 |
| `TestGet_NotExists` | 键不存在时返回空 |
| `TestGetAll_Empty` | 空缓存返回空字典 |
| `TestGetAll_SingleDevice` | 单设备所有区域 |
| `TestGetAll_MultiDevice` | 多设备隔离 |
| `TestConcurrent_Set` | 并发写入线程安全 |
| `TestDeviceIsolation` | DEVICE-001 和 DEVICE-002 的同名区域互不覆盖 |

### MqttPushServiceTests

| 用例 | 验证点 |
|------|--------|
| `TestBuildPayload_SingleArea` | 单区域 Payload 构造 |
| `TestBuildPayload_MultiArea` | 多区域合并到同一 Payload |
| `TestBuildPayload_DeviceCode` | Payload 包含 DeviceCode |
| `TestGroupByTopic` | 按 Topic 分组正确 |
| `TestGroupByTopic_MultiDevice` | 多设备 Topic 分组 |

### AppSettingsTests

| 用例 | 验证点 |
|------|--------|
| `TestDefaultValues` | 默认值正确 |
| `TestDevices_Loaded` | 设备列表加载 |
| `TestDevices_Url` | 设备 URL 正确 |
| `TestDevices_CaptureFrequency` | 采集频率配置正确 |
| `TestDevices_Topics` | Topic 列表正确 |
| `TestDevices_DefaultExtendPayload` | 设备级 Payload 正确 |

## 测试运行

OcrServer 和 OcrServer.Tests 均为 .NET 10 项目，测试直接引用真实服务实现。

```powershell
dotnet build && dotnet test
```

## 测试图片说明

测试使用两组真实采集图片：

- **纵拉机**：`test_screenshot_1.png`, `test_screenshot_2.png`, `20260117115113709.png`
- **拉弯机**：`screenshot.png`, `20260113064214930.png`

这些图片来自 ScreenTextCollector 项目的 `TestImages/` 目录。
