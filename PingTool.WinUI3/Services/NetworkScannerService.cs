using PingTool.Models;
using PingTool.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PingTool.Services;

/// <summary>
/// Network Scanner Service with ethical safeguards
/// 
/// LEGAL & ETHICAL RESPONSIBILITY:
/// This service should ONLY be used to scan networks you own or have explicit permission to scan.
/// Unauthorized network scanning may violate:
/// - Computer Fraud and Abuse Act (CFAA) in the USA
/// - Computer Misuse Act in the UK
/// - Similar laws in other jurisdictions
/// 
/// Users must acknowledge legal responsibility before using this feature.
/// </summary>
public class NetworkScannerService
{
    private const int DefaultTimeout = 1000;
    private const int MaxConcurrentScans = 50;
    private const int MinDelayBetweenPingsMs = 10;

    public async IAsyncEnumerable<NetworkScanResult> ScanRangeAsync(
        string startIp,
        string endIp,
        NetworkScanSettings settings,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!settings.UserAcknowledgedLegalNotice)
        {
            throw new InvalidOperationException("User must acknowledge legal and ethical responsibility before scanning.");
        }

        var ipList = GenerateIpRange(startIp, endIp);
        var totalIps = ipList.Count;
        var scannedCount = 0;

        using var semaphore = new SemaphoreSlim(settings.MaxConcurrentScans);
        var tasks = new List<Task<NetworkScanResult?>>();

        for (int i = 0; i < ipList.Count; i++)
        {
            // Check cancellation frequently
            cancellationToken.ThrowIfCancellationRequested();

            var ip = ipList[i];
            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                NetworkScanResult? result = null;
                try
                {
                    // Quick cancellation check
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.Delay(MinDelayBetweenPingsMs, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    result = await ScanSingleIpAsync(ip, settings, cancellationToken);
                    Interlocked.Increment(ref scannedCount);
                    progress?.Report((int)((double)scannedCount / totalIps * 100));
                    return result;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);

            // Yield results as they complete
            var completedTasks = tasks.Where(t => t.IsCompleted).ToList();
            foreach (var completedTask in completedTasks)
            {
                tasks.Remove(completedTask);
                var taskResult = await completedTask;
                if (taskResult != null)
                    yield return taskResult;
            }

            // Control flow - wait if we have too many concurrent tasks
            if (tasks.Count >= settings.MaxConcurrentScans)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);
                var taskResult = await completed;
                if (taskResult != null)
                    yield return taskResult;
            }

