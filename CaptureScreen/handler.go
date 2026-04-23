package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"time"
)

// ScreenRequest 截图请求结构（新版：只传屏幕编号）
type ScreenRequest struct {
	Screens []struct {
		ScreenIndex int `json:"screen_index"`
	} `json:"screens"`
}

// ScreenResponse 截图响应结构（新版：每屏幕只返回完整截图）
type ScreenResponse struct {
	Timestamp int64 `json:"timestamp"`
	Screens   []struct {
		ScreenIndex int    `json:"screen_index"`
		Image       string `json:"image"`
	} `json:"screens"`
}

// ErrorResponse 错误响应结构
type ErrorResponse struct {
	Error string `json:"error"`
}

// HealthHandler GET /health 健康检查
func HealthHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]string{
		"status":  "ok",
		"version": "1.0.0",
	})
}

// ScreenshotHandler POST /api/screenshot 截图处理器
// 新版：只根据屏幕编号截取完整屏幕，返回每屏幕一张图
func ScreenshotHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	// 解析请求体
	var req ScreenRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		w.WriteHeader(http.StatusBadRequest)
		json.NewEncoder(w).Encode(ErrorResponse{Error: "Invalid request format: " + err.Error()})
		return
	}

	if len(req.Screens) == 0 {
		w.WriteHeader(http.StatusBadRequest)
		json.NewEncoder(w).Encode(ErrorResponse{Error: "screens is required and must not be empty"})
		return
	}

	// 获取显示器列表（用于验证屏幕索引范围）
	monitors, err := GetMonitors()
	if err != nil {
		w.WriteHeader(http.StatusInternalServerError)
		json.NewEncoder(w).Encode(ErrorResponse{Error: "Failed to get monitors: " + err.Error()})
		return
	}

	// 构建响应
	var resp ScreenResponse
	resp.Timestamp = time.Now().UnixNano() / 1e6 // 毫秒时间戳

	// 处理每个屏幕（只截取整屏，不按区域分别截图）
	for _, screen := range req.Screens {
		// 验证屏幕索引
		if screen.ScreenIndex < 0 || screen.ScreenIndex >= len(monitors) {
			w.WriteHeader(http.StatusBadRequest)
			json.NewEncoder(w).Encode(ErrorResponse{
				Error: formatScreenIndexError(screen.ScreenIndex, len(monitors)),
			})
			return
		}

		// 获取该屏幕的完整尺寸
		monitor := monitors[screen.ScreenIndex]

		// 截取完整屏幕（传入相对坐标 0,0 和屏幕宽高）
		imageBase64, err := CaptureArea(
			screen.ScreenIndex,
			0,
			0,
			monitor.Width,
			monitor.Height,
		)

		if err != nil {
			log.Printf("Screenshot failed [screen=%d]: %v", screen.ScreenIndex, err)
			w.WriteHeader(http.StatusInternalServerError)
			json.NewEncoder(w).Encode(ErrorResponse{Error: "Screenshot failed: " + err.Error()})
			return
		}

		resp.Screens = append(resp.Screens, struct {
			ScreenIndex int    `json:"screen_index"`
			Image       string `json:"image"`
		}{
			ScreenIndex: screen.ScreenIndex,
			Image:       imageBase64,
		})

		log.Printf("Screenshot captured [screen=%d, size=%dx%d]", screen.ScreenIndex, monitor.Width, monitor.Height)
	}

	// 返回响应
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(resp)
}

// formatScreenIndexError 格式化屏幕索引越界错误消息
func formatScreenIndexError(index, total int) string {
	if total == 1 {
		return fmt.Sprintf("screen index %d out of range, available: 0", index)
	}
	return fmt.Sprintf("screen index %d out of range, available: 0-%d", index, total-1)
}
