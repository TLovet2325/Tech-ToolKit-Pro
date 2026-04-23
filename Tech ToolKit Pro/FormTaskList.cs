using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormTaskList : Form
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
        //  PROCESS DATA MODEL
        // ════════════════════════════════════════════════════════════
        class ProcInfo
        {
            public int PID { get; set; }
            public string Name { get; set; }
            public long MemMB { get; set; }
            public float CpuPct { get; set; }
            public int Threads { get; set; }
            public string Status { get; set; }
            public bool IsHog { get; set; }  // RAM hog flag
            public bool IsUseless { get; set; }  // useless/killable flag
            public string HogReason { get; set; }  // why flagged
        }

        // ════════════════════════════════════════════════════════════
        //  KNOWN USELESS / BLOAT PROCESSES
        // ════════════════════════════════════════════════════════════
        static readonly HashSet<string> KnownUseless = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "OneDrive","OneDriveSetup","SkypeApp","SkypeBackgroundHost",
            "YourPhone","YourPhoneServer","WinStore.App","SearchApp",
            "Cortana","bingsvc","SearchIndexer","MsMpEng",
            "SgrmBroker","WUDFHost","NisSrv","MpCmdRun",
            "spoolsv","fax","WerFault","wermgr",
            "GameBarPresenceWriter","GameBar","XboxGameBarWidgets",
            "XblGameSave","XboxNetApiSvc","XblAuthManager",
            "DiagTrack","dmwappushservice","WaaSMedicSvc",
            "TiWorker","TrustedInstaller","UsoClient",
            "MusNotifyIcon","WinMailApp","msteams",
            "Teams","slack","discord","Spotify",
            "acrotray","AdobeUpdateService","AdobeIPCBroker",
            "AGSService","AdobeARM","armsvc",
            "iTunesHelper","AppleMobileDeviceService","bonjour",
            "GoogleCrashHandler","GoogleUpdate","GoogleUpdateCore",
            "CCleaner","CCleaner64","CCleanerBrowser",
            "jusched","jucheck","javaws",
            "nvtray","NvContainer","NvDisplay.Container",
            "EpicGamesLauncher","EpicWebHelper","GalaxyClient",
            "upc","uplay_launcher","steam","steamwebhelper",
            "RiotClientServices","RiotClientUx","LeagueClient"
        };

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, statsBar, filterBar, listPanel, logPanel, bottomBar;
        Label lblTitle, lblProcCount, lblTotalRam, lblHogCount;
        Label lblStatus;
        ListView procList, logList;
        Button btnRefresh, btnKillHogs, btnKillSelected,
                 btnKillUseless, btnEndTask, btnClearLog;
        TextBox txtSearch;
        CheckBox chkHogsOnly, chkUselessOnly;
        System.Windows.Forms.Timer autoRefresh;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<ProcInfo> allProcs = new List<ProcInfo>();
        List<ProcInfo> viewProcs = new List<ProcInfo>();
        const long HOG_MB = 200;   // > 200 MB = RAM hog
        const float HOG_CPU = 15f;   // > 15% CPU = hog

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormTaskList()
        {
            BuildUI();
            RefreshProcesses();

            autoRefresh = new System.Windows.Forms.Timer();
            autoRefresh.Interval = 3000;
            autoRefresh.Tick += (s, e) => RefreshProcesses();
            autoRefresh.Start();
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Task List";
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
                Text = "⚠  TASK LIST  &  PROCESS KILLER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Detect RAM hogs · Kill useless processes · Manual End Task",
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
                Height = 52,
                BackColor = Color.FromArgb(18, 22, 30),
                Padding = new Padding(14, 8, 14, 8)
            };
            statsBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, statsBar.Height - 1,
                        statsBar.Width, statsBar.Height - 1);
            };

            lblProcCount = MakeStatLabel("0 processes", C_BLUE, new Point(14, 14));
            lblTotalRam = MakeStatLabel("0 MB in use", C_AMBER, new Point(140, 14));
            lblHogCount = MakeStatLabel("0 hogs", C_RED, new Point(280, 14));

            var lblLive = new Label
            {
                Text = "● AUTO-REFRESH 3s",
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = C_GREEN,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            statsBar.Controls.AddRange(new Control[]
                { lblProcCount, lblTotalRam, lblHogCount, lblLive });
            statsBar.Resize += (s, e) =>
                lblLive.Location = new Point(statsBar.Width - lblLive.Width - 14, 16);

            // ── Filter bar ────────────────────────────────────────────
            filterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = C_SURF,
                Padding = new Padding(10, 6, 10, 6)
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
                Size = new Size(200, 26),
                Text = "Search process..."
            };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "Search process...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) => { if (txtSearch.Text == "") txtSearch.Text = "Search process..."; };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            chkHogsOnly = new CheckBox
            {
                Text = "🔴 RAM Hogs only",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_RED,
                AutoSize = true,
                Location = new Point(222, 12),
                BackColor = Color.Transparent
            };
            chkHogsOnly.CheckedChanged += (s, e) => ApplyFilter();

            chkUselessOnly = new CheckBox
            {
                Text = "⚠ Useless only",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_AMBER,
                AutoSize = true,
                Location = new Point(370, 12),
                BackColor = Color.Transparent
            };
            chkUselessOnly.CheckedChanged += (s, e) => ApplyFilter();

            btnRefresh = MakeBtn("↺  Refresh", C_BLUE, new Size(100, 28));
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += (s, e) => RefreshProcesses();

            filterBar.Controls.AddRange(new Control[]
                { txtSearch, chkHogsOnly, chkUselessOnly, btnRefresh });
            filterBar.Resize += (s, e) =>
                btnRefresh.Location = new Point(filterBar.Width - 116, 8);

            // ── Process list ──────────────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            procList = new ListView
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
            procList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left | AnchorStyles.Right;

            procList.Columns.Add("⚠", 24);
            procList.Columns.Add("Process", 170);
            procList.Columns.Add("PID", 100);
            procList.Columns.Add("RAM (MB)", 150);
            procList.Columns.Add("RAM Bar", 150);
            procList.Columns.Add("CPU %", 115);
            procList.Columns.Add("Threads", 115);
            procList.Columns.Add("Status", 100);
            procList.Columns.Add("Note", 185);

            procList.DrawColumnHeader += DrawProcHeader;
            procList.DrawItem += (s, e) => { };
            procList.DrawSubItem += DrawProcRow;
            procList.ColumnClick += ProcList_ColumnClick;

            listPanel.Controls.Add(procList);
            listPanel.Resize += (s, e) =>
                procList.Size = new Size(listPanel.Width - 20, listPanel.Height - 12);

            // ── Log panel (bottom split) ───────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
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

            logList.Columns.Add("Time", 105);
            logList.Columns.Add("Action", 180);
            logList.Columns.Add("Process", 280);
            logList.Columns.Add("PID", 129);
            logList.Columns.Add("Result", 432);

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

            btnKillHogs = MakeBtn("🔴 Kill RAM Hogs", C_RED, new Size(160, 34));
            btnKillUseless = MakeBtn("⚠ Kill Useless", C_AMBER, new Size(145, 34));
            btnEndTask = MakeBtn("✕  End Task", C_RED, new Size(120, 34));
            btnClearLog = MakeBtn("🗑  Clear Log", C_SUB, new Size(110, 34));

            btnKillHogs.Click += BtnKillHogs_Click;
            btnKillUseless.Click += BtnKillUseless_Click;
            btnEndTask.Click += BtnEndTask_Click;
            btnClearLog.Click += (s, e) => logList.Items.Clear();

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnKillHogs, btnKillUseless, btnEndTask, btnClearLog });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnKillHogs.Location = new Point(16, y);
                btnKillUseless.Location = new Point(188, y);
                btnEndTask.Location = new Point(344, y);
                btnClearLog.Location = new Point(476, y);
                lblStatus.Location = new Point(600,
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
        //  REFRESH PROCESSES
        // ════════════════════════════════════════════════════════════
        void RefreshProcesses()
        {
            try
            {
                var procs = Process.GetProcesses();
                long maxMem = procs.Max(p => { try { return p.WorkingSet64; } catch { return 0; } });

                allProcs = procs
                    .Select(p =>
                    {
                        long mem = 0;
                        int thr = 0;
                        bool res = true;
                        try { mem = p.WorkingSet64 / (1024 * 1024); thr = p.Threads.Count; res = p.Responding; }
                        catch { }

                        bool isHog = mem >= HOG_MB;
                        bool isUseless = KnownUseless.Contains(p.ProcessName);

                        string reason = "";
                        if (isHog && isUseless) reason = "RAM hog + bloatware";
                        else if (isHog) reason = string.Format("Using {0} MB RAM", mem);
                        else if (isUseless) reason = "Known bloatware";
                        else if (!res) reason = "Not responding";

                        return new ProcInfo
                        {
                            PID = p.Id,
                            Name = p.ProcessName,
                            MemMB = mem,
                            CpuPct = 0f,
                            Threads = thr,
                            Status = res ? "Running" : "Not Responding",
                            IsHog = isHog,
                            IsUseless = isUseless,
                            HogReason = reason
                        };
                    })
                    .OrderByDescending(p => p.MemMB)
                    .ToList();

                // Update stats
                long totalMem = allProcs.Sum(p => p.MemMB);
                int hogCount = allProcs.Count(p => p.IsHog);

                lblProcCount.Text = string.Format("{0} processes", allProcs.Count);
                lblTotalRam.Text = string.Format("{0:0} MB in use", totalMem);
                lblHogCount.Text = string.Format("{0} RAM hogs", hogCount);
                lblHogCount.ForeColor = hogCount > 0 ? C_RED : C_GREEN;

                ApplyFilter();
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  APPLY FILTER + POPULATE LIST
        // ════════════════════════════════════════════════════════════
        void ApplyFilter()
        {
            string search = txtSearch.Text.Trim().ToLower();
            if (search == "search process...") search = "";

            viewProcs = allProcs.Where(p =>
            {
                if (chkHogsOnly.Checked && !p.IsHog) return false;
                if (chkUselessOnly.Checked && !p.IsUseless) return false;
                if (!string.IsNullOrEmpty(search) &&
                    !p.Name.ToLower().Contains(search)) return false;
                return true;
            }).ToList();

            long maxMem = viewProcs.Count > 0
                ? Math.Max(viewProcs.Max(p => p.MemMB), 1) : 1;

            procList.BeginUpdate();
            procList.Items.Clear();

            foreach (var p in viewProcs)
            {
                // Flag column
                string flag = "";
                if (p.IsHog && p.IsUseless) flag = "🔴⚠";
                else if (p.IsHog) flag = "🔴";
                else if (p.IsUseless) flag = "⚠";
                else if (p.Status == "Not Responding") flag = "💀";

                var item = new ListViewItem(flag);
                item.SubItems.Add(p.Name);
                item.SubItems.Add(p.PID.ToString());
                item.SubItems.Add(p.MemMB.ToString() + " MB");
                item.SubItems.Add("");  // bar drawn by owner draw
                item.SubItems.Add(p.CpuPct.ToString("0.0") + "%");
                item.SubItems.Add(p.Threads.ToString());
                item.SubItems.Add(p.Status);
                item.SubItems.Add(p.HogReason);

                // Tag: store memPct for bar drawing + flags
                float memPct = (float)p.MemMB / maxMem * 100f;
                item.Tag = new object[] { memPct, p.IsHog, p.IsUseless, p.PID, p.Name };
                item.ForeColor = p.Status == "Not Responding" ? C_RED
                               : p.IsHog && p.IsUseless ? C_RED
                               : p.IsHog ? C_AMBER
                               : p.IsUseless ? Color.FromArgb(255, 200, 80)
                               : C_TXT;
                procList.Items.Add(item);
            }

            procList.EndUpdate();
            SetStatus(string.Format("Showing {0} of {1} processes", viewProcs.Count, allProcs.Count));
        }

        // ════════════════════════════════════════════════════════════
        //  KILL RAM HOGS
        // ════════════════════════════════════════════════════════════
        void BtnKillHogs_Click(object sender, EventArgs e)
        {
            var hogs = allProcs.Where(p => p.IsHog).ToList();
            if (hogs.Count == 0)
            {
                MessageBox.Show("No RAM hogs found! Your system is running clean.",
                    "No Hogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Build list preview (top 10)
            string list = string.Join("\n",
                hogs.Take(10).Select(p =>
                    string.Format("  • {0}  ({1} MB)", p.Name, p.MemMB)));
            if (hogs.Count > 10)
                list += string.Format("\n  ... and {0} more", hogs.Count - 10);

            var confirm = MessageBox.Show(
                string.Format(
                    "Kill {0} RAM hog process(es)?\n\n{1}\n\n" +
                    "⚠ Unsaved work in these apps will be lost.\nContinue?",
                    hogs.Count, list),
                "Kill RAM Hogs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            int killed = 0, failed = 0;
            foreach (var p in hogs)
            {
                bool ok = KillProcess(p.PID, p.Name, "Kill Hog");
                if (ok) killed++; else failed++;
            }

            SetStatus(string.Format("Killed {0} hogs, {1} failed.", killed, failed));
            RefreshProcesses();
        }

        // ════════════════════════════════════════════════════════════
        //  KILL USELESS PROCESSES
        // ════════════════════════════════════════════════════════════
        void BtnKillUseless_Click(object sender, EventArgs e)
        {
            var useless = allProcs.Where(p => p.IsUseless).ToList();
            if (useless.Count == 0)
            {
                MessageBox.Show("No known useless/bloatware processes found.",
                    "Nothing to Kill", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string list = string.Join("\n",
                useless.Take(12).Select(p =>
                    string.Format("  • {0}  ({1} MB)", p.Name, p.MemMB)));
            if (useless.Count > 12)
                list += string.Format("\n  ... and {0} more", useless.Count - 12);

            var confirm = MessageBox.Show(
                string.Format(
                    "Kill {0} useless/bloatware process(es)?\n\n{1}\n\n" +
                    "These are background apps and system bloat.\n" +
                    "Windows will restart some of them automatically.\nContinue?",
                    useless.Count, list),
                "Kill Useless Processes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            int killed = 0, failed = 0;
            foreach (var p in useless)
            {
                bool ok = KillProcess(p.PID, p.Name, "Kill Useless");
                if (ok) killed++; else failed++;
            }

            SetStatus(string.Format("Killed {0} useless processes, {1} failed.", killed, failed));
            RefreshProcesses();
        }

        // ════════════════════════════════════════════════════════════
        //  MANUAL END TASK (selected rows)
        // ════════════════════════════════════════════════════════════
        void BtnEndTask_Click(object sender, EventArgs e)
        {
            if (procList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Select one or more processes in the list first.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = new List<(int pid, string name)>();
            foreach (ListViewItem item in procList.SelectedItems)
            {
                if (item.Tag is object[] tag)
                    selected.Add(((int)tag[3], (string)tag[4]));
            }

            if (selected.Count == 0) return;

            string preview = string.Join("\n",
                selected.Select(x => string.Format("  • {0}  (PID {1})", x.name, x.pid)));

            var confirm = MessageBox.Show(
                string.Format(
                    "End task for {0} process(es)?\n\n{1}\n\n" +
                    "⚠ Unsaved work will be lost.\nContinue?",
                    selected.Count, preview),
                "End Task",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            int killed = 0, failed = 0;
            foreach (var (pid, name) in selected)
            {
                bool ok = KillProcess(pid, name, "End Task");
                if (ok) killed++; else failed++;
            }

            SetStatus(string.Format("End Task: {0} terminated, {1} failed.", killed, failed));
            RefreshProcesses();
        }

        // ════════════════════════════════════════════════════════════
        //  KILL PROCESS CORE
        // ════════════════════════════════════════════════════════════
        bool KillProcess(int pid, string name, string action)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(3000);
                AddLog(action, name, pid, "✔  Terminated successfully", C_GREEN);
                return true;
            }
            catch (Exception ex)
            {
                string msg = ex.Message.Length > 60
                    ? ex.Message.Substring(0, 60) + "..." : ex.Message;
                AddLog(action, name, pid, "✖  " + msg, C_RED);
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SORT ON COLUMN CLICK
        // ════════════════════════════════════════════════════════════
        int lastSortCol = 3; // default sort by RAM
        bool sortAsc = false;

        void ProcList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (lastSortCol == e.Column) sortAsc = !sortAsc;
            else { lastSortCol = e.Column; sortAsc = false; }

            switch (e.Column)
            {
                case 1:
                    viewProcs = sortAsc
                    ? viewProcs.OrderBy(p => p.Name).ToList()
                    : viewProcs.OrderByDescending(p => p.Name).ToList(); break;
                case 2:
                    viewProcs = sortAsc
                    ? viewProcs.OrderBy(p => p.PID).ToList()
                    : viewProcs.OrderByDescending(p => p.PID).ToList(); break;
                case 3:
                    viewProcs = sortAsc
                    ? viewProcs.OrderBy(p => p.MemMB).ToList()
                    : viewProcs.OrderByDescending(p => p.MemMB).ToList(); break;
                case 5:
                    viewProcs = sortAsc
                    ? viewProcs.OrderBy(p => p.CpuPct).ToList()
                    : viewProcs.OrderByDescending(p => p.CpuPct).ToList(); break;
                case 6:
                    viewProcs = sortAsc
                    ? viewProcs.OrderBy(p => p.Threads).ToList()
                    : viewProcs.OrderByDescending(p => p.Threads).ToList(); break;
            }

            long maxMem = viewProcs.Count > 0
                ? Math.Max(viewProcs.Max(p => p.MemMB), 1) : 1;

            procList.BeginUpdate();
            procList.Items.Clear();

            foreach (var p in viewProcs)
            {
                string flag = "";
                if (p.IsHog && p.IsUseless) flag = "🔴⚠";
                else if (p.IsHog) flag = "🔴";
                else if (p.IsUseless) flag = "⚠";
                else if (p.Status == "Not Responding") flag = "💀";

                var item = new ListViewItem(flag);
                item.SubItems.Add(p.Name);
                item.SubItems.Add(p.PID.ToString());
                item.SubItems.Add(p.MemMB.ToString() + " MB");
                item.SubItems.Add("");
                item.SubItems.Add(p.CpuPct.ToString("0.0") + "%");
                item.SubItems.Add(p.Threads.ToString());
                item.SubItems.Add(p.Status);
                item.SubItems.Add(p.HogReason);

                float memPct = (float)p.MemMB / maxMem * 100f;
                item.Tag = new object[] { memPct, p.IsHog, p.IsUseless, p.PID, p.Name };
                item.ForeColor = p.Status == "Not Responding" ? C_RED
                               : p.IsHog && p.IsUseless ? C_RED
                               : p.IsHog ? C_AMBER
                               : p.IsUseless ? Color.FromArgb(255, 200, 80)
                               : C_TXT;
                procList.Items.Add(item);
            }
            procList.EndUpdate();
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — process list header
        // ════════════════════════════════════════════════════════════
        void DrawProcHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
            {
                var rc = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                    e.Bounds.Width - 5, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, ft, br, rc, sf);
            }
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — process list rows
        // ════════════════════════════════════════════════════════════
        void DrawProcRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var tag = e.Item.Tag as object[];

            // Row background
            Color bg;
            if (e.Item.Selected)
                bg = Color.FromArgb(33, 58, 88);
            else if (tag != null && (bool)tag[1] && (bool)tag[2])
                bg = Color.FromArgb(40, 248, 81, 73);   // hog + useless — red tint
            else if (tag != null && (bool)tag[1])
                bg = Color.FromArgb(30, 255, 163, 72);  // hog — amber tint
            else if (tag != null && (bool)tag[2])
                bg = Color.FromArgb(20, 255, 200, 80);  // useless — yellow tint
            else
                bg = e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2;

            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            // Left accent stripe on selected
            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            // Column 4 — draw RAM bar
            if (e.ColumnIndex == 4 && tag != null)
            {
                float pct = (float)tag[0];
                bool isHog = (bool)tag[1];
                Color barCol = pct > 80 ? C_RED
                              : pct > 50 ? C_AMBER
                              : isHog ? C_AMBER : C_BLUE;

                // Track
                var track = new Rectangle(
                    e.Bounds.X + 4, e.Bounds.Y + e.Bounds.Height - 9,
                    e.Bounds.Width - 8, 6);
                using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                    e.Graphics.FillRectangle(br, track);

                int fw = (int)(track.Width * (pct / 100f));
                if (fw > 1)
                {
                    using (var br = new LinearGradientBrush(
                        new Rectangle(track.X, track.Y, Math.Max(fw, 1), track.Height),
                        Color.FromArgb(160, barCol.R, barCol.G, barCol.B), barCol,
                        LinearGradientMode.Horizontal))
                        e.Graphics.FillRectangle(br,
                            new Rectangle(track.X, track.Y, fw, track.Height));
                }

                // Pct text
                using (var sf = new StringFormat
                { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                using (var br = new SolidBrush(C_SUB))
                using (var ft = new Font("Segoe UI", 7.5f))
                {
                    var rc = new Rectangle(
                        e.Bounds.X + 4, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height - 8);
                    e.Graphics.DrawString(string.Format("{0:0}%", pct), ft, br, rc, sf);
                }
                return;
            }

            // Text columns
            Color fg = e.Item.ForeColor;
            if (e.ColumnIndex == 0)
                fg = C_TXT; // flag column always bright
            else if (e.ColumnIndex == 7)
                fg = e.SubItem.Text == "Running" ? C_GREEN : C_RED;
            else if (e.ColumnIndex == 8)
                fg = string.IsNullOrEmpty(e.SubItem.Text) ? C_SUB : C_AMBER;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 1 || e.ColumnIndex == 0
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 4, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, procList.Font, br, rc, sf);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — log header + rows
        // ════════════════════════════════════════════════════════════
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
                     : e.ColumnIndex == 1 ? C_PURPLE
                     : e.ColumnIndex == 2 ? C_TXT
                     : e.ColumnIndex == 3 ? C_SUB
                     : (tag == "ok" ? C_GREEN : tag == "fail" ? C_RED : C_SUB);

            if (e.Item.ForeColor != C_TXT)
                fg = e.ColumnIndex >= 4 ? e.Item.ForeColor : fg;

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

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void AddLog(string action, string name, int pid, string result, Color fg)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(action, name, pid, result, fg))); return; }

            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(action);
            item.SubItems.Add(name);
            item.SubItems.Add(pid.ToString());
            item.SubItems.Add(result);
            item.ForeColor = fg;
            item.Tag = fg == C_GREEN ? "ok" : "fail";
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") || msg.Contains("Killed")
                ? C_GREEN : msg.Contains("failed") ? C_AMBER : C_SUB;
        }

        Label MakeStatLabel(string text, Color c, Point loc)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = c,
                AutoSize = true,
                Location = loc
            };
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (autoRefresh != null) autoRefresh.Stop();
            base.OnFormClosed(e);
        }
    }
}