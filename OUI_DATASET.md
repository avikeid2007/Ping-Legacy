# OUI (MAC Vendor) Dataset

The Network Scanner can show **MAC Address** and **Vendor** for **Local Network** scans.

Vendor names are resolved by matching the first 3 bytes of a MAC address (the **OUI**) against an offline list stored at:

- `PingTool.WinUI3/Assets/oui.csv`

## Why `oui.csv` is empty by default

Large OUI databases are typically distributed under licenses/terms that may not be compatible with bundling in this repo by default.
To avoid accidental licensing issues, the repo ships an empty `oui.csv` and provides an import script.

## Provide a dataset you can redistribute

You can use any OUI dataset you have permission to ship (example formats supported):
- `OUI,VENDOR` CSV
- Wireshark-style `manuf` file (`00:11:22\tVendor`)
- Generic text lines starting with an OUI/MAC prefix followed by a vendor name

## Generate `Assets/oui.csv`

From the repo root:

```powershell
# Example: Wireshark manuf file (download first)
Invoke-WebRequest -Uri "https://www.wireshark.org/download/automated/data/manuf" -OutFile .\manuf

# Verify it exists
Test-Path .\manuf

# Import it
powershell -ExecutionPolicy Bypass -File .\tools\Import-OuiDataset.ps1 -InputPath .\manuf

# Example: CSV file
powershell -ExecutionPolicy Bypass -File .\tools\Import-OuiDataset.ps1 -InputPath .\oui_source.csv
```

If your dataset file is not in the repo root, pass a full path:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Import-OuiDataset.ps1 -InputPath "C:\path\to\manuf"
```

The script writes:
- `PingTool.WinUI3/Assets/oui.csv`

## Ship it in the app

`PingTool.WinUI3/PingTool.WinUI3.csproj` already includes `Assets/oui.csv` as app content, so once you generate it, vendor names will appear in Local Network scans.
