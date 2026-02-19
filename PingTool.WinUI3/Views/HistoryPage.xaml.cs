using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage.Pickers;

namespace PingTool.Views;

public sealed partial class HistoryPage : Page
{
    private HistoryType? _currentFilter;

    public ObservableCollection<HistoryItem> FilteredHistory { get; } = new();

    public HistoryPage()
    {
        InitializeComponent();
        HistoryService.Instance.HistoryChanged += OnHistoryChanged;
        LoadHistory();
    }

    private void OnHistoryChanged()
    {
        DispatcherQueue.TryEnqueue(() => LoadHistory());
    }

    private void LoadHistory()
    {
        FilteredHistory.Clear();
        var items = HistoryService.Instance.GetHistory(_currentFilter);
        foreach (var item in items)
        {
            FilteredHistory.Add(item);
        }
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = FilteredHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio)
        {
            if (radio == AllFilter || radio.Tag == null)
            {
                _currentFilter = null;
            }
            else if (radio.Tag is string tag)
            {
                _currentFilter = tag switch
                {
                    "Ping" => HistoryType.Ping,
                    "Traceroute" => HistoryType.Traceroute,
                    "PortScan" => HistoryType.PortScan,
                    "SpeedTest" => HistoryType.SpeedTest,
                    "MultiPing" => HistoryType.MultiPing,
                    "NetworkScan" => HistoryType.NetworkScan,
                    _ => null
                };
            }
            LoadHistory();
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var item = FilteredHistory.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                var content = HistoryService.Instance.ExportToText(item);

                try
                {
                    var picker = new FileSavePicker();
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
                    picker.SuggestedFileName = $"{item.TypeDisplay}_{item.Target}_{item.Timestamp:yyyyMMdd_HHmmss}";

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        await Windows.Storage.FileIO.WriteTextAsync(file, content);
                    }
                }
                catch
                {
                    // Fallback: copy to clipboard
                    FileHelper.CopyText(content);
                }
            }
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (FilteredHistory.Count == 0)
        {
            return;
        }

        var csv = HistoryService.Instance.ExportToCsv(FilteredHistory);

        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("CSV File", new List<string> { ".csv" });
            picker.SuggestedFileName = _currentFilter.HasValue
                ? $"history_{_currentFilter.Value}_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"history_all_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, csv);
            }
        }
        catch
        {
            FileHelper.CopyText(csv);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            HistoryService.Instance.RemoveHistory(id);
            var item = FilteredHistory.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                FilteredHistory.Remove(item);
            }
            UpdateEmptyState();
        }
    }

    private async void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear History",
            Content = _currentFilter.HasValue
                ? $"Are you sure you want to clear all {_currentFilter} history?"
                : "Are you sure you want to clear all history?",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            HistoryService.Instance.ClearHistory(_currentFilter);
            LoadHistory();
        }
    }
}
