using OcrServer.Configuration;
using OcrServer.Serialization;
using OcrServer.Services;
using Serilog;
using System.Text.Json;

namespace OcrServer;

public class Program
{
    public static int Main(string[] args)
    {
        // 从 data/appsettings.json 加载配置
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);

        var appSettingsPath = Path.Combine(dataDir, "appsettings.json");
        AppSettings appSettings;

        if (File.Exists(appSettingsPath))
        {
            string json = File.ReadAllText(appSettingsPath);
            appSettings = JsonSerializer.Deserialize(json, JsonContext.Default.AppSettings) ?? new AppSettings();
        }
        else
        {
            Console.Error.WriteLine("配置文件不存在: " + appSettingsPath);
            Console.Error.WriteLine("请复制 data/appsettings.json 示例文件并修改配置。");
            return 1;
        }

        var loggerConfig = appSettings.Serilog.CreateLogger(dataDir);
        Log.Logger = loggerConfig.CreateLogger();

        try
        {
            Log.Information("OcrServer 启动中...");
            Log.Information("加载设备数: {DeviceCount}", appSettings.Devices.Count);

            foreach (var device in appSettings.Devices)
            {
                Log.Information("  设备: {Code} -> {Url}", device.DeviceCode, device.CaptureScreenUrl);
            }

            // 构建 WebApplicationBuilder
            var builder = WebApplication.CreateBuilder(args);

            // 注册 Serilog
            builder.Services.AddSerilog(Log.Logger);

            // 注册 AppSettings（单例）
            builder.Services.AddSingleton(appSettings);

            // 注册 ResultCache（单例）
            builder.Services.AddSingleton<ResultCache>();

            // 注册 MqttPushService（后台服务）
            builder.Services.AddSingleton<MqttPushService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttPushService>());

            // 注册 CollectService（后台服务）
            builder.Services.AddSingleton<CollectService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<CollectService>());

            // HTTP 服务
            if (appSettings.Http.EnableHttp)
            {
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(appSettings.Http.Port);
                });
            }

            var app = builder.Build();

            // 注册 HTTP API 路由
            HttpApiService.MapRoutes(app);

            Log.Information("HTTP 服务监听: {Ip}:{Port}", appSettings.Http.Ip, appSettings.Http.Port);
            Log.Information("OcrServer 启动完成");

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "OcrServer 启动失败");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
