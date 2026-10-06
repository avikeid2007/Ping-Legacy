using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PingTool.Models;
using PingTool.ViewModels;
using Windows.System;

namespace PingTool.Views;

public sealed partial class SubnetCalculatorPage : Page
{
    public SubnetCalculatorViewModel ViewModel { get; } = new();

    public SubnetCalculatorPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.CalculateCommand.Execute(null);
        }
    }

    private Visibility GetErrorVisibility(string? errorMessage) =>
        string.IsNullOrEmpty(errorMessage) ? Visibility.Collapsed : Visibility.Visible;

    private Visibility GetResultVisibility(SubnetCalculationResult? result) =>
        result is null ? Visibility.Collapsed : Visibility.Visible;

    private string GetAddressType(bool isPrivate) => isPrivate ? "Private" : "Public";
}
