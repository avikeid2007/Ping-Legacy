using PingTool.Models;
using System;
using System.Net;

namespace PingTool.Services;

/// <summary>
/// Calculates IPv4 subnet details (network/broadcast address, usable host range,
/// mask, class, etc.) from CIDR notation input such as "192.168.1.10/24".
/// </summary>
public static class SubnetCalculatorService
{
    public static SubnetCalculationResult Calculate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Enter an address in CIDR notation, e.g. 192.168.1.10/24.");
        }

        var parts = input.Trim().Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
        {
            throw new ArgumentException("Invalid CIDR notation. Use the format 192.168.1.10/24.");
        }

        if (!IPAddress.TryParse(parts[0].Trim(), out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Invalid IPv4 address.");
        }

        var ipBytes = ip.GetAddressBytes();
        var maskBytes = PrefixToMask(prefixLength);

        var networkBytes = new byte[4];
        var broadcastBytes = new byte[4];
        var wildcardBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            wildcardBytes[i] = (byte)~maskBytes[i];
        }

        var totalAddresses = (long)Math.Pow(2, 32 - prefixLength);
        long usableHosts;
        string firstHost, lastHost;

        if (prefixLength == 32)
        {
            usableHosts = 1;
            firstHost = lastHost = new IPAddress(networkBytes).ToString();
        }
        else if (prefixLength == 31)
        {
            // RFC 3021 point-to-point link: both addresses are usable.
            usableHosts = 2;
            firstHost = new IPAddress(networkBytes).ToString();
            lastHost = new IPAddress(broadcastBytes).ToString();
        }
        else
        {
            usableHosts = Math.Max(0, totalAddresses - 2);
            firstHost = Offset(networkBytes, 1).ToString();
            lastHost = Offset(broadcastBytes, -1).ToString();
        }

        return new SubnetCalculationResult
        {
            CidrNotation = $"{new IPAddress(networkBytes)}/{prefixLength}",
            NetworkAddress = new IPAddress(networkBytes).ToString(),
            BroadcastAddress = new IPAddress(broadcastBytes).ToString(),
            SubnetMask = new IPAddress(maskBytes).ToString(),
            WildcardMask = new IPAddress(wildcardBytes).ToString(),
            FirstUsableHost = firstHost,
            LastUsableHost = lastHost,
            TotalAddresses = totalAddresses,
            UsableHosts = usableHosts,
            PrefixLength = prefixLength,
            IpClass = GetIpClass(ipBytes[0]),
            IsPrivate = NetworkScannerService.IsPrivateNetwork(ip.ToString())
        };
    }

    private static byte[] PrefixToMask(int prefixLength)
    {
        var maskBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            if (prefixLength >= 8)
            {
                maskBytes[i] = 255;
                prefixLength -= 8;
            }
            else if (prefixLength > 0)
            {
                maskBytes[i] = (byte)(256 - Math.Pow(2, 8 - prefixLength));
                prefixLength = 0;
            }
            else
            {
                maskBytes[i] = 0;
            }
        }
        return maskBytes;
    }

    /// <summary>Adds (or subtracts) 1 from a 4-byte IPv4 address, carrying across octets.</summary>
    private static IPAddress Offset(byte[] address, int delta)
    {
        var result = (byte[])address.Clone();
        if (delta > 0)
        {
            for (var i = 3; i >= 0; i--)
            {
                if (result[i] < 255) { result[i]++; break; }
                result[i] = 0;
            }
        }
        else
        {
            for (var i = 3; i >= 0; i--)
            {
                if (result[i] > 0) { result[i]--; break; }
                result[i] = 255;
            }
        }
        return new IPAddress(result);
    }

    private static string GetIpClass(byte firstOctet) => firstOctet switch
    {
        < 128 => "A",
        < 192 => "B",
        < 224 => "C",
        < 240 => "D (Multicast)",
        _ => "E (Reserved)"
    };
}
