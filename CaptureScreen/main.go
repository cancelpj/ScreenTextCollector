package main

import (
	"fmt"
	"log"
	"net/http"
	"os"
)

var version = "1.0.0"

func main() {
	// 设置日志格式
	log.SetFlags(log.Ldate | log.Ltime | log.Lshortfile)

	// 加载配置（默认 config.json）
	configPath := "config.json"
	if len(os.Args) > 1 {
		configPath = os.Args[1]
	}

	if err := LoadConfig(configPath); err != nil {
		log.Printf("Warning: Failed to load config (%s), using default: %v", configPath, err)
		config.Server.IP = "0.0.0.0"
		config.Server.Port = 8080
		config.JpegQuality = 85
		config.LogLevel = "info"
	}

	// 注册路由
	http.HandleFunc("/health", HealthHandler)
	http.HandleFunc("/api/screenshot", ScreenshotHandler)

	// 启动服务器
	addr := fmt.Sprintf("%s:%d", config.Server.IP, config.Server.Port)
	log.Printf("CaptureScreen v%s starting...", version)
	log.Printf("Listening on: %s", addr)
	log.Printf("JPEG Quality: %d", config.JpegQuality)
	log.Printf("Log Level: %s", config.LogLevel)

	// 检测显示器
	monitors, err := GetMonitors()
	if err != nil {
		log.Printf("Warning: Failed to get monitors: %v", err)
	} else {
		log.Printf("%d monitors detected:", len(monitors))
		for _, m := range monitors {
			log.Printf("  - Monitor %d: %dx%d at (%d, %d)",
				m.Index, m.Width, m.Height, m.Left, m.Top)
		}
	}

	log.Printf("Server started, waiting for requests...")
	if err := http.ListenAndServe(addr, nil); err != nil {
		log.Fatalf("Failed to start server: %v", err)
	}
}
