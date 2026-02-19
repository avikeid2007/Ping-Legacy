using PingTool.Helpers;
using PingTool.Models;
using System.Text;

namespace PingTool.Services;

public class HistoryService
{
    // Singleton instance
    private static readonly Lazy<HistoryService> _instance = new(() => new HistoryService());
    public static HistoryService Instance => _instance.Value;

    private const int MaxHistoryItems = 500;
    private const string RetentionDaysKey = "HistoryRetentionDays";
    private const int DefaultRetentionDays = 15;
    
    private readonly List<HistoryItem> _history = new();
    private readonly object _lock = new();

    public event Action? HistoryChanged;

    public IReadOnlyList<HistoryItem> History => _history.AsReadOnly();

    public static int RetentionDays
    {
        get => SettingsHelper.Read<int?>(RetentionDaysKey) ?? DefaultRetentionDays;
        set => SettingsHelper.Save(RetentionDaysKey, value);
    }

    private HistoryService()
    {
        LoadHistory();
        CleanupExpiredHistory();
    }

    private void CleanupExpiredHistory()
    {
        lock (_lock)
        {
            var cutoffDate = DateTime.Now.AddDays(-RetentionDays);
            var removed = _history.RemoveAll(h => h.Timestamp < cutoffDate);
            if (removed > 0)
            {
                SaveHistory();
            }
        }
    }

    public void AddHistory(HistoryItem item)
    {
        lock (_lock)
        {
            _history.Insert(0, item);

            // Trim to max items
            while (_history.Count > MaxHistoryItems)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            SaveHistory();
            HistoryChanged?.Invoke();
        }
    }

    public void RemoveHistory(string id)
    {
        lock (_lock)
        {
            var item = _history.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                _history.Remove(item);
                SaveHistory();
                HistoryChanged?.Invoke();
            }
        }
    }

    public void ClearHistory(HistoryType? type = null)
    {
        lock (_lock)
        {
            if (type.HasValue)
            {
                _history.RemoveAll(h => h.Type == type.Value);
            }
            else
            {
                _history.Clear();
            }
            SaveHistory();
            HistoryChanged?.Invoke();
        }
    }

    public IEnumerable<HistoryItem> GetHistory(HistoryType? type = null)
    {
        lock (_lock)
        {
            return type.HasValue
                ? _history.Where(h => h.Type == type.Value).ToList()
                : _history.ToList();
        }
    }

    public string ExportToText(HistoryItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {item.TypeDisplay} Report ===");
        sb.AppendLine($"Date: {item.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Target: {item.Target}");

        if (!string.IsNullOrEmpty(item.DomainName))
        {
            sb.AppendLine($"Domain: {item.DomainName}");
        }

        sb.AppendLine($"Status: {(item.IsSuccess ? "Success" : "Failed")}");
        sb.AppendLine();
        sb.AppendLine("--- Summary ---");
        sb.AppendLine(item.Summary);
        sb.AppendLine();

        switch (item.Type)
        {
            case HistoryType.Ping:
                sb.AppendLine("--- Statistics ---");
                sb.AppendLine($"Total Pings: {item.PingCount}");
                sb.AppendLine($"Average Latency: {item.AvgLatency}ms");
                sb.AppendLine($"Packet Loss: {item.PacketLoss:F1}%");
                break;

            case HistoryType.SpeedTest:
                sb.AppendLine("--- Speed Test Results ---");
                sb.AppendLine($"Download: {item.DownloadSpeed:F1} Mbps");
                sb.AppendLine($"Upload: {item.UploadSpeed:F1} Mbps");
                sb.AppendLine($"Latency: {item.Latency}ms");
                break;

            case HistoryType.PortScan:
                sb.AppendLine("--- Port Scan Results ---");
                sb.AppendLine($"Open Ports: {item.OpenPorts}");
                sb.AppendLine($"Closed Ports: {item.ClosedPorts}");
                sb.AppendLine($"Total Scanned: {item.TotalPorts}");
                break;

            case HistoryType.Traceroute:
                sb.AppendLine("--- Traceroute Results ---");
                sb.AppendLine($"Total Hops: {item.HopCount}");
                break;
        }

        if (!string.IsNullOrEmpty(item.Details))
        {
            sb.AppendLine();
            sb.AppendLine("--- Details ---");
            sb.AppendLine(item.Details);
        }

        return sb.ToString();
    }

    public string ExportToCsv(IEnumerable<HistoryItem> items)
    {
        static string CsvEscape(string? value)
        {
            var text = value ?? string.Empty;
            var mustQuote = text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
            if (text.Contains('"'))
            {
                text = text.Replace("\"", "\"\"");
            }
            return mustQuote ? $"\"{text}\"" : text;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Type,Target,Domain,Success,Summary,Details,AvgLatencyMs,PacketLossPercent,PingCount,DownloadMbps,UploadMbps,LatencyMs,OpenPorts,ClosedPorts,TotalPorts,HopCount");

        foreach (var item in items)
        {
            sb.Append(CsvEscape(item.Timestamp.ToString("o"))).Append(',');
            sb.Append(CsvEscape(item.TypeDisplay)).Append(',');
            sb.Append(CsvEscape(item.Target)).Append(',');
            sb.Append(CsvEscape(item.DomainName)).Append(',');
            sb.Append(CsvEscape(item.IsSuccess ? "true" : "false")).Append(',');
            sb.Append(CsvEscape(item.Summary)).Append(',');
            sb.Append(CsvEscape(item.Details)).Append(',');

            sb.Append(CsvEscape(item.AvgLatency?.ToString())).Append(',');
            sb.Append(CsvEscape(item.PacketLoss?.ToString("F1"))).Append(',');
            sb.Append(CsvEscape(item.PingCount?.ToString())).Append(',');

            sb.Append(CsvEscape(item.DownloadSpeed?.ToString("F1"))).Append(',');
            sb.Append(CsvEscape(item.UploadSpeed?.ToString("F1"))).Append(',');
            sb.Append(CsvEscape(item.Latency?.ToString())).Append(',');

            sb.Append(CsvEscape(item.OpenPorts?.ToString())).Append(',');
            sb.Append(CsvEscape(item.ClosedPorts?.ToString())).Append(',');
            sb.Append(CsvEscape(item.TotalPorts?.ToString())).Append(',');
            sb.Append(CsvEscape(item.HopCount?.ToString()));

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void LoadHistory()
    {
        var saved = SettingsHelper.Read<List<HistoryItem>>("UnifiedHistory");
        if (saved != null)
        {
            _history.AddRange(saved);
        }
    }

    private void SaveHistory()
    {
        SettingsHelper.Save("UnifiedHistory", _history);
    }
}
