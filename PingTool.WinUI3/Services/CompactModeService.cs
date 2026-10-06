using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace PingTool.Services;

/// <summary>
/// Shrinks the main window into a small always-on-top floating widget ("compact mode") and
/// restores it back to its previous size/position - the WinUI3/Windows App SDK equivalent of
/// the UWP app's ApplicationViewMode.CompactOverlay feature.
/// </summary>
public static class CompactModeService
{
    private const int CompactWidth = 340;
    private const int CompactHeight = 260;

    private static RectInt32? _savedBounds;

    public static bool IsCompact { get; private set; }

    /// <summary>Raised after entering/exiting compact mode so the shell can hide/show its nav pane.</summary>
    public static event Action<bool>? CompactModeChanged;

    public static void Enter(Window? window)
    {
        if (IsCompact || window == null) return;

        var appWindow = GetAppWindow(window);
        if (appWindow == null) return;

        _savedBounds = new RectInt32(appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
        }

        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var x = workArea.X + workArea.Width - CompactWidth - 24;
        var y = workArea.Y + workArea.Height - CompactHeight - 24;

        appWindow.MoveAndResize(new RectInt32(x, y, CompactWidth, CompactHeight));
        IsCompact = true;
        CompactModeChanged?.Invoke(true);
    }

    public static void Exit(Window? window)
    {
        if (!IsCompact || window == null) return;

        var appWindow = GetAppWindow(window);
        if (appWindow == null) return;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = false;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        if (_savedBounds is { } bounds)
        {
            appWindow.MoveAndResize(bounds);
        }

        IsCompact = false;
        CompactModeChanged?.Invoke(false);
    }

    private static AppWindow? GetAppWindow(Window window)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }
}
