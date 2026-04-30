using System.Net.Sockets;

namespace OcrServer.Utilities;

/// <summary>
/// 网络异常友好提示工具
/// </summary>
public static class NetworkExceptionHelper
{
    /// <summary>
    /// 将 HttpRequestException 或 TaskCanceledException（超时）转换为友好提示
    /// </summary>
    public static string GetFriendlyMessage(Exception ex, string? targetName = null)
    {
        string target = string.IsNullOrEmpty(targetName) ? "目标服务" : targetName;

        // TaskCanceledException 超时（HttpClient 超时抛出此异常）
        if (ex is TaskCanceledException tce && tce.InnerException is TimeoutException)
        {
            return $"{target} 连接超时：服务响应过慢，请检查目标机器网络状态";
        }

        // HttpRequestException
        if (ex is not HttpRequestException hrex)
        {
            return $"{target} 异常：{ex.Message}";
        }

        // 连接被拒绝 / 积极拒绝 = 服务未启动
        if (hrex.InnerException is SocketException ||
            hrex.Message.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
            hrex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            hrex.Message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target} 连接失败：服务未启动或网络不通，请检查目标机器是否在线、服务是否已启动、端口是否可访问";
        }

        // 超时
        if (hrex.InnerException is TimeoutException ||
            hrex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target} 连接超时：服务响应过慢，请检查目标机器网络状态";
        }

        // DNS 解析失败
        if (hrex.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound } ||
            hrex.Message.Contains("name or service not known", StringComparison.OrdinalIgnoreCase) ||
            hrex.Message.Contains("no such host", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target} 连接失败：无法解析主机地址，请检查 IP 地址配置是否正确";
        }

        // 网络中断 / 取消
        if (hrex.Message.Contains("aborted", StringComparison.OrdinalIgnoreCase) ||
            hrex.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target} 连接中断：请求被取消或网络中断，请检查网络连接稳定性";
        }

        // 未知网络错误
        return $"{target} 连接异常：{hrex.Message}，请检查网络配置和服务状态";
    }
}
