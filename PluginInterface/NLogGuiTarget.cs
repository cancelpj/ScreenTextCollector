using NLog;
using NLog.Targets;

namespace PluginInterface
{
    [Target("NLogGuiTarget")]
    public sealed class NLogGuiTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            // 如果是来自 UI 的日志（由 Form1.LogWrite 标记），则跳过，避免循环触发
            if (logEvent.Properties != null &&
                logEvent.Properties.ContainsKey("FromUi") &&
                logEvent.Properties["FromUi"] is bool fromUi && fromUi)
            {
                return;
            }

            var message = Layout.Render(logEvent);
            Tool.RaiseLog(logEvent.Level, message);
        }
    }
}