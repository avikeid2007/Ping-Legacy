using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PingTool.Models;
using PingTool.Services;

namespace PingTool.Views;

public sealed partial class SpeedTestPage : Page
{

    private readonly SpeedTestService _speedTestService = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public SpeedTestPage()
    {
        InitializeComponent();

        _speedTestService.ProgressUpdated += OnProgressUpdated;
        _speedTestService.StatusUpdated += OnStatusUpdated;
    }

    private void OnProgressUpdated(double progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ProgressBar.Value = progress;
        });
    }

    private void OnStatusUpdated(string status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusText.Text = status;

            // Pulse whichever metric's icon matches the phase currently being measured.
            if (status.Contains("download", StringComparison.OrdinalIgnoreCase))
            {
                UploadPulseStoryboard.Stop();
                DownloadPulseStoryboard.Begin();
            }
            else if (status.Contains("upload", StringComparison.OrdinalIgnoreCase))
            {
                DownloadPulseStoryboard.Stop();
                UploadPulseStoryboard.Begin();
            }
            else
            {
                DownloadPulseStoryboard.Stop();
                UploadPulseStoryboard.Stop();
            }
        });
    }

    /// <summary>Eases a TextBlock's displayed number up from 0 to <paramref name="toValue"/>.</summary>
    private static async Task AnimateNumberAsync(TextBlock target, double toValue, Func<double, string> format, int durationMs = 650)
    {
        const int frameMs = 16;
        var steps = Math.Max(1, durationMs / frameMs);
        for (var i = 1; i <= steps; i++)
        {
            var eased = 1 - Math.Pow(1 - (double)i / steps, 3);
            target.Text = format(toValue * eased);
            await Task.Delay(frameMs);
        }
        target.Text = format(toValue);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            _isRunning = false;
            StartButtonText.Text = "Start Test";
            StartButtonIcon.Glyph = "\uE768";
            ProgressBar.Visibility = Visibility.Collapsed;
            DownloadPulseStoryboard.Stop();
            UploadPulseStoryboard.Stop();
            return;
        }

        _isRunning = true;
        StartButtonText.Text = "Stop";
        StartButtonIcon.Glyph = "\uE711";
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;

        // Reset displays
        DownloadSpeed.Text = "--";
        UploadSpeed.Text = "--";
        Latency.Text = "-- ms";

        _cts = new CancellationTokenSource();

        try
        {
            var result = await _speedTestService.RunSpeedTestAsync(_cts.Token);

            if (result.IsSuccess)
            {
                ResultPopStoryboard.Begin();
                await Task.WhenAll(
                    AnimateNumberAsync(DownloadSpeed, result.DownloadSpeedMbps, v => v.ToString("F1")),
                    AnimateNumberAsync(UploadSpeed, result.UploadSpeedMbps, v => v.ToString("F1")),
                    AnimateNumberAsync(Latency, result.LatencyMs, v => $"{v:F0} ms"));

                StatusText.Text = $"Test completed at {result.TestTime:HH:mm:ss}";

                // Save to history
                SaveToHistory(result);
            }
            else
            {
                StatusText.Text = result.Error ?? "Test failed";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Test cancelled";
        }
        finally
        {
            _isRunning = false;
            StartButtonText.Text = "Start Test";
            StartButtonIcon.Glyph = "\uE768";
            ProgressBar.Visibility = Visibility.Collapsed;
            DownloadPulseStoryboard.Stop();
            UploadPulseStoryboard.Stop();
        }
    }

    private void SaveToHistory(SpeedTestResult result)
    {
        var historyItem = new HistoryItem
        {
            Type = HistoryType.SpeedTest,
            Target = "Internet Speed Test",
            IsSuccess = result.IsSuccess,
            DownloadSpeed = result.DownloadSpeedMbps,
            UploadSpeed = result.UploadSpeedMbps,
            Latency = result.LatencyMs,
            Summary = $"Download: {result.DownloadSpeedMbps:F1} Mbps, Upload: {result.UploadSpeedMbps:F1} Mbps, Latency: {result.LatencyMs}ms",
            Details = $"Test completed at {result.TestTime:HH:mm:ss}\n" +
                     $"Download Speed: {result.DownloadSpeedMbps:F1} Mbps\n" +
                     $"Upload Speed: {result.UploadSpeedMbps:F1} Mbps\n" +
                     $"Latency: {result.LatencyMs}ms"
        };

        HistoryService.Instance.AddHistory(historyItem);
    }
}
