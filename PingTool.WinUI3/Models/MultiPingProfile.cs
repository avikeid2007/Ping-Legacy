namespace PingTool.Models;

/// <summary>A user-saved group of hostnames for the Multi-Ping page.</summary>
public class MultiPingProfile
{
    public string Name { get; set; } = string.Empty;
    public List<string> Hostnames { get; set; } = new();
}
