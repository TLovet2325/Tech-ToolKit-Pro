# 🛠️ Tech ToolKit Pro

> A professional Windows maintenance and diagnostic desktop application built with C# WinForms (.NET Framework 4.x)

![Platform](https://img.shields.io/badge/platform-Windows-blue?logo=windows)
![Language](https://img.shields.io/badge/language-C%23-239120?logo=csharp)
![Framework](https://img.shields.io/badge/framework-.NET%20Framework%204.x-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📖 Overview

Tech ToolKit Pro is a dark-themed, all-in-one Windows system maintenance suite. It provides 20+ tools covering disk health, system scans, network resets, security checks, file recovery, and backup — all in a single polished desktop application.

---

## ✨ Features

- 📊 **Dashboard** — Live CPU, RAM and disk usage with real-time process table
- 🔧 **Disk Maintenance** — 9 tools including CHKDSK, SFC, DISM, Defrag and more
- 💾 **Disk SMART** — WMI-powered drive health, partitions and volume info
- 🧹 **Temp Cleanup** — Clear temp files, Windows cache and SoftwareDistribution
- 🌐 **Network Reset** — Flush DNS, WinSock reset and TCP/IP stack reset
- 🛡 **MRT Scan** — Microsoft Malicious Software Removal Tool with auto-detection
- 🔒 **Defender Scan** — Quick, Full, Custom and Boot-time scans via MpCmdRun
- 🔄 **File Recovery** — Recover from Recycle Bin, Shadow Copies and Windows Backup
- 🗄 **Windows Backup** — Backup files and folders to ZIP or WinRAR archive
- 🛡 **System Restore** — Create, restore and delete restore points
- 📦 **App Updates** — winget-powered update detection and installation
- 🔄 **Windows Update** — Search, download and install updates via WUApiLib COM
- 📋 **Task List** — Live process manager with RAM hog detection
- 🗑 **Uninstall Apps** — Registry-based app scanner with direct uninstall
- 📊 **System Report** — Hardware info, memory test and performance benchmark

---

## ⚙️ Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 / 11 (64-bit) |
| Framework | .NET Framework 4.7.2+ |
| IDE | Visual Studio 2019 / 2022 |
| NuGet | `System.Management` |
| COM Reference | `WUApiLib` (Windows Update form) |
| Optional | WinRAR (for RAR backup format) |
| Optional | `winget` / App Installer (for App Updates) |

---

## 🚀 Getting Started

```bash
git clone https://github.com/yourusername/TechToolKitPro.git
```

1. Open `Tech_ToolKit_Pro.sln` in Visual Studio
2. Restore NuGet packages
3. Add COM reference: `Project → Add Reference → COM → Windows Update Agent API`
4. Ensure `app.manifest` is set in Project Properties
5. Build and run **as Administrator**

---

## 📁 Project Structure

```
Tech_ToolKit_Pro/
├── Form1.cs                    Main shell with sidebar navigation
├── AdminHelper.cs              Centralised admin rights management
├── Program.cs                  Entry point
├── app.manifest                UAC elevation manifest
├── FormDashboard.cs
├── FormDiskMaintenance.cs
├── FormDiskSmart.cs
├── FormTempCleanUp.cs
├── FormUltraCleanUp.cs
├── FormFlushDNS.cs
├── FormMRTscan.cs
├── FormDefenderScan.cs
├── FormSmartScan.cs
├── FormPointofRestoration.cs
├── FormRecoveryFiles.cs
├── FormWinBackUp.cs
├── FormAppsUptates.cs
├── FormShowUpdate.cs
├── FormTaskList.cs
├── FormUninstallApps.cs
└── FormSystemReport.cs
```

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgements

- [Microsoft WUApiLib](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/portal) — Windows Update Agent API
- [CrystalDiskInfo](https://crystalmark.info/en/software/crystaldiskinfo/) — inspiration for SMART display
- [WinRAR](https://www.rarlab.com) — RAR format support (optional, must be installed separately)

---

*Built with ❤️ using C# WinForms · Dark theme inspired by GitHub's dark mode*
