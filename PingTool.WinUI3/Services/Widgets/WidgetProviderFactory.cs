using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace PingTool.Services.Widgets;

/// <summary>Boilerplate COM class factory needed so the OS can activate <see cref="WidgetProvider"/>
/// via CoCreateInstance when the Widgets Board requests it. Not specific to widgets - this is the
/// standard way to expose a WinRT/.NET type as an out-of-process COM server.</summary>
internal static class ComGuids
{
    public const string IClassFactory = "00000001-0000-0000-C000-000000000046";
    public const string IUnknown = "00000000-0000-0000-C000-000000000046";
}

[ComImport, ComVisible(false), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(ComGuids.IClassFactory)]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer(bool fLock);
}

[ComVisible(true)]
internal class WidgetProviderFactory<T> : IClassFactory
    where T : IWidgetProvider, new()
{
    private const int CLASS_E_NOAGGREGATION = -2147221232;
    private const int E_NOINTERFACE = -2147467262;

    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;

        if (pUnkOuter != IntPtr.Zero)
        {
            Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
        }

        if (riid == typeof(T).GUID || riid == Guid.Parse(ComGuids.IUnknown))
        {
            ppvObject = MarshalInspectable<IWidgetProvider>.FromManaged(new T());
        }
        else
        {
            Marshal.ThrowExceptionForHR(E_NOINTERFACE);
        }

        return 0;
    }

    int IClassFactory.LockServer(bool fLock) => 0;
}
