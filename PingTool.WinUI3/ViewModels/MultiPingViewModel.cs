using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingTool.Models;
using PingTool.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;

namespace PingTool.ViewModels;

public partial class MultiPingViewModel : ObservableObject
{
    private readonly Dictionary<string, CancellationTokenSource> _pingCts = new();
    private bool _isRunning;

    public ObservableCollection<PingTarget> Targets { get; } = new();

    [ObservableProperty]
    private string _newHostname = string.Empty;

    [ObservableProperty]
    private bool _isAllRunning;

    public bool HasTargets => Targets.Count > 0;

    [RelayCommand]
    private void AddTarget()
    {
        if (string.IsNullOrWhiteSpace(NewHostname)) return;
        if (Targets.Count >= 8) return; // Max 8 targets
        if (Targets.Any(t => t.Hostname.Equals(NewHostname, StringComparison.OrdinalIgnoreCase))) return;

        Targets.Add(new PingTarget { Hostname = NewHostname.Trim() });
        NewHostname = string.Empty;
        OnPropertyChanged(nameof(HasTargets));
    }

    [RelayCommand]
    private void AddPreset(string preset)
    {
        var hosts = preset switch
        {
            "dns" => new[] { "8.8.8.8", "1.1.1.1", "9.9.9.9", "208.67.222.222" },
            "cdn" => new[] { "cloudflare.com", "akamai.com", "fastly.com" },
            "popular" => new[] { "google.com", "microsoft.com", "github.com", "amazon.com" },
            _ => Array.Empty<string>()
        };

        foreach (var host in hosts)
        {
            if (Targets.Count >= 8) break;
            if (!Targets.Any(t => t.Hostname.Equals(host, StringComparison.OrdinalIgnoreCase)))
            {
                Targets.Add(new PingTarget { Hostname = host });
            }
        }
        OnPropertyChanged(nameof(HasTargets));
    }

    [RelayCommand]
    private void RemoveTarget(PingTarget target)
    {
        StopTarget(target);
        Targets.Remove(target);
        OnPropertyChanged(nameof(HasTargets));
    }

    [RelayCommand]
    private void ClearAll()
    {
        StopAll();
        Targets.Clear();
        OnPropertyChanged(nameof(HasTargets));
    }

    [RelayCommand]
    private void StartAll()
    {
        IsAllRunning = true;
        foreach (var target in Targets)
        {
            StartTarget(target);
        }
    }

    [RelayCommand]
    private void StopAll()
    {
        IsAllRunning = false;
        foreach (var target in Targets)
        {
            StopTarget(target);
        }
    }

    [RelayCommand]
    private void ToggleAll()
    {
        if (IsAllRunning)
            StopAll();
        else
            StartAll();
    }

    private void StartTarget(PingTarget target)
    {
        if (target.IsActive) return;

        var cts = new CancellationTokenSource();
        _pingCts[target.Hostname] = cts;
        target.IsActive = true;
        target.Reset();

        _ = PingLoopAsync(target, cts.Token);
    }

    private void StopTarget(PingTarget target)
    {
        if (_pingCts.TryGetValue(target.Hostname, out var cts))
        {
            cts.Cancel();
            _pingCts.Remove(target.Hostname);
        }
        
        // Save history if there were any pings
        if (target.SuccessCount + target.FailCount > 0)
        {
            SaveTargetHistory(target);
        }
        
        target.IsActive = false;
        target.Status = PingStatus.Idle;
    }

    private void SaveTargetHistory(PingTarget target)
    {
        var historyItem = new HistoryItem
        {
            Type = HistoryType.MultiPing,
            Target = target.IpAddress,
            DomainName = target.Hostname != target.IpAddress ? target.Hostname : null,
            IsSuccess = target.FailCount == 0,
            Summary = $"Avg: {target.AvgPing:F0}ms, Jitter: {target.Jitter:F0}ms, Loss: {target.PacketLoss:F1}%",
            AvgLatency = (long)target.AvgPing,
            PacketLoss = target.PacketLoss,
            PingCount = target.SuccessCount + target.FailCount,
            Details = $"Min: {(target.MinPing == long.MaxValue ? 0 : target.MinPing)}ms, Max: {target.MaxPing}ms, Avg: {target.AvgPing:F1}ms, Jitter: {target.Jitter:F1}ms, Sent: {target.SuccessCount + target.FailCount}, Received: {target.SuccessCount}"
        };
        
        HistoryService.Instance.AddHistory(historyItem);
    }

    private async Task PingLoopAsync(PingTarget target, CancellationToken token)
    {
        using var ping = new Ping();

        // Resolve IP first
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(target.Hostname);
            if (addresses.Length > 0)
                target.IpAddress = addresses[0].ToString();
        }
        catch
        {
            target.IpAddress = target.Hostname;
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                target.Status = PingStatus.Pinging;
                var reply = await ping.SendPingAsync(target.Hostname, 3000);
                
                if (reply.Status == IPStatus.Success)
                {
                    target.AddPingResult(reply.RoundtripTime, true);
                }
                else
                {
                    target.AddPingResult(0, false);
                }
            }
            catch
            {
                target.AddPingResult(0, false);
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        target.IsActive = false;
    }
}
