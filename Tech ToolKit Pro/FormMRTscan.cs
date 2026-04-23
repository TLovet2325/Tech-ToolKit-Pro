using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormMRTscan : Form
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
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, infoPanel, scanPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus, lblMrtPath, lblMrtVersion;
        RadioButton rbGuiQuick, rbGuiFull, rbSilentQuick, rbSilentFull;
        ScanProgressBar scanBar;
        Label lblPct, lblElapsed, lblScanStatus;
        ListView logList;
        Button btnStartScan, btnViewLog, btnCancel;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime scanStart;
        bool scanning = false;
        Process mrtProc;
        string mrtExePath = null;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormMRTscan()
        {
            BuildUI();
            DetectMRT();
            AdminHelper.ShowAdminBanner(this,
                "⚠  MRT scans require Administrator rights. " +
                "Click 'Restart as Admin' to enable scanning.");
        }

        // ════════════════════════════════════════════════════════════
        //  FIND MRT
        // ════════════════════════════════════════════════════════════
        string FindMrtPath()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

            string[] candidates =
            {
                Path.Combine(sys32,   "mrt.exe"),
                Path.Combine(sys32,   "MRT.exe"),
                Path.Combine(windir,  "System32", "mrt.exe"),
                Path.Combine(windir,  "SysWOW64",  "mrt.exe"),
                Path.Combine(windir,  "SysNative", "mrt.exe")
            };
            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            // Defender Platform subfolders
            string defPlatform = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");
            if (Directory.Exists(defPlatform))
            {
                try
                {
                    foreach (string sub in Directory.GetDirectories(defPlatform))
                    {
                        if (File.Exists(Path.Combine(sub, "mrt.exe")))
                            return Path.Combine(sub, "mrt.exe");
                        if (File.Exists(Path.Combine(sub, "MRT.exe")))
                            return Path.Combine(sub, "MRT.exe");
                    }
                }
                catch { }
            }

            // WU cache
            string wuCache = Path.Combine(windir, "SoftwareDistribution", "Download");
            if (Directory.Exists(wuCache))
            {
                try
                {
                    foreach (string f in Directory.GetFiles(
                        wuCache, "mrt.exe", SearchOption.AllDirectories))
                        return f;
                }
                catch { }
            }

            // WHERE fallback
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C where mrt.exe 2>nul",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                string res = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                if (!string.IsNullOrEmpty(res))
                {
                    string first = res.Split('\n')[0].Trim();
                    if (File.Exists(first)) return first;
                }
            }
            catch { }

            return null;
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "MRT Scan";
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
                    new Rectangle(0, 0, 4, 52), C_RED, C_AMBER,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🛡  MALICIOUS SOFTWARE REMOVAL TOOL",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(new Label
            {
                Text = "Microsoft MRT  ·  mrt.exe  ·  Detects and removes prevalent malware",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            });

            // ── Info panel (MRT path + version) ──────────────────────
            infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(16, 22, 32),
                Padding = new Padding(16, 6, 16, 6)
            };
            infoPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, infoPanel.Height - 1,
                        infoPanel.Width, infoPanel.Height - 1);
                using (var br = new SolidBrush(
                    Color.FromArgb(18, C_BLUE.R, C_BLUE.G, C_BLUE.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        infoPanel.Width, infoPanel.Height);
            };
            lblMrtPath = new Label
            {
                Text = "Locating MRT...",
                Font = new Font("Consolas", 8f),
                ForeColor = C_BLUE,
                AutoSize = true,
                Location = new Point(16, 10)
            };
            lblMrtVersion = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 28)
            };
            infoPanel.Controls.Add(lblMrtPath);
            infoPanel.Controls.Add(lblMrtVersion);
            infoPanel.Controls.Add(new Label
            {
                Text = "ℹ  MRT log written to %windir%\\debug\\mrt.log after each scan",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 48)
            });

            // ════════════════════════════════════════════════════════
            //  SCAN TYPE PANEL
            //  ─────────────────────────────────────────────────────
            //  Layout:  two columns side by side
            //
            //   ┌─────────────────────┬────────────────────────┐
            //   │  📺 GUI MODE        │  🔇 SILENT MODE        │
            //   │  (Recommended)      │  (Background)          │
            //   │                     │                         │
            //   │  ○ Quick Scan (GUI) │  ○ Quick Scan (Silent) │
            //   │  ○ Full Scan  (GUI) │  ○ Full Scan  (Silent) │
            //   └─────────────────────┴────────────────────────┘
            //
            //  Implemented with a TableLayoutPanel (1 row, 2 cols)
            //  inside scanPanel, so both columns resize together.
            // ════════════════════════════════════════════════════════
            scanPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 258,
                BackColor = C_BG,
                Padding = new Padding(12, 10, 12, 8)
            };
            scanPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, scanPanel.Height - 1,
                        scanPanel.Width, scanPanel.Height - 1);
            };

            scanPanel.Controls.Add(new Label
            {
                Text = "SELECT SCAN TYPE",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(12, 10)
            });

            // ── Two-column grid ───────────────────────────────────────
            var grid = new TableLayoutPanel
            {
                Location = new Point(12, 30),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left |
                                  AnchorStyles.Right
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 168f));
            grid.Size = new Size(scanPanel.Width - 24, 168);

            scanPanel.Resize += (s, e) =>
                grid.Size = new Size(scanPanel.Width - 24, 168);

            // ── Left column — GUI MODE ────────────────────────────────
            var leftCard = BuildModeCard(
                "📺  GUI MODE",
                "Opens the official Microsoft MRT window.\nYou can watch real progress.",
                C_BLUE,
                new[]
                {
                    new ModeOption { Label = "⚡  Quick Scan",
                        Desc   = "Opens MRT wizard  ·  Quick Scan selected\nFastest — 5 to 15 minutes.",
                        Accent = C_GREEN,
                        IsDefault = true },
                    new ModeOption { Label = "🔍  Full Scan",
                        Desc   = "Opens MRT wizard  ·  Full Scan selected\nScans every file  ·  Flag: mrt.exe /F",
                        Accent = C_AMBER,
                        IsDefault = false }
                },
                out rbGuiQuick, out rbGuiFull);

            // ── Right column — SILENT MODE ────────────────────────────
            var rightCard = BuildModeCard(
                "🔇  SILENT MODE",
                "Runs in background with no window.\nReads mrt.log when complete.",
                C_SUB,
                new[]
                {
                    new ModeOption { Label = "⚡  Quick Scan",
                        Desc   = "mrt.exe /Q  ·  Silent background scan\nProgress tracked via elapsed timer.",
                        Accent = C_GREEN,
                        IsDefault = false },
                    new ModeOption { Label = "🔍  Full Scan",
                        Desc   = "mrt.exe /Q /F  ·  Full scan, no window\n⚠  May take several hours.",
                        Accent = C_RED,
                        IsDefault = false }
                },
                out rbSilentQuick, out rbSilentFull);

            grid.Controls.Add(leftCard, 0, 0);
            grid.Controls.Add(rightCard, 1, 0);
            scanPanel.Controls.Add(grid);

            // ── Progress section ──────────────────────────────────────
            scanPanel.Controls.Add(new Label
            {
                Text = "SCAN  PROGRESS",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(12, 202)
            });

            scanBar = new ScanProgressBar(C_RED, C_AMBER)
            {
                Location = new Point(12, 220),
                Size = new Size(100, 14),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lblPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblElapsed = new Label
            {
                Text = "Elapsed: 00:00:00",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(12, 236)
            };
            lblScanStatus = new Label
            {
                Text = "Ready to scan.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(200, 236)
            };
            scanPanel.Controls.AddRange(new Control[]
                { scanBar, lblPct, lblElapsed, lblScanStatus });
            scanPanel.Resize += (s, e) =>
            {
                scanBar.Size = new Size(scanPanel.Width - 70, 14);
                lblPct.Location = new Point(scanPanel.Width - 52, 226);
            };

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 8)
            };
            logPanel.Controls.Add(new Label
            {
                Text = "SCAN LOG",
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
            logList.Columns.Add("Time", 170);
            logList.Columns.Add("Event", 220);
            logList.Columns.Add("Detail", 732);
            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawRow;
            logPanel.Controls.Add(logList);
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 24, logPanel.Height - 28);

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
            btnStartScan = MakeBtn("🛡  Start MRT Scan", C_RED, new Size(175, 34));
            btnViewLog = MakeBtn("📄  View MRT Log", C_BLUE, new Size(155, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnCancel.Enabled = false;
            btnStartScan.Click += BtnStart_Click;
            btnViewLog.Click += (s, e) => OpenMrtLog();
            btnCancel.Click += BtnCancel_Click;
            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnStartScan, btnViewLog, btnCancel });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnStartScan.Location = new Point(16, y);
                btnViewLog.Location = new Point(203, y);
                btnCancel.Location = new Point(370, y);
                lblStatus.Location = new Point(490,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            Controls.Add(logPanel);
            Controls.Add(scanPanel);
            Controls.Add(infoPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - scanStart;
                lblElapsed.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
                if (scanning && (rbSilentQuick.Checked || rbSilentFull.Checked))
                    scanBar.Value = (int)(ts.TotalSeconds % 100);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD MODE CARD  — one column card (GUI or Silent)
        //  Returns the card Panel and exposes the two radio buttons.
        // ════════════════════════════════════════════════════════════
        class ModeOption
        {
            public string Label { get; set; }
            public string Desc { get; set; }
            public Color Accent { get; set; }
            public bool IsDefault { get; set; }
        }

        Panel BuildModeCard(string title, string subtitle, Color headerAccent,
            ModeOption[] options,
            out RadioButton rb0, out RadioButton rb1)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Padding = new Padding(10, 8, 10, 8)
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(
                    Color.FromArgb(55, headerAccent.R, headerAccent.G, headerAccent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(headerAccent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };

            // Column header
            var lblHeader = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = headerAccent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 10)
            };
            var lblSub = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Size = new Size(card.Width - 20, 28),
                Location = new Point(10, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Separator line
            var sep = new Panel
            {
                BackColor = C_BORDER,
                Location = new Point(10, 60),
                Size = new Size(card.Width - 20, 1),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Option 0 (e.g. Quick Scan)
            var rb_0 = new RadioButton
            {
                Text = options[0].Label,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = options[0].Accent,
                AutoSize = true,
                Checked = options[0].IsDefault,
                BackColor = Color.Transparent,
                Location = new Point(10, 70)
            };
            var desc_0 = new Label
            {
                Text = options[0].Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Size = new Size(card.Width - 30, 32),
                Location = new Point(28, 90),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Option 1 (e.g. Full Scan)
            var rb_1 = new RadioButton
            {
                Text = options[1].Label,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = options[1].Accent,
                AutoSize = true,
                Checked = options[1].IsDefault,
                BackColor = Color.Transparent,
                Location = new Point(10, 126)
            };
            var desc_1 = new Label
            {
                Text = options[1].Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                BackColor = Color.Transparent,
                Size = new Size(card.Width - 30, 32),
                Location = new Point(28, 146),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            card.Controls.AddRange(new Control[]
            {
                lblHeader, lblSub, sep,
                rb_0, desc_0, rb_1, desc_1
            });

            // Resize: keep sub-label and desc widths proportional
            card.Resize += (s, e) =>
            {
                int w = card.Width - 30;
                lblSub.Width = w + 10;
                sep.Width = w + 10;
                desc_0.Width = w;
                desc_1.Width = w;
            };

            rb0 = rb_0;
            rb1 = rb_1;
            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT MRT
        // ════════════════════════════════════════════════════════════
        void DetectMRT()
        {
            AddLog("Info", "Searching for mrt.exe...", C_BLUE);
            mrtExePath = FindMrtPath();

            if (mrtExePath != null)
            {
                lblMrtPath.Text = "  ✔  " + mrtExePath;
                lblMrtPath.ForeColor = C_GREEN;
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(mrtExePath);
                    lblMrtVersion.Text = string.Format(
                        "Version: {0}   |   Company: {1}",
                        vi.FileVersion ?? "–", vi.CompanyName ?? "–");
                }
                catch { lblMrtVersion.Text = "Version info unavailable"; }
                AddLog("Info", "MRT found: " + mrtExePath, C_GREEN);
                btnStartScan.Enabled = true;
            }
            else
            {
                lblMrtPath.Text = "  ✖  mrt.exe not found";
                lblMrtPath.ForeColor = C_RED;
                lblMrtVersion.Text = "Run Windows Update or download from microsoft.com";
                btnStartScan.Enabled = false;
                AddLog("Warning", "MRT not found. Run Windows Update.", C_AMBER);

                if (MessageBox.Show(
                        "mrt.exe was not found automatically.\n\n" +
                        "Would you like to browse for it manually?",
                        "MRT Not Found",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                    BrowseForMrt();
            }
        }

        void BrowseForMrt()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Locate mrt.exe",
                Filter = "MRT executable|mrt.exe|All executables|*.exe",
                FileName = "mrt.exe"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (!File.Exists(dlg.FileName)) return;
                mrtExePath = dlg.FileName;
                lblMrtPath.Text = "  ✔  " + dlg.FileName + "  (manual)";
                lblMrtPath.ForeColor = C_GREEN;
                btnStartScan.Enabled = true;
                AddLog("Info", "MRT set manually: " + dlg.FileName, C_GREEN);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  START SCAN
        //  ─────────────────────────────────────────────────────────
        //  Correct MRT flags:
        //    GUI Quick   →  mrt.exe          (opens wizard, quick default)
        //    GUI Full    →  mrt.exe /F       (opens wizard, full pre-selected)
        //    Silent Quick→  mrt.exe /Q
        //    Silent Full →  mrt.exe /Q /F    (/EX was WRONG — caused instant finish)
        // ════════════════════════════════════════════════════════════
        void BtnStart_Click(object sender, EventArgs e)
        {
            if (!AdminHelper.EnsureAdmin("MRT Scan")) return;

            if (string.IsNullOrEmpty(mrtExePath) || !File.Exists(mrtExePath))
            {
                mrtExePath = FindMrtPath();
                if (mrtExePath == null)
                {
                    MessageBox.Show(
                        "mrt.exe could not be found.\nRun Windows Update first.",
                        "MRT Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            string scanName, mrtArgs;
            bool silentMode;

            if (rbGuiQuick.Checked) { scanName = "Quick Scan (GUI)"; mrtArgs = ""; silentMode = false; }
            else if (rbGuiFull.Checked) { scanName = "Full Scan (GUI)"; mrtArgs = "/F"; silentMode = false; }
            else if (rbSilentQuick.Checked) { scanName = "Quick Scan (Silent)"; mrtArgs = "/Q"; silentMode = true; }
            else { scanName = "Full Scan (Silent)"; mrtArgs = "/Q /F"; silentMode = true; }

            string confirmMsg = silentMode
                ? string.Format(
                    "Start MRT {0}?\n\nCommand:  mrt.exe {1}\n\n" +
                    "Runs silently with no window.\nResults read from mrt.log when done.\n\n" +
                    "⚠  Full scan may take SEVERAL HOURS.\n\nContinue?",
                    scanName, mrtArgs.Trim())
                : string.Format(
                    "Start MRT {0}?\n\nCommand:  mrt.exe {1}\n\n" +
                    "The official Microsoft MRT window will open.\n" +
                    "Watch real progress there.\nResults read from mrt.log when closed.\n\nContinue?",
                    scanName, mrtArgs.Trim());

            if (MessageBox.Show(confirmMsg, "Confirm MRT Scan",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            scanning = true;
            scanStart = DateTime.Now;
            btnStartScan.Enabled = false;
            btnCancel.Enabled = true;
            scanBar.Animate = true;
            scanBar.Value = 0;
            scanBar.SetColors(C_RED, C_AMBER);
            scanBar.Invalidate();
            lblScanStatus.Text = silentMode
                ? string.Format("Running {0} silently...", scanName)
                : "MRT window open — scan in progress...";
            lblScanStatus.ForeColor = C_AMBER;
            lblPct.Text = silentMode ? "..." : "GUI";
            elapsedTimer.Start();

            AddLog("Started",
                string.Format("mrt.exe {0}  [{1}]",
                    mrtArgs.Trim(), mrtExePath), C_BLUE);
            SetStatus(string.Format("Running MRT {0}...", scanName));

            mrtProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = mrtExePath,
                    Arguments = mrtArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = silentMode
                        ? ProcessWindowStyle.Hidden
                        : ProcessWindowStyle.Normal,
                    CreateNoWindow = silentMode
                },
                EnableRaisingEvents = true
            };
            mrtProc.Exited += (s2, e2) =>
            {
                if (InvokeRequired)
                    Invoke(new Action(() => ScanFinished(mrtProc.ExitCode)));
                else
                    ScanFinished(mrtProc.ExitCode);
            };

            if (silentMode)
            {
                var pulse = new System.Windows.Forms.Timer { Interval = 60 };
                int step = 0;
                pulse.Tick += (s2, e2) =>
                {
                    step = (step + 1) % 100;
                    scanBar.Value = step;
                    if (!scanBar.Animate) pulse.Stop();
                };
                pulse.Start();
            }

            try
            {
                mrtProc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => mrtProc.WaitForExit());
            }
            catch (Exception ex)
            {
                ScanFinished(-1);
                AddLog("Error", ex.Message, C_RED);
            }
        }

        void ScanFinished(int exitCode)
        {
            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStartScan.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - scanStart;
            string elapsed = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            string resultText = exitCode == 0 ? "✔  Clean — no threats found"
                               : exitCode == 1 ? "⚠  Threat found and removed"
                               : exitCode == 2 ? "⚠  Threat found, partial removal"
                               : "✖  Scan error or cancelled";
            Color resultColor = exitCode == 0 ? C_GREEN
                               : exitCode == 1 ? C_AMBER
                               : exitCode == 2 ? C_AMBER : C_RED;

            scanBar.Value = 100;
            scanBar.SetColors(exitCode >= 0 ? C_GREEN : C_RED, C_TEAL);
            scanBar.Invalidate();
            lblScanStatus.Text = resultText;
            lblScanStatus.ForeColor = resultColor;
            lblPct.Text = "Done";

            AddLog("Finished",
                string.Format("Duration: {0}  |  Exit: {1}  |  {2}",
                    elapsed, exitCode, resultText),
                resultColor);
            SetStatus(string.Format("✔  MRT scan finished in {0}. {1}", elapsed, resultText));
            ReadMrtLog();
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (mrtProc != null && !mrtProc.HasExited)
                try { mrtProc.Kill(); } catch { }
            scanning = false;
            scanBar.Animate = false;
            elapsedTimer.Stop();
            btnStartScan.Enabled = true;
            btnCancel.Enabled = false;
            lblScanStatus.Text = "Scan cancelled.";
            lblScanStatus.ForeColor = C_AMBER;
            SetStatus("Scan cancelled.");
            AddLog("Cancelled", "MRT scan cancelled by user.", C_AMBER);
        }

        void ReadMrtLog()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "debug", "mrt.log");
            if (!File.Exists(logPath))
            { AddLog("Log", "mrt.log not found: " + logPath, C_SUB); return; }
            try
            {
                string[] lines = File.ReadAllLines(logPath);
                AddLog("Log", string.Format("mrt.log — {0} lines", lines.Length), C_BLUE);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string lo = line.ToLower();
                    Color c = lo.Contains("found") || lo.Contains("infected") ? C_RED
                            : lo.Contains("clean") || lo.Contains("no threat") ? C_GREEN
                            : lo.Contains("error") ? C_AMBER : C_SUB;
                    AddLog("Log", line.Trim(), c);
                }
            }
            catch (Exception ex)
            { AddLog("Error", "Could not read mrt.log: " + ex.Message, C_RED); }
        }

        void OpenMrtLog()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "debug", "mrt.log");
            if (File.Exists(logPath))
                Process.Start("notepad.exe", logPath);
            else
                MessageBox.Show(
                    "MRT log not found.\nRun a scan first.\n\nExpected:\n" + logPath,
                    "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AddLog(string ev, string detail, Color fg)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(ev, detail, fg))); return; }
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(ev);
            item.SubItems.Add(detail);
            item.ForeColor = fg;
            item.Tag = fg == C_GREEN ? "ok" : fg == C_RED ? "fail" : "info";
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

        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 6, e.Bounds.Height), sf);
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
            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1
                        ? (tag == "ok" ? C_GREEN
                         : tag == "fail" ? C_RED : C_AMBER)
                     : e.Item.ForeColor;
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (mrtProc != null && !mrtProc.HasExited)
                try { mrtProc.Kill(); } catch { }
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SCAN PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class ScanProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public ScanProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bg
            _t = new System.Windows.Forms.Timer { Interval = 25 };
            _t.Tick += (s, e) =>
            {
                if (_animate)
                {
                    _pulse = (_pulse + 5) % (Width > 0 ? Width * 2 : 400);
                    Invalidate();
                }
            };
            _t.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);
            if (_animate)
            {
                int pw = Math.Max(Width / 3, 60);
                int px = _pulse % (Width + pw) - pw;
                var blend = new ColorBlend(3);
                blend.Colors = new[]
                {
                    Color.Transparent,
                    Color.FromArgb(200, _c1.R, _c1.G, _c1.B),
                    Color.Transparent
                };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                var rect = new Rectangle(px, 0, pw, Height);
                if (rect.Width > 0)
                {
                    using (var br = new LinearGradientBrush(
                        new Rectangle(rect.X, 0, Math.Max(rect.Width, 1), Height),
                        Color.Transparent,
                        Color.FromArgb(200, _c1.R, _c1.G, _c1.B),
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
                        new Rectangle(0, 0, fw, Height), _c1, _c2,
                        LinearGradientMode.Horizontal))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height));
                    using (var br = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _t != null) _t.Stop();
            base.Dispose(disposing);
        }
    }
}