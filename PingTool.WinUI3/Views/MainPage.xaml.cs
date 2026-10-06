using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using PingTool.Services;
using PingTool.ViewModels;
using Windows.System;
using Windows.UI;

namespace PingTool.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; } = new();

    // Gradient brushes for dynamic binding
    private static readonly LinearGradientBrush SuccessGradient = CreateGradient("#11998e", "#38ef7d");
    private static readonly LinearGradientBrush WarningGradient = CreateGradient("#f093fb", "#f5576c");
    private static readonly LinearGradientBrush AccentGradient = CreateGradient("#667eea", "#764ba2");

    public MainPage()
    {
        InitializeComponent();
        DataContext = ViewModel;

        // Subscribe to chart data changes
        ViewModel.ChartValues.CollectionChanged += (s, e) => DrawChart();

        // Resize/restore the actual OS window whenever compact mode is toggled.
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.IsCompactMode)) return;

        if (ViewModel.IsCompactMode)
        {
            CompactModeService.Enter(App.MainWindow);
        }
        else
        {
            CompactModeService.Exit(App.MainWindow);
        }
    }

    private void ShareResults_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PingCollection.Count == 0 || App.MainWindow is not Window window) return;

        ShareService.ShareText(window, $"Ping results - {ViewModel.HostNameOrAddress}", ViewModel.BuildShareText());
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        await ViewModel.StopPingOnNavigateAwayAsync();
    }

    private void TxtAddress_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.StartPingCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        await ViewModel.HandleKeyboardShortcutAsync(e.Key, isCtrlPressed);
    }

    private async void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Text is string host)
        {
            await ViewModel.SelectFavoriteCommand.ExecuteAsync(host);
        }
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddToFavoritesCommand.Execute(null);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate<SettingsPage>();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChart();
    }

    private void DrawChart()
    {
        if (ChartCanvas == null || ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
            return;

        ChartCanvas.Children.Clear();

        var values = ViewModel.ChartValues.Where(v => v >= 0).ToList();
        if (values.Count < 2)
        {
            // Draw empty state message
            var emptyText = new TextBlock
            {
                Text = "Chart will appear here when pinging...",
                Foreground = new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(emptyText, ChartCanvas.ActualWidth / 2 - 100);
            Canvas.SetTop(emptyText, ChartCanvas.ActualHeight / 2 - 10);
            ChartCanvas.Children.Add(emptyText);
            return;
        }

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight - 10; // Leave padding
        var maxValue = Math.Max(values.Max() * 1.2, 50); // 20% headroom, min 50
        var stepX = width / (values.Count - 1);

        // Draw horizontal grid lines
        for (int i = 0; i <= 3; i++)
        {
            var y = height * i / 3;
            var gridLine = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            ChartCanvas.Children.Add(gridLine);
        }

        // Create path for gradient fill under the line
        var fillPath = new Microsoft.UI.Xaml.Shapes.Path
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Color = Color.FromArgb(80, 102, 126, 234), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(10, 102, 126, 234), Offset = 1 }
                }
            },
            StrokeThickness = 0
        };

        var fillGeometry = new PathGeometry();
        var fillFigure = new PathFigure { StartPoint = new Windows.Foundation.Point(0, height) };

        // Create line path with thicker stroke
        var linePath = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(255, 102, 126, 234)),
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var lineGeometry = new PathGeometry();
        var lineFigure = new PathFigure();

        for (int i = 0; i < values.Count; i++)
        {
            var x = i * stepX;
            var y = height - (values[i] / maxValue * height);

            if (i == 0)
            {
                lineFigure.StartPoint = new Windows.Foundation.Point(x, y);
                fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, y) });
            }
            else
            {
                lineFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, y) });
                fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, y) });
            }
        }

        // Close fill path
        fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point((values.Count - 1) * stepX, height) });
        fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(0, height) });

        fillGeometry.Figures.Add(fillFigure);
        lineGeometry.Figures.Add(lineFigure);

        fillPath.Data = fillGeometry;
        linePath.Data = lineGeometry;

        ChartCanvas.Children.Add(fillPath);
        ChartCanvas.Children.Add(linePath);

        // Draw current value indicator (last point)
        if (values.Count > 0)
        {
            var lastX = (values.Count - 1) * stepX;
            var lastY = height - (values.Last() / maxValue * height);
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(255, 102, 126, 234)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, lastX - 4);
            Canvas.SetTop(dot, lastY - 4);
            ChartCanvas.Children.Add(dot);
        }
    }

    private static LinearGradientBrush CreateGradient(string color1, string color2)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = ParseColor(color1), Offset = 0 },
                new GradientStop { Color = ParseColor(color2), Offset = 1 }
            }
        };
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(255,
            byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
    }

    // Helper methods for x:Bind
    public Symbol GetStartStopSymbol(bool isPingStarted) =>
        isPingStarted ? Symbol.Pause : Symbol.Play;

    public string GetStartStopText(bool isPingStarted) =>
        isPingStarted ? "Stop" : "Start";

    public string GetConnectionGlyph(bool isWlan) =>
        isWlan ? "\uE701" : "\uE839";

    public Brush GetConnectionColor(bool hasInternet) =>
        hasInternet
            ? new SolidColorBrush(Colors.Green)
            : new SolidColorBrush(Colors.Red);

    public string GetStatusText(bool hasInternet) =>
        hasInternet ? "Connected" : "Disconnected";

    public Visibility GetEmptyVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetFullModeVisibility(bool isCompactMode) =>
        isCompactMode ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GetCompactModeVisibility(bool isCompactMode) =>
        isCompactMode ? Visibility.Visible : Visibility.Collapsed;

    public string GetLatestPingSummary(int count)
    {
        if (count == 0 || ViewModel.PingCollection.Count == 0) return "--";
        var latest = ViewModel.PingCollection[^1];
        return $"{latest.Time} ms";
    }

    private void ExitCompactMode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleCompactModeCommand.Execute(null);
    }

    public string FormatAvg(double avg) =>
        avg.ToString("F1");

    public string FormatLoss(double loss) =>
        loss.ToString("F1");

    public Brush GetLossColor(double loss) =>
        loss switch
        {
            0 => new SolidColorBrush(Colors.Green),
            < 5 => new SolidColorBrush(Colors.Orange),
            _ => new SolidColorBrush(Colors.Red)
        };

    public Brush GetLossGradient(double loss) =>
        loss switch
        {
            0 => SuccessGradient,
            < 5 => AccentGradient,
            _ => WarningGradient
        };

    #region DNS Lookup Helpers

    public Visibility GetVisibility(bool isVisible) =>
        isVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetDnsResultVisibility(DnsLookupResult? result) =>
        result?.IsSuccess == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetDnsErrorVisibility(DnsLookupResult? result) =>
        result != null && !result.IsSuccess ? Visibility.Visible : Visibility.Collapsed;

    public string GetDnsIpv4(DnsLookupResult? result) =>
        result?.IPv4Addresses?.Count > 0 ? string.Join(", ", result.IPv4Addresses) : "None";

    public string GetDnsIpv6(DnsLookupResult? result) =>
        result?.IPv6Addresses?.Count > 0 ? string.Join(", ", result.IPv6Addresses) : "None";

    public string GetDnsCanonical(DnsLookupResult? result) =>
        result?.CanonicalName ?? "";

    public string GetDnsError(DnsLookupResult? result) =>
        result?.Error ?? "";

    public Visibility GetVisibility(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    #endregion
}
