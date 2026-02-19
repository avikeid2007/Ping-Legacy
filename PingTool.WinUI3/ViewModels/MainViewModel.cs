using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Input;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Windows.Networking.Connectivity;
using Windows.System;

namespace PingTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PingService _pingService = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    private System.Threading.Timer? _dataRefreshTimer;

    private CancellationTokenSource? _pingCts;
    private Guid _pingId;
    private DateTimeOffset _pingDate;

    private readonly string _urlRegex = @"^(http|https|ftp|)?\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
    private readonly string _ipRegex = @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}";

    // Ping statistics tracking
    private readonly List<long> _successfulPingTimes = new();
    private int _totalPings;
    private int _failedPings;
    private int _consecutiveFailures;
    private const int FailureThreshold = 3;
    private bool _notificationShown;

    #region Network Properties

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private string _ipv6Address = string.Empty;

    [ObservableProperty]
    private string _publicIp = string.Empty;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private bool _isWlan;

    [ObservableProperty]
    private long _totalReceivedBytes;

    [ObservableProperty]
    private long _totalSentBytes;

    [ObservableProperty]
    private bool _isSupportIPV6;

    [ObservableProperty]
    private int _wifiBars;

    [ObservableProperty]
    private string _hostNameOrAddress = "8.8.8.8";

    [ObservableProperty]
    private string _ipType = string.Empty;

    [ObservableProperty]
    private ObservableCollection<NetworkInterface> _localLANCollection = new();

    [ObservableProperty]
    private NetworkInterface? _selectedLocalLAN;

    [ObservableProperty]
    private bool _hasInternetAccess;

    [ObservableProperty]
    private bool _isPingStarted;

    [ObservableProperty]
    private bool _isCompactMode;

    [ObservableProperty]
    private ObservableCollection<PingMassage> _pingCollection = new();

    #endregion

    #region Statistics Properties

    [ObservableProperty]
    private long _minPing;

    [ObservableProperty]
    private long _maxPing;

    [ObservableProperty]
    private double _avgPing;

    [ObservableProperty]
    private double _packetLoss;

    [ObservableProperty]
    private double _jitter;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failCount;

    #endregion

    #region Favorites Properties

    [ObservableProperty]
    private ObservableCollection<string> _favoriteHosts = new();

    [ObservableProperty]
    private string? _selectedFavorite;

    #endregion

    #region Chart Data

    [ObservableProperty]
    private ObservableCollection<double> _chartValues = new();

    private const int MaxChartPoints = 50;

    #endregion

    #region DNS Lookup Properties

    private readonly DnsLookupService _dnsLookupService = new();

    [ObservableProperty]
    private string _dnsLookupHost = string.Empty;

    [ObservableProperty]
    private DnsLookupResult? _dnsResult;

    [ObservableProperty]
    private bool _isDnsLookupRunning;

    #endregion

    public async Task InitializeAsync()
    {
        // Load saved host address
        var savedAddress = SettingsHelper.Read<string>("HostNameOrAddress");
        if (!string.IsNullOrEmpty(savedAddress))
        {
            HostNameOrAddress = savedAddress;
        }

        // Load favorites
        LoadFavorites();

        // Load auto-start preference
        var isPingAutoStart = SettingsHelper.Read<bool?>("IsPingAutoStart") ?? true;

        await SetNetworkInfoAsync();
        _ = FetchPublicIpAsync(); // Fire and forget - don't wait for this
        NetworkInformation.NetworkStatusChanged += async _ =>
        {
            await SetNetworkInfoAsync();
            _ = FetchPublicIpAsync();
        };

        // Start data usage auto-refresh timer (every 2 seconds)
        _dataRefreshTimer = new System.Threading.Timer(
            async _ => await RefreshDataUsageAsync(),
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));

        if (isPingAutoStart)
        {
            await Task.Delay(2000);
            await StartPingAsync();
        }
    }

    private async Task FetchPublicIpAsync()
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var publicIp = await httpClient.GetStringAsync("https://api.ipify.org");
            _dispatcherQueue.TryEnqueue(() => PublicIp = publicIp.Trim());
        }
        catch
        {
            _dispatcherQueue.TryEnqueue(() => PublicIp = "N/A");
        }
    }

    private async Task RefreshDataUsageAsync()
    {
        try
        {
            if (SelectedLocalLAN != null)
            {
                var ipStats = SelectedLocalLAN.GetIPStatistics();
                _dispatcherQueue.TryEnqueue(() =>
                {
                    TotalReceivedBytes = ipStats.BytesReceived / 1048576;
                    TotalSentBytes = ipStats.BytesSent / 1048576;
                });
            }
        }
        catch
        {
            // Ignore refresh errors
        }
    }

    #region Ping Commands

    [RelayCommand]
    private async Task StartPingAsync()
    {
        if (!IsPingStarted)
        {
            if (IsValidHostNameOrAddress(HostNameOrAddress))
            {
                _pingId = Guid.NewGuid();
                _pingDate = DateTimeOffset.Now;
                IsPingStarted = true;

                // Reset statistics
                ResetStatistics();

                // Save the host address
                SettingsHelper.Save("HostNameOrAddress", HostNameOrAddress);

                // Start ping with cancellation support
                _pingCts = new CancellationTokenSource();
                var cleanHost = HostNameOrAddress.Replace("http://", "").Replace("https://", "").TrimEnd('/');

                _ = Task.Run(async () =>
                {
                    await foreach (var result in _pingService.StartPingAsync(cleanHost, cancellationToken: _pingCts.Token))
                    {
                        var pingMessage = new PingMassage
                        {
                            PingId = _pingId,
                            Date = _pingDate,
                            IpAddress = result.IpAddress,
                            Time = result.Time,
                            Size = result.Size,
                            Ttl = result.Ttl,
                            Response = result.Response
                        };

                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            PingCollection.Add(pingMessage);
                            UpdateStatistics(result);
                            SQLiteHelper.Save(pingMessage);
                        });
                    }
                });

                await DeleteOlderHistoryAsync();
            }
        }
        else
        {
            // Stop ping and save to history
            _pingCts?.Cancel();
            _pingService.Stop();

            // Save ping session to history
            SavePingToHistory();

            IsPingStarted = false;
        }
    }

    private void SavePingToHistory()
    {
        if (_totalPings == 0) return;

        var host = HostNameOrAddress?.Replace("http://", "").Replace("https://", "").TrimEnd('/') ?? "";
        var firstPing = PingCollection.FirstOrDefault();
        var domainName = host != firstPing?.IpAddress ? host : null;

        var historyItem = new HistoryItem
        {
            Type = HistoryType.Ping,
            Target = firstPing?.IpAddress ?? host,
            DomainName = domainName,
            IsSuccess = SuccessCount > 0,
            PingCount = _totalPings,
            AvgLatency = (long)AvgPing,
            PacketLoss = PacketLoss,
            Summary = $"{_totalPings} pings, Avg: {AvgPing:F0}ms, Jitter: {Jitter:F0}ms, Loss: {PacketLoss:F1}%",
            Details = $"Target: {host}\n" +
                     $"IP: {firstPing?.IpAddress}\n" +
                     $"Total Pings: {_totalPings}\n" +
                     $"Success: {SuccessCount}, Failed: {FailCount}\n" +
                     $"Min: {MinPing}ms, Max: {MaxPing}ms, Avg: {AvgPing:F1}ms, Jitter: {Jitter:F1}ms\n" +
                     $"Packet Loss: {PacketLoss:F1}%"
        };

        HistoryService.Instance.AddHistory(historyItem);
    }

    [RelayCommand]
    private void ClearPing()
    {
        PingCollection.Clear();
        ChartValues.Clear();
        ResetStatistics();
    }

    [RelayCommand]
    private void CopyIpAddress()
    {
        FileHelper.CopyText(IpAddress);
    }

    [RelayCommand]
    private void CopyPublicIp()
    {
        if (!string.IsNullOrEmpty(PublicIp) && PublicIp != "N/A")
        {
            FileHelper.CopyText(PublicIp);
        }
    }

    #endregion

    #region DNS Lookup Commands

    [RelayCommand]
    private async Task DnsLookupAsync()
    {
        if (string.IsNullOrWhiteSpace(DnsLookupHost))
        {
            // Use current ping host if DNS host is empty
            DnsLookupHost = HostNameOrAddress;
        }

        if (string.IsNullOrWhiteSpace(DnsLookupHost)) return;

        IsDnsLookupRunning = true;
        try
        {
            DnsResult = await _dnsLookupService.LookupAsync(DnsLookupHost.Trim());
        }
        finally
        {
            IsDnsLookupRunning = false;
        }
    }

    [RelayCommand]
    private void CopyDnsResult()
    {
        if (DnsResult?.IsSuccess == true)
        {
            var text = $"Hostname: {DnsResult.Hostname}\n" +
                      $"Canonical: {DnsResult.CanonicalName}\n" +
                      $"IPv4: {string.Join(", ", DnsResult.IPv4Addresses)}\n" +
                      $"IPv6: {string.Join(", ", DnsResult.IPv6Addresses)}";
            FileHelper.CopyText(text);
        }
    }

    #endregion

    #region Export Commands

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (PingCollection.Count > 0)
        {
            var stats = $"=== Ping Statistics ===\r\n" +
                       $"Host: {HostNameOrAddress}\r\n" +
                       $"Min: {MinPing}ms | Max: {MaxPing}ms | Avg: {AvgPing:F1}ms\r\n" +
                       $"Packet Loss: {PacketLoss:F1}% ({FailCount}/{_totalPings})\r\n" +
                       $"========================\r\n\r\n";
            var text = stats + string.Join("\r\n", PingCollection.Select(x => x.Response));
            await FileHelper.SaveFileAsync(text, "ping.txt");
        }
    }

    [RelayCommand]
    private async Task ToggleCompactModeAsync()
    {
        IsCompactMode = !IsCompactMode;
        if (IsCompactMode && !IsPingStarted)
        {
            await StartPingAsync();
        }
    }

    #endregion

    #region Favorites Commands

    [RelayCommand]
    private void AddToFavorites()
    {
        if (!string.IsNullOrWhiteSpace(HostNameOrAddress) && !FavoriteHosts.Contains(HostNameOrAddress))
        {
            FavoriteHosts.Add(HostNameOrAddress);
            SaveFavorites();
        }
    }

    [RelayCommand]
    private void RemoveFromFavorites(string host)
    {
        if (FavoriteHosts.Contains(host))
        {
            FavoriteHosts.Remove(host);
            SaveFavorites();
        }
    }

    [RelayCommand]
    private async Task SelectFavoriteAsync(string host)
    {
        if (!string.IsNullOrWhiteSpace(host))
        {
            // Stop current ping if running
            if (IsPingStarted)
            {
                await StartPingAsync();
            }

            HostNameOrAddress = host;
            PingCollection.Clear();
            ChartValues.Clear();
            ResetStatistics();

            // Start pinging the new host
            await StartPingAsync();
        }
    }

    private void LoadFavorites()
    {
        var favorites = SettingsHelper.Read<List<string>>("FavoriteHosts");
        if (favorites != null)
        {
            FavoriteHosts = new ObservableCollection<string>(favorites);
        }
        else
        {
            // Add some default favorites
            FavoriteHosts = new ObservableCollection<string>
            {
                "8.8.8.8",
                "1.1.1.1",
                "google.com",
                "cloudflare.com"
            };
            SaveFavorites();
        }
    }

    private void SaveFavorites()
    {
        SettingsHelper.Save("FavoriteHosts", FavoriteHosts.ToList());
    }

    #endregion

    #region Keyboard Shortcuts

    public async Task HandleKeyboardShortcutAsync(VirtualKey key, bool isCtrlPressed)
    {
        switch (key)
        {
            case VirtualKey.F5:
                if (!IsPingStarted)
                    await StartPingAsync();
                break;
            case VirtualKey.Escape:
                if (IsPingStarted)
                    await StartPingAsync(); // Toggle off
                break;
            case VirtualKey.E when isCtrlPressed:
                await ExportAsync();
                break;
            case VirtualKey.D when isCtrlPressed:
                ClearPing();
                break;
            case VirtualKey.F when isCtrlPressed:
                AddToFavorites();
                break;
        }
    }

    #endregion

    #region Statistics Methods

    private void ResetStatistics()
    {
        _successfulPingTimes.Clear();
        _totalPings = 0;
        _failedPings = 0;
        MinPing = 0;
        MaxPing = 0;
        AvgPing = 0;
        PacketLoss = 0;
        Jitter = 0;
        SuccessCount = 0;
        FailCount = 0;
    }

    private void UpdateStatistics(PingResult result)
    {
        _totalPings++;

        if (result.IsSuccess && result.Time >= 0)
        {
            _successfulPingTimes.Add(result.Time);
            SuccessCount++;

            // Reset consecutive failures on success
            _consecutiveFailures = 0;
            _notificationShown = false;

            MinPing = _successfulPingTimes.Min();
            MaxPing = _successfulPingTimes.Max();
            AvgPing = _successfulPingTimes.Average();

            if (_successfulPingTimes.Count >= 2)
            {
                var mean = AvgPing;
                var variance = _successfulPingTimes.Select(t => Math.Pow(t - mean, 2)).Average();
                Jitter = Math.Sqrt(variance);
            }
            else
            {
                Jitter = 0;
            }

            // Update chart
            ChartValues.Add(result.Time);
            if (ChartValues.Count > MaxChartPoints)
            {
                ChartValues.RemoveAt(0);
            }
        }
        else
        {
            _failedPings++;
            FailCount++;
            _consecutiveFailures++;

            // Show notification on connection drop (3+ consecutive failures)
            if (_consecutiveFailures >= FailureThreshold && !_notificationShown)
            {
                ShowConnectionDropNotification();
                _notificationShown = true;
            }

            // Add a spike value for failed pings in chart
            ChartValues.Add(-1);
            if (ChartValues.Count > MaxChartPoints)
            {
                ChartValues.RemoveAt(0);
            }
        }

        PacketLoss = _totalPings > 0 ? (double)_failedPings / _totalPings * 100 : 0;
    }

    private void ShowConnectionDropNotification()
    {
        try
        {
            var toastXml = new Windows.Data.Xml.Dom.XmlDocument();
            toastXml.LoadXml($@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>Connection Lost</text>
                            <text>Unable to reach {HostNameOrAddress} - {_consecutiveFailures} consecutive failures</text>
                        </binding>
                    </visual>
                    <audio src='ms-winsoundevent:Notification.Default' />
                </toast>");

            var toast = new Windows.UI.Notifications.ToastNotification(toastXml);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("Ping Legacy").Show(toast);
        }
        catch
        {
            // Ignore notification errors
        }
    }

    #endregion

    #region Network Methods

    private async Task SetNetworkInfoAsync()
    {
        try
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();

            _dispatcherQueue.TryEnqueue(() =>
            {
                LocalLANCollection = new ObservableCollection<NetworkInterface>(NetworkInterface.GetAllNetworkInterfaces());
            });

            if (icp?.NetworkAdapter == null)
            {
                _dispatcherQueue.TryEnqueue(ClearNetworkStatus);
                return;
            }

            var hasInternet = icp.GetNetworkConnectivityLevel() >= NetworkConnectivityLevel.InternetAccess;

            // Get all hostnames for this network adapter
            var hostnames = NetworkInformation.GetHostNames()
                .Where(hn => hn.IPInformation?.NetworkAdapter != null
                    && hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId)
                .ToList();

            // Find IPv4 and IPv6 addresses
            var ipv4Host = hostnames.FirstOrDefault(hn => hn.Type == Windows.Networking.HostNameType.Ipv4);
            var ipv6Host = hostnames.FirstOrDefault(hn => hn.Type == Windows.Networking.HostNameType.Ipv6);
            var hostname = ipv4Host ?? ipv6Host ?? hostnames.FirstOrDefault();

            if (hostname != null)
            {
                var currentNetworkId = "{" + hostname.IPInformation!.NetworkAdapter.NetworkAdapterId.ToString().ToUpper() + "}";

                _dispatcherQueue.TryEnqueue(() =>
                {
                    HasInternetAccess = hasInternet;
                    SelectedLocalLAN = LocalLANCollection.FirstOrDefault(x => x.Id.ToUpper() == currentNetworkId);
                    IpAddress = ipv4Host?.CanonicalName ?? hostname.CanonicalName;
                    Ipv6Address = ipv6Host?.CanonicalName ?? string.Empty;
                    IpType = hostname.Type.ToString();
                    ProfileName = icp.ProfileName;
                    IsWlan = icp.IsWlanConnectionProfile;
                    WifiBars = Convert.ToInt32(icp.GetSignalBars());

                    if (SelectedLocalLAN != null)
                    {
                        var ipStats = SelectedLocalLAN.GetIPStatistics();
                        TotalReceivedBytes = ipStats.BytesReceived / 1048576;
                        TotalSentBytes = ipStats.BytesSent / 1048576;
                        IsSupportIPV6 = SelectedLocalLAN.Supports(NetworkInterfaceComponent.IPv6);
                    }
                });
            }
        }
        catch
        {
            // Ignore network info errors
        }
    }

    private void ClearNetworkStatus()
    {
        IpAddress = string.Empty;
        IpType = string.Empty;
        ProfileName = string.Empty;
        HasInternetAccess = false;
    }

    private async Task DeleteOlderHistoryAsync()
    {
        try
        {
            await Task.Run(() => SQLiteHelper.DeleteOld(9));
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private bool IsValidHostNameOrAddress(string hostNameOrAddress)
    {
        if (string.IsNullOrEmpty(hostNameOrAddress)) return false;
        return IsValidHost(hostNameOrAddress, _ipRegex) || IsValidHost(hostNameOrAddress, _urlRegex);
    }

    private static bool IsValidHost(string url, string pattern)
    {
        var reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return reg.IsMatch(url);
    }

    public async Task StopPingOnNavigateAwayAsync()
    {
        if (IsPingStarted)
        {
            await StartPingAsync(); // Toggle off
        }
    }

    #endregion
}

