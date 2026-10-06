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

    /// <summary>
    /// Shows a save picker offering multiple file formats (one entry per extension in
    /// <paramref name="contentByExtension"/>) and writes whichever content matches the
    /// extension the user actually chose.
    /// </summary>
    public static async Task SaveExportAsync(string suggestedFileNameWithoutExtension, IReadOnlyDictionary<string, string> contentByExtension)
    {
        try
        {
            var savePicker = new FileSavePicker();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            foreach (var extension in contentByExtension.Keys)
            {
                var label = extension.TrimStart('.').ToUpperInvariant() + " File";
                savePicker.FileTypeChoices.Add(label, new List<string> { extension });
            }

            savePicker.SuggestedFileName = suggestedFileNameWithoutExtension;

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var extension = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                var content = contentByExtension.TryGetValue(extension, out var matched)
                    ? matched
                    : contentByExtension.Values.First();
                await FileIO.WriteTextAsync(file, content);
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
    }

    /// <summary>Escapes a value for use as a CSV field (quotes it if it contains a comma, quote, or newline).</summary>
    public static string ToCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

