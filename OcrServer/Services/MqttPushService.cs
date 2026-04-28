using MQTTnet;
using OcrServer.Configuration;
using OcrServer.Serialization;
using System.Text;
using ILogger = Serilog.ILogger;

namespace OcrServer.Services;

/// <summary>
/// MQTTnet 推送服务，按 Topic 分组发布采集结果
/// </summary>
public sealed class MqttPushService : IHostedService, IDisposable
{
    private readonly MqttBrokerConfig _config;
    private readonly ILogger _logger;
    private readonly IMqttClient _mqttClient;
    private CancellationTokenSource? _cts;
    private volatile bool _isStopping;

    // 防止 StartAsync 和 OnDisconnectedAsync 并发触发重连
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public MqttPushService(AppSettings appSettings, ILogger logger)
    {
        _config = appSettings.MqttBroker;
        _logger = logger;
        _mqttClient = new MqttClientFactory().CreateMqttClient();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.EnableMqttPush)
        {
            _logger.Information("MQTT 推送已禁用");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // 订阅断开事件，自动重连
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

        // 将 MQTT 连接作为后台任务执行，不阻塞 StartAsync，让采集服务尽快启动
        _ = Task.Run(() => ConnectWithRetryAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("MQTT 服务正在停止...");
        _isStopping = true;
        _cts?.Cancel();

        // 等待正在进行的连接/重连完成
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MQTT 断开连接时异常");
            }
        }
        finally
        {
            _connectLock.Release();
        }

        _logger.Information("MQTT 服务已停止");
    }

    /// <summary>
    /// MQTTnet 断开事件回调：自动重连
    /// </summary>
    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        if (_isStopping)
            return;

        // 尝试获取锁，防止与 StartAsync 或其他 OnDisconnectedAsync 并发
        if (!await _connectLock.WaitAsync(TimeSpan.Zero))
            return;

        try
        {
            _logger.Warning("MQTT 连接断开，reason={Reason}，启动重连", e.Reason);
            await ConnectWithRetryAsync(_cts?.Token ?? CancellationToken.None);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// 连接（启动时或重连时调用），失败时按指数退避重试
    /// </summary>
    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        var reconnect = _config.Reconnect;
        int retryCount = 0;
        int delaySeconds = reconnect.InitialDelaySeconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (reconnect.MaxRetries > 0 && retryCount >= reconnect.MaxRetries)
            {
                _logger.Error("MQTT 重连已达到最大次数 {MaxRetries}，停止重连", reconnect.MaxRetries);
                return;
            }

            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
                    .WithTcpServer(_config.Ip, _config.Port)
                    .WithClientId(_config.ClientId)
                    .WithCredentials(_config.Username ?? "", _config.Password ?? "")
                    .WithCleanSession(true)
                    .WithTimeout(TimeSpan.FromSeconds(10))
                    .Build();

                await _mqttClient.ConnectAsync(options, cancellationToken);
                _logger.Information("MQTT 连接成功，Broker: {Ip}:{Port}", _config.Ip, _config.Port);
                return;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("MQTT 连接已取消");
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.Warning("MQTT 连接失败（第 {RetryCount} 次），{Delay}秒后重试... Error: {Error}",
                    retryCount, delaySeconds, ex.Message);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                delaySeconds = Math.Min(delaySeconds * 2, reconnect.MaxDelaySeconds);
            }
        }
    }

    /// <summary>
    /// 发布消息到指定 Topic
    /// </summary>
    public async Task PublishAsync(string topic, MqttPayload payload, CancellationToken cancellationToken = default)
    {
        if (!_mqttClient.IsConnected)
        {
            _logger.Warning("MQTT 未连接，跳过发布到 {Topic}", topic);
            return;
        }

        try
        {
            // 根层级所有字段平铺：TIMESTAMP、采集结果、扩展字段全部在同一层
            var dict = payload.Root;
            string json = System.Text.Json.JsonSerializer.Serialize(dict, JsonContext.Default.DictionaryStringString);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(json))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);
            _logger.Debug("MQTT 发布成功，Topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MQTT 发布失败，Topic: {Topic}", topic);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _connectLock.Dispose();
        _mqttClient.Dispose();
    }
}
