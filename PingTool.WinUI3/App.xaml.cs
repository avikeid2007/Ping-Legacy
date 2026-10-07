using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml;
using PingTool.Helpers;
using PingTool.Services;
using Sentry;
using Sentry.Protocol;

namespace PingTool;

/// <summary>
/// Ping Legacy - WinUI 3 Application
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Signaled when the app is genuinely shutting down (MainWindow.Closed), so long-running
    /// background work (e.g. scheduled/streaming pings) can stop touching UI elements that are
    /// about to be torn down instead of throwing COMException/ObjectDisposedException.
    /// </summary>
    public static readonly CancellationTokenSource ShutdownCts = new();

    private Window? _window;

    public App()
    {
        // Must init before InitializeComponent so startup exceptions are captured too.
        // DSN is baked in at build time via AssemblyMetadata from the SENTRY_DSN env var/CI
        // secret - never hardcoded here, so it's never committed to source control.
        SentrySdk.Init(options =>
        {
            // Sentry throws if Dsn is left null; an explicit "" is how you disable it.
            options.Dsn = GetBuildTimeDsn() ?? string.Empty;
            // Keep off: no need for IP/computer name on crash reports for this app.
            options.SendDefaultPii = false;
            options.TracesSampleRate = 1.0;
            // Client app, not a server handling concurrent requests.
            options.IsGlobalModeEnabled = true;
#if DEBUG
            options.Debug = true;
#endif
        });

        InitializeComponent();

        // Catch anything that slips through the framework's dispatch so the app can
        // log and survive instead of hard-crashing (this was previously unhandled).
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static string? GetBuildTimeDsn() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SentryDsn")?.Value;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services
        SQLiteHelper.InitializeDatabase();

        _window = new MainWindow();
        MainWindow = _window;  // Set the static property so theme service can access it

        if (!string.IsNullOrEmpty(args.Arguments))
        {
            NavigationService.PendingLaunchPage = JumpListHelper.ResolveArguments(args.Arguments);
        }
        _ = JumpListHelper.UpdateAsync();

        ThemeSelectorService.Initialize();  // Initialize AFTER window is created

        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        TrackUnhandled(e.Exception);
        // Keep the app alive for recoverable errors instead of terminating the process.
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            TrackUnhandled(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TrackUnhandled(e.Exception);
        e.SetObserved();
    }

    private static void TrackUnhandled(Exception ex)
    {
        ex.Data[Mechanism.HandledKey] = false;
        ex.Data[Mechanism.MechanismKey] = "Application.UnhandledException";
        SentrySdk.CaptureException(ex);
    }

    public static Window? MainWindow { get; set; }
}
