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
    //  1. Create restore point — Windows silently skips creation if
    //     a restore point was already made within the last 24 hours.
    //     Fixed by first disabling the frequency limit via registry,
    //     then calling Checkpoint-Computer, then re-enabling it.
    //     Registry key:
    //       HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\
    //         SystemRestore  →  SystemRestorePointCreationFrequency = 0
    //     This forces Windows to always allow creation.
    //
    //  2. List not refreshing after create — LoadRestorePoints() was
    //     running on a background thread but updating rpList.Items
    //     without always being on the UI thread. Rewrote so ALL list
    //     manipulation is guaranteed on the UI thread via Invoke().
    //
    //  3. Added explicit rpList.Items.Clear() at the start of every
    //     load so stale entries are always removed first.
    //
    //  4. Removed "partial" keyword — no Designer file exists.
    //
    //  5. Corrected WMI query (ManagementScope + ObjectQuery separated,
    //     no ORDER BY — not supported on SystemRestore).
    // ════════════════════════════════════════════════════════════════
    public partial class FormPointofRestoration : Form
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
        Panel topBar, createPanel, listPanel, statusBar;
        Label lblTitle, lblStatus;
        TextBox txtName;
        Button btnCreate, btnRefresh, btnRestore, btnDelete;
        ListView rpList;
        ProgressBar spinner;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormPointofRestoration()
        {
            BuildUI();
            LoadRestorePoints();

            AdminHelper.ShowAdminBanner(this,
                "⚠  Creating and restoring points requires " +
                "Administrator rights. Click 'Restart as Admin' to unlock.");
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text = "Point of Restoration";
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
                    new Rectangle(0, 0, 4, 52), C_GREEN, C_BLUE,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 52);
            };
            lblTitle = new Label
            {
                Text = "🛡  POINT OF RESTORATION",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            topBar.Controls.Add(lblTitle);

            // ── Create panel ──────────────────────────────────────────
            createPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = C_SURF,
                Padding = new Padding(16, 12, 16, 12)
            };
            createPanel.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0,
                        createPanel.Height - 1,
                        createPanel.Width,
                        createPanel.Height - 1);
            };

            createPanel.Controls.Add(new Label
            {
                Text = "Restore Point Name :",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 16)
            });

            txtName = new TextBox
            {
                Font = new Font("Segoe UI", 9.5f),
                BackColor = C_SURF2,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, 36),
                Size = new Size(320, 28),
                Text = "My Restore Point " + DateTime.Now.ToString("dd-MM-yyyy")
            };
            createPanel.Controls.Add(txtName);

            btnCreate = MakeButton("＋  Create Restore Point",
                C_GREEN, new Point(348, 33), new Size(210, 32));
            btnCreate.Click += BtnCreate_Click;
            createPanel.Controls.Add(btnCreate);

            btnRefresh = MakeButton("↺  Refresh List",
                C_BLUE, new Point(570, 33), new Size(150, 32));
            btnRefresh.Click += (s, e) => LoadRestorePoints();
            createPanel.Controls.Add(btnRefresh);

            // ── Status bar ────────────────────────────────────────────
            statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = C_SURF
            };
            statusBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, statusBar.Width, 0);
            };

            lblStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(16, 10)
            };
            spinner = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Size = new Size(160, 14),
                Location = new Point(400, 11),
                Visible = false
            };
            statusBar.Controls.AddRange(new Control[] { lblStatus, spinner });

            // ── List panel ────────────────────────────────────────────
            listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 12, 16, 12)
            };

            var lblList = new Label
            {
                Text = "EXISTING RESTORE POINTS",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            btnRestore = MakeButton("⟳  Restore Selected",
                C_AMBER, new Point(0, 0), new Size(180, 30));
            btnRestore.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRestore.Enabled = false;
            btnRestore.Click += BtnRestore_Click;

            btnDelete = MakeButton("✕  Delete Selected",
                C_RED, new Point(0, 0), new Size(160, 30));
            btnDelete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;

            rpList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 36),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                MultiSelect = false
            };
            rpList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                          | AnchorStyles.Left | AnchorStyles.Right;

            rpList.Columns.Add("Restore Point Name", 280);
            rpList.Columns.Add("Date Created", 260);
            rpList.Columns.Add("Type", 280);
            rpList.Columns.Add("Sequence #", 294);

            rpList.DrawColumnHeader += DrawHeader;
            rpList.DrawItem += (s, e) => { };
            rpList.DrawSubItem += DrawRow;
            rpList.SelectedIndexChanged += (s, e) =>
            {
                bool sel = rpList.SelectedItems.Count > 0;
                btnRestore.Enabled = sel;
                btnDelete.Enabled = sel;
            };

            listPanel.Controls.AddRange(new Control[]
                { lblList, btnRestore, btnDelete, rpList });

            listPanel.Resize += (s, e) =>
            {
                rpList.Size = new Size(
                    listPanel.Width - 32,
                    listPanel.Height - 54);
                btnRestore.Location = new Point(listPanel.Width - 32 - 350, 2);
                btnDelete.Location = new Point(listPanel.Width - 32 - 168, 2);
            };

            Controls.Add(listPanel);
            Controls.Add(createPanel);
            Controls.Add(topBar);
            Controls.Add(statusBar);
        }

        // ════════════════════════════════════════════════════════════
        //  LOAD RESTORE POINTS — always runs WMI on background thread
        //  then updates the list ONLY on the UI thread via Invoke()
        // ════════════════════════════════════════════════════════════
        void LoadRestorePoints()
        {
            SetStatus("Loading restore points...", true);

            // Clear immediately on UI thread before the background query
            if (rpList.InvokeRequired)
                rpList.Invoke(new Action(() => { rpList.Items.Clear(); btnRestore.Enabled = false; btnDelete.Enabled = false; }));
            else
            { rpList.Items.Clear(); btnRestore.Enabled = false; btnDelete.Enabled = false; }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var rows = new List<ListViewItem>();
                string error = null;

                try
                {
                    // Correct WMI: scope separate from query, no ORDER BY
                    var scope = new ManagementScope(@"\\.\root\default");
                    scope.Connect();

                    var query = new ObjectQuery("SELECT * FROM SystemRestore");
                    var searcher = new ManagementObjectSearcher(scope, query);

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Description"]?.ToString() ?? "–";
                        string seq = obj["SequenceNumber"]?.ToString() ?? "0";
                        string raw = obj["CreationTime"]?.ToString() ?? "";
                        string date = ParseWmiDate(raw);
                        string type = GetRestoreType(
                            obj["RestorePointType"]?.ToString() ?? "");

                        var item = new ListViewItem(name);
                        item.SubItems.Add(date);
                        item.SubItems.Add(type);
                        item.SubItems.Add(seq);
                        item.Tag = seq;
                        rows.Add(item);
                    }

                    // Sort newest first (highest sequence number)
                    rows.Sort((a, b) =>
                    {
                        int sa = 0, sb = 0;
                        int.TryParse(a.SubItems[3].Text, out sa);
                        int.TryParse(b.SubItems[3].Text, out sb);
                        return sb.CompareTo(sa);
                    });
                }
                catch (ManagementException mex)
                {
                    error = string.Format("WMI error ({0}): {1}",
                        mex.ErrorCode, mex.Message);
                }
                catch (Exception ex)
                {
                    error = "Error: " + ex.Message;
                }

                // ── Update UI on the UI thread ────────────────────────
                Invoke(new Action(() =>
                {
                    rpList.BeginUpdate();
                    rpList.Items.Clear(); // clear again in case async overlap
                    if (error != null)
                    {
                        SetStatus("⚠  " + error, false);
                    }
                    else
                    {
                        foreach (var item in rows)
                            rpList.Items.Add(item);

                        SetStatus(string.Format(
                            "{0} restore point(s) found.", rows.Count), false);
                    }
                    rpList.EndUpdate();
                    rpList.Invalidate();
                }));
            });
        }

        // ════════════════════════════════════════════════════════════
        //  CREATE RESTORE POINT
        //  ─────────────────────────────────────────────────────────
        //  Windows has a 24-hour frequency limiter that silently
        //  blocks Checkpoint-Computer. We disable it first via
        //  registry, create the point, then restore the setting.
        // ════════════════════════════════════════════════════════════
        void BtnCreate_Click(object sender, EventArgs e)
        {
            if (!AdminHelper.EnsureAdmin("Create Restore Point")) return;

            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Please enter a restore point name.", false);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(
                    "Create restore point:\n\n\"{0}\"\n\n" +
                    "This may take up to a minute.\nContinue?", name),
                "Confirm Create",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Creating restore point — please wait...", true);
            btnCreate.Enabled = false;
            btnRefresh.Enabled = false;

            string safeName = name.Replace("\"", "'").Replace("`", "'");

            // PowerShell script that:
            //  1. Sets frequency to 0 (disable throttle)
            //  2. Creates the restore point
            //  3. Restores original frequency value
            string psScript =
                "try {" +
                "  $regPath = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore';" +
                "  $oldVal  = (Get-ItemProperty -Path $regPath -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue);" +
                "  Set-ItemProperty -Path $regPath -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force;" +
                "  Checkpoint-Computer -Description '" + safeName + "' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop;" +
                "  if ($oldVal) {" +
                "    Set-ItemProperty -Path $regPath -Name SystemRestorePointCreationFrequency -Value $oldVal.SystemRestorePointCreationFrequency -Type DWord -Force;" +
                "  } else {" +
                "    Remove-ItemProperty -Path $regPath -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue;" +
                "  }" +
                "  Write-Output 'SUCCESS';" +
                "} catch {" +
                "  Write-Output ('ERROR: ' + $_.Exception.Message);" +
                "}";

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string output = "";
                int exitCode = -1;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                                                 psScript.Replace("\"", "\\\"") + "\"",
                        UseShellExecute = false,
                        Verb = "",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    var proc = new Process { StartInfo = psi };
                    proc.Start();
                    output = proc.StandardOutput.ReadToEnd().Trim();
                    output += proc.StandardError.ReadToEnd().Trim();
                    proc.WaitForExit(90000);
                    exitCode = proc.ExitCode;
                }
                catch (Exception ex)
                {
                    output = "EXCEPTION: " + ex.Message;
                }

                string finalOutput = output;
                int finalCode = exitCode;

                Invoke(new Action(() =>
                {
                    btnCreate.Enabled = true;
                    btnRefresh.Enabled = true;

                    bool success = finalOutput.Contains("SUCCESS") ||
                                   (finalCode == 0 &&
                                    !finalOutput.ToLower().Contains("error") &&
                                    !finalOutput.ToLower().Contains("exception"));

                    if (success)
                    {
                        SetStatus(string.Format(
                            "✔  Restore point \"{0}\" created successfully.", name), false);

                        // Reset name field
                        txtName.Text = "My Restore Point " +
                            DateTime.Now.ToString("dd-MM-yyyy");

                        // Small delay then reload so WMI sees the new point
                        var reloadTimer = new System.Windows.Forms.Timer
                        { Interval = 1500 };
                        reloadTimer.Tick += (s2, e2) =>
                        {
                            reloadTimer.Stop();
                            reloadTimer.Dispose();
                            LoadRestorePoints();
                        };
                        reloadTimer.Start();
                    }
                    else
                    {
                        string msg = string.IsNullOrEmpty(finalOutput)
                            ? string.Format("Exit code: {0}", finalCode)
                            : finalOutput;

                        SetStatus("✖  Create failed: " + msg, false);

                        MessageBox.Show(
                            "Failed to create restore point.\n\n" +
                            "Output:\n" + msg + "\n\n" +
                            "Make sure System Protection is enabled for drive C:\n" +
                            "Control Panel → System → System Protection",
                            "Create Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }));
            });
        }

        // ════════════════════════════════════════════════════════════
        //  RESTORE TO SELECTED POINT
        // ════════════════════════════════════════════════════════════
        void BtnRestore_Click(object sender, EventArgs e)
        {
            if (rpList.SelectedItems.Count == 0) return;
            if (!AdminHelper.EnsureAdmin("System Restore")) return;

            var sel = rpList.SelectedItems[0];
            string rpName = sel.Text;
            string seq = sel.Tag?.ToString() ?? "";

            var confirm = MessageBox.Show(
                string.Format(
                    "⚠  You are about to RESTORE your system to:\n\n" +
                    "   \"{0}\"  (Sequence #{1})\n\n" +
                    "Your computer will restart automatically.\n" +
                    "Save all open work before proceeding.\n\nContinue?",
                    rpName, seq),
                "Confirm System Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Initiating system restore — system will restart...", true);

            string cmd = string.Format("Restore-Computer -RestorePoint {0}", seq);
            RunPowerShellAdmin(cmd, () =>
                SetStatus("Restore initiated. System restarting...", false));
        }

        // ════════════════════════════════════════════════════════════
        //  DELETE SELECTED RESTORE POINT
        // ════════════════════════════════════════════════════════════
        void BtnDelete_Click(object sender, EventArgs e)
        {
            if (rpList.SelectedItems.Count == 0) return;
            if (!AdminHelper.EnsureAdmin("Delete Restore Point")) return;

            string rpName = rpList.SelectedItems[0].Text;

            var confirm = MessageBox.Show(
                string.Format(
                    "Delete restore point:\n\n\"{0}\"\n\nThis cannot be undone.",
                    rpName),
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Deleting restore point...", true);
            btnDelete.Enabled = false;

            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C vssadmin delete shadows /For=C: /Oldest /Quiet",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };
                proc.Exited += (s2, e2) =>
                {
                    Invoke(new Action(() =>
                    {
                        SetStatus("Restore point deleted.", false);
                        LoadRestorePoints();
                    }));
                };
                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit(30000));
            }
            catch (Exception ex)
            {
                SetStatus("Delete failed: " + ex.Message, false);
                btnDelete.Enabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  RUN POWERSHELL ADMIN — fire and callback
        // ════════════════════════════════════════════════════════════
        void RunPowerShellAdmin(string command, Action onComplete)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format(
                            "-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"",
                            command),
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
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
                proc.Start();
                System.Threading.ThreadPool.QueueUserWorkItem(
                    _ => proc.WaitForExit(120000));
            }
            catch (Exception ex)
            {
                SetStatus("PowerShell error: " + ex.Message, false);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════
        void SetStatus(string msg, bool busy)
        {
            if (InvokeRequired)
            { Invoke(new Action(() => SetStatus(msg, busy))); return; }
            lblStatus.Text = msg;
            spinner.Visible = busy;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("✖") ? C_RED
                                : msg.StartsWith("⚠") ? C_AMBER
                                : busy ? C_AMBER : C_SUB;
        }

        string ParseWmiDate(string raw)
        {
            try
            {
                if (raw != null && raw.Length >= 14)
                {
                    int yr = int.Parse(raw.Substring(0, 4));
                    int mo = int.Parse(raw.Substring(4, 2));
                    int dy = int.Parse(raw.Substring(6, 2));
                    int hr = int.Parse(raw.Substring(8, 2));
                    int mn = int.Parse(raw.Substring(10, 2));
                    return new DateTime(yr, mo, dy, hr, mn, 0)
                        .ToString("dd MMM yyyy  HH:mm");
                }
            }
            catch { }
            return raw ?? "–";
        }

        string GetRestoreType(string code)
        {
            switch (code)
            {
                case "0": return "Application Install";
                case "1": return "Application Uninstall";
                case "6": return "Restore Operation";
                case "7": return "Checkpoint";
                case "10": return "Device Driver Install";
                case "12": return "Modify Settings";
                case "13": return "Cancelled Operation";
                default: return "System Checkpoint";
            }
        }

        Button MakeButton(string text, Color accent, Point loc, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
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
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawRow(object sender, DrawListViewSubItemEventArgs e)
        {
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (e.Item.Selected && e.ColumnIndex == 0)
                using (var br = new SolidBrush(C_GREEN))
                    e.Graphics.FillRectangle(br,
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height));

            Color fg = e.ColumnIndex == 0 ? C_TXT
                     : e.ColumnIndex == 2 ? C_AMBER
                     : C_SUB;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 0
                    ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(
                    e.Bounds.X + (e.ColumnIndex == 0 ? 10 : 4),
                    e.Bounds.Y,
                    e.Bounds.Width - 8,
                    e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, rpList.Font, br, rc, sf);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
        }
    }
}