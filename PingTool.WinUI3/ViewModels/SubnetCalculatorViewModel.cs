using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingTool.Models;
using PingTool.Services;
using System;

namespace PingTool.ViewModels;

public partial class SubnetCalculatorViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Input { get; set; } = "192.168.1.10/24";

    [ObservableProperty]
    public partial SubnetCalculationResult? Result { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public SubnetCalculatorViewModel()
    {
        Calculate();
    }

    [RelayCommand]
    private void Calculate()
    {
        try
        {
            Result = SubnetCalculatorService.Calculate(Input);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            Result = null;
            ErrorMessage = ex.Message;
        }
    }
}
