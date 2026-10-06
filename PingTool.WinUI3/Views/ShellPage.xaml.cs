using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System;

namespace PingTool.Views;

public sealed partial class ShellPage : Page
{
    private static readonly List<CommandPaletteItem> _allCommands = new()
    {
        new CommandPaletteItem("Ping", "Continuous ping monitor", "\uE968", typeof(MainPage)),
        new CommandPaletteItem("Multi-Ping", "Ping multiple targets at once", "\uF0E2", typeof(MultiPingPage)),
        new CommandPaletteItem("Traceroute", "Trace the hop-by-hop network path", "\uE8F1", typeof(TraceroutePage)),
        new CommandPaletteItem("Port Scanner", "Scan open ports on a host", "\uE946", typeof(PortScannerPage)),
        new CommandPaletteItem("Network Scanner", "Discover devices on your network", "\uE839", typeof(NetworkScannerPage)),
        new CommandPaletteItem("Subnet Calculator", "Calculate CIDR / network details", "\uE8EF", typeof(SubnetCalculatorPage)),
        new CommandPaletteItem("Speed Test", "Test your connection speed", "\uE8B0", typeof(SpeedTestPage)),
        new CommandPaletteItem("Scheduled", "Manage scheduled pings", "\uE823", typeof(ScheduledPingsPage)),
        new CommandPaletteItem("Data Usage", "View network data usage", "\uE9D9", typeof(DataUsagePage)),
        new CommandPaletteItem("History", "View past activity", "\uE81C", typeof(HistoryPage)),
        new CommandPaletteItem("Feedback", "Send suggestions", "\uE939", typeof(SuggestionPage)),
        new CommandPaletteItem("Settings", "App settings", "\uE713", typeof(SettingsPage)),
    };

    private readonly ObservableCollection<CommandPaletteItem> _commandPaletteResults = new();

    public ShellPage()
    {
        InitializeComponent();
        NavigationService.Frame = shellFrame;
        CommandPaletteResultsList.ItemsSource = _commandPaletteResults;

        // Navigate to main page on load
        Loaded += OnLoaded;

        // Hide the nav pane while the compact floating widget is active to maximize its space.
        CompactModeService.CompactModeChanged += OnCompactModeChanged;
        Unloaded += (_, _) => CompactModeService.CompactModeChanged -= OnCompactModeChanged;
    }

    private void OnCompactModeChanged(bool isCompact)
    {
        navigationView.IsPaneVisible = !isCompact;
        navigationView.IsSettingsVisible = !isCompact;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var target = NavigationService.PendingLaunchPage ?? typeof(MainPage);
        NavigationService.PendingLaunchPage = null;
        NavigationService.Navigate(target);
    }

    public bool IsBackEnabled => NavigationService.CanGoBack;

    public object? Selected => navigationView.SelectedItem;

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            NavigationService.Navigate(typeof(SettingsPage));
        }
        else if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var pageType = NavHelper.GetNavigateTo(item);
            if (pageType != null)
            {
                NavigationService.Navigate(pageType);
            }
        }
    }

    private async void RateUs_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9P1KVKT59T2M"));
    }

    private void SearchNavItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        OpenCommandPalette();
    }

    private void OpenCommandPalette_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenCommandPalette();
    }

    private void OpenCommandPalette()
    {
        CommandPaletteSearchBox.Text = string.Empty;
        FilterCommandPalette(string.Empty);
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandPaletteSearchBox.Focus(FocusState.Programmatic);
    }

    private void CloseCommandPalette()
    {
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
    }

    private void CommandPaletteOverlay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        CloseCommandPalette();
    }

    private void CommandPalettePanel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent this tap from bubbling up to the overlay's Tapped handler (which would close the palette).
        e.Handled = true;
    }

    private void CommandPaletteSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterCommandPalette(CommandPaletteSearchBox.Text);
    }

    private void FilterCommandPalette(string query)
    {
        _commandPaletteResults.Clear();

        var matches = string.IsNullOrWhiteSpace(query)
            ? _allCommands
            : _allCommands.Where(c =>
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var match in matches)
        {
            _commandPaletteResults.Add(match);
        }

        if (_commandPaletteResults.Count > 0)
        {
            CommandPaletteResultsList.SelectedIndex = 0;
        }
    }

    private void CommandPaletteSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                CloseCommandPalette();
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                NavigateToSelectedCommand();
                e.Handled = true;
                break;
            case VirtualKey.Down:
                if (CommandPaletteResultsList.SelectedIndex < _commandPaletteResults.Count - 1)
                {
                    CommandPaletteResultsList.SelectedIndex++;
                }
                e.Handled = true;
                break;
            case VirtualKey.Up:
                if (CommandPaletteResultsList.SelectedIndex > 0)
                {
                    CommandPaletteResultsList.SelectedIndex--;
                }
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandPaletteItem item)
        {
            NavigateToCommand(item);
        }
    }

    private void NavigateToSelectedCommand()
    {
        if (CommandPaletteResultsList.SelectedItem is CommandPaletteItem item)
        {
            NavigateToCommand(item);
        }
        else if (_commandPaletteResults.Count > 0)
        {
            NavigateToCommand(_commandPaletteResults[0]);
        }
    }

    private void NavigateToCommand(CommandPaletteItem item)
    {
        CloseCommandPalette();
        NavigationService.Navigate(item.PageType);
    }
}
