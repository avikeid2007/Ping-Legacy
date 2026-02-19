using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PingTool.Models;

public partial class PingTarget : ObservableObject
{
    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private long _lastPing;

    [ObservableProperty]
    private double _avgPing;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failCount;

    [ObservableProperty]
    private double _packetLoss;

    [ObservableProperty]
    private double _jitter;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private PingStatus _status = PingStatus.Idle;

    [ObservableProperty]
    private long _minPing = long.MaxValue;

    [ObservableProperty]
    private long _maxPing;

    public ObservableCollection<long> PingHistory { get; } = new();

    public string StatusText => Status switch
    {
        PingStatus.Success => $"{LastPing}ms",
        PingStatus.Failed => "Failed",
        PingStatus.Pinging => "...",
        _ => "Idle"
    };

    public void AddPingResult(long latency, bool success)
    {
        if (success)
        {
            LastPing = latency;
            SuccessCount++;
            
            if (latency < MinPing) MinPing = latency;
            if (latency > MaxPing) MaxPing = latency;
            
            PingHistory.Add(latency);
            if (PingHistory.Count > 20)
                PingHistory.RemoveAt(0);

            if (PingHistory.Count >= 2)
            {
                var mean = PingHistory.Average();
                var variance = PingHistory.Select(t => Math.Pow(t - mean, 2)).Average();
                Jitter = Math.Sqrt(variance);
            }
            else
            {
                Jitter = 0;
            }
            
            Status = PingStatus.Success;
        }
        else
        {
            FailCount++;
            Status = PingStatus.Failed;
        }

        var total = SuccessCount + FailCount;
        if (total > 0)
        {
            PacketLoss = (double)FailCount / total * 100;
            if (SuccessCount > 0 && PingHistory.Count > 0)
                AvgPing = PingHistory.Average();
        }
    }

    public void Reset()
    {
        LastPing = 0;
        AvgPing = 0;
        SuccessCount = 0;
        FailCount = 0;
        PacketLoss = 0;
        Jitter = 0;
        MinPing = long.MaxValue;
        MaxPing = 0;
        Status = PingStatus.Idle;
        IpAddress = string.Empty;
        PingHistory.Clear();
    }
}

public enum PingStatus
{
    Idle,
    Pinging,
    Success,
    Failed
}
