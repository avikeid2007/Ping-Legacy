using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace PingTool;

public sealed partial class MainWindow : Window
{
    private bool _isExiting;
    private bool _hasShownBackgroundToast;

    public MainWindow()
    {
        InitializeComponent();

        // Set window title
        Title = "Ping Legacy";

        // Store reference to main window for later use
        App.MainWindow = this;

        TrayIcon.LeftClickCommand = new RelayCommand(ShowAndActivate);
        TrayIcon.DoubleClickCommand = new RelayCommand(ShowAndActivate);
        TrayIcon.ForceCreate();

        // If there are active scheduled pings, keep the process alive in the background (hidden
        // behind the tray icon) instead of letting the system close terminate the app - scheduled
        // pings run on in-process timers (Services.ScheduledPingService) with no OS-level
        // background task, so they only keep running while this process is alive.
        AppWindow.Closing += AppWindow_Closing;

        Closed += (_, _) =>
        {
            if (!TrayIcon.IsDisposed)
            {
                TrayIcon.Dispose();
            }
        };
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;

        var hasActiveSchedules = Services.ScheduledPingService.Instance.ScheduledPings.Any(p => p.IsEnabled);
        if (!hasActiveSchedules) return;

        // Keep the process (and its scheduled-ping timers) alive - hide to tray instead of closing.
        args.Cancel = true;
        AppWindow.Hide();

        if (!_hasShownBackgroundToast)
        {
            _hasShownBackgroundToast = true;
            TrayIcon.ShowNotification(
                "Ping Legacy is still running",
                "Scheduled pings keep running in the background. Right-click the tray icon to exit.");
        }
    }

    /// <summary>Updates the tray icon's live color/glyph and tooltip to reflect the current ping status.</summary>
    public void UpdateTrayStatus(string tooltip, PingTool.Services.TrayStatus status)
    {
        TrayIcon.ToolTipText = tooltip;

        var (color, glyph) = status switch
        {
            Services.TrayStatus.Online => (Microsoft.UI.Colors.SeaGreen, "\u2713"),
            Services.TrayStatus.Offline => (Microsoft.UI.Colors.Crimson, "\u2715"),
            _ => (Microsoft.UI.Colors.Gray, "?")
        };

        TrayIcon.IconSource = new H.NotifyIcon.GeneratedIconSource
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color),
            BackgroundType = H.NotifyIcon.BackgroundType.Ellipse,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            Text = glyph
        };
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e) => ShowAndActivate();

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Application.Current.Exit();
    }

    private void ShowAndActivate()
    {
        AppWindow.Show();
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }

        Activate();
    }
}
