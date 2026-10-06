using PingTool.Helpers;
using PingTool.Models;
using System.Net.NetworkInformation;
using System.Timers;

namespace PingTool.Services;

public class ScheduledPingService : IDisposable
{
    /// <summary>Shared instance so scheduled pings keep running across page navigation (and are
    /// inspectable from MainWindow to decide whether to minimize-to-tray instead of exiting).</summary>
    public static ScheduledPingService Instance { get; } = new();

    private readonly List<ScheduledPing> _scheduledPings = new();
    private readonly Dictionary<string, System.Timers.Timer> _timers = new();
    private readonly object _lock = new();

    public event Action<ScheduledPing, bool, long>? PingCompleted;
    public event Action<ScheduledPing>? PingFailed;
    public event Action<ScheduledPing>? PingRecovered;

    // Returns a snapshot, not a live view - callers (UI thread, AppWindow.Closing) must not
    // enumerate the same List<T> a timer thread may concurrently be mutating/serializing.
    public IReadOnlyList<ScheduledPing> ScheduledPings
    {
        get { lock (_lock) { return _scheduledPings.ToList(); } }
    }

    public ScheduledPingService()
    {
        LoadScheduledPings();
    }

    public void AddScheduledPing(ScheduledPing ping)
    {
        lock (_lock)
        {
            _scheduledPings.Add(ping);
            if (ping.IsEnabled)
            {
                StartTimer(ping);
            }
            SaveScheduledPings();
        }
    }

    public void RemoveScheduledPing(string id)
    {
        lock (_lock)
        {
            var ping = _scheduledPings.FirstOrDefault(p => p.Id == id);
            if (ping != null)
            {
                StopTimer(ping.Id);
                _scheduledPings.Remove(ping);
                SaveScheduledPings();
            }
        }
    }

    public void ToggleEnabled(string id)
    {
        lock (_lock)
        {
            var ping = _scheduledPings.FirstOrDefault(p => p.Id == id);
            if (ping != null)
            {
                ping.IsEnabled = !ping.IsEnabled;
                if (ping.IsEnabled)
                {
                    StartTimer(ping);
                }
                else
                {
                    StopTimer(ping.Id);
                }
                SaveScheduledPings();
            }
        }
    }

    public void UpdateInterval(string id, int intervalMinutes)
    {
        lock (_lock)
        {
            var ping = _scheduledPings.FirstOrDefault(p => p.Id == id);
            if (ping != null)
            {
                ping.IntervalMinutes = intervalMinutes;
                if (ping.IsEnabled)
                {
                    StopTimer(ping.Id);
                    StartTimer(ping);
                }
                SaveScheduledPings();
            }
        }
    }

    private void StartTimer(ScheduledPing ping)
    {
        StopTimer(ping.Id);

        var timer = new System.Timers.Timer(ping.IntervalMinutes * 60 * 1000);
        timer.Elapsed += async (s, e) =>
        {
            try
            {
                await ExecutePingAsync(ping);
            }
            catch (Exception ex)
            {
                // Raised on the timer's thread pool thread - unobserved exceptions here crash the app.
                Sentry.SentrySdk.CaptureException(ex);
            }
        };
        timer.AutoReset = true;
        timer.Start();

        _timers[ping.Id] = timer;

        // Also run immediately
        _ = ExecutePingAsync(ping);
    }

    private void StopTimer(string id)
    {
        if (_timers.TryGetValue(id, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            _timers.Remove(id);
        }
    }

    private async Task ExecutePingAsync(ScheduledPing ping)
    {
        try
        {
            using var pingSender = new Ping();
            var reply = await pingSender.SendPingAsync(ping.Host, 3000);

            ping.LastRun = DateTime.Now;

            if (reply.Status == IPStatus.Success)
            {
                ping.LastLatency = reply.RoundtripTime;
                ping.LastResult = "Success";
                ping.ConsecutiveFailures = 0;

                if (ping.WasDownNotified)
                {
                    ping.WasDownNotified = false;
                    PingRecovered?.Invoke(ping);
                }

                PingCompleted?.Invoke(ping, true, reply.RoundtripTime);
            }
            else
            {
                ping.LastResult = reply.Status.ToString();
                ping.LastLatency = null;
                ping.ConsecutiveFailures++;

                if (ping.NotifyOnFailure && ping.ConsecutiveFailures >= 2)
                {
                    ping.WasDownNotified = true;
                    PingFailed?.Invoke(ping);
                }
                PingCompleted?.Invoke(ping, false, -1);
            }
        }
        catch (Exception ex)
        {
            ping.LastRun = DateTime.Now;
            ping.LastResult = ex.Message;
            ping.LastLatency = null;
            ping.ConsecutiveFailures++;

            if (ping.NotifyOnFailure && ping.ConsecutiveFailures >= 2)
            {
                ping.WasDownNotified = true;
                PingFailed?.Invoke(ping);
            }
            PingCompleted?.Invoke(ping, false, -1);
        }

        SaveScheduledPings();
    }

    public async Task RunNowAsync(string id)
    {
        var ping = _scheduledPings.FirstOrDefault(p => p.Id == id);
        if (ping != null)
        {
            await ExecutePingAsync(ping);
        }
    }

    private void LoadScheduledPings()
    {
        var saved = SettingsHelper.Read<List<ScheduledPing>>("ScheduledPings");
        if (saved != null)
        {
            _scheduledPings.AddRange(saved);
            foreach (var ping in _scheduledPings.Where(p => p.IsEnabled))
            {
                StartTimer(ping);
            }
        }
    }

    private void SaveScheduledPings()
    {
        // Snapshot under the lock - ExecutePingAsync calls this from a timer thread without
        // already holding _lock, so serializing the live list directly could race a concurrent
        // Add/Remove/Toggle from the UI thread (lock is reentrant, so this is safe from those too).
        List<ScheduledPing> snapshot;
        lock (_lock)
        {
            snapshot = _scheduledPings.ToList();
        }
        SettingsHelper.Save("ScheduledPings", snapshot);
    }

    public void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
            timer.Dispose();
        }
        _timers.Clear();
    }
}