            // Yield control periodically to allow cancellation to be processed
            if (i % 10 == 0)
            {
                await Task.Yield();
            }
        }

        // Wait for remaining tasks with frequent cancellation checks
        while (tasks.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var completed = await Task.WhenAny(tasks);
            tasks.Remove(completed);

            NetworkScanResult? taskResult = null;
            var taskCompleted = false;

            try
            {
                taskResult = await completed;
                taskCompleted = true;
            }
            catch (OperationCanceledException)
            {
                taskCompleted = true;
            }

            if (taskCompleted && taskResult != null)
                yield return taskResult;
        }
    }

    public async IAsyncEnumerable<NetworkScanResult> ScanSubnetAsync(
        string subnet,
        NetworkScanSettings settings,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (startIp, endIp) = ParseSubnet(subnet);

        await foreach (var result in ScanRangeAsync(startIp, endIp, settings, progress, cancellationToken))
        {
            yield return result;
        }
    }

    public async IAsyncEnumerable<NetworkScanResult> ScanLocalNetworkAsync(
        NetworkScanSettings settings,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var localIp = GetLocalIPAddress();
        if (string.IsNullOrEmpty(localIp))
        {
            yield break;
        }

        var subnet = $"{localIp.Substring(0, localIp.LastIndexOf('.'))}.0/24";

        await foreach (var result in ScanSubnetAsync(subnet, settings, progress, cancellationToken))
        {
            yield return result;
        }
    }

    private async Task<NetworkScanResult> ScanSingleIpAsync(
        string ipAddress,
        NetworkScanSettings settings,
        CancellationToken cancellationToken)
    {
        var result = new NetworkScanResult
        {
            IpAddress = ipAddress,
            ScanTime = DateTime.Now,
            Status = "Scanning..."
        };

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, settings.TimeoutMs);

            result.IsOnline = reply.Status == IPStatus.Success;
            result.ResponseTime = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            result.Status = reply.Status.ToString();

            if (result.IsOnline && settings.LookupMacVendor)
            {
                try
                {
                    // MAC addresses are only knowable for on-link (local L2) targets.
                    // If the IP isn't on any local interface subnet, skip.
                    if (IPAddress.TryParse(ipAddress, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (IsOnLocalSubnet(parsed))
                        {
                            // Ping should have populated neighbor cache for local targets.
                            result.MacAddress = NeighborCacheHelper.TryGetMacAddress(ipAddress);

                            if (!string.IsNullOrWhiteSpace(result.MacAddress))
                            {
                                await OuiVendorLookupService.Shared.EnsureLoadedAsync(cancellationToken);
                                result.Vendor = OuiVendorLookupService.Shared.LookupVendor(result.MacAddress);
                            }
                        }
                    }
                }
                catch
                {
                    // Optional enrichment; ignore failures.
                }
            }

            if (result.IsOnline && settings.ResolveHostnames)
            {
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                    result.Hostname = hostEntry.HostName;
                }
                catch
                {
                    result.Hostname = null;
                }
            }
        }
        catch (PingException)
        {
            result.IsOnline = false;
            result.Status = "Unreachable";
        }
        catch (Exception ex)
        {
            result.IsOnline = false;
            result.Status = $"Error: {ex.Message}";
        }

        return result;
    }

    private static bool IsOnLocalSubnet(IPAddress target)
    {
        // Determine whether the target is on-link for any local interface.
        // If it isn't, we can't discover its MAC via neighbor/ARP tables.
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                IPInterfaceProperties props;
                try
                {
                    props = nic.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var mask = unicast.IPv4Mask;
                    if (mask is null)
                    {
                        continue;
                    }

                    if (IsInSameSubnet(unicast.Address, target, mask))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // If anything goes sideways, err on the safe side: don't attempt MAC resolution.
        }

        return false;
    }

    private static bool IsInSameSubnet(IPAddress a, IPAddress b, IPAddress mask)
    {
        var aBytes = a.GetAddressBytes();
        var bBytes = b.GetAddressBytes();
        var mBytes = mask.GetAddressBytes();

        if (aBytes.Length != bBytes.Length || aBytes.Length != mBytes.Length)
        {
            return false;
        }

        for (var i = 0; i < aBytes.Length; i++)
        {
            if ((aBytes[i] & mBytes[i]) != (bBytes[i] & mBytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private List<string> GenerateIpRange(string startIp, string endIp)
    {
        var ipList = new List<string>();

        var startBytes = IPAddress.Parse(startIp).GetAddressBytes();
        var endBytes = IPAddress.Parse(endIp).GetAddressBytes();

        uint start = BitConverter.ToUInt32(startBytes.Reverse().ToArray(), 0);
        uint end = BitConverter.ToUInt32(endBytes.Reverse().ToArray(), 0);

        if (end < start)
        {
            throw new ArgumentException("End IP must be greater than or equal to start IP");
        }

        const uint maxRange = 65536;
        if (end - start > maxRange)
        {
            throw new ArgumentException($"IP range too large. Maximum {maxRange} addresses allowed.");
        }

        for (uint i = start; i <= end; i++)
        {
            var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
            ipList.Add(new IPAddress(bytes).ToString());
        }

        return ipList;
    }

    private (string startIp, string endIp) ParseSubnet(string subnet)
    {
        var parts = subnet.Split('/');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid subnet format. Use CIDR notation (e.g., 192.168.1.0/24)");
        }

        var baseIp = IPAddress.Parse(parts[0]);
        var prefixLength = int.Parse(parts[1]);

        if (prefixLength < 0 || prefixLength > 32)
        {
            throw new ArgumentException("Prefix length must be between 0 and 32");
        }

        var ipBytes = baseIp.GetAddressBytes();
        var maskBytes = new byte[4];

        for (int i = 0; i < 4; i++)
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

        var networkBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        var broadcastBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        return (new IPAddress(networkBytes).ToString(), new IPAddress(broadcastBytes).ToString());
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return string.Empty;
    }

    public static bool IsPrivateNetwork(string ipAddress)
    {
        var ip = IPAddress.Parse(ipAddress);
        var bytes = ip.GetAddressBytes();

        if (bytes[0] == 10)
            return true;

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        if (bytes[0] == 127)
            return true;

        return false;
    }
}
