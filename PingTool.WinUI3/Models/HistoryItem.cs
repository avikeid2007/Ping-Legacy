namespace PingTool.Models;

public enum HistoryType
{
    Ping,
    Traceroute,
    PortScan,
    SpeedTest,
    MultiPing,
    NetworkScan
}

public class HistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public HistoryType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Target { get; set; } = string.Empty;
    public string? DomainName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }

    // Ping specific
    public long? AvgLatency { get; set; }
    public double? PacketLoss { get; set; }
    public int? PingCount { get; set; }

    // Speed Test specific
    public double? DownloadSpeed { get; set; }
    public double? UploadSpeed { get; set; }
    public long? Latency { get; set; }

    // Port Scan specific
    public int? OpenPorts { get; set; }
    public int? ClosedPorts { get; set; }
    public int? TotalPorts { get; set; }

    // Traceroute specific
    public int? HopCount { get; set; }

    public string TypeIcon => Type switch
    {
        HistoryType.Ping => "\uE968",
        HistoryType.Traceroute => "\uE8F1",
        HistoryType.PortScan => "\uE946",
        HistoryType.SpeedTest => "\uE8B0",
        HistoryType.MultiPing => "\uF0E2",
        HistoryType.NetworkScan => "\uE839",
        _ => "\uE946"
    };

    public string TypeDisplay => Type switch
    {
        HistoryType.Ping => "Ping",
        HistoryType.Traceroute => "Traceroute",
        HistoryType.PortScan => "Port Scan",
        HistoryType.SpeedTest => "Speed Test",
        HistoryType.MultiPing => "Multi-Ping",
        HistoryType.NetworkScan => "Network Scan",
        _ => "Unknown"
    };

    public string DisplayTitle => string.IsNullOrEmpty(DomainName) ? Target : $"{DomainName} ({Target})";
    public string TimestampDisplay => Timestamp.ToString("g");
}
