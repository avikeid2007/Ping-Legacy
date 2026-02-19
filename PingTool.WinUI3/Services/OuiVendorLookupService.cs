using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace PingTool.Services;

internal sealed class OuiVendorLookupService
{
    private static readonly Uri OuiCsvUri = new("ms-appx:///Assets/oui.csv");

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private Dictionary<string, string>? _vendors;
    private bool _loaded;

    public static OuiVendorLookupService Shared { get; } = new();

    private OuiVendorLookupService() { }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string? content = null;

            // Try method 1: ms-appx URI (for packaged apps)
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(OuiCsvUri);
                content = await FileIO.ReadTextAsync(file);
                System.Diagnostics.Debug.WriteLine($"[OUI] Loaded from ms-appx URI: {OuiCsvUri}");
            }
            catch (Exception ex1)
            {
                System.Diagnostics.Debug.WriteLine($"[OUI] Failed to load from ms-appx URI: {ex1.Message}");

                // Try method 2: Load from file system (for unpackaged/debug scenarios)
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var filePath = Path.Combine(baseDir, "Assets", "oui.csv");
                    System.Diagnostics.Debug.WriteLine($"[OUI] Trying file system path: {filePath}");

                    if (File.Exists(filePath))
                    {
                        content = await File.ReadAllTextAsync(filePath, cancellationToken);
                        System.Diagnostics.Debug.WriteLine($"[OUI] Loaded from file system: {filePath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[OUI] File not found at: {filePath}");
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"[OUI] Failed to load from file system: {ex2.Message}");
                }
            }

            if (content != null)
            {
                System.Diagnostics.Debug.WriteLine($"[OUI] File loaded, content length: {content.Length} characters");

                using var reader = new StringReader(content);
                string? line;
                int lineNumber = 0;
                int validEntries = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    cancellationToken.ThrowIfCancellationRequested();

                    line = line.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    // Allow comment lines.
                    if (line.StartsWith('#'))
                    {
                        continue;
                    }

                    var parts = line.Split(',', 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    var prefix = NormalizePrefix(parts[0]);
                    var vendor = parts[1].Trim();

                    if (prefix is null || vendor.Length == 0)
                    {
                        continue;
                    }

                    // Last one wins.
                    dict[prefix] = vendor;
                    validEntries++;
                }

                System.Diagnostics.Debug.WriteLine($"[OUI] Loaded {validEntries} valid entries from {lineNumber} lines");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[OUI] Failed to load OUI database from any source");
            }

            _vendors = dict;
            _loaded = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public string? LookupVendor(string? macAddress)
    {
        if (!_loaded || _vendors is null || string.IsNullOrWhiteSpace(macAddress))
        {
            System.Diagnostics.Debug.WriteLine($"[OUI] Lookup skipped - Loaded:{_loaded}, Vendors:{_vendors?.Count ?? 0}, MAC:{macAddress}");
            return null;
        }

        var oui = ExtractOui(macAddress);
        if (oui is null)
        {
            System.Diagnostics.Debug.WriteLine($"[OUI] Failed to extract OUI from MAC: {macAddress}");
            return null;
        }

        var found = _vendors.TryGetValue(oui, out var vendor);
        System.Diagnostics.Debug.WriteLine($"[OUI] Lookup for {macAddress} -> OUI:{oui} -> Found:{found} -> Vendor:{vendor}");

        return found ? vendor : null;
    }

    private static string? ExtractOui(string macAddress)
    {
        var hex = new string(macAddress
            .Where(Uri.IsHexDigit)
            .Select(c => char.ToUpperInvariant(c))
            .ToArray());

        return hex.Length >= 6 ? hex[..6] : null;
    }

    private static string? NormalizePrefix(string prefix)
    {
        var hex = new string(prefix
            .Where(Uri.IsHexDigit)
            .Select(c => char.ToUpperInvariant(c))
            .ToArray());

        return hex.Length == 6 ? hex : null;
    }
}
