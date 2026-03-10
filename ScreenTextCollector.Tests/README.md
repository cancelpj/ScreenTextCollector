# 测试项目创建总结

## 已完成的工作

### 1. 创建测试项目结构
- ✅ 创建了 `ScreenTextCollector.Tests` 项目目录
- ✅ 创建了 SDK 风格的 `.csproj` 文件（.NET Framework 4.8）
- ✅ **升级到 xUnit v3**（v3.2.2）- 支持 .NET Framework 4.7+
- ✅ 配置 OutputType 为 Exe（xUnit v3 要求）
- ✅ 将测试项目添加到解决方案文件中
- ✅ 复制测试数据到输出目录

### 2. 创建测试类文件
- ✅ `MethodResultTests.cs` - 测试 MethodResult 类的所有构造函数和属性（5个测试）
- ✅ `SettingsTests.cs` - 测试 Settings 配置类及相关配置模型（6个测试）
- ✅ `CheckProcessTests.cs` - 测试进程检查功能（5个测试）
- ✅ `OcrServiceTests.cs` - OCR 服务测试，使用真实测试数据（97个测试）

### 3. 配置项目引用
- ✅ 添加了对 PluginInterface 项目的引用
- ✅ 添加了对 ScreenTextCollector.OpenCvSharp 项目的引用

## 测试数据

测试使用 `TestImages` 目录下的真实数据：

```
TestImages/
├── 纵拉机/
│   ├── test_screenshot_1.png    # 测试截图1
│   ├── test_screenshot_2.png    # 测试截图2
│   ├── data/
│   │   └── 20260117115113709.png  # 验证模板
│   └── appsettings.json         # 49个采集区域（V001-V049）
└── 拉弯机/
    ├── test_screenshot.png     # 测试截图
    ├── data/
    │   └── 20260113064214930.png  # 验证模板
    └── appsettings.json         # 32个采集区域（V001-V032）
```

## 推荐使用方式

### 命令行构建和运行
```bash
# 还原 NuGet 包
dotnet restore ScreenTextCollector.Tests/ScreenTextCollector.Tests.csproj

# 构建测试项目
dotnet build ScreenTextCollector.Tests/ScreenTextCollector.Tests.csproj -c Debug

# 运行测试
cd ScreenTextCollector.Tests/bin/Debug/net48
./ScreenTextCollector.Tests.exe
```

### Visual Studio
1. 打开 `ScreenTextCollector.sln`
2. 构建解决方案（Ctrl+Shift+B）
3. 打开测试资源管理器（测试 → 测试资源管理器）
4. 运行所有测试

## 测试用例说明

### MethodResultTests（5个测试）
- `Constructor_Default_ShouldSetSuccessType` - 测试默认构造函数设置 Success 类型
- `Constructor_WithMessage_ShouldSetWarningType` - 测试带消息的构造函数设置 Warning 类型
- `Constructor_WithMessageAndType_ShouldSetSpecifiedType` - 测试带消息和类型的构造函数
- `Constructor_WithMessageAndException_ShouldSetErrorType` - 测试带异常的构造函数设置 Error 类型
- `Properties_CanBeSetDirectly` - 测试属性可以直接设置

### SettingsTests（6个测试）
- `Settings_DefaultValues_ShouldBeCorrect` - 测试 Settings 默认值
- `Settings_Properties_CanBeSet` - 测试 Settings 属性设置
- `HttpConfig_DefaultValues_ShouldBeCorrect` - 测试 HttpConfig 默认值
- `MqttBrokerConfig_DefaultValues_ShouldBeCorrect` - 测试 MqttBrokerConfig 默认值
- `ImageVerificationArea_Properties_CanBeSet` - 测试图像验证区域属性
- `ImageCollectionArea_Properties_CanBeSet` - 测试图像采集区域属性

### CheckProcessTests（5个测试）
- `GetCurrentProcess_ShouldReturnValidProcessName` - 测试获取当前进程名称
- `GetProcessesByName_WithRunningProcess_ShouldReturnAtLeastOne` - 测试查找运行中的进程
- `GetProcessesByName_WithNonExistentProcess_ShouldReturnEmpty` - 测试查找不存在的进程
- `GetProcessesByName_WithEmptyString_ShouldReturnEmpty` - 测试空字符串进程名
- `GetProcessesByName_ShouldBeCaseInsensitive` - 测试进程名不区分大小写

### OcrServiceTests（97个测试）
使用真实测试数据验证 OCR 功能：

**基础测试（4个）**
- `OcrService_ImplementsIOcrService` - 验证实现 IOcrService 接口
- `VerifyImage_Test_ShouldReturnTrue` - 图像验证测试（3个参数化测试）
- `VerifyImage_NotExistTemplate_ShouldReturnFalse` - 测试不存在的模板
- `PerformOcr_OutOfRangeArea_ShouldHandleGracefully` - 测试超范围区域

**纵拉机 OCR 测试（59个参数化测试）**
- test_screenshot_1.png: V001-V049（49个）
- test_screenshot_2.png: V001-V010（10个）

**拉弯机 OCR 测试（32个参数化测试）**
- test_screenshot.png: V001-V032

## 测试执行结果

```
=== TEST EXECUTION SUMMARY ===
   ScreenTextCollector.Tests
   Total: 113, Errors: 0, Failed: 0, Skipped: 0, Time: 7.424s
```

## 注意事项

1. **测试数据**：测试数据会自动复制到输出目录的 `TestData` 和 `data` 文件夹
2. **OCR 依赖**：OCR 测试需要 Tesseract 训练数据（eng.traineddata），已包含在输出目录
3. **进程测试**：某些测试依赖系统进程，在不同环境下可能有不同结果
4. **参数化测试**：使用 `[Theory]` + `[InlineData]` 实现高效的批量测试

## TDD 开发流程

1. **编写测试**：先编写测试用例，定义期望的行为
2. **运行测试**：确认测试失败（红灯）
3. **实现功能**：编写最少的代码使测试通过
4. **运行测试**：确认测试通过（绿灯）
5. **重构代码**：优化代码结构，保持测试通过
6. **重复循环**：继续下一个功能的开发
