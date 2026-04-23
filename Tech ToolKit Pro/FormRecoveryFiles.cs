using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormRecoveryFiles : Form
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
        //  RECOVERED FILE MODEL
        // ════════════════════════════════════════════════════════════
        class RecoveredFile
        {
            public string FileName { get; set; }
            public string Extension { get; set; }
            public string OriginalPath { get; set; }
            public long SizeBytes { get; set; }
            public string SizeStr { get; set; }
            public string DeletedOn { get; set; }
            public string Source { get; set; }
            public string Recoverability { get; set; }
            public Color RecovColor { get; set; }
            public bool Selected { get; set; } = true;
            public string FullPath { get; set; }
            public ListViewItem LvItem { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  WIN32
        // ════════════════════════════════════════════════════════════
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll")]
        static extern int SHQueryRecycleBin(
            string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, optPanel, progPanel, listPanel, logPanel, bottomBar;
        Label lblTitle, lblStatus;
        Label lblFound, lblSelected, lblTotalSize;
        Label lblProgText, lblProgPct, lblProgSub;
        RecoverProgressBar progBar;
        ListView fileList, logList;
        Button btnScan, btnRecover, btnRecoverSelected,
                 btnCancel, btnOpenDest, btnClearLog;
        ComboBox cbDrive;
        TextBox txtDestination, txtFilter;
        CheckBox chkRecycleBin, chkShadow, chkBackup, chkTypeFilter;
        BackgroundWorker scanWorker, recoverWorker;
        System.Windows.Forms.Timer elapsedTimer;
        DateTime opStart;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<RecoveredFile> allFiles = new List<RecoveredFile>();
        bool scanning = false;
        bool recovering = false;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormRecoveryFiles()
        {
            BuildUI();        // 1 — build all controls first
            SetupWorkers();   // 2 — wire background workers
            PopulateDrives(); // 3 — fill the drive combo

            // 4 — banner AFTER BuildUI so Controls collection exists
            AdminHelper.ShowAdminBanner(this,
                "⚠  Shadow Copies and Windows Backup scans require " +
                "Administrator rights. Click 'Restart as Admin' to unlock them.");
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Recover Files";
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
            lblTitle = new Label
            {
                Text = "🔄  FILE  RECOVERY  CENTER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(new Label
            {
                Text = "Scan · Recover from Recycle Bin · Shadow Copies · Backup",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            });

            // ── Options panel ─────────────────────────────────────────
            optPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = C_SURF,
                Padding = new Padding(14, 10, 14, 10)
            };
            optPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, optPanel.Height - 1,
                        optPanel.Width, optPanel.Height - 1);
            };

            optPanel.Controls.Add(MakeOptLabel("Scan Drive:", new Point(14, 12)));
            cbDrive = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(94, 10),
                Size = new Size(100, 26)
            };
            optPanel.Controls.Add(cbDrive);

            optPanel.Controls.Add(MakeOptLabel("Recover To:", new Point(218, 12)));
            txtDestination = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(306, 10),
                Size = new Size(280, 26),
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Recovered")
            };
            optPanel.Controls.Add(txtDestination);

            var btnBrowse = MakeSmallBtn("📁  Browse", C_BLUE, new Size(90, 26));
            btnBrowse.Location = new Point(594, 10);
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog
                {
                    Description = "Select recovery destination folder",
                    SelectedPath = txtDestination.Text
                })
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtDestination.Text = dlg.SelectedPath;
            };
            optPanel.Controls.Add(btnBrowse);

            optPanel.Controls.Add(MakeOptLabel("File Filter:", new Point(14, 50)));
            chkTypeFilter = new CheckBox
            {
                Text = "Enable",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(88, 52),
                BackColor = Color.Transparent
            };
            optPanel.Controls.Add(chkTypeFilter);

            txtFilter = new TextBox
            {
                Font = new Font("Segoe UI", 9f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(152, 50),
                Size = new Size(250, 26),
                Text = "*.jpg;*.png;*.doc;*.docx;*.pdf;*.mp4;*.zip",
                Enabled = false
            };
            optPanel.Controls.Add(txtFilter);
            chkTypeFilter.CheckedChanged += (s, e) =>
                txtFilter.Enabled = chkTypeFilter.Checked;

            optPanel.Controls.Add(new Label
            {
                Text = "← separate extensions with semicolons",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(410, 54)
            });

            optPanel.Controls.Add(MakeOptLabel("Sources:", new Point(14, 90)));
            chkRecycleBin = MakeSourceChk("🗑  Recycle Bin", C_AMBER, new Point(88, 88), true);
            chkShadow = MakeSourceChk("💾  Shadow Copies", C_BLUE, new Point(240, 88), true);
            chkBackup = MakeSourceChk("📦  Windows Backup", C_GREEN, new Point(392, 88), true);
            optPanel.Controls.Add(chkRecycleBin);
            optPanel.Controls.Add(chkShadow);
            optPanel.Controls.Add(chkBackup);
            optPanel.Controls.Add(new Label
            {
                Text = "ℹ  Admin rights required for Shadow Copies and Windows Backup",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(80, 139, 148, 158),
                AutoSize = true,
                Location = new Point(550, 92)
            });

            // ── Stats bar ─────────────────────────────────────────────
            var statsBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(16, 22, 30)
            };
            statsBar.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(
                    Color.FromArgb(10, C_TEAL.R, C_TEAL.G, C_TEAL.B)))
                    e.Graphics.FillRectangle(br, 0, 0,
                        statsBar.Width, statsBar.Height);
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, statsBar.Height - 1,
                        statsBar.Width, statsBar.Height - 1);
            };

            lblFound = MakeStat("0 files found", C_TEAL, new Point(14, 11));
            lblSelected = MakeStat("0 selected", C_GREEN, new Point(160, 11));
            lblTotalSize = MakeStat("0 B total", C_AMBER, new Point(290, 11));

            var txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 8.5f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(190, 22),
                Text = "Search files...",
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            txtSearch.GotFocus += (s, e) =>
            { if (txtSearch.Text == "Search files...") txtSearch.Text = ""; };
            txtSearch.LostFocus += (s, e) =>
            { if (txtSearch.Text == "") txtSearch.Text = "Search files..."; };
            txtSearch.TextChanged += (s, e) => ApplySearch(txtSearch.Text);

            var btnSelAll = MakeSmallBtn("☑ All", C_BLUE, new Size(56, 22));
            var btnSelNone = MakeSmallBtn("☐ None", C_SUB, new Size(56, 22));
            btnSelAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelNone.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelAll.Click += (s, e) => ToggleAll(true);
            btnSelNone.Click += (s, e) => ToggleAll(false);

            statsBar.Controls.AddRange(new Control[]
                { lblFound, lblSelected, lblTotalSize, txtSearch, btnSelAll, btnSelNone });
            statsBar.Resize += (s, e) =>
            {
                txtSearch.Location = new Point(statsBar.Width - 320, 9);
                btnSelAll.Location = new Point(statsBar.Width - 122, 9);
                btnSelNone.Location = new Point(statsBar.Width - 60, 9);
            };

            // ── Progress panel ────────────────────────────────────────
            progPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
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
            progBar = new RecoverProgressBar(C_TEAL, C_BLUE)
            {
                Location = new Point(14, 28),
                Size = new Size(100, 14),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lblProgSub = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(14, 50)
            };
            progPanel.Controls.AddRange(new Control[]
                { lblProgText, lblProgPct, progBar, lblProgSub });
            progPanel.Resize += (s, e) =>
            {
                progBar.Size = new Size(progPanel.Width - 72, 14);
                lblProgPct.Location = new Point(progPanel.Width - 52, 26);
            };

            // ── File list ─────────────────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 6, 10, 6)
            };

            fileList = new ListView
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
            fileList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left | AnchorStyles.Right;

            fileList.Columns.Add("", 22);
            fileList.Columns.Add("File Name", 230);
            fileList.Columns.Add("Type", 110);
            fileList.Columns.Add("Original Path", 220);
            fileList.Columns.Add("Size", 124);
            fileList.Columns.Add("Deleted On", 158);
            fileList.Columns.Add("Source", 152);
            fileList.Columns.Add("Recoverability", 110);

            fileList.DrawColumnHeader += DrawFileHeader;
            fileList.DrawItem += (s, e) => { };
            fileList.DrawSubItem += DrawFileRow;
            fileList.ItemChecked += (s, e) => UpdateCounts();
            fileList.ColumnClick += FileList_ColumnClick;

            listPanel.Controls.Add(fileList);
            listPanel.Resize += (s, e) =>
                fileList.Size = new Size(listPanel.Width - 20, listPanel.Height - 12);

            // ── Log panel ─────────────────────────────────────────────
            logPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 110,
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
                Text = "RECOVERY LOG",
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

            logList.Columns.Add("Time", 115);
            logList.Columns.Add("File", 450);
            logList.Columns.Add("Action", 201);
            logList.Columns.Add("Result", 360);

            logList.DrawColumnHeader += DrawLogHeader;
            logList.DrawItem += (s2, e2) => { };
            logList.DrawSubItem += DrawLogRow;

            logPanel.Controls.AddRange(new Control[] { lblLog, logList });
            logPanel.Resize += (s, e) =>
                logList.Size = new Size(logPanel.Width - 20, logPanel.Height - 26);

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };

            lblStatus = new Label
            {
                Text = "Select a drive & sources, then click Scan.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true
            };

            btnScan = MakeBtn("🔍  Scan Drive", C_BLUE, new Size(155, 34));
            btnRecover = MakeBtn("🔄  Recover All", C_GREEN, new Size(150, 34));
            btnRecoverSelected = MakeBtn("☑  Recover Selected", C_TEAL, new Size(170, 34));
            btnCancel = MakeBtn("✕  Cancel", C_SUB, new Size(100, 34));
            btnOpenDest = MakeBtn("📁  Open Destination", C_PURPLE, new Size(165, 34));
            btnClearLog = MakeBtn("🗑  Clear Log", C_SUB, new Size(110, 34));

            btnRecover.Enabled = false;
            btnRecoverSelected.Enabled = false;
            btnCancel.Enabled = false;

            btnScan.Click += BtnScan_Click;
            btnRecover.Click += (s, e) => StartRecover(false);
            btnRecoverSelected.Click += (s, e) => StartRecover(true);
            btnCancel.Click += BtnCancel_Click;
            btnOpenDest.Click += (s, e) =>
            {
                string dest = txtDestination.Text;
                if (!Directory.Exists(dest))
                    try { Directory.CreateDirectory(dest); } catch { }
                try { Process.Start("explorer.exe", dest); } catch { }
            };
            btnClearLog.Click += (s, e) => logList.Items.Clear();

            bottomBar.Controls.AddRange(new Control[]
            {
                lblStatus, btnScan, btnRecover, btnRecoverSelected,
                btnCancel, btnOpenDest, btnClearLog
            });
            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                btnScan.Location = new Point(14, y);
                btnRecover.Location = new Point(181, y);
                btnRecoverSelected.Location = new Point(343, y);
                btnCancel.Location = new Point(525, y);
                btnOpenDest.Location = new Point(637, y);
                btnClearLog.Location = new Point(814, y);
                lblStatus.Location = new Point(940,
                    (bottomBar.Height - lblStatus.Height) / 2);
            };

            // ── Assemble (Fill → Bottom → Top) ────────────────────────
            Controls.Add(listPanel);   // Fill  — must be first
            Controls.Add(progPanel);   // Top
            Controls.Add(statsBar);    // Top
            Controls.Add(optPanel);    // Top
            Controls.Add(topBar);      // Top   — topmost
            Controls.Add(logPanel);    // Bottom
            Controls.Add(bottomBar);   // Bottom

            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) =>
            {
                var ts = DateTime.Now - opStart;
                lblProgSub.Text = string.Format("Elapsed: {0:D2}:{1:D2}:{2:D2}",
                    (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            };
        }

        // ════════════════════════════════════════════════════════════
        //  POPULATE DRIVES
        // ════════════════════════════════════════════════════════════
        void PopulateDrives()
        {
            cbDrive.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.Name.TrimEnd('\\')
                    : string.Format("{0} ({1})",
                        drive.Name.TrimEnd('\\'), drive.VolumeLabel);
                cbDrive.Items.Add(label);
            }
            if (cbDrive.Items.Count > 0)
                cbDrive.SelectedIndex = 0;
        }

        string SelectedDriveLetter()
        {
            if (cbDrive.SelectedItem == null) return "C:";
            return cbDrive.SelectedItem.ToString().Substring(0, 2);
        }

        // ════════════════════════════════════════════════════════════
        //  SETUP WORKERS
        // ════════════════════════════════════════════════════════════
        void SetupWorkers()
        {
            scanWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            scanWorker.DoWork += ScanWorker_DoWork;
            scanWorker.ProgressChanged += ScanWorker_Progress;
            scanWorker.RunWorkerCompleted += ScanWorker_Completed;

            recoverWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            recoverWorker.DoWork += RecoverWorker_DoWork;
            recoverWorker.ProgressChanged += RecoverWorker_Progress;
            recoverWorker.RunWorkerCompleted += RecoverWorker_Completed;
        }

        // ════════════════════════════════════════════════════════════
        //  SCAN BUTTON
        // ════════════════════════════════════════════════════════════
        void BtnScan_Click(object sender, EventArgs e)
        {
            if (scanning || recovering) return;

            bool anySource = chkRecycleBin.Checked
                          || chkShadow.Checked
                          || chkBackup.Checked;
            if (!anySource)
            {
                MessageBox.Show("Please select at least one scan source.",
                    "No Source Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool needsAdmin = chkShadow.Checked || chkBackup.Checked;
            if (needsAdmin && !AdminHelper.EnsureAdmin("File Recovery Scan"))
                return;

            allFiles.Clear();
            fileList.Items.Clear();
            logList.Items.Clear();

            scanning = true;
            btnScan.Enabled = false;
            btnRecover.Enabled = false;
            btnRecoverSelected.Enabled = false;
            btnCancel.Enabled = true;
            progPanel.Visible = true;
            progBar.Animate = true;
            progBar.Value = 0;
            progBar.SetColors(C_TEAL, C_BLUE);
            progBar.Invalidate();
            lblProgText.Text = "Scanning for recoverable files...";
            lblProgPct.Text = "...";
            lblProgSub.Text = "Initialising scan";

            opStart = DateTime.Now;
            elapsedTimer.Start();

            SetStatus("Scanning...");
            AddLog("Scan", "Starting scan on drive " + SelectedDriveLetter(), "INFO", C_BLUE);

            scanWorker.RunWorkerAsync(new object[]
            {
                SelectedDriveLetter(),
                chkRecycleBin.Checked,
                chkShadow.Checked,
                chkBackup.Checked,
                chkTypeFilter.Checked ? txtFilter.Text : ""
            });
        }

        // ════════════════════════════════════════════════════════════
        //  SCAN WORKER — DO WORK
        // ════════════════════════════════════════════════════════════
        void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as object[];
            string drive = args[0] as string;
            bool recycle = (bool)args[1];
            bool shadow = (bool)args[2];
            bool backup = (bool)args[3];
            string filter = args[4] as string ?? "";

            var extensions = filter.Length > 0
                ? filter.Split(';')
                    .Select(x => x.Trim().TrimStart('*').ToLower())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList()
                : new List<string>();

            int total = (recycle ? 1 : 0) + (shadow ? 1 : 0) + (backup ? 1 : 0);
            int phase = 0;

            if (recycle && !scanWorker.CancellationPending)
            {
                phase++;
                scanWorker.ReportProgress(
                    (int)((double)(phase - 1) / total * 100),
                    new object[] { "status", "Scanning Recycle Bin...", 0 });
                ScanRecycleBin(drive, extensions, scanWorker, e);
            }

            if (shadow && !scanWorker.CancellationPending)
            {
                phase++;
                scanWorker.ReportProgress(
                    (int)((double)(phase - 1) / total * 100),
                    new object[] { "status", "Scanning Shadow Copies (VSS)...", 0 });
                ScanShadowCopies(drive, extensions, scanWorker, e);
            }

            if (backup && !scanWorker.CancellationPending)
            {
                phase++;
                scanWorker.ReportProgress(
                    (int)((double)(phase - 1) / total * 100),
                    new object[] { "status", "Scanning Windows Backup...", 0 });
                ScanWindowsBackup(drive, extensions, scanWorker, e);
            }

            e.Result = "ok";
        }

        // ── Scan Recycle Bin ─────────────────────────────────────────
        void ScanRecycleBin(string drive, List<string> exts,
            BackgroundWorker bw, DoWorkEventArgs e)
        {
            try
            {
                string rbPath = drive + "\\$Recycle.Bin";
                if (!Directory.Exists(rbPath)) return;

                string[] files;
                try { files = Directory.GetFiles(rbPath, "*.*", SearchOption.AllDirectories); }
                catch { return; }

                foreach (string file in files)
                {
                    if (bw.CancellationPending) { e.Cancel = true; return; }
                    try
                    {
                        var fi = new FileInfo(file);
                        string fn = fi.Name;

                        if (fn.StartsWith("$I", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string originalName = fn;
                        string originalPath = file;
                        DateTime deletedOn = fi.LastWriteTime;

                        string metaFile = Path.Combine(
                            fi.DirectoryName ?? "",
                            "$I" + fn.Substring(2));

                        if (File.Exists(metaFile))
                        {
                            try
                            {
                                byte[] meta = File.ReadAllBytes(metaFile);
                                if (meta.Length > 24)
                                {
                                    long ft = BitConverter.ToInt64(meta, 8);
                                    if (ft > 0)
                                        deletedOn = DateTime.FromFileTimeUtc(ft).ToLocalTime();

                                    string orig = Encoding.Unicode
                                        .GetString(meta, 24, meta.Length - 26)
                                        .TrimEnd('\0');
                                    if (!string.IsNullOrWhiteSpace(orig))
                                    {
                                        originalPath = orig;
                                        originalName = Path.GetFileName(orig);
                                    }
                                }
                            }
                            catch { }
                        }

                        string ext = Path.GetExtension(originalName).ToLower();
                        if (exts.Count > 0 && !exts.Contains(ext)) continue;

                        var rf = new RecoveredFile
                        {
                            FileName = originalName,
                            Extension = ext.TrimStart('.').ToUpper(),
                            OriginalPath = originalPath,
                            SizeBytes = fi.Length,
                            SizeStr = FormatSize(fi.Length),
                            DeletedOn = deletedOn.ToString("dd/MM/yyyy HH:mm"),
                            Source = "Recycle Bin",
                            Recoverability = "Good",
                            RecovColor = C_GREEN,
                            FullPath = file,
                            Selected = true
                        };

                        bw.ReportProgress(0, new object[] { "found", rf });
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ── Scan Shadow Copies ────────────────────────────────────────
        void ScanShadowCopies(string drive, List<string> exts,
            BackgroundWorker bw, DoWorkEventArgs e)
        {
            try
            {
                int exitCode = 0;
                string output = AdminHelper.RunCommand(
                    "vssadmin",
                    "list shadows /for=" + drive + "\\",
                    out exitCode, 30000);

                if (string.IsNullOrWhiteSpace(output)) return;

                var shadowPaths = new List<string>();
                foreach (string line in output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Contains("GLOBALROOT"))
                    {
                        int idx = trimmed.IndexOf("\\\\?\\", StringComparison.Ordinal);
                        if (idx >= 0)
                            shadowPaths.Add(trimmed.Substring(idx).Trim());
                    }
                }

                bw.ReportProgress(0, new object[]
                {
                    "status",
                    string.Format("Found {0} shadow copy snapshot(s)", shadowPaths.Count), 0
                });

                foreach (string sp in shadowPaths)
                {
                    if (bw.CancellationPending) { e.Cancel = true; return; }
                    try
                    {
                        string mountPoint = sp.TrimEnd('\\') + "\\";
                        string[] files;
                        try { files = Directory.GetFiles(mountPoint, "*.*", SearchOption.AllDirectories); }
                        catch { continue; }

                        foreach (string file in files)
                        {
                            if (bw.CancellationPending) { e.Cancel = true; return; }
                            try
                            {
                                string ext = Path.GetExtension(file).ToLower();
                                if (exts.Count > 0 && !exts.Contains(ext)) continue;

                                string relative = file.Replace(mountPoint, "");
                                string livePath = Path.Combine(drive + "\\", relative);
                                if (File.Exists(livePath)) continue;

                                var fi = new FileInfo(file);
                                var rf = new RecoveredFile
                                {
                                    FileName = fi.Name,
                                    Extension = ext.TrimStart('.').ToUpper(),
                                    OriginalPath = livePath,
                                    SizeBytes = fi.Length,
                                    SizeStr = FormatSize(fi.Length),
                                    DeletedOn = fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"),
                                    Source = "Shadow Copy",
                                    Recoverability = "Good",
                                    RecovColor = C_GREEN,
                                    FullPath = file,
                                    Selected = true
                                };
                                bw.ReportProgress(0, new object[] { "found", rf });
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ── Scan Windows Backup ───────────────────────────────────────
        void ScanWindowsBackup(string drive, List<string> exts,
            BackgroundWorker bw, DoWorkEventArgs e)
        {
            var backupPaths = new List<string>
            {
                Path.Combine(drive + "\\", "WindowsImageBackup"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile), "FileHistory"),
                @"\\localhost\Backup"
            };

            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                backupPaths.Add(Path.Combine(d.Name, "WindowsImageBackup"));
                backupPaths.Add(Path.Combine(d.Name, "FileHistory"));
            }

            foreach (string bp in backupPaths)
            {
                if (bw.CancellationPending) { e.Cancel = true; return; }
                if (!Directory.Exists(bp)) continue;

                bw.ReportProgress(0, new object[]
                    { "status", "Scanning backup: " + bp, 0 });

                try
                {
                    string[] files;
                    try { files = Directory.GetFiles(bp, "*.*", SearchOption.AllDirectories); }
                    catch { continue; }

                    foreach (string file in files)
                    {
                        if (bw.CancellationPending) { e.Cancel = true; return; }
                        try
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (exts.Count > 0 && !exts.Contains(ext)) continue;

                            if (ext == ".xml" || ext == ".wbcat" ||
                                ext == ".vhd" || ext == ".vhdx" ||
                                ext == ".mrimg") continue;

                            var fi = new FileInfo(file);
                            var rf = new RecoveredFile
                            {
                                FileName = fi.Name,
                                Extension = ext.TrimStart('.').ToUpper(),
                                OriginalPath = file,
                                SizeBytes = fi.Length,
                                SizeStr = FormatSize(fi.Length),
                                DeletedOn = fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"),
                                Source = "Windows Backup",
                                Recoverability = "Good",
                                RecovColor = C_GREEN,
                                FullPath = file,
                                Selected = true
                            };
                            bw.ReportProgress(0, new object[] { "found", rf });
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SCAN PROGRESS + COMPLETED
        // ════════════════════════════════════════════════════════════
        void ScanWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            if (type == "status")
            {
                lblProgText.Text = d[1] as string;
                int p = d.Length > 2 ? Convert.ToInt32(d[2]) : 0;
                if (p > 0) { progBar.Value = p; lblProgPct.Text = p + "%"; }
            }
            else if (type == "found")
            {
                var rf = d[1] as RecoveredFile;
                if (rf == null) return;
                allFiles.Add(rf);
                AddFileToList(rf);
                UpdateCounts();
                lblProgSub.Text = "Found: " + rf.FileName;
            }
        }

        void ScanWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            scanning = false;
            progBar.Animate = false;
            elapsedTimer.Stop();
            btnScan.Enabled = true;
            btnCancel.Enabled = false;

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Scan cancelled.");
                AddLog("Scan", "Cancelled by user.", "INFO", C_AMBER);
                return;
            }

            var ts = DateTime.Now - opStart;
            string el = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Invalidate();

            if (allFiles.Count == 0)
            {
                lblProgText.Text = "✔  Scan complete — No recoverable files found.";
                lblProgSub.Text = string.Format("Duration: {0}", el);
                SetStatus("✔  No recoverable files found on this drive.");
                AddLog("Scan", "Complete — no files found.", "INFO", C_GREEN);
                return;
            }

            lblProgText.Text = string.Format(
                "✔  Scan complete — {0} recoverable file(s) found", allFiles.Count);
            lblProgSub.Text = string.Format("Duration: {0}", el);

            btnRecover.Enabled = true;
            btnRecoverSelected.Enabled = true;

            SetStatus(string.Format(
                "✔  Found {0} recoverable file(s) in {1}. Ready to recover.",
                allFiles.Count, el));
            AddLog("Scan", string.Format(
                "Complete — {0} file(s) found in {1}", allFiles.Count, el), "INFO", C_GREEN);

            UpdateCounts();
        }

        // ════════════════════════════════════════════════════════════
        //  RECOVER
        // ════════════════════════════════════════════════════════════
        void StartRecover(bool selectedOnly)
        {
            if (recovering || scanning) return;

            List<RecoveredFile> toRecover = selectedOnly
                ? allFiles.Where(f => f.Selected).ToList()
                : new List<RecoveredFile>(allFiles);

            if (toRecover.Count == 0)
            {
                MessageBox.Show(
                    selectedOnly
                        ? "No files selected.\nCheck the checkboxes in the list."
                        : "No files to recover.",
                    "Nothing to Recover",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!AdminHelper.EnsureAdmin("File Recovery")) return;

            string dest = txtDestination.Text.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("Please specify a recovery destination folder.",
                    "No Destination", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string preview = string.Join("\n",
                toRecover.Take(8).Select(f => "  • " + f.FileName));
            if (toRecover.Count > 8)
                preview += string.Format("\n  ... and {0} more", toRecover.Count - 8);

            var confirm = MessageBox.Show(
                string.Format(
                    "Recover {0} file(s) to:\n  {1}\n\n{2}\n\nTotal: {3}\nContinue?",
                    toRecover.Count, dest, preview,
                    FormatSize(toRecover.Sum(f => f.SizeBytes))),
                "Confirm Recovery",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

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

            recovering = true;
            opStart = DateTime.Now;
            btnScan.Enabled = false;
            btnRecover.Enabled = false;
            btnRecoverSelected.Enabled = false;
            btnCancel.Enabled = true;
            progPanel.Visible = true;
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Value = 0;
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "Recovering {0} file(s)...", toRecover.Count);
            lblProgPct.Text = "0%";
            lblProgSub.Text = "Starting...";

            elapsedTimer.Start();
            SetStatus(string.Format("Recovering {0} file(s)...", toRecover.Count));

            recoverWorker.RunWorkerAsync(new object[] { toRecover, dest });
        }

        // ════════════════════════════════════════════════════════════
        //  RECOVER WORKER
        // ════════════════════════════════════════════════════════════
        void RecoverWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as object[];
            var toRec = args[0] as List<RecoveredFile>;
            string dest = args[1] as string;
            int total = toRec.Count;
            int done = 0;

            foreach (var rf in toRec)
            {
                if (recoverWorker.CancellationPending) { e.Cancel = true; return; }

                int pct = (int)((double)done / total * 100);
                recoverWorker.ReportProgress(pct,
                    new object[] { "start", rf.FileName, done + 1, total });

                bool ok = false;
                string err = "";

                try
                {
                    string subDir = "";
                    try
                    {
                        string rel = rf.OriginalPath;
                        if (rel.Length > 3 && rel[1] == ':')
                            rel = rel.Substring(3);
                        subDir = Path.GetDirectoryName(rel) ?? "";
                    }
                    catch { }

                    string destDir = string.IsNullOrEmpty(subDir)
                        ? dest
                        : Path.Combine(dest, subDir.TrimStart('\\'));

                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    string destFile = Path.Combine(destDir, rf.FileName);
                    if (File.Exists(destFile))
                    {
                        string noExt = Path.GetFileNameWithoutExtension(rf.FileName);
                        string ext2 = Path.GetExtension(rf.FileName);
                        destFile = Path.Combine(destDir,
                            string.Format("{0}_recovered_{1:HHmmss}{2}",
                                noExt, DateTime.Now, ext2));
                    }

                    File.Copy(rf.FullPath, destFile, true);
                    ok = true;
                }
                catch (Exception ex) { err = ex.Message; }

                done++;
                recoverWorker.ReportProgress(
                    (int)((double)done / total * 100),
                    new object[] { "done", rf.FileName, ok, err });
            }

            e.Result = new object[] { "ok", done, total };
        }

        void RecoverWorker_Progress(object sender, ProgressChangedEventArgs e)
        {
            var d = e.UserState as object[];
            if (d == null) return;
            string type = d[0] as string ?? "";

            progBar.Value = e.ProgressPercentage;
            lblProgPct.Text = e.ProgressPercentage + "%";

            if (type == "start")
            {
                string name = d[1] as string;
                int idx = (int)d[2];
                int total = (int)d[3];
                lblProgText.Text = string.Format(
                    "Recovering {0} / {1}:  {2}", idx, total,
                    name.Length > 50 ? name.Substring(0, 50) + "…" : name);
            }
            else if (type == "done")
            {
                string name = d[1] as string;
                bool ok = Convert.ToBoolean(d[2]);
                string err = d[3] as string;
                AddLog(name ?? "", "Recover",
                    ok ? "✔  Recovered successfully" : "✖  Failed: " + err,
                    ok ? C_GREEN : C_RED);
                UpdateFileStatus(name, ok ? "Recovered" : "Failed", ok ? C_GREEN : C_RED);
            }
        }

        void RecoverWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            recovering = false;
            elapsedTimer.Stop();
            btnScan.Enabled = true;
            btnCancel.Enabled = false;

            var ts = DateTime.Now - opStart;
            string el = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)ts.TotalHours, ts.Minutes, ts.Seconds);

            if (e.Cancelled)
            {
                progPanel.Visible = false;
                SetStatus("Recovery cancelled.");
                AddLog("Recover", "Cancelled.", "INFO", C_AMBER);
                return;
            }

            var res = e.Result as object[];
            if (res == null) return;

            int done = (int)res[1];
            int total = (int)res[2];

            progBar.Value = 100;
            lblProgPct.Text = "100%";
            progBar.SetColors(C_GREEN, C_TEAL);
            progBar.Animate = false;
            progBar.Invalidate();
            lblProgText.Text = string.Format(
                "✔  Recovery complete — {0}/{1} file(s) recovered", done, total);
            lblProgSub.Text = string.Format("Duration: {0}", el);

            SetStatus(string.Format(
                "✔  Recovered {0}/{1} file(s) in {2}. Saved to: {3}",
                done, total, el, txtDestination.Text));
            AddLog("Complete", string.Format(
                "{0}/{1} files recovered in {2}", done, total, el), "INFO", C_GREEN);

            btnRecover.Enabled = false;
            btnRecoverSelected.Enabled = false;

            MessageBox.Show(
                string.Format(
                    "✔  Recovery complete!\n\n{0} / {1} file(s) recovered.\n" +
                    "Duration: {2}\n\nFiles saved to:\n{3}",
                    done, total, el, txtDestination.Text),
                "Recovery Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void BtnCancel_Click(object sender, EventArgs e)
        {
            if (scanning && scanWorker.IsBusy) scanWorker.CancelAsync();
            if (recovering && recoverWorker.IsBusy) recoverWorker.CancelAsync();
            btnCancel.Enabled = false;
            SetStatus("Cancelling...");
            AddLog("Cancel", "Operation cancelled by user.", "INFO", C_AMBER);
        }

        // ════════════════════════════════════════════════════════════
        //  FILE LIST HELPERS
        // ════════════════════════════════════════════════════════════
        void AddFileToList(RecoveredFile rf)
        {
            if (InvokeRequired) { Invoke(new Action(() => AddFileToList(rf))); return; }
            var item = new ListViewItem("");
            item.Checked = rf.Selected;
            item.SubItems.Add(rf.FileName);
            item.SubItems.Add(rf.Extension);
            item.SubItems.Add(TruncPath(rf.OriginalPath, 36));
            item.SubItems.Add(rf.SizeStr);
            item.SubItems.Add(rf.DeletedOn);
            item.SubItems.Add(rf.Source);
            item.SubItems.Add(rf.Recoverability);
            item.Tag = rf;
            item.ForeColor = C_TXT;
            rf.LvItem = item;
            fileList.Items.Add(item);
        }

        void ApplySearch(string query)
        {
            string q = query.Trim().ToLower();
            if (q == "search files...") q = "";
            fileList.BeginUpdate();
            fileList.Items.Clear();
            foreach (var rf in allFiles)
            {
                if (!string.IsNullOrEmpty(q) &&
                    !rf.FileName.ToLower().Contains(q) &&
                    !rf.Extension.ToLower().Contains(q) &&
                    !rf.Source.ToLower().Contains(q))
                    continue;
                AddFileToList(rf);
            }
            fileList.EndUpdate();
            UpdateCounts();
        }

        void ToggleAll(bool check)
        {
            foreach (ListViewItem item in fileList.Items)
            {
                item.Checked = check;
                if (item.Tag is RecoveredFile rf) rf.Selected = check;
            }
            UpdateCounts();
        }

        // ── FIX 2: ListViewItem has no Invalidate(). ──────────────────
        // Update the sub-item text and call fileList.Invalidate()
        // to repaint the whole list — that is the correct approach.
        void UpdateFileStatus(string name, string status, Color c)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => UpdateFileStatus(name, status, c))); return; }

            foreach (var rf in allFiles)
            {
                if (!rf.FileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                rf.Recoverability = status;
                rf.RecovColor = c;
                if (rf.LvItem != null && rf.LvItem.SubItems.Count > 7)
                    rf.LvItem.SubItems[7].Text = status;
                break;
            }

            // Repaint the ListView — ListViewItem has no Invalidate() method
            fileList.Invalidate();
        }

        void UpdateCounts()
        {
            if (InvokeRequired) { Invoke(new Action(UpdateCounts)); return; }
            int sel = 0;
            long size = 0;
            foreach (ListViewItem item in fileList.Items)
            {
                if (item.Checked && item.Tag is RecoveredFile rf)
                { sel++; size += rf.SizeBytes; rf.Selected = true; }
                else if (item.Tag is RecoveredFile rf2)
                    rf2.Selected = false;
            }
            lblFound.Text = string.Format("{0} files found", allFiles.Count);
            lblSelected.Text = string.Format("{0} selected", sel);
            lblTotalSize.Text = FormatSize(size);
        }

        int sortCol = 3;
        bool sortAsc = false;

        void FileList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortCol == e.Column) sortAsc = !sortAsc;
            else { sortCol = e.Column; sortAsc = true; }

            IEnumerable<RecoveredFile> sorted = allFiles;
            switch (e.Column)
            {
                case 1: sorted = sortAsc ? allFiles.OrderBy(f => f.FileName) : allFiles.OrderByDescending(f => f.FileName); break;
                case 2: sorted = sortAsc ? allFiles.OrderBy(f => f.Extension) : allFiles.OrderByDescending(f => f.Extension); break;
                case 4: sorted = sortAsc ? allFiles.OrderBy(f => f.SizeBytes) : allFiles.OrderByDescending(f => f.SizeBytes); break;
                case 5: sorted = sortAsc ? allFiles.OrderBy(f => f.DeletedOn) : allFiles.OrderByDescending(f => f.DeletedOn); break;
                case 6: sorted = sortAsc ? allFiles.OrderBy(f => f.Source) : allFiles.OrderByDescending(f => f.Source); break;
                case 7: sorted = sortAsc ? allFiles.OrderBy(f => f.Recoverability) : allFiles.OrderByDescending(f => f.Recoverability); break;
            }

            fileList.BeginUpdate();
            fileList.Items.Clear();
            foreach (var rf in sorted) AddFileToList(rf);
            fileList.EndUpdate();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        // ── FIX 1: fg is now optional (default = C_TXT). ─────────────
        // This is why all 7 call-sites that passed 3 args compiling:
        //   AddLog("Scan", "message", C_BLUE)          ← 3 args = fine
        //   AddLog("Scan", "message", "detail", C_BLUE)← 4 args = fine
        // Both signatures now work with the single optional-param version.
        void AddLog(string file, string action, string result,
            Color fg = default(Color))
        {
            // default(Color) == Color.Empty — treat that as C_TXT
            if (fg == default(Color) || fg == Color.Empty) fg = C_TXT;

            if (InvokeRequired)
            { Invoke(new Action(() => AddLog(file, action, result, fg))); return; }

            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(file.Length > 40 ? file.Substring(0, 40) + "…" : file);
            item.SubItems.Add(action);
            item.SubItems.Add(result);
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
                                : msg.StartsWith("⚠") ? C_AMBER
                                : msg.Contains("✖") ? C_RED : C_SUB;
        }

        string FormatSize(long bytes)
        {
            if (bytes <= 0) return "–";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.0} KB", bytes / 1024.0);
            if (bytes < 1024L * 1024 * 1024) return string.Format("{0:0.0} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:0.00} GB", bytes / (1024.0 * 1024 * 1024));
        }

        string TruncPath(string path, int maxLen)
        {
            if (string.IsNullOrEmpty(path)) return "–";
            return path.Length > maxLen
                ? "…" + path.Substring(path.Length - maxLen + 1) : path;
        }

        Label MakeStat(string text, Color c, Point loc) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = c,
            AutoSize = true,
            Location = loc
        };

        Label MakeOptLabel(string text, Point loc) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = C_SUB,
            AutoSize = true,
            Location = loc
        };

        CheckBox MakeSourceChk(string text, Color c, Point loc, bool chk) =>
            new CheckBox
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = c,
                AutoSize = true,
                Location = loc,
                Checked = chk,
                BackColor = Color.Transparent
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
            b.FlatAppearance.BorderColor = Color.FromArgb(45, accent.R, accent.G, accent.B);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, accent.R, accent.G, accent.B);
            return b;
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — file list
        // ════════════════════════════════════════════════════════════
        void DrawFileHeader(object sender, DrawListViewColumnHeaderEventArgs e)
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

        void DrawFileRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var rf = e.Item.Tag as RecoveredFile;
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : rf != null && rf.Recoverability == "Recovered"
                    ? Color.FromArgb(14, 63, 185, 119)
                    : rf != null && rf.Recoverability == "Failed"
                        ? Color.FromArgb(14, 248, 81, 73)
                        : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);

            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_TEAL))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            if (e.ColumnIndex == 0 && rf != null)
            {
                using (var br = new SolidBrush(rf.RecovColor))
                    e.Graphics.FillEllipse(br,
                        e.Bounds.X + 5,
                        e.Bounds.Y + (e.Bounds.Height - 8) / 2,
                        8, 8);
                return;
            }

            Color fg = e.ColumnIndex == 1 ? C_TXT
                     : e.ColumnIndex == 2 ? C_BLUE
                     : e.ColumnIndex == 3 ? C_SUB
                     : e.ColumnIndex == 4 ? C_AMBER
                     : e.ColumnIndex == 5 ? C_SUB
                     : e.ColumnIndex == 6
                        ? (rf != null && rf.Source == "Recycle Bin" ? C_AMBER :
                           rf != null && rf.Source == "Shadow Copy" ? C_BLUE :
                           rf != null && rf.Source == "Windows Backup" ? C_GREEN : C_SUB)
                     : e.ColumnIndex == 7 && rf != null ? rf.RecovColor
                     : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 1 || e.ColumnIndex == 3
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                    e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, fileList.Font, br, rc, sf);
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
            if (elapsedTimer != null) elapsedTimer.Stop();
            if (scanWorker != null && scanWorker.IsBusy) scanWorker.CancelAsync();
            if (recoverWorker != null && recoverWorker.IsBusy) recoverWorker.CancelAsync();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  RECOVER PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class RecoverProgressBar : Control
    {
        int _val;
        Color _c1, _c2;
        bool _animate;
        int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _animate; } set { _animate = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColors(Color c1, Color c2) { _c1 = c1; _c2 = c2; Invalidate(); }

        public RecoverProgressBar(Color c1, Color c2)
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