using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FIXES APPLIED
    //  ─────────────────────────────────────────────────────────────
    //  1. "Ambiguity / already contains a definition" for historyList
    //     and logList — they were declared BOTH as class-level fields
    //     AND with a type keyword inside the BuildLogTab /
    //     BuildHistoryTab methods (e.g.  ListView logList = new…).
    //     Fix: declare all ListView fields at class level only; inside
    //     the Build* methods just assign them (no type keyword).
    //
    //  2. ZipFile.Open / ZipFile.CreateEntryFromFile — these live in
    //     System.IO.Compression.FileSystem.dll which is a SEPARATE
    //     assembly not available in all project configurations.
    //     Fix: replaced with ZipArchive + manual entry creation using
    //     only System.IO.Compression (always available), which works
    //     in every WinForms .NET Framework project without extra refs.
    // ════════════════════════════════════════════════════════════════
    public partial class FormWinBackUp : Form
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
        //  BACKUP ITEM MODEL
        // ════════════════════════════════════════════════════════════
        class BackupItem
        {
            public string Path { get; set; }
            public bool IsFolder { get; set; }
            public long SizeBytes { get; set; }
            public string SizeStr { get; set; }
            public bool Selected { get; set; } = true;
        }

        // ════════════════════════════════════════════════════════════
        //  CONTROLS — ALL declared here at class level (no duplicates)
        // ════════════════════════════════════════════════════════════
        Panel topBar, optPanel, sourcePanel,
                         progPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus;
        Label lblProgText, lblProgPct, lblProgSub;
        Label lblTotalSize, lblFileCount;
        BackupProgressBar progBar;

        // ── ListView fields declared ONCE here ────────────────────
        ListView sourceList;    // files/folders to back up
        ListView logList;       // backup log (log tab)
        ListView historyList;   // history (history tab)

        Button btnAddFile, btnAddFolder, btnRemove,
                         btnClearSources, btnStartBackup,
                         btnCancel, btnOpenDest, btnClearHistory;
        TextBox txtDest, txtArchiveName;
        ComboBox cbFormat, cbCompression;
        CheckBox chkTimestamp, chkOpenAfter;
        TabControl tabBottom;

        BackgroundWorker backupWorker;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime backupStart;

        List<BackupItem> sources = new List<BackupItem>();
        bool winrarFound = false;
        string winrarPath = "";

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormWinBackUp()
        {
            BuildUI();
            SetupWorker();
            DetectWinRAR();
            RefreshSourceStats();
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Windows Backup";
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
                    new Rectangle(0, 0, 4, 52), C_BLUE, C_PURPLE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🗄  WINDOWS  BACKUP  CENTER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(new Label
            {
                Text = "Backup files and folders to ZIP or WinRAR archive",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            });

            // ── Options panel ─────────────────────────────────────────
            optPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = C_SURF,
                Padding = new Padding(14, 10, 14, 10)
            };
            optPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, optPanel.Height - 1,
                        optPanel.Width, optPanel.Height - 1);
            };

            // Row 1 — destination + name
            optPanel.Controls.Add(MakeOptLabel("Save To:", new Point(14, 12)));
            txtDest = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(90, 10),
                Size = new Size(340, 26),
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Backups")
            };
            optPanel.Controls.Add(txtDest);

            var btnBrowseDest = MakeSmallBtn("📁  Browse", C_BLUE, new Size(90, 26));
            btnBrowseDest.Location = new Point(438, 10);
            btnBrowseDest.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog
                {
                    Description = "Select backup destination folder",
                    SelectedPath = txtDest.Text
                })
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtDest.Text = dlg.SelectedPath;
            };
            optPanel.Controls.Add(btnBrowseDest);

            optPanel.Controls.Add(MakeOptLabel("Name:", new Point(544, 12)));
            txtArchiveName = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(594, 10),
                Size = new Size(200, 26),
                Text = "MyBackup"
            };
            optPanel.Controls.Add(txtArchiveName);

            chkTimestamp = new CheckBox
            {
                Text = "Add timestamp",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Checked = true,
                Location = new Point(804, 14),
                BackColor = Color.Transparent
            };
            optPanel.Controls.Add(chkTimestamp);

            // Row 2 — format + compression + open-after
            optPanel.Controls.Add(MakeOptLabel("Format:", new Point(14, 52)));
            cbFormat = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(90, 50),
                Size = new Size(135, 26)
            };
            cbFormat.Items.AddRange(new object[]
                { "ZIP  (Built-in)", "RAR  (WinRAR)" });
            cbFormat.SelectedIndex = 0;
            optPanel.Controls.Add(cbFormat);

            optPanel.Controls.Add(MakeOptLabel("Compression:", new Point(240, 52)));
            cbCompression = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(340, 50),
                Size = new Size(148, 26)
            };
            cbCompression.Items.AddRange(new object[]
                { "No Compression", "Fast (Level 1)", "Normal (Level 5)", "Best (Level 9)" });
            cbCompression.SelectedIndex = 2;
            optPanel.Controls.Add(cbCompression);

            chkOpenAfter = new CheckBox
            {
                Text = "Open folder after backup",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Checked = true,
                Location = new Point(504, 54),
                BackColor = Color.Transparent
            };
            optPanel.Controls.Add(chkOpenAfter);

            // Row 3 — stats
            lblTotalSize = MakeOptLabel("Total: 0 B", new Point(14, 84));
            lblTotalSize.ForeColor = C_AMBER;
            lblFileCount = MakeOptLabel("0 items", new Point(140, 84));
            lblFileCount.ForeColor = C_BLUE;
            optPanel.Controls.Add(lblTotalSize);
            optPanel.Controls.Add(lblFileCount);

            // ── Source panel ──────────────────────────────────────────
            sourcePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 230,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 4)
            };

            sourcePanel.Controls.Add(new Label
            {
                Text = "FILES  &  FOLDERS  TO  BACK  UP",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            });

            btnAddFile = MakeSmallBtn("📄  Add Files", C_BLUE, new Size(110, 26));
            btnAddFolder = MakeSmallBtn("📁  Add Folder", C_GREEN, new Size(110, 26));
            btnRemove = MakeSmallBtn("✕  Remove", C_RED, new Size(90, 26));
            btnClearSources = MakeSmallBtn("🗑  Clear All", C_SUB, new Size(90, 26));

            btnAddFile.Location = new Point(0, 22);
            btnAddFolder.Location = new Point(118, 22);
            btnRemove.Location = new Point(236, 22);
            btnClearSources.Location = new Point(334, 22);

            btnAddFile.Click += BtnAddFile_Click;
            btnAddFolder.Click += BtnAddFolder_Click;
            btnRemove.Click += BtnRemove_Click;
            btnClearSources.Click += (s, e) =>
            {
                sources.Clear();
                sourceList.Items.Clear();
                RefreshSourceStats();
            };

            // sourceList — assign to class field (no 'ListView' keyword here)
            sourceList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 54),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                MultiSelect = true,
                CheckBoxes = true
            };
            sourceList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                              | AnchorStyles.Left | AnchorStyles.Right;

            sourceList.Columns.Add("", 22);
            sourceList.Columns.Add("Path", 580);
            sourceList.Columns.Add("Type", 280);
            sourceList.Columns.Add("Size", 244);

            sourceList.DrawColumnHeader += DrawSourceHeader;
            sourceList.DrawItem += (s, e) => { };
            sourceList.DrawSubItem += DrawSourceRow;
            sourceList.ItemChecked += (s, e) =>
            {
                if (e.Item.Tag is BackupItem bi) bi.Selected = e.Item.Checked;
                RefreshSourceStats();
            };

            sourcePanel.Controls.AddRange(new Control[]
                { btnAddFile, btnAddFolder, btnRemove, btnClearSources, sourceList });
            sourcePanel.Resize += (s, e) =>
                sourceList.Size = new Size(
                    sourcePanel.Width - 20,
                    sourcePanel.Height - 60);

            // ── Progress panel ────────────────────────────────────────
            progPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = C_SURF,
                Padding = new Padding(14, 8, 14, 8),
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
                Location = new Point(14, 8)
            };
            lblProgPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            progBar = new BackupProgressBar(C_BLUE, C_PURPLE)
            {
                Location = new Point(14, 28),
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
                Location = new Point(14, 52)
            };
            progPanel.Controls.AddRange(new Control[]
                { lblProgText, lblProgPct, progBar, lblProgSub });
            progPanel.Resize += (s, e) =>
            {
                progBar.Size = new Size(progPanel.Width - 72, 16);
                lblProgPct.Location = new Point(progPanel.Width - 54, 26);
            };

            // ── Bottom status bar ─────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };

            lblStatus = new Label
            {
                Text = "Add files or folders, then click Start Backup.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnStartBackup = MakeBtn("🗄  Start Backup", C_BLUE, new Size(165, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnOpenDest = MakeBtn("📁  Open Folder", C_GREEN, new Size(145, 34));

            btnCancel.Enabled = false;

            btnStartBackup.Click += BtnStartBackup_Click;
            btnCancel.Click += BtnCancel_Click;
            btnOpenDest.Click += (s, e) =>
            {
                string dest = txtDest.Text.Trim();
                if (!Directory.Exists(dest))
                    try { Directory.CreateDirectory(dest); } catch { }
                try { Process.Start("explorer.exe", dest); } catch { }
            };

            bottomBar.Controls.AddRange(new Control[]
                { lblStatus, btnStartBackup, btnCancel, btnOpenDest });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnStartBackup.Location = new Point(14, y);
                btnCancel.Location = new Point(191, y);
                btnOpenDest.Location = new Point(303, y);
                lblStatus.Location = new Point(462,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Tab control (Log + History) ───────────────────────────
            tabBottom = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = C_BG
            };
            tabBottom.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabBottom.DrawItem += DrawTab;

            BuildLogTab();
            BuildHistoryTab();

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(tabBottom);    // Fill — first
            Controls.Add(progPanel);    // Top
            Controls.Add(sourcePanel);  // Top
            Controls.Add(optPanel);     // Top
            Controls.Add(topBar);       // Top — topmost
            Controls.Add(bottomBar);    // Bottom

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - backupStart;
                lblProgSub.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  LOG TAB — assigns class-level logList (no redeclaration)
        // ════════════════════════════════════════════════════════════
        void BuildLogTab()
        {
            var page = new TabPage("  📋  Backup Log  ");
            page.BackColor = C_BG;

            logPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            // Assign class-level field — NO 'ListView' type keyword
            logList = new ListView
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
                OwnerDraw = true
            };
            logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;

            logList.Columns.Add("Time", 170);
            logList.Columns.Add("Event", 250);
            logList.Columns.Add("Detail", 698);

            logList.DrawColumnHeader += DrawLogHeader;
            logList.DrawItem += (s, e) => { };
            logList.DrawSubItem += DrawLogRow;

            logPanel.Controls.Add(logList);
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 20, logPanel.Height - 12);

            page.Controls.Add(logPanel);
            tabBottom.TabPages.Add(page);
        }

        // ════════════════════════════════════════════════════════════
        //  HISTORY TAB — assigns class-level historyList (no redecl)
        // ════════════════════════════════════════════════════════════
        void BuildHistoryTab()
        {
            var page = new TabPage("  🕓  Backup History  ");
            page.BackColor = C_BG;

            var hPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            btnClearHistory = MakeSmallBtn("🗑  Clear History", C_SUB, new Size(130, 24));
            btnClearHistory.Location = new Point(0, 0);
            btnClearHistory.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearHistory.Click += (s, e) => historyList.Items.Clear();

            // Assign class-level field — NO 'ListView' type keyword
            historyList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 30),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            historyList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                               | AnchorStyles.Left | AnchorStyles.Right;

            historyList.Columns.Add("Date / Time", 80);
            historyList.Columns.Add("Archive Name", 314);
            historyList.Columns.Add("Format", 80);
            historyList.Columns.Add("Size", 100);
            historyList.Columns.Add("Files", 95);
            historyList.Columns.Add("Duration", 105);
            historyList.Columns.Add("Status", 114);

            historyList.DrawColumnHeader += DrawLogHeader;
            historyList.DrawItem += (s, e) => { };
            historyList.DrawSubItem += DrawHistoryRow;

            hPanel.Controls.AddRange(new Control[] { btnClearHistory, historyList });
            hPanel.Resize += (s, e) =>
            {
                btnClearHistory.Location = new Point(hPanel.Width - 150, 0);
                historyList.Size = new Size(hPanel.Width - 20, hPanel.Height - 36);
            };

            page.Controls.Add(hPanel);
            tabBottom.TabPages.Add(page);
        }

        // ════════════════════════════════════════════════════════════
        //  DETECT WINRAR
        // ════════════════════════════════════════════════════════════
        void DetectWinRAR()
        {
            string[] candidates =
            {
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                    "WinRAR", "WinRAR.exe"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                    "WinRAR", "WinRAR.exe")
            };

            foreach (string p in candidates)
            {
                if (File.Exists(p))
                {
                    winrarFound = true;
                    winrarPath = p;
                    AddLog("Info",
                        string.Format("WinRAR found: {0}", p), C_GREEN);
                    return;
                }
            }

            AddLog("Info",
                "WinRAR not found — ZIP will be used. " +
                "Install WinRAR to enable RAR archives.", C_AMBER);
        }

        // ════════════════════════════════════════════════════════════
        //  ADD FILES / FOLDERS
        // ════════════════════════════════════════════════════════════
        void BtnAddFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Select files to backup",
                Multiselect = true,
                Filter = "All Files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                foreach (string f in dlg.FileNames)
                    AddSourceItem(f, false);
            }
        }

        void BtnAddFolder_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "Select folder to backup"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                AddSourceItem(dlg.SelectedPath, true);
            }
        }

        void AddSourceItem(string path, bool isFolder)
        {
            if (sources.Any(s => s.Path.Equals(path,
                StringComparison.OrdinalIgnoreCase))) return;

            long size = 0;
            try
            {
                if (isFolder)
                    foreach (var f in Directory.GetFiles(path, "*.*",
                        SearchOption.AllDirectories))
                        try { size += new FileInfo(f).Length; } catch { }
                else
                    size = new FileInfo(path).Length;
            }
            catch { }

            var item = new BackupItem
            {
                Path = path,
                IsFolder = isFolder,
                SizeBytes = size,
                SizeStr = FormatSize(size),
                Selected = true
            };
            sources.Add(item);

            var lv = new ListViewItem(isFolder ? "📁" : "📄");
            lv.Checked = true;
            lv.SubItems.Add(path);
            lv.SubItems.Add(isFolder ? "Folder" : "File");
            lv.SubItems.Add(item.SizeStr);
            lv.Tag = item;
            lv.ForeColor = C_TXT;
            sourceList.Items.Add(lv);

            RefreshSourceStats();
            AddLog("Added", path, isFolder ? C_GREEN : C_BLUE);
        }

        void BtnRemove_Click(object sender, EventArgs e)
        {
            var toRemove = new List<ListViewItem>();
            foreach (ListViewItem item in sourceList.SelectedItems)
                toRemove.Add(item);
            foreach (var item in toRemove)
            {
                if (item.Tag is BackupItem bi) sources.Remove(bi);
                sourceList.Items.Remove(item);
            }
            RefreshSourceStats();
        }

        void RefreshSourceStats()
        {
            long total = sources.Where(s => s.Selected).Sum(s => s.SizeBytes);
            int count = sources.Count(s => s.Selected);
            lblTotalSize.Text = string.Format("Total: {0}", FormatSize(total));
            lblFileCount.Text = string.Format("{0} item(s) selected", count);
        }

        // ════════════════════════════════════════════════════════════
        //  SETUP WORKER
        // ════════════════════════════════════════════════════════════
        void SetupWorker()
        {
            backupWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            backupWorker.DoWork += BackupWorker_DoWork;
            backupWorker.ProgressChanged += BackupWorker_Progress;
            backupWorker.RunWorkerCompleted += BackupWorker_Completed;
        }

        // ════════════════════════════════════════════════════════════
        //  START BACKUP
        // ════════════════════════════════════════════════════════════
        void BtnStartBackup_Click(object sender, EventArgs e)
        {
            var selected = sources.Where(s => s.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Please add at least one file or folder to back up.",
                    "Nothing to Backup",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string dest = txtDest.Text.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("Please select a destination folder.",
                    "No Destination", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool useRAR = cbFormat.SelectedIndex == 1;
            if (useRAR && !winrarFound)
            {
                MessageBox.Show(
                    "WinRAR is not installed.\n\n" +
                    "Install WinRAR from https://www.rarlab.com\n" +
                    "or switch to ZIP format.",
                    "WinRAR Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string baseName = txtArchiveName.Text.Trim();
            if (string.IsNullOrEmpty(baseName)) baseName = "Backup";
            if (chkTimestamp.Checked)
                baseName += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string ext = useRAR ? ".rar" : ".zip";
            string archPath = Path.Combine(dest, baseName + ext);

            string preview = string.Join("\n",
                selected.Take(6).Select(s =>
                    "  " + (s.IsFolder ? "📁 " : "📄 ") + s.Path));
            if (selected.Count > 6)
                preview += string.Format("\n  ... and {0} more", selected.Count - 6);

            var confirm = MessageBox.Show(
                string.Format(
                    "Start backup?\n\n" +
                    "Format  : {0}\n" +
                    "Archive : {1}\n" +
                    "Items   : {2}\n" +
                    "Total   : {3}\n\n{4}\n\nContinue?",
                    useRAR ? "RAR (WinRAR)" : "ZIP",
                    archPath,
                    selected.Count,
                    FormatSize(selected.Sum(s => s.SizeBytes)),
                    preview),
                "Confirm Backup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                if (!Directory.Exists(dest))
                    Directory.CreateDirectory(dest);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot create destination folder:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            backupStart = DateTime.Now;
            btnStartBackup.Enabled = false;
            btnCancel.Enabled = true;
            progPanel.Visible = true;
            progBar.Value = 0;
            progBar.Animate = true;
            progBar.SetColors(C_BLUE, C_PURPLE);
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "Creating {0} archive...", useRAR ? "RAR" : "ZIP");
            lblProgPct.Text = "0%";
            lblProgSub.Text = "Starting...";

            logList.Items.Clear();
            AddLog("Started",
                string.Format("{0} → {1}", useRAR ? "RAR" : "ZIP", archPath), C_BLUE);

            elapsedTimer.Start();

            int compLevel = cbCompression.SelectedIndex;

            backupWorker.RunWorkerAsync(new object[]
            {
                selected, archPath, useRAR, compLevel, winrarPath
            });
        }

        // ════════════════════════════════════════════════════════════
        //  BACKUP WORKER
        // ════════════════════════════════════════════════════════════
        void BackupWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as object[];
            var selected = args[0] as List<BackupItem>;
            string arch = args[1] as string;
            bool useRAR = (bool)args[2];
            int comp = (int)args[3];
            string rarExe = args[4] as string;

            int total = selected.Count;
            int done = 0;
            long totalBytes = selected.Sum(s => s.SizeBytes);
            long processedB = 0;

            backupWorker.ReportProgress(0,
                new object[] { "status", "Preparing..." });

            try
            {
                if (useRAR)
                    DoRarBackup(selected, arch, comp, rarExe,
                        ref done, ref processedB, total, totalBytes);
                else
                    DoZipBackup(selected, arch, comp,
                        ref done, ref processedB, total, totalBytes);
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
                try { if (File.Exists(arch)) File.Delete(arch); } catch { }
                return;
            }
            catch (Exception ex)
            {
                e.Result = new object[] { "error", ex.Message };
                return;
            }

            long archSize = 0;
            try { if (File.Exists(arch)) archSize = new FileInfo(arch).Length; } catch { }

            e.Result = new object[]
            {
                "ok", arch, done, total, archSize,
                useRAR ? "RAR" : "ZIP"
            };
        }

        // ── ZIP backup — uses ZipArchive (no FileSystem assembly needed) ──
        void DoZipBackup(List<BackupItem> selected, string archPath,
            int compLevel,
            ref int done, ref long processedB,
            int total, long totalBytes)
        {
            // Map compression level
            CompressionLevel zlevel =
                compLevel == 0 ? CompressionLevel.NoCompression
              : compLevel == 1 ? CompressionLevel.Fastest
              : CompressionLevel.Optimal;   // covers Normal and Best

            // ZipArchive — available in System.IO.Compression (no extra dll)
            using (var fs = new FileStream(archPath, FileMode.Create,
                FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                foreach (var item in selected)
                {
                    if (backupWorker.CancellationPending)
                        throw new OperationCanceledException();

                    if (item.IsFolder)
                    {
                        string[] files;
                        try
                        {
                            files = Directory.GetFiles(item.Path, "*.*",
                                SearchOption.AllDirectories);
                        }
                        catch { done++; continue; }

                        string folderName = Path.GetFileName(
                            item.Path.TrimEnd(Path.DirectorySeparatorChar));

                        foreach (string file in files)
                        {
                            if (backupWorker.CancellationPending)
                                throw new OperationCanceledException();
                            try
                            {
                                string rel = file.Substring(
                                    item.Path.Length).TrimStart('\\', '/');
                                string entryName = folderName + "/" + rel.Replace('\\', '/');

                                AddFileToZip(zip, file, entryName, zlevel);

                                try { processedB += new FileInfo(file).Length; } catch { }
                                int pct = CalcPct(processedB, totalBytes);
                                backupWorker.ReportProgress(pct,
                                    new object[] { "file",
                                        Path.GetFileName(file), pct });
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        if (backupWorker.CancellationPending)
                            throw new OperationCanceledException();
                        try
                        {
                            string entryName = Path.GetFileName(item.Path);
                            AddFileToZip(zip, item.Path, entryName, zlevel);

                            try { processedB += new FileInfo(item.Path).Length; } catch { }
                            int pct = CalcPct(processedB, totalBytes);
                            backupWorker.ReportProgress(pct,
                                new object[] { "file",
                                    Path.GetFileName(item.Path), pct });
                        }
                        catch { }
                    }

                    done++;
                }
            }
        }

        // Helper — add a single file into an open ZipArchive by reading its bytes
        void AddFileToZip(ZipArchive zip, string filePath,
            string entryName, CompressionLevel level)
        {
            var entry = zip.CreateEntry(entryName, level);
            entry.LastWriteTime = File.GetLastWriteTime(filePath);
            using (var es = entry.Open())
            using (var fs = new FileStream(filePath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buf = new byte[81920]; // 80 KB buffer
                int read;
                while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    if (backupWorker.CancellationPending)
                        throw new OperationCanceledException();
                    es.Write(buf, 0, read);
                }
            }
        }

        // ── RAR backup — delegates to WinRAR.exe via command line ────────
        void DoRarBackup(List<BackupItem> selected, string archPath,
            int compLevel, string rarExe,
            ref int done, ref long processedB,
            int total, long totalBytes)
        {
            string mLevel = compLevel == 0 ? "0"
                          : compLevel == 1 ? "1"
                          : compLevel == 2 ? "3"
                          : "5";

            string listFile = Path.Combine(Path.GetTempPath(),
                "ttk_backup_" + DateTime.Now.Ticks + ".txt");

            try
            {
                File.WriteAllLines(listFile,
                    selected.Select(s => s.Path));

                string rarArgs = string.Format(
                    "a -r -ep1 -m{0} -ibck \"{1}\" @\"{2}\"",
                    mLevel, archPath, listFile);

                backupWorker.ReportProgress(5,
                    new object[] { "status", "Running WinRAR..." });

                var psi = new ProcessStartInfo
                {
                    FileName = rarExe,
                    Arguments = rarArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var proc = new Process { StartInfo = psi };
                proc.Start();

                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (backupWorker.CancellationPending)
                    {
                        try { proc.Kill(); } catch { }
                        throw new OperationCanceledException();
                    }
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        done++;
                        int pct = Math.Min(95,
                            (int)(done * 90.0 / Math.Max(total, 1)));
                        backupWorker.ReportProgress(pct,
                            new object[] { "rar_line", line.Trim(), pct });
                    }
                }

                proc.WaitForExit(600000);

                if (proc.ExitCode != 0 && proc.ExitCode != 1)
                    throw new Exception(string.Format(
                        "WinRAR exited with code {0}", proc.ExitCode));

                done = total;
            }
            finally
            {
                try { File.Delete(listFile); } catch { }
            }
        }

        static int CalcPct(long processed, long total) =>
            total <= 0 ? 0 : (int)Math.Min(99, processed * 100L / total);

        // ════════════════════════════════════════════════════════════
        //  WORKER EVENTS
        // ════════════════════════════════════════════════════════════
        void BackupWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            if (type == "status")
            {
                lblProgSub.Text = d[1] as string;
            }
            else if (type == "file" || type == "rar_line")
            {
                string name = d[1] as string ?? "";
                int pct = d.Length > 2 ? Convert.ToInt32(d[2]) : 0;
                progBar.Value = pct;
                lblProgPct.Text = pct + "%";
                lblProgSub.Text = "Compressing: " +
                    (name.Length > 70 ? "..." + name.Substring(name.Length - 67) : name);
                AddLog("Adding", name, C_SUB);
            }
        }

        void BackupWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            elapsedTimer.Stop();
            btnStartBackup.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - backupStart;
            string elapsed = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Backup cancelled.");
                AddLog("Cancelled", "Backup cancelled by user.", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null) return;

            if ((string)res[0] == "error")
            {
                string err = res[1] as string;
                progBar.SetColors(C_RED, C_AMBER);
                progBar.Animate = false;
                progBar.Value = 100;
                progBar.Invalidate();
                lblProgText.Text = "✖  Backup failed";
                lblProgPct.Text = "Error";
                SetStatus("✖  Backup failed: " + err);
                AddLog("Error", err, C_RED);
                MessageBox.Show("Backup failed:\n\n" + err,
                    "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string archPath = res[1] as string;
            int doneFiles = (int)res[2];
            int totalFiles = (int)res[3];
            long archSize = (long)res[4];
            string fmt = res[5] as string;

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "✔  Backup complete — {0}", Path.GetFileName(archPath));
            lblProgSub.Text = string.Format(
                "Duration: {0}  |  Archive: {1}", elapsed, FormatSize(archSize));

            SetStatus(string.Format(
                "✔  Backup complete in {0}. Saved to: {1}", elapsed, archPath));
            AddLog("Complete",
                string.Format("{0} items  ·  {1}  ·  {2}",
                    doneFiles, FormatSize(archSize), elapsed), C_GREEN);

            AddHistory(archPath, fmt, archSize, doneFiles, elapsed, true);

            if (chkOpenAfter.Checked)
                try
                {
                    Process.Start("explorer.exe",
                        Path.GetDirectoryName(archPath));
                }
                catch { }
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (backupWorker.IsBusy)
                backupWorker.CancelAsync();
            btnCancel.Enabled = false;
            SetStatus("Cancelling...");
        }

        // ════════════════════════════════════════════════════════════
        //  HISTORY
        // ════════════════════════════════════════════════════════════
        void AddHistory(string archPath, string fmt, long size,
            int files, string duration, bool success)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                    AddHistory(archPath, fmt, size, files, duration, success)));
                return;
            }
            var item = new ListViewItem(
                DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss"));
            item.SubItems.Add(Path.GetFileName(archPath));
            item.SubItems.Add(fmt);
            item.SubItems.Add(FormatSize(size));
            item.SubItems.Add(files.ToString());
            item.SubItems.Add(duration);
            item.SubItems.Add(success ? "✔  Success" : "✖  Failed");
            item.ForeColor = success ? C_GREEN : C_RED;
            item.Tag = success ? "ok" : "fail";
            historyList.Items.Insert(0, item);
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
            item.Tag = fg == C_GREEN ? "ok"
                           : fg == C_RED ? "fail" : "info";
            logList.Items.Add(item);
            logList.EnsureVisible(logList.Items.Count - 1);
        }

        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("✖") ? C_RED : C_SUB;
        }

        string FormatSize(long bytes)
        {
            if (bytes <= 0) return "–";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.0} KB", bytes / 1024.0);
            if (bytes < 1024L * 1024 * 1024) return string.Format("{0:0.0} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:0.00} GB", bytes / (1024.0 * 1024 * 1024));
        }

        Label MakeOptLabel(string text, Point loc) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = C_SUB,
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
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, accent.R, accent.G, accent.B);
            return b;
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW
        // ════════════════════════════════════════════════════════════
        void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tc = sender as TabControl;
            var page = tc.TabPages[e.Index];
            bool sel = e.Index == tc.SelectedIndex;
            using (var br = new SolidBrush(sel ? C_SURF : C_BG))
                e.Graphics.FillRectangle(br, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(sel ? C_TXT : C_SUB))
                e.Graphics.DrawString(page.Text,
                    new Font("Segoe UI Semibold", 8.5f), br, e.Bounds, sf);
            if (sel)
                using (var p = new Pen(C_BLUE, 2))
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                        e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawSourceHeader(object sender, DrawListViewColumnHeaderEventArgs e)
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

        void DrawSourceRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var bi = e.Item.Tag as BackupItem;
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);
            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));
            Color fg = e.ColumnIndex == 0 ? C_TXT
                     : e.ColumnIndex == 1 ? (bi != null && bi.IsFolder ? C_GREEN : C_TXT)
                     : e.ColumnIndex == 2 ? (bi != null && bi.IsFolder ? C_TEAL : C_BLUE)
                     : C_AMBER;
            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 1
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, sourceList.Font, br,
                    new Rectangle(e.Bounds.X + 4, e.Bounds.Y,
                        e.Bounds.Width - 6, e.Bounds.Height), sf);
        }

        void DrawLogHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(24, 30, 38)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat
            { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 7.5f))
            using (var br = new SolidBrush(C_SUB))
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 6, e.Bounds.Height), sf);
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawLogRow(object sender, DrawListViewSubItemEventArgs e)
        {
            string tag = e.Item.Tag as string ?? "";
            var lv = sender as ListView ?? logList;
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);
            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1
                        ? (tag == "ok" ? C_GREEN : tag == "fail" ? C_RED : C_AMBER)
                     : e.Item.ForeColor;
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, lv.Font, br,
                    new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
        }

        void DrawHistoryRow(object sender, DrawListViewSubItemEventArgs e)
        {
            string tag = e.Item.Tag as string ?? "";
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);
            Color fg = e.ColumnIndex == 0 ? C_SUB
                     : e.ColumnIndex == 1 ? C_TXT
                     : e.ColumnIndex == 2 ? C_BLUE
                     : e.ColumnIndex == 3 ? C_AMBER
                     : e.ColumnIndex == 4 ? C_SUB
                     : e.ColumnIndex == 5 ? C_SUB
                     : (tag == "ok" ? C_GREEN : C_RED);
            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 1
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, historyList.Font, br,
                    new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (backupWorker != null && backupWorker.IsBusy)
                backupWorker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  BACKUP PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class BackupProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public BackupProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bgBackColor = Color.Transparent;
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