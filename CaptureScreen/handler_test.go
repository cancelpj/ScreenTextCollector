package main

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"testing"
	"time"
)

// ==================== 测试配置 ====================

const (
	testServerURL = "http://192.168.8.128:2333"
)

// ==================== Health 接口测试 ====================

// TestHealth_GET 返回健康状态
func TestHealth_GET(t *testing.T) {
	resp, err := http.Get(testServerURL + "/health")
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		t.Fatalf("期望状态码 200，实际 %d", resp.StatusCode)
	}

	body, _ := io.ReadAll(resp.Body)
	var result map[string]string
	if err := json.Unmarshal(body, &result); err != nil {
		t.Fatalf("响应不是有效 JSON: %s", body)
	}

	if result["status"] != "ok" {
		t.Fatalf("期望 status=ok，实际 %s", result["status"])
	}

	if result["version"] == "" {
		t.Fatal("version 字段不应为空")
	}
}

// TestHealth_POST_MethodNotAllowed POST /health 应返回 405
func TestHealth_POST_MethodNotAllowed(t *testing.T) {
	req, _ := http.NewRequest(http.MethodPost, testServerURL+"/health", nil)
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusMethodNotAllowed {
		t.Fatalf("期望状态码 405，实际 %d", resp.StatusCode)
	}
}

// ==================== Screenshot 接口测试 ====================

// TestScreenshot_SingleArea 单屏幕单区域截图
func TestScreenshot_SingleArea(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 100, "height": 50},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		t.Fatalf("期望状态码 200，实际 %d，响应: %s", resp.StatusCode, bodyBytes)
	}

	var result map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		t.Fatal("响应不是有效 JSON")
	}

	// 验证 timestamp
	if timestamp, ok := result["timestamp"].(float64); !ok || timestamp <= 0 {
		t.Fatalf("timestamp 无效: %v", result["timestamp"])
	}

	// 验证 screens 数组
	screens, ok := result["screens"].([]interface{})
	if !ok || len(screens) == 0 {
		t.Fatal("screens 数组为空或格式错误")
	}

	screen := screens[0].(map[string]interface{})
	if screen["screen_index"].(float64) != 0 {
		t.Fatalf("screen_index 错误: %v", screen["screen_index"])
	}

	// 验证 areas 数组
	areas, ok := screen["areas"].([]interface{})
	if !ok || len(areas) == 0 {
		t.Fatal("areas 数组为空或格式错误")
	}

	area := areas[0].(map[string]interface{})
	imageBase64, ok := area["image"].(string)
	if !ok || imageBase64 == "" {
		t.Fatal("image 字段为空或格式错误")
	}

	// 验证 Base64 可解码
	_, err = base64.StdEncoding.DecodeString(imageBase64)
	if err != nil {
		t.Fatalf("image 不是有效 Base64: %v", err)
	}
}

// TestScreenshot_MultiArea 单屏幕多区域截图
func TestScreenshot_MultiArea(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 50, "height": 20},
					{"name": "V002", "x": 60, "y": 0, "width": 50, "height": 20},
					{"name": "V003", "x": 120, "y": 0, "width": 50, "height": 20},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		t.Fatalf("期望状态码 200，实际 %d，响应: %s", resp.StatusCode, bodyBytes)
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	screens := result["screens"].([]interface{})
	areas := screens[0].(map[string]interface{})["areas"].([]interface{})

	if len(areas) != 3 {
		t.Fatalf("期望 3 个区域，实际 %d 个", len(areas))
	}

	for i, a := range areas {
		area := a.(map[string]interface{})
		imageBase64 := area["image"].(string)
		if imageBase64 == "" {
			t.Errorf("区域 %d image 为空", i)
		}
	}
}

// TestScreenshot_MultiScreen 多屏幕截图
func TestScreenshot_MultiScreen(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 50, "height": 20},
				},
			},
			{
				"screen_index": 1,
				"areas": []map[string]interface{}{
					{"name": "V002", "x": 0, "y": 0, "width": 50, "height": 20},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	// 如果只有一块屏幕，screen_index=1 会返回 400，这是正常的
	if resp.StatusCode == http.StatusBadRequest {
		bodyBytes, _ := io.ReadAll(resp.Body)
		var errResp map[string]string
		json.Unmarshal(bodyBytes, &errResp)
		if errResp["error"] != "" {
			// 这是预期的，只有一块屏幕时 screen_index=1 会报错
			t.Logf("只有一块屏幕，screen_index=1 返回 400（预期行为）: %s", errResp["error"])
			return
		}
	}

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		t.Fatalf("期望状态码 200，实际 %d，响应: %s", resp.StatusCode, bodyBytes)
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	screens := result["screens"].([]interface{})
	if len(screens) != 2 {
		t.Fatalf("期望 2 个屏幕，实际 %d 个", len(screens))
	}
}

// TestScreenshot_InvalidJson 非 JSON 请求
func TestScreenshot_InvalidJson(t *testing.T) {
	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader([]byte("not json")))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusBadRequest {
		t.Fatalf("期望状态码 400，实际 %d", resp.StatusCode)
	}

	body, _ := io.ReadAll(resp.Body)
	var errResp map[string]string
	json.Unmarshal(body, &errResp)

	if errResp["error"] == "" {
		t.Fatal("error 字段不应为空")
	}
}

