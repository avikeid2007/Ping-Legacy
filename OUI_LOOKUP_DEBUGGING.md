# OUI Vendor Lookup Diagnostic Tool

## Summary

I've added comprehensive debug logging to the `OuiVendorLookupService` to diagnose why MAC vendors are showing as "Unknown vendor".

## Changes Made

### 1. Added Debug Logging to `OuiVendorLookupService.cs`

**Loading Phase:**
- Logs when OUI database loading starts
- Shows file content length
- Counts valid entries loaded
- Reports total lines processed
- Logs any exceptions during loading

**Lookup Phase:**
- Logs every lookup attempt
- Shows MAC address input
- Shows extracted OUI
- Shows lookup result (found/not found)
- Shows vendor name if found

## How to Use

1. **Run the application** in Debug mode (F5 in Visual Studio)
2. **Navigate to Network Scanner**
3. **Acknowledge the legal notice**
4. **Start a scan**
5. **Check the Output window** in Visual Studio (View → Output)

## Expected Debug Output

### During App Startup / First Scan:
```
[OUI] Loading OUI database from ms-appx:///Assets/oui.csv
[OUI] File loaded, content length: 1234567 characters
[OUI] Loaded 39000 valid entries from 39089 lines
```

### During Each MAC Lookup:
```
[OUI] Lookup for 98:9D:B2:6C:BB:F1 -> OUI:989DB2 -> Found:True -> Vendor:GOIPGlobalSe GOIP Global Services Pvt. Ltd.
[OUI] Lookup for 98:25:4A:D8:35:6B -> OUI:98254A -> Found:True -> Vendor:TPLink       TP-Link Systems Inc
[OUI] Lookup for 40:AE:30:2D:B4:71 -> OUI:40AE30 -> Found:True -> Vendor:Samsung Electronics Co.,Ltd
```

## Possible Issues & Solutions

### Issue 1: OUI Database Not Loading
**Symptoms:**
```
[OUI] Failed to load OUI database: <exception message>
```

**Solutions:**
- Verify file exists: `Test-Path "PingTool.WinUI3\Assets\oui.csv"`
- Check file size: `(Get-Item "PingTool.WinUI3\Assets\oui.csv").Length`
- Rebuild: `dotnet clean && dotnet build`

### Issue 2: OUI Not Being Extracted
**Symptoms:**
```
[OUI] Failed to extract OUI from MAC: 98:9D:B2:6C:BB:F1
```

**Solutions:**
- Check MAC address format
- Ensure it has at least 6 hex digits

### Issue 3: OUI Not Found in Database
**Symptoms:**
```
[OUI] Lookup for XX:XX:XX:XX:XX:XX -> OUI:XXXXXX -> Found:False -> Vendor:
```

**Solutions:**
- Verify OUI exists in database:
  ```powershell
  Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "XXXXXX"
  ```
- If not found, the OUI might be:
  - Recently assigned (need newer database)
  - Locally administered (randomized MAC)
  - Not registered with IEEE

### Issue 4: Lookup Skipped
**Symptoms:**
```
[OUI] Lookup skipped - Loaded:False, Vendors:0, MAC:98:9D:B2:6C:BB:F1
```

**Solutions:**
- Database failed to load
- Check previous log messages for loading errors

## Verification Steps

1. **Check if database loads successfully:**
   - Look for `[OUI] Loaded X valid entries` message
   - Should show ~39,000 entries

2. **Test specific OUI lookups:**
   ```powershell
   # Check if your MACs are in database
   Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "989DB2"
   Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "98254A"
   Select-String -Path "PingTool.WinUI3\Assets\oui.csv" -Pattern "40AE30"
   ```

3. **Verify file is deployed:**
   ```powershell
   # After build, check bin folder
   Get-ChildItem -Recurse -Filter "oui.csv" "PingTool.WinUI3\bin"
   ```

## Manual Test

You can also manually test the lookup service:

1. Add a temporary test method to `NetworkScannerPage.xaml.cs`:
   ```csharp
   private async void TestOuiLookup()
   {
       await PingTool.Services.OuiVendorLookupService.Shared.EnsureLoadedAsync();
       
       var testMacs = new[] {
           "98:9D:B2:6C:BB:F1",
           "98:25:4A:D8:35:6B",
           "40:AE:30:2D:B4:71"
       };
       
       foreach (var mac in testMacs)
       {
           var vendor = PingTool.Services.OuiVendorLookupService.Shared.LookupVendor(mac);
           System.Diagnostics.Debug.WriteLine($"Manual Test: {mac} -> {vendor ?? "NULL"}");
       }
   }
   ```

2. Call it from `OnNavigatedTo` or a button click

## Next Steps

1. Run the app in Debug mode
2. Perform a network scan
3. Check the Output window for debug messages
4. Share the log output to diagnose the exact issue

The logging will reveal:
- ✅ Whether the database loads successfully
- ✅ How many entries are loaded
- ✅ Whether lookups are finding matches
- ✅ What vendor names are being returned

This will definitively show why "Unknown vendor" is appearing in your results.
