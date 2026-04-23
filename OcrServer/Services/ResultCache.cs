using System.Collections.Concurrent;

namespace OcrServer.Services;

/// <summary>
/// 线程安全的 OCR 结果缓存，按 DeviceCode 隔离
/// Key: DeviceCode -> Key: AreaName -> Value: OcrResult
/// </summary>
public sealed class ResultCache
{
    // 外层：DeviceCode（按设备隔离）
    // 内层：AreaName -> OcrResult
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 更新指定设备的单个区域的 OCR 结果
    /// </summary>
    public void Set(string deviceCode, string areaName, string ocrResult)
    {
        var deviceCache = _cache.GetOrAdd(deviceCode, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        deviceCache[areaName] = ocrResult;
    }

    /// <summary>
    /// 批量更新指定设备的多个区域结果
    /// </summary>
    public void SetBatch(string deviceCode, Dictionary<string, string> results)
    {
        var deviceCache = _cache.GetOrAdd(deviceCode, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        foreach (var kvp in results)
        {
            deviceCache[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// 获取指定设备的所有区域结果
    /// </summary>
    public Dictionary<string, string> GetDevice(string deviceCode)
    {
        if (_cache.TryGetValue(deviceCode, out var deviceCache))
        {
            return new Dictionary<string, string>(deviceCache, StringComparer.OrdinalIgnoreCase);
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取指定设备的指定区域结果
    /// </summary>
    public string Get(string deviceCode, string areaName)
    {
        if (_cache.TryGetValue(deviceCode, out var deviceCache) &&
            deviceCache.TryGetValue(areaName, out var result))
        {
            return result;
        }
        return "";
    }

    /// <summary>
    /// 获取所有设备的所有区域结果（用于 /collect 接口）
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> GetAll()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in _cache)
        {
            result[device.Key] = new Dictionary<string, string>(device.Value, StringComparer.OrdinalIgnoreCase);
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