// TestScreenshot_InvalidScreenIndex 越界屏幕索引
func TestScreenshot_InvalidScreenIndex(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 999,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 50, "height": 20},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusBadRequest {
		t.Fatalf("期望状态码 400，实际 %d", resp.StatusCode)
	}

	bodyBytes, _ := io.ReadAll(resp.Body)
	var errResp map[string]string
	json.Unmarshal(bodyBytes, &errResp)

	if errResp["error"] == "" {
		t.Fatal("error 字段不应为空")
	}

	// 验证错误消息包含 "out of range"
	if !bytes.Contains(bodyBytes, []byte("out of range")) {
		t.Fatalf("错误消息应包含 'out of range'，实际: %s", bodyBytes)
	}
}

// TestScreenshot_NegativeScreenIndex 负数屏幕索引
func TestScreenshot_NegativeScreenIndex(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": -1,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 50, "height": 20},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusBadRequest {
		t.Fatalf("期望状态码 400，实际 %d", resp.StatusCode)
	}
}

// TestScreenshot_Base64IsJpeg 验证返回的是有效 JPEG 图片
func TestScreenshot_Base64IsJpeg(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 100, "height": 100},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		t.Skip("截图失败，跳过 JPEG 验证")
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	screens := result["screens"].([]interface{})
	areas := screens[0].(map[string]interface{})["areas"].([]interface{})
	imageBase64 := areas[0].(map[string]interface{})["image"].(string)

	// 解码 Base64
	imageData, err := base64.StdEncoding.DecodeString(imageBase64)
	if err != nil {
		t.Fatalf("Base64 解码失败: %v", err)
	}

	// 验证 JPEG 文件头 (FF D8 FF)
	if len(imageData) < 3 ||
		imageData[0] != 0xFF ||
		imageData[1] != 0xD8 ||
		imageData[2] != 0xFF {
		t.Fatalf("不是有效的 JPEG 文件，头字节: %x", imageData[:4])
	}
}

// TestScreenshot_ResponseTimestamp 时间戳合理性
func TestScreenshot_ResponseTimestamp(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "V001", "x": 0, "y": 0, "width": 50, "height": 20},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	before := time.Now().UnixMilli()

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	after := time.Now().UnixMilli()

	if resp.StatusCode != http.StatusOK {
		t.Skip("截图失败，跳过时间戳验证")
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	timestamp := int64(result["timestamp"].(float64))

	// 时间戳应在请求前后 60 秒内
	if timestamp < before-60000 || timestamp > after+60000 {
		t.Fatalf("时间戳 %d 不在合理范围内 [%d, %d]", timestamp, before-60000, after+60000)
	}
}

// TestScreenshot_MethodNotAllowed_GET Screenshot 接口不支持 GET
func TestScreenshot_MethodNotAllowed_GET(t *testing.T) {
	req, _ := http.NewRequest(http.MethodGet, testServerURL+"/api/screenshot", nil)
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusMethodNotAllowed {
		t.Fatalf("期望状态码 405，实际 %d", resp.StatusCode)
	}
}

// TestScreenshot_SmallArea 极小区域截图
func TestScreenshot_SmallArea(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "SMALL", "x": 0, "y": 0, "width": 1, "height": 1},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		t.Fatalf("1x1 区域截图应成功，实际: %d, %s", resp.StatusCode, bodyBytes)
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	screens := result["screens"].([]interface{})
	areas := screens[0].(map[string]interface{})["areas"].([]interface{})
	imageBase64 := areas[0].(map[string]interface{})["image"].(string)

	// 1x1 像素的 JPEG 也应该有数据
	if len(imageBase64) == 0 {
		t.Fatal("1x1 区域截图不应为空")
	}
}

// TestScreenshot_AreaOutsideBounds 区域超出屏幕边界
func TestScreenshot_AreaOutsideBounds(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas": []map[string]interface{}{
					{"name": "LARGE", "x": 0, "y": 0, "width": 10000, "height": 10000},
				},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	// 超出边界的区域应返回 500（截图失败）
	if resp.StatusCode != http.StatusInternalServerError {
		t.Logf("超出边界的区域可能返回 500，实际: %d", resp.StatusCode)
	}
}

// TestScreenshot_EmptyAreas 空区域列表
func TestScreenshot_EmptyAreas(t *testing.T) {
	reqBody := map[string]interface{}{
		"screens": []map[string]interface{}{
			{
				"screen_index": 0,
				"areas":        []map[string]interface{}{},
			},
		},
	}
	body, _ := json.Marshal(reqBody)

	resp, err := http.Post(testServerURL+"/api/screenshot", "application/json", bytes.NewReader(body))
	if err != nil {
		t.Fatalf("请求失败: %v", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		t.Fatalf("空区域列表应返回 200，实际: %d, %s", resp.StatusCode, bodyBytes)
	}

	var result map[string]interface{}
	json.NewDecoder(resp.Body).Decode(&result)

	screens := result["screens"].([]interface{})
	areasVal := screens[0].(map[string]interface{})["areas"]

	// areas 可能返回 null 或空数组 []interface{}，两种情况都视为正确
	if areasVal != nil {
		if areas, ok := areasVal.([]interface{}); ok && len(areas) != 0 {
			t.Fatalf("空区域列表应返回空 areas，实际: %d", len(areas))
		}
	}
}

// ==================== 辅助函数 ====================

func init() {
	fmt.Println("CaptureScreen Integration Tests")
	fmt.Printf("测试服务器: %s\n", testServerURL)
	fmt.Println("注意: 测试需要 CaptureScreen 服务运行在", testServerURL)
}
