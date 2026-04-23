package main

import (
	"encoding/json"
	"io/ioutil"
	"log"
)

// Config 配置结构体（JSON 格式）
type Config struct {
	Server struct {
		IP   string `json:"ip"`
		Port int    `json:"port"`
	} `json:"server"`
	JpegQuality int    `json:"jpeg_quality"`
	LogLevel    string `json:"log_level"`
}

var config Config

// LoadConfig 加载 JSON 配置文件
func LoadConfig(path string) error {
	data, err := ioutil.ReadFile(path)
	if err != nil {
		return err
	}

	err = json.Unmarshal(data, &config)
	if err != nil {
		return err
	}

	// 设置默认值
	if config.Server.IP == "" {
		config.Server.IP = "0.0.0.0"
	}
	if config.Server.Port == 0 {
		config.Server.Port = 8080
	}
	if config.JpegQuality == 0 {
		config.JpegQuality = 85
	}
	if config.LogLevel == "" {
		config.LogLevel = "info"
	}

	log.Printf("Config loaded: IP=%s, Port=%d, JpegQuality=%d, LogLevel=%s",
		config.Server.IP, config.Server.Port, config.JpegQuality, config.LogLevel)

	return nil
}
