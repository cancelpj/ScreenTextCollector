# 测试项目创建总结

## 已完成的工作

### 1. 创建测试项目结构
- ✅ 创建了 `ScreenTextCollector.Tests` 项目目录
- ✅ 创建了 SDK 风格的 `.csproj` 文件（.NET Framework 4.8）
- ✅ **升级到 xUnit v3**（v2.0.0）- 支持 .NET Framework 4.7+
- ✅ 配置 OutputType 为 Exe（xUnit v3 要求）
- ✅ 将测试项目添加到解决方案文件中

### 2. 创建测试类文件
- ✅ `MethodResultTests.cs` - 测试 MethodResult 类的所有构造函数和属性（5个测试）
- ✅ `SettingsTests.cs` - 测试 Settings 配置类及相关配置模型（6个测试）
- ✅ `CheckProcessTests.cs` - 测试 FunctionCall.CheckProcess 进程检查方法（4个测试）
- ✅ `OcrServiceTests.cs` - OCR 服务测试（5个测试，标记为 Skip，需要测试图片）

### 3. 配置项目引用
- ✅ 添加了对 PluginInterface 项目的引用
- ✅ 添加了对 ScreenTextCollector.OpenCvSharp 项目的引用
- ✅ 添加了对 ScreenTextCollector 项目的引用
- ✅ 在 ScreenTextCollector.csproj 中添加了 `InternalsVisibleTo` 属性，允许测试项目访问 internal 成员

## xUnit v3 特性

### 与 v2 的主要区别
1. **OutputType 要求**：测试项目必须设置 `<OutputType>Exe</OutputType>`
2. **包名变化**：使用 `xunit.v3` 替代 `xunit`
3. **API 兼容**：大部分 v2 的测试代码可以直接在 v3 中运行
4. **性能提升**：v3 提供了更好的性能和并行执行能力

### 当前配置
```xml
<PackageReference Include="xunit.v3" Version="2.0.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
```

## 当前问题

### NuGet 包还原问题
PluginInterface 项目使用旧式 .csproj 格式混合 PackageReference，导致 NuGet 包（NLog、Newtonsoft.Json）在使用 `dotnet CLI` 时无法正确还原。

**错误信息**：
```
error CS0246: The type or namespace name 'NLog' could not be found
error CS0246: The type or namespace name 'Newtonsoft' could not be found
```

**原因**：旧式 .csproj 格式（ToolsVersion="15.0"）与 dotnet CLI 的包还原机制存在兼容性问题。

## 推荐使用方式

### ✅ 方案 1：使用 Visual Studio（强烈推荐）
1. 在 Visual Studio 中打开 `ScreenTextCollector.sln`
2. Visual Studio 会自动还原 NuGet 包
3. 构建解决方案（Ctrl+Shift+B）
4. 打开测试资源管理器（测试 → 测试资源管理器）
5. 运行所有测试

**优势**：
- 自动处理旧式项目格式的 NuGet 还原
- 提供图形化测试运行界面
- 支持调试测试用例
- 显示测试覆盖率

### 方案 2：使用 MSBuild（命令行）
```bash
# 还原 NuGet 包
msbuild ScreenTextCollector.sln /t:Restore

# 构建解决方案
msbuild ScreenTextCollector.sln /p:Configuration=Debug

# 运行测试（需要安装 xUnit 控制台运行器）
dotnet test ScreenTextCollector.Tests/ScreenTextCollector.Tests.csproj
```

### 方案 3：转换为 SDK 风格项目（长期方案）
将 PluginInterface 项目转换为 SDK 风格的 .csproj 格式，这样可以更好地与 dotnet CLI 集成。

## 测试用例说明

### MethodResultTests（5个测试）
- `Constructor_Default_ShouldSetSuccessType` - 测试默认构造函数
- `Constructor_WithMessage_ShouldSetWarningType` - 测试带消息的构造函数
- `Constructor_WithMessageAndType_ShouldSetSpecifiedType` - 测试带消息和类型的构造函数
- `Constructor_WithMessageAndException_ShouldSetErrorType` - 测试带异常的构造函数
- `Properties_CanBeSetDirectly` - 测试属性可以直接设置

### SettingsTests（6个测试）
- `Settings_DefaultValues_ShouldBeCorrect` - 测试默认值
- `Settings_Properties_CanBeSet` - 测试属性设置
- `HttpConfig_DefaultValues_ShouldBeCorrect` - 测试 HTTP 配置默认值
- `MqttBrokerConfig_DefaultValues_ShouldBeCorrect` - 测试 MQTT 配置默认值
- `ImageVerificationArea_Properties_CanBeSet` - 测试图像验证区域
- `ImageCollectionArea_Properties_CanBeSet` - 测试图像采集区域

### CheckProcessTests（4个测试）
- `CheckProcess_WithRunningProcess_ShouldReturnRunning` - 测试运行中的进程
- `CheckProcess_WithNonExistentProcess_ShouldReturnStandby` - 测试不存在的进程
- `CheckProcess_WithEmptyString_ShouldReturnStandby` - 测试空字符串
- `CheckProcess_WithSystemProcess_ShouldReturnRunning` - 测试系统进程

### OcrServiceTests（5个测试，全部标记为 Skip）
这些测试需要准备测试图片文件和 Tesseract OCR 数据才能运行。

## 项目文件位置

```
ScreenTextCollector.Tests/
├── ScreenTextCollector.Tests.csproj    # 项目文件（xUnit v3）
├── MethodResultTests.cs                # MethodResult 测试
├── SettingsTests.cs                    # Settings 测试
├── CheckProcessTests.cs                # CheckProcess 测试
├── OcrServiceTests.cs                  # OcrService 测试
└── README.md                           # 本文档
```

## 运行测试（Visual Studio）

1. **打开测试资源管理器**
   - 菜单：测试 → 测试资源管理器
   - 快捷键：Ctrl+E, T

2. **运行测试**
   - 运行所有测试：点击"运行所有测试"按钮
   - 运行单个测试：右键点击测试 → 运行
   - 调试测试：右键点击测试 → 调试

3. **查看结果**
   - 绿色勾：测试通过
   - 红色叉：测试失败
   - 黄色感叹号：测试跳过（Skip）

## 注意事项

1. **OcrService 测试**：需要准备测试图片和 Tesseract 数据文件才能取消 Skip 标记
2. **CheckProcess 测试**：某些测试依赖系统进程（如 explorer），在不同环境下可能有不同结果
3. **内部成员访问**：已通过 `InternalsVisibleTo` 配置，测试可以访问 Program.CheckProcess 方法
4. **xUnit v3**：测试项目必须是可执行文件（OutputType=Exe），这是 xUnit v3 的要求

## TDD 开发流程

1. **编写测试**：先编写测试用例，定义期望的行为
2. **运行测试**：确认测试失败（红灯）
3. **实现功能**：编写最少的代码使测试通过
4. **运行测试**：确认测试通过（绿灯）
5. **重构代码**：优化代码结构，保持测试通过
6. **重复循环**：继续下一个功能的开发
