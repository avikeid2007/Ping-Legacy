using System;

namespace PingTool.Models;

public class NetworkScanResult
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public long ResponseTime { get; set; }
    public string? Hostname { get; set; }
    public DateTime ScanTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class NetworkScanSettings
{
    public string StartIp { get; set; } = string.Empty;
    public string EndIp { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 1000;
    public int MaxConcurrentScans { get; set; } = 50;
    public bool ResolveHostnames { get; set; } = true;
    public bool UserAcknowledgedLegalNotice { get; set; }
}
