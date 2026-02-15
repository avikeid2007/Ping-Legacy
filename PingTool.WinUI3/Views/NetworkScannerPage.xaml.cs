using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PingTool.ViewModels;

namespace PingTool.Views;

public sealed partial class NetworkScannerPage : Page
{
    public NetworkScannerViewModel ViewModel { get; } = new();

    public NetworkScannerPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private Symbol GetScanSymbol(bool isScanning) => isScanning ? Symbol.Stop : Symbol.Play;

    private string GetScanText(bool isScanning) => isScanning ? "STOP SCAN" : "START SCAN";

    private Visibility GetEmptyVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private async void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartScanCommand.ExecuteAsync(null);
    }

    private void ScanTypeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.Tag is string scanType)
        {
            ViewModel.SelectedScanType = scanType;
        }
    }
}
