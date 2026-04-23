# CaptureScreen 截图服务

纯 Go 实现的 Windows 截图服务，支持多屏幕、多区域批量截图。

## 编译

版本号在编译时通过 `-ldflags="-X main.version=YYYY.MM.DD.HHmm"` 注入，格式为"年月日时分"（如 `2026.04.23.1430`）。

### 标准编译（64位）
Bash 示例：
```bash
VER=$(date -d "now" "+%Y.%m.%d.%H%M")  # Linux/macOS
go build -ldflags="-s -w -X main.version=$VER" -o CaptureScreen64.exe
```
PowerShell 示例：
```powershell
$VER = Get-Date -Format "yyyy.MM.dd.HHmm"; go build -ldflags="-s -w -X main.version=$VER" -o CaptureScreen64.exe
```

### 静态编译（32位，适用于 Windows XP/2000）
直接运行 `build32.cmd`，脚本自动生成版本号并注入。

## 配置

编辑 `config.json`：

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

## 运行

```bash
# 使用默认配置文件 config.json
CaptureScreen.exe

# 指定配置文件
CaptureScreen.exe custom-config.json
```

## API 接口

### GET /health
健康检查

**响应**：
```json
{
  "status": "ok",
  "version": "2026.04.23.1430"
}
```

### POST /api/screenshot
批量截图

**请求体**：
```json
{
  "screens": [
    {
      "screen_index": 0,
      "areas": [
        { "name": "V001", "x": 60, "y": 205, "width": 44, "height": 11 },
        { "name": "V002", "x": 111, "y": 205, "width": 44, "height": 11 }
      ]
    }
  ]
}
```

**响应**（成功）：
```json
{
  "timestamp": 1712345678900,
  "screens": [
    {
      "screen_index": 0,
      "areas": [
        { "name": "V001", "image": "BASE64_ENCODED_JPEG..." },
        { "name": "V002", "image": "BASE64_ENCODED_JPEG..." }
      ]
    }
  ]
}
```

**响应**（屏幕索引越界）：
```json
{
  "error": "screen index 2 out of range, available: 0-1"
}
```

## 测试

### 前置条件

CaptureScreen 服务必须运行在目标地址（默认 `http://192.168.8.128:2333`）。

```powershell
# 启动服务（配置文件 config.json 需指定对应端口）
CaptureScreen.exe
```

### 运行测试

```powershell
# 运行所有测试
go test -v

# 只运行健康检查测试
go test -v -run TestHealth

# 只运行截图接口测试
go test -v -run TestScreenshot

# 只运行单个测试用例
go test -v -run TestScreenshot_SingleArea
```

### 测试用例说明

| 用例 | 说明 |
|------|------|
| `TestHealth_GET` | GET /health 返回 `{"status":"ok","version":"YYYY.MM.DD.HHmm"}` |
| `TestHealth_POST_MethodNotAllowed` | POST /health 返回 405 |
| `TestScreenshot_SingleArea` | 单屏幕单区域截图，返回有效 Base64 JPEG |
| `TestScreenshot_MultiArea` | 单屏幕多区域截图，各区域均返回截图 |
| `TestScreenshot_MultiScreen` | 多屏幕截图（若只有一块屏，screen_index=1 返回 400 为预期行为） |
| `TestScreenshot_InvalidJson` | 非 JSON 请求返回 400 |
| `TestScreenshot_InvalidScreenIndex` | 越界 screen_index 返回 400，错误消息包含 "out of range" |
| `TestScreenshot_NegativeScreenIndex` | 负数 screen_index 返回 400 |
| `TestScreenshot_Base64IsJpeg` | 验证返回的 Base64 可解码为有效 JPEG 文件（头字节 FF D8 FF） |
| `TestScreenshot_ResponseTimestamp` | 验证返回的 timestamp 为毫秒级 Unix 时间戳且合理 |
| `TestScreenshot_MethodNotAllowed_GET` | GET /api/screenshot 返回 405 |
| `TestScreenshot_SmallArea` | 1x1 像素极小区域也能正常截图 |
| `TestScreenshot_AreaOutsideBounds` | 超大区域（超出屏幕边界）返回 500 |
| `TestScreenshot_EmptyAreas` | 空区域列表返回 200，areas 为空数组 |

### 测试报告示例

```
=== RUN   TestHealth_GET
--- PASS: TestHealth_GET (0.01s)
=== RUN   TestScreenshot_SingleArea
--- PASS: TestScreenshot_SingleArea (0.05s)
...
PASS
ok      main     0.823s
```

## 依赖

- Go 1.10.8（最后支持 Windows XP 的版本）
- 零第三方依赖（全部使用标准库）

## 特性

- ✅ 零运行时依赖（静态编译）
- ✅ 支持多屏幕
- ✅ 支持多区域批量截图
- ✅ JPEG 压缩
- ✅ Base64 编码
- ✅ 屏幕索引越界检测
