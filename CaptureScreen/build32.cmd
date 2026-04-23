@echo off
set CGO_ENABLED=0
set GOOS=windows
set GOARCH=386
go build -ldflags="-s -w" -o CaptureScreen32.exe
if %ERRORLEVEL% EQU 0 (
    if exist publish_old rd publish_old /s /q
    if exist publish ren publish publish_old
    mkdir publish
    move /Y CaptureScreen32.exe publish >nul
    copy /Y config.json publish >nul
    echo Build successful.
    dir /B publish
) else (
    echo Build failed!
    exit /b 1
)