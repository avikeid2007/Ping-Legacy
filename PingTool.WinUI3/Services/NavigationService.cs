using Microsoft.UI.Xaml.Controls;

namespace PingTool.Services;

public static class NavigationService
{
    private static Frame? _frame;

    public static Frame? Frame
    {
        get => _frame;
        set => _frame = value;
    }

    public static bool CanGoBack => Frame?.CanGoBack ?? false;

    /// <summary>Set by App.OnLaunched when the app was launched from a jump list item, before
    /// ShellPage's default "navigate to MainPage" Loaded handler runs. ShellPage consumes and
    /// clears this once it navigates, so it only affects the launch that set it.</summary>
    public static Type? PendingLaunchPage { get; set; }

    public static void GoBack()
    {
        if (CanGoBack)
        {
            Frame?.GoBack();
        }
    }

    public static bool Navigate(Type pageType, object? parameter = null)
    {
        if (Frame == null) return false;
        return Frame.Navigate(pageType, parameter);
    }

    public static bool Navigate<T>(object? parameter = null) where T : Page
    {
        return Navigate(typeof(T), parameter);
    }
}
