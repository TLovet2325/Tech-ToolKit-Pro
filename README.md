# 🛠️ Tech ToolKit Pro

> A professional Windows maintenance and diagnostic desktop application built with C# WinForms (.NET Framework 4.x)

![Platform](https://img.shields.io/badge/platform-Windows-blue?logo=windows)
![Language](https://img.shields.io/badge/language-C%23-239120?logo=csharp)
![Framework](https://img.shields.io/badge/framework-.NET%20Framework%204.x-purple)
![UI](https://img.shields.io/badge/UI-WinForms-darkblue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📸 Overview

Tech ToolKit Pro is a dark-themed, all-in-one Windows system maintenance suite. It provides 20+ tools covering disk health, system scans, network resets, security checks, file recovery, backup, and more — all in a single polished desktop application.

![Image alt](https://github.com/TLovet2325/Tech-ToolKit-Pro/blob/bbba3b95f0cc4483828596ce905a86e211b05717/Screenshot%202026-04-16%20052108.png)

---

## ✨ Features

### 📊 Dashboard
- Live CPU, RAM and disk usage gauges with sparkline history graphs
- Running process table with real-time memory usage
- Auto-refresh every 2 seconds

### 🔧 Disk Maintenance
- 9 maintenance tools in a responsive 3-column card grid
- Each tool opens an **elevated CMD or PowerShell window** so you can watch the output live
- Tools include:
  - Disable Hibernation (`powercfg /h off`)
  - Check Disk CHKDSK (`chkdsk C: /f /r /x`)
  - SFC System Scan (`sfc /scannow`)
  - DISM Health Restore (`DISM /Cleanup-Image /RestoreHealth`)
  - Optimize Drive (`defrag C: /U /V`)
  - Clear Windows Update Cache (multi-step: stop → delete → restart)
  - Clear Event Logs
  - Compact OS
  - Control Panel launcher
- Run all 9 tools sequentially with one click
- Animated progress bars per card
- Command log with timestamps and colour-coded status

### 💾 Disk SMART Health
- WMI-powered physical drive scan
- Shows model, interface, firmware, serial number, capacity
- Colour-coded health indicator (Healthy / Failing / Error / Unknown)
- Logical volume breakdown with used/free space per partition
- CHKDSK and Defrag launch via elevated CMD window

### 🧹 Temp Cleanup & Ultra Cleanup
- Scan and clean `AppData\Local\Temp`
- Also cleans Windows Temp, SoftwareDistribution, Prefetch
- Launches `cleanmgr /sageset:1 /sagerun:1` for Disk Cleanup
- Animated gradient progress bar, BackgroundWorker based (non-blocking)

### 🌐 Network Reset Tools (Flush DNS)
- Three tools, each opens a **visible terminal window**:
  - **Flush DNS** — `cmd /K ipconfig /flushdns`
  - **Reset WinSock** — Elevated CMD via PowerShell + `netsh winsock reset`
  - **Reset TCP/IP Stack** — Elevated CMD via PowerShell + `netsh int ip reset`
- Dual-mode success detection (exit code + output keyword) handles `netsh`'s quirky exit code 1

### 🛡 MRT Scan (Malicious Software Removal Tool)
- Searches 7+ known locations for `mrt.exe` (System32, SysWOW64, Defender Platform subfolders, WU cache, WHERE command fallback)
- Manual browse fallback if MRT is not auto-detected
- Quick / Full / Custom scan modes
- Live scan log with colour-coded threat detection

### 🔒 Windows Defender Scan
- Quick, Full, Custom and Boot-time scan modes via `MpCmdRun.exe`
- Real-time RichTextBox output streaming

### 🧪 Smart Scan
- 6 sequential scan engines with individual checkboxes:
  - Windows Defender · SFC · DISM · Disk Check · Network Check · BCD
- Sequential execution with per-engine progress

### 🔄 File Recovery
- Scans Recycle Bin (`$Recycle.Bin` with `$I` metadata parsing for original filenames)
- Shadow Copy (VSS) scanning via `vssadmin`
- Windows Backup / File History scanning
- File type filter, search box, sortable columns
- Recover selected or all files with collision-safe naming
- Recovery log with per-file status

### 🗄 Windows Backup
- Add individual files and entire folders to backup list
- **ZIP** format using built-in `System.IO.Compression.ZipArchive` (no extra DLL)
- **RAR** format via WinRAR CLI (auto-detected in Program Files)
- 4 compression levels: None / Fast / Normal / Best
- Custom archive name with optional timestamp
- Live progress with file-by-file log
- Backup history tab (date, name, format, size, duration, status)

### 🛡 Point of Restoration
- Lists existing restore points via WMI (`root\default` → `SystemRestore`)
- Create new restore point — bypasses Windows 24-hour throttle via registry key `SystemRestorePointCreationFrequency = 0`
- Restore system to selected point
- Delete restore points via `vssadmin`
- WMI runs on background thread; list updates on UI thread via `Invoke()`

### 📦 Apps & Updates
- `winget upgrade` powered update detection and installation
- Per-app checkboxes, live output streaming

### 🔄 Windows Update
- `WUApiLib` COM automation for searching, downloading and installing updates
- Severity colour-coding, reboot detection

### 📋 Task List
- Live process viewer with RAM hog detection (> 200 MB)
- Kill hogs / useless / selected processes

### 🗑 Uninstall Apps
- Registry scan (32-bit + 64-bit) for installed applications
- Bloatware detection, direct uninstall via `UninstallString`

### 📊 System Report
- 4-tab report: System Info · Hardware Report · Memory Test · Performance Test
- CPU, RAM, Disk scored /100
- Export to `.txt`

---

## 🏗️ Architecture

```
Tech_ToolKit_Pro/
├── Form1.cs                   Main shell with animated sidebar navigation
├── Form1.Designer.cs          Designer file (IsMdiContainer = false)
├── AdminHelper.cs             Centralised admin rights management
├── Program.cs                 Entry point with admin status logging
├── app.manifest               UAC highestAvailable + Win10/11 compatibility
│
├── FormDashboard.cs           Live system monitoring
├── FormDiskMaintenance.cs     9 disk maintenance tools
├── FormDiskSmart.cs           SMART health via WMI
├── FormTempCleanUp.cs         Temp file cleanup
├── FormUltraCleanUp.cs        Deep system cleanup
├── FormFlushDNS.cs            Network reset tools
├── FormMRTscan.cs             MRT malware scan
├── FormDefenderScan.cs        Windows Defender scan
├── FormSmartScan.cs           6-engine sequential scan
├── FormPointofRestoration.cs  System restore point management
├── FormRecoveryFiles.cs       File recovery (Recycle Bin / VSS / Backup)
├── FormWinBackUp.cs           ZIP / WinRAR backup
├── FormAppsUptates.cs         winget-powered app updates
├── FormShowUpdate.cs          Windows Update via WUApiLib COM
├── FormTaskList.cs            Live process manager
├── FormUninstallApps.cs       App uninstaller
└── FormSystemReport.cs        4-tab system diagnostic report
```

### Navigation pattern
Child forms are embedded into `Form1`'s `contentPanel` at runtime:

```csharp
void LoadForm(Form f)
{
    contentPanel.Controls.Clear();
    f.TopLevel = false;
    f.FormBorderStyle = FormBorderStyle.None;
    f.Dock = DockStyle.Fill;
    contentPanel.Controls.Add(f);
    f.Show();
}
```

### Admin rights pattern
All forms that need elevation use `AdminHelper`:

```csharp
// In constructor (after BuildUI):
AdminHelper.ShowAdminBanner(this, "⚠ This feature requires admin rights.");

// Before a privileged action:
if (!AdminHelper.EnsureAdmin("Feature Name")) return;
```

---

## 🎨 Dark Theme

All forms share the same colour palette:

| Token | Hex | Usage |
|---|---|---|
| `C_BG` | `#0D1117` | Form background |
| `C_SURF` | `#16171B` | Cards, panels, headers |
| `C_SURF2` | `#1E2430` | Alternating rows |
| `C_BLUE` | `#58A6FF` | Primary accent, links |
| `C_GREEN` | `#3FB977` | Success, healthy |
| `C_AMBER` | `#FFA348` | Warnings, admin required |
| `C_RED` | `#F85149` | Errors, failures |
| `C_PURPLE` | `#BC8CFF` | Secondary accent |
| `C_TEAL` | `#38BDC1` | Tertiary accent |
| `C_TXT` | `#E6EDF3` | Primary text |
| `C_SUB` | `#8B949E` | Secondary/muted text |

Custom owner-drawn `ListView` controls throughout for consistent dark rendering.

---

## ⚙️ Requirements

| Requirement | Version |
|---|---|
| Windows | 10 or 11 (64-bit recommended) |
| .NET Framework | 4.7.2 or higher |
| Visual Studio | 2019 / 2022 |
| NuGet package | `System.Management` (for WMI queries) |
| COM Reference | `WUApiLib` (for Windows Update form) |
| Optional | WinRAR installed (for RAR backup format) |
| Optional | `winget` / App Installer (for App Updates form) |

> ⚠️ **Administrator rights** are required for most maintenance tools. The app requests elevation via `app.manifest` (`highestAvailable`) and prompts per-feature via `AdminHelper.EnsureAdmin()`.

---

## 🚀 Getting Started

### 1. Clone the repository
```bash
git clone https://github.com/yourusername/TechToolKitPro.git
cd TechToolKitPro
```

### 2. Open in Visual Studio
Open `Tech_ToolKit_Pro.sln` in Visual Studio 2019 or 2022.

### 3. Restore NuGet packages
```
Tools → NuGet Package Manager → Restore NuGet Packages
```
Or via CLI:
```bash
nuget restore Tech_ToolKit_Pro.sln
```

### 4. Add COM reference (for Windows Update form)
```
Project → Add Reference → COM → Windows Update Agent API
```

### 5. Set the app manifest
Ensure `app.manifest` is set in:
```
Project Properties → Application → Manifest → app.manifest
```

### 6. Set `IsMdiContainer = false`
In `Form1.Designer.cs`, verify:
```csharp
this.IsMdiContainer = false;
```

### 7. Build and run
```
F5  or  Build → Start Debugging
```
Run **as Administrator** for full functionality.

---

## 🔑 Key Implementation Notes

### WMI Queries
All WMI queries use `ManagementScope` + `ObjectQuery` **separately** to avoid "invalid query" errors:

```csharp
// CORRECT
var scope   = new ManagementScope(@"\\.\root\default");
scope.Connect();
var query   = new ObjectQuery("SELECT * FROM SystemRestore");
var searcher = new ManagementObjectSearcher(scope, query);

// WRONG — mixes path into query string
new ManagementObjectSearcher(@"\\.\root\default",
    "SELECT * FROM SystemRestore ORDER BY SequenceNumber DESC");
```

### Process Launching (system tools)
All elevated system tools are launched via `cmd.exe /K` or `powershell.exe Start-Process -Verb RunAs` to ensure System32 tools are always resolved and the output window stays open:

```csharp
// CORRECT — opens visible elevated CMD window
LaunchExe  = "powershell.exe";
LaunchArgs = "-NoProfile -ExecutionPolicy Bypass -Command " +
             "\"Start-Process cmd.exe -ArgumentList '/K sfc /scannow' -Verb RunAs\"";

// WRONG — "file not found" in some contexts
LaunchExe  = "sfc";
LaunchArgs = "/scannow";
```

### ZIP Archives
Uses `ZipArchive` directly (no `System.IO.Compression.FileSystem` assembly needed):

```csharp
using (var fs  = new FileStream(archPath, FileMode.Create, FileAccess.Write))
using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, false))
{
    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
    using (var es = entry.Open())
    using (var src = File.OpenRead(filePath))
        src.CopyTo(es);
}
```

### SplitContainer Safety
`SplitterDistance` is only set after the container has a real size:

```csharp
void SafeSetSplitter()
{
    int total = mainSplit.Height;
    int minD  = mainSplit.Panel1MinSize;
    int maxD  = total - mainSplit.Panel2MinSize - mainSplit.SplitterWidth;
    if (maxD <= minD) return;  // not sized yet — skip
    int desired = Math.Max(minD, Math.Min((int)(total * 0.62f), maxD));
    try { mainSplit.SplitterDistance = desired; } catch { }
}
```

---

## 📝 Known Gotchas Fixed During Development

| # | Issue | Fix |
|---|---|---|
| 1 | `AdminHelper.ShowAdminBanner(this)` before `BuildUI()` → NRE | Always call after `BuildUI()` |
| 2 | `EnumerationOptions` → not available in .NET 4.x | Replaced with `Directory.GetFiles(..., SearchOption.AllDirectories)` |
| 3 | `ListViewItem.Invalidate()` → method doesn't exist | Call `listView.Invalidate()` instead |
| 4 | WMI "invalid query" | Separate `ManagementScope` from `ObjectQuery`, no `ORDER BY` on SystemRestore |
| 5 | `SplitterDistance` `InvalidOperationException` on startup | Guard with min/max clamp, only set in `SizeChanged` |
| 6 | Leading spaces in `Exe`/`Args` strings → "file not found" | All strings cleaned, system tools routed via `cmd.exe` and `pwsh.exe` |
| 7 | Blurry/double-rendered label text | Removed custom `Paint` handlers; use `AutoSize = true` + `MaximumSize` |
| 8 | `ZipFile.Open` → missing assembly | Use `ZipArchive` with manual stream copy |
| 9 | `netsh int ip reset` exits code 1 on success | Dual-mode detection: exit code OR output keyword |
| 10 | Windows 24-hour restore point throttle | Set `SystemRestorePointCreationFrequency = 0` via registry before creating |
| 11 | Duplicate `ListView` field declarations | Declare at class level only, assign in methods (no type keyword) |

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgements

- [Microsoft WUApiLib](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/portal) — Windows Update Agent API
- [CrystalDiskInfo](https://crystalmark.info/en/software/crystaldiskinfo/) — inspiration for SMART display
- [WinRAR](https://www.rarlab.com) — RAR format support (optional, must be installed separately)

---

*Built with ❤️ using C# WinForms · Dark theme inspired by GitHub's dark mode*
