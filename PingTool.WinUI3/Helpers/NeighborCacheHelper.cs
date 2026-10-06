using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PingTool.Helpers;

/// <summary>
/// Reads the Windows ARP/neighbor cache via the modern, documented GetIpNetTable2 API.
/// The legacy GetIpNetTable's MIB_IPNETROW layout is undocumented/ambiguous across
/// x86/x64/ARM64 and could overread its native buffer, raising an uncatchable
/// AccessViolationException. GetIpNetTable2's MIB_IPNET_ROW2 layout is fully documented,
/// so the struct below can be marshaled correctly on every architecture this app ships.
/// </summary>
internal static class NeighborCacheHelper
{
    // IPv4 only; local scans in this app are IPv4 /24.
    private const ushort AfInet = 2;
    private const int SockAddrInetSize = 28; // sizeof(SOCKADDR_INET)
    private const int PhysicalAddressSize = 32; // IF_MAX_PHYS_ADDRESS_LENGTH

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpNetTable2(ushort family, out IntPtr table);

    [DllImport("iphlpapi.dll")]
    private static extern void FreeMibTable(IntPtr table);

    // Mirrors the documented MIB_IPNET_ROW2 layout (netioapi.h) field-for-field.
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPNET_ROW2
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SockAddrInetSize)]
        public byte[] Address; // SOCKADDR_INET: bytes[0..1]=family, bytes[4..7]=IPv4 address

        public uint InterfaceIndex;
        public ulong InterfaceLuid;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = PhysicalAddressSize)]
        public byte[] PhysicalAddress;

        public uint PhysicalAddressLength;
        public int State;
        public byte Flags;
        public uint ReachabilityTime;
    }

    public static string? TryGetMacAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        var targetBytes = ip.GetAddressBytes();
        var table = IntPtr.Zero;

        try
        {
            var queryResult = GetIpNetTable2(AfInet, out table);
            if (queryResult != 0 || table == IntPtr.Zero)
            {
                return null;
            }

            var numEntries = Marshal.ReadInt32(table);
            var rowSize = Marshal.SizeOf<MIB_IPNET_ROW2>();
            // MIB_IPNET_ROW2 requires 8-byte alignment, so the row array is padded
            // to start 8 bytes after the leading NumEntries field.
            var rowsStart = IntPtr.Add(table, 8);

            for (var i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MIB_IPNET_ROW2>(IntPtr.Add(rowsStart, i * rowSize));

                if (row.Address is null || row.PhysicalAddress is null || row.PhysicalAddressLength <= 0)
                {
                    continue;
                }

                var family = BitConverter.ToUInt16(row.Address, 0);
                if (family != AfInet)
                {
                    continue;
                }

                if (!row.Address.AsSpan(4, 4).SequenceEqual(targetBytes))
                {
                    continue;
                }

                var macLen = Math.Clamp((int)row.PhysicalAddressLength, 0, row.PhysicalAddress.Length);
                if (macLen <= 0)
                {
                    return null;
                }

                return string.Join(":", row.PhysicalAddress.Take(macLen).Select(b => b.ToString("X2")));
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (table != IntPtr.Zero)
            {
                FreeMibTable(table);
            }
        }
    }
}
