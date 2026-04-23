using System;
using OcrServer.Serialization;
using Serilog;

namespace OcrServer.Services;

/// <summary>
/// ASP.NET Core Minimal API HTTP 服务
/// </summary>
public static class HttpApiService
{
    /// <summary>
    /// 注册 HTTP API 路由
    /// </summary>
    public static void MapRoutes(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<Serilog.ILogger>();
        var resultCache = app.Services.GetRequiredService<ResultCache>();

        // GET /health — 健康检查
        app.MapGet("/health", () =>
        {
            var response = new HealthResponse
            {
                Status = "ok",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return Results.Ok(response);
        });

        // GET /collect — 返回所有设备所有区域
        app.MapGet("/collect", () =>
        {
            var all = resultCache.GetAll();
            return Results.Ok(all);
        });

        // GET /collect/{deviceCode} — 返回指定设备所有区域
        app.MapGet("/collect/{deviceCode}", (string deviceCode) =>
        {
            var deviceData = resultCache.GetDevice(deviceCode);
            if (deviceData.Count == 0)
                return Results.NotFound($"Device '{deviceCode}' not found or no data collected yet.");

            return Results.Ok(deviceData);
        });

        // GET /collect/{deviceCode}/{areaName} — 返回指定设备指定区域
        app.MapGet("/collect/{deviceCode}/{areaName}", (string deviceCode, string areaName) =>
        {
            var value = resultCache.Get(deviceCode, areaName);
            if (string.IsNullOrEmpty(value))
                return Results.NotFound($"Area '{areaName}' not found for device '{deviceCode}'.");

            return Results.Ok(value);
        });
    }
}
