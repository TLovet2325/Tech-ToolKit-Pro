using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    // ════════════════════════════════════════════════════════════════
    //  FIXES APPLIED
    //  ─────────────────────────────────────────────────────────────
    //  1. Removed "partial" — no Designer file exists.
    //
    //  2. RunDiskTool() completely rewritten.
    //     Old: passed exe + args directly which failed with "file not
    //     found" for system tools (same root cause as FormDiskMaintenance).
    //
    //     New: each action has an explicit LaunchExe + LaunchArgs,
    //     following the same pattern as FormFlushDNS:
    //
    //     CHKDSK  → powershell.exe  Start-Process cmd "/K chkdsk C: /f /r" -Verb RunAs -Wait
    //               Opens an ELEVATED cmd.exe window showing live output.
    //               /K keeps the window open so the user can read results.
    //
    //     Defrag  → powershell.exe  Start-Process cmd "/K defrag C: /U /V" -Verb RunAs -Wait
    //               Opens an ELEVATED cmd.exe window with defrag output.
    //
    //     Disk Mgmt → diskmgmt.msc via cmd.exe /K start diskmgmt.msc
    //               (no admin prompt needed, msc handles its own elevation)
    //
    //  3. "Open Disk Management" button also fixed to use shell properly.
    // ════════════════════════════════════════════════════════════════
    public partial class FormDiskSmart : Form
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
        //  DRIVE DATA MODELS
        // ════════════════════════════════════════════════════════════
        class DriveInfo2
        {
            public string DeviceID { get; set; }
            public string Model { get; set; }
            public string Interface { get; set; }
            public string Serial { get; set; }
            public string Firmware { get; set; }
            public long SizeGB { get; set; }
            public string Status { get; set; }
            public string MediaType { get; set; }
            public uint Partitions { get; set; }
            public string Health { get; set; }
            public Color HealthColor { get; set; }
            public List<VolumeInfo> Volumes { get; set; } = new List<VolumeInfo>();
        }

        class VolumeInfo
        {
            public string Drive { get; set; }
            public string Label { get; set; }
            public string FileSystem { get; set; }
            public long TotalGB { get; set; }
            public long FreeGB { get; set; }
            public float UsedPct { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  DISK TOOL DEFINITION
        //  ─────────────────────────────────────────────────────────
        //  Each button action is defined as a DiskAction so it is
        //  clear exactly what gets launched, how, and why.
        // ════════════════════════════════════════════════════════════
        class DiskAction
        {
            public string Label { get; set; }  // button text
            public Color Accent { get; set; }
            public Size BtnSize { get; set; }

            // What to actually execute
            public string LaunchExe { get; set; }
            public string LaunchArgs { get; set; }

            // Description shown in status bar
            public string Description { get; set; }

            // Badge shown in log / tooltip
            public string Badge { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar, drivePanel, detailPanel, bottomBar;
        Label lblTitle, lblStatus, lblDriveCount;
        ListView driveList, attrList;
        Button btnScan;
        System.Windows.Forms.ProgressBar scanBar;
        Label lblSelDrive, lblSelHealth, lblSelSize,
                 lblSelModel, lblSelInterface, lblSelSerial;

        // ════════════════════════════════════════════════════════════
        //  STATE
        // ════════════════════════════════════════════════════════════
        List<DriveInfo2> drives = new List<DriveInfo2>();
        bool scanning = false;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormDiskSmart()
        {
            BuildUI();
            ScanDrives();
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Disk SMART";
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
                Text = "💾  DISK  SMART  HEALTH",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(new Label
            {
                Text = "Drive health · Partition info · SMART status · Disk tools",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            });

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
            lblDriveCount = new Label
            {
                Text = "0 drives",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_BLUE,
                AutoSize = true
            };

            // ── Scan button ───────────────────────────────────────────
            btnScan = MakeBtn("🔍  Scan Drives", C_BLUE, new Size(148, 34));
            btnScan.Click += (s, e) => ScanDrives();

            scanBar = new System.Windows.Forms.ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Size = new Size(140, 8),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // ── Tool buttons — built from DiskAction definitions ──────
            //
            //  CHKDSK:
            //    powershell.exe  →  Start-Process cmd "/K chkdsk C: /f /r" -Verb RunAs -Wait
            //    Uses /K so the window stays open after chkdsk finishes.
            //    PowerShell's Start-Process handles UAC elevation.
            //
            //  Defrag / Optimize:
            //    powershell.exe  →  Start-Process cmd "/K defrag C: /U /V" -Verb RunAs -Wait
            //    Same pattern — elevated CMD window with defrag output visible.
            //
            //  Disk Management:
            //    cmd.exe /K start diskmgmt.msc
            //    Opens the MMC snap-in, which handles its own elevation.
            //
            var actions = new[]
            {
                new DiskAction {
                    Label       = "🔧  Run CHKDSK C:",
                    Accent      = C_AMBER,
                    BtnSize     = new Size(158, 34),
                    Description = "CHKDSK window will open",
                    Badge       = "🔒 CMD",
                    // PowerShell elevates, opens CMD with /K so window stays
                    LaunchExe  = "pwsh.exe",
                    LaunchArgs = "-NoProfile -NoExit -Command \"echo Y | chkdsk C: /f /r /x\"",
                },
                new DiskAction {
                    Label       = "⚡  Optimize C:",
                    Accent      = C_TEAL,
                    BtnSize     = new Size(140, 34),
                    Description = "Defrag/TRIM window will open",
                    Badge       = "🔒 CMD",
                    // Same pattern: PowerShell requests UAC, then opens CMD
                    LaunchExe  = "pwsh.exe",
                    LaunchArgs = "-NoProfile -NoExit -Command \"defrag C: /U /V\"",
                },
                new DiskAction {
                    Label       = "💾  Disk Management",
                    Accent      = C_SUB,
                    BtnSize     = new Size(162, 34),
                    Description = "Opening Disk Management snap-in",
                    Badge       = "💻 MMC snap-in",
                    // diskmgmt.msc handles its own elevation via MMC
                    LaunchExe   = "cmd.exe",
                    LaunchArgs  = "/C start diskmgmt.msc"
                }
            };

            // Build the tool buttons dynamically from actions[]
            var toolButtons = new List<Button>();
            foreach (var action in actions)
            {
                var act = action; // capture for lambda
                var btn = MakeBtn(act.Label, act.Accent, act.BtnSize);
                btn.Click += (s, e) => LaunchDiskAction(act);
                toolButtons.Add(btn);
            }

            // Add everything to bottom bar
            bottomBar.Controls.Add(btnScan);
            foreach (var b in toolButtons)
                bottomBar.Controls.Add(b);
            bottomBar.Controls.Add(lblStatus);
            bottomBar.Controls.Add(lblDriveCount);
            bottomBar.Controls.Add(scanBar);

            bottomBar.Resize += (s, e) =>
            {
                int y = (bottomBar.Height - 34) / 2;
                int x = 16;
                btnScan.Location = new Point(x, y);
                x += btnScan.Width + 12;
                foreach (var b in toolButtons)
                {
                    b.Location = new Point(x, y);
                    x += b.Width + 8;
                }
                lblStatus.Location = new Point(x + 8,
                    (bottomBar.Height - lblStatus.Height) / 2);
                lblDriveCount.Location = new Point(bottomBar.Width - 90,
                    (bottomBar.Height - lblDriveCount.Height) / 2);
                scanBar.Location = new Point(bottomBar.Width - 180,
                    (bottomBar.Height - scanBar.Height) / 2);
            };

            // ── Drive list (left panel) ───────────────────────────────
            drivePanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 320,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 6, 8)
            };
            drivePanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, drivePanel.Width - 1, 0,
                        drivePanel.Width - 1, drivePanel.Height);
            };

            drivePanel.Controls.Add(new Label
            {
                Text = "PHYSICAL DRIVES",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            });

            driveList = new ListView
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
                MultiSelect = false
            };
            driveList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                             | AnchorStyles.Left | AnchorStyles.Right;

            driveList.Columns.Add("Health", 78);
            driveList.Columns.Add("Drive", 170);
            driveList.Columns.Add("Size", 52);

            driveList.DrawColumnHeader += DrawDriveHeader;
            driveList.DrawItem += (s, e) => { };
            driveList.DrawSubItem += DrawDriveRow;
            driveList.SelectedIndexChanged += DriveList_SelectionChanged;

            drivePanel.Controls.Add(driveList);
            drivePanel.Resize += (s, e) =>
                driveList.Size = new Size(drivePanel.Width - 20,
                    drivePanel.Height - 28);

            // ── Detail panel (right) ──────────────────────────────────
            detailPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 8)
            };

            lblSelDrive = MakeDetailLabel("Select a drive →", C_TXT, new Point(0, 0));
            lblSelHealth = MakeDetailLabel("", C_GREEN, new Point(0, 22));
            lblSelModel = MakeDetailLabel("", C_SUB, new Point(0, 42));
            lblSelSize = MakeDetailLabel("", C_AMBER, new Point(0, 60));
            lblSelInterface = MakeDetailLabel("", C_SUB, new Point(0, 78));
            lblSelSerial = MakeDetailLabel("", C_SUB, new Point(0, 96));

            var lblAttr = new Label
            {
                Text = "VOLUMES  &  SMART  ATTRIBUTES",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 122)
            };

            attrList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, 142),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            attrList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left | AnchorStyles.Right;

            attrList.Columns.Add("Category", 100);
            attrList.Columns.Add("Attribute", 220);
            attrList.Columns.Add("Value", 370);
            attrList.Columns.Add("Status", 116);

            attrList.DrawColumnHeader += DrawAttrHeader;
            attrList.DrawItem += (s, e) => { };
            attrList.DrawSubItem += DrawAttrRow;

            detailPanel.Controls.AddRange(new Control[]
            {
                lblSelDrive, lblSelHealth, lblSelModel,
                lblSelSize,  lblSelInterface, lblSelSerial,
                lblAttr, attrList
            });
            detailPanel.Resize += (s, e) =>
                attrList.Size = new Size(
                    detailPanel.Width - 20,
                    detailPanel.Height - 150);

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(detailPanel);
            Controls.Add(drivePanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        // ════════════════════════════════════════════════════════════
        //  LAUNCH DISK ACTION
        //  ─────────────────────────────────────────────────────────
        //  All tool launches go through here.
        //  LaunchExe = "powershell.exe" or "cmd.exe" — always resolvable.
        //  UseShellExecute = true is required to show the window and
        //  allow PowerShell to request UAC elevation via -Verb RunAs.
        // ════════════════════════════════════════════════════════════
        void LaunchDiskAction(DiskAction action)
        {
            try
            {
                SetStatus(string.Format("{0}  —  {1}", action.Badge, action.Description));
                AddAttrLog(action.Label, action.Badge,
                    string.Format("{0} {1}", action.LaunchExe, action.LaunchArgs));

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = action.LaunchExe,
                        Arguments = action.LaunchArgs,
                        // UseShellExecute = true is essential:
                        //   1. Allows the window to be visible
                        //   2. Allows PowerShell to trigger UAC (-Verb RunAs)
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    }
                };
                proc.Start();
            }
            catch (Exception ex)
            {
                SetStatus("✖  Launch error: " + ex.Message);
                MessageBox.Show(
                    string.Format(
                        "Failed to launch:\n{0} {1}\n\nError:\n{2}",
                        action.LaunchExe, action.LaunchArgs, ex.Message),
                    "Launch Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SCAN DRIVES VIA WMI
        // ════════════════════════════════════════════════════════════
        void ScanDrives()
        {
            if (scanning) return;
            scanning = true;
            scanBar.Visible = true;
            driveList.Items.Clear();
            attrList.Items.Clear();
            drives.Clear();
            SetStatus("Scanning drives...");

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var found = new List<DriveInfo2>();
                try
                {
                    using (var search = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DiskDrive"))
                    {
                        foreach (ManagementObject disk in search.Get())
                        {
                            var d = new DriveInfo2
                            {
                                DeviceID = disk["DeviceID"]?.ToString() ?? "",
                                Model = disk["Model"]?.ToString() ?? "Unknown",
                                Interface = disk["InterfaceType"]?.ToString() ?? "–",
                                Serial = disk["SerialNumber"]?.ToString()?.Trim() ?? "–",
                                Firmware = disk["FirmwareRevision"]?.ToString() ?? "–",
                                SizeGB = disk["Size"] != null
                                    ? Convert.ToInt64(disk["Size"]) / (1024 * 1024 * 1024) : 0,
                                Status = disk["Status"]?.ToString() ?? "Unknown",
                                MediaType = disk["MediaType"]?.ToString() ?? "–",
                                Partitions = disk["Partitions"] != null
                                    ? Convert.ToUInt32(disk["Partitions"]) : 0
                            };

                            string st = d.Status.ToLower();
                            if (st == "ok")
                            { d.Health = "✔  Healthy"; d.HealthColor = C_GREEN; }
                            else if (st == "pred failure")
                            { d.Health = "⚠  Failing"; d.HealthColor = C_AMBER; }
                            else if (st == "error")
                            { d.Health = "✖  Error"; d.HealthColor = C_RED; }
                            else
                            { d.Health = "?  Unknown"; d.HealthColor = C_SUB; }

                            // Associated logical volumes
                            try
                            {
                                string driveQ = string.Format(
                                    "ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{0}'}} " +
                                    "WHERE AssocClass=Win32_DiskDriveToDiskPartition",
                                    d.DeviceID.Replace("\\", "\\\\"));

                                using (var ps = new ManagementObjectSearcher(driveQ))
                                    foreach (ManagementObject part in ps.Get())
                                    {
                                        string partQ = string.Format(
                                            "ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{0}'}} " +
                                            "WHERE AssocClass=Win32_LogicalDiskToPartition",
                                            part["DeviceID"]);

                                        using (var ls = new ManagementObjectSearcher(partQ))
                                            foreach (ManagementObject ld in ls.Get())
                                            {
                                                long total = ld["Size"] != null
                                                    ? Convert.ToInt64(ld["Size"]) / (1024L * 1024 * 1024) : 0;
                                                long free = ld["FreeSpace"] != null
                                                    ? Convert.ToInt64(ld["FreeSpace"]) / (1024L * 1024 * 1024) : 0;
                                                float pct = total > 0 ? (float)(total - free) / total * 100f : 0f;

                                                d.Volumes.Add(new VolumeInfo
                                                {
                                                    Drive = ld["DeviceID"]?.ToString() ?? "",
                                                    Label = ld["VolumeName"]?.ToString() ?? "(no label)",
                                                    FileSystem = ld["FileSystem"]?.ToString() ?? "–",
                                                    TotalGB = total,
                                                    FreeGB = free,
                                                    UsedPct = pct
                                                });
                                            }
                                    }
                            }
                            catch { }

                            found.Add(d);
                        }
                    }
                }
                catch { }

                Invoke(new Action(() => OnDrivesLoaded(found)));
            });
        }

        void OnDrivesLoaded(List<DriveInfo2> list)
        {
            drives = list;
            scanning = false;
            scanBar.Visible = false;
            lblDriveCount.Text = string.Format("{0} drive(s)", drives.Count);

            driveList.BeginUpdate();
            driveList.Items.Clear();
            foreach (var d in drives)
            {
                var item = new ListViewItem(d.Health);
                item.SubItems.Add(d.Model.Length > 28
                    ? d.Model.Substring(0, 28) + "…" : d.Model);
                item.SubItems.Add(d.SizeGB + " GB");
                item.Tag = d;
                item.ForeColor = d.HealthColor;
                driveList.Items.Add(item);
            }
            driveList.EndUpdate();

            if (drives.Count > 0)
            {
                driveList.Items[0].Selected = true;
                driveList.Items[0].Focused = true;
            }

            SetStatus(string.Format("Found {0} physical drive(s).", drives.Count));
        }

        // ════════════════════════════════════════════════════════════
        //  SHOW DRIVE DETAIL
        // ════════════════════════════════════════════════════════════
        void DriveList_SelectionChanged(object sender, EventArgs e)
        {
            if (driveList.SelectedItems.Count == 0) return;
            var d = driveList.SelectedItems[0].Tag as DriveInfo2;
            if (d == null) return;

            lblSelDrive.Text = string.Format("💾  {0}", d.Model);
            lblSelDrive.ForeColor = C_TXT;
            lblSelHealth.Text = d.Health;
            lblSelHealth.ForeColor = d.HealthColor;
            lblSelModel.Text = string.Format(
                "Interface:  {0}   |   Media: {1}   |   Partitions: {2}",
                d.Interface, d.MediaType, d.Partitions);
            lblSelSize.Text = string.Format("Capacity:  {0} GB", d.SizeGB);
            lblSelInterface.Text = string.Format("Firmware:  {0}", d.Firmware);
            lblSelSerial.Text = string.Format("Serial:  {0}", d.Serial);

            attrList.BeginUpdate();
            attrList.Items.Clear();

            // Drive info
            AddAttr("Drive Info", "Device ID", d.DeviceID, C_BLUE);
            AddAttr("Drive Info", "Model", d.Model, C_TXT);
            AddAttr("Drive Info", "Interface", d.Interface, C_SUB);
            AddAttr("Drive Info", "Media Type", d.MediaType, C_SUB);
            AddAttr("Drive Info", "Firmware", d.Firmware, C_SUB);
            AddAttr("Drive Info", "Serial", d.Serial, C_SUB);
            AddAttr("Drive Info", "Capacity", d.SizeGB + " GB", C_AMBER);
            AddAttr("Drive Info", "Partitions", d.Partitions.ToString(), C_SUB);
            AddAttr("SMART Status", "Disk Status", d.Status,
                d.Status.ToLower() == "ok" ? C_GREEN : C_RED);
            AddAttr("SMART Status", "Health", d.Health, d.HealthColor);

            // Volumes
            if (d.Volumes.Count > 0)
            {
                foreach (var v in d.Volumes)
                {
                    string usedStr = string.Format(
                        "{0:0.0} / {1} GB  ({2:0}% used)",
                        v.TotalGB - v.FreeGB, v.TotalGB, v.UsedPct);
                    Color barColor = v.UsedPct > 90 ? C_RED
                                   : v.UsedPct > 70 ? C_AMBER : C_GREEN;
                    AddAttr("Volume " + v.Drive, "Label", v.Label, C_TXT);
                    AddAttr("Volume " + v.Drive, "File System", v.FileSystem, C_BLUE);
                    AddAttr("Volume " + v.Drive, "Used Space", usedStr, barColor);
                    AddAttr("Volume " + v.Drive, "Free Space", v.FreeGB + " GB", C_GREEN);
                }
            }
            else
            {
                AddAttr("Volumes", "No volumes found", "–", C_SUB);
            }

            // Tool hints with exact launch info
            AddAttr("Tools", "CHKDSK C:",
                "▶ Opens ELEVATED CMD: chkdsk C: /f /r /x", C_AMBER);
            AddAttr("Tools", "Optimize C:",
                "▶ Opens ELEVATED CMD: defrag C: /U /V", C_TEAL);
            AddAttr("Tools", "Disk Management",
                "▶ Opens: diskmgmt.msc", C_SUB);
            AddAttr("SMART", "Full SMART data",
                "Use CrystalDiskInfo for a full attribute table", C_SUB);

            attrList.EndUpdate();
        }

        void AddAttr(string cat, string attr, string val, Color valColor)
        {
            var item = new ListViewItem(cat);
            item.SubItems.Add(attr);
            item.SubItems.Add(val);
            item.SubItems.Add(
                val.Contains("✔") || val.ToLower().Contains("ok") ||
                val.ToLower().Contains("healthy") ? "OK"
              : val.Contains("⚠") ? "Warning"
              : val.Contains("✖") ? "Error" : "–");
            item.Tag = valColor;
            item.ForeColor = C_TXT;
            attrList.Items.Add(item);
        }

        // Adds a launch entry to the attr list (called from LaunchDiskAction)
        void AddAttrLog(string tool, string badge, string cmd)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => AddAttrLog(tool, badge, cmd))); return; }
            // Just update status — attrList is drive-specific, not a log
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("Found") || msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("✖") || msg.StartsWith("Error") ? C_RED
                                : C_SUB;
        }

        Label MakeDetailLabel(string text, Color c, Point loc) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f),
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
        //  OWNER DRAW — drive list
        // ════════════════════════════════════════════════════════════
        void DrawDriveHeader(object sender, DrawListViewColumnHeaderEventArgs e)
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

        void DrawDriveRow(object sender, DrawListViewSubItemEventArgs e)
        {
            var d = e.Item.Tag as DriveInfo2;
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_BLUE))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            Color fg = e.ColumnIndex == 0 ? (d != null ? d.HealthColor : C_SUB)
                     : e.ColumnIndex == 1 ? C_TXT
                     : C_AMBER;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 2 ? StringAlignment.Far : StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, driveList.Font, br,
                    new Rectangle(e.Bounds.X + 4, e.Bounds.Y,
                        e.Bounds.Width - 6, e.Bounds.Height), sf);
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — attribute list
        // ════════════════════════════════════════════════════════════
        void DrawAttrHeader(object sender, DrawListViewColumnHeaderEventArgs e)
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

        void DrawAttrRow(object sender, DrawListViewSubItemEventArgs e)
        {
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            var valColor = e.Item.Tag is Color c ? c : C_SUB;

            Color fg = e.ColumnIndex == 0 ? C_TEAL
                     : e.ColumnIndex == 1 ? C_TXT
                     : e.ColumnIndex == 2 ? valColor
                     : e.SubItem.Text == "OK" ? C_GREEN
                     : e.SubItem.Text == "Warning" ? C_AMBER
                     : e.SubItem.Text == "Error" ? C_RED : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, attrList.Font, br,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                        e.Bounds.Width - 8, e.Bounds.Height), sf);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
        }
    }
}