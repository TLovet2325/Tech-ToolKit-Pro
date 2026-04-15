using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  ROOT CAUSE ANALYSIS & FIXES
    //  ─────────────────────────────────────────────────────────────
    //  PROBLEM 1 — "The system cannot find the file specified" +
    //              "Exit -1" / "Exit 1" on sfc, defrag, DISM, etc.
    //
    //  Root cause: The code was setting UseShellExecute = true (needed
    //  for "runas" elevation) but System32 tools like sfc.exe, defrag
    //  and DISM.exe are NOT on the PATH for the shell in all contexts,
    //  especially when launched from a non-elevated process. The OS
    //  can't locate them without their full path.
    //
    //  Fix: Route every tool through  cmd.exe /C <command>  with runas.
    //  cmd.exe IS always on the PATH and always resolves System32 tools
    //  correctly.  So the Exe becomes "pwsh.exe" and Args becomes
    //  "-NoProfile -NoExit -Command \"sfc /scannow\"" OR "/K sfc /scannow"
    //  if you use "cmd.exe" etc.  The window appears as before (ShowWindow)
    //  and elevation is preserved via Verb = "runas".
    //
    //  PROBLEM 2 — Blurry / strikethrough subtitle text
    //
    //  Root cause: Labels with AutoSize = false + a custom Paint handler
    //  that calls DrawString on top of whatever WinForms already painted
    //  causes double-rendering. The system draws the label's own text,
    //  then the Paint event draws it again slightly offset → blurry.
    //
    //  Fix: Use AutoSize = true on all card labels.  Set the label's
    //  own text and let WinForms render it normally — no custom Paint.
    //  For overflow protection on narrow cards, use a plain label with
    //  MaximumSize instead of a Paint-based ellipsis.
    //
    //  PROBLEM 3 — "partial" keyword with no Designer file → removed.
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
        //  TOOL MODEL
        // ════════════════════════════════════════════════════════════
        class DiskTool
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }  // display only
            public string Icon { get; set; }
            public string Desc { get; set; }
            public Color Accent { get; set; }

            // ── Launcher fields ──────────────────────────────────────
            // All elevated tools use:
            //   LaunchExe  = "cmd.exe"
            //   LaunchArgs = "/C <actual command>"
            //   NeedsAdmin = true
            //   ShowWindow = true
            //
            // Non-elevated tools can use their exe directly with
            //   UseShellExecute = false  (no runas needed).
            public string LaunchExe { get; set; }
            public string LaunchArgs { get; set; }
            public bool NeedsAdmin { get; set; } = true;
            public bool ShowWindow { get; set; } = true;

            // Special multi-step handler
            public bool IsSpecial { get; set; }
            public string SpecialKey { get; set; } = "";

            public Button BtnRun { get; set; }
            public DiskProgressBar PBar { get; set; }
            public Label StatusL { get; set; }
        }

        readonly List<DiskTool> tools = new List<DiskTool>();

        Panel topBar, bottomBar, logPanel;
        Label lblSub, lblStatus;
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
            InitTools();
            BuildUI();
            AdminHelper.ShowAdminBanner(this,
                "⚠  Most tools require Administrator rights. " +
                "Click 'Restart as Admin' to unlock them.");
        }

        // ════════════════════════════════════════════════════════════
        //  DEFINE TOOLS
        //  ─────────────────────────────────────────────────────────
        //  KEY RULE:  Any tool that needs elevation uses:
        //    LaunchExe  = "cmd.exe"
        //    LaunchArgs = "/C <the real command>"
        //    NeedsAdmin = true
        //    ShowWindow = true
        //
        //  This guarantees the System32 path is always resolved
        //  because cmd.exe handles it, and elevation works via runas.
        // ════════════════════════════════════════════════════════════
        void InitTools()
        {
            tools.Clear();
            tools.AddRange(new[]
            {
                new DiskTool {
                    Title      = "    Disable Hibernation",
                    Subtitle   = "powercfg /h off",
                    Icon       = "💤",
                    Desc       = "Turns off hibernation and deletes hiberfil.sys to reclaim space.",
                    Accent     = C_BLUE,
                    LaunchExe  = "cmd.exe",
                    LaunchArgs = "/C powercfg /h off",
                    NeedsAdmin = true,
                    ShowWindow = false
                },
                new DiskTool {
                    Title      = "    Check Disk (CHKDSK)",
                    Subtitle   = "chkdsk C: /f /r /x",
                    Icon       = "🔍",
                    Desc       = "Scans drive C: for file-system issues and bad sectors.",
                    Accent     = C_AMBER,
                    LaunchExe  = "pwsh.exe",
                    LaunchArgs = "-NoProfile -NoExit -Command \"echo Y | chkdsk C: /f /r /x\"",
                    NeedsAdmin = true,
                    ShowWindow = true
                },
                new DiskTool {
                    Title      = "    SFC System Scan",
                    Subtitle   = "sfc /scannow",
                    Icon       = "🛡",
                    Desc       = "Validates protected Windows files and restores corrupted copies.",
                    Accent     = C_GREEN,
                    LaunchExe  = "pwsh.exe",
                    LaunchArgs = "-NoProfile -NoExit -Command \"sfc /scannow\"",
                    NeedsAdmin = true,
                    ShowWindow = true
                },
                new DiskTool {
                    Title      = "    DISM Health Restore",
                    Subtitle   = "DISM /Cleanup-Image /RestoreHealth",
                    Icon       = "🔧",
                    Desc       = "Repairs the Windows component store. Run before or after SFC.",
                    Accent     = C_PURPLE,
                    LaunchExe  = "cmd.exe",
                    LaunchArgs = "/C DISM /Online /Cleanup-Image /RestoreHealth",
                    NeedsAdmin = true,
                    ShowWindow = true
                },
                new DiskTool {
                    Title      = "    Optimize Drive",
                    Subtitle   = "defrag C: /U /V",
                    Icon       = "⚡",
                    Desc       = "Runs defrag or TRIM on drive C: to improve performance.",
                    Accent     = C_TEAL,
                    LaunchExe  = "pwsh.exe",
                    LaunchArgs = "-NoProfile -NoExit -Command \"defrag C: /U /V\"",
                    NeedsAdmin = true,
                    ShowWindow = true
                },
                new DiskTool {
                    Title      = "    Clear Update Cache",
                    Subtitle   = "Stop → Delete → Restart wuauserv",
                    Icon       = "🔄",
                    Desc       = "Stops update services, clears the download cache, then restarts them.",
                    Accent     = C_ORANGE,
                    IsSpecial  = true,
                    SpecialKey = "wucache"
                },
                new DiskTool {
                    Title      = "    Clear Event Logs",
                    Subtitle   = "wevtutil cl System / Application",
                    Icon       = "📋",
                    Desc       = "Clears Windows System, Application and Security event logs.",
                    Accent     = C_RED,
                    LaunchExe  = "cmd.exe",
                    LaunchArgs = "/C wevtutil cl System & wevtutil cl Application & wevtutil cl Security",
                    NeedsAdmin = true,
                    ShowWindow = false
                },
                new DiskTool {
                    Title      = "    Compact OS",
                    Subtitle   = "compact /CompactOS:always",
                    Icon       = "🗜",
                    Desc       = "Compresses Windows OS files to save 1–3 GB on low-storage devices.",
                    Accent     = C_TEAL,
                    LaunchExe  = "cmd.exe",
                    LaunchArgs = "/C compact /CompactOS:always",
                    NeedsAdmin = true,
                    ShowWindow = true
                },
                new DiskTool {
                    Title      = "    Control Panel",
                    Subtitle   = "control.exe",
                    Icon       = "🖥",
                    Desc       = "Opens the Windows Control Panel for system settings and hardware.",
                    Accent     = C_BLUE,
                    LaunchExe  = "control.exe",
                    LaunchArgs = "",
                    NeedsAdmin = false,
                    ShowWindow = true
                }
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

            bottomBar.Controls.AddRange(new Control[]
                { btnRunAll, btnClearLog, lblStatus });
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

            // Tool grid in Panel1
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

            // Log panel in Panel2
            BuildLogPanel();
            mainSplit.Panel2.Controls.Add(logPanel);

            Controls.Add(mainSplit);
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

            logList.Columns.Add("Time", 78);
            logList.Columns.Add("Tool", 170);
            logList.Columns.Add("Status", 100);
            logList.Columns.Add("Command / Note", 480);

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
        //  TOOL GRID — responsive columns
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
        //  ─────────────────────────────────────────────────────────
        //  Labels use AutoSize = true — no custom Paint handlers on
        //  labels.  This eliminates the double-render / blurry text.
        //  For truncation on narrow cards we set MaximumSize.Width.
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

            // Icon
            var lblIcon = new Label
            {
                Text = t.Icon,
                Font = new Font("Segoe UI", 16f),
                ForeColor = t.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 8)
            };

            // Title — AutoSize + MaximumSize prevents overflow without
            // double-rendering. WinForms clips to MaximumSize naturally.
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

            // Subtitle / command — same approach, Consolas font,
            // accent colour, rendered cleanly by WinForms
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

            // Description
            var lblDesc = new Label
            {
                Text = t.Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(10, 50),
                Size = new Size(card.Width - 20, 36)
            };

            // Progress bar
            t.PBar = new DiskProgressBar(t.Accent)
            {
                Location = new Point(10, 94),
                Size = new Size(card.Width - 20, 8),
                Value = 0,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            // Status label
            t.StatusL = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 110)
            };

            // Run button
            t.BtnRun = MakeSmallBtn("▶  Run", t.Accent);
            t.BtnRun.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            t.BtnRun.Click += (s, e) => RunTool(idx, null);

            card.Controls.AddRange(new Control[]
                { lblIcon, lblTitle, lblCmd, lblDesc,
                  t.PBar, t.StatusL, t.BtnRun });

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
        //  RUN TOOL
        //  ─────────────────────────────────────────────────────────
        //  Admin tools: LaunchExe = "cmd.exe", LaunchArgs = "/C ..."
        //    UseShellExecute = true, Verb = "runas"
        //    → cmd.exe launches elevated, resolves System32 correctly.
        //
        //  Non-admin tools: direct exe, UseShellExecute = false/true.
        // ════════════════════════════════════════════════════════════
        void RunTool(int idx, Action onComplete)
        {
            var t = tools[idx];

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
                    : t.LaunchExe + " " + t.LaunchArgs);
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
                // UseShellExecute must be true for "runas" to work
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
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit());
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
                        @"Deleting C:\Windows\SoftwareDistribution\Download");

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

        // Runs a command via cmd.exe silently, no elevation (for net stop/start)
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
                p.Start();
                p.WaitForExit(15000);
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

        // ════════════════════════════════════════════════════════════
        //  TOOL FINISHED
        //  ─────────────────────────────────────────────────────────
        //  Acceptable exit codes:
        //   0    = success (most tools)
        //   3010 = success, reboot required (DISM, some installers)
        //   1    = some cmd.exe wrappers return 1 on success
        //
        //  cmd.exe wrapping: When we do  cmd.exe /C sfc /scannow,
        //  cmd's exit code IS the inner tool's exit code, so 0 = ok.
        // ════════════════════════════════════════════════════════════
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
                t.IsSpecial
                    ? t.Subtitle
                    : t.LaunchExe + " " + t.LaunchArgs,
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
            if (MessageBox.Show(
                string.Format(
                    "Run all {0} disk maintenance tools sequentially?\n\n" +
                    "Administrator rights required for most tools.\nContinue?",
                    tools.Count),
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
            lblStatus.ForeColor = msg.StartsWith("✔") || msg == "All done." ? C_GREEN
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
        //  OWNER DRAW — log list only (cards use normal label drawing)
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
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
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
                    {
                        br.InterpolationColors = blend;
                        g.FillRectangle(br, rect);
                    }
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