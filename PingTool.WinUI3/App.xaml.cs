using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.UI.Xaml;
using PingTool.Helpers;
using PingTool.Services;

namespace PingTool;

/// <summary>
/// Ping Legacy - WinUI 3 Application
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();

        AppCenter.Start("6d7768e2-cf4e-41fb-a2b8-30c20c7ef36b",
            typeof(Analytics), typeof(Crashes));

        // Catch anything that slips through the framework's dispatch so the app can
        // log and survive instead of hard-crashing (this was previously unhandled).
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services
        SQLiteHelper.InitializeDatabase();

        _window = new MainWindow();
        MainWindow = _window;  // Set the static property so theme service can access it

        ThemeSelectorService.Initialize();  // Initialize AFTER window is created

        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Crashes.TrackError(e.Exception);
        // Keep the app alive for recoverable errors instead of terminating the process.
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Crashes.TrackError(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Crashes.TrackError(e.Exception);
        e.SetObserved();
    }

    public static Window? MainWindow { get; set; }
}
