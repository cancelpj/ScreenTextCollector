using System.Collections.Concurrent;

namespace OcrServer.Services;

/// <summary>
/// 单台设备的缓存条目：OCR 结果 + 采集时间戳
/// </summary>
internal sealed class DeviceCacheEntry
{
    public ConcurrentDictionary<string, string> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);
    public long Timestamp { get; set; }
}

/// <summary>
/// 线程安全的 OCR 结果缓存，按 DeviceCode 隔离
/// Key: DeviceCode -> Key: AreaName -> Value: OcrResult
/// </summary>
public sealed class ResultCache
{
    private readonly ConcurrentDictionary<string, DeviceCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 更新指定设备的单个区域的 OCR 结果
    /// </summary>
    public void Set(string deviceCode, string areaName, string ocrResult)
    {
        var entry = _cache.GetOrAdd(deviceCode, _ => new DeviceCacheEntry());
        entry.Cache[areaName] = ocrResult;
        entry.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 批量更新指定设备的多个区域结果，同时更新该设备的时间戳
    /// </summary>
    public void SetBatch(string deviceCode, Dictionary<string, string> results)
    {
        var entry = _cache.GetOrAdd(deviceCode, _ => new DeviceCacheEntry());
        foreach (var kvp in results)
        {
            entry.Cache[kvp.Key] = kvp.Value;
        }
        entry.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 获取指定设备的时间戳（毫秒），若设备不存在返回 0
    /// </summary>
    public long GetTimestamp(string deviceCode)
    {
        if (_cache.TryGetValue(deviceCode, out var entry))
            return entry.Timestamp;
        return 0;
    }

    /// <summary>
    /// 获取指定设备的所有区域结果，附带 __timestamp 字段
    /// </summary>
    public Dictionary<string, string> GetDevice(string deviceCode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_cache.TryGetValue(deviceCode, out var entry))
        {
            foreach (var kvp in entry.Cache)
                result[kvp.Key] = kvp.Value;
            if (entry.Timestamp > 0)
                result["__timestamp"] = entry.Timestamp.ToString();
        }
        return result;
    }

    /// <summary>
    /// 获取指定设备的指定区域结果
    /// </summary>
    public string Get(string deviceCode, string areaName)
    {
        if (_cache.TryGetValue(deviceCode, out var entry) &&
            entry.Cache.TryGetValue(areaName, out var result))
        {
            return result;
        }
        return "";
    }

    /// <summary>
    /// 获取所有设备的所有区域结果，附带到各设备数据中
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> GetAll()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _cache)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var area in kvp.Value.Cache)
                dict[area.Key] = area.Value;
            if (kvp.Value.Timestamp > 0)
                dict["__timestamp"] = kvp.Value.Timestamp.ToString();
            result[kvp.Key] = dict;
        }
        return result;
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 清空指定设备的缓存
    /// </summary>
    public void ClearDevice(string deviceCode)
    {
        _cache.TryRemove(deviceCode, out _);
    }
}
