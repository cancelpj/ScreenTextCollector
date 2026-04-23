package main

import (
	"bytes"
	"encoding/base64"
	"fmt"
	"image"
	"image/jpeg"
	"syscall"
	"unsafe"
)

// Win32 API 声明
var (
	modGdi32  = syscall.NewLazyDLL("gdi32.dll")
	modUser32 = syscall.NewLazyDLL("user32.dll")

	procGetDC             = modUser32.NewProc("GetDC")
	procReleaseDC         = modUser32.NewProc("ReleaseDC")
	procEnumDisplayMonitors = modUser32.NewProc("EnumDisplayMonitors")
	procGetMonitorInfoW   = modUser32.NewProc("GetMonitorInfoW")

	procCreateCompatibleDC    = modGdi32.NewProc("CreateCompatibleDC")
	procCreateCompatibleBitmap = modGdi32.NewProc("CreateCompatibleBitmap")
	procSelectObject     = modGdi32.NewProc("SelectObject")
	procBitBlt           = modGdi32.NewProc("BitBlt")
	procDeleteObject     = modGdi32.NewProc("DeleteObject")
	procDeleteDC         = modGdi32.NewProc("DeleteDC")
	procGetDIBits        = modGdi32.NewProc("GetDIBits")
)

const (
	SRCCOPY     = 0x00CC0020 // BitBlt rop code
	BI_RGB      = 0
	DIB_RGB_COLORS = 0
)

// MONITORINFO 结构体（Windows API）
type MONITORINFO struct {
	CbSize    uint32
	RcMonitor RECT
	RcWork    RECT
	DwFlags   uint32
}

// RECT 结构体（Windows API）
type RECT struct {
	Left   int32
	Top    int32
	Right  int32
	Bottom int32
}

// BITMAPINFOHEADER 结构体（Windows API）
type BITMAPINFOHEADER struct {
	BiSize          uint32
	BiWidth         int32
	BiHeight        int32
	BiPlanes        uint16
	BiBitCount      uint16
	BiCompression   uint32
	BiSizeImage     uint32
	BiXPelsPerMeter int32
	BiYPelsPerMeter int32
	BiClrUsed       uint32
	BiClrImportant  uint32
}

// BITMAPINFO 结构体（Windows API）
type BITMAPINFO struct {
	BmiHeader BITMAPINFOHEADER
	BmiColors [1]uint32
}

// MonitorInfo 屏幕信息
type MonitorInfo struct {
	Index  int
	Left   int
	Top    int
	Width  int
	Height int
}

// 枚举显示器回调（包级别变量，避免闭包捕获问题）
var monitors []MonitorInfo

// monitorEnumProc 枚举显示器回调函数（syscall.NewCallback 需要普通函数）
func monitorEnumProc(hMonitor syscall.Handle, hdcMonitor syscall.Handle, lprcMonitor *RECT, dwData uintptr) uintptr {
	var mi MONITORINFO
	mi.CbSize = uint32(unsafe.Sizeof(mi))

	ret, _, _ := procGetMonitorInfoW.Call(
		uintptr(hMonitor),
		uintptr(unsafe.Pointer(&mi)),
	)

	if ret != 0 {
		monitor := MonitorInfo{
			Index:  len(monitors),
			Left:   int(mi.RcMonitor.Left),
			Top:    int(mi.RcMonitor.Top),
			Width:  int(mi.RcMonitor.Right - mi.RcMonitor.Left),
			Height: int(mi.RcMonitor.Bottom - mi.RcMonitor.Top),
		}
		monitors = append(monitors, monitor)
	}

	return 1 // 继续枚举
}

// GetMonitors 获取所有显示器信息
func GetMonitors() ([]MonitorInfo, error) {
	monitors = make([]MonitorInfo, 0)

	ret, _, err := procEnumDisplayMonitors.Call(
		0,
		0,
		syscall.NewCallback(monitorEnumProc),
		0,
	)

	if ret == 0 {
		return nil, fmt.Errorf("failed to enumerate monitors: %v", err)
	}

	if len(monitors) == 0 {
		return nil, fmt.Errorf("no monitors detected")
	}

	return monitors, nil
}

