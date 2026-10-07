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
        SetDefaultTrayIcon();

        try
        {
            TrayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            // Shell_NotifyIcon can fail on some machines/sessions (e.g. no shell, restricted
            // sandbox); don't let a missing tray icon take down the whole app.
            Sentry.SentrySdk.CaptureException(ex);
        }

        // If there are active scheduled pings, keep the process alive in the background (hidden
        // behind the tray icon) instead of letting the system close terminate the app - scheduled
        // pings run on in-process timers (Services.ScheduledPingService) with no OS-level
        // background task, so they only keep running while this process is alive.
        AppWindow.Closing += AppWindow_Closing;

        Closed += (_, _) =>
        {
            // Stop any background work (e.g. streaming/scheduled pings) from touching UI that is
            // being torn down from this point on.
            if (!App.ShutdownCts.IsCancellationRequested)
            {
                App.ShutdownCts.Cancel();
            }

            if (!TrayIcon.IsDisposed)
            {
                TrayIcon.Dispose();
            }
        };
    }

    /// <summary>
    /// Loads the default (idle) tray icon from <c>Assets/DefaultIcon.png</c>.
    /// Decodes and resizes the bitmap synchronously via System.Drawing before converting it to a
    /// native HICON: assigning a WinUI <c>ImageSource</c> (e.g. BitmapImage) directly decodes
    /// asynchronously, and H.NotifyIcon's HICON conversion can run before the pixels are ready or
    /// against a non-square/oversized source, throwing "Argument 'picture' must be a picture that
    /// can be used as a Icon." (seen as a launch crash in Store reports).
    /// </summary>
    private void SetDefaultTrayIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultIcon.png");
            using var source = new System.Drawing.Bitmap(path);
            using var resized = new System.Drawing.Bitmap(source, new System.Drawing.Size(32, 32));
            var hIcon = resized.GetHicon();
            TrayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
        }
        catch (Exception ex)
        {
            // Worst case: the tray icon falls back to the OS default; don't crash the app over it.
            Sentry.SentrySdk.CaptureException(ex);
        }
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
        // Scheduled pings run on their own timers and can still be in-flight when the window/tray
        // icon is torn down (e.g. app exiting); avoid touching a disposed TrayIcon.
        if (TrayIcon.IsDisposed)
        {
            return;
        }

        var (color, glyph) = status switch
        {
            Services.TrayStatus.Online => (Microsoft.UI.Colors.SeaGreen, "\u2713"),
            Services.TrayStatus.Offline => (Microsoft.UI.Colors.Crimson, "\u2715"),
            _ => (Microsoft.UI.Colors.Gray, "?")
        };

        try
        {
            TrayIcon.ToolTipText = tooltip;

            // GeneratedIconSource renders via Win2D/Direct2D, which can throw a COMException on
            // machines/sessions without a usable GPU adapter; keep the previous icon if it fails.
            TrayIcon.IconSource = new H.NotifyIcon.GeneratedIconSource
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color),
                BackgroundType = H.NotifyIcon.BackgroundType.Ellipse,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                Text = glyph
            };
        }
        catch (ObjectDisposedException)
        {
            // Benign race: TrayIcon was disposed between the IsDisposed check and the update calls.
        }
        catch (Exception ex)
        {
            Sentry.SentrySdk.CaptureException(ex);
        }
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
