using System;

namespace PingTool.Models;

public class NetworkScanResult
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public long ResponseTime { get; set; }
    public string ResponseTimeDisplay => IsOnline ? $"{ResponseTime} ms" : "—";
    public string? MacAddress { get; set; }
    public string MacAddressDisplay => string.IsNullOrWhiteSpace(MacAddress) ? "—" : MacAddress;
    public string? Vendor { get; set; }
    public string VendorDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Vendor))
            {
                return Vendor;
            }

            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                return "—";
            }

            return IsLocallyAdministeredMac(MacAddress)
                ? "Randomized / local MAC"
                : "Unknown vendor";
        }
    }
    public string? Hostname { get; set; }
    public DateTime ScanTime { get; set; }
    public string Status { get; set; } = string.Empty;

    private static bool IsLocallyAdministeredMac(string mac)
    {
        // Locally administered bit is bit 1 of the first octet.
        // Many modern OSes use randomized MACs which won't match public OUIs.
        var foundNibbles = 0;
        var firstByte = 0;

        for (var i = 0; i < mac.Length; i++)
        {
            var c = mac[i];
            if (!Uri.IsHexDigit(c))
            {
                continue;
            }

            var nibble = HexToNibble(c);
            if (nibble < 0)
            {
                continue;
            }

            firstByte = (firstByte << 4) | nibble;
            foundNibbles++;
            if (foundNibbles >= 2)
            {
                break;
            }
        }

        if (foundNibbles < 2)
        {
            return false;
        }

        return (firstByte & 0x02) != 0;
    }

    private static int HexToNibble(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return (c - 'a') + 10;
        if (c >= 'A' && c <= 'F') return (c - 'A') + 10;
        return -1;
    }
}

public class NetworkScanSettings
{
    public string StartIp { get; set; } = string.Empty;
    public string EndIp { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 1000;
    public int MaxConcurrentScans { get; set; } = 50;
    public bool ResolveHostnames { get; set; } = true;
    public bool LookupMacVendor { get; set; }
    public bool UserAcknowledgedLegalNotice { get; set; }
}
