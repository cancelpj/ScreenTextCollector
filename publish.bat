@echo off
chcp 65001 >nul
echo ========================================
echo 一键发布 ScreenTextCollector 和 LabelTool
echo ========================================

:: 使用 wmic 获取干净的日期时间格式
for /f "tokens=2 delims==" %%a in ('wmic os get localdatetime /value') do set "DT=%%a"
set YEAR=%DT:~0,4%
set MONTH=%DT:~4,2%
set DAY=%DT:~6,2%
set HOUR=%DT:~8,2%
set MINUTE=%DT:~10,2%
set SECOND=%DT:~12,2%

:: 去除前导零（避免 09 变成 9 后又变回 09 的问题）
set /a MONTH=1%MONTH%-100
set /a DAY=1%DAY%-100
set /a HOUR=1%HOUR%-100

:: 格式化输出
set PUBLISH_DIR=%~dp0publish_%YEAR%.%MONTH%.%DAY%.%HOUR%%MINUTE%%SECOND%
set MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe

:: 创建发布目录
if not exist "%PUBLISH_DIR%" mkdir "%PUBLISH_DIR%"

:: 先恢复 NuGet 包
echo.
echo [0/2] 正在恢复 NuGet 包...
"%MSBUILD_PATH%" ScreenTextCollector.sln /t:Restore /v:minimal

:: 先发布 LabelTool（它依赖 PluginInterface）
echo.
echo [1/2] 正在发布 LabelTool...
"%MSBUILD_PATH%" LabelTool\LabelTool.csproj /p:Configuration=Release /p:PublishDir=%PUBLISH_DIR%\ /t:Publish /v:minimal

:: 再发布 ScreenTextCollector（它依赖 PluginInterface、OpenCvSharp、PaddleOCR）
echo.
echo [2/2] 正在发布 ScreenTextCollector...
"%MSBUILD_PATH%" ScreenTextCollector\ScreenTextCollector.csproj /p:Configuration=Release /p:PublishDir=%PUBLISH_DIR%\ /t:Publish /v:minimal

echo.
echo ========================================
echo 发布完成！输出目录: %PUBLISH_DIR%
echo ========================================

:: 显示发布结果
dir /b "%PUBLISH_DIR%\*.exe" 2>nul

pause