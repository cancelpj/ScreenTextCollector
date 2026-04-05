@echo off
chcp 65001 >nul
echo ===========================
echo 一键发布 LabelTool
echo ===========================

:: 使用 wmic 获取干净的日期时间格式
for /f "tokens=2 delims==" %%a in ('wmic os get localdatetime /value') do set "DT=%%a"

:: 检查 wmic 是否成功获取日期时间
if not defined DT (
    echo [错误] 无法获取系统日期时间，wmic 命令可能已被禁用或不支持。
    echo 请尝试以管理员权限运行，或使用 PowerShell 替代方案。
    pause
    exit /b 1
)

set YEAR=%DT:~0,4%
set MONTH=%DT:~4,2%
set DAY=%DT:~6,2%
set HOUR=%DT:~8,2%
set MINUTE=%DT:~10,2%
set SECOND=%DT:~12,2%

:: 格式化输出
set PUBLISH_DIR=%~dp0publish_%YEAR%.%MONTH%.%DAY%.%HOUR%%MINUTE%%SECOND%

:: 查找 MSBuild：优先使用 dotnet msbuild（跨版本），否则查找 VS 自带路径
where dotnet >nul 2>&1
if %errorlevel% equ 0 (
    set MSBUILD=dotnet msbuild
) else (
    set MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe
    if not exist "%MSBUILD%" (
        echo [错误] 未找到 dotnet，也未找到 MSBuild，请安装 Visual Studio 或 .NET SDK。
        pause
        exit /b 1
    )
)

:: 先恢复 NuGet 包
echo.
echo [1/3] 正在恢复 NuGet 包...
%MSBUILD% ScreenTextCollector.sln /t:Restore /v:minimal
if %errorlevel% neq 0 (
    echo [错误] NuGet 包恢复失败，请检查网络连接或项目配置。
    pause
    exit /b 1
)

:: 发布 LabelTool
echo.
echo [2/3] 正在发布 LabelTool...
if not exist "%PUBLISH_DIR%" mkdir "%PUBLISH_DIR%"
%MSBUILD% LabelTool\LabelTool.csproj /p:Configuration=Release /p:PublishDir=%PUBLISH_DIR%\ /t:Publish /v:minimal
if %errorlevel% neq 0 (
    echo [错误] 发布失败，请检查项目配置或依赖项。
    pause
    exit /b 1
)

:: 用 WinRAR 压缩发布目录
set ZIP_NAME=%~dp0ScreenTextCollector_%YEAR%.%MONTH%.%DAY%.%HOUR%%MINUTE%%SECOND%.zip
set WINRAR=%OneDrive%\_soft\效率\WinRAR\WinRAR.exe
if not exist "%WINRAR%" (
    echo [警告] 未找到 WinRAR，跳过压缩步骤。
) else (
    echo.
    echo [3/3] 正在压缩为 %ZIP_NAME%...
    "%WINRAR%" a -afzip -r -ep1 -m5 "%ZIP_NAME%" "%PUBLISH_DIR%\"
    if %errorlevel% equ 0 (
        echo 压缩完成！
    ) else (
        echo [错误] 压缩失败。
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo 发布完成！输出目录: %PUBLISH_DIR%
if exist "%ZIP_NAME%" echo 压缩包: %ZIP_NAME%
echo ========================================

:: 显示发布结果
dir /b "%PUBLISH_DIR%\*.exe" 2>nul
if %errorlevel% neq 0 (
    echo [警告] 未找到任何 .exe 文件。
)

echo.
pause
