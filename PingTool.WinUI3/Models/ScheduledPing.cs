namespace PingTool.Models;

public class ScheduledPing
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Host { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 5;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRun { get; set; }
    public string? LastResult { get; set; }
    public long? LastLatency { get; set; }
    public bool NotifyOnFailure { get; set; } = true;
    public int ConsecutiveFailures { get; set; }

    /// <summary>True once a "down" toast has been shown for the current outage, so a single
    /// "recovered" toast can be shown the next time this host responds successfully again.</summary>
    public bool WasDownNotified { get; set; }
    
    public string IntervalDisplay => IntervalMinutes switch
    {
        1 => "Every minute",
        < 60 => $"Every {IntervalMinutes} minutes",
        60 => "Every hour",
        _ => $"Every {IntervalMinutes / 60} hours"
    };
    
    public string StatusDisplay => LastRun.HasValue 
        ? $"Last: {LastLatency}ms at {LastRun:HH:mm:ss}" 
        : "Not run yet";
}
