using Serilog;
using Serilog.Events;

namespace OcrServer.Configuration;

/// <summary>
/// Serilog 日志配置
/// </summary>
public class SerilogConfig
{
    /// <summary>
    /// 全局最小日志级别，默认 Warning
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// 控制台输出配置
    /// </summary>
    public SerilogConsoleConfig Console { get; set; } = new();

    /// <summary>
    /// 文件输出配置
    /// </summary>
    public SerilogFileConfig File { get; set; } = new();

    /// <summary>
    /// 根据配置创建 LoggerConfiguration
    /// </summary>
    /// <param name="dataDir">可选，data 目录路径。传入时日志文件路径拼接为绝对路径</param>
    public LoggerConfiguration CreateLogger(string? dataDir = null)
    {
        var logLevel = ParseLogEventLevel(MinimumLevel);
        var consoleLevel = ParseLogEventLevel(Console.MinimumLevel);
        var fileLevel = ParseLogEventLevel(File.MinimumLevel);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console(restrictedToMinimumLevel: consoleLevel);

        var filePath = dataDir != null
            ? Path.Combine(dataDir, "..", File.Path)
            : File.Path;

        loggerConfig.WriteTo.File(
            filePath,
            rollingInterval: ParseRollingInterval(File.RollingInterval),
            restrictedToMinimumLevel: fileLevel,
            retainedFileCountLimit: File.RetainedFileCountLimit);

        return loggerConfig;
    }

    private static LogEventLevel ParseLogEventLevel(string level) => level.ToLowerInvariant() switch
    {
        "verbose" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "information" => LogEventLevel.Information,
        "warning" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information,
    };

    private static RollingInterval ParseRollingInterval(string interval) => interval.ToLowerInvariant() switch
    {
        "minute" => RollingInterval.Minute,
        "hour" => RollingInterval.Hour,
        "day" => RollingInterval.Day,
        "month" => RollingInterval.Month,
        "year" => RollingInterval.Year,
        _ => RollingInterval.Day,
    };
}

/// <summary>
/// Serilog 控制台配置
/// </summary>
public class SerilogConsoleConfig
{
    /// <summary>
    /// 控制台最小日志级别，默认 Debug
    /// </summary>
    public string MinimumLevel { get; set; } = "Debug";
}

/// <summary>
/// Serilog 文件配置
/// </summary>
public class SerilogFileConfig
{
    /// <summary>
    /// 日志文件路径，支持 RollingInterval 占位符（如 ocrserver-.log）
    /// </summary>
    public string Path { get; set; } = "logs/ocrserver-.log";

    /// <summary>
    /// 滚动间隔，默认 Day
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// 文件最小日志级别，默认 Warning
    /// </summary>
    public string MinimumLevel { get; set; } = "Warning";

    /// <summary>
    /// 保留文件数量，默认 7
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;
}
