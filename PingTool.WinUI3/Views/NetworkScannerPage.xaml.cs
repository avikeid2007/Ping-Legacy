using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PingTool.Services;
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
        if (!ViewModel.IsScanning && ViewModel.LegalNoticeAcknowledged)
        {
            var isPublicTarget = ViewModel.SelectedScanType switch
            {
                "Local" => false,
                "Range" => !NetworkScannerService.IsPrivateNetwork(ViewModel.StartIp) || !NetworkScannerService.IsPrivateNetwork(ViewModel.EndIp),
                "Subnet" => !NetworkScannerService.IsPrivateNetwork((ViewModel.Subnet ?? string.Empty).Split('/')[0]),
                _ => false
            };

            if (isPublicTarget)
            {
                var dialog = new ContentDialog
                {
                    Title = "Public Network Warning",
                    Content = "The target range/subnet appears to be a public IP address space. Scanning public networks without explicit written authorization may be illegal.\n\nOnly continue if you own the network or have written permission.",
                    PrimaryButtonText = "I Have Authorization",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }
            }
        }

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
