using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using WUApiLib;

namespace Tech_ToolKit_Pro
{
    public partial class FormShowUpdate : Form
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
        //  UPDATE DATA MODEL
        // ════════════════════════════════════════════════════════════
        class UpdateItem
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string KBArticle { get; set; }
            public string Category { get; set; }
            public string Size { get; set; }
            public long SizeBytes { get; set; }
            public bool IsMandatory { get; set; }
            public bool NeedsReboot { get; set; }
            public string Severity { get; set; }
            public Color SevColor { get; set; }
            public IUpdate WuUpdate { get; set; }   // raw WU object
            public bool Selected { get; set; } = true;
            public string InstallStatus { get; set; } = "Pending";
            public Color StatusColor { get; set; }
            // UI refs
            public ListViewItem LvItem { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, statsBar, filterBar, listPanel,
                        logPanel, bottomBar, progPanel;
        Label lblTitle, lblStatus;
        Label lblTotal, lblCritical, lblSize, lblSelected;
        Label lblProgText, lblProgPct, lblProgSub;
        UpdateProgressBar progBar;
        ListView updateList, logList;
        Button btnSearch, btnSelectAll, btnSelectNone,
                        btnInstall, btnInstallSelected, btnCancel,
                        btnOpenWU;
        CheckBox chkCritOnly;
        TextBox txtSearch;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime installStart;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<UpdateItem> allUpdates = new List<UpdateItem>();
        List<UpdateItem> viewUpdates = new List<UpdateItem>();
        BackgroundWorker searchWorker;
        BackgroundWorker installWorker;
        bool searching = false;
        bool installing = false;
        int installIndex = 0;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormShowUpdate()
        {
            BuildUI();
            SetupWorkers();
            
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Windows Updates";
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
                Text = "🔄  WINDOWS  UPDATE  CENTER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Search · Select · Install available Windows updates",
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
                    Color.FromArgb(12, C_BLUE.R, C_BLUE.G, C_BLUE.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        statsBar.Width, statsBar.Height);
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, statsBar.Height - 1,
                        statsBar.Width, statsBar.Height - 1);
            };

            lblTotal = MakeStat("0 updates", C_BLUE, new Point(16, 14));
            lblCritical = MakeStat("0 critical", C_RED, new Point(130, 14));
            lblSize = MakeStat("0 MB total", C_AMBER, new Point(260, 14));
            lblSelected = MakeStat("0 selected", C_GREEN, new Point(380, 14));

