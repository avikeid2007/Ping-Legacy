using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PingTool.Services;

public static class WakeOnLanService
{
    // Standard magic packet: 6 bytes of 0xFF followed by the target MAC repeated 16 times.
    public static async Task SendMagicPacketAsync(string macAddress, int port = 9)
    {
        var macBytes = ParseMacAddress(macAddress);

        var packet = new byte[6 + 16 * 6];
        for (var i = 0; i < 6; i++)
        {
            packet[i] = 0xFF;
        }
        for (var i = 0; i < 16; i++)
        {
            Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6);
        }

        using var client = new UdpClient { EnableBroadcast = true };
        await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, port));
    }

    private static byte[] ParseMacAddress(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            throw new ArgumentException("MAC address is required.", nameof(macAddress));
        }

        var hex = Regex.Replace(macAddress, "[^0-9A-Fa-f]", string.Empty);
        if (hex.Length != 12)
        {
            throw new ArgumentException($"'{macAddress}' is not a valid MAC address.", nameof(macAddress));
        }

        return Enumerable.Range(0, 6)
            .Select(i => System.Convert.ToByte(hex.Substring(i * 2, 2), 16))
            .ToArray();
    }
}
