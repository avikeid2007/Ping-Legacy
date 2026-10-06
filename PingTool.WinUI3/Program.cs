using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PingTool.Services.Widgets;

namespace PingTool;

/// <summary>
/// Custom Main (replaces the XAML-generated one, via DISABLE_XAML_GENERATED_MAIN) so we can
/// register the Windows Widgets COM server (<see cref="WidgetProvider"/>) before the normal
/// WinUI app starts. The OS launches this exe via the Package.appxmanifest's com:ComServer /
/// uap3:AppExtension registration whenever it needs to activate the widget provider; the same
/// exe is also what a user double-clicks normally, so both paths go through this Main.
/// </summary>
public static class Program
{
    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    // Must match the com:Class Id / CreateInstance ClassId in Package.appxmanifest.
    private static readonly Guid WidgetProviderClsid = Guid.Parse("6abd8b48-5e94-443e-9641-90611cbdc06c");

    private const uint CLSCTX_LOCAL_SERVER = 0x4;
    private const uint REGCLS_MULTIPLEUSE = 0x1;

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        try
        {
            CoRegisterClassObject(
                WidgetProviderClsid,
                new WidgetProviderFactory<WidgetProvider>(),
                CLSCTX_LOCAL_SERVER,
                REGCLS_MULTIPLEUSE,
                out _);
        }
        catch (Exception ex)
        {
            // Non-fatal: the Widgets Board may be unavailable (e.g. pre-Windows 11), and the
            // normal app UI should still start regardless.
            Sentry.SentrySdk.CaptureException(ex);
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }
}