            statsBar.Controls.AddRange(new Control[]
                { lblTotal, lblCritical, lblSize, lblSelected });

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
                Text = "Filter updates..."
            };
            txtSearch.GotFocus += (s, e) =>
            { if (txtSearch.Text == "Filter updates...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) =>
            { if (txtSearch.Text == "") txtSearch.Text = "Filter updates..."; };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            chkCritOnly = new CheckBox
            {
                Text = "🔴 Critical only",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_RED,
                AutoSize = true,
                Location = new Point(244, 12),
                BackColor = Color.Transparent
            };
            chkCritOnly.CheckedChanged += (s, e) => ApplyFilter();

            btnSelectAll = MakeSmallBtn("☑ All", C_BLUE, new Size(65, 26));
            btnSelectNone = MakeSmallBtn("☐ None", C_SUB, new Size(65, 26));
            btnSelectAll.Click += (s, e) => ToggleAll(true);
            btnSelectNone.Click += (s, e) => ToggleAll(false);

            filterBar.Controls.AddRange(new Control[]
                { txtSearch, chkCritOnly, btnSelectAll, btnSelectNone });
            filterBar.Resize += (s, e) =>
            {
                btnSelectAll.Location = new Point(filterBar.Width - 148, 9);
                btnSelectNone.Location = new Point(filterBar.Width - 76, 9);
            };

            // ── Progress panel ────────────────────────────────────────
            progPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
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
                Text = "Installing updates...",
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

            progBar = new UpdateProgressBar(C_BLUE, C_GREEN)
            {
                Location = new Point(16, 30),
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
                Location = new Point(16, 54)
            };

            progPanel.Controls.AddRange(new Control[]
                { lblProgText, lblProgPct, progBar, lblProgSub });
            progPanel.Resize += (s, e) =>
            {
                progBar.Size = new Size(progPanel.Width - 80, 16);
                lblProgPct.Location = new Point(progPanel.Width - 54, 28);
            };

            // ── Update list ───────────────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            updateList = new ListView
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
            updateList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                              | AnchorStyles.Left | AnchorStyles.Right;

            updateList.Columns.Add("", 24);   // severity dot
            updateList.Columns.Add("Update Title", 320);
            updateList.Columns.Add("Category", 230);
            updateList.Columns.Add("KB Article", 150);
            updateList.Columns.Add("Size", 140);
            updateList.Columns.Add("Severity", 140);
            updateList.Columns.Add("Status", 122);

            updateList.DrawColumnHeader += DrawUpdateHeader;
            updateList.DrawItem += (s, e) => { };
            updateList.DrawSubItem += DrawUpdateRow;
            updateList.ItemChecked += (s, e) => UpdateSelectedCount();

            listPanel.Controls.Add(updateList);
            listPanel.Resize += (s, e) =>
                updateList.Size = new Size(listPanel.Width - 20, listPanel.Height - 12);

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 130,
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
                Text = "INSTALL LOG",
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

            logList.Columns.Add("Time", 166);
            logList.Columns.Add("Update", 490);
            logList.Columns.Add("Action", 250);
            logList.Columns.Add("Result", 220);

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
                Text = "Click 'Search Updates' to check for available updates.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnSearch = MakeBtn("🔍  Search Updates", C_BLUE, new Size(175, 34));
            btnInstall = MakeBtn("⬇  Install All", C_GREEN, new Size(145, 34));
            btnInstallSelected = MakeBtn("☑  Install Selected", C_TEAL, new Size(165, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnOpenWU = MakeBtn("🔄  Open Windows Update", C_PURPLE, new Size(185, 34));

            btnInstall.Enabled = false;
            btnInstallSelected.Enabled = false;
            btnCancel.Enabled = false;

            btnSearch.Click += BtnSearch_Click;
            btnInstall.Click += (s, e) => StartInstall(false);
            btnInstallSelected.Click += (s, e) => StartInstall(true);
            btnCancel.Click += BtnCancel_Click;
            btnOpenWU.Click += (s, e) =>
                System.Diagnostics.Process.Start("ms-settings:windowsupdate");

            bottomBar.Controls.AddRange(new Control[]
            {
                lblStatus, btnSearch, btnInstall,
                btnInstallSelected, btnCancel, btnOpenWU
            });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnSearch.Location = new Point(16, y);
                btnInstall.Location = new Point(203, y);
                btnInstallSelected.Location = new Point(360, y);
                btnCancel.Location = new Point(537, y);
                btnOpenWU.Location = new Point(649, y);
                lblStatus.Location = new Point(850,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(listPanel);
            Controls.Add(progPanel);
            Controls.Add(filterBar);
            Controls.Add(statsBar);
            Controls.Add(topBar);
            Controls.Add(logPanel);
            Controls.Add(bottomBar);

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - installStart;
                lblProgSub.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  SETUP WORKERS
        // ════════════════════════════════════════════════════════════
        void SetupWorkers()
        {
            searchWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            searchWorker.DoWork += SearchWorker_DoWork;
            searchWorker.ProgressChanged += SearchWorker_Progress;
            searchWorker.RunWorkerCompleted += SearchWorker_Completed;

            installWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            installWorker.DoWork += InstallWorker_DoWork;
            installWorker.ProgressChanged += InstallWorker_Progress;
            installWorker.RunWorkerCompleted += InstallWorker_Completed;
        }

        // ════════════════════════════════════════════════════════════
        //  SEARCH FOR UPDATES
        // ════════════════════════════════════════════════════════════
        void BtnSearch_Click(object sender, EventArgs e)
        {
            if (searching || installing) return;

            allUpdates.Clear();
            viewUpdates.Clear();
            updateList.Items.Clear();
            logList.Items.Clear();

            searching = true;
            btnSearch.Enabled = false;
            btnInstall.Enabled = false;
            btnInstallSelected.Enabled = false;
            progPanel.Visible = true;

            progBar.Animate = true;
            progBar.Value = 0;
            progBar.Invalidate();
            lblProgText.Text = "Searching for available updates...";
            lblProgPct.Text = "...";
            lblProgSub.Text = "Contacting Windows Update servers";

            SetStatus("Searching for updates...");
            ResetStats();
            AddLog("Search", "Checking for available Windows updates...", C_BLUE);

            searchWorker.RunWorkerAsync();
        }

        // ════════════════════════════════════════════════════════════
        //  SEARCH WORKER
        // ════════════════════════════════════════════════════════════
        void SearchWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var found = new List<UpdateItem>();

            try
            {
                searchWorker.ReportProgress(10,
                    new object[] { "status", "Initializing Windows Update session..." });

                var session = new UpdateSession();
                var searcher = session.CreateUpdateSearcher();
                searcher.Online = true;

                searchWorker.ReportProgress(20,
                    new object[] { "status", "Querying Windows Update servers..." });

                ISearchResult result = searcher.Search(
                    "IsInstalled=0 and Type='Software'");

                int count = result.Updates.Count;
                searchWorker.ReportProgress(50,
                    new object[] { "status", string.Format("Found {0} update(s). Loading details...", count) });

                for (int i = 0; i < count; i++)
                {
                    if (searchWorker.CancellationPending)
                    { e.Cancel = true; return; }

                    IUpdate upd = result.Updates[i];

                    string kb = "";
                    string cat = "";
                    long szBytes = 0;

                    try
                    {
                        if (upd.KBArticleIDs != null && upd.KBArticleIDs.Count > 0)
                            kb = "KB" + upd.KBArticleIDs[0];
                        if (upd.Categories != null && upd.Categories.Count > 0)
                            cat = upd.Categories[0].Name;
                        if (upd.MaxDownloadSize > 0)
                            szBytes = (long)upd.MaxDownloadSize;
                    }
                    catch { }

                    // Severity mapping
                    string sev = "Optional";
                    Color sevC = C_SUB;
                    try
                    {
                        string ms = upd.MsrcSeverity ?? "";
                        if (ms == "Critical") { sev = "Critical"; sevC = C_RED; }
                        else if (ms == "Important") { sev = "Important"; sevC = C_AMBER; }
                        else if (ms == "Moderate") { sev = "Moderate"; sevC = C_BLUE; }
                        else if (upd.IsMandatory) { sev = "Required"; sevC = C_AMBER; }
                    }
                    catch { }

                    found.Add(new UpdateItem
                    {
                        Title = upd.Title ?? "(Untitled update)",
                        Description = upd.Description ?? "",
                        KBArticle = kb,
                        Category = cat,
                        Size = szBytes > 0 ? FormatSize(szBytes) : "–",
                        SizeBytes = szBytes,
                        IsMandatory = upd.IsMandatory,
                        NeedsReboot = false,
                        Severity = sev,
                        SevColor = sevC,
                        WuUpdate = upd,
                        Selected = true,
                        InstallStatus = "Pending",
                        StatusColor = C_SUB
                    });

                    int pct = 50 + (int)((double)(i + 1) / count * 50);
                    searchWorker.ReportProgress(pct,
                        new object[]
                        {
                            "progress",
                            string.Format("Loading: {0}", upd.Title?.Length > 55
                                ? upd.Title.Substring(0, 55) + "…" : upd.Title)
                        });
                }
            }
            catch (Exception ex)
            {
                e.Result = new object[] { "error", ex.Message };
                return;
            }

            e.Result = new object[] { "ok", found };
        }

        void SearchWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string;
            string msg = d[1] as string;

            progBar.Value = e.ProgressPercentage;
            lblProgPct.Text = e.ProgressPercentage + "%";
            lblProgSub.Text = msg;
        }

        void SearchWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            searching = false;
            progBar.Animate = false;
            btnSearch.Enabled = true;

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Search cancelled.");
                AddLog("Search", "Cancelled by user.", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null) return;

            if ((string)res[0] == "error")
            {
                string err = res[1] as string;
                progPanel.Visible = false;
                SetStatus("Search failed: " + err);
                AddLog("Error", err, C_RED);

                MessageBox.Show(
                    "Could not search for updates:\n\n" + err +
                    "\n\nMake sure Windows Update service is running\n" +
                    "and you have internet access.",
                    "Search Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            allUpdates = res[1] as List<UpdateItem>;

            if (allUpdates.Count == 0)
            {
                progBar.Value = 100;
                lblProgPct.Text = "100%";
                lblProgText.Text = "✔  Your system is up to date!";
                lblProgSub.Text = "No updates available.";
                progBar.SetColors(C_GREEN, C_GREEN);
                progBar.Invalidate();
                SetStatus("✔  System is up to date. No updates available.");
                AddLog("Search", "No updates found — system is up to date.", C_GREEN);
                UpdateStats();
                return;
            }

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.Animate = false;
            progBar.SetColors(C_BLUE, C_GREEN);
            progBar.Invalidate();

            ApplyFilter();
            UpdateStats();

            int crit = allUpdates.Count(u => u.Severity == "Critical");
            lblProgText.Text = string.Format(
                "✔  Found {0} update(s) — {1} critical",
                allUpdates.Count, crit);
            lblProgSub.Text = string.Format(
                "Total size: {0}",
                FormatSize(allUpdates.Sum(u => u.SizeBytes)));

            btnInstall.Enabled = allUpdates.Count > 0;
            btnInstallSelected.Enabled = allUpdates.Count > 0;

            SetStatus(string.Format(
                "Found {0} update(s) — {1} critical. Ready to install.",
                allUpdates.Count, crit));
            AddLog("Search", string.Format(
                "Found {0} update(s), {1} critical, total {2}",
                allUpdates.Count, crit,
                FormatSize(allUpdates.Sum(u => u.SizeBytes))), C_BLUE);
        }

        // ════════════════════════════════════════════════════════════
        //  START INSTALL
        // ════════════════════════════════════════════════════════════
        void StartInstall(bool selectedOnly)
        {
            if (installing || searching) return;

            List<UpdateItem> toInstall = selectedOnly
                ? allUpdates.Where(u =>
                    u.InstallStatus == "Pending" && u.Selected).ToList()
                : allUpdates.Where(u =>
                    u.InstallStatus == "Pending").ToList();

            if (toInstall.Count == 0)
            {
                MessageBox.Show(
                    selectedOnly
                        ? "No selected pending updates to install.\nCheck the checkboxes in the list."
                        : "No pending updates to install.",
                    "Nothing to Install",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool needsReboot = toInstall.Any(u => u.NeedsReboot);
            string preview = string.Join("\n",
                toInstall.Take(8).Select(u =>
                    "  • " + (u.Title.Length > 60
                        ? u.Title.Substring(0, 60) + "…" : u.Title)));
            if (toInstall.Count > 8)
                preview += string.Format("\n  ... and {0} more", toInstall.Count - 8);

            var confirm = MessageBox.Show(
                string.Format(
                    "Install {0} update(s)?\n\n{1}\n\n" +
                    "Total size: {2}\n" +
                    (needsReboot ? "⚠  Some updates require a reboot.\n" : "") +
                    "\nAdministrator rights required.\nContinue?",
                    toInstall.Count, preview,
                    FormatSize(toInstall.Sum(u => u.SizeBytes))),
                "Confirm Install",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            installing = true;
            installIndex = 0;
            installStart = DateTime.Now;

            btnInstall.Enabled = false;
            btnInstallSelected.Enabled = false;
            btnSearch.Enabled = false;
            btnCancel.Enabled = true;
            progPanel.Visible = true;
            progBar.SetColors(C_BLUE, C_GREEN);
            progBar.Value = 0;
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "Installing {0} update(s)...", toInstall.Count);
            lblProgPct.Text = "0%";
            lblProgSub.Text = "Preparing...";

            elapsedTimer.Start();
            SetStatus("Installing updates...");
            AddLog("Install", string.Format(
                "Starting installation of {0} update(s)", toInstall.Count), C_BLUE);

            installWorker.RunWorkerAsync(toInstall);
        }

        // ════════════════════════════════════════════════════════════
        //  INSTALL WORKER
        // ════════════════════════════════════════════════════════════
        void InstallWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var toInstall = e.Argument as List<UpdateItem>;
            if (toInstall == null || toInstall.Count == 0) return;

            int total = toInstall.Count;
            int done = 0;

            installWorker.ReportProgress(0,
                new object[] { "status", "Initializing installer session..." });

            try
            {
                var session = new UpdateSession();
                var installer = session.CreateUpdateInstaller();

                for (int i = 0; i < total; i++)
                {
                    if (installWorker.CancellationPending)
                    { e.Cancel = true; return; }

                    var item = toInstall[i];

                    installWorker.ReportProgress(
                        (int)((double)i / total * 100),
                        new object[]
                        {
                            "start", i, total,
                            item.Title.Length > 60
                                ? item.Title.Substring(0, 60) + "…"
                                : item.Title
                        });

                    try
                    {
                        // Download first
                        var toDownload = new UpdateCollection();
                        toDownload.Add(item.WuUpdate);

                        var downloader = session.CreateUpdateDownloader();
                        downloader.Updates = toDownload;

                        installWorker.ReportProgress(
                            (int)((double)i / total * 100),
                            new object[] { "downloading", i, item.Title });

                        var dlResult = downloader.Download();

                        // Install
                        var toInst = new UpdateCollection();
                        toInst.Add(item.WuUpdate);

                        installer.Updates = toInst;

                        installWorker.ReportProgress(
                            (int)((double)i / total * 100),
                            new object[] { "installing", i, item.Title });

                        IInstallationResult instResult = installer.Install();
                        done++;

                        bool ok = instResult.ResultCode == OperationResultCode.orcSucceeded
                                     || instResult.ResultCode == OperationResultCode.orcSucceededWithErrors;
                        bool reboot = instResult.RebootRequired;
                        int pct = (int)((double)done / total * 100);

                        installWorker.ReportProgress(pct,
                            new object[]
                            {
                                "done", i, ok, reboot,
                                item.Title,
                                (int)instResult.ResultCode
                            });
                    }
                    catch (Exception ex)
                    {
                        done++;
                        installWorker.ReportProgress(
                            (int)((double)done / total * 100),
                            new object[]
                            {
                                "error", i, item.Title, ex.Message
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                e.Result = new object[] { "fatal", ex.Message };
                return;
            }

            e.Result = new object[] { "ok", done, total };
        }

        void InstallWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            progBar.Value = e.ProgressPercentage;
            lblProgPct.Text = e.ProgressPercentage + "%";

            if (type == "status")
            {
                lblProgSub.Text = d[1] as string;
            }
            else if (type == "start")
            {
                int i = (int)d[1];
                int tot = (int)d[2];
                string name = d[3] as string;
                lblProgText.Text = string.Format(
                    "Installing update {0} of {1}", i + 1, tot);
                lblProgSub.Text = name;
                AddLog(name, "Started", "Downloading + Installing...", C_BLUE);
            }
            else if (type == "downloading")
            {
                string name = d[2] as string;
                lblProgSub.Text = "Downloading: " + name;
            }
            else if (type == "installing")
            {
                string name = d[2] as string;
                lblProgSub.Text = "Installing: " + name;
            }
            else if (type == "done")
            {
                int idx = (int)d[1];
                bool ok = Convert.ToBoolean(d[2]);
                bool reboot = Convert.ToBoolean(d[3]);
                string name = d[4] as string;
                int code = (int)d[5];

                string result = ok
                    ? (reboot ? "✔  Installed — Reboot required" : "✔  Installed successfully")
                    : string.Format("✖  Failed (code {0})", code);

                Color fc = ok ? C_GREEN : C_RED;

                // Update the list item status
                UpdateItemStatus(name, ok ? "Installed" : "Failed", fc);
                AddLog(name, "Done", result, fc);

                if (ok && reboot)
                    AddLog(name, "⚠ Reboot", "Restart required to complete this update.", C_AMBER);
            }
            else if (type == "error")
            {
                string name = d[2] as string;
                string err = d[3] as string;
                UpdateItemStatus(name, "Error", C_RED);
                AddLog(name, "Error", err, C_RED);
            }
        }

        void InstallWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            installing = false;
            elapsedTimer.Stop();

            btnSearch.Enabled = true;
            btnCancel.Enabled = false;
            btnInstall.Enabled = false;
            btnInstallSelected.Enabled = false;

            var ts = DateTime.Now - installStart;
            string elap = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Installation cancelled.");
                AddLog("Cancelled", "Installation cancelled by user.", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null) return;

            if ((string)res[0] == "fatal")
            {
                string err = res[1] as string;
                progBar.SetColors(C_RED, C_AMBER);
                progBar.Invalidate();
                lblProgText.Text = "✖  Installation failed";
                lblProgSub.Text = err;
                SetStatus("Install failed: " + err);
                AddLog("Fatal Error", err, C_RED);
                MessageBox.Show(
                    "Installation failed:\n\n" + err,
                    "Install Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int done = (int)res[1];
            int total = (int)res[2];

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "✔  Installation complete — {0}/{1} installed", done, total);
            lblProgSub.Text = string.Format("Duration: {0}", elap);

            bool needReboot = allUpdates.Any(u =>
                u.InstallStatus == "Installed" && u.NeedsReboot);

            SetStatus(string.Format(
                "✔  {0}/{1} updates installed in {2}.{3}",
                done, total, elap,
                needReboot ? "  ⚠ Reboot required." : ""));

            AddLog("Complete",
                string.Format("{0}/{1} installed · {2}", done, total, elap), C_GREEN);

            if (needReboot)
            {
                var reboot = MessageBox.Show(
                    "One or more updates require a system restart to complete.\n\n" +
                    "Restart now?",
                    "Restart Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (reboot == DialogResult.Yes)
                    System.Diagnostics.Process.Start("shutdown", "/r /t 30 /c \"Restarting for Windows Updates\"");
            }
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (searching && searchWorker.IsBusy)
                searchWorker.CancelAsync();
            if (installing && installWorker.IsBusy)
                installWorker.CancelAsync();

            btnCancel.Enabled = false;
            SetStatus("Cancelling...");
            AddLog("Cancel", "Cancellation requested.", C_AMBER);
        }

        // ════════════════════════════════════════════════════════════
        //  FILTER
        // ════════════════════════════════════════════════════════════
        void ApplyFilter()
        {
            string s = txtSearch.Text.Trim().ToLower();
            if (s == "filter updates...") s = "";

            viewUpdates = allUpdates.Where(u =>
            {
                if (chkCritOnly.Checked && u.Severity != "Critical") return false;
                if (!string.IsNullOrEmpty(s) &&
                    !u.Title.ToLower().Contains(s) &&
                    !u.KBArticle.ToLower().Contains(s) &&
                    !u.Category.ToLower().Contains(s)) return false;
                return true;
            }).ToList();

            updateList.BeginUpdate();
            updateList.Items.Clear();
            foreach (var u in viewUpdates)
            {
                var item = new ListViewItem("");
                item.Checked = u.Selected;
                item.SubItems.Add(u.Title);
                item.SubItems.Add(u.Category);
                item.SubItems.Add(u.KBArticle);
                item.SubItems.Add(u.Size);
                item.SubItems.Add(u.Severity);
                item.SubItems.Add(u.InstallStatus);
                item.Tag = u;
                item.ForeColor = u.SevColor == C_RED ? C_RED
                               : u.InstallStatus == "Installed" ? C_GREEN
                               : C_TXT;
                u.LvItem = item;
                updateList.Items.Add(item);
            }
            updateList.EndUpdate();

            UpdateSelectedCount();
        }

        void ToggleAll(bool check)
        {
            updateList.ItemChecked -= (s, e) => UpdateSelectedCount();
            foreach (ListViewItem item in updateList.Items)
            {
                item.Checked = check;
                if (item.Tag is UpdateItem u) u.Selected = check;
            }
            updateList.ItemChecked += (s, e) => UpdateSelectedCount();
            UpdateSelectedCount();
        }

        // ════════════════════════════════════════════════════════════
        //  STATS & STATUS HELPERS
        // ════════════════════════════════════════════════════════════
        void UpdateStats()
        {
            int total = allUpdates.Count;
            int crit = allUpdates.Count(u => u.Severity == "Critical");
            long sz = allUpdates.Sum(u => u.SizeBytes);
            int sel = allUpdates.Count(u => u.Selected);

            lblTotal.Text = string.Format("{0} updates", total);
            lblCritical.Text = string.Format("{0} critical", crit);
            lblCritical.ForeColor = crit > 0 ? C_RED : C_SUB;
            lblSize.Text = FormatSize(sz);
            lblSelected.Text = string.Format("{0} selected", sel);
        }

        void ResetStats()
        {
            lblTotal.Text = "0 updates";
            lblCritical.Text = "0 critical";
            lblSize.Text = "0 B";
            lblSelected.Text = "0 selected";
        }

        void UpdateSelectedCount()
        {
            int sel = 0;
            foreach (ListViewItem item in updateList.Items)
            {
                if (item.Checked && item.Tag is UpdateItem u)
                { u.Selected = true; sel++; }
                else if (item.Tag is UpdateItem u2)
                    u2.Selected = false;
            }
            lblSelected.Text = string.Format("{0} selected", sel);
        }

        void UpdateItemStatus(string title, string status, Color c)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => UpdateItemStatus(title, status, c))); return; }

            var upd = allUpdates.FirstOrDefault(u =>
                u.Title.StartsWith(title, StringComparison.OrdinalIgnoreCase));
            if (upd == null) return;

            upd.InstallStatus = status;
            upd.StatusColor = c;

            if (upd.LvItem != null && upd.LvItem.SubItems.Count > 6)
            {
                upd.LvItem.SubItems[6].Text = status;
                upd.LvItem.ForeColor = c;
                // ListViewItem does not have Invalidate(); invalidate the ListView instead
                updateList?.Invalidate();
            }
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("⚠") ? C_AMBER
                                : msg.Contains("fail") || msg.Contains("Error")
                                    ? C_RED : C_SUB;
        }

        void AddLog(string update, string action, Color fg)
        {
            // Backwards-compatible overload for existing 3-argument calls
            AddLog(update, action, "", fg);
        }

        void AddLog(string update, string action, string result, Color fg)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(update, action, result, fg))); return; }

            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(update.Length > 50
                ? update.Substring(0, 50) + "…" : update);
            item.SubItems.Add(action);
            item.SubItems.Add(result);
            item.ForeColor = fg;
            item.Tag = fg == C_GREEN ? "ok"
                           : fg == C_RED ? "fail"
                           : fg == C_AMBER ? "warn" : "info";
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        string FormatSize(long bytes)
        {
            if (bytes <= 0) return "–";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.0} KB", bytes / 1024.0);
            if (bytes < 1024L * 1024 * 1024) return string.Format("{0:0.0} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:0.00} GB", bytes / (1024.0 * 1024 * 1024));
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
        //  OWNER DRAW — update list
        // ════════════════════════════════════════════════════════════
        void DrawUpdateHeader(object sender, DrawListViewColumnHeaderEventArgs e)
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

        void DrawUpdateRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var upd = e.Item.Tag as UpdateItem;

            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : upd != null && upd.Severity == "Critical"
                    ? Color.FromArgb(20, 248, 81, 73)
                    : upd != null && upd.Severity == "Important"
                        ? Color.FromArgb(14, 255, 163, 72)
                        : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);

            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            // Column 0: severity dot
            if (e.ColumnIndex == 0 && upd != null)
            {
                using (var br = new SolidBrush(upd.SevColor))
                    e.Graphics.FillEllipse(br,
                        e.Bounds.X + 6, e.Bounds.Y + (e.Bounds.Height - 8) / 2, 8, 8);
                return;
            }

            Color fg = C_TXT;
            if (e.ColumnIndex == 1)
                fg = upd != null && upd.InstallStatus == "Installed" ? C_GREEN : C_TXT;
            else if (e.ColumnIndex == 2)
                fg = C_BLUE;
            else if (e.ColumnIndex == 3)
                fg = C_SUB;
            else if (e.ColumnIndex == 4)
                fg = C_AMBER;
            else if (e.ColumnIndex == 5 && upd != null)
                fg = upd.SevColor;
            else if (e.ColumnIndex == 6 && upd != null)
                fg = upd.InstallStatus == "Installed" ? C_GREEN
                   : upd.InstallStatus == "Failed" ? C_RED
                   : upd.InstallStatus == "Error" ? C_RED
                   : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 1
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, updateList.Font, br, rc, sf);
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
                      : tag == "fail" ? C_RED
                      : tag == "warn" ? C_AMBER : C_SUB);

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
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (searchWorker != null && searchWorker.IsBusy)
                searchWorker.CancelAsync();
            if (installWorker != null && installWorker.IsBusy)
                installWorker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  UPDATE PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class UpdateProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public UpdateProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bg
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

                    if (fw > 8 && _val > 5)
                    {
                        string txt = _val + "%";
                        using (var ft = new Font("Segoe UI Semibold", 7f))
                        using (var br = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                        {
                            var sz = g.MeasureString(txt, ft);
                            g.DrawString(txt, ft, br,
                                (fw - sz.Width) / 2f,
                                (Height - sz.Height) / 2f);
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