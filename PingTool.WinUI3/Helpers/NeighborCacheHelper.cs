using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PingTool.Helpers;

internal static class NeighborCacheHelper
{
    // IPv4 only; local scans in this app are IPv4 /24.

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPNETROW
    {
        public int dwIndex;
        public int dwPhysAddrLen;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] bPhysAddr;

        public int dwAddr;
        public int dwType;
    }

    public static string? TryGetMacAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        // First call to get required buffer size.
        var bufferSize = 0;
        _ = GetIpNetTable(IntPtr.Zero, ref bufferSize, bOrder: false);
        if (bufferSize <= 0)
        {
            return null;
        }

        var buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal(bufferSize);
            var result = GetIpNetTable(buffer, ref bufferSize, bOrder: false);
            if (result != 0)
            {
                return null;
            }

            var entryCount = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MIB_IPNETROW>();

            for (var i = 0; i < entryCount; i++)
            {
                var currentPtr = IntPtr.Add(rowPtr, i * rowSize);
                var row = Marshal.PtrToStructure<MIB_IPNETROW>(currentPtr);

                // Some rows can have null bPhysAddr depending on marshaling; be defensive.
                if (row.bPhysAddr is null || row.dwPhysAddrLen <= 0)
                {
                    continue;
                }

                // dwAddr is an IPv4 address.
                var rowIp = new IPAddress(unchecked((uint)row.dwAddr));
                if (!rowIp.Equals(ip))
                {
                    continue;
                }

                var macLen = Math.Clamp(row.dwPhysAddrLen, 0, row.bPhysAddr.Length);
                if (macLen <= 0)
                {
                    return null;
                }

                var macBytes = row.bPhysAddr.Take(macLen);
                return string.Join(":", macBytes.Select(b => b.ToString("X2")));
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
