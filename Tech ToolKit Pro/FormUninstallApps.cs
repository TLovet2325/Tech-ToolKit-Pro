using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Management;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormUninstallApps : Form
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
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  APP DATA MODEL
        // ════════════════════════════════════════════════════════════
        class AppInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public string InstallDate { get; set; }
            public string EstimatedSize { get; set; }
            public string UninstallCmd { get; set; }
            public string RegistryKey { get; set; }
            public bool IsBloat { get; set; }
            public long SizeKB { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  KNOWN BLOATWARE LIST
        // ════════════════════════════════════════════════════════════
        static readonly HashSet<string> BloatKeywords = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Bing","Cortana","Xbox","Candy Crush","Bubble Witch",
            "March of Empires","Microsoft Solitaire","Minecraft",
            "Netflix","Twitter","Facebook","Instagram","TikTok",
            "Spotify","Disney","WildTangent","PicsArt","iHeartRadio",
            "Pandora","HP JumpStart","HP Sure","HP Wolf","McAfee",
            "Norton Security Scan","Avast","AVG Toolbar",
            "Babylon Toolbar","Ask Toolbar","Yahoo Toolbar",
            "Bonjour","QuickTime","RealPlayer","WinZip",
            "WinRAR","CCleaner","Driver Booster","Driver Easy",
            "PC Cleaner","Registry Cleaner","Advanced SystemCare",
            "MyPC Backup","MyCleanPC","SlimCleaner","Optimizer Pro"
        };

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, filterBar, listPanel, logPanel, bottomBar, statsBar;
        Label lblTitle, lblStatus, lblAppCount, lblTotalSize, lblBloatCount;
        ListView appList, logList;
        Button btnLoad, btnUninstall, btnUninstallBloat,
                 btnOpenPrograms, btnClearLog;
        TextBox txtSearch;
        CheckBox chkBloatOnly;
        System.Windows.Forms.ProgressBar loadBar;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<AppInfo> allApps = new List<AppInfo>();
        List<AppInfo> viewApps = new List<AppInfo>();
        bool loading = false;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormUninstallApps()
        {
            BuildUI();
            LoadApps();
            InitializeComponent();
        
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Uninstall Apps";
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
                    new Rectangle(0, 0, 4, 52), C_RED, C_PURPLE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🗑  UNINSTALL  APPLICATIONS",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Browse · Search · Detect bloatware · Uninstall",
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
                Height = 46,
                BackColor = Color.FromArgb(18, 22, 30)
            };
            statsBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0,
                        statsBar.Height - 1, statsBar.Width, statsBar.Height - 1);
            };

            lblAppCount = MakeStat("0 apps", C_BLUE, new Point(14, 13));
            lblTotalSize = MakeStat("0 MB installed", C_AMBER, new Point(130, 13));
            lblBloatCount = MakeStat("0 bloatware", C_RED, new Point(300, 13));

            loadBar = new System.Windows.Forms.ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Size = new Size(160, 8),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            statsBar.Controls.AddRange(new Control[]
                { lblAppCount, lblTotalSize, lblBloatCount, loadBar });
            statsBar.Resize += (s, e) =>
                loadBar.Location = new Point(statsBar.Width - 176, 18);

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
                Size = new Size(240, 26),
                Text = "Search applications..."
            };
            txtSearch.GotFocus += (s, e) =>
            { if (txtSearch.Text == "Search applications...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) =>
            { if (txtSearch.Text == "") txtSearch.Text = "Search applications..."; };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            chkBloatOnly = new CheckBox
            {
                Text = "⚠  Bloatware only",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_AMBER,
                AutoSize = true,
                Location = new Point(264, 12),
                BackColor = Color.Transparent
            };
            chkBloatOnly.CheckedChanged += (s, e) => ApplyFilter();

            btnLoad = MakeBtn("↺  Reload List", C_BLUE, new Size(130, 28));
            btnLoad.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoad.Click += (s, e) => LoadApps();

            btnOpenPrograms = MakeBtn("🖥  Control Panel", C_SUB, new Size(140, 28));
            btnOpenPrograms.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenPrograms.Click += (s, e) =>
                Process.Start("appwiz.cpl");

            filterBar.Controls.AddRange(new Control[]
                { txtSearch, chkBloatOnly, btnLoad, btnOpenPrograms });
            filterBar.Resize += (s, e) =>
            {
                btnLoad.Location = new Point(filterBar.Width - 148, 8);
                btnOpenPrograms.Location = new Point(filterBar.Width - 300, 8);
            };

            // ── App list ──────────────────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
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
                MultiSelect = true
            };
            appList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            appList.Columns.Add("⚠", 22);
            appList.Columns.Add("Application", 380);
            appList.Columns.Add("Version", 140);
            appList.Columns.Add("Publisher", 220);
            appList.Columns.Add("Install Date", 205);
            appList.Columns.Add("Size", 142);

            appList.DrawColumnHeader += DrawAppHeader;
            appList.DrawItem += (s, e) => { };
            appList.DrawSubItem += DrawAppRow;
            appList.ColumnClick += AppList_ColumnClick;

            listPanel.Controls.Add(appList);
            listPanel.Resize += (s, e) =>
                appList.Size = new Size(listPanel.Width - 20, listPanel.Height - 12);

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                BackColor = C_BG,
                Padding = new Padding(10, 4, 10, 6)
            };
            logPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, logPanel.Width, 0);
            };

            var lblLog = new Label
            {
                Text = "ACTION LOG",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 4)
            };

            logList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8f),
                Location = new Point(0, 22),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            logList.Columns.Add("Time", 135);
            logList.Columns.Add("App", 430);
            logList.Columns.Add("Action", 280);
            logList.Columns.Add("Result", 281);

            logList.DrawColumnHeader += DrawLogHeader;
            logList.DrawItem += (s2, e2) => { };
            logList.DrawSubItem += DrawLogRow;

            logPanel.Controls.AddRange(new Control[] { lblLog, logList });
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 20, logPanel.Height - 26);

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
                Text = "Ready.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnUninstall = MakeBtn("🗑  Uninstall Selected", C_RED, new Size(185, 34));
            btnUninstallBloat = MakeBtn("⚠  Remove All Bloatware", C_AMBER, new Size(190, 34));
            btnClearLog = MakeBtn("🗑  Clear Log", C_SUB, new Size(110, 34));

            btnUninstall.Click += BtnUninstall_Click;
            btnUninstallBloat.Click += BtnUninstallBloat_Click;
            btnClearLog.Click += (s, e) => logList.Items.Clear();

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnUninstall, btnUninstallBloat, btnClearLog });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnUninstall.Location = new Point(16, y);
                btnUninstallBloat.Location = new Point(213, y);
                btnClearLog.Location = new Point(415, y);
                lblStatus.Location = new Point(540,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(listPanel);
            Controls.Add(filterBar);
            Controls.Add(statsBar);
            Controls.Add(topBar);
            Controls.Add(logPanel);
            Controls.Add(bottomBar);
        }

        // ════════════════════════════════════════════════════════════
        //  LOAD APPS VIA REGISTRY / WMI
        // ════════════════════════════════════════════════════════════
        void LoadApps()
        {
            if (loading) return;
            loading = true;
            loadBar.Visible = true;
            lblStatus.Text = "Loading installed applications...";
            appList.Items.Clear();
            allApps.Clear();

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var found = new List<AppInfo>();

                // ── 32-bit registry ───────────────────────────────────
                ScanRegistry(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    Microsoft.Win32.RegistryView.Registry64, found);

                // ── 64-bit registry ───────────────────────────────────
                ScanRegistry(
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                    Microsoft.Win32.RegistryView.Registry64, found);

                // De-duplicate by name
                var unique = found
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name)
                             && !string.IsNullOrWhiteSpace(a.UninstallCmd))
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(a => a.Name)
                    .ToList();

                Invoke(new Action(() => OnAppsLoaded(unique)));
            });
        }

        void ScanRegistry(string path,
            Microsoft.Win32.RegistryView view,
            List<AppInfo> list)
        {
            try
            {
                using (var base64 = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine, view))
                using (var key = base64.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (var sk = key.OpenSubKey(sub))
                            {
                                if (sk == null) continue;
                                string name = sk.GetValue("DisplayName") as string ?? "";
                                string ver = sk.GetValue("DisplayVersion") as string ?? "";
                                string pub = sk.GetValue("Publisher") as string ?? "";
                                string date = sk.GetValue("InstallDate") as string ?? "";
                                string ucmd = sk.GetValue("UninstallString") as string ?? "";
                                object szObj = sk.GetValue("EstimatedSize");
                                long szKB = szObj != null ? Convert.ToInt64(szObj) : 0;

                                // Format date
                                if (date.Length == 8)
                                    try
                                    {
                                        date = DateTime.ParseExact(date, "yyyyMMdd",
                                        null).ToString("dd/MM/yyyy");
                                    }
                                    catch { }

                                // Format size
                                string szStr = szKB > 0
                                    ? (szKB > 1024
                                        ? string.Format("{0:0.0} MB", szKB / 1024.0)
                                        : szKB + " KB")
                                    : "–";

                                // Bloat check
                                bool isBloat = BloatKeywords.Any(kw =>
                                    name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);

                                if (!string.IsNullOrWhiteSpace(name))
                                    list.Add(new AppInfo
                                    {
                                        Name = name,
                                        Version = ver,
                                        Publisher = pub,
                                        InstallDate = date,
                                        EstimatedSize = szStr,
                                        UninstallCmd = ucmd,
                                        RegistryKey = sub,
                                        IsBloat = isBloat,
                                        SizeKB = szKB
                                    });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        void OnAppsLoaded(List<AppInfo> apps)
        {
            allApps = apps;
            loading = false;
            loadBar.Visible = false;

            long totalMB = allApps.Sum(a => a.SizeKB) / 1024;
            int bloatCount = allApps.Count(a => a.IsBloat);

            lblAppCount.Text = string.Format("{0} apps", allApps.Count);
            lblTotalSize.Text = string.Format("{0:0} MB installed", totalMB);
            lblBloatCount.Text = string.Format("{0} bloatware", bloatCount);
            lblBloatCount.ForeColor = bloatCount > 0 ? C_AMBER : C_GREEN;

            ApplyFilter();
            SetStatus(string.Format("Loaded {0} applications.", allApps.Count));
        }

        // ════════════════════════════════════════════════════════════
        //  FILTER
        // ════════════════════════════════════════════════════════════
        void ApplyFilter()
        {
            string s = txtSearch.Text.Trim().ToLower();
            if (s == "search applications...") s = "";

            viewApps = allApps.Where(a =>
            {
                if (chkBloatOnly.Checked && !a.IsBloat) return false;
                if (!string.IsNullOrEmpty(s) &&
                    !a.Name.ToLower().Contains(s) &&
                    !a.Publisher.ToLower().Contains(s)) return false;
                return true;
            }).ToList();

            appList.BeginUpdate();
            appList.Items.Clear();
            foreach (var a in viewApps)
            {
                var item = new ListViewItem(a.IsBloat ? "⚠" : "");
                item.SubItems.Add(a.Name);
                item.SubItems.Add(a.Version);
                item.SubItems.Add(a.Publisher);
                item.SubItems.Add(a.InstallDate);
                item.SubItems.Add(a.EstimatedSize);
                item.Tag = a;
                item.ForeColor = a.IsBloat ? C_AMBER : C_TXT;
                appList.Items.Add(item);
            }
            appList.EndUpdate();

            SetStatus(string.Format("Showing {0} of {1} apps",
                viewApps.Count, allApps.Count));
        }

        // ════════════════════════════════════════════════════════════
        //  UNINSTALL SELECTED
        // ════════════════════════════════════════════════════════════
        void BtnUninstall_Click(object sender, EventArgs e)
        {
            if (appList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Select one or more apps from the list first.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = appList.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as AppInfo)
                .Where(a => a != null).ToList();

            string preview = string.Join("\n",
                selected.Select(a => "  • " + a.Name));

            var confirm = MessageBox.Show(
                string.Format("Uninstall {0} application(s)?\n\n{1}\n\n" +
                    "⚠ This cannot be undone.\nContinue?",
                    selected.Count, preview),
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            foreach (var app in selected)
                RunUninstall(app);
        }

        // ════════════════════════════════════════════════════════════
        //  REMOVE ALL BLOATWARE
        // ════════════════════════════════════════════════════════════
        void BtnUninstallBloat_Click(object sender, EventArgs e)
        {
            var bloat = allApps.Where(a => a.IsBloat).ToList();
            if (bloat.Count == 0)
            {
                MessageBox.Show("No known bloatware found on this system!",
                    "Clean System", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string preview = string.Join("\n",
                bloat.Take(12).Select(a => "  • " + a.Name));
            if (bloat.Count > 12)
                preview += string.Format("\n  ... and {0} more", bloat.Count - 12);

            var confirm = MessageBox.Show(
                string.Format(
                    "Remove {0} bloatware application(s)?\n\n{1}\n\n" +
                    "⚠ This cannot be undone. Continue?",
                    bloat.Count, preview),
                "Remove All Bloatware",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            foreach (var app in bloat)
                RunUninstall(app);
        }

        // ════════════════════════════════════════════════════════════
        //  RUN UNINSTALL COMMAND
        // ════════════════════════════════════════════════════════════
        void RunUninstall(AppInfo app)
        {
            try
            {
                string cmd = app.UninstallCmd.Trim();

                // Silent flags
                string exe = cmd;
                string args = "/S /SILENT /VERYSILENT /NORESTART /quiet /passive";

                // MsiExec
                if (cmd.ToLower().Contains("msiexec"))
                {
                    exe = "msiexec.exe";
                    args = cmd.Replace("msiexec.exe", "")
                              .Replace("MsiExec.exe", "")
                              .Replace("msiexec", "")
                              .Trim() + " /quiet /norestart";
                }
                else if (cmd.StartsWith("\""))
                {
                    int end = cmd.IndexOf('"', 1);
                    if (end > 1)
                    {
                        exe = cmd.Substring(1, end - 1);
                        args = cmd.Substring(end + 1).Trim()
                             + " /S /SILENT /VERYSILENT /NORESTART";
                    }
                }
                else
                {
                    int sp = cmd.IndexOf(' ');
                    if (sp > 0)
                    {
                        exe = cmd.Substring(0, sp);
                        args = cmd.Substring(sp + 1)
                             + " /S /SILENT /VERYSILENT /NORESTART";
                    }
                }

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    },
                    EnableRaisingEvents = true
                };

                proc.Exited += (s, ev) =>
                {
                    bool ok = proc.ExitCode == 0 || proc.ExitCode == 3010;
                    Invoke(new Action(() =>
                    {
                        AddLog(app.Name, "Uninstall",
                            ok ? "✔  Uninstalled successfully"
                               : string.Format("⚠  Exit code {0} — may need manual removal",
                                   proc.ExitCode),
                            ok ? C_GREEN : C_AMBER);
                        SetStatus(ok
                            ? string.Format("✔  {0} uninstalled.", app.Name)
                            : string.Format("⚠  {0} may need manual removal.", app.Name));
                        LoadApps();
                    }));
                };

                proc.Start();
                AddLog(app.Name, "Started", "Running uninstaller...", C_BLUE);
                System.Threading.ThreadPool.QueueUserWorkItem(_ => proc.WaitForExit(120000));
            }
            catch (Exception ex)
            {
                AddLog(app.Name, "Error", ex.Message, C_RED);
                SetStatus("Error: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SORT
        // ════════════════════════════════════════════════════════════
        int sortCol = 1;
        bool sortAsc = true;

        void AppList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortCol == e.Column) sortAsc = !sortAsc;
            else { sortCol = e.Column; sortAsc = true; }

            switch (e.Column)
            {
                case 1:
                    viewApps = sortAsc
                    ? viewApps.OrderBy(a => a.Name).ToList()
                    : viewApps.OrderByDescending(a => a.Name).ToList(); break;
                case 2:
                    viewApps = sortAsc
                    ? viewApps.OrderBy(a => a.Version).ToList()
                    : viewApps.OrderByDescending(a => a.Version).ToList(); break;
                case 3:
                    viewApps = sortAsc
                    ? viewApps.OrderBy(a => a.Publisher).ToList()
                    : viewApps.OrderByDescending(a => a.Publisher).ToList(); break;
                case 5:
                    viewApps = sortAsc
                    ? viewApps.OrderBy(a => a.SizeKB).ToList()
                    : viewApps.OrderByDescending(a => a.SizeKB).ToList(); break;
            }

            appList.BeginUpdate();
            appList.Items.Clear();
            foreach (var a in viewApps)
            {
                var item = new ListViewItem(a.IsBloat ? "⚠" : "");
                item.SubItems.Add(a.Name);
                item.SubItems.Add(a.Version);
                item.SubItems.Add(a.Publisher);
                item.SubItems.Add(a.InstallDate);
                item.SubItems.Add(a.EstimatedSize);
                item.Tag = a;
                item.ForeColor = a.IsBloat ? C_AMBER : C_TXT;
                appList.Items.Add(item);
            }
            appList.EndUpdate();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AddLog(string app, string action, string result, Color fg)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(app, action, result, fg))); return; }
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(app);
            item.SubItems.Add(action);
            item.SubItems.Add(result);
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
                                : msg.StartsWith("Error") ? C_RED : C_SUB;
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

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — app list
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
            var app = e.Item.Tag as AppInfo;
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : app != null && app.IsBloat
                    ? Color.FromArgb(22, 255, 163, 72)
                    : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);

            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            Color fg = e.ColumnIndex == 0
                ? (app != null && app.IsBloat ? C_AMBER : C_BORDER)
                : e.ColumnIndex == 1 ? (app != null && app.IsBloat ? C_AMBER : C_TXT)
                : e.ColumnIndex == 3 ? C_BLUE
                : e.ColumnIndex == 5 ? C_GREEN
                : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 0 || e.ColumnIndex == 1
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

        void DrawLogHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(24, 30, 38)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 7.5f))
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

        void DrawLogRow(object sender, DrawListViewSubItemEventArgs e)
        {
            string tag = e.Item.Tag as string ?? "";
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1 ? C_TXT
                     : e.ColumnIndex == 2 ? C_PURPLE
                     : (tag == "ok" ? C_GREEN
                      : tag == "fail" ? C_RED : C_AMBER);

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br, rc, sf);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
        }
    }
}