using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace PingTool.Helpers
{
    public static class FileHelper
    {
        public static void CopyText(string text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        public static async Task<bool> SaveFileAsync(string text, string fileName = "")
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("Text File", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = fileName;
            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                await Windows.Storage.FileIO.WriteTextAsync(file, text);
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
