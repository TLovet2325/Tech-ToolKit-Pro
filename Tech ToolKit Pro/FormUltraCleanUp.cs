using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormUltraCleanUp : Form
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
        //  CLEAN TARGETS
        // ════════════════════════════════════════════════════════════
        class CleanTarget
        {
            public string Label { get; set; }
            public string Path { get; set; }
            public string Desc { get; set; }
            public Color Accent { get; set; }
            public bool IsCleanmgr { get; set; } = false; // special cleanmgr target
            public int Found { get; set; }
            public int Deleted { get; set; }
            public int Failed { get; set; }
            public long Saved { get; set; }
            public CheckBox Chk { get; set; }
            public Label StatLbl { get; set; }
            public GradientProgressBar TargetBar { get; set; }
        }

        List<CleanTarget> targets = new List<CleanTarget>
        {
            new CleanTarget {
                Label  = "Windows Temp",
                Path   = @"C:\Windows\Temp",
                Desc   = "Temporary files created by Windows and applications",
                Accent = Color.FromArgb(88,  166, 255)
            },
            new CleanTarget {
                Label  = "Software Distribution",
                Path   = @"C:\Windows\SoftwareDistribution\Download",
                Desc   = "Windows Update downloaded files (safe to delete)",
                Accent = Color.FromArgb(255, 163,  72)
            },
            new CleanTarget {
                Label  = "Prefetch",
                Path   = @"C:\Windows\Prefetch",
                Desc   = "App launch cache files — Windows will rebuild them",
                Accent = Color.FromArgb(63,  185, 119)
            },
            new CleanTarget {
                Label       = "Disk Cleanup",
                Path        = "",
                Desc        = "Run Windows Disk Cleanup (cleanmgr) — removes system junk, recycle bin, thumbnails",
                Accent      = Color.FromArgb(188, 140, 255),
                IsCleanmgr  = true
            }
        };

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, targetPanel, overallPanel, logPanel, bottomBar;
        Label lblTitle;
        Label lblOverallPct, lblOverallStatus;
        GradientProgressBar overallBar;
        Label lblTotalFiles, lblTotalSize, lblDeleted, lblFailed, lblSaved;
        Button btnScan, btnClean, btnCancel, btnDiskCleanup;
        ListView logList;
        BackgroundWorker worker;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<FileInfo> allFiles = new List<FileInfo>();
        int totalDeleted = 0, totalFailed = 0;
        long totalSaved = 0;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormUltraCleanUp()
        {
            BuildUI();
            SetupWorker();
            InitializeComponent();

        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Ultra CleanUp";
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
                Text = "⚡  ULTRA CLEANUP",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "Windows Temp  ·  SoftwareDistribution  ·  Prefetch  ·  Disk Cleanup",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ── Target cards (4 columns now) ──────────────────────────
            targetPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 118,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 0)
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            for (int i = 0; i < targets.Count; i++)
                tbl.Controls.Add(BuildTargetCard(targets[i]), i, 0);

            targetPanel.Controls.Add(tbl);

            // ── Overall progress ──────────────────────────────────────
            overallPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = C_SURF,
                Padding = new Padding(16, 10, 16, 10)
            };
            overallPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, overallPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0, overallPanel.Height - 1,
                        overallPanel.Width, overallPanel.Height - 1);
                }
            };

            var lblOverallTag = new Label
            {
                Text = "OVERALL PROGRESS",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 10)
            };

            lblOverallPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            overallBar = new GradientProgressBar(C_BLUE, C_GREEN)
            {
                Location = new Point(16, 32),
                Size = new Size(100, 18),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblOverallStatus = new Label
            {
                Text = "Select folders above and click SCAN.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 58)
            };

            // Summary row
            var sumRow = new FlowLayoutPanel
            {
                AutoSize = true,
                Location = new Point(16, 76),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight
            };

            lblTotalFiles = MakeInlineStat("0 files", C_BLUE);
            lblTotalSize = MakeInlineStat("0 B", C_AMBER);
            lblDeleted = MakeInlineStat("0 deleted", C_GREEN);
            lblFailed = MakeInlineStat("0 skipped", C_RED);
            lblSaved = MakeInlineStat("0 B freed", C_GREEN);
            sumRow.Controls.AddRange(new Control[]
                { lblTotalFiles, Dot(), lblTotalSize, Dot(),
                  lblDeleted, Dot(), lblFailed, Dot(), lblSaved });

            // Disk Cleanup separate button in overall panel
            btnDiskCleanup = MakeBtn("🧹  Run Disk Cleanup (cleanmgr)", C_PURPLE, new Size(260, 30));
            btnDiskCleanup.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDiskCleanup.Click += BtnDiskCleanup_Click;

            overallPanel.Controls.AddRange(new Control[]
                { lblOverallTag, lblOverallPct, overallBar,
                  lblOverallStatus, sumRow, btnDiskCleanup });

            overallPanel.Resize += (s, e) =>
            {
                overallBar.Size = new Size(overallPanel.Width - 80, 18);
                lblOverallPct.Location = new Point(overallPanel.Width - 56, 30);
                btnDiskCleanup.Location = new Point(
                    overallPanel.Width - btnDiskCleanup.Width - 16, 72);
            };

            // ── Buttons ───────────────────────────────────────────────
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

            btnScan = MakeBtn("🔍  Scan Selected", C_BLUE, new Size(180, 34));
            btnClean = MakeBtn("⚡  Clean All", C_RED, new Size(160, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(110, 34));

            btnClean.Enabled = false;
            btnCancel.Enabled = false;

            btnScan.Click += BtnScan_Click;
            btnClean.Click += BtnClean_Click;
            btnCancel.Click += (s, e) => { if (worker.IsBusy) worker.CancelAsync(); };

            bottomBar.Controls.AddRange(new Control[] { btnScan, btnClean, btnCancel });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnScan.Location = new Point(16, y);
                btnClean.Location = new Point(210, y);
                btnCancel.Location = new Point(384, y);
            };

            // ── Log list ──────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 8)
            };

            var lblLog = new Label
            {
                Text = "FILE LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

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

            logList.Columns.Add("Status", 80);
            logList.Columns.Add("Folder", 260);
            logList.Columns.Add("File", 310);
            logList.Columns.Add("Size", 110);
            logList.Columns.Add("Path", 366);

            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawRow;

            logPanel.Controls.AddRange(new Control[] { lblLog, logList });
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 20, logPanel.Height - 28);

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(logPanel);
            Controls.Add(overallPanel);
            Controls.Add(targetPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        // ════════════════════════════════════════════════════════════
        //  TARGET CARD BUILDER
        // ════════════════════════════════════════════════════════════
        Panel BuildTargetCard(CleanTarget t)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(
                    Color.FromArgb(45, t.Accent.R, t.Accent.G, t.Accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(t.Accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };

            t.Chk = new CheckBox
            {
                Text = t.Label,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = t.Accent,
                AutoSize = true,
                Checked = true,
                Location = new Point(10, 12),
                BackColor = Color.Transparent
            };

            var lblDesc = new Label
            {
                Text = t.Desc,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = false,
                Location = new Point(10, 34),
                Size = new Size(card.Width - 20, 28)
            };

            t.TargetBar = new GradientProgressBar(t.Accent,
                Color.FromArgb(180, t.Accent.R, t.Accent.G, t.Accent.B))
            {
                Location = new Point(10, 66),
                Size = new Size(card.Width - 20, 8),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            t.StatLbl = new Label
            {
                Text = t.IsCleanmgr ? "Click 'Run Disk Cleanup'" : "Not scanned",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(10, 80)
            };

            card.Controls.AddRange(new Control[]
                { t.Chk, lblDesc, t.TargetBar, t.StatLbl });
            card.Resize += (s, e) =>
            {
                lblDesc.Width = card.Width - 20;
                t.TargetBar.Width = card.Width - 20;
            };
            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  DISK CLEANUP BUTTON — cleanmgr /sageset:1 then /sagerun:1
        // ════════════════════════════════════════════════════════════
        void BtnDiskCleanup_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will run Windows Disk Cleanup in two steps:\n\n" +
                "  Step 1:  cleanmgr /sageset:1\n" +
                "           → Opens the settings dialog so you can\n" +
                "              choose which categories to clean.\n\n" +
                "  Step 2:  cleanmgr /sagerun:1\n" +
                "           → Silently runs the cleanup using your\n" +
                "              saved settings from Step 1.\n\n" +
                "Administrator rights required.\n\nContinue?",
                "Run Disk Cleanup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirm != DialogResult.Yes) return;

            // Update the Disk Cleanup card status
            var cleanmgrTarget = targets.Find(t => t.IsCleanmgr);
            if (cleanmgrTarget != null)
            {
                cleanmgrTarget.StatLbl.Text = "Running sageset...";
                cleanmgrTarget.StatLbl.ForeColor = C_AMBER;
                cleanmgrTarget.TargetBar.Value = 10;
                cleanmgrTarget.TargetBar.Invalidate();
            }

            btnDiskCleanup.Enabled = false;
            AddLog("Running", "Disk Cleanup", "cleanmgr /sageset:1",
                "", "Launching settings dialog...", C_PURPLE, "running");

            SetOverall(-1, "Step 1 — Running cleanmgr /sageset:1  (configure cleanup categories)...");

            // Step 1 — sageset (shows dialog for user to pick categories)
            RunCommand("cleanmgr", "/sageset:1", waitForExit: true,
                onComplete: () =>
                {
                    if (cleanmgrTarget != null)
                    {
                        cleanmgrTarget.StatLbl.Text = "Running sagerun...";
                        cleanmgrTarget.StatLbl.ForeColor = C_AMBER;
                        cleanmgrTarget.TargetBar.Value = 50;
                        cleanmgrTarget.TargetBar.Invalidate();
                    }

                    AddLog("Running", "Disk Cleanup", "cleanmgr /sagerun:1",
                        "", "Executing cleanup silently...", C_PURPLE, "running");

                    SetOverall(-1,
                        "Step 2 — Running cleanmgr /sagerun:1  (executing cleanup silently)...");

                    // Step 2 — sagerun (runs silently with saved settings)
                    RunCommand("cleanmgr", "/sagerun:1", waitForExit: true,
                        onComplete: () =>
                        {
                            if (cleanmgrTarget != null)
                            {
                                cleanmgrTarget.StatLbl.Text = "✔  Disk Cleanup complete";
                                cleanmgrTarget.StatLbl.ForeColor = C_GREEN;
                                cleanmgrTarget.TargetBar.Value = 100;
                                cleanmgrTarget.TargetBar.ChangeColors(C_GREEN,
                                    Color.FromArgb(180, 63, 185, 119));
                                cleanmgrTarget.TargetBar.Invalidate();
                            }

                            AddLog("✔ Done", "Disk Cleanup", "cleanmgr /sagerun:1",
                                "", "Disk Cleanup completed successfully.", C_GREEN, "deleted");

                            SetOverall(-1, "✔  Disk Cleanup completed successfully.");
                            lblOverallStatus.ForeColor = C_GREEN;
                            btnDiskCleanup.Enabled = true;
                        });
                });
        }

        // ════════════════════════════════════════════════════════════
        //  RUN COMMAND HELPER
        //  Runs a process async and fires onComplete on the UI thread
        // ════════════════════════════════════════════════════════════
        void RunCommand(string exe, string args,
            bool waitForExit, Action onComplete)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas",   // administrator
                    WindowStyle = ProcessWindowStyle.Normal
                },
                EnableRaisingEvents = true
            };

            proc.Exited += (s, e) =>
            {
                if (InvokeRequired)
                    Invoke(new Action(onComplete));
                else
                    onComplete();
            };

            try
            {
                proc.Start();
                if (waitForExit)
                {
                    // wait on background thread so UI stays responsive
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        proc.WaitForExit();
                        // Exited event fires automatically
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to run " + exe + " " + args + "\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnDiskCleanup.Enabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ADD LOG ENTRY
        // ════════════════════════════════════════════════════════════
        void AddLog(string status, string folder, string file,
            string size, string path, Color fg, string tag)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                    AddLog(status, folder, file, size, path, fg, tag)));
                return;
            }
            var item = new ListViewItem(status);
            item.SubItems.Add(folder);
            item.SubItems.Add(file);
            item.SubItems.Add(size);
            item.SubItems.Add(path);
            item.ForeColor = fg;
            item.Tag = tag;
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        // ════════════════════════════════════════════════════════════
        //  BACKGROUND WORKER
        // ════════════════════════════════════════════════════════════
        void SetupWorker()
        {
            worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_Completed;
        }

        // ════════════════════════════════════════════════════════════
        //  SCAN
        // ════════════════════════════════════════════════════════════
        void BtnScan_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy) return;

            allFiles.Clear();
            logList.Items.Clear();
            totalDeleted = totalFailed = 0;
            totalSaved = 0;

            foreach (var t in targets)
            {
                t.Found = t.Deleted = t.Failed = 0;
                t.Saved = 0;
                if (!t.IsCleanmgr)
                {
                    t.StatLbl.Text = "Scanning...";
                    t.StatLbl.ForeColor = C_AMBER;
                    t.TargetBar.Value = 0;
                }
            }

            ResetSummary();
            SetOverall(0, "Scanning selected folders...");

            btnScan.Enabled = false;
            btnClean.Enabled = false;
            btnCancel.Enabled = true;

            worker.RunWorkerAsync("scan");
        }

        // ════════════════════════════════════════════════════════════
        //  CLEAN
        // ════════════════════════════════════════════════════════════
        void BtnClean_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy || allFiles.Count == 0) return;

            var confirm = MessageBox.Show(
                string.Format(
                    "Clean {0} files ({1}) from:\n\n" +
                    "  • C:\\Windows\\Temp\n" +
                    "  • C:\\Windows\\SoftwareDistribution\\Download\n" +
                    "  • C:\\Windows\\Prefetch\n\n" +
                    "Administrator rights required.\n" +
                    "Use the '🧹 Run Disk Cleanup' button separately\n" +
                    "to also run cleanmgr.\n\nContinue?",
                    allFiles.Count, FormatSize(GetTotalBytes(allFiles))),
                "Confirm Ultra Clean",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            logList.Items.Clear();
            totalDeleted = totalFailed = 0;
            totalSaved = 0;
            foreach (var t in targets)
                if (!t.IsCleanmgr) { t.Deleted = t.Failed = 0; t.Saved = 0; }

            SetOverall(0, "Cleaning...");
            btnScan.Enabled = false;
            btnClean.Enabled = false;
            btnCancel.Enabled = true;

            worker.RunWorkerAsync("clean");
        }

        // ════════════════════════════════════════════════════════════
        //  DO WORK
        // ════════════════════════════════════════════════════════════
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string mode = e.Argument as string;

            if (mode == "scan")
            {
                var collected = new List<FileInfo>();
                for (int i = 0; i < targets.Count; i++)
                {
                    if (worker.CancellationPending) { e.Cancel = true; return; }
                    var t = targets[i];
                    if (t.IsCleanmgr || !t.Chk.Checked) continue;
                    if (!Directory.Exists(t.Path)) continue;

                    worker.ReportProgress(0, new object[] { "scan_start", i });
                    CollectFiles(new DirectoryInfo(t.Path), collected, i, worker, e);
                    worker.ReportProgress(0, new object[] { "scan_done", i, t.Found });
                }
                e.Result = new object[] { "scan", collected };
            }
            else if (mode == "clean")
            {
                int total = allFiles.Count;
                int done = 0;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (worker.CancellationPending) { e.Cancel = true; break; }
                    var t = targets[i];
                    if (t.IsCleanmgr || !t.Chk.Checked) continue;

                    var tFiles = allFiles.FindAll(f =>
                        f.FullName.StartsWith(t.Path,
                            StringComparison.OrdinalIgnoreCase));

                    int tDone = 0;
                    foreach (var fi in tFiles)
                    {
                        if (worker.CancellationPending) { e.Cancel = true; break; }

                        bool ok = false; long sz = 0; string err = "";
                        try { sz = fi.Length; fi.Delete(); ok = true; }
                        catch (Exception ex) { err = ex.Message; }

                        done++; tDone++;
                        int overall = (int)((double)done / total * 100);
                        int tPct = (int)((double)tDone / tFiles.Count * 100);

                        worker.ReportProgress(overall,
                            new object[] { "file", i, ok, fi.Name,
                                sz, fi.FullName, err, tPct });
                    }

                    try
                    {
                        foreach (var d in new DirectoryInfo(t.Path).GetDirectories())
                            try { d.Delete(true); } catch { }
                    }
                    catch { }

                    worker.ReportProgress(
                        (int)((double)done / total * 100),
                        new object[] { "target_done", i });
                }

                e.Result = new object[] { "clean" };
            }
        }

        void CollectFiles(DirectoryInfo dir, List<FileInfo> list,
            int idx, BackgroundWorker bw, DoWorkEventArgs e)
        {
            if (bw.CancellationPending) { e.Cancel = true; return; }
            try
            {
                foreach (var fi in dir.GetFiles())
                {
                    list.Add(fi);
                    targets[idx].Found++;
                    bw.ReportProgress(0, new object[]
                        { "scan_file", idx, fi.Name, fi.Length, fi.FullName });
                }
                foreach (var sub in dir.GetDirectories())
                    CollectFiles(sub, list, idx, bw, e);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  PROGRESS CHANGED
        // ════════════════════════════════════════════════════════════
        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string;

            if (type == "scan_start")
            {
                int i = (int)d[1];
                targets[i].StatLbl.Text = "Scanning...";
                targets[i].StatLbl.ForeColor = C_AMBER;
            }
            else if (type == "scan_file")
            {
                int i = (int)d[1];
                string name = d[2] as string;
                long sz = Convert.ToInt64(d[3]);
                string path = d[4] as string;

                SetOverall(0, "Scanning: " + name);
                targets[i].StatLbl.Text = string.Format("{0} files found", targets[i].Found);

                if (logList.Items.Count < 3000)
                {
                    var item = new ListViewItem("Found");
                    item.SubItems.Add(targets[i].Label);
                    item.SubItems.Add(name);
                    item.SubItems.Add(FormatSize(sz));
                    item.SubItems.Add(path);
                    item.ForeColor = C_BLUE;
                    item.Tag = "found";
                    logList.Items.Add(item);
                }

                lblTotalFiles.Text = allFiles.Count.ToString() + " files";
            }
            else if (type == "scan_done")
            {
                int i = (int)d[1];
                int count = (int)d[2];
                targets[i].StatLbl.Text = count > 0
                    ? string.Format("{0} files", count) : "✔ Already clean";
                targets[i].StatLbl.ForeColor = count > 0 ? C_AMBER : C_GREEN;
                targets[i].TargetBar.Value = count > 0 ? 100 : 0;
            }
            else if (type == "file")
            {
                int i = (int)d[1];
                bool ok = Convert.ToBoolean(d[2]);
                string name = d[3] as string;
                long sz = Convert.ToInt64(d[4]);
                string path = d[5] as string;
                string err = d[6] as string;
                int tPct = (int)d[7];

                overallBar.Value = e.ProgressPercentage;
                lblOverallPct.Text = e.ProgressPercentage + "%";
                lblOverallStatus.Text = (ok ? "Deleted: " : "Skipped: ") + name;

                var t = targets[i];
                if (ok) { t.Deleted++; t.Saved += sz; totalDeleted++; totalSaved += sz; }
                else t.Failed++; totalFailed++;

                t.TargetBar.Value = tPct;
                t.StatLbl.Text = string.Format("✔ {0}  ✖ {1}  freed {2}",
                    t.Deleted, t.Failed, FormatSize(t.Saved));
                t.StatLbl.ForeColor = C_GREEN;

                var item = new ListViewItem(ok ? "✔ Deleted" : "✖ Skipped");
                item.SubItems.Add(t.Label);
                item.SubItems.Add(name);
                item.SubItems.Add(FormatSize(sz));
                item.SubItems.Add(ok ? path : path + " — " + err);
                item.ForeColor = ok ? C_GREEN : C_RED;
                item.Tag = ok ? "deleted" : "failed";
                logList.Items.Add(item);
                logList.EnsureVisible(logList.Items.Count - 1);

                lblDeleted.Text = totalDeleted + " deleted";
                lblFailed.Text = totalFailed + " skipped";
                lblSaved.Text = FormatSize(totalSaved) + " freed";
            }
            else if (type == "target_done")
            {
                int i = (int)d[1];
                targets[i].TargetBar.Value = 100;
                targets[i].TargetBar.ChangeColors(C_GREEN,
                    Color.FromArgb(180, 63, 185, 119));
                targets[i].TargetBar.Invalidate();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  WORKER COMPLETED
        // ════════════════════════════════════════════════════════════
        void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            btnScan.Enabled = true;
            btnCancel.Enabled = false;

            if (e.Cancelled)
            {
                lblOverallStatus.Text = "Operation cancelled by user.";
                lblOverallStatus.ForeColor = C_AMBER;
                btnClean.Enabled = allFiles.Count > 0;
                return;
            }

            var result = e.Result as object[];
            if (result == null) return;
            string mode = result[0] as string;

            if (mode == "scan")
            {
                allFiles = result[1] as List<FileInfo>;
                long total = GetTotalBytes(allFiles);

                lblTotalFiles.Text = allFiles.Count + " files";
                lblTotalSize.Text = FormatSize(total);

                overallBar.Value = 100;
                lblOverallPct.Text = "100%";
                lblOverallStatus.Text = allFiles.Count == 0
                    ? "✔  All folders are clean!"
                    : string.Format("Scan complete — {0} files  ({1})  ready to clean.",
                        allFiles.Count, FormatSize(total));
                lblOverallStatus.ForeColor =
                    allFiles.Count == 0 ? C_GREEN : C_AMBER;

                btnClean.Enabled = allFiles.Count > 0;
            }
            else if (mode == "clean")
            {
                overallBar.Value = 100;
                lblOverallPct.Text = "100%";
                overallBar.ChangeColors(C_GREEN, C_BLUE);
                overallBar.Invalidate();

                lblOverallStatus.Text = string.Format(
                    "✔  Done — {0} deleted · {1} skipped · {2} freed",
                    totalDeleted, totalFailed, FormatSize(totalSaved));
                lblOverallStatus.ForeColor = C_GREEN;

                lblDeleted.Text = totalDeleted + " deleted";
                lblFailed.Text = totalFailed + " skipped";
                lblSaved.Text = FormatSize(totalSaved) + " freed";
                btnClean.Enabled = false;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void SetOverall(int pct, string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetOverall(pct, msg))); return; }
            if (pct >= 0) { overallBar.Value = pct; lblOverallPct.Text = pct + "%"; }
            lblOverallStatus.Text = msg;
            lblOverallStatus.ForeColor = C_SUB;
        }

        void ResetSummary()
        {
            lblTotalFiles.Text = "0 files";
            lblTotalSize.Text = "0 B";
            lblDeleted.Text = "0 deleted";
            lblFailed.Text = "0 skipped";
            lblSaved.Text = "0 B freed";
            overallBar.Value = 0;
            lblOverallPct.Text = "0%";
            overallBar.ChangeColors(C_BLUE, C_GREEN);
        }

        long GetTotalBytes(List<FileInfo> files)
        {
            long t = 0;
            foreach (var f in files) try { t += f.Length; } catch { }
            return t;
        }

        string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.0} KB", bytes / 1024.0);
            if (bytes < 1024L * 1024 * 1024) return string.Format("{0:0.0} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:0.00} GB", bytes / (1024.0 * 1024 * 1024));
        }

        Label MakeInlineStat(string text, Color c)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = c,
                AutoSize = true,
                Margin = new Padding(0, 0, 4, 0)
            };
        }

        Label Dot()
        {
            return new Label
            {
                Text = "·",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_BORDER,
                AutoSize = true,
                Margin = new Padding(0, 0, 4, 0)
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
            {
                var rc = new Rectangle(e.Bounds.X + 8, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, ft, br, rc, sf);
            }
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p,
                    e.Bounds.Left, e.Bounds.Bottom - 1,
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

            Color fg = C_SUB;
            if (e.ColumnIndex == 0)
                fg = tag == "deleted" ? C_GREEN
                   : tag == "failed" ? C_RED
                   : tag == "running" ? C_PURPLE
                   : C_BLUE;
            else if (e.ColumnIndex == 1)
            {
                foreach (var t in targets)
                    if (e.SubItem.Text == t.Label) { fg = t.Accent; break; }
            }
            else if (e.ColumnIndex == 2) fg = C_TXT;
            else if (e.ColumnIndex == 3) fg = C_AMBER;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 3
                    ? StringAlignment.Far : StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(
                    e.Bounds.X + 6, e.Bounds.Y,
                    e.Bounds.Width - 10, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, logList.Font, br, rc, sf);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (worker != null && worker.IsBusy) worker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  GRADIENT PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class GradientProgressBar : Control
    {
        int _val;
        Color _c1, _c2;

        public int Value
        {
            get { return _val; }
            set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        // C#
        public GradientProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void ChangeColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);

            int fw = (int)(Width * (_val / 100.0));
            if (fw > 2)
            {
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, fw, Height),
                    _c1, _c2, LinearGradientMode.Horizontal))
                    g.FillRectangle(br, new Rectangle(0, 0, fw, Height));

                using (var br = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                    g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));

                if (fw > 6)
                {
                    int gx = fw - 4;
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(gx - 3, 0, 8, Height);
                        using (var pgb = new PathGradientBrush(path))
                        {
                            pgb.CenterColor = Color.FromArgb(180, 255, 255, 255);
                            pgb.SurroundColors = new[] { Color.Transparent };
                            g.FillPath(pgb, path);
                        }
                    }
                }
            }

            if (_val > 10 && Height >= 14)
            {
                string txt = _val + "%";
                using (var ft = new Font("Segoe UI Semibold", 7f))
                using (var br = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
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