// CaptureArea 截取指定屏幕的指定区域，返回 Base64 JPEG 字符串
func CaptureArea(screenIndex, x, y, width, height int) (string, error) {
	// 获取显示器列表
	monitors, err := GetMonitors()
	if err != nil {
		return "", err
	}

	// 检查屏幕索引
	if screenIndex < 0 || screenIndex >= len(monitors) {
		return "", fmt.Errorf("screen index %d out of range, available: 0-%d", screenIndex, len(monitors)-1)
	}

	monitor := monitors[screenIndex]

	// 计算绝对坐标（相对坐标 + 显示器偏移）
	absX := monitor.Left + x
	absY := monitor.Top + y

	// 获取桌面 DC
	hdcScreen, _, _ := procGetDC.Call(0)
	if hdcScreen == 0 {
		return "", fmt.Errorf("failed to get desktop DC")
	}
	defer procReleaseDC.Call(0, hdcScreen)

	// 创建兼容 DC
	hdcMem, _, _ := procCreateCompatibleDC.Call(hdcScreen)
	if hdcMem == 0 {
		return "", fmt.Errorf("failed to create compatible DC")
	}
	defer procDeleteDC.Call(hdcMem)

	// 创建兼容位图
	hBitmap, _, _ := procCreateCompatibleBitmap.Call(hdcScreen, uintptr(width), uintptr(height))
	if hBitmap == 0 {
		return "", fmt.Errorf("failed to create compatible bitmap")
	}
	defer procDeleteObject.Call(hBitmap)

	// 选择位图到 DC
	procSelectObject.Call(hdcMem, hBitmap)

	// BitBlt 复制屏幕内容到内存 DC
	ret, _, _ := procBitBlt.Call(
		hdcMem,
		0, 0,
		uintptr(width), uintptr(height),
		hdcScreen,
		uintptr(absX), uintptr(absY),
		SRCCOPY,
	)
	if ret == 0 {
		return "", fmt.Errorf("BitBlt failed")
	}

	// 准备 BITMAPINFO（获取 DIB 数据）
	var bi BITMAPINFO
	bi.BmiHeader.BiSize = uint32(unsafe.Sizeof(bi.BmiHeader))
	bi.BmiHeader.BiWidth = int32(width)
	bi.BmiHeader.BiHeight = -int32(height) // 负值 = 自顶向下 bitmap
	bi.BmiHeader.BiPlanes = 1
	bi.BmiHeader.BiBitCount = 24
	bi.BmiHeader.BiCompression = BI_RGB

	// 计算行字节数（4 字节对齐）
	stride := ((width*3 + 3) / 4) * 4
	bits := make([]byte, stride*height)

	// 获取位图原始数据（BGR 格式）
	ret, _, _ = procGetDIBits.Call(
		hdcMem, hBitmap,
		0, uintptr(height),
		uintptr(unsafe.Pointer(&bits[0])),
		uintptr(unsafe.Pointer(&bi)),
		DIB_RGB_COLORS,
	)
	if ret == 0 {
		return "", fmt.Errorf("failed to get DIB bits")
	}

	// BGR -> RGBA 转换（image/jpeg 需要 RGBA）
	img := image.NewRGBA(image.Rect(0, 0, width, height))
	for row := 0; row < height; row++ {
		for col := 0; col < width; col++ {
			srcIdx := row*stride + col*3
			dstIdx := row*img.Stride + col*4
			img.Pix[dstIdx+0] = bits[srcIdx+2] // R
			img.Pix[dstIdx+1] = bits[srcIdx+1] // G
			img.Pix[dstIdx+2] = bits[srcIdx+0] // B
			img.Pix[dstIdx+3] = 255            // A
		}
	}

	// JPEG 编码
	var buf bytes.Buffer
	err = jpeg.Encode(&buf, img, &jpeg.Options{Quality: config.JpegQuality})
	if err != nil {
		return "", fmt.Errorf("failed to encode JPEG: %v", err)
	}

	return base64.StdEncoding.EncodeToString(buf.Bytes()), nil
}
