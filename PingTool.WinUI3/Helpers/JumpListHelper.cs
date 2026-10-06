using PingTool.Views;
using Windows.UI.StartScreen;

namespace PingTool.Helpers;

/// <summary>
/// Builds the taskbar jump list ("New Ping", "Scheduled Pings", etc.) and resolves the
/// argument string an item was launched with back to the page it should navigate to.
/// </summary>
public static class JumpListHelper
{
    // Key used both for JumpListItem.CreateWithArguments and for resolving LaunchActivatedEventArgs.Arguments.
    private static readonly (string Key, string DisplayName, Type PageType)[] Tasks =
    {
        ("ping", "New Ping", typeof(MainPage)),
        ("multiping", "Multi-Ping", typeof(MultiPingPage)),
        ("networkscanner", "Network Scanner", typeof(NetworkScannerPage)),
        ("scheduled", "Scheduled Pings", typeof(ScheduledPingsPage)),
    };

    public static async Task UpdateAsync()
    {
        if (!JumpList.IsSupported())
        {
            return;
        }

        try
        {
            var jumpList = await JumpList.LoadCurrentAsync();
            jumpList.Items.Clear();

            foreach (var (key, displayName, _) in Tasks)
            {
                var item = JumpListItem.CreateWithArguments(key, displayName);
                item.GroupName = "Tasks";
                jumpList.Items.Add(item);
            }

            await jumpList.SaveAsync();
        }
        catch
        {
            // Non-fatal - the app works fine without jump list entries.
        }
    }

    /// <summary>Returns the page type for a jump list argument string, or null if unrecognized.</summary>
    public static Type? ResolveArguments(string arguments)
    {
        foreach (var (key, _, pageType) in Tasks)
        {
            if (string.Equals(key, arguments, StringComparison.OrdinalIgnoreCase))
            {
                return pageType;
            }
        }

        return null;
    }
}
