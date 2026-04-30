using OcrServer.Configuration;
using OcrServer.Serialization;
using OcrServer.Utilities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ILogger = Serilog.ILogger;

namespace OcrServer.Services;

/// <summary>
/// 定时采集主循环：按 CaptureFrequency 并发轮询所有设备，
/// 执行完整屏幕截图 → 图像验证 → OCR → 结果缓存
/// </summary>
public sealed class CollectService : IHostedService, IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly ResultCache _resultCache;
    private readonly MqttPushService _mqttPushService;
    private readonly ILogger _logger;
    private readonly string _dataDir;

    // 每台设备一个独立的 HttpClient（CaptureScreenClient）
    private readonly ConcurrentDictionary<string, CaptureScreenClient> _clients = new();
    private readonly OcrService _ocrService;
    private CancellationTokenSource? _cts;
    private Task? _mainLoop;

    // 设备代码 -> CaptureSettings 映射（从各设备的 JSON 文件加载）
    private readonly ConcurrentDictionary<string, CaptureSettings> _deviceSettings = new();

    // CSV 文件锁（细粒度，按日期文件锁而不是目录锁）
    private static readonly ConcurrentDictionary<string, object> _csvFileLocks = new(StringComparer.OrdinalIgnoreCase);

    public CollectService(
        AppSettings appSettings,
        ResultCache resultCache,
        MqttPushService mqttPushService,
        ILogger logger,
        string dataDir = "data")
    {
        _appSettings = appSettings;
        _resultCache = resultCache;
        _mqttPushService = mqttPushService;
        _logger = logger;
        _dataDir = dataDir;

        _ocrService = new OcrService(logger, dataDir);

        // 为每个设备创建 CaptureScreenClient 并加载对应的 CaptureSettings
        foreach (var device in appSettings.Devices)
        {
            _clients[device.DeviceCode] = new CaptureScreenClient(device, logger);
            LoadDeviceSettings(device.DeviceCode);
        }
    }

    /// <summary>
    /// 从 data/CaptureSettings.{DeviceCode}.json 加载采集配置
    /// </summary>
    private void LoadDeviceSettings(string deviceCode)
    {
        var settingsPath = Path.Combine(_dataDir, $"CaptureSettings.{deviceCode}.json");
        if (!File.Exists(settingsPath))
        {
            _logger.Warning("设备 {DeviceCode} 配置文件不存在: {Path}", deviceCode, settingsPath);
            return;
        }

        try
        {
            string json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize(json, JsonContext.Default.CaptureSettings);
            _deviceSettings[deviceCode] = settings;
            _logger.Information("设备 {DeviceCode} 加载配置成功，采集区域: {AreaCount}",
                deviceCode, settings?.CollectionAreas?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "设备 {DeviceCode} 配置加载失败: {Path}", deviceCode, settingsPath);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _mainLoop = Task.Run(() => MainLoop(_cts.Token), _cts.Token);
        _logger.Information("采集服务已启动，共 {DeviceCount} 台设备", _appSettings.Devices.Count);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("采集服务正在停止...");
        _cts?.Cancel();
        if (_mainLoop != null)
            await Task.WhenAny(_mainLoop, Task.Delay(Timeout.Infinite, cancellationToken));
        _logger.Information("采集服务已停止");
    }

    /// <summary>
    /// 定时采集主循环：所有设备并发采集，单设备失败不影响其他
    /// </summary>
    private async Task MainLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var deviceTasks = new List<Task>();

                foreach (var device in _appSettings.Devices)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    deviceTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await CollectDeviceAsync(device, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "设备 {DeviceCode} 采集异常", device.DeviceCode);
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(deviceTasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "采集主循环异常");
            }

            // 按各设备独立频率休眠（取最小频率作为主循环周期）
            int minFrequency = _appSettings.Devices.Count > 0
                ? _appSettings.Devices.Min(d => d.CaptureFrequency)
                : 5;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(minFrequency), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 采集单台设备：
    /// 1. 获取完整屏幕截图
    /// 2. 图像验证（确认目标画面存在）
    /// 3. 多区域并发 OCR
    /// 4. 缓存 + CSV + MQTT 推送
    /// </summary>
    private async Task CollectDeviceAsync(DeviceConfig device, CancellationToken cancellationToken)
    {
        var deviceCode = device.DeviceCode;
        if (!_deviceSettings.TryGetValue(deviceCode, out var captureSettings) ||
            captureSettings.CollectionAreas == null ||
            captureSettings.CollectionAreas.Count == 0)
        {
            _logger.Debug("设备 {DeviceCode} 无采集区域配置", deviceCode);
            return;
        }

        // 步骤 1：获取完整屏幕截图（按 ScreenNumber 去重）
        var screenNumbers = captureSettings.CollectionAreas
            .Select(a => a.ScreenNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        Dictionary<int, string> fullScreens;
        try
        {
            if (!_clients.TryGetValue(deviceCode, out var client))
            {
                _logger.Error("设备 {DeviceCode} 的 HTTP 客户端未找到", deviceCode);
                return;
            }

            fullScreens = await client.CaptureFullScreenAsync(screenNumbers, cancellationToken);
            _logger.Debug("设备 {DeviceCode} 获取完整屏幕截图成功，屏幕数: {Count}", deviceCode, fullScreens.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(NetworkExceptionHelper.GetFriendlyMessage(ex, $"设备 {deviceCode}"));
            return;
        }

        // 步骤 2：图像验证
        if (captureSettings.VerificationAreas != null && captureSettings.VerificationAreas.Count > 0)
        {
            // 图像验证只使用第一个屏幕（通常所有验证区域都在 Screen 0）
            int verifyScreen = captureSettings.VerificationAreas[0].ScreenNumber;
            if (fullScreens.TryGetValue(verifyScreen, out var fullImage))
            {
                if (!_ocrService.VerifyImage(fullImage, captureSettings.VerificationAreas))
                {
                    _logger.Warning("设备 {DeviceCode} 图像验证未通过，跳过本次采集", deviceCode);
                    return;
                }
            }
            else
            {
                _logger.Warning("设备 {DeviceCode} 验证屏幕 {Screen} 无截图，跳过本次采集", deviceCode, verifyScreen);
                return;
            }
        }

        // 步骤 3：OCR 识别（多区域并发，从完整截图中裁剪）
        var ocrResults = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 按屏幕分组，每个屏幕一个 OCR 任务
        var screenGroups = captureSettings.CollectionAreas
            .GroupBy(a => a.ScreenNumber);

        var ocrTasks = screenGroups.Select(async group =>
        {
            if (!fullScreens.TryGetValue(group.Key, out var fullImage))
            {
                _logger.Warning("设备 {DeviceCode} 屏幕 {Screen} 无截图，跳过 OCR", deviceCode, group.Key);
                return;
            }

            foreach (var area in group)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    string text = _ocrService.PerformOcr(fullImage, area);
                    ocrResults[area.Name] = text;
                    _logger.Debug("设备 {DeviceCode} 区域 {AreaName} OCR结果: {Text}", deviceCode, area.Name, text);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "设备 {DeviceCode} 区域 {AreaName} OCR失败", deviceCode, area.Name);
                }
            }
        }).ToList();

        await Task.WhenAll(ocrTasks);

        if (ocrResults.IsEmpty)
            return;

        // 步骤 4：结果推送
        _resultCache.SetBatch(deviceCode, new Dictionary<string, string>(ocrResults));

        if (_appSettings.CsvRecord)
            SaveToCsv(deviceCode, ocrResults);

        if (_appSettings.MqttBroker.EnableMqttPush)
        {
            await PushToMqttAsync(device, captureSettings, ocrResults, cancellationToken);
        }

        _logger.Information("设备 {DeviceCode} 采集完成，结果: {Results}",
            deviceCode, JsonSerializer.Serialize(new Dictionary<string, string>(ocrResults), JsonContext.Default.DictionaryStringString));
    }

    /// <summary>
    /// 按 Topic 分组并发推送 MQTT 消息
    /// </summary>
    private async Task PushToMqttAsync(
        DeviceConfig device,
        CaptureSettings captureSettings,
        ConcurrentDictionary<string, string> ocrResults,
        CancellationToken cancellationToken)
    {
        var topicGroups = captureSettings.CollectionAreas
            .Where(a => ocrResults.ContainsKey(a.Name))
            .GroupBy(a => a.Topic);

        var publishTasks = topicGroups.Select(async group =>
        {
            string topicName = group.Key;

            var payload = new MqttPayload();
            payload.Root["TIMESTAMP"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            foreach (var area in group)
            {
                payload.Root[area.Name] = ocrResults[area.Name];
            }

            if (device.DefaultExtendPayload != null)
            {
                foreach (var kvp in device.DefaultExtendPayload)
                {
                    payload.Root[kvp.Key] = kvp.Value;
                }
            }

            var topicConfig = device.Topics?.FirstOrDefault(t => t.Name == topicName);
            if (topicConfig?.ExtendPayload != null)
            {
                foreach (var kvp in topicConfig.ExtendPayload)
                {
                    payload.Root[kvp.Key] = kvp.Value;
                }
            }

            await _mqttPushService.PublishAsync(topicName, payload, cancellationToken);
        }).ToList();

        await Task.WhenAll(publishTasks);
    }

    /// <summary>
    /// 保存采集结果到 CSV 文件
    /// </summary>
    private static void SaveToCsv(string deviceCode, IDictionary<string, string> results)
    {
        var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        if (!Directory.Exists(saveDir))
            Directory.CreateDirectory(saveDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var csvPath = Path.Combine(saveDir, $"{DateTime.Now:yyyyMMdd}.csv");

        var lines = new List<string>();
        foreach (var item in results)
        {
            lines.Add($"{timestamp},{deviceCode},{item.Key},{item.Value}");
        }

        if (lines.Count == 0)
            return;

        // 使用细粒度文件锁而非目录锁
        lock (_csvFileLocks.GetOrAdd(csvPath, _ => new object()))
        {
            File.AppendAllLines(csvPath, lines, Encoding.UTF8);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _ocrService.Dispose();
        foreach (var client in _clients.Values)
            client.Dispose();
    }
}
