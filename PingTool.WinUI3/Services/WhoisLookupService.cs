using System.Net.Sockets;
using System.Text;

namespace PingTool.Services;

/// <summary>
/// Performs WHOIS lookups for domain names and IP addresses.
/// Queries the IANA root WHOIS server first and follows the "refer"/"whois"
/// pointer to the authoritative registry server when one is provided.
/// </summary>
public class WhoisLookupService
{
    private const int WhoisPort = 43;
    private const string IanaWhoisServer = "whois.iana.org";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public async Task<string> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Enter a domain name or IP address to look up.");
        }

        query = query.Trim();

        var referralResponse = await QueryServerAsync(IanaWhoisServer, query, cancellationToken);
        var authoritativeServer = ExtractReferralServer(referralResponse);

        if (string.IsNullOrWhiteSpace(authoritativeServer) ||
            string.Equals(authoritativeServer, IanaWhoisServer, StringComparison.OrdinalIgnoreCase))
        {
            return referralResponse;
        }

        var record = await QueryServerAsync(authoritativeServer, query, cancellationToken);
        return string.IsNullOrWhiteSpace(record) ? referralResponse : record;
    }

    private static async Task<string> QueryServerAsync(string server, string query, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();

        var connectTask = client.ConnectAsync(server, WhoisPort, cancellationToken).AsTask();
        if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeout, cancellationToken)) != connectTask)
        {
            throw new TimeoutException($"Connection to {server} timed out.");
        }
        await connectTask;

        using var stream = client.GetStream();
        var requestBytes = Encoding.ASCII.GetBytes(query + "\r\n");
        await stream.WriteAsync(requestBytes, cancellationToken);

        using var reader = new StreamReader(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string? ExtractReferralServer(string response)
    {
        foreach (var rawLine in response.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("refer:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("whois:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Split(':', 2)[1].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
