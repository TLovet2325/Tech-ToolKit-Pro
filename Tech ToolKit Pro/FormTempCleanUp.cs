using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormTempCleanUp : Form
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
        static readonly Color C_TXT = Color.FromArgb(230, 237, 243);
        static readonly Color C_SUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, summaryPanel, progressPanel, logPanel, bottomBar;
        Label lblTitle;
        Label lblTotalFiles, lblTotalSize, lblDeleted, lblFailed, lblSaved;
        Label lblProgressText, lblPercent, lblStatus;
        GradientBar progressBar;
        Button btnScan, btnClean, btnCancel;
        ListView logList;
        BackgroundWorker worker;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        string tempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        List<FileInfo> foundFiles = new List<FileInfo>();
        int deleted = 0;
        int failed = 0;
        long savedBytes = 0;
        bool scanning = false;
        bool cleaning = false;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormTempCleanUp()
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
            Text = "Clean Temp";
            BackColor = C_BG;
            ForeColor = C_TXT;
            FormBorderStyle = FormBorderStyle.None;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9f);

            // ── Top bar ───────────────────────────────────────────────
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = C_SURF
            };
            topBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 51, topBar.Width, 51);
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, 4, 52), C_AMBER, C_RED,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };

            lblTitle = new Label
            {
                Text = "🗑  TEMP FILES CLEANUP",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            var lblPath = new Label
            {
                Text = "  " + Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            topBar.Controls.AddRange(new Control[] { lblTitle, lblPath });
            topBar.Resize += (s, e) =>
                lblPath.Location = new Point(topBar.Width - lblPath.Width - 12, 18);

            // ── Summary cards ─────────────────────────────────────────
            summaryPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = C_BG,
                Padding = new Padding(12, 10, 12, 0)
            };

            var cardRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < 5; i++)
                cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

            lblTotalFiles = MakeStat("0", "Total Files", C_BLUE);
            lblTotalSize = MakeStat("0 MB", "Total Size", C_AMBER);
            lblDeleted = MakeStat("0", "Deleted", C_GREEN);
            lblFailed = MakeStat("0", "Skipped", C_RED);
            lblSaved = MakeStat("0 MB", "Space Freed", C_GREEN);

            cardRow.Controls.Add(WrapStat(lblTotalFiles, "Total Files", C_BLUE), 0, 0);
            cardRow.Controls.Add(WrapStat(lblTotalSize, "Total Size", C_AMBER), 1, 0);
            cardRow.Controls.Add(WrapStat(lblDeleted, "Deleted", C_GREEN), 2, 0);
            cardRow.Controls.Add(WrapStat(lblFailed, "Skipped", C_RED), 3, 0);
            cardRow.Controls.Add(WrapStat(lblSaved, "Space Freed", C_GREEN), 4, 0);

            summaryPanel.Controls.Add(cardRow);

            // ── Progress panel ────────────────────────────────────────
            progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = C_SURF,
                Padding = new Padding(16, 10, 16, 10)
            };
            progressPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                {
                    e.Graphics.DrawLine(p, 0, 0,
                        progressPanel.Width, 0);
                    e.Graphics.DrawLine(p, 0,
                        progressPanel.Height - 1,
                        progressPanel.Width,
                        progressPanel.Height - 1);
                }
            };

            lblProgressText = new Label
            {
                Text = "Click  SCAN  to find temp files.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 12)
            };

            lblPercent = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            progressBar = new GradientBar(C_BLUE, C_GREEN)
            {
                Location = new Point(16, 38),
                Size = new Size(100, 14),   // resized on Resize
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 60)
            };

            progressPanel.Controls.AddRange(new Control[]
                { lblProgressText, lblPercent, progressBar, lblStatus });

            progressPanel.Resize += (s, e) =>
            {
                progressBar.Size = new Size(progressPanel.Width - 80, 14);
                lblPercent.Location = new Point(progressPanel.Width - 56, 36);
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

            btnScan = MakeBtn("🔍  Scan Temp Folder", C_BLUE, new Size(190, 34));
            btnClean = MakeBtn("🗑  Clean All Files", C_RED, new Size(180, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(110, 34));

            btnClean.Enabled = false;
            btnCancel.Enabled = false;

            btnScan.Click += BtnScan_Click;
            btnClean.Click += BtnClean_Click;
            btnCancel.Click += BtnCancel_Click;

            bottomBar.Controls.AddRange(new Control[]
                { btnScan, btnClean, btnCancel });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnScan.Location = new Point(16, y);
                btnClean.Location = new Point(222, y);
                btnCancel.Location = new Point(418, y);
            };

            // ── Log list ──────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(12, 8, 12, 8)
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
                OwnerDraw = true,
                VirtualMode = false
            };
            logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            logList.Columns.Add("Status", 170);
            logList.Columns.Add("File Name", 380);
            logList.Columns.Add("Size", 110);
            logList.Columns.Add("Path", 462);

            logList.DrawColumnHeader += DrawHeader;
            logList.DrawItem += (s2, e2) => { };
            logList.DrawSubItem += DrawRow;

            logPanel.Controls.AddRange(new Control[] { lblLog, logList });
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(
                    logPanel.Width - 24,
                    logPanel.Height - 28);

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(logPanel);
            Controls.Add(progressPanel);
            Controls.Add(summaryPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        // ════════════════════════════════════════════════════════════
        //  BACKGROUND WORKER SETUP
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
        //  SCAN BUTTON
        // ════════════════════════════════════════════════════════════
        void BtnScan_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy) return;

            foundFiles.Clear();
            logList.Items.Clear();
            deleted = failed = 0;
            savedBytes = 0;
            ResetStats();

            scanning = true;
            cleaning = false;

            btnScan.Enabled = false;
            btnClean.Enabled = false;
            btnCancel.Enabled = true;

            SetProgress(0, "Scanning temp folder...");
            worker.RunWorkerAsync("scan");
        }

        // ════════════════════════════════════════════════════════════
        //  CLEAN BUTTON
        // ════════════════════════════════════════════════════════════
        void BtnClean_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy || foundFiles.Count == 0) return;

            var confirm = MessageBox.Show(
                string.Format(
                    "Delete {0} temp files ({1})?\n\nThis cannot be undone.",
                    foundFiles.Count,
                    FormatSize(foundFiles.Count > 0
                        ? GetTotalBytes(foundFiles) : 0)),
                "Confirm Clean",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            deleted = failed = 0;
            savedBytes = 0;
            logList.Items.Clear();

            cleaning = true;
            scanning = false;

            btnScan.Enabled = false;
            btnClean.Enabled = false;
            btnCancel.Enabled = true;

            SetProgress(0, "Cleaning...");
            worker.RunWorkerAsync("clean");
        }

        // ════════════════════════════════════════════════════════════
        //  CANCEL BUTTON
        // ════════════════════════════════════════════════════════════
        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (worker.IsBusy)
                worker.CancelAsync();
        }

        // ════════════════════════════════════════════════════════════
        //  BACKGROUND WORKER — DO WORK
        // ════════════════════════════════════════════════════════════
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string mode = e.Argument as string;

            if (mode == "scan")
            {
                // ── SCAN ─────────────────────────────────────────────
                var files = new List<FileInfo>();
                try
                {
                    var di = new DirectoryInfo(tempPath);
                    CollectFiles(di, files, worker, e);
                }
                catch { }

                e.Result = new object[] { "scan", files };
            }
            else if (mode == "clean")
            {
                // ── CLEAN ────────────────────────────────────────────
                int total = foundFiles.Count;
                int done = 0;
                var results = new List<object[]>();

                foreach (var fi in foundFiles)
                {
                    if (worker.CancellationPending) { e.Cancel = true; break; }

                    bool ok = false;
                    long sz = 0;
                    string err = "";

                    try
                    {
                        sz = fi.Length;
                        fi.Delete();
                        ok = true;
                    }
                    catch (Exception ex)
                    {
                        err = ex.Message;
                    }

                    done++;
                    int pct = (int)((double)done / total * 100);
                    results.Add(new object[] { ok, fi.Name, sz, fi.FullName, err });

                    worker.ReportProgress(pct,
                        new object[] { "file", ok, fi.Name, sz, fi.FullName, err });
                }

                // Also delete empty subdirectories
                try
                {
                    foreach (var dir in new DirectoryInfo(tempPath).GetDirectories())
                    {
                        try { dir.Delete(true); } catch { }
                    }
                }
                catch { }

                e.Result = new object[] { "clean", results };
            }
        }

        // Recursively collect files
        void CollectFiles(DirectoryInfo dir, List<FileInfo> list,
            BackgroundWorker bw, DoWorkEventArgs e)
        {
            if (bw.CancellationPending) { e.Cancel = true; return; }

            try
            {
                foreach (var fi in dir.GetFiles())
                {
                    list.Add(fi);
                    bw.ReportProgress(0, new object[]
                        { "scan_file", fi.Name, fi.Length, fi.FullName });
                }
                foreach (var sub in dir.GetDirectories())
                    CollectFiles(sub, list, bw, e);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  PROGRESS CHANGED
        // ════════════════════════════════════════════════════════════
        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var data = e.UserState as object[];
            if (data == null) return;

            string type = data[0] as string;

            if (type == "scan_file")
            {
                string name = data[1] as string;
                long sz = Convert.ToInt64(data[2]);
                string path = data[3] as string;

                lblStatus.Text = "Found: " + name;

                var item = new ListViewItem("Found");
                item.SubItems.Add(name);
                item.SubItems.Add(FormatSize(sz));
                item.SubItems.Add(path);
                item.ForeColor = C_BLUE;
                item.Tag = "found";
                if (logList.Items.Count < 2000)  // cap display at 2000
                    logList.Items.Add(item);

                // update total count live
                lblTotalFiles.Text = (logList.Items.Count).ToString();
            }
            else if (type == "file")
            {
                bool ok = Convert.ToBoolean(data[1]);
                string name = data[2] as string;
                long sz = Convert.ToInt64(data[3]);
                string path = data[4] as string;
                string err = data[5] as string;

                progressBar.Value = e.ProgressPercentage;
                lblPercent.Text = e.ProgressPercentage + "%";
                lblStatus.Text = (ok ? "Deleted: " : "Skipped: ") + name;

                if (ok) { deleted++; savedBytes += sz; }
                else failed++;

                var item = new ListViewItem(ok ? "✔ Deleted" : "✖ Skipped");
                item.SubItems.Add(name);
                item.SubItems.Add(FormatSize(sz));
                item.SubItems.Add(ok ? path : path + "  —  " + err);
                item.ForeColor = ok ? C_GREEN : C_RED;
                item.Tag = ok ? "deleted" : "failed";

                logList.Items.Add(item);
                logList.EnsureVisible(logList.Items.Count - 1);

                // live stats
                lblDeleted.Text = deleted.ToString();
                lblFailed.Text = failed.ToString();
                lblSaved.Text = FormatSize(savedBytes);
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
                SetProgress(progressBar.Value, "Cancelled by user.");
                lblStatus.Text = "Operation cancelled.";
                btnClean.Enabled = foundFiles.Count > 0;
                return;
            }

            var result = e.Result as object[];
            if (result == null) return;

            string mode = result[0] as string;

            if (mode == "scan")
            {
                foundFiles = result[1] as List<FileInfo>;
                long total = GetTotalBytes(foundFiles);

                lblTotalFiles.Text = foundFiles.Count.ToString();
                lblTotalSize.Text = FormatSize(total);

                progressBar.Value = 100;
                lblPercent.Text = "100%";

                SetProgress(100,
                    foundFiles.Count == 0
                        ? "Temp folder is already clean! ✔"
                        : string.Format("Scan complete — {0} files found  ({1})",
                            foundFiles.Count, FormatSize(total)));

                btnClean.Enabled = foundFiles.Count > 0;
            }
            else if (mode == "clean")
            {
                progressBar.Value = 100;
                lblPercent.Text = "100%";

                SetProgress(100,
                    string.Format(
                        "Done — {0} deleted, {1} skipped, {2} freed.",
                        deleted, failed, FormatSize(savedBytes)));

                lblDeleted.Text = deleted.ToString();
                lblFailed.Text = failed.ToString();
                lblSaved.Text = FormatSize(savedBytes);

                btnClean.Enabled = false;

                // Change bar to green on success
                progressBar.ChangeColors(C_GREEN, C_BLUE);
                progressBar.Invalidate();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void SetProgress(int pct, string msg)
        {
            progressBar.Value = pct;
            lblPercent.Text = pct + "%";
            lblProgressText.Text = msg;
        }

        void ResetStats()
        {
            lblTotalFiles.Text = "0";
            lblTotalSize.Text = "0 B";
            lblDeleted.Text = "0";
            lblFailed.Text = "0";
            lblSaved.Text = "0 B";
            progressBar.Value = 0;
            lblPercent.Text = "0%";
            lblStatus.Text = "";
            progressBar.ChangeColors(C_BLUE, C_GREEN);
        }

        long GetTotalBytes(List<FileInfo> files)
        {
            long total = 0;
            foreach (var f in files)
                try { total += f.Length; } catch { }
            return total;
        }

        string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.0} KB", bytes / 1024.0);
            if (bytes < 1024 * 1024 * 1024) return string.Format("{0:0.0} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:0.00} GB", bytes / (1024.0 * 1024 * 1024));
        }

        // ════════════════════════════════════════════════════════════
        //  STAT CARD HELPERS
        // ════════════════════════════════════════════════════════════
        Label MakeStat(string val, string tag, Color accent)
        {
            if (tag is null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            return new Label
            {
                Text = val,
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = accent,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        Panel WrapStat(Label valLbl, string tagText, Color accent)
        {
            var card = new Panel
            {
                BackColor = C_SURF,
                Margin = new Padding(4),
                Dock = DockStyle.Fill
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(40, accent.R, accent.G, accent.B), 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };

            var sub = new Label
            {
                Text = tagText,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };

            card.Controls.Add(valLbl);
            card.Controls.Add(sub);
            card.Resize += (s, e) =>
            {
                valLbl.Location = new Point((card.Width - valLbl.Width) / 2, 8);
                sub.Location = new Point((card.Width - sub.Width) / 2, 42);
            };
            return card;
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
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, accent.R, accent.G, accent.B);
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

            Color fg = C_SUB;
            if (e.ColumnIndex == 0)
                fg = tag == "deleted" ? C_GREEN : tag == "failed" ? C_RED : C_BLUE;
            else if (e.ColumnIndex == 1)
                fg = C_TXT;
            else if (e.ColumnIndex == 2)
                fg = C_AMBER;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 2
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
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  GRADIENT PROGRESS BAR — custom smooth bar
    // ════════════════════════════════════════════════════════════════
    public class GradientBar : Control
    {
        int _val;
        Color _c1, _c2;

        public int Value
        {
            get { return _val; }
            set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public GradientBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;

            // include SupportsTransparentBackColor before setting BackColor
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);

            UpdateStyles();              // ensure styles are applied
            BackColor = Color.Transparent;
            Height = 14;
        }

        public void ChangeColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // track
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);

            // fill
            int fw = (int)(Width * (_val / 100.0));
            if (fw > 2)
            {
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, fw, Height),
                    _c1, _c2,
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, new Rectangle(0, 0, fw, Height));

                // shimmer highlight
                using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillRectangle(br, new Rectangle(0, 0, fw, Height / 2));
            }

            // percentage text inside bar
            if (_val > 8)
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