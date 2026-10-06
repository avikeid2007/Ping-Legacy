using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace PingTool.Views;

public sealed partial class TraceroutePage : Page
{

    private readonly TracerouteService _tracerouteService = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public ObservableCollection<TracerouteHop> Hops { get; } = new();

    public TraceroutePage()
    {
        InitializeComponent();

    }

    private async void TraceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            _isRunning = false;
            TraceButtonText.Text = "Trace";
            ProgressIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var host = HostInput.Text?.Trim();
        if (string.IsNullOrEmpty(host)) return;

        var confirmed = await ShowAuthorizationDialogAsync(host);
        if (!confirmed) return;

        await ExecuteTracerouteAsync(host);
    }

    private async Task<bool> ShowAuthorizationDialogAsync(string host)
    {
        var checkBox = new CheckBox
        {
            Content = "I confirm that I own this system or have permission to test it.",
            Margin = new Thickness(0, 12, 0, 0)
        };

        var content = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "You are about to run a traceroute to analyze the network path to the target system.\n\n" +
                           "Traceroute should only be used for troubleshooting systems you own or are authorized to test. " +
                           "Repeated or unauthorized use may violate policies or laws.",
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
                        new TextBlock { Text = "Test Type:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = "Traceroute" }
                    }
                },
                checkBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Traceroute Authorization",
            Content = content,
            PrimaryButtonText = "Start Traceroute",
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

    private async Task ExecuteTracerouteAsync(string host)
    {
        Hops.Clear();
        _isRunning = true;
        TraceButtonText.Text = "Stop";
        ProgressIndicator.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        _cts = new CancellationTokenSource();

        try
        {
            await foreach (var hop in _tracerouteService.TraceAsync(host, _cts.Token))
            {
                Hops.Add(hop);
            }

            if (Hops.Count > 0)
            {
                SaveToHistory(host);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        finally
        {
            _isRunning = false;
            TraceButtonText.Text = "Trace";
            ProgressIndicator.Visibility = Visibility.Collapsed;
            if (Hops.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
            }
        }
    }

    private void SaveToHistory(string host)
    {
        var sb = new StringBuilder();
        foreach (var hop in Hops)
        {
            sb.AppendLine($"Hop {hop.HopNumber}: {hop.IpAddress} ({hop.Hostname}) - {hop.LatencyDisplay}");
        }

        var lastHop = Hops.LastOrDefault(h => !string.IsNullOrEmpty(h.IpAddress) && h.IpAddress != "*");

        var historyItem = new HistoryItem
        {
            Type = HistoryType.Traceroute,
            Target = host,
            DomainName = lastHop?.Hostname != lastHop?.IpAddress ? lastHop?.Hostname : null,
            IsSuccess = Hops.Any(h => h.IsSuccess),
            HopCount = Hops.Count,
            Summary = $"{Hops.Count} hops to {host}",
            Details = sb.ToString()
        };

        HistoryService.Instance.AddHistory(historyItem);
    }

    private void HostInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && !_isRunning)
        {
            TraceButton_Click(sender, e);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Hops.Count == 0) return;

        var host = HostInput.Text?.Trim() ?? string.Empty;

        await FileHelper.SaveExportAsync("traceroute-results", new Dictionary<string, string>
        {
            [".txt"] = BuildTextExport(host),
            [".csv"] = BuildCsvExport(),
            [".json"] = BuildJsonExport(host)
        });
    }

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        if (Hops.Count == 0 || App.MainWindow is not Window window) return;

        var host = HostInput.Text?.Trim() ?? string.Empty;
        ShareService.ShareText(window, $"Traceroute to {host}", BuildTextExport(host));
    }

    private string BuildTextExport(string host)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Traceroute Results ===");
        sb.AppendLine($"Target: {host}");
        sb.AppendLine($"Hops: {Hops.Count}");
        sb.AppendLine("===========================");
        sb.AppendLine();
        foreach (var hop in Hops)
        {
            sb.AppendLine($"Hop {hop.HopNumber}: {hop.IpAddress} ({hop.Hostname}) - {hop.LatencyDisplay}");
        }
        return sb.ToString();
    }

    private string BuildCsvExport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hop,IpAddress,Hostname,Latency1Ms,Latency2Ms,Latency3Ms,Status");
        foreach (var hop in Hops)
        {
            sb.AppendLine(string.Join(",",
                hop.HopNumber,
                FileHelper.ToCsvField(hop.IpAddress),
                FileHelper.ToCsvField(hop.Hostname),
                hop.Latency1,
                hop.Latency2,
                hop.Latency3,
                hop.Status));
        }
        return sb.ToString();
    }

    private string BuildJsonExport(string host)
    {
        var export = new
        {
            Target = host,
            ExportedAt = DateTimeOffset.Now,
            HopCount = Hops.Count,
            Hops = Hops.Select(hop => new
            {
                hop.HopNumber,
                hop.IpAddress,
                hop.Hostname,
                hop.Latency1,
                hop.Latency2,
                hop.Latency3,
                hop.Status
            })
        };
        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }
}
