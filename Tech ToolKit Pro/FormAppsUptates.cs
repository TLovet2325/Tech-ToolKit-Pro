using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormAppsUptates : Form
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
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  APP UPDATE DATA MODEL
        // ════════════════════════════════════════════════════════════
        class AppUpdate
        {
            public string Name { get; set; }
            public string ID { get; set; }
            public string CurrentVer { get; set; }
            public string AvailableVer { get; set; }
            public string Source { get; set; }
            public string Status { get; set; } = "Pending";
            public Color StatusColor { get; set; }
            public bool Selected { get; set; } = true;
            public bool IsUnknownVer { get; set; }
            public ListViewItem LvItem { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, statsBar, filterBar, listPanel,
                        outputPanel, bottomBar, progPanel;
        Label lblTitle, lblStatus;
        Label lblTotal, lblSelected, lblUpToDate;
        Label lblProgText, lblProgPct, lblProgSub, lblElapsed;
        AppProgressBar progBar;
        ListView appList;
        RichTextBox rtbOutput;
        Button btnCheck, btnUpdateAll, btnUpdateSelected,
                        btnCancel, btnSelectAll, btnSelectNone,
                        btnClearOutput, btnOpenWinget;
        TextBox txtSearch;
        CheckBox chkShowAll;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime updateStart;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<AppUpdate> allApps = new List<AppUpdate>();
        List<AppUpdate> viewApps = new List<AppUpdate>();
        BackgroundWorker checkWorker;
        BackgroundWorker updateWorker;
        bool checking = false;
        bool updating = false;
        bool wingetFound = false;
        string wingetPath = "winget";

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormAppsUptates()
        {
            BuildUI();
            SetupWorkers();
            DetectWinget();
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "App Updates";
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
                    new Rectangle(0, 0, 4, 52), C_GREEN, C_TEAL,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "📦  APPLICATION  UPDATE  CENTER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Powered by winget  ·  Check · Select · Update installed apps",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ── Stats bar ─────────────────────────────────────────────
            statsBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(16, 22, 30)
            };
            statsBar.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(
                    Color.FromArgb(12, C_GREEN.R, C_GREEN.G, C_GREEN.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        statsBar.Width, statsBar.Height);
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, statsBar.Height - 1,
                        statsBar.Width, statsBar.Height - 1);
            };

            lblTotal = MakeStat("0 updates available", C_AMBER, new Point(16, 14));
            lblSelected = MakeStat("0 selected", C_GREEN, new Point(210, 14));
            lblUpToDate = MakeStat("", C_SUB, new Point(330, 14));

            statsBar.Controls.AddRange(new Control[]
                { lblTotal, lblSelected, lblUpToDate });

            // ── Filter bar ────────────────────────────────────────────
            filterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = C_SURF
            };
            filterBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, filterBar.Height - 1,
                        filterBar.Width, filterBar.Height - 1);
            };

            txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 9),
                Size = new Size(220, 26),
                Text = "Filter apps..."
            };
            txtSearch.GotFocus += (s, e) =>
            { if (txtSearch.Text == "Filter apps...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) =>
            { if (txtSearch.Text == "") txtSearch.Text = "Filter apps..."; };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            chkShowAll = new CheckBox
            {
                Text = "Show unknown versions",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(244, 12),
                BackColor = Color.Transparent,
                Checked = false
            };
            chkShowAll.CheckedChanged += (s, e) => ApplyFilter();

            var btnSelAll = MakeSmallBtn("☑ All", C_BLUE, new Size(65, 26));
            var btnSelNone = MakeSmallBtn("☐ None", C_SUB, new Size(65, 26));
            btnSelAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelNone.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelAll.Click += (s, e) => ToggleAll(true);
            btnSelNone.Click += (s, e) => ToggleAll(false);

            filterBar.Controls.AddRange(new Control[]
                { txtSearch, chkShowAll, btnSelAll, btnSelNone });
            filterBar.Resize += (s, e) =>
            {
                btnSelAll.Location = new Point(filterBar.Width - 148, 9);
                btnSelNone.Location = new Point(filterBar.Width - 76, 9);
            };

            // ── Progress panel ────────────────────────────────────────
            progPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = C_SURF,
                Padding = new Padding(16, 8, 16, 8),
                Visible = false
            };
            progPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, progPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0, progPanel.Height - 1,
                        progPanel.Width, progPanel.Height - 1);
                }
            };

            lblProgText = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(16, 8)
            };

            lblProgPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            progBar = new AppProgressBar(C_GREEN, C_TEAL)
            {
                Location = new Point(16, 28),
                Size = new Size(100, 16),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblProgSub = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 52)
            };

            lblElapsed = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            progPanel.Controls.AddRange(new Control[]
                { lblProgText, lblProgPct, progBar, lblProgSub, lblElapsed });
            progPanel.Resize += (s, e) =>
            {
                progBar.Size = new Size(progPanel.Width - 80, 16);
                lblProgPct.Location = new Point(progPanel.Width - 54, 26);
                lblElapsed.Location = new Point(progPanel.Width - 120, 52);
            };

            // ── App list (top half) ───────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 260,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            appList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 0),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                MultiSelect = true,
                CheckBoxes = true
            };
            appList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            appList.Columns.Add("Application", 330);
            appList.Columns.Add("App ID", 230);
            appList.Columns.Add("Current", 165);
            appList.Columns.Add("Available", 145);
            appList.Columns.Add("Source", 130);
            appList.Columns.Add("Status", 126);

            appList.DrawColumnHeader += DrawAppHeader;
            appList.DrawItem += (s, e) => { };
            appList.DrawSubItem += DrawAppRow;
            appList.ItemChecked += (s, e) => UpdateSelectedCount();

            listPanel.Controls.Add(appList);
            listPanel.Resize += (s, e) =>
                appList.Size = new Size(listPanel.Width - 20, listPanel.Height - 12);

            // ── Output console (bottom half) ──────────────────────────
            outputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 8)
            };

            var lblOut = new Label
            {
                Text = "WINGET OUTPUT",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            btnClearOutput = MakeSmallBtn("🗑 Clear", C_SUB, new Size(70, 22));
            btnClearOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearOutput.Click += (s, e) => rtbOutput.Clear();

            rtbOutput = new RichTextBox
            {
                BackColor = Color.FromArgb(10, 14, 20),
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f),
                Location = new Point(0, 24),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };
            rtbOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                             | AnchorStyles.Left | AnchorStyles.Right;

            outputPanel.Controls.AddRange(new Control[]
                { lblOut, btnClearOutput, rtbOutput });
            outputPanel.Resize += (s, e) =>
            {
                rtbOutput.Size = new Size(outputPanel.Width - 20, outputPanel.Height - 30);
                btnClearOutput.Location = new Point(outputPanel.Width - 90, 0);
            };

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = C_SURF
            };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };

            lblStatus = new Label
            {
                Text = "Click 'Check for Updates' to scan installed applications.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnCheck = MakeBtn("🔍  Check Updates", C_BLUE, new Size(165, 34));
            btnUpdateAll = MakeBtn("⬆  Update All", C_GREEN, new Size(140, 34));
            btnUpdateSelected = MakeBtn("☑  Update Selected", C_TEAL, new Size(160, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnOpenWinget = MakeBtn("📦  Open Winget Docs", C_PURPLE, new Size(165, 34));

            btnUpdateAll.Enabled = false;
            btnUpdateSelected.Enabled = false;
            btnCancel.Enabled = false;

            btnCheck.Click += BtnCheck_Click;
            btnUpdateAll.Click += (s, e) => StartUpdate(false);
            btnUpdateSelected.Click += (s, e) => StartUpdate(true);
            btnCancel.Click += BtnCancel_Click;
            btnOpenWinget.Click += (s, e) =>
                Process.Start("https://aka.ms/winget-cli");

            bottomBar.Controls.AddRange(new Control[]
            {
                lblStatus, btnCheck, btnUpdateAll,
                btnUpdateSelected, btnCancel, btnOpenWinget
            });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnCheck.Location = new Point(16, y);
                btnUpdateAll.Location = new Point(193, y);
                btnUpdateSelected.Location = new Point(345, y);
                btnCancel.Location = new Point(517, y);
                btnOpenWinget.Location = new Point(629, y);
                lblStatus.Location = new Point(808,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(outputPanel);
            Controls.Add(listPanel);
            Controls.Add(progPanel);
            Controls.Add(filterBar);
            Controls.Add(statsBar);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - updateStart;
                lblElapsed.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT WINGET
        // ════════════════════════════════════════════════════════════
        void DetectWinget()
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                proc.Start();
                string ver = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);

                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(ver))
                {
                    wingetFound = true;
                    wingetPath = "winget";
                    AppendOutput(string.Format("✔  winget found — version {0}", ver), C_GREEN);
                    AppendOutput("ℹ  Click 'Check Updates' to scan all installed applications.", C_SUB);
                    SetStatus("winget ready. Click 'Check Updates' to start.");
                }
                else
                {
                    WingetNotFound();
                }
            }
            catch
            {
                WingetNotFound();
            }
        }

        void WingetNotFound()
        {
            wingetFound = false;
            btnCheck.Enabled = false;
            btnUpdateAll.Enabled = false;
            btnUpdateSelected.Enabled = false;

            AppendOutput("✖  winget not found on this system.", C_RED);
            AppendOutput("", C_TXT);
            AppendOutput("winget (Windows Package Manager) is required.", C_AMBER);
            AppendOutput("It is included with Windows 10 1709+ / Windows 11.", C_AMBER);
            AppendOutput("", C_TXT);
            AppendOutput("To install winget:", C_SUB);
            AppendOutput("  1. Open Microsoft Store", C_SUB);
            AppendOutput("  2. Search 'App Installer'", C_SUB);
            AppendOutput("  3. Install or Update 'App Installer' by Microsoft", C_SUB);
            AppendOutput("  4. Restart this application", C_SUB);
            AppendOutput("", C_TXT);
            AppendOutput("Or click 'Open Winget Docs' to download manually.", C_BLUE);

            SetStatus("✖  winget not found. Install 'App Installer' from Microsoft Store.");

            MessageBox.Show(
                "winget (Windows Package Manager) is not installed.\n\n" +
                "To use App Updates you need winget.\n\n" +
                "Install 'App Installer' from the Microsoft Store\n" +
                "then restart Tech ToolKit Pro.",
                "winget Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        // ════════════════════════════════════════════════════════════
        //  SETUP WORKERS
        // ════════════════════════════════════════════════════════════
        void SetupWorkers()
        {
            checkWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            checkWorker.DoWork += CheckWorker_DoWork;
            checkWorker.ProgressChanged += CheckWorker_Progress;
            checkWorker.RunWorkerCompleted += CheckWorker_Completed;

            updateWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            updateWorker.DoWork += UpdateWorker_DoWork;
            updateWorker.ProgressChanged += UpdateWorker_Progress;
            updateWorker.RunWorkerCompleted += UpdateWorker_Completed;
        }

        // ════════════════════════════════════════════════════════════
        //  CHECK FOR UPDATES
        // ════════════════════════════════════════════════════════════
        void BtnCheck_Click(object sender, EventArgs e)
        {
            if (checking || updating || !wingetFound) return;

            allApps.Clear();
            viewApps.Clear();
            appList.Items.Clear();
            rtbOutput.Clear();

            checking = true;
            btnCheck.Enabled = false;
            btnUpdateAll.Enabled = false;
            btnUpdateSelected.Enabled = false;
            progPanel.Visible = true;
            progBar.Animate = true;
            progBar.Value = 0;
            progBar.SetColors(C_BLUE, C_TEAL);
            progBar.Invalidate();
            lblProgText.Text = "Checking for application updates...";
            lblProgPct.Text = "...";
            lblProgSub.Text = "Running winget upgrade...";

            SetStatus("Checking for updates...");
            ResetStats();

            AppendOutput("═══ APP UPDATE CHECK ═══", C_GREEN);
            AppendOutput(string.Format("Started: {0}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")), C_SUB);
            AppendOutput("Running: winget upgrade --include-unknown", C_SUB);
            AppendOutput("", C_TXT);

            checkWorker.RunWorkerAsync();
        }

        // ════════════════════════════════════════════════════════════
        //  CHECK WORKER
        // ════════════════════════════════════════════════════════════
        void CheckWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var found = new List<AppUpdate>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = wingetPath,
                    Arguments = "upgrade --include-unknown",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                var proc = new Process { StartInfo = psi };
                proc.Start();

                string allOutput = "";
                string line;
                bool headerPassed = false;
                int lineCount = 0;

                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (checkWorker.CancellationPending) { e.Cancel = true; proc.Kill(); return; }

                    allOutput += line + "\n";
                    lineCount++;

                    checkWorker.ReportProgress(0,
                        new object[] { "raw", line });

                    // Detect separator line "──────" to know header is done
                    if (line.Contains("──") || line.Contains("---"))
                    {
                        headerPassed = true;
                        continue;
                    }

                    if (!headerPassed) continue;

                    // Parse update lines
                    var parsed = ParseWingetLine(line);
                    if (parsed != null)
                    {
                        found.Add(parsed);
                        checkWorker.ReportProgress(0,
                            new object[] { "found", parsed });
                    }
                }

                proc.WaitForExit(120000);

                // Fallback: try to parse the entire output if table parsing didn't work
                if (found.Count == 0 && !string.IsNullOrEmpty(allOutput))
                {
                    var fallback = ParseWingetOutput(allOutput);
                    found.AddRange(fallback);
                }
            }
            catch (Exception ex)
            {
                e.Result = new object[] { "error", ex.Message };
                return;
            }

            e.Result = new object[] { "ok", found };
        }

        // ── Parses a single winget table row ─────────────────────────
        AppUpdate ParseWingetLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            if (line.TrimStart().StartsWith("Name") ||
                line.TrimStart().StartsWith("─") ||
                line.TrimStart().StartsWith("-") ||
                line.Contains("upgrades available") ||
                line.Contains("No applicable")) return null;

            // Split on 2+ spaces (winget uses column padding)
            var parts = Regex.Split(line.Trim(), @"\s{2,}")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            if (parts.Length < 3) return null;

            string name = parts[0].Trim();
            string id = parts.Length > 1 ? parts[1].Trim() : "";
            string current = parts.Length > 2 ? parts[2].Trim() : "–";
            string avail = parts.Length > 3 ? parts[3].Trim() : "–";
            string source = parts.Length > 4 ? parts[4].Trim() : "winget";

            // Filter garbage lines
            if (name.Length < 2 || id.Length < 2) return null;
            if (name.All(c => c == '─' || c == '-' || c == ' ')) return null;

            bool unknownVer = avail.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                           || current.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

            return new AppUpdate
            {
                Name = name,
                ID = id,
                CurrentVer = current,
                AvailableVer = avail,
                Source = source,
                IsUnknownVer = unknownVer,
                Selected = !unknownVer,
                Status = "Pending",
                StatusColor = C_SUB
            };
        }

        // ── Fallback full-output parser ───────────────────────────────
        List<AppUpdate> ParseWingetOutput(string output)
        {
            var result = new List<AppUpdate>();
            var lines = output.Split('\n');

            bool inTable = false;
            foreach (var line in lines)
            {
                if (line.Contains("──") || line.Contains("---"))
                { inTable = true; continue; }

                if (!inTable) continue;

                var parsed = ParseWingetLine(line);
                if (parsed != null) result.Add(parsed);
            }
            return result;
        }

        void CheckWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            if (type == "raw")
            {
                AppendOutput(d[1] as string ?? "", C_SUB);
            }
            else if (type == "found")
            {
                var app = d[1] as AppUpdate;
                if (app == null) return;

                var item = new ListViewItem(app.Name);
                item.Checked = app.Selected;
                item.SubItems.Add(app.ID);
                item.SubItems.Add(app.CurrentVer);
                item.SubItems.Add(app.AvailableVer);
                item.SubItems.Add(app.Source);
                item.SubItems.Add("Pending");
                item.Tag = app;
                item.ForeColor = app.IsUnknownVer ? C_SUB : C_TXT;
                app.LvItem = item;

                allApps.Add(app);
                appList.Items.Add(item);
                UpdateSelectedCount();
                UpdateStats();

                lblProgSub.Text = string.Format("Found: {0}", app.Name);
            }
        }

        void CheckWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            checking = false;
            progBar.Animate = false;
            btnCheck.Enabled = true;

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Check cancelled.");
                AppendOutput("⚠  Check cancelled by user.", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null)
            {
                // Result already populated via Progress events
                res = new object[] { "ok", allApps };
            }

            if ((string)res[0] == "error")
            {
                string err = res[1] as string;
                progPanel.Visible = false;
                SetStatus("Check failed: " + err);
                AppendOutput("✖  Error: " + err, C_RED);
                return;
            }

            // If we got results via result (not progress events)
            if (res[1] is List<AppUpdate> freshList && freshList.Count > 0
                && allApps.Count == 0)
            {
                allApps = freshList;
                ApplyFilter();
            }

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.SetColors(allApps.Count > 0 ? C_AMBER : C_GREEN,
                allApps.Count > 0 ? C_RED : C_TEAL);
            progBar.Animate = false;
            progBar.Invalidate();

            int known = allApps.Count(a => !a.IsUnknownVer);

            if (allApps.Count == 0)
            {
                lblProgText.Text = "✔  All applications are up to date!";
                lblProgSub.Text = "No updates available.";
                SetStatus("✔  All applications are up to date.");
                AppendOutput("", C_TXT);
                AppendOutput("✔  All applications are up to date!", C_GREEN);
            }
            else
            {
                lblProgText.Text = string.Format(
                    "Found {0} update(s) available  ({1} with known versions)",
                    allApps.Count, known);
                lblProgSub.Text = string.Format(
                    "{0} apps can be updated automatically", known);

                btnUpdateAll.Enabled = known > 0;
                btnUpdateSelected.Enabled = known > 0;

                SetStatus(string.Format(
                    "{0} app update(s) found. {1} ready to install.",
                    allApps.Count, known));

                AppendOutput("", C_TXT);
                AppendOutput(string.Format(
                    "═══ Found {0} update(s) — {1} ready ═══",
                    allApps.Count, known), C_GREEN);
            }

            ApplyFilter();
            UpdateStats();
        }

        // ════════════════════════════════════════════════════════════
        //  START UPDATE
        // ════════════════════════════════════════════════════════════
        void StartUpdate(bool selectedOnly)
        {
            if (updating || checking) return;

            List<AppUpdate> toUpdate = selectedOnly
                ? allApps.Where(a => a.Selected && a.Status == "Pending" && !a.IsUnknownVer).ToList()
                : allApps.Where(a => a.Status == "Pending" && !a.IsUnknownVer).ToList();

            if (toUpdate.Count == 0)
            {
                MessageBox.Show(
                    selectedOnly
                        ? "No selected apps ready to update.\n\nMake sure checkboxes are ticked\nand the apps don't have 'Unknown' versions."
                        : "No apps to update.",
                    "Nothing to Update",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string preview = string.Join("\n",
                toUpdate.Take(10).Select(a =>
                    string.Format("  • {0}  {1} → {2}",
                        a.Name.Length > 40 ? a.Name.Substring(0, 40) + "…" : a.Name,
                        a.CurrentVer, a.AvailableVer)));
            if (toUpdate.Count > 10)
                preview += string.Format("\n  ... and {0} more", toUpdate.Count - 10);

            var confirm = MessageBox.Show(
                string.Format(
                    "Update {0} application(s)?\n\n{1}\n\n" +
                    "winget will download and install each update.\n" +
                    "Some apps may require a restart.\nContinue?",
                    toUpdate.Count, preview),
                "Confirm App Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            updating = true;
            updateStart = DateTime.Now;

            btnCheck.Enabled = false;
            btnUpdateAll.Enabled = false;
            btnUpdateSelected.Enabled = false;
            btnCancel.Enabled = true;
            progPanel.Visible = true;
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Value = 0;
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "Updating {0} application(s)...", toUpdate.Count);
            lblProgPct.Text = "0%";
            lblProgSub.Text = "Starting...";

            elapsedTimer.Start();
            SetStatus(string.Format("Updating {0} app(s)...", toUpdate.Count));

            AppendOutput("", C_TXT);
            AppendOutput("═══ STARTING APP UPDATES ═══", C_GREEN);
            AppendOutput(string.Format("Apps to update: {0}", toUpdate.Count), C_SUB);
            AppendOutput(string.Format("Time: {0}", DateTime.Now.ToString("HH:mm:ss")), C_SUB);
            AppendOutput("", C_TXT);

            updateWorker.RunWorkerAsync(toUpdate);
        }

        // ════════════════════════════════════════════════════════════
        //  UPDATE WORKER
        // ════════════════════════════════════════════════════════════
        void UpdateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var toUpdate = e.Argument as List<AppUpdate>;
            if (toUpdate == null) return;

            int total = toUpdate.Count;
            int done = 0;

            for (int i = 0; i < total; i++)
            {
                if (updateWorker.CancellationPending) { e.Cancel = true; return; }

                var app = toUpdate[i];
                int pct = (int)((double)i / total * 100);

                updateWorker.ReportProgress(pct,
                    new object[]
                    {
                        "start", i, total, app.ID, app.Name,
                        app.CurrentVer, app.AvailableVer
                    });

                string output = "";
                int exitCode = 0;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = wingetPath,
                        Arguments = string.Format(
                            "upgrade --id \"{0}\" --silent --accept-source-agreements --accept-package-agreements",
                            app.ID),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };

                    var proc = new Process { StartInfo = psi };
                    proc.Start();

                    // Stream output line by line
                    string line;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        if (updateWorker.CancellationPending) break;
                        output += line + "\n";
                        updateWorker.ReportProgress(pct,
                            new object[] { "output", line, app.Name });
                    }

                    string errOut = proc.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(errOut)) output += errOut;

                    proc.WaitForExit(300000); // 5 min timeout per app
                    exitCode = proc.ExitCode;
                    done++;
                }
                catch (Exception ex)
                {
                    output = ex.Message;
                    exitCode = -1;
                    done++;
                }

                int newPct = (int)((double)done / total * 100);
                bool ok = (exitCode == 0 || exitCode == -1073741515
                           || output.ToLower().Contains("successfully installed")
                           || output.ToLower().Contains("successfully upgraded"));

                updateWorker.ReportProgress(newPct,
                    new object[] { "done", i, ok, app.ID, app.Name, exitCode });
            }

            e.Result = new object[] { "ok", done, total };
        }

        void UpdateWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            progBar.Value = e.ProgressPercentage;
            lblProgPct.Text = e.ProgressPercentage + "%";

            if (type == "start")
            {
                int i = (int)d[1];
                int total = (int)d[2];
                string appId = d[3] as string;
                string appName = d[4] as string;
                string curVer = d[5] as string;
                string newVer = d[6] as string;

                lblProgText.Text = string.Format(
                    "Updating {0} / {1}:  {2}",
                    i + 1, total,
                    appName.Length > 40 ? appName.Substring(0, 40) + "…" : appName);
                lblProgSub.Text = string.Format(
                    "{0}  →  {1}", curVer, newVer);

                AppendOutput(string.Format(
                    "─── [{0}/{1}] Updating: {2} ───",
                    i + 1, total, appName), C_AMBER);
                AppendOutput(string.Format(
                    "    ID: {0}  |  {1} → {2}",
                    appId, curVer, newVer), C_SUB);

                UpdateAppStatus(appName, "Updating...", C_AMBER);
            }
            else if (type == "output")
            {
                string line = d[1] as string ?? "";
                string appName = d[2] as string ?? "";
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Color c = line.ToLower().Contains("error") ? C_RED
                            : line.ToLower().Contains("success") ? C_GREEN
                            : line.ToLower().Contains("download") ? C_BLUE
                            : line.ToLower().Contains("install") ? C_TEAL
                            : C_SUB;
                    AppendOutput("    " + line.Trim(), c);
                    lblProgSub.Text = line.Trim().Length > 80
                        ? line.Trim().Substring(0, 80) + "…"
                        : line.Trim();
                }
            }
            else if (type == "done")
            {
                int i = (int)d[1];
                bool ok = Convert.ToBoolean(d[2]);
                string appId = d[3] as string;
                string appName = d[4] as string;
                int code = (int)d[5];

                string status = ok ? "✔ Updated" : string.Format("✖ Failed ({0})", code);
                Color sc = ok ? C_GREEN : C_RED;

                UpdateAppStatus(appName, ok ? "Updated" : "Failed", sc);
                AppendOutput(string.Format(
                    "    Result: {0}", status), sc);
                AppendOutput("", C_TXT);
            }
        }

        void UpdateWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            updating = false;
            elapsedTimer.Stop();

            btnCheck.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - updateStart;
            string elap = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Update cancelled.");
                AppendOutput("⚠  Update cancelled by user.", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null) return;

            if ((string)res[0] == "ok")
            {
                int done = (int)res[1];
                int total = (int)res[2];
                int failed = allApps.Count(a => a.Status == "Failed");

                progBar.Value = 100;
                lblProgPct.Text = "100%";
                progBar.SetColors(failed > 0 ? C_AMBER : C_GREEN,
                    failed > 0 ? C_RED : C_TEAL);
                progBar.Animate = false;
                progBar.Invalidate();

                lblProgText.Text = string.Format(
                    "✔  Update complete — {0}/{1} updated, {2} failed",
                    done - failed, total, failed);
                lblProgSub.Text = string.Format("Duration: {0}", elap);

                AppendOutput("═══ UPDATE COMPLETE ═══", C_GREEN);
                AppendOutput(string.Format(
                    "Updated : {0}/{1}", done - failed, total), C_GREEN);
                AppendOutput(string.Format(
                    "Failed  : {0}", failed),
                    failed > 0 ? C_RED : C_GREEN);
                AppendOutput(string.Format(
                    "Duration: {0}", elap), C_SUB);

                SetStatus(string.Format(
                    "✔  {0}/{1} apps updated in {2}.{3}",
                    done - failed, total, elap,
                    failed > 0 ? string.Format("  {0} failed.", failed) : ""));

                btnUpdateAll.Enabled = false;
                btnUpdateSelected.Enabled = false;

                if (failed > 0)
                    MessageBox.Show(
                        string.Format(
                            "{0} app(s) failed to update.\n\n" +
                            "Some apps may require you to close them first,\n" +
                            "or update manually from their settings.",
                            failed),
                        "Some Updates Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
            }
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (checking && checkWorker.IsBusy) checkWorker.CancelAsync();
            if (updating && updateWorker.IsBusy) updateWorker.CancelAsync();
            btnCancel.Enabled = false;
            SetStatus("Cancelling...");
        }

        // ════════════════════════════════════════════════════════════
        //  FILTER
        // ════════════════════════════════════════════════════════════
        void ApplyFilter()
        {
            string s = txtSearch.Text.Trim().ToLower();
            if (s == "filter apps...") s = "";

            viewApps = allApps.Where(a =>
            {
                if (!chkShowAll.Checked && a.IsUnknownVer) return false;
                if (!string.IsNullOrEmpty(s) &&
                    !a.Name.ToLower().Contains(s) &&
                    !a.ID.ToLower().Contains(s)) return false;
                return true;
            }).ToList();

            appList.BeginUpdate();
            appList.Items.Clear();
            foreach (var a in viewApps)
            {
                var item = new ListViewItem(a.Name);
                item.Checked = a.Selected;
                item.SubItems.Add(a.ID);
                item.SubItems.Add(a.CurrentVer);
                item.SubItems.Add(a.AvailableVer);
                item.SubItems.Add(a.Source);
                item.SubItems.Add(a.Status);
                item.Tag = a;
                item.ForeColor = a.IsUnknownVer ? C_SUB
                               : a.Status == "Updated" ? C_GREEN
                               : a.Status == "Failed" ? C_RED : C_TXT;
                a.LvItem = item;
                appList.Items.Add(item);
            }
            appList.EndUpdate();
            UpdateSelectedCount();
        }

        void ToggleAll(bool check)
        {
            foreach (ListViewItem item in appList.Items)
            {
                var a = item.Tag as AppUpdate;
                if (a != null && !a.IsUnknownVer)
                {
                    item.Checked = check;
                    a.Selected = check;
                }
            }
            UpdateSelectedCount();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void UpdateAppStatus(string name, string status, Color c)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => UpdateAppStatus(name, status, c))); return; }

            var app = allApps.FirstOrDefault(a =>
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(a.Name.Substring(0, Math.Min(20, a.Name.Length)),
                    StringComparison.OrdinalIgnoreCase));
            if (app == null) return;

            app.Status = status;
            app.StatusColor = c;

            if (app.LvItem != null && app.LvItem.SubItems.Count > 5)
            {
                app.LvItem.SubItems[5].Text = status;
                app.LvItem.ForeColor = c;
                app.LvItem.ListView?.Invalidate();
            }
        }

        void UpdateSelectedCount()
        {
            int sel = 0;
            foreach (ListViewItem item in appList.Items)
            {
                if (item.Checked && item.Tag is AppUpdate a)
                { a.Selected = true; sel++; }
                else if (item.Tag is AppUpdate a2)
                    a2.Selected = false;
            }
            lblSelected.Text = string.Format("{0} selected", sel);
        }

        void UpdateStats()
        {
            int known = allApps.Count(a => !a.IsUnknownVer);
            int unknown = allApps.Count(a => a.IsUnknownVer);
            lblTotal.Text = string.Format("{0} updates available", allApps.Count);
            lblUpToDate.Text = unknown > 0
                ? string.Format("{0} with unknown version", unknown) : "";
        }

        void ResetStats()
        {
            lblTotal.Text = "0 updates available";
            lblSelected.Text = "0 selected";
            lblUpToDate.Text = "";
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("⚠") ? C_AMBER
                                : msg.Contains("✖") ? C_RED : C_SUB;
        }

        void AppendOutput(string text, Color c)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AppendOutput(text, c))); return; }
            rtbOutput.SelectionStart = rtbOutput.TextLength;
            rtbOutput.SelectionLength = 0;
            rtbOutput.SelectionColor = c;
            rtbOutput.AppendText(text + "\n");
            rtbOutput.ScrollToCaret();
        }

        Label MakeStat(string text, Color c, Point loc) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = c,
            AutoSize = true,
            Location = loc
        };

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

        Button MakeSmallBtn(string text, Color accent, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = accent,
                BackColor = Color.FromArgb(16, accent.R, accent.G, accent.B),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(50, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, accent.R, accent.G, accent.B);
            return b;
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW
        // ════════════════════════════════════════════════════════════
        void DrawAppHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
            {
                var rc = new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                    e.Bounds.Width - 6, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, ft, br, rc, sf);
            }
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawAppRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var app = e.Item.Tag as AppUpdate;

            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : app != null && app.Status == "Updated"
                    ? Color.FromArgb(14, 63, 185, 119)
                    : app != null && app.Status == "Failed"
                        ? Color.FromArgb(14, 248, 81, 73)
                        : app != null && app.IsUnknownVer
                            ? Color.FromArgb(8, 200, 200, 200)
                            : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);

            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_GREEN))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            Color fg = C_TXT;
            if (app != null)
            {
                if (e.ColumnIndex == 0)
                    fg = app.IsUnknownVer ? C_SUB
                       : app.Status == "Updated" ? C_GREEN
                       : app.Status == "Failed" ? C_RED
                       : app.Status == "Updating..." ? C_AMBER : C_TXT;
                else if (e.ColumnIndex == 1) fg = C_SUB;
                else if (e.ColumnIndex == 2) fg = C_SUB;
                else if (e.ColumnIndex == 3)
                    fg = app.IsUnknownVer ? C_BORDER : C_AMBER;
                else if (e.ColumnIndex == 4) fg = C_BLUE;
                else if (e.ColumnIndex == 5)
                    fg = app.Status == "Updated" ? C_GREEN
                       : app.Status == "Failed" ? C_RED
                       : app.Status == "Updating..." ? C_AMBER : C_SUB;
            }

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 0
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, appList.Font, br, rc, sf);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (checkWorker != null && checkWorker.IsBusy) checkWorker.CancelAsync();
            if (updateWorker != null && updateWorker.IsBusy) updateWorker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  APP PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class AppProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public AppProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bgBackColor = Color.Transparent;
            _t = new System.Windows.Forms.Timer { Interval = 25 };
            _t.Tick += (s, e) =>
            {
                if (_animate) { _pulse = (_pulse + 5) % (Width > 0 ? Width * 2 : 400); Invalidate(); }
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
                blend.Colors = new[] { Color.Transparent,
                    Color.FromArgb(200, _c1.R, _c1.G, _c1.B), Color.Transparent };
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

                    if (fw > 10 && _val > 5)
                    {
                        string txt = _val + "%";
                        using (var ft = new Font("Segoe UI Semibold", 7f))
                        using (var br = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                        {
                            var sz = g.MeasureString(txt, ft);
                            g.DrawString(txt, ft, br,
                                (fw - sz.Width) / 2f, (Height - sz.Height) / 2f);
                        }
                    }
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