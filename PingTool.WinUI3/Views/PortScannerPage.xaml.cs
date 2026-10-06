using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace PingTool.Views;

public sealed partial class PortScannerPage : Page
{

    private readonly PortScannerService _scannerService = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _disclaimerAccepted;
    private string _currentHost = string.Empty;

    public ObservableCollection<PortScanResult> Results { get; } = new();

    public PortScannerPage()
    {
        InitializeComponent();

        LoadDisclaimer();
    }

    private void LoadDisclaimer()
    {
        _disclaimerAccepted = SettingsHelper.Read<bool?>("PortScanDisclaimerAccepted") ?? false;
        UpdateDisclaimerVisibility();
    }

    private void UpdateDisclaimerVisibility()
    {
        DisclaimerPanel.Visibility = _disclaimerAccepted ? Visibility.Collapsed : Visibility.Visible;
        ScannerPanel.Visibility = _disclaimerAccepted ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AcceptDisclaimer_Click(object sender, RoutedEventArgs e)
    {
        _disclaimerAccepted = true;
        SettingsHelper.Save("PortScanDisclaimerAccepted", true);
        UpdateDisclaimerVisibility();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            _isRunning = false;
            ScanButtonText.Text = "Scan";
            ScanButtonIcon.Glyph = "\uE768";
            ProgressBar.Visibility = Visibility.Collapsed;
            return;
        }

        var host = NormalizeHost(HostInput.Text?.Trim());
        if (string.IsNullOrEmpty(host)) return;

        var confirmed = await ShowAuthorizationDialogAsync(host);
        if (!confirmed) return;

        _currentHost = host;
        await ExecuteScanAsync(host);
    }

    /// <summary>
    /// Accepts IPv6 addresses typed with URL-style brackets (e.g. "[::1]" or "[2001:db8::1]:8080",
    /// as commonly copy-pasted from a browser address bar) and unwraps them to the bare literal
    /// ("::1", "2001:db8::1") that TcpClient/Dns expect. Plain hostnames and unbracketed IPv4/IPv6
    /// literals are returned unchanged.
    /// </summary>
    private static string NormalizeHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var host = input.Trim();
        if (host.StartsWith('['))
        {
            var closingBracket = host.IndexOf(']');
            if (closingBracket > 0)
            {
                host = host.Substring(1, closingBracket - 1);
            }
        }

        return host;
    }

    /// <summary>
    /// Resolves the host before scanning and shows the actual address/family (IPv4 or IPv6) that
    /// will be targeted, so the user can confirm an IPv6-only or dual-stack host resolved correctly.
    /// </summary>
    private static async Task<UIElement> BuildResolvedAddressPanelAsync(string host)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0),
            Spacing = 8
        };
        panel.Children.Add(new TextBlock { Text = "Resolves To:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
            var address = addresses.FirstOrDefault();
            if (address != null)
            {
                var family = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4";
                panel.Children.Add(new TextBlock { Text = $"{address} ({family})" });
            }
            else
            {
                panel.Children.Add(new TextBlock { Text = "Could not resolve", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"] });
            }
        }
        catch
        {
            panel.Children.Add(new TextBlock { Text = "Could not resolve", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"] });
        }

        return panel;
    }

    private async Task<bool> ShowAuthorizationDialogAsync(string host)
    {
        var checkBox = new CheckBox
        {
            Content = "I confirm that I own this system or have permission to scan it.",
            Margin = new Thickness(0, 12, 0, 0)
        };

        var content = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "You are about to perform a port scan on the target system.\n\n" +
                           "Port scanning should only be done on systems you own or have explicit permission to test. " +
                           "Unauthorized scanning may be illegal and could violate network or service provider policies.",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 16, 0, 0),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Target:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = host, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"] }
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 0),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Scan Type:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = "Port Scan" }
                    }
                },
                await BuildResolvedAddressPanelAsync(host),
                checkBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Port Scan Authorization",
            Content = content,
            PrimaryButtonText = "Start Scan",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
            IsPrimaryButtonEnabled = false
        };

        checkBox.Checked += (s, e) => dialog.IsPrimaryButtonEnabled = true;
        checkBox.Unchecked += (s, e) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task ExecuteScanAsync(string host)
    {
        Results.Clear();
        _isRunning = true;
        ScanButtonText.Text = "Stop";
        ScanButtonIcon.Glyph = "\uE711";
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        EmptyState.Visibility = Visibility.Collapsed;
        StatsPanel.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();

        int open = 0, closed = 0, filtered = 0;
        var ports = PortScannerService.CommonPorts;
        int current = 0;

        try
        {
            await foreach (var result in _scannerService.ScanPortsAsync(host, ports, cancellationToken: _cts.Token))
            {
                Results.Add(result);
                current++;
                ProgressBar.Value = (current / (double)ports.Length) * 100;

                if (result.IsOpen)
                    open++;
                else if (result.Status.Contains("Filtered"))
                    filtered++;
                else
                    closed++;

                OpenCount.Text = open.ToString();
                ClosedCount.Text = closed.ToString();
                FilteredCount.Text = filtered.ToString();
            }

            // Save to history after scan completes
            if (Results.Count > 0)
            {
                SaveToHistory(host, open, closed, filtered);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        finally
        {
            _isRunning = false;
            ScanButtonText.Text = "Scan";
            ScanButtonIcon.Glyph = "\uE768";
            ProgressBar.Visibility = Visibility.Collapsed;
            if (Results.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                StatsPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SaveToHistory(string host, int open, int closed, int filtered)
    {
        var sb = new StringBuilder();
        foreach (var result in Results)
        {
            sb.AppendLine($"Port {result.Port} ({result.ServiceName}): {result.Status}");
        }

        var historyItem = new HistoryItem
        {
            Type = HistoryType.PortScan,
            Target = host,
            IsSuccess = open > 0,
            OpenPorts = open,
            ClosedPorts = closed + filtered,
            TotalPorts = Results.Count,
            Summary = $"{open} open, {closed} closed, {filtered} filtered ports",
            Details = sb.ToString()
        };

        HistoryService.Instance.AddHistory(historyItem);
    }

    private void HostInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && !_isRunning && _disclaimerAccepted)
        {
            ScanButton_Click(sender, e);
        }
    }
}
