using Sentry;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PingTool.Helpers;

public static class FileHelper
{
    public static void CopyText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            // The clipboard is frequently locked by another process (RDP, clipboard
            // managers, etc.) - this must never crash the app.
            SentrySdk.CaptureException(ex);
        }
    }

    public static async Task SaveFileAsync(string content, string suggestedFileName)
    {
        try
        {
            var savePicker = new FileSavePicker();

            // Get the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text Files", new List<string> { ".txt" });
            savePicker.SuggestedFileName = suggestedFileName;

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                await FileIO.WriteTextAsync(file, content);
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
    }
}
