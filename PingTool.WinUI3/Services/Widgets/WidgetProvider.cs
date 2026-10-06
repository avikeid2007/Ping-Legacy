using Microsoft.Windows.Widgets.Providers;
using PingTool.Services;

namespace PingTool.Services.Widgets;

/// <summary>
/// Windows 11 Widgets Board provider for Ping Legacy. Shows the most recent ping result
/// (host, latency/online-offline status, last-updated time) as a small "Ping Status" widget.
/// Content is refreshed by <see cref="WidgetStatusService.ReportPingResult"/>, called from
/// MainViewModel whenever a new ping result comes in, so the widget stays live even across
/// separate app launches (the last result is persisted via SettingsHelper).
/// </summary>
public class WidgetProvider : IWidgetProvider
{
    private static readonly Dictionary<string, string> RunningWidgets = new();
    private static readonly object RunningWidgetsLock = new();

    public WidgetProvider()
    {
        try
        {
            var runningWidgets = WidgetManager.GetDefault().GetWidgetInfos();
            lock (RunningWidgetsLock)
            {
                foreach (var widgetInfo in runningWidgets)
                {
                    var id = widgetInfo.WidgetContext.Id;
                    RunningWidgets[id] = widgetInfo.WidgetContext.DefinitionId;
                }
            }
        }
        catch
        {
            // WidgetManager is only available on Windows 11 with the Widgets Board installed.
        }
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        lock (RunningWidgetsLock)
        {
            RunningWidgets[widgetContext.Id] = widgetContext.DefinitionId;
        }

        UpdateWidget(widgetContext.Id);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        lock (RunningWidgetsLock)
        {
            RunningWidgets.Remove(widgetId);
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        if (actionInvokedArgs.Verb == "refresh")
        {
            UpdateWidget(actionInvokedArgs.WidgetContext.Id);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        UpdateWidget(contextChangedArgs.WidgetContext.Id);
    }

    public void Activate(WidgetContext widgetContext) => UpdateWidget(widgetContext.Id);

    public void Deactivate(string widgetId)
    {
        // No background work needed while the Widgets Board isn't actively showing this widget.
    }

    /// <summary>Pushes the latest persisted ping result to every currently pinned widget.</summary>
    public static void UpdateAllWidgets()
    {
        List<string> widgetIds;
        lock (RunningWidgetsLock)
        {
            widgetIds = new List<string>(RunningWidgets.Keys);
        }

        foreach (var widgetId in widgetIds)
        {
            UpdateWidget(widgetId);
        }
    }

    private static void UpdateWidget(string widgetId)
    {
        try
        {
            var host = SettingsHelper.Read<string>("WidgetLastHost");
            var isSuccess = SettingsHelper.Read<bool?>("WidgetLastSuccess");
            var latency = SettingsHelper.Read<long?>("WidgetLastLatency");
            var updated = SettingsHelper.Read<DateTime?>("WidgetLastUpdated");

            string statusText;
            string statusColor;
            if (host is null || isSuccess is null)
            {
                statusText = "No active ping";
                statusColor = "default";
            }
            else
            {
                statusText = isSuccess.Value ? $"{latency} ms" : "Unreachable";
                statusColor = isSuccess.Value ? "good" : "attention";
            }

            var updatedText = updated.HasValue ? $"Updated {updated:HH:mm:ss}" : "No data yet";

            var data = $$"""
            {
                "host": {{System.Text.Json.JsonSerializer.Serialize(host ?? "Not pinging")}},
                "status": {{System.Text.Json.JsonSerializer.Serialize(statusText)}},
                "statusColor": "{{statusColor}}",
                "updated": {{System.Text.Json.JsonSerializer.Serialize(updatedText)}}
            }
            """;

            var updateOptions = new WidgetUpdateRequestOptions(widgetId)
            {
                Template = PingStatusTemplate,
                Data = data
            };

            WidgetManager.GetDefault().UpdateWidget(updateOptions);
        }
        catch
        {
            // Widget host unavailable - ignore, this is best-effort.
        }
    }

    private const string PingStatusTemplate = """
    {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.5",
        "body": [
            {
                "type": "TextBlock",
                "text": "Ping Legacy",
                "size": "medium",
                "weight": "bolder"
            },
            {
                "type": "TextBlock",
                "text": "${host}",
                "wrap": true,
                "spacing": "none"
            },
            {
                "type": "TextBlock",
                "text": "${status}",
                "size": "extraLarge",
                "weight": "bolder",
                "color": "${statusColor}"
            },
            {
                "type": "TextBlock",
                "text": "${updated}",
                "size": "small",
                "isSubtle": true
            }
        ],
        "actions": [
            {
                "type": "Action.Execute",
                "title": "Refresh",
                "verb": "refresh"
            }
        ]
    }
    """;
}
