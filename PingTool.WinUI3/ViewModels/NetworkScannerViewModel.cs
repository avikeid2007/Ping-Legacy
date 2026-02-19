using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PingTool.ViewModels;

public partial class NetworkScannerViewModel : ObservableObject
{
    private readonly NetworkScannerService _scannerService = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = 
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _startIp = "192.168.1.1";

    [ObservableProperty]
    private string _endIp = "192.168.1.254";

    [ObservableProperty]
    private string _subnet = "192.168.1.0/24";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _statusText = "Ready to scan";

    [ObservableProperty]
    private ObservableCollection<NetworkScanResult> _scanResults = new();

    [ObservableProperty]
    private int _timeoutMs = 1000;

    [ObservableProperty]
    private int _maxConcurrentScans = 50;

    [ObservableProperty]
    private bool _resolveHostnames = true;

    [ObservableProperty]
    private bool _legalNoticeAcknowledged;

    [ObservableProperty]
    private int _onlineCount;

    [ObservableProperty]
    private int _offlineCount;

    [ObservableProperty]
    private int _totalScanned;

    [ObservableProperty]
    private string _selectedScanType = "Range";

    public NetworkScannerViewModel()
    {
        TrySetLocalNetwork();
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        if (!LegalNoticeAcknowledged)
        {
            StatusText = "⚠️ You must acknowledge the legal notice before scanning";
            return;
        }

        if (IsScanning)
        {
            // Stop scan - don't await, just trigger cancellation
            await StopScanAsync();
            return;
        }

        ScanResults.Clear();
        OnlineCount = 0;
        OfflineCount = 0;
        TotalScanned = 0;
        Progress = 0;

        IsScanning = true;
        StatusText = "Scanning...";

        _scanCts = new CancellationTokenSource();

        // Run scan in background task to keep UI responsive
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = new NetworkScanSettings
                {
                    StartIp = StartIp,
                    EndIp = EndIp,
                    TimeoutMs = TimeoutMs,
                    MaxConcurrentScans = MaxConcurrentScans,
                    ResolveHostnames = ResolveHostnames,
                    // MAC addresses can only be resolved for on-link (local L2) targets.
                    // We enable it for all scan types; the service will no-op when not applicable.
                    LookupMacVendor = true,
                    UserAcknowledgedLegalNotice = LegalNoticeAcknowledged
                };

                var progressReporter = new Progress<int>(p =>
                {
                    _dispatcherQueue.TryEnqueue(() => Progress = p);
                });

                IAsyncEnumerable<NetworkScanResult> scanOperation = SelectedScanType switch
                {
                    "Range" => _scannerService.ScanRangeAsync(StartIp, EndIp, settings, progressReporter, _scanCts.Token),
                    "Subnet" => _scannerService.ScanSubnetAsync(Subnet, settings, progressReporter, _scanCts.Token),
                    "Local" => _scannerService.ScanLocalNetworkAsync(settings, progressReporter, _scanCts.Token),
                    _ => _scannerService.ScanRangeAsync(StartIp, EndIp, settings, progressReporter, _scanCts.Token)
                };

                await foreach (var result in scanOperation.WithCancellation(_scanCts.Token))
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        ScanResults.Add(result);
                        TotalScanned++;

                        if (result.IsOnline)
                            OnlineCount++;
                        else
                            OfflineCount++;
                    });
                }

                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusText = $"Scan complete: {OnlineCount} online, {OfflineCount} offline";
                    SaveScanToHistory();
                });
            }
            catch (OperationCanceledException)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusText = "Scan cancelled by user";
                });
            }
            catch (Exception ex)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusText = $"Error: {ex.Message}";
                });
            }
            finally
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsScanning = false;
                });
                _scanCts?.Dispose();
                _scanCts = null;
            }
        });

        await Task.CompletedTask;
    }

    private async Task StopScanAsync()
    {
        StatusText = "Stopping scan...";
        _scanCts?.Cancel();

        // Wait briefly for cancellation to propagate
        await Task.Delay(50);

        StatusText = "Scan cancelled";
    }

    [RelayCommand]
    private void ClearResults()
    {
        ScanResults.Clear();
        OnlineCount = 0;
        OfflineCount = 0;
        TotalScanned = 0;
        Progress = 0;
        StatusText = "Results cleared";
    }

    [RelayCommand]
    private async Task ExportResultsAsync()
    {
        if (ScanResults.Count == 0)
        {
            StatusText = "No results to export";
            return;
        }

        var header = "=== Network Scan Results ===\r\n" +
                    $"Scan Type: {SelectedScanType}\r\n" +
                    $"Range: {(SelectedScanType == "Range" ? $"{StartIp} - {EndIp}" : Subnet)}\r\n" +
                    $"Total Scanned: {TotalScanned}\r\n" +
                    $"Online: {OnlineCount} | Offline: {OfflineCount}\r\n" +
                    $"Scan Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                    "============================\r\n\r\n";

        var results = string.Join("\r\n", ScanResults
            .Where(r => r.IsOnline)
            .OrderBy(r => r.IpAddress)
            .Select(r => $"{r.IpAddress,-15} | {r.ResponseTimeDisplay,6} | {r.MacAddressDisplay,-17} | {r.VendorDisplay,-20} | {r.Hostname ?? "N/A"}"));

        var content = header + "ONLINE HOSTS:\r\n" + "IP Address       | Time   | MAC Address        | Vendor               | Hostname\r\n" + results;

        await FileHelper.SaveFileAsync(content, "network_scan.txt");
        StatusText = "Results exported successfully";
    }

    [RelayCommand]
    private void CopyResult(NetworkScanResult result)
    {
        var text = $"{result.IpAddress} | {(result.IsOnline ? result.ResponseTimeDisplay : "Offline")} | {result.MacAddressDisplay} | {result.VendorDisplay} | {result.Hostname ?? "N/A"}";
        FileHelper.CopyText(text);
    }

    [RelayCommand]
    private void UseLocalNetwork()
    {
        TrySetLocalNetwork();
        SelectedScanType = "Local";
    }

    private void TrySetLocalNetwork()
    {
        try
        {
            var localIp = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (localIp != null)
            {
                var ipParts = localIp.ToString().Split('.');
                StartIp = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.1";
                EndIp = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.254";
                Subnet = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.0/24";
            }
        }
        catch
        {
            StartIp = "192.168.1.1";
            EndIp = "192.168.1.254";
            Subnet = "192.168.1.0/24";
        }
    }

    private void SaveScanToHistory()
    {
        if (TotalScanned == 0) return;

        var targetInfo = SelectedScanType switch
        {
            "Range" => $"{StartIp} - {EndIp}",
            "Subnet" => Subnet,
            "Local" => "Local Network",
            _ => "Unknown"
        };

        var historyItem = new HistoryItem
        {
            Type = HistoryType.NetworkScan,
            Target = targetInfo,
            IsSuccess = OnlineCount > 0,
            Summary = $"Found {OnlineCount} online hosts out of {TotalScanned} scanned",
            Details = $"Scan Type: {SelectedScanType}\n" +
                     $"Target: {targetInfo}\n" +
                     $"Total Scanned: {TotalScanned}\n" +
                     $"Online: {OnlineCount}\n" +
                     $"Offline: {OfflineCount}\n" +
                     $"Scan Duration: {Progress}%"
        };

        HistoryService.Instance.AddHistory(historyItem);
    }
}
