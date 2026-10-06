using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace PingTool.Services;

/// <summary>
/// Wraps the Windows Share Sheet (Share contract) for packaged WinUI 3 desktop apps.
/// WinUI 3 windows have no CoreWindow, so DataTransferManager.GetForCurrentView()/ShowShareUI()
/// aren't available - instead the per-window DataTransferManager must be obtained and shown via
/// the IDataTransferManagerInterop COM interop interface, keyed by the window's HWND.
/// </summary>
public static class ShareService
{
    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    private static readonly Guid DataTransferManagerIid =
        new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

    /// <summary>Shows the Share Sheet for <paramref name="window"/>, offering <paramref name="text"/> as plain text.</summary>
    public static void ShareText(Window window, string title, string text)
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var iid = DataTransferManagerIid;
        var dtmPointer = interop.GetForWindow(hWnd, ref iid);
        var dataTransferManager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(dtmPointer);

        dataTransferManager.DataRequested += OnDataRequested;

        void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            sender.DataRequested -= OnDataRequested;
            args.Request.Data.Properties.Title = title;
            args.Request.Data.SetText(text);
            args.Request.Data.RequestedOperation = DataPackageOperation.Copy;
        }

        interop.ShowShareUIForWindow(hWnd);
    }
}
