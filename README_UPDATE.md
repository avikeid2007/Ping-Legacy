# README Update - Add These Sections

## Replace the "Network Scanner (MAC & Vendor)" section with:

---

## 🧭 Network Scanner (MAC & Vendor Detection)

The **Network Scanner** discovers devices on your network and displays:
- **MAC Address** (from Windows ARP/neighbor cache)
- **Vendor** (offline OUI database lookup)
- **Hostname** (optional DNS resolution)

### 📋 How It Works

**MAC Address Detection:**
- ✅ **Works for**: Devices on the same local subnet/VLAN
- ❌ **Won't work for**: Routed networks, public IPs, devices behind routers
- ⚠️ **Randomized MACs**: Modern devices may use privacy-enhanced random addresses

**Vendor Lookup:**
- 📦 **Requires**: `oui.csv` file in `PingTool.WinUI3/Assets/`
- 🔍 **Matches**: First 6 hex digits (3 bytes) of MAC against IEEE OUI database
- ⚠️ **Results**:
  - ✅ Match found → Shows manufacturer name
  - ❌ Not in database → "Unknown vendor"
  - 🔀 Randomized MAC → "Randomized / local MAC"

### 🛠️ Troubleshooting "Unknown vendor"

If **all** MACs show "Unknown vendor", the `oui.csv` file is likely missing or empty:

1. **Check if OUI file exists:**
   ```powershell
   Get-Item "PingTool.WinUI3\Assets\oui.csv"
   (Get-Content "PingTool.WinUI3\Assets\oui.csv" | Measure-Object -Line).Lines
   ```
   Expected: ~39,000 lines (~1.1 MB)

2. **Generate the OUI dataset:**
   - See `OUI_DATASET.md` for detailed instructions
   - Run `tools/Import-OuiDataset.ps1` PowerShell script
   - This downloads and converts Wireshark's OUI database

3. **Verify build configuration:**
   - Open `PingTool.WinUI3.csproj` in Visual Studio
   - Find `oui.csv` → Right-click → Properties
   - Ensure **Build Action** = "Content"
   - Ensure **Copy to Output Directory** = "Copy if newer"

4. **Rebuild the application:**
   ```bash
   dotnet clean
   dotnet build -c Release
   ```

### 📊 OUI Dataset Format

The `oui.csv` file uses a simple comma-separated format:
```csv
MAC_PREFIX,VENDOR_NAME
989DB2,Cisco Systems
98254A,Apple Inc
40AE30,Samsung Electronics
CC32E5,TP-Link Corporation
```

**Your scan results analysis:**
From your export, these MACs **should** resolve if the database is loaded:
- `98:9D:B2:xx:xx:xx` → OUI `989DB2` (likely a router/networking device)
- `98:25:4A:xx:xx:xx` → OUI `98254A` (likely mobile/consumer device)
- `40:AE:30:xx:xx:xx` → OUI `40AE30` (likely IoT/smart device)

**Sources for OUI data:**
- 🌐 [Wireshark manuf file](https://gitlab.com/wireshark/wireshark/-/raw/master/manuf) (Recommended)
- 📋 [IEEE MA-L Listing](https://standards-oui.ieee.org/)
- 🔍 [Wireshark OUI Lookup](https://www.wireshark.org/tools/oui-lookup.html)

---

## Add this new section BEFORE the License section:

---

## 💬 Feedback & Support

We've made it easy for **everyone** to provide feedback — GitHub account **not required**!

### Three Ways to Submit Feedback

<table>
<tr>
<td width="33%" align="center">

**🐙 GitHub Issue**

Direct browser integration  
Public tracking & discussion  
Requires GitHub account

*Best for feature requests & bugs*

</td>
<td width="33%" align="center">

**📧 Email Support**

Opens your email client  
No account needed  
Quick and familiar

*Best for questions & private feedback*

</td>
<td width="33%" align="center">

**💾 Export to File**

Save feedback locally  
Send later via any method  
Offline workflow

*Maximum flexibility*

</td>
</tr>
</table>

All methods include **optional system information** (OS version, app version, etc.) to help diagnose issues faster.

### 📝 What You Can Submit
- 🌟 **Feature Requests**: New tools or improvements
- 🐛 **Bug Reports**: Issues or unexpected behavior
- ❓ **Questions**: How-to or clarification

---

## 📶 Network Statistics & Data Usage

The **Data Usage** page provides comprehensive network monitoring powered by Windows Networking APIs.

### 📊 What's Tracked

**Today's Usage**
- 📥 Downloaded data
- 📤 Uploaded data
- 📊 Total combined usage

**Last 30 Days**
- 📈 Monthly download/upload trends
- 📅 Daily average calculation
- 🔝 Peak usage day identification

**Connection Details**
- 📡 Active network name (SSID for Wi-Fi)
- 🔌 Connection type (Wi-Fi, Ethernet, Cellular)
- 📶 Signal strength (Wi-Fi only)
- 💰 Cost type (Unlimited, Fixed, Metered)

**Network Interface Information**
- 🖥️ Interface name and adapter ID
- ⚡ Connection speed (Mbps/Gbps)
- 🌐 IPv4 and IPv6 addresses
- 🔗 MAC address (formatted with colons)
- 🌍 DNS servers (primary & secondary)

**Additional Stats**
- ⏱️ Current session data usage
- 📡 Roaming data (if applicable)
- 🚀 Average network speed
- 🕐 Last updated timestamp

### 🔄 Real-time Updates

All data refreshes automatically when:
- ✅ Page loads
- 🔄 "Refresh" button clicked
- 🔌 Network connection changes

Data is formatted automatically (B → KB → MB → GB) for easy reading.

---
