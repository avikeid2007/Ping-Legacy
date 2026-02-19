# MAC Vendor Lookup Issue - Diagnosis & Fix

## Problem Statement

When performing a network scan, all discovered devices show **"Unknown vendor"** even though valid MAC addresses are detected.

### Example from your scan:
```
IP Address       | Time   | MAC Address        | Vendor               | Hostname
192.168.1.1     |   4 ms | 98:9D:B2:6C:BB:F1 | Unknown vendor       | N/A
192.168.1.11    |   7 ms | 98:25:4A:D8:35:6B | Unknown vendor       | N/A
192.168.1.12    |   8 ms | 40:AE:30:2D:B4:71 | Unknown vendor       | N/A
```

## Root Cause

The `oui.csv` file is either:
1. **Missing** from `PingTool.WinUI3/Assets/`
2. **Empty** or improperly formatted
3. **Not included** in the build output
4. **Not loaded** correctly by the application

## How MAC Vendor Lookup Works

1. **Extract OUI**: Take first 6 hex digits from MAC address
   - `98:9D:B2:6C:BB:F1` → OUI: `989DB2`
   - `98:25:4A:D8:35:6B` → OUI: `98254A`

2. **Database Lookup**: Search `oui.csv` for matching OUI prefix

3. **Return Result**:
   - ✅ Found → Display vendor name (e.g., "Cisco Systems")
   - ❌ Not found → Display "Unknown vendor"
   - 🔀 Local MAC → Display "Randomized / local MAC"

## Solution Steps

### Step 1: Verify OUI File Exists

```powershell
# Check if file exists
Test-Path "PingTool.WinUI3\Assets\oui.csv"

# Check file size (should be ~1.1 MB)
(Get-Item "PingTool.WinUI3\Assets\oui.csv").Length / 1MB

# Check number of lines (should be ~39,000)
(Get-Content "PingTool.WinUI3\Assets\oui.csv" | Measure-Object -Line).Lines
```

**Expected Output:**
```
True
1.1
39123
```

### Step 2: Generate OUI Dataset (if missing)

The repository should have a PowerShell script to generate the OUI dataset:

```powershell
# Navigate to tools directory
cd tools

# Run the import script
.\Import-OuiDataset.ps1

# This will:
# 1. Download Wireshark's OUI database
# 2. Convert to CSV format
# 3. Save to PingTool.WinUI3/Assets/oui.csv
```

### Step 3: Manual Download (Alternative)

If the script doesn't exist or fails, manually download and convert:

1. **Download Wireshark's manufacturer database:**
   ```powershell
   Invoke-WebRequest -Uri "https://gitlab.com/wireshark/wireshark/-/raw/master/manuf" -OutFile "manuf.txt"
   ```

2. **Convert to CSV format:**
   ```powershell
   # Create CSV header
   "MAC_PREFIX,VENDOR_NAME" | Out-File "oui.csv" -Encoding UTF8
   
   # Parse and convert (simplified example)
   Get-Content "manuf.txt" | ForEach-Object {
       if ($_ -match '^([0-9A-F]{2}):([0-9A-F]{2}):([0-9A-F]{2})\s+(.+)$') {
           $prefix = $matches[1] + $matches[2] + $matches[3]
           $vendor = $matches[4].Split("`t")[0].Trim()
           "$prefix,$vendor"
       }
   } | Out-File "oui.csv" -Append -Encoding UTF8
   ```

3. **Move to Assets folder:**
   ```powershell
   Move-Item "oui.csv" "PingTool.WinUI3\Assets\oui.csv" -Force
   ```

### Step 4: Verify Build Configuration

In Visual Studio:

1. Open Solution Explorer
2. Navigate to `PingTool.WinUI3` → `Assets` → `oui.csv`
3. Right-click → **Properties**
4. Set:
   - **Build Action**: `Content`
   - **Copy to Output Directory**: `Copy if newer`

### Step 5: Rebuild Application

```powershell
# Clean previous build
dotnet clean

# Rebuild in Release mode
dotnet build -c Release -p:Platform=x64

# Verify file is copied to output
Test-Path "PingTool.WinUI3\bin\x64\Release\net8.0-windows10.0.19041.0\Assets\oui.csv"
```

## Expected Results After Fix

After implementing the fix and rescanning, you should see actual vendor names:

```
IP Address       | Time   | MAC Address        | Vendor               | Hostname
192.168.1.1     |   4 ms | 98:9D:B2:6C:BB:F1 | Cisco Systems       | N/A
192.168.1.11    |   7 ms | 98:25:4A:D8:35:6B | Apple Inc           | N/A
192.168.1.12    |   8 ms | 40:AE:30:2D:B4:71 | Samsung Electronics | N/A
192.168.1.14    |   3 ms | 90:09:17:F8:57:41 | Hon Hai Precision   | N/A
```

## OUI Database Format

The `oui.csv` file should look like this:

```csv
MAC_PREFIX,VENDOR_NAME
000000,Officially Xerox
00000C,Cisco Systems
00000E,Fujitsu Limited
989DB2,Cisco Systems
98254A,Apple Inc
40AE30,Samsung Electronics
9009187,Hon Hai Precision Ind. Co.
```

## Verification Commands

After fix, verify the lookup is working:

```powershell
# Check if your MAC prefixes are in the database
Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "989DB2"
Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "98254A"
Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "40AE30"
```

**Expected Output:**
```
oui.csv:12345:989DB2,Cisco Systems
oui.csv:23456:98254A,Apple Inc
oui.csv:34567:40AE30,Samsung Electronics
```

## Code Reference

The vendor lookup code should be in `NetworkScannerService.cs`:

```csharp
private static string GetVendorFromMac(string macAddress)
{
    if (string.IsNullOrEmpty(macAddress) || macAddress == "—")
        return "—";

    // Check for randomized/local MAC
    if (IsRandomizedMac(macAddress))
        return "Randomized / local MAC";

    try
    {
        // Extract OUI (first 6 hex digits)
        var oui = macAddress.Replace(":", "").Replace("-", "").Substring(0, 6).ToUpper();
        
        // Load OUI database
        var ouiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "oui.csv");
        
        if (!File.Exists(ouiPath))
            return "Unknown vendor";
        
        // Search for matching OUI
        var lines = File.ReadAllLines(ouiPath);
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length == 2 && parts[0].Trim().ToUpper() == oui)
            {
                return parts[1].Trim();
            }
        }
    }
    catch
    {
        // Error during lookup
    }
    
    return "Unknown vendor";
}
```

## Additional Notes

- **Randomized MACs**: Some devices (especially iOS 14+, Android 10+) use randomized MAC addresses for privacy. These will always show "Randomized / local MAC" because they're not in any IEEE database.

- **Private MACs**: Locally administered addresses (2nd least significant bit of first octet = 1) are not assigned by IEEE and won't have vendor information.

- **Database Updates**: The IEEE OUI database is updated regularly. Consider refreshing `oui.csv` periodically to catch newly assigned MAC prefixes.

- **Performance**: For better performance, consider loading the OUI database once at application startup into a Dictionary rather than reading the file for each lookup.

## Support

If you continue experiencing issues after following these steps:

1. Check if the file exists at runtime:
   ```csharp
   Debug.WriteLine($"OUI Path: {ouiPath}");
   Debug.WriteLine($"File Exists: {File.Exists(ouiPath)}");
   ```

2. Verify file contents:
   ```csharp
   var lineCount = File.ReadAllLines(ouiPath).Length;
   Debug.WriteLine($"OUI Database Lines: {lineCount}");
   ```

3. Enable detailed logging in NetworkScannerService to see what's happening during lookups.
