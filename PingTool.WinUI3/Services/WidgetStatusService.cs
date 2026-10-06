using PingTool.Services.Widgets;

namespace PingTool.Services;

/// <summary>
/// Bridge between the ping loop (MainViewModel) and the Windows Widgets Board provider
/// (<see cref="WidgetProvider"/>). Persists the latest result via SettingsHelper so the "Ping
/// Status" widget has data to show even if the Widgets Board launches a fresh app instance that
/// isn't currently running a ping itself.
/// </summary>
public static class WidgetStatusService
{
    public static void ReportPingResult(string host, bool isSuccess, long latencyMs)
    {
        SettingsHelper.Save("WidgetLastHost", host);
        SettingsHelper.Save("WidgetLastSuccess", isSuccess);
        SettingsHelper.Save("WidgetLastLatency", latencyMs);
        SettingsHelper.Save("WidgetLastUpdated", DateTime.Now);

        WidgetProvider.UpdateAllWidgets();
    }
}
