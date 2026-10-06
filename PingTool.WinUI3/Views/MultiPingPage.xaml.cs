using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PingTool.Models;
using PingTool.ViewModels;
using Windows.UI;

namespace PingTool.Views;

public sealed partial class MultiPingPage : Page
{
    public MultiPingViewModel ViewModel { get; } = new();

    public MultiPingPage()
    {
        InitializeComponent();
    }

    private void NewHostInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.AddTargetCommand.Execute(null);
        }
    }

    private void NewProfileNameInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SaveProfileCommand.Execute(null);
        }
    }

    private Visibility GetEmptyProfilesVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PingTarget target)
        {
            ViewModel.RemoveTargetCommand.Execute(target);
        }
    }

    private async void ShowDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PingTarget target)
        {
            var content = new StackPanel { Spacing = 4, MinWidth = 450, MaxWidth = 600 };

            // Add header info
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 12) };
            var statusDot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusDot);
            var headerText = new TextBlock
            {
                Text = $"Pinging {target.Hostname} ({target.IpAddress})",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            headerPanel.Children.Add(headerText);
            content.Children.Add(headerPanel);

            // Response count
            var countText = new TextBlock
            {
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(countText);

            // Scrollable ping history
            var scrollViewer = new ScrollViewer { MaxHeight = 350, MinHeight = 200 };
            var historyPanel = new StackPanel { Spacing = 2 };
            scrollViewer.Content = historyPanel;
            content.Children.Add(scrollViewer);

            // Stats panel
            var statsText = new TextBlock
            {
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            content.Children.Add(statsText);

            // Update function
            void UpdateUI()
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Update status dot color
                    statusDot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(GetStatusColor(target.Status));

                    // Update count
                    countText.Text = $"{target.SuccessCount + target.FailCount} responses";

                    // Update history
                    historyPanel.Children.Clear();
                    foreach (var ping in target.PingHistory)
                    {
                        var pingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                        var dot = new Border
                        {
                            Width = 8,
                            Height = 8,
                            CornerRadius = new CornerRadius(4),
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                                Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129)),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        pingRow.Children.Add(dot);

                        var text = new TextBlock
                        {
                            Text = $"Reply from {target.IpAddress}: bytes=32 time={ping}ms TTL=119",
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            FontSize = 12
                        };
                        pingRow.Children.Add(text);

                        historyPanel.Children.Add(pingRow);
                    }

                    // Auto-scroll to bottom
                    scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);

                    // Update stats
                    var statsParts = new List<string>();
                    statsParts.Add($"Sent={target.SuccessCount + target.FailCount}");
                    statsParts.Add($"Received={target.SuccessCount}");
                    statsParts.Add($"Lost={target.FailCount} ({target.PacketLoss:F1}%)");
                    if (target.SuccessCount > 0)
                    {
                        statsParts.Add($"Min={target.MinPing}ms");
                        statsParts.Add($"Max={target.MaxPing}ms");
                        statsParts.Add($"Avg={target.AvgPing:F1}ms");
                    }
                    statsText.Text = string.Join(" | ", statsParts);
                });
            }

            // Initial update
            UpdateUI();

            // Subscribe to changes
            void OnCollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => UpdateUI();
            target.PingHistory.CollectionChanged += OnCollectionChanged;

            var dialog = new ContentDialog
            {
                Title = "Ping Results (Live)",
                Content = content,
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();

            // Unsubscribe when dialog closes
            target.PingHistory.CollectionChanged -= OnCollectionChanged;
        }
    }

    public static Color GetStatusColor(PingStatus status)
    {
        return status switch
        {
            PingStatus.Success => Color.FromArgb(255, 16, 185, 129), // Green
            PingStatus.Failed => Color.FromArgb(255, 239, 68, 68),   // Red
            PingStatus.Pinging => Color.FromArgb(255, 245, 158, 11), // Yellow/Orange
            _ => Color.FromArgb(255, 107, 114, 128)                  // Gray
        };
    }

    public string GetToggleGlyph(bool isRunning) => isRunning ? "\uE71A" : "\uE768";
    public string GetToggleText(bool isRunning) => isRunning ? "Stop All" : "Start All";
    public Visibility GetEmptyVisibility(bool hasTargets) => hasTargets ? Visibility.Collapsed : Visibility.Visible;
}
