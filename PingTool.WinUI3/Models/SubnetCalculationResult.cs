namespace PingTool.Models;

public class SubnetCalculationResult
{
    public string CidrNotation { get; set; } = string.Empty;
    public string NetworkAddress { get; set; } = string.Empty;
    public string BroadcastAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string WildcardMask { get; set; } = string.Empty;
    public string FirstUsableHost { get; set; } = string.Empty;
    public string LastUsableHost { get; set; } = string.Empty;
    public long TotalAddresses { get; set; }
    public long UsableHosts { get; set; }
    public int PrefixLength { get; set; }
    public string IpClass { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
}
