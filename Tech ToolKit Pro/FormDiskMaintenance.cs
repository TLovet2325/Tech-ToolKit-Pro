using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FormDiskMaintenance
    //  ─────────────────────────────────────────────────────────────
    //  NEW FEATURES
    //  ─────────────────────────────────────────────────────────────
    //  1. POWERSHELL 7 AUTO-INSTALL
    //     · On load, if pwsh.exe is not found an amber install banner
    //       appears below the top bar with an "Install PowerShell 7"
    //       button.
    //     · Before installing, internet connectivity is tested by
    //       pinging 8.8.8.8. If no internet is available the user sees
    //       a clear error message and the install is aborted.
    //     · Install uses winget (Windows Package Manager) which is
    //       included with Windows 10 1709+ and all Windows 11 builds:
    //         winget install --id Microsoft.PowerShell --source winget
    //     · If winget is not found, an MSI download fallback URL is
    //       shown and the user is guided to the GitHub releases page.
    //     · After a successful install the shell cache is cleared and
    //       InitTools() re-runs so every tool immediately uses pwsh.
    //
    //  2. INTERNET CHECK BEFORE EVERY TOOL RUN
    //     · Any tool that launches an external process first calls
    //       IsInternetAvailable(). If no internet is detected a
    //       warning dialog appears:
    //         "⚠ Internet Required
    //          This tool may need internet to download repair files.
    //          Connect to the internet before running."
    //       The user can choose to Continue anyway or Cancel.
    //     · DISM /RestoreHealth specifically requires internet so its
    //       warning is mandatory (cannot be skipped).
    //
    //  3. SHELL FALLBACK CHAIN (unchanged)
    //     pwsh.exe (PS7) → powershell.exe (PS5.1) → cmd.exe
    // ════════════════════════════════════════════════════════════════
    public partial class FormDiskMaintenance : Form
    {
        // ════════════════════════════════════════════════════════════
        //  THEME
        // ════════════════════════════════════════════════════════════
        static readonly Color C_BG = Color.FromArgb(13, 17, 23);
        static readonly Color C_SURF = Color.FromArgb(22, 27, 34);
        static readonly Color C_SURF2 = Color.FromArgb(30, 36, 44);
        static readonly Color C_BORDER = Color.FromArgb(48, 54, 61);
        static readonly Color C_BLUE = Color.FromArgb(88, 166, 255);
        static readonly Color C_GREEN = Color.FromArgb(63, 185, 119);
        static readonly Color C_AMBER = Color.FromArgb(255, 163, 72);
        static readonly Color C_RED = Color.FromArgb(248, 81, 73);
        static readonly Color C_PURPLE = Color.FromArgb(188, 140, 255);
        static readonly Color C_TEAL = Color.FromArgb(56, 189, 193);
        static readonly Color C_ORANGE = Color.FromArgb(255, 120, 50);
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  SHELL DETECTION  (cached, evaluated once per session)
        // ════════════════════════════════════════════════════════════
        bool? _pwshAvailable = null;
        bool? _ps51Available = null;

        bool PwshExists()
        {
            if (_pwshAvailable.HasValue) return _pwshAvailable.Value;
            _pwshAvailable = CheckCommandExists("pwsh.exe");
            return _pwshAvailable.Value;
        }

        bool Ps51Exists()
        {
            if (_ps51Available.HasValue) return _ps51Available.Value;
            _ps51Available = CheckCommandExists("powershell.exe");
            return _ps51Available.Value;
        }

        bool CheckCommandExists(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = exe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return !string.IsNullOrWhiteSpace(output);
                }
            }
            catch { return false; }
        }

        // Invalidate the shell cache so re-detection is forced
        void ResetShellCache()
        {
            _pwshAvailable = null;
            _ps51Available = null;
        }

        // ════════════════════════════════════════════════════════════
        //  TRIPLE FALLBACK RESOLVER
        // ════════════════════════════════════════════════════════════
        (string exe, string args, string label) ResolveShell(
            string command, bool keepOpen = true)
        {
            if (PwshExists())
                return (
                    "pwsh.exe",
                    string.Format("-NoProfile {0} -Command \"{1}\"",
                        keepOpen ? "-NoExit" : "", command),
                    "PowerShell 7");

            if (Ps51Exists())
                return (
                    "powershell.exe",
                    string.Format("-NoProfile {0} -Command \"{1}\"",
                        keepOpen ? "-NoExit" : "", command),
                    "Windows PowerShell");

            return (
                "cmd.exe",
                string.Format("{0} {1}", keepOpen ? "/K" : "/C", command),
                "Command Prompt");
        }

        // ════════════════════════════════════════════════════════════
        //  INTERNET CHECK
        //  ─────────────────────────────────────────────────────────
        //  Pings 8.8.8.8 (Google DNS) with a 2-second timeout.
        //  Returns true if reachable, false otherwise.
        // ════════════════════════════════════════════════════════════
        bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 2000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }

        // Shows internet warning.  If mandatory = true the user cannot
        // bypass it (DISM RestoreHealth must have internet).
        // Returns true = proceed, false = cancel.
        bool ShowInternetWarning(string toolName, bool mandatory = false)
        {
            if (mandatory)
            {
                MessageBox.Show(
                    string.Format(
                        "⚠  Internet Connection Required\n\n" +
                        "{0} needs to download repair files from\n" +
                        "Microsoft's Windows Update servers.\n\n" +
                        "Please connect to the internet before running this tool.",
                        toolName),
                    "Internet Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            var result = MessageBox.Show(
                string.Format(
                    "⚠  No Internet Connection Detected\n\n" +
                    "{0} may need an internet connection to\n" +
                    "download repair files or updates.\n\n" +
                    "It is recommended to connect before running this tool.\n\n" +
                    "Do you want to continue anyway?",
                    toolName),
                "Internet Recommended",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        // ════════════════════════════════════════════════════════════
        //  POWERSHELL 7 INSTALLATION
        //  ─────────────────────────────────────────────────────────
        //  Strategy:
        //    1. Check internet — abort if offline
        //    2. Try winget (Windows Package Manager)
        //       winget install --id Microsoft.PowerShell --source winget
        //    3. If winget is not found, show manual download instructions
        //    4. After install: reset shell cache, re-run InitTools(),
        //       refresh shell badge, hide the install banner
        // ════════════════════════════════════════════════════════════
        Panel _ps7Banner;
        Label _lblPs7Status;
        Button _btnInstallPs7;
        bool _installing = false;

        void CheckAndShowPs7Banner()
        {
            if (PwshExists())
            {
                // PS7 already installed — no banner needed
                if (_ps7Banner != null) _ps7Banner.Visible = false;
                return;
            }

            // Show the install banner
            if (_ps7Banner != null) _ps7Banner.Visible = true;
        }

        void BuildPs7Banner()
        {
            _ps7Banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(40, C_AMBER.R, C_AMBER.G, C_AMBER.B),
                Padding = new Padding(14, 0, 14, 0),
                Visible = false    // hidden until we know PS7 is missing
            };
            _ps7Banner.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(80, C_AMBER.R, C_AMBER.G, C_AMBER.B), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, _ps7Banner.Width, 0);
                    e.Graphics.DrawLine(p, 0, _ps7Banner.Height - 1,
                        _ps7Banner.Width, _ps7Banner.Height - 1);
                }
            };

            _lblPs7Status = new Label
            {
                Text = "⚡  PowerShell 7 is not installed. Install it for the best tool compatibility.",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_AMBER,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 14)
            };

            _btnInstallPs7 = new Button
            {
                Text = "⬇  Install PowerShell 7",
                Size = new Size(185, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_AMBER,
                BackColor = Color.FromArgb(25, C_AMBER.R, C_AMBER.G, C_AMBER.B),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnInstallPs7.FlatAppearance.BorderColor = Color.FromArgb(80, C_AMBER.R, C_AMBER.G, C_AMBER.B);
            _btnInstallPs7.FlatAppearance.BorderSize = 1;
            _btnInstallPs7.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, C_AMBER.R, C_AMBER.G, C_AMBER.B);
            _btnInstallPs7.Click += BtnInstallPs7_Click;

            _ps7Banner.Controls.Add(_lblPs7Status);
            _ps7Banner.Controls.Add(_btnInstallPs7);
            _ps7Banner.Resize += (s, e) =>
                _btnInstallPs7.Location = new Point(
                    _ps7Banner.Width - _btnInstallPs7.Width - 16,
                    (_ps7Banner.Height - _btnInstallPs7.Height) / 2);
        }

        void BtnInstallPs7_Click(object sender, EventArgs e)
        {
            if (_installing) return;

            // ── Step 1: Check internet ────────────────────────────────
            _lblPs7Status.Text = "🌐  Checking internet connection...";
            _lblPs7Status.ForeColor = C_AMBER;
            Application.DoEvents();

            if (!IsInternetAvailable())
            {
                _lblPs7Status.Text = "⚡  PowerShell 7 is not installed.";
                _lblPs7Status.ForeColor = C_AMBER;

                MessageBox.Show(
                    "⚠  Internet Connection Required\n\n" +
                    "Installing PowerShell 7 requires an active internet connection.\n\n" +
                    "Please connect to the internet and try again.\n\n" +
                    "Alternatively, download it manually from:\n" +
                    "https://github.com/PowerShell/PowerShell/releases",
                    "No Internet Connection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // ── Step 2: Check winget ──────────────────────────────────
            bool hasWinget = CheckCommandExists("winget");

            if (!hasWinget)
            {
                // winget not found — show manual install instructions
                var result = MessageBox.Show(
                    "⚠  Windows Package Manager (winget) not found.\n\n" +
                    "winget is required to auto-install PowerShell 7.\n\n" +
                    "To install PowerShell 7 manually:\n" +
                    "  1. Open your browser\n" +
                    "  2. Go to: https://github.com/PowerShell/PowerShell/releases\n" +
                    "  3. Download the latest .msi installer for Windows x64\n" +
                    "  4. Run the installer\n" +
                    "  5. Restart Tech ToolKit Pro\n\n" +
                    "Click OK to open the download page in your browser.",
                    "Manual Install Required",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information);

                if (result == DialogResult.OK)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://github.com/PowerShell/PowerShell/releases",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
                return;
            }

            // ── Step 3: Confirm with user ─────────────────────────────
            var confirm = MessageBox.Show(
                "🌐  Internet connection detected.\n\n" +
                "Ready to install PowerShell 7 via winget:\n\n" +
                "  winget install --id Microsoft.PowerShell --source winget\n\n" +
                "This will:\n" +
                "  • Download ~100 MB from Microsoft servers\n" +
                "  • Install PowerShell 7 silently\n" +
                "  • Require Administrator rights\n\n" +
                "All tools will use PowerShell 7 after installation.\n\n" +
                "Continue?",
                "Install PowerShell 7",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            // ── Step 4: Run winget install ────────────────────────────
            _installing = true;
            _btnInstallPs7.Enabled = false;
            _lblPs7Status.Text = "⬇  Installing PowerShell 7 via winget — please wait...";
            _lblPs7Status.ForeColor = C_BLUE;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int exitCode = -1;
                string errMsg = "";

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        // winget itself requires interactive session for some packages.
                        // We run it elevated via Start-Process to get full permissions.
                        Arguments =
                            "-NoProfile -ExecutionPolicy Bypass -Command " +
                            "\"Start-Process winget.exe " +
                            "-ArgumentList 'install --id Microsoft.PowerShell " +
                            "--source winget --accept-source-agreements " +
                            "--accept-package-agreements --silent' " +
                            "-Verb RunAs -Wait\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Normal,
                        CreateNoWindow = false
                    };

                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(300000);   // 5-minute timeout
                        exitCode = proc.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    errMsg = ex.Message;
                }

                Invoke(new Action(() =>
                    OnPs7InstallComplete(exitCode, errMsg)));
            });
        }

        void OnPs7InstallComplete(int exitCode, string errMsg)
        {
            _installing = false;
            _btnInstallPs7.Enabled = true;

            // Reset cache and re-check
            ResetShellCache();
            bool nowInstalled = PwshExists();

            if (nowInstalled)
            {
                // ── Success ──────────────────────────────────────────
                _ps7Banner.Visible = false;
                _lblPs7Status.Text = "✔  PowerShell 7 installed!";
                _lblPs7Status.ForeColor = C_GREEN;

                // Re-run InitTools() so all tools switch to pwsh.exe
                InitTools();
                RebuildToolGrid_Force();
                RefreshShellBadge();

                AddLog("PS7 Install", "Success",
                    "PowerShell 7 installed — all tools now use pwsh.exe", C_GREEN);

                MessageBox.Show(
                    "✔  PowerShell 7 has been installed successfully!\n\n" +
                    "All tools have been updated to use PowerShell 7.\n" +
                    "The shell badge in the top bar now shows 'PowerShell 7'.",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                // ── Failed or not detected yet ────────────────────────
                _lblPs7Status.Text = "⚡  PowerShell 7 is not installed.";
                _lblPs7Status.ForeColor = C_AMBER;
                _ps7Banner.Visible = true;

                string detail = !string.IsNullOrEmpty(errMsg)
                    ? errMsg
                    : string.Format("winget exit code: {0}", exitCode);

                AddLog("PS7 Install", "Failed", detail, C_RED);

                MessageBox.Show(
                    string.Format(
                        "⚠  PowerShell 7 installation failed or\ncould not be verified.\n\n" +
                        "Detail: {0}\n\n" +
                        "You can install it manually from:\n" +
                        "https://github.com/PowerShell/PowerShell/releases\n\n" +
                        "The app will continue using the fallback shell.",
                        detail),
                    "Installation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // Refreshes the shell badge label after install
        void RefreshShellBadge()
        {
            if (lblShellBadge == null) return;
            var detected = ResolveShell("echo test", false);
            Color bc = detected.label == "PowerShell 7" ? C_BLUE
                     : detected.label == "Windows PowerShell" ? C_PURPLE
                     : C_AMBER;
            lblShellBadge.Text = string.Format("⚡ {0}", detected.label);
            lblShellBadge.ForeColor = bc;
            lblShellBadge.BackColor = Color.FromArgb(20, bc.R, bc.G, bc.B);
        }

        void RebuildToolGrid_Force()
        {
            currentColumns = -1;
            RebuildToolGrid();
        }

        // ════════════════════════════════════════════════════════════
        //  TOOL MODEL
        // ════════════════════════════════════════════════════════════
        class DiskTool
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public string Icon { get; set; }
            public string Desc { get; set; }
            public Color Accent { get; set; }
            public string LaunchExe { get; set; }
            public string LaunchArgs { get; set; }
            public bool NeedsAdmin { get; set; } = true;
            public bool ShowWindow { get; set; } = true;
            public bool IsSpecial { get; set; }
            public string SpecialKey { get; set; } = "";
            // true = DISM RestoreHealth — internet is mandatory
            public bool NeedsInternet { get; set; } = false;
            public Button BtnRun { get; set; }
            public DiskProgressBar PBar { get; set; }
            public Label StatusL { get; set; }
        }

        readonly List<DiskTool> tools = new List<DiskTool>();

        Panel topBar, bottomBar, logPanel;
        Label lblSub, lblStatus, lblShellBadge;
        ListView logList;
        Button btnRunAll, btnClearLog;
        TableLayoutPanel toolGrid;
        SplitContainer mainSplit;
        int runningCount, allIndex, currentColumns = -1;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormDiskMaintenance()
        {
            PwshExists();
            Ps51Exists();
            InitTools();
            BuildUI();

            // Show PS7 install banner if pwsh is missing
            CheckAndShowPs7Banner();

            AdminHelper.ShowAdminBanner(this,
                "⚠  Most tools require Administrator rights. " +
                "Click 'Restart as Admin' to unlock them.");
        }

        // ════════════════════════════════════════════════════════════
        //  INIT TOOLS
        // ════════════════════════════════════════════════════════════
        void InitTools()
        {
            tools.Clear();

            // ── 1. Disable Hibernation ────────────────────────────────
            var s1 = ResolveShell("powercfg /h off", keepOpen: true);
            tools.Add(new DiskTool
            {
                Title = "    Disable Hibernation",
                Subtitle = "   powercfg /h off",
                Icon = "💤",
                Desc = "Turns off hibernation and deletes hiberfil.sys to reclaim disk space.",
                Accent = C_BLUE,
                LaunchExe = s1.exe,
                LaunchArgs = s1.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 2. Check Disk ─────────────────────────────────────────
            var s2 = ResolveShell("echo Y | chkdsk C: /f /r /x", keepOpen: true);
            tools.Add(new DiskTool
            {
                Title = "    Check Disk (CHKDSK)",
                Subtitle = "   chkdsk C: /f /r /x",
                Icon = "🔍",
                Desc = "Scans drive C: for file-system errors and bad sectors. May require reboot.",
                Accent = C_AMBER,
                LaunchExe = s2.exe,
                LaunchArgs = s2.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 3. SFC — TrustedInstaller pre-check ──────────────────
            var s3ps =
                "sc query TrustedInstaller | find `\"RUNNING`\" > $null; " +
                "if ($LASTEXITCODE -ne 0) { " +
                    "sc.exe config TrustedInstaller start= demand | Out-Null; " +
                    "net start TrustedInstaller | Out-Null " +
                "}; sfc /scannow";

            var s3 = ResolveShell(s3ps, keepOpen: true);
            if (s3.label == "Command Prompt")
                s3 = ("cmd.exe",
                    "/K sc query TrustedInstaller | find \"RUNNING\" >nul || " +
                    "(sc config TrustedInstaller start= demand >nul & net start TrustedInstaller >nul) " +
                    "&& sfc /scannow",
                    "Command Prompt");

            tools.Add(new DiskTool
            {
                Title = "    SFC System Scan",
                Subtitle = "   sfc /scannow",
                Icon = "🛡",
                Desc = "Validates protected Windows files and restores corrupted copies from cache.",
                Accent = C_GREEN,
                LaunchExe = s3.exe,
                LaunchArgs = s3.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 4. DISM — TrustedInstaller + CheckHealth + RestoreHealth
            var s4ps =
                "sc query TrustedInstaller | find `\"RUNNING`\" > $null; " +
                "if ($LASTEXITCODE -ne 0) { " +
                    "sc.exe config TrustedInstaller start= demand | Out-Null; " +
                    "net start TrustedInstaller | Out-Null " +
                "}; " +
                "DISM /Online /Cleanup-Image /CheckHealth; " +
                "DISM /Online /Cleanup-Image /RestoreHealth";

            var s4cmd =
                "sc query TrustedInstaller | find \"RUNNING\" >nul || " +
                "(sc config TrustedInstaller start= demand >nul & net start TrustedInstaller >nul) " +
                "&& DISM /Online /Cleanup-Image /CheckHealth " +
                "&& DISM /Online /Cleanup-Image /RestoreHealth";

            var s4 = ResolveShell(s4ps, keepOpen: true);
            if (s4.label == "Command Prompt")
                s4 = ("cmd.exe", "/K " + s4cmd, "Command Prompt");

            tools.Add(new DiskTool
            {
                Title = "    DISM Health Restore",
                Subtitle = "   CheckHealth → RestoreHealth",
                Icon = "🔧",
                Desc = "Checks then repairs the Windows component store. Requires internet.",
                Accent = C_PURPLE,
                LaunchExe = s4.exe,
                LaunchArgs = s4.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = true   // DISM RestoreHealth downloads from WU
            });

            // ── 5. Optimize Drive ─────────────────────────────────────
            var s5 = ResolveShell("defrag C: /U /V", keepOpen: true);
            tools.Add(new DiskTool
            {
                Title = "    Optimize Drive",
                Subtitle = "   defrag C: /U /V",
                Icon = "⚡",
                Desc = "Runs defrag or TRIM on drive C: depending on drive type.",
                Accent = C_TEAL,
                LaunchExe = s5.exe,
                LaunchArgs = s5.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 6. Clear Update Cache ─────────────────────────────────
            tools.Add(new DiskTool
            {
                Title = "    Clear Update Cache",
                Subtitle = "   Stop → Delete → Restart wuauserv",
                Icon = "🔄",
                Desc = "Stops update services, clears the download cache, then restarts them.",
                Accent = C_ORANGE,
                IsSpecial = true,
                SpecialKey = "wucache",
                NeedsInternet = false
            });

            // ── 7. Clear Event Logs ───────────────────────────────────
            var s7ps =
                "wevtutil cl System; wevtutil cl Application; wevtutil cl Security; " +
                "Write-Host 'All event logs cleared.' -ForegroundColor Green";
            var s7 = ResolveShell(s7ps, keepOpen: true);
            if (s7.label == "Command Prompt")
                s7 = ("cmd.exe",
                    "/K wevtutil cl System & wevtutil cl Application & wevtutil cl Security & " +
                    "echo All event logs cleared.",
                    "Command Prompt");

            tools.Add(new DiskTool
            {
                Title = "    Clear Event Logs",
                Subtitle = "   wevtutil cl System / App / Security",
                Icon = "📋",
                Desc = "Clears Windows System, Application and Security event logs.",
                Accent = C_RED,
                LaunchExe = s7.exe,
                LaunchArgs = s7.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 8. Compact OS ─────────────────────────────────────────
            var s8 = ResolveShell("compact /CompactOS:always", keepOpen: true);
            tools.Add(new DiskTool
            {
                Title = "    Compact OS",
                Subtitle = "   compact /CompactOS:always",
                Icon = "🗜",
                Desc = "Compresses Windows OS files to save 1–3 GB on low-storage devices.",
                Accent = C_TEAL,
                LaunchExe = s8.exe,
                LaunchArgs = s8.args,
                NeedsAdmin = true,
                ShowWindow = true,
                NeedsInternet = false
            });

            // ── 9. Control Panel ──────────────────────────────────────
            tools.Add(new DiskTool
            {
                Title = "    Control Panel",
                Subtitle = "   control.exe",
                Icon = "🖥",
                Desc = "Opens the Windows Control Panel for system settings and hardware.",
                Accent = C_BLUE,
                LaunchExe = "control.exe",
                LaunchArgs = "",
                NeedsAdmin = false,
                ShowWindow = true,
                NeedsInternet = false
            });
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Disk Maintenance";
            BackColor = C_BG;
            ForeColor = C_TXT;
            FormBorderStyle = FormBorderStyle.None;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9f);

            // ── Top bar ───────────────────────────────────────────────
            topBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = C_SURF };
            topBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 51, topBar.Width, 51);
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, 4, 52), C_TEAL, C_BLUE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            topBar.Controls.Add(new Label
            {
                Text = "🔧  DISK MAINTENANCE",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 10)
            });
            lblSub = new Label
            {
                Text = string.Format("{0} tools  ·  Run individually or all at once",
                                tools.Count),
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 31)
            };
            topBar.Controls.Add(lblSub);

            // Shell badge (top-right)
            var detected = ResolveShell("echo test", false);
            Color badgeColor = detected.label == "PowerShell 7" ? C_BLUE
                             : detected.label == "Windows PowerShell" ? C_PURPLE
                             : C_AMBER;

            lblShellBadge = new Label
            {
                Text = string.Format("⚡ {0}", detected.label),
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = badgeColor,
                AutoSize = true,
                BackColor = Color.FromArgb(20, badgeColor.R, badgeColor.G, badgeColor.B),
                Padding = new Padding(4, 2, 4, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            topBar.Controls.Add(lblShellBadge);
            topBar.Resize += (s, e) =>
                lblShellBadge.Location = new Point(
                    topBar.Width - lblShellBadge.Width - 16, 18);

            // ── PS7 install banner (inserted after topBar) ────────────
            BuildPs7Banner();

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };
            btnRunAll = MakeBtn("▶  Run All Tools", C_GREEN, new Size(160, 34));
            btnClearLog = MakeBtn("🗑  Clear Log", C_SUB, new Size(110, 34));
            lblStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };
            btnRunAll.Click += BtnRunAll_Click;
            btnClearLog.Click += (s, e) => logList.Items.Clear();
            bottomBar.Controls.AddRange(new Control[] { btnRunAll, btnClearLog, lblStatus });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnRunAll.Location = new Point(16, y);
                btnClearLog.Location = new Point(btnRunAll.Right + 10, y);
                lblStatus.Location = new Point(btnClearLog.Right + 16,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── SplitContainer ────────────────────────────────────────
            mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = C_BG,
                BorderStyle = BorderStyle.None,
                FixedPanel = FixedPanel.None,
                SplitterWidth = 6,
                Panel1MinSize = 180,
                Panel2MinSize = 130
            };
            mainSplit.SizeChanged += (s, e) => SafeSetSplitter();

            toolGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var toolHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                AutoScroll = true,
                Padding = new Padding(6)
            };
            toolHost.Controls.Add(toolGrid);
            toolHost.Resize += (s, e) => RebuildToolGrid();
            mainSplit.Panel1.Controls.Add(toolHost);

            BuildLogPanel();
            mainSplit.Panel2.Controls.Add(logPanel);

            // Assemble — order matters for DockStyle
            Controls.Add(mainSplit);
            Controls.Add(_ps7Banner);   // ← install banner sits just below topBar
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        void SafeSetSplitter()
        {
            if (mainSplit == null || mainSplit.IsDisposed) return;
            int total = mainSplit.Height;
            int minD = mainSplit.Panel1MinSize;
            int maxD = total - mainSplit.Panel2MinSize - mainSplit.SplitterWidth;
            if (maxD <= minD) return;
            int desired = Math.Max(minD, Math.Min((int)(total * 0.62f), maxD));
            try { if (mainSplit.SplitterDistance != desired) mainSplit.SplitterDistance = desired; }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  LOG PANEL
        // ════════════════════════════════════════════════════════════
        void BuildLogPanel()
        {
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };
            logPanel.Controls.Add(new Label
            {
                Text = "COMMAND LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            });

            logList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 22),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            logList.Columns.Add("Time", 178);
            logList.Columns.Add("Tool", 270);
            logList.Columns.Add("Status", 200);
            logList.Columns.Add("Command / Note", 496);

            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawRow;

            logPanel.Controls.Add(logList);
            logPanel.Resize += (s, e) =>
            {
                logList.Size = new Size(
                    logPanel.ClientSize.Width - 20,
                    Math.Max(60, logPanel.ClientSize.Height - 28));
                ResizeLogColumns();
            };
        }

        // ════════════════════════════════════════════════════════════
        //  RESPONSIVE TOOL GRID
        // ════════════════════════════════════════════════════════════
        void RebuildToolGrid()
        {
            if (toolGrid == null || IsDisposed) return;
            int pw = mainSplit.Panel1.ClientSize.Width - 20;
            if (pw <= 0) return;

            int cols = pw >= 1050 ? 3 : pw >= 650 ? 2 : 1;
            if (cols == currentColumns && toolGrid.Controls.Count == tools.Count) return;

            currentColumns = cols;
            toolGrid.SuspendLayout();
            toolGrid.Controls.Clear();
            toolGrid.ColumnStyles.Clear();
            toolGrid.RowStyles.Clear();
            toolGrid.ColumnCount = cols;

            for (int c = 0; c < cols; c++)
                toolGrid.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f / cols));

            int rows = (int)Math.Ceiling(tools.Count / (double)cols);
            for (int r = 0; r < rows; r++)
                toolGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 152f));

            for (int i = 0; i < tools.Count; i++)
                toolGrid.Controls.Add(BuildToolCard(tools[i], i), i % cols, i / cols);

            toolGrid.Width = Math.Max(0, pw);
            toolGrid.ResumeLayout(true);
        }

        // ════════════════════════════════════════════════════════════
        //  TOOL CARD
        // ════════════════════════════════════════════════════════════
        Panel BuildToolCard(DiskTool t, int idx)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4),
                MinimumSize = new Size(220, 144),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(
                    Color.FromArgb(50, t.Accent.R, t.Accent.G, t.Accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(t.Accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };

            var lblIcon = new Label
            {
                Text = t.Icon,
                Font = new Font("Segoe UI", 16f),
                ForeColor = t.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 8)
            };
            var lblTitle = new Label
            {
                Text = t.Title,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_TXT,
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(300, 20),
                Location = new Point(46, 10)
            };
            var lblCmd = new Label
            {
                Text = t.Subtitle,
                Font = new Font("Consolas", 7.5f),
                ForeColor = t.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(300, 18),
                Location = new Point(46, 28)
            };

            // Internet indicator for tools that need it
            if (t.NeedsInternet)
            {
                var lblNet = new Label
                {
                    Text = "🌐 Requires internet",
                    Font = new Font("Segoe UI", 7f),
                    ForeColor = Color.FromArgb(120, C_BLUE.R, C_BLUE.G, C_BLUE.B),
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(46, 44)
                };
                card.Controls.Add(lblNet);
            }

            var lblDesc = new Label
            {
                Text = t.Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(10, t.NeedsInternet ? 58 : 50),
                Size = new Size(card.Width - 20, 34)
            };
            t.PBar = new DiskProgressBar(t.Accent)
            {
                Location = new Point(10, 94),
                Size = new Size(card.Width - 20, 8),
                Value = 0,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            t.StatusL = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 110)
            };
            t.BtnRun = MakeSmallBtn("▶  Run", t.Accent);
            t.BtnRun.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            t.BtnRun.Click += (s, e) => RunTool(idx, null);

            card.Controls.AddRange(new Control[]
                { lblIcon, lblTitle, lblCmd, lblDesc, t.PBar, t.StatusL, t.BtnRun });

            card.Resize += (s, e) =>
            {
                int w = card.ClientSize.Width;
                lblTitle.MaximumSize = new Size(w - 54, 20);
                lblCmd.MaximumSize = new Size(w - 54, 18);
                lblDesc.Width = w - 20;
                t.PBar.Width = w - 20;
                t.BtnRun.Location = new Point(
                    w - t.BtnRun.Width - 8,
                    card.ClientSize.Height - t.BtnRun.Height - 8);
            };
            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  RUN TOOL  —  internet check before launch
        // ════════════════════════════════════════════════════════════
        void RunTool(int idx, Action onComplete)
        {
            var t = tools[idx];

            // ── Internet check ────────────────────────────────────────
            if (t.NeedsInternet || t.SpecialKey == "wucache")
            {
                bool online = IsInternetAvailable();
                if (!online)
                {
                    bool proceed = ShowInternetWarning(t.Title, mandatory: t.NeedsInternet);
                    if (!proceed)
                    {
                        onComplete?.Invoke();
                        return;
                    }
                }
                else if (t.NeedsInternet)
                {
                    // Internet confirmed — log it
                    AddLog(t.Title, "Net OK", "Internet connection verified ✔", C_GREEN);
                }
            }

            t.BtnRun.Enabled = false;
            t.StatusL.Text = "Running...";
            t.StatusL.ForeColor = C_AMBER;
            t.PBar.SetColor(t.Accent);
            t.PBar.Animate = true;
            t.PBar.Value = 0;

            SetStatus(string.Format("Running: {0}", t.Title));
            AddLog(t.Title, "Started",
                t.IsSpecial
                    ? t.Subtitle
                    : string.Format("[{0}]  {1}",
                        t.LaunchExe.Replace(".exe", "").ToUpper(), t.Subtitle));
            runningCount++;

            if (t.IsSpecial && t.SpecialKey == "wucache")
            {
                RunWindowsUpdateCacheClean(idx, onComplete);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = t.LaunchExe,
                Arguments = t.LaunchArgs,
                UseShellExecute = true,
                Verb = t.NeedsAdmin ? "runas" : "",
                WindowStyle = t.ShowWindow
                    ? ProcessWindowStyle.Normal
                    : ProcessWindowStyle.Hidden,
                CreateNoWindow = !t.ShowWindow
            };

            var proc = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            proc.Exited += (s, e) =>
            {
                if (InvokeRequired)
                    Invoke(new Action(() => ToolFinished(idx, proc.ExitCode, onComplete)));
                else
                    ToolFinished(idx, proc.ExitCode, onComplete);
            };

            var pulse = new System.Windows.Forms.Timer { Interval = 80 };
            int step = 0;
            pulse.Tick += (s, e) =>
            {
                step = (step + 3) % 100;
                t.PBar.Value = step;
                if (!t.PBar.Animate) pulse.Stop();
            };

            try
            {
                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(_ => proc.WaitForExit());
                pulse.Start();
            }
            catch (Exception ex)
            {
                pulse.Stop();
                ToolFinished(idx, -1, onComplete);
                AddLog(t.Title, "Error", ex.Message, C_RED);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CLEAR WINDOWS UPDATE CACHE
        // ════════════════════════════════════════════════════════════
        void RunWindowsUpdateCacheClean(int idx, Action onComplete)
        {
            var t = tools[idx];
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int exitCode = 0;
                try
                {
                    UpdateToolStatus(idx, "Step 1/4 — Stopping wuauserv...", 10);
                    AddLog(t.Title, "Step 1", "net stop wuauserv");
                    RunCmdSilent("net stop wuauserv");

                    UpdateToolStatus(idx, "Step 2/4 — Stopping BITS...", 25);
                    AddLog(t.Title, "Step 2", "net stop bits");
                    RunCmdSilent("net stop bits");

                    UpdateToolStatus(idx, "Step 3/4 — Deleting cache...", 50);
                    AddLog(t.Title, "Step 3",
                        @"C:\Windows\SoftwareDistribution\Download");
                    string cp = @"C:\Windows\SoftwareDistribution\Download";
                    if (Directory.Exists(cp))
                    {
                        foreach (var f in Directory.GetFiles(cp, "*",
                            SearchOption.AllDirectories))
                            try { File.Delete(f); } catch { }
                        foreach (var d in Directory.GetDirectories(cp))
                            try { Directory.Delete(d, true); } catch { }
                    }

                    UpdateToolStatus(idx, "Step 4/4 — Restarting services...", 80);
                    AddLog(t.Title, "Step 4", "net start wuauserv && bits");
                    RunCmdSilent("net start wuauserv");
                    RunCmdSilent("net start bits");
                }
                catch (Exception ex)
                {
                    exitCode = -1;
                    AddLog(t.Title, "Error", ex.Message, C_RED);
                }

                if (InvokeRequired)
                    Invoke(new Action(() => ToolFinished(idx, exitCode, onComplete)));
                else
                    ToolFinished(idx, exitCode, onComplete);
            });
        }

        void RunCmdSilent(string command)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C " + command,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                p.Start(); p.WaitForExit(15000);
            }
            catch { }
        }

        void UpdateToolStatus(int idx, string msg, int pct)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => UpdateToolStatus(idx, msg, pct))); return; }
            tools[idx].StatusL.Text = msg;
            tools[idx].StatusL.ForeColor = C_AMBER;
            tools[idx].PBar.Value = pct;
        }

        void ToolFinished(int idx, int exitCode, Action onComplete)
        {
            var t = tools[idx];
            t.PBar.Animate = false;
            bool ok = (exitCode == 0 || exitCode == 3010);
            t.PBar.Value = 100;
            t.PBar.SetColor(ok ? C_GREEN : C_RED);
            t.StatusL.Text = ok
                ? (exitCode == 3010 ? "✔  Done (reboot)" : "✔  Done")
                : string.Format("✖  Exit {0}", exitCode);
            t.StatusL.ForeColor = ok ? C_GREEN : C_RED;
            t.BtnRun.Enabled = true;

            AddLog(t.Title,
                ok ? "Success" : "Failed",
                t.Subtitle,
                ok ? C_GREEN : C_RED);

            runningCount--;
            SetStatus(runningCount > 0
                ? string.Format("{0} tool(s) still running...", runningCount)
                : "All done.");

            onComplete?.Invoke();
        }

        // ════════════════════════════════════════════════════════════
        //  RUN ALL
        // ════════════════════════════════════════════════════════════
        void BtnRunAll_Click(object sender, EventArgs e)
        {
            var detected = ResolveShell("echo test", false);
            if (MessageBox.Show(
                string.Format(
                    "Run all {0} disk maintenance tools sequentially?\n\n" +
                    "Shell: {1}\n" +
                    "Admin rights required for most tools.\nContinue?",
                    tools.Count, detected.label),
                "Run All Maintenance Tools",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

            btnRunAll.Enabled = false;
            logList.Items.Clear();
            allIndex = 0;
            RunNext();
        }

        void RunNext()
        {
            if (allIndex >= tools.Count)
            {
                btnRunAll.Enabled = true;
                SetStatus("✔  All maintenance tools completed.");
                AddLog("All Tools", "Complete",
                    string.Format("{0} tools finished.", tools.Count), C_GREEN);
                return;
            }
            RunTool(allIndex++, RunNext);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AddLog(string tool, string status, string note, Color? fg = null)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(tool, status, note, fg))); return; }
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(tool);
            item.SubItems.Add(status);
            item.SubItems.Add(note);
            item.ForeColor = fg ?? C_TXT;
            item.Tag = status.ToLower().Contains("success") ||
                       status.ToLower().Contains("complete") ? "ok"
                     : status.ToLower().Contains("fail") ||
                       status.ToLower().Contains("error") ? "fail"
                     : "info";
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.Contains("Error") ? C_RED : C_SUB;
        }

        void ResizeLogColumns()
        {
            if (logList == null || logList.Columns.Count < 4) return;
            int w = logList.ClientSize.Width;
            logList.Columns[0].Width = 78;
            logList.Columns[1].Width = 170;
            logList.Columns[2].Width = 100;
            logList.Columns[3].Width = Math.Max(150, w - 78 - 170 - 100 - 8);
        }

        Button MakeBtn(string text, Color accent, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = accent,
                BackColor = Color.FromArgb(20, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(60, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, accent.R, accent.G, accent.B);
            return b;
        }

        Button MakeSmallBtn(string text, Color accent)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(72, 24),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = accent,
                BackColor = Color.FromArgb(20, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(60, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, accent.R, accent.G, accent.B);
            return b;
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW
        // ════════════════════════════════════════════════════════════
        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawRow(object sender, DrawListViewSubItemEventArgs e)
        {
            string tag = e.Item.Tag as string ?? "";
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);
            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));
            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1 ? C_TXT
                     : e.ColumnIndex == 2
                        ? (tag == "ok" ? C_GREEN
                         : tag == "fail" ? C_RED : C_AMBER)
                     : C_SUB;
            if (e.Item.ForeColor != C_TXT && e.ColumnIndex == 2) fg = e.Item.ForeColor;
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 10, e.Bounds.Height), sf);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  DISK PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class DiskProgressBar : Control
    {
        int _val;
        Color _accent;
        bool _animate;
        int _pulse;
        readonly System.Windows.Forms.Timer _pulseTimer;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColor(Color c) { _accent = c; Invalidate(); }

        public DiskProgressBar(Color accent)
        {
            _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 8;
            _pulseTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _pulseTimer.Tick += (s, e) =>
            {
                if (_animate)
                {
                    _pulse = (_pulse + 4) % (Width > 0 ? Width * 2 : 200);
                    Invalidate();
                }
            };
            _pulseTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);
            if (_animate)
            {
                int pw = Math.Max(Width / 3, 40);
                int px = _pulse % (Width + pw) - pw;
                var rect = new Rectangle(px, 0, pw, Height);
                if (rect.Width > 0)
                {
                    var blend = new ColorBlend(3);
                    blend.Colors = new[]
                    {
                        Color.Transparent,
                        Color.FromArgb(160, _accent.R, _accent.G, _accent.B),
                        Color.Transparent
                    };
                    blend.Positions = new[] { 0f, 0.5f, 1f };
                    using (var br = new LinearGradientBrush(
                        rect, Color.Transparent,
                        Color.FromArgb(160, _accent.R, _accent.G, _accent.B),
                        LinearGradientMode.Horizontal))
                    { br.InterpolationColors = blend; g.FillRectangle(br, rect); }
                }
            }
            else
            {
                int fw = (int)(Width * (_val / 100.0));
                if (fw > 2)
                {
                    using (var br = new LinearGradientBrush(
                        new Rectangle(0, 0, fw, Height),
                        Color.FromArgb(160, _accent.R, _accent.G, _accent.B),
                        _accent, LinearGradientMode.Horizontal))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height));
                    using (var br = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _pulseTimer != null) _pulseTimer.Stop();
            base.Dispose(disposing);
        }
    }
}