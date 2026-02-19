using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PingTool.Services;
using Windows.ApplicationModel;

namespace PingTool.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Set current theme selection
        var currentTheme = ThemeSelectorService.Theme;
        switch (currentTheme)
        {
            case ElementTheme.Light:
                LightThemeRadio.IsChecked = true;
                break;
            case ElementTheme.Dark:
                DarkThemeRadio.IsChecked = true;
                break;
            default:
                SystemThemeRadio.IsChecked = true;
                break;
        }

        // Set auto-start toggle
        var isPingAutoStart = SettingsHelper.Read<bool?>("IsPingAutoStart") ?? true;
        AutoStartToggle.IsOn = isPingAutoStart;

        // Set notification toggle
        var notificationsEnabled = SettingsHelper.Read<bool?>("NotificationsEnabled") ?? true;
        NotificationToggle.IsOn = notificationsEnabled;

        // Set version
        try
        {
            var version = Package.Current.Id.Version;
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }
        catch { }

        // Set retention days
        RetentionDaysBox.Value = HistoryService.RetentionDays;
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string themeTag)
        {
            if (Enum.TryParse<ElementTheme>(themeTag, out var theme))
            {
                ThemeSelectorService.Theme = theme;
            }
        }
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsHelper.Save("IsPingAutoStart", AutoStartToggle.IsOn);
    }

    private void NotificationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsHelper.Save("NotificationsEnabled", NotificationToggle.IsOn);
    }

    private void RetentionDaysBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue))
        {
            HistoryService.RetentionDays = (int)args.NewValue;
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Ping History",
            Content = "Are you sure you want to clear all ping history data? This action cannot be undone.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            // Clear ping database
            try
            {
                Helpers.SQLiteHelper.ClearAllPingMessages();
                await ShowSuccessDialog("Ping history cleared successfully.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to clear history: {ex.Message}");
            }
        }
    }

    private async void ClearUnifiedHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear All History",
            Content = "This will remove all history from Ping, Multi-Ping, Traceroute, Port Scan, Network Scan, and Speed Test. Continue?",
            PrimaryButtonText = "Clear All",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            HistoryService.Instance.ClearHistory();
            await ShowSuccessDialog("All history cleared successfully.");
        }
    }

    private async Task ShowSuccessDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Success",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
