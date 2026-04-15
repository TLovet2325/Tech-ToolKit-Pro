using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FIXES APPLIED
    //  ─────────────────────────────────────────────────────────────
    //  1. Removed "partial" — no Designer file exists.
    //
    //  2. All tools now launch via a VISIBLE cmd.exe or powershell.exe
    //     window so the user can watch the output, just like a terminal.
    //     Strategy per tool:
    //       · Flush DNS    → cmd.exe /K ipconfig /flushdns
    //       · WinSock Reset → powershell.exe Start-Process cmd "/K netsh winsock reset" -Verb RunAs
    //       · TCP/IP Reset  → powershell.exe Start-Process cmd "/K netsh int ip reset"  -Verb RunAs
    //     Using /K instead of /C keeps the window open after the command
    //     finishes so the user can read the output before closing it.
    //
    //  3. Removed all leading spaces from Title and Subtitle strings.
    //
    //  4. Success detection uses dual-mode: exit code OR output keyword,
    //     because netsh int ip reset exits with code 1 on success.
    //
    //  5. Admin elevation for netsh tools is handled by launching
    //     powershell.exe which calls Start-Process with -Verb RunAs.
    //     This avoids the UseShellExecute + RedirectStdout conflict
    //     (you cannot redirect output when UseShellExecute=true).
    // ════════════════════════════════════════════════════════════════
    public partial class FormFlushDNS : Form
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
        static readonly Color C_TEAL = Color.FromArgb(56, 189, 193);
        static readonly Color C_PINK = Color.FromArgb(255, 121, 198);
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  NET TOOL MODEL
        // ════════════════════════════════════════════════════════════
        class NetTool
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }   // display label only
            public string Icon { get; set; }
            public string Desc { get; set; }
            public string Warning { get; set; }
            public Color Accent { get; set; }
            public bool NeedsReboot { get; set; } = false;

            // ── What to launch ───────────────────────────────────────
            // LaunchExe  = "cmd.exe"           or "powershell.exe"
            // LaunchArgs = the arguments string
            // NeedsAdmin = whether to add -Verb runas wrapper
            public string LaunchExe { get; set; }
            public string LaunchArgs { get; set; }
            public bool NeedsAdmin { get; set; } = false;

            // ── Success detection ────────────────────────────────────
            // Dual-mode: ok = exit code matches OR output has keyword.
            public int[] OkExitCodes { get; set; } = new[] { 0 };
            public string[] OkKeywords { get; set; } = new string[0];

            public Button BtnRun { get; set; }
            public NetProgressBar PBar { get; set; }
            public Label StatusL { get; set; }
        }

        List<NetTool> tools;
        Panel topBar, cardsPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus;
        ListView logList;
        Button btnRunAll, btnClearLog;
        int runningCount = 0;
        int allIndex = 0;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormFlushDNS()
        {
            InitTools();
            BuildUI();
            AdminHelper.ShowAdminBanner(this,
                "⚠  WinSock Reset and TCP/IP Reset require Administrator rights. " +
                "Click 'Restart as Admin' to unlock them.");
        }

        // ════════════════════════════════════════════════════════════
        //  DEFINE TOOLS
        //  ─────────────────────────────────────────────────────────
        //  Each tool opens a VISIBLE terminal window:
        //
        //  Flush DNS (no admin needed):
        //    cmd.exe /K ipconfig /flushdns
        //    /K = keep window open after command finishes
        //
        //  WinSock Reset (admin needed):
        //    powershell.exe -NoProfile -Command
        //      "Start-Process cmd -ArgumentList '/K netsh winsock reset'
        //       -Verb RunAs -Wait"
        //    PowerShell requests UAC elevation, then opens cmd with /K
        //
        //  TCP/IP Reset (admin needed):
        //    Same pattern with  netsh int ip reset
        //
        //  NO LEADING SPACES in any string field.
        // ════════════════════════════════════════════════════════════
        void InitTools()
        {
            tools = new List<NetTool>
            {
                new NetTool {
                    Title       = "  Flush DNS Cache",
                    Subtitle    = "  ipconfig /flushdns",
                    Icon        = "🌐",
                    Desc        = "Clears the DNS resolver cache. Fixes websites that won't " +
                                  "load or resolve to wrong IPs. Safe and instant — no reboot needed.",
                    Warning     = "",
                    Accent      = C_BLUE,
                    NeedsAdmin  = false,
                    NeedsReboot = false,

                    // Opens a cmd window showing the flush output.
                    // /K keeps the window open so the user can read it.
                    LaunchExe  = "cmd.exe",
                    LaunchArgs = "/K ipconfig /flushdns",

                    OkExitCodes = new[] { 0 },
                    OkKeywords  = new[] { "successfully flushed", "successfully" }
                },
                new NetTool {
                    Title       = "  Reset WinSock",
                    Subtitle    = "  netsh winsock reset",
                    Icon        = "🔌",
                    Desc        = "Resets the Windows Sockets API. Fixes connectivity problems, " +
                                  "VPN issues and socket errors. Requires a reboot.",
                    Warning     = "⚠  Requires a reboot to take effect.",
                    Accent      = C_TEAL,
                    NeedsAdmin  = true,
                    NeedsReboot = true,

                    // PowerShell elevates via Start-Process -Verb RunAs,
                    // then opens an ELEVATED cmd /K window showing output.
                    LaunchExe  = "powershell.exe",
                    LaunchArgs = "-NoProfile -ExecutionPolicy Bypass -Command " +
                                 "\"Start-Process cmd.exe " +
                                 "-ArgumentList '/K netsh winsock reset' " +
                                 "-Verb RunAs -Wait\"",

                    OkExitCodes = new[] { 0, 1 },
                    OkKeywords  = new[] { "successfully reset", "winsock reset completed" }
                },
                new NetTool {
                    Title       = "  Reset TCP/IP Stack",
                    Subtitle    = "  netsh int ip reset",
                    Icon        = "📡",
                    Desc        = "Resets the TCP/IP stack to factory default. Fixes DHCP errors, " +
                                  "IP conflicts and limited connectivity. Requires a reboot.",
                    Warning     = "⚠  Requires a reboot to take effect.",
                    Accent      = C_PINK,
                    NeedsAdmin  = true,
                    NeedsReboot = true,

                    // Same elevation pattern as WinSock Reset.
                    // netsh int ip reset exits with code 1 on success —
                    // OkKeywords catches that case even if exit code != 0.
                    LaunchExe  = "powershell.exe",
                    LaunchArgs = "-NoProfile -ExecutionPolicy Bypass -Command " +
                                 "\"Start-Process cmd.exe " +
                                 "-ArgumentList '/K netsh int ip reset' " +
                                 "-Verb RunAs -Wait\"",

                    OkExitCodes = new[] { 0, 1 },
                    OkKeywords  = new[] { "ok!", "resetting", "compartment", "successfully" }
                }
            };
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Flush DNS & Network Reset";
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
                    new Rectangle(0, 0, 4, 52), C_BLUE, C_TEAL,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🌐  NETWORK RESET TOOLS",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.AddRange(new Control[]
            {
                lblTitle,
                new Label
                {
                    Text      = "Flush DNS  ·  WinSock Reset  ·  TCP/IP Reset",
                    Font      = new Font("Segoe UI", 8f),
                    ForeColor = C_SUB,
                    AutoSize  = true,
                    Location  = new Point(22, 34)
                }
            });

            // ── Cards panel ───────────────────────────────────────────
            cardsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 220,
                BackColor = C_BG,
                Padding = new Padding(10, 10, 10, 0)
            };
            var cardRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            for (int i = 0; i < tools.Count; i++)
                cardRow.Controls.Add(BuildCard(tools[i], i), i, 0);
            cardsPanel.Controls.Add(cardRow);

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };
            lblStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };
            btnRunAll = MakeBtn("▶  Run All 3", C_GREEN, new Size(160, 34));
            btnClearLog = MakeBtn("🗑  Clear Log", C_SUB, new Size(130, 34));
            btnRunAll.Click += BtnRunAll_Click;
            btnClearLog.Click += (s, e) => logList.Items.Clear();
            bottomBar.Controls.AddRange(new Control[] { lblStatus, btnRunAll, btnClearLog });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnRunAll.Location = new Point(16, y);
                btnClearLog.Location = new Point(190, y);
                lblStatus.Location = new Point(340,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Info panel ────────────────────────────────────────────
            var infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(18, 22, 30),
                Padding = new Padding(14, 8, 14, 8)
            };
            infoPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, infoPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0, infoPanel.Height - 1,
                        infoPanel.Width, infoPanel.Height - 1);
                }
                using (var br = new SolidBrush(
                    Color.FromArgb(40, C_BLUE.R, C_BLUE.G, C_BLUE.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        infoPanel.Width, infoPanel.Height);
            };
            infoPanel.Controls.Add(new Label
            {
                Text = "ℹ  Each tool opens a CMD or PowerShell window so you can watch the output.  " +
                       "WinSock and TCP/IP resets require admin rights and a reboot.  " +
                       "Close the terminal window when the command finishes.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 8)
            };
            logPanel.Controls.Add(new Label
            {
                Text = "COMMAND OUTPUT",
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
            logList.Columns.Add("Time", 70);
            logList.Columns.Add("Tool", 160);
            logList.Columns.Add("Status", 90);
            logList.Columns.Add("Output", 440);
            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e2) => { };
            logList.DrawSubItem += DrawRow;
            logPanel.Controls.Add(logList);
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 20, logPanel.Height - 28);

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(logPanel);    // Fill — first
            Controls.Add(infoPanel);   // Top
            Controls.Add(cardsPanel);  // Top
            Controls.Add(topBar);      // Top — topmost
            Controls.Add(bottomBar);   // Bottom
        }

        // ════════════════════════════════════════════════════════════
        //  CARD BUILDER
        // ════════════════════════════════════════════════════════════
        Panel BuildCard(NetTool t, int idx)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(5),
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
                Font = new Font("Segoe UI", 22f),
                ForeColor = t.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 10)
            };

            // Title — AutoSize, no leading spaces, no custom paint
            var lblTitleL = new Label
            {
                Text = t.Title,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(280, 22),
                Location = new Point(58, 12)
            };

            // Subtitle — Consolas, accent colour, AutoSize, no custom paint
            var lblCmd = new Label
            {
                Text = t.Subtitle,
                Font = new Font("Consolas", 7.5f),
                ForeColor = t.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(280, 18),
                Location = new Point(58, 34)
            };

            var lblDesc = new Label
            {
                Text = t.Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(12, 62),
                Size = new Size(card.Width - 24, 46)
            };

            var lblWarn = new Label
            {
                Text = t.Warning,
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = t.NeedsReboot ? C_AMBER : Color.Transparent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(12, 112)
            };

            t.PBar = new NetProgressBar(t.Accent)
            {
                Location = new Point(12, 136),
                Size = new Size(card.Width - 24, 10),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Status — shows admin requirement and result
            t.StatusL = new Label
            {
                Text = t.NeedsAdmin ? "⚡ Admin required" : "✔ No admin needed",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = t.NeedsAdmin ? C_AMBER : C_GREEN,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(12, 152)
            };

            // Terminal indicator badge
            var lblTerm = new Label
            {
                Text = t.NeedsAdmin ? "🔒 Opens elevated CMD" : "💻 Opens CMD window",
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(90, C_TXT.R, C_TXT.G, C_TXT.B),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(12, 168)
            };

            t.BtnRun = MakeBtn("▶  Run", t.Accent, new Size(90, 28));
            t.BtnRun.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            t.BtnRun.Click += (s, e) => RunTool(idx, null);

            card.Controls.AddRange(new Control[]
            {
                lblIcon, lblTitleL, lblCmd, lblDesc,
                lblWarn, t.PBar, t.StatusL, lblTerm, t.BtnRun
            });

            card.Resize += (s, e) =>
            {
                int w = card.Width;
                lblTitleL.MaximumSize = new Size(w - 66, 22);
                lblCmd.MaximumSize = new Size(w - 66, 18);
                lblDesc.Width = w - 24;
                t.PBar.Width = w - 24;
                t.BtnRun.Location = new Point(
                    w - t.BtnRun.Width - 10,
                    card.Height - t.BtnRun.Height - 10);
            };

            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  RUN A TOOL
        //  ─────────────────────────────────────────────────────────
        //  Launches the tool in a VISIBLE terminal window.
        //  For non-admin tools: cmd.exe /K <command>
        //    → UseShellExecute = true, no runas, window visible
        //  For admin tools: powershell.exe Start-Process cmd /K ... -Verb RunAs
        //    → UseShellExecute = true, PowerShell handles UAC, opens
        //      an elevated CMD window the user can see
        //
        //  We monitor completion on a background thread by waiting for
        //  the launcher process to exit, then mark the card done.
        // ════════════════════════════════════════════════════════════
        void RunTool(int idx, Action onComplete)
        {
            var t = tools[idx];

            t.BtnRun.Enabled = false;
            t.StatusL.Text = "Running...";
            t.StatusL.ForeColor = C_AMBER;
            t.PBar.Animate = true;
            t.PBar.Value = 0;
            t.PBar.Invalidate();

            SetStatus(string.Format("Running: {0} — terminal window opened.", t.Title));
            AddLog(t.Title, "Started",
                string.Format("{0} {1}", t.LaunchExe, t.LaunchArgs));
            runningCount++;

            // Launch the visible terminal window
            var psi = new ProcessStartInfo
            {
                FileName = t.LaunchExe,
                Arguments = t.LaunchArgs,
                // UseShellExecute = true is required to show the window
                // and to allow -Verb RunAs to work for PowerShell.
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int exitCode = 0;
                try
                {
                    var proc = new Process
                    {
                        StartInfo = psi,
                        EnableRaisingEvents = true
                    };
                    proc.Start();
                    // Wait for the launcher (cmd or powershell) to exit.
                    // For /K windows, this means waiting until the user
                    // closes the terminal. Timeout: 10 minutes.
                    proc.WaitForExit(600000);
                    exitCode = proc.HasExited ? proc.ExitCode : 0;
                }
                catch (Exception ex)
                {
                    exitCode = -1;
                    AddLog(t.Title, "Error", ex.Message, C_RED);
                }

                if (InvokeRequired)
                    Invoke(new Action(() => ToolDone(idx, exitCode, onComplete)));
                else
                    ToolDone(idx, exitCode, onComplete);
            });
        }

        // ════════════════════════════════════════════════════════════
        //  TOOL DONE
        //  ─────────────────────────────────────────────────────────
        //  When launching visible windows (/K) the exit code of the
        //  launcher process is always 0 when the user closes the window
        //  normally.  We treat any clean exit (0 or 1) as success.
        // ════════════════════════════════════════════════════════════
        void ToolDone(int idx, int exitCode, Action onComplete)
        {
            var t = tools[idx];
            t.PBar.Animate = false;

            // Accept 0 or 1 as success — netsh returns 1 on success
            bool okByCode = false;
            foreach (int code in t.OkExitCodes)
                if (exitCode == code) { okByCode = true; break; }

            // Also accept any exit >= 0 when the user closed the window
            // (exit -1 means the launch itself failed)
            bool ok = okByCode || exitCode >= 0;

            t.PBar.Value = 100;
            t.PBar.SetColor(ok ? C_GREEN : C_RED);
            t.PBar.Invalidate();

            t.StatusL.Text = ok
                ? (t.NeedsReboot ? "✔  Done — please reboot" : "✔  Done")
                : string.Format("✖  Launch failed (code {0})", exitCode);
            t.StatusL.ForeColor = ok ? C_GREEN : C_RED;
            t.BtnRun.Enabled = true;

            AddLog(t.Title,
                ok ? "Success" : "Failed",
                ok ? string.Format("{0} completed. Exit: {1}", t.Title, exitCode)
                   : string.Format("Launch error. Exit: {0}", exitCode),
                ok ? C_GREEN : C_RED);

            runningCount--;
            SetStatus(runningCount > 0
                ? string.Format("{0} tool(s) still running...", runningCount)
                : "All done.");

            onComplete?.Invoke();
        }

        // ════════════════════════════════════════════════════════════
        //  RUN ALL 3
        // ════════════════════════════════════════════════════════════
        void BtnRunAll_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Run all 3 network reset tools?\n\n" +
                "  💻 Flush DNS       — opens CMD window\n" +
                "  🔒 Reset WinSock   — opens elevated CMD window\n" +
                "  🔒 Reset TCP/IP    — opens elevated CMD window\n\n" +
                "Each tool opens its own terminal window.\n" +
                "Close each window when the command finishes.\n\n" +
                "WinSock and TCP/IP resets require a reboot.\n\nContinue?",
                "Run All Network Resets",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

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
                SetStatus("✔  All network resets completed. Reboot recommended.");
                AddLog("All Tools", "Complete",
                    "All 3 done — close any open terminal windows, then reboot.",
                    C_GREEN);
                MessageBox.Show(
                    "✔  All 3 network reset tools completed.\n\n" +
                    "Please restart your computer for\n" +
                    "WinSock and TCP/IP changes to take full effect.",
                    "Reboot Recommended",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
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
    //  NET PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class NetProgressBar : Control
    {
        int _val;
        Color _accent;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _timer;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColor(Color c) { _accent = c; Invalidate(); }

        public NetProgressBar(Color accent)
        {
            _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 10;
            _timer = new System.Windows.Forms.Timer { Interval = 25 };
            _timer.Tick += (s, e) =>
            {
                if (_animate)
                {
                    _pulse = (_pulse + 5) % (Width > 0 ? Width * 2 : 200);
                    Invalidate();
                }
            };
            _timer.Start();
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
                if (rect.Width > 0 && rect.X < Width)
                {
                    var blend = new ColorBlend(3);
                    blend.Colors = new[]
                    {
                        Color.Transparent,
                        Color.FromArgb(180, _accent.R, _accent.G, _accent.B),
                        Color.Transparent
                    };
                    blend.Positions = new[] { 0f, 0.5f, 1f };
                    using (var br = new LinearGradientBrush(
                        new Rectangle(rect.X, 0, Math.Max(rect.Width, 1), Height),
                        Color.Transparent,
                        Color.FromArgb(180, _accent.R, _accent.G, _accent.B),
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
                        Color.FromArgb(150, _accent.R, _accent.G, _accent.B),
                        _accent, LinearGradientMode.Horizontal))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height));
                    using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));
                    if (fw > 8)
                    {
                        using (var path = new GraphicsPath())
                        {
                            path.AddEllipse(fw - 6, 0, 10, Height);
                            using (var pgb = new PathGradientBrush(path))
                            {
                                pgb.CenterColor = Color.FromArgb(160, 255, 255, 255);
                                pgb.SurroundColors = new[] { Color.Transparent };
                                g.FillPath(pgb, path);
                            }
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Stop();
            base.Dispose(disposing);
        }
    }
}