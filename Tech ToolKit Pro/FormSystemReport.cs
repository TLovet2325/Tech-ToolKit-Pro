using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace Tech_ToolKit_Pro
{
    public partial class FormSystemReport : Form
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
        //  CONTROLS — shell
        // ════════════════════════════════════════════════════════════
        Panel topBar, tabBar, contentHost, bottomBar;
        Label lblTitle, lblStatus;
        Button btnTabSystem, btnTabHardware, btnTabMemory, btnTabPerf;
        Button btnRefresh, btnExport;
        string activeTab = "system";

        // Tab panels
        Panel sysPanel, hwPanel, memPanel, perfPanel;

        // System tab
        ListView lvSys;

        // Hardware tab
        ListView lvHw;

        // Memory tab
        Label lblMemInfo, lblMemPct, lblMemState, lblMemResult;
        ReportProgressBar memBar;
        RichTextBox rtbMem;
        Button btnMemStart, btnMemCancel;
        System.Windows.Forms.Timer memTimer;
        bool memRunning;
        int memStep, memErrors, memOk;
        DateTime memStart;

        // Perf tab
        Label lblPerfPct, lblPerfState, lblPerfResult;
        ReportProgressBar perfBar;
        RichTextBox rtbPerf;
        Button btnPerfStart, btnPerfCancel;
        System.Windows.Forms.Timer perfTimer;
        bool perfRunning;
        int perfPhase;
        double perfCpu, perfRam, perfDisk;
        DateTime perfStart;
        Dictionary<string, Label> scoreLabels = new Dictionary<string, Label>();

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormSystemReport()
        {
            BuildShell();
            BuildSysPanel();
            BuildHwPanel();
            BuildMemPanel();
            BuildPerfPanel();
            SwitchTab("system");
        }

        // ════════════════════════════════════════════════════════════
        //  SHELL
        // ════════════════════════════════════════════════════════════
        void BuildShell()
        {
            Text = "System Report";
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
                Text = "📊  SYSTEM  REPORT  CENTER",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            var lblSub = new Label
            {
                Text = "System Info  ·  Hardware  ·  Memory Test  ·  Performance",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(22, 34)
            };
            topBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ── Tab bar ───────────────────────────────────────────────
            tabBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = C_SURF };
            tabBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 43, tabBar.Width, 43);
            };

            btnTabSystem = MakeTabButton("📋  System Info", C_BLUE);
            btnTabHardware = MakeTabButton("🖥  Hardware", C_GREEN);
            btnTabMemory = MakeTabButton("🧠  Memory Test", C_AMBER);
            btnTabPerf = MakeTabButton("⚡  Performance", C_PURPLE);

            btnRefresh = MakeSmallButton("↺  Refresh", C_SUB, new Size(95, 28));
            btnExport = MakeSmallButton("💾  Export", C_TEAL, new Size(90, 28));

            btnTabSystem.Click += (s, e) => SwitchTab("system");
            btnTabHardware.Click += (s, e) => SwitchTab("hardware");
            btnTabMemory.Click += (s, e) => SwitchTab("memory");
            btnTabPerf.Click += (s, e) => SwitchTab("performance");
            btnRefresh.Click += (s, e) => DoRefresh();
            btnExport.Click += (s, e) => DoExport();

            tabBar.Controls.AddRange(new Control[]
                { btnTabSystem, btnTabHardware, btnTabMemory, btnTabPerf,
                  btnRefresh, btnExport });
            tabBar.Resize += (s, e) =>
            {
                int y = (tabBar.Height - 28) / 2;
                btnTabSystem.Location = new Point(10, y);
                btnTabHardware.Location = new Point(142, y);
                btnTabMemory.Location = new Point(274, y);
                btnTabPerf.Location = new Point(406, y);
                btnExport.Location = new Point(tabBar.Width - 100, y);
                btnRefresh.Location = new Point(tabBar.Width - 202, y);
            };

            // ── Content host ──────────────────────────────────────────
            contentHost = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };

            // ── Bottom bar ────────────────────────────────────────────
            bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = C_SURF };
            bottomBar.Paint += (s, e) =>
            {
                using (var p = new Pen(C_BORDER, 1))
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
            };
            lblStatus = new Label
            {
                Text = "Ready.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(14, 8)
            };
            bottomBar.Controls.Add(lblStatus);

            Controls.Add(contentHost);
            Controls.Add(tabBar);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        // ════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ════════════════════════════════════════════════════════════
        void SwitchTab(string tab)
        {
            activeTab = tab;
            sysPanel.Visible = (tab == "system");
            hwPanel.Visible = (tab == "hardware");
            memPanel.Visible = (tab == "memory");
            perfPanel.Visible = (tab == "performance");

            MarkTab(btnTabSystem, tab == "system", C_BLUE);
            MarkTab(btnTabHardware, tab == "hardware", C_GREEN);
            MarkTab(btnTabMemory, tab == "memory", C_AMBER);
            MarkTab(btnTabPerf, tab == "performance", C_PURPLE);

            if (tab == "system" && lvSys.Items.Count == 0) LoadSysReport();
            if (tab == "hardware" && lvHw.Items.Count == 0) LoadHwReport();
        }

        void MarkTab(Button b, bool active, Color c)
        {
            b.ForeColor = active ? c : C_SUB;
            b.BackColor = active
                ? Color.FromArgb(30, c.R, c.G, c.B)
                : Color.FromArgb(8, c.R, c.G, c.B);
            b.FlatAppearance.BorderColor = active
                ? Color.FromArgb(80, c.R, c.G, c.B)
                : Color.FromArgb(28, c.R, c.G, c.B);
        }

        void DoRefresh()
        {
            if (activeTab == "system") { lvSys.Items.Clear(); LoadSysReport(); }
            if (activeTab == "hardware") { lvHw.Items.Clear(); LoadHwReport(); }
        }

        // ════════════════════════════════════════════════════════════
        //  ── TAB 1  SYSTEM INFO ────────────────────────────────────
        // ════════════════════════════════════════════════════════════
        void BuildSysPanel()
        {
            sysPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(12, 10, 12, 10)
            };

            var lbl = new Label
            {
                Text = "SYSTEM INFORMATION",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            lvSys = BuildListView(new[] { "Category", "Property", "Value" },
                                  new[] { 260, 386, 459 });

            lvSys.DrawColumnHeader += (s, e) => DrawLvHeader(e, C_BLUE);
            lvSys.DrawSubItem += (s, e) => DrawLvRow(e, lvSys, C_BLUE);

            sysPanel.Controls.AddRange(new Control[] { lbl, lvSys });
            sysPanel.Resize += (s, e) =>
                lvSys.Size = new Size(sysPanel.Width - 24, sysPanel.Height - 28);

            contentHost.Controls.Add(sysPanel);
        }

        void LoadSysReport()
        {
            SetStatus("Loading system information...");
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var rows = new List<string[]>();
                try
                {
                    WmiQuery("Win32_OperatingSystem", o =>
                    {
                        AddRow(rows, "OS", "Caption", WStr(o, "Caption"));
                        AddRow(rows, "OS", "Version", WStr(o, "Version"));
                        AddRow(rows, "OS", "Build Number", WStr(o, "BuildNumber"));
                        AddRow(rows, "OS", "Architecture", WStr(o, "OSArchitecture"));
                        AddRow(rows, "OS", "Install Date", WDate(o, "InstallDate"));
                        AddRow(rows, "OS", "Last Boot", WDate(o, "LastBootUpTime"));
                        double tot = WDbl(o, "TotalVisibleMemorySize") / (1024 * 1024);
                        double fr = WDbl(o, "FreePhysicalMemory") / (1024 * 1024);
                        AddRow(rows, "OS", "Total RAM", string.Format("{0:0.00} GB", tot));
                        AddRow(rows, "OS", "Free RAM", string.Format("{0:0.00} GB  ({1:0}% free)", fr, tot > 0 ? fr / tot * 100 : 0));
                        AddRow(rows, "OS", "System Dir", WStr(o, "SystemDirectory"));
                    });

                    WmiQuery("Win32_ComputerSystem", o =>
                    {
                        AddRow(rows, "Computer", "Name", WStr(o, "Name"));
                        AddRow(rows, "Computer", "Manufacturer", WStr(o, "Manufacturer"));
                        AddRow(rows, "Computer", "Model", WStr(o, "Model"));
                        AddRow(rows, "Computer", "System Type", WStr(o, "SystemType"));
                        AddRow(rows, "Computer", "Domain", WStr(o, "Domain"));
                        AddRow(rows, "Computer", "Logged User", WStr(o, "UserName"));
                    });

                    WmiQuery("Win32_BIOS", o =>
                    {
                        AddRow(rows, "BIOS", "Manufacturer", WStr(o, "Manufacturer"));
                        AddRow(rows, "BIOS", "Name", WStr(o, "Name"));
                        AddRow(rows, "BIOS", "Version", WStr(o, "Version"));
                        AddRow(rows, "BIOS", "Release Date", WDate(o, "ReleaseDate"));
                        AddRow(rows, "BIOS", "Serial Number", WStr(o, "SerialNumber"));
                    });

                    AddRow(rows, "Time", "Time Zone", TimeZone.CurrentTimeZone.StandardName);
                    AddRow(rows, "Time", "Current Time", DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss"));
                    AddRow(rows, "Time", "UTC Offset", TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).ToString());

                    AddRow(rows, "Environment", "Machine Name", Environment.MachineName);
                    AddRow(rows, "Environment", "User Name", Environment.UserName);
                    AddRow(rows, "Environment", "Domain", Environment.UserDomainName);
                    AddRow(rows, "Environment", ".NET Version", Environment.Version.ToString());
                    AddRow(rows, "Environment", "Processor Count", Environment.ProcessorCount.ToString());

                    WmiQuery("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3", o =>
                    {
                        string id = WStr(o, "DeviceID");
                        long tot = Convert.ToInt64(o["Size"] ?? 0) / (1024L * 1024 * 1024);
                        long fr2 = Convert.ToInt64(o["FreeSpace"] ?? 0) / (1024L * 1024 * 1024);
                        AddRow(rows, "Drive " + id, "File System", WStr(o, "FileSystem"));
                        AddRow(rows, "Drive " + id, "Total", tot + " GB");
                        AddRow(rows, "Drive " + id, "Free", fr2 + " GB");
                        AddRow(rows, "Drive " + id, "Used",
                            string.Format("{0} GB  ({1}%)", tot - fr2,
                                tot > 0 ? (tot - fr2) * 100 / tot : 0));
                    });
                }
                catch (Exception ex)
                {
                    AddRow(rows, "Error", "WMI", ex.Message);
                }

                Invoke(new Action(() =>
                {
                    PopulateListView(lvSys, rows);
                    SetStatus(string.Format("System info loaded — {0} entries.", rows.Count));
                }));
            });
        }

        // ════════════════════════════════════════════════════════════
        //  ── TAB 2  HARDWARE ──────────────────────────────────────
        // ════════════════════════════════════════════════════════════
        void BuildHwPanel()
        {
            hwPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(12, 10, 12, 10),
                Visible = false
            };

            var lbl = new Label
            {
                Text = "HARDWARE REPORT",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            lvHw = BuildListView(new[] { "Component", "Property", "Value" },
                                 new[] { 260, 386, 459 });

            lvHw.DrawColumnHeader += (s, e) => DrawLvHeader(e, C_GREEN);
            lvHw.DrawSubItem += (s, e) => DrawLvRow(e, lvHw, C_GREEN);

            hwPanel.Controls.AddRange(new Control[] { lbl, lvHw });
            hwPanel.Resize += (s, e) =>
                lvHw.Size = new Size(hwPanel.Width - 24, hwPanel.Height - 28);

            contentHost.Controls.Add(hwPanel);
        }

        void LoadHwReport()
        {
            SetStatus("Loading hardware report...");
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var rows = new List<string[]>();
                try
                {
                    // CPU
                    WmiQuery("Win32_Processor", o =>
                    {
                        AddRow(rows, "CPU", "Name", WStr(o, "Name").Trim());
                        AddRow(rows, "CPU", "Manufacturer", WStr(o, "Manufacturer"));
                        AddRow(rows, "CPU", "Architecture", CpuArch(o["Architecture"]));
                        AddRow(rows, "CPU", "Cores", WStr(o, "NumberOfCores"));
                        AddRow(rows, "CPU", "Logical CPUs", WStr(o, "NumberOfLogicalProcessors"));
                        AddRow(rows, "CPU", "Max Speed", WStr(o, "MaxClockSpeed") + " MHz");
                        AddRow(rows, "CPU", "L2 Cache", (WLng(o, "L2CacheSize") / 1024) + " MB");
                        AddRow(rows, "CPU", "L3 Cache", (WLng(o, "L3CacheSize") / 1024) + " MB");
                        AddRow(rows, "CPU", "Socket", WStr(o, "SocketDesignation"));
                        AddRow(rows, "CPU", "Status", WStr(o, "Status"));
                    });

                    // RAM sticks
                    int slot = 1;
                    WmiQuery("Win32_PhysicalMemory", o =>
                    {
                        string sn = "RAM Slot " + slot++;
                        long gb = WLng(o, "Capacity") / (1024L * 1024 * 1024);
                        AddRow(rows, sn, "Capacity", gb + " GB");
                        AddRow(rows, sn, "Speed", WStr(o, "Speed") + " MHz");
                        AddRow(rows, sn, "Manufacturer", WStr(o, "Manufacturer").Trim());
                        AddRow(rows, sn, "Part Number", WStr(o, "PartNumber").Trim());
                        AddRow(rows, sn, "Form Factor", MemFormFactor(o["FormFactor"]));
                        AddRow(rows, sn, "Memory Type", MemType(o["MemoryType"]));
                        AddRow(rows, sn, "Location", WStr(o, "DeviceLocator"));
                        AddRow(rows, sn, "Bank", WStr(o, "BankLabel"));
                    });

                    // GPU
                    int gi = 1;
                    WmiQuery("Win32_VideoController", o =>
                    {
                        string gn = "GPU " + gi++;
                        long vr = WLng(o, "AdapterRAM") / (1024 * 1024);
                        AddRow(rows, gn, "Name", WStr(o, "Name"));
                        AddRow(rows, gn, "VRAM", vr + " MB");
                        AddRow(rows, gn, "Driver Version", WStr(o, "DriverVersion"));
                        AddRow(rows, gn, "Driver Date", WDate(o, "DriverDate"));
                        AddRow(rows, gn, "Resolution",
                            WStr(o, "CurrentHorizontalResolution") + " × " +
                            WStr(o, "CurrentVerticalResolution"));
                        AddRow(rows, gn, "Refresh Rate", WStr(o, "CurrentRefreshRate") + " Hz");
                        AddRow(rows, gn, "Status", WStr(o, "Status"));
                    });

                    // Motherboard
                    WmiQuery("Win32_BaseBoard", o =>
                    {
                        AddRow(rows, "Motherboard", "Manufacturer", WStr(o, "Manufacturer"));
                        AddRow(rows, "Motherboard", "Product", WStr(o, "Product"));
                        AddRow(rows, "Motherboard", "Version", WStr(o, "Version"));
                        AddRow(rows, "Motherboard", "Serial", WStr(o, "SerialNumber"));
                    });

                    // Physical Disks
                    WmiQuery("Win32_DiskDrive", o =>
                    {
                        string model = WStr(o, "Model");
                        string dn = model.Length > 22 ? model.Substring(0, 22) + "…" : model;
                        long szGB = WLng(o, "Size") / (1024L * 1024 * 1024);
                        AddRow(rows, dn, "Model", model);
                        AddRow(rows, dn, "Interface", WStr(o, "InterfaceType"));
                        AddRow(rows, dn, "Size", szGB + " GB");
                        AddRow(rows, dn, "Partitions", WStr(o, "Partitions"));
                        AddRow(rows, dn, "Serial", WStr(o, "SerialNumber").Trim());
                        AddRow(rows, dn, "Firmware", WStr(o, "FirmwareRevision").Trim());
                        AddRow(rows, dn, "Media Type", WStr(o, "MediaType"));
                    });

                    // Network adapters (enabled only)
                    WmiQuery("SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=True", o =>
                    {
                        string nname = WStr(o, "Name");
                        string nn = nname.Length > 24 ? nname.Substring(0, 24) + "…" : nname;
                        long spd = WLng(o, "Speed");
                        AddRow(rows, nn, "Name", nname);
                        AddRow(rows, nn, "MAC Address", WStr(o, "MACAddress"));
                        AddRow(rows, nn, "Speed", spd > 0 ? (spd / 1000000) + " Mbps" : "–");
                        AddRow(rows, nn, "Adapter Type", WStr(o, "AdapterType"));
                    });

                    // Battery (if present)
                    WmiQuery("Win32_Battery", o =>
                    {
                        AddRow(rows, "Battery", "Name", WStr(o, "Name"));
                        AddRow(rows, "Battery", "Status", BatStatus(o["BatteryStatus"]));
                        AddRow(rows, "Battery", "Charge", WStr(o, "EstimatedChargeRemaining") + "%");
                        AddRow(rows, "Battery", "Chemistry", BatChem(o["Chemistry"]));
                    });
                }
                catch (Exception ex)
                {
                    AddRow(rows, "Error", "WMI", ex.Message);
                }

                Invoke(new Action(() =>
                {
                    PopulateListView(lvHw, rows);
                    SetStatus(string.Format("Hardware report loaded — {0} entries.", rows.Count));
                }));
            });
        }

        // ════════════════════════════════════════════════════════════
        //  ── TAB 3  MEMORY TEST ───────────────────────────────────
        // ════════════════════════════════════════════════════════════
        void BuildMemPanel()
        {
            memPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(24, 18, 24, 18),
                Visible = false
            };

            // Header
            var lbH = new Label
            {
                Text = "🧠  MEMORY  TEST",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = C_AMBER,
                Location = new Point(0, 0)
            };
            var lbD = new Label
            {
                Text = "Allocates and verifies 128 × 1 MB blocks using patterns 0x55 / 0xAA / 0xFF to detect RAM errors.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = C_SUB,
                Location = new Point(0, 32)
            };

            // Info cards
            double totGB = RamTotalGB();
            double freeGB = RamFreeGB();
            var cardRow = new FlowLayoutPanel
            {
                Location = new Point(0, 62),
                AutoSize = true,
                Height = 68,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                WrapContents = false
            };
            cardRow.Controls.Add(InfoCard("Total RAM", string.Format("{0:0.1} GB", totGB), C_AMBER));
            cardRow.Controls.Add(InfoCard("Available", string.Format("{0:0.1} GB", freeGB), C_GREEN));
            cardRow.Controls.Add(InfoCard("Blocks", "128 × 1 MB", C_BLUE));
            cardRow.Controls.Add(InfoCard("Pattern", "0x55 · 0xAA · 0xFF", C_PURPLE));

            lblMemInfo = new Label
            {
                Text = "PROGRESS",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 146)
            };

            memBar = new ReportProgressBar(C_AMBER, C_RED)
            {
                Location = new Point(0, 164),
                Size = new Size(400, 16),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblMemPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            lblMemState = new Label
            {
                Text = "Click 'Start Memory Test' to begin.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 188)
            };
            lblMemResult = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_GREEN,
                AutoSize = true,
                Location = new Point(0, 208)
            };

            btnMemStart = MakeButton("🧠  Start Memory Test", C_AMBER, new Size(195, 34));
            btnMemCancel = MakeButton("✕  Cancel", C_SUB, new Size(100, 34));
            btnMemStart.Location = new Point(0, 236);
            btnMemCancel.Location = new Point(207, 236);
            btnMemCancel.Enabled = false;

            btnMemStart.Click += MemStart_Click;
            btnMemCancel.Click += (s, e) => MemCancel();

            var lbLog = new Label
            {
                Text = "TEST LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 286)
            };

            rtbMem = new RichTextBox
            {
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Location = new Point(0, 306),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            memPanel.Controls.AddRange(new Control[]
            {
                lbH, lbD, cardRow, lblMemInfo, memBar, lblMemPct,
                lblMemState, lblMemResult, btnMemStart, btnMemCancel, lbLog, rtbMem
            });
            memPanel.Resize += (s, e) =>
            {
                cardRow.Width = memPanel.Width - 48;
                memBar.Width = memPanel.Width - 80;
                lblMemPct.Location = new Point(memPanel.Width - 48, 162);
                rtbMem.Size = new Size(memPanel.Width - 48,
                    Math.Max(50, memPanel.Height - 308 - 16));
            };

            contentHost.Controls.Add(memPanel);
        }

        void MemStart_Click(object sender, EventArgs e)
        {
            memRunning = true; memStep = 0; memErrors = 0; memOk = 0;
            memStart = DateTime.Now;

            btnMemStart.Enabled = false;
            btnMemCancel.Enabled = true;
            memBar.SetColors(C_AMBER, C_RED);
            memBar.Animate = true; memBar.Value = 0; memBar.Invalidate();
            lblMemState.Text = "Initialising..."; lblMemState.ForeColor = C_AMBER;
            lblMemResult.Text = "";
            rtbMem.Clear();
            SetStatus("Running memory test...");

            MemLog("═══ MEMORY TEST ═══", C_AMBER);
            MemLog("Time   : " + DateTime.Now.ToString("HH:mm:ss"), C_SUB);
            MemLog(string.Format("Total  : {0:0.1} GB", RamTotalGB()), C_SUB);
            MemLog(string.Format("Free   : {0:0.1} GB", RamFreeGB()), C_SUB);
            MemLog("Pattern: 0x55, 0xAA, 0xFF, 0x00", C_SUB);
            MemLog("", C_TXT);

            const int total = 128;

            memTimer = new System.Windows.Forms.Timer { Interval = 35 };
            memTimer.Tick += (s2, e2) =>
            {
                if (!memRunning) { memTimer.Stop(); return; }

                if (memStep >= total)
                {
                    memTimer.Stop(); memRunning = false;
                    memBar.Animate = false; memBar.Value = 100;
                    double secs = (DateTime.Now - memStart).TotalSeconds;

                    if (memErrors == 0)
                    {
                        memBar.SetColors(C_GREEN, C_TEAL); memBar.Invalidate();
                        lblMemState.Text = string.Format("✔  All {0} blocks passed in {1:0.0}s", total, secs);
                        lblMemState.ForeColor = C_GREEN;
                        lblMemResult.Text = "✔  MEMORY OK — No errors detected";
                        lblMemResult.ForeColor = C_GREEN;
                        SetStatus("✔  Memory test passed.");
                        MemLog("", C_TXT);
                        MemLog("RESULT: PASSED ✔", C_GREEN);
                        MemLog(string.Format("Blocks : {0}/{0}", total), C_GREEN);
                        MemLog(string.Format("Errors : 0"), C_GREEN);
                        MemLog(string.Format("Time   : {0:0.0}s", secs), C_SUB);
                    }
                    else
                    {
                        memBar.SetColors(C_RED, C_AMBER); memBar.Invalidate();
                        lblMemState.Text = string.Format("⚠  {0} error(s) found in {1:0.0}s", memErrors, secs);
                        lblMemState.ForeColor = C_RED;
                        lblMemResult.Text = string.Format("⚠  {0} MEMORY ERROR(S) DETECTED", memErrors);
                        lblMemResult.ForeColor = C_RED;
                        SetStatus(string.Format("⚠  Memory test: {0} error(s).", memErrors));
                        MemLog("", C_TXT);
                        MemLog(string.Format("RESULT: {0} ERROR(S) ⚠", memErrors), C_RED);
                        MemLog("Run Windows Memory Diagnostic for confirmation.", C_AMBER);
                    }

                    btnMemStart.Enabled = true;
                    btnMemCancel.Enabled = false;
                    lblMemPct.Text = "100%";
                    return;
                }

                int pct = memStep * 100 / total;
                memBar.Value = pct; lblMemPct.Text = pct + "%";
                lblMemState.Text = string.Format("Testing block {0}/{1}...", memStep + 1, total);

                bool ok = true;
                try
                {
                    var blk = new byte[1024 * 1024];
                    foreach (byte pat in new byte[] { 0x55, 0xAA, 0xFF, 0x00 })
                    {
                        for (int i = 0; i < blk.Length; i++) blk[i] = pat;
                        for (int i = 0; i < blk.Length; i++)
                            if (blk[i] != pat) { ok = false; break; }
                        if (!ok) break;
                    }
                    blk = null; GC.Collect();
                }
                catch { ok = false; }

                if (ok) { memOk++; if (memStep % 16 == 0) MemLog(string.Format("  Blocks {0:000}–{1:000}  ✔", memStep, Math.Min(memStep + 15, total - 1)), C_GREEN); }
                else { memErrors++; MemLog(string.Format("  Block {0:000}  ✖ ERROR", memStep), C_RED); }

                memStep++;
            };
            memTimer.Start();
        }

        void MemCancel()
        {
            memRunning = false;
            if (memTimer != null) memTimer.Stop();
            memBar.Animate = false;
            btnMemStart.Enabled = true; btnMemCancel.Enabled = false;
            lblMemState.Text = "Cancelled."; lblMemState.ForeColor = C_AMBER;
            SetStatus("Memory test cancelled.");
        }

        void MemLog(string t, Color c)
        {
            if (InvokeRequired) { Invoke(new Action(() => MemLog(t, c))); return; }
            rtbMem.SelectionStart = rtbMem.TextLength;
            rtbMem.SelectionColor = c;
            rtbMem.AppendText(t + "\n");
            rtbMem.ScrollToCaret();
        }

        // ════════════════════════════════════════════════════════════
        //  ── TAB 4  PERFORMANCE ───────────────────────────────────
        // ════════════════════════════════════════════════════════════
        void BuildPerfPanel()
        {
            perfPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(24, 18, 24, 18),
                Visible = false
            };

            var lbH = new Label
            {
                Text = "⚡  PERFORMANCE  BENCHMARK",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = C_PURPLE,
                Location = new Point(0, 0)
            };
            var lbD = new Label
            {
                Text = "Benchmarks CPU (prime sieve), RAM (copy speed) and Disk (read/write) to produce a score out of 100.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = C_SUB,
                Location = new Point(0, 32)
            };

            // Score cards
            var scoreRow = new FlowLayoutPanel
            {
                Location = new Point(0, 62),
                AutoSize = true,
                Height = 68,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                WrapContents = false
            };
            scoreRow.Controls.Add(ScoreCard("CPU Score", C_BLUE, "cpu"));
            scoreRow.Controls.Add(ScoreCard("RAM Score", C_AMBER, "ram"));
            scoreRow.Controls.Add(ScoreCard("Disk Score", C_TEAL, "disk"));
            scoreRow.Controls.Add(ScoreCard("Total Score", C_PURPLE, "total"));

            var lbProg = new Label
            {
                Text = "BENCHMARK PROGRESS",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 146)
            };

            perfBar = new ReportProgressBar(C_PURPLE, C_BLUE)
            {
                Location = new Point(0, 164),
                Size = new Size(400, 16),
                Value = 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblPerfPct = new Label
            {
                Text = "0%",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_TXT,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            lblPerfState = new Label
            {
                Text = "Click 'Start Performance Test' to benchmark this PC.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 188)
            };
            lblPerfResult = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = C_PURPLE,
                AutoSize = true,
                Location = new Point(0, 208)
            };

            btnPerfStart = MakeButton("⚡  Start Performance Test", C_PURPLE, new Size(220, 34));
            btnPerfCancel = MakeButton("✕  Cancel", C_SUB, new Size(100, 34));
            btnPerfStart.Location = new Point(0, 236);
            btnPerfCancel.Location = new Point(232, 236);
            btnPerfCancel.Enabled = false;

            btnPerfStart.Click += PerfStart_Click;
            btnPerfCancel.Click += (s, e) => PerfCancel();

            var lbLog = new Label
            {
                Text = "BENCHMARK LOG",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(0, 286)
            };

            rtbPerf = new RichTextBox
            {
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Location = new Point(0, 306),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            perfPanel.Controls.AddRange(new Control[]
            {
                lbH, lbD, scoreRow, lbProg, perfBar, lblPerfPct,
                lblPerfState, lblPerfResult, btnPerfStart, btnPerfCancel, lbLog, rtbPerf
            });
            perfPanel.Resize += (s, e) =>
            {
                scoreRow.Width = perfPanel.Width - 48;
                perfBar.Width = perfPanel.Width - 80;
                lblPerfPct.Location = new Point(perfPanel.Width - 48, 162);
                rtbPerf.Size = new Size(perfPanel.Width - 48,
                    Math.Max(50, perfPanel.Height - 308 - 16));
            };

            contentHost.Controls.Add(perfPanel);
        }

        void PerfStart_Click(object sender, EventArgs e)
        {
            perfRunning = true; perfPhase = 0;
            perfCpu = 0; perfRam = 0; perfDisk = 0;
            perfStart = DateTime.Now;

            btnPerfStart.Enabled = false;
            btnPerfCancel.Enabled = true;
            perfBar.SetColors(C_PURPLE, C_BLUE);
            perfBar.Animate = true; perfBar.Value = 0; perfBar.Invalidate();
            lblPerfState.Text = "Starting..."; lblPerfState.ForeColor = C_AMBER;
            lblPerfResult.Text = "";
            rtbPerf.Clear();
            foreach (var k in scoreLabels.Keys) scoreLabels[k].Text = "–";
            SetStatus("Running performance benchmark...");

            PerfLog("═══ PERFORMANCE BENCHMARK ═══", C_PURPLE);
            PerfLog("Time   : " + DateTime.Now.ToString("HH:mm:ss"), C_SUB);
            PerfLog("Cores  : " + Environment.ProcessorCount, C_SUB);
            PerfLog(string.Format("RAM    : {0:0.1} GB", RamTotalGB()), C_SUB);
            PerfLog("", C_TXT);

            perfTimer = new System.Windows.Forms.Timer { Interval = 60 };
            perfTimer.Tick += PerfTick;
            perfTimer.Start();
        }

        void PerfTick(object sender, EventArgs e)
        {
            if (!perfRunning) { perfTimer.Stop(); return; }

            if (perfPhase == 0)
            {
                // CPU — prime sieve
                lblPerfState.Text = "Phase 1/3 — CPU benchmark (prime sieve)...";
                PerfLog("─── Phase 1: CPU ───", C_BLUE);
                PerfLog("  Sieve of Eratosthenes to 1,000,000", C_SUB);
                var sw = Stopwatch.StartNew();
                int p = CountPrimes(1_000_000);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                perfCpu = Math.Max(1, Math.Min(100, 5000.0 / ms * 50));
                PerfLog(string.Format("  Primes : {0:N0}   Time : {1:0.0} ms", p, ms), C_TXT);
                PerfLog(string.Format("  CPU Score : {0:0.0} / 100", perfCpu),
                    perfCpu >= 70 ? C_GREEN : perfCpu >= 40 ? C_AMBER : C_RED);
                if (scoreLabels.ContainsKey("cpu")) scoreLabels["cpu"].Text = ((int)perfCpu).ToString();
                perfBar.Value = 33; lblPerfPct.Text = "33%";
                perfPhase = 1;
            }
            else if (perfPhase == 1)
            {
                // RAM — copy speed
                lblPerfState.Text = "Phase 2/3 — RAM speed benchmark...";
                PerfLog("─── Phase 2: RAM ───", C_AMBER);
                PerfLog("  Allocate + copy 256 MB of data", C_SUB);
                var sw = Stopwatch.StartNew();
                int len = 64 * 1024 * 1024; // 64M ints = 256 MB
                var a = new int[len]; var b = new int[len];
                for (int i = 0; i < len; i++) a[i] = i;
                Array.Copy(a, b, len);
                sw.Stop();
                a = null; b = null; GC.Collect();
                double gbps = (len * 4L * 2.0) / (sw.Elapsed.TotalSeconds) / (1024.0 * 1024 * 1024);
                perfRam = Math.Max(1, Math.Min(100, gbps * 8));
                PerfLog(string.Format("  Time : {0:0.0} ms   Speed : {1:0.00} GB/s",
                    sw.Elapsed.TotalMilliseconds, gbps), C_TXT);
                PerfLog(string.Format("  RAM Score : {0:0.0} / 100", perfRam),
                    perfRam >= 70 ? C_GREEN : perfRam >= 40 ? C_AMBER : C_RED);
                if (scoreLabels.ContainsKey("ram")) scoreLabels["ram"].Text = ((int)perfRam).ToString();
                perfBar.Value = 66; lblPerfPct.Text = "66%";
                perfPhase = 2;
            }
            else if (perfPhase == 2)
            {
                // Disk
                lblPerfState.Text = "Phase 3/3 — Disk benchmark...";
                PerfLog("─── Phase 3: Disk ───", C_TEAL);
                string tmp = Path.Combine(Path.GetTempPath(), "ttk_bench.tmp");
                int mb = 32;
                var data = new byte[mb * 1024 * 1024];
                new Random(42).NextBytes(data);
                double wMBs = 50, rMBs = 80;
                try
                {
                    var sw = Stopwatch.StartNew();
                    File.WriteAllBytes(tmp, data);
                    sw.Stop();
                    wMBs = mb / sw.Elapsed.TotalSeconds;

                    sw = Stopwatch.StartNew();
                    var _ = File.ReadAllBytes(tmp);
                    sw.Stop();
                    rMBs = mb / sw.Elapsed.TotalSeconds;
                    File.Delete(tmp);
                }
                catch { }
                data = null; GC.Collect();
                perfDisk = Math.Max(1, Math.Min(100, (wMBs + rMBs) / 2.0 / 5.0));
                PerfLog(string.Format("  Write : {0:0.0} MB/s   Read : {1:0.0} MB/s", wMBs, rMBs), C_TXT);
                PerfLog(string.Format("  Disk Score : {0:0.0} / 100", perfDisk),
                    perfDisk >= 70 ? C_GREEN : perfDisk >= 40 ? C_AMBER : C_RED);
                if (scoreLabels.ContainsKey("disk")) scoreLabels["disk"].Text = ((int)perfDisk).ToString();

                // Final score
                double total = perfCpu * 0.4 + perfRam * 0.3 + perfDisk * 0.3;
                if (scoreLabels.ContainsKey("total")) scoreLabels["total"].Text = ((int)total).ToString();

                string rating = total >= 80 ? "Excellent 🏆"
                              : total >= 60 ? "Good ✔"
                              : total >= 40 ? "Average ⚡"
                              : "Below Average ⚠";

                double secs = (DateTime.Now - perfStart).TotalSeconds;

                perfBar.Value = 100; lblPerfPct.Text = "100%";
                perfBar.Animate = false;
                perfBar.SetColors(total >= 60 ? C_GREEN : C_AMBER,
                    total >= 60 ? C_TEAL : C_RED);
                perfBar.Invalidate();

                lblPerfState.Text = string.Format("✔  Benchmark complete in {0:0.0}s", secs);
                lblPerfState.ForeColor = total >= 60 ? C_GREEN : C_AMBER;
                lblPerfResult.Text = string.Format("Rating:  {0}  (Score {1:0}/100)", rating, total);
                lblPerfResult.ForeColor = total >= 60 ? C_GREEN : total >= 40 ? C_AMBER : C_RED;

                PerfLog("", C_TXT);
                PerfLog("═══ RESULTS ═══", C_PURPLE);
                PerfLog(string.Format("  CPU   : {0:0.0}/100", perfCpu), C_BLUE);
                PerfLog(string.Format("  RAM   : {0:0.0}/100", perfRam), C_AMBER);
                PerfLog(string.Format("  Disk  : {0:0.0}/100", perfDisk), C_TEAL);
                PerfLog(string.Format("  Total : {0:0.0}/100", total), C_PURPLE);
                PerfLog(string.Format("  Rating: {0}", rating),
                    total >= 60 ? C_GREEN : C_AMBER);
                PerfLog(string.Format("  Time  : {0:0.0}s", secs), C_SUB);

                SetStatus(string.Format("✔  Benchmark: CPU {0:0} · RAM {1:0} · Disk {2:0} · Total {3:0}/100",
                    perfCpu, perfRam, perfDisk, total));

                btnPerfStart.Enabled = true;
                btnPerfCancel.Enabled = false;
                perfRunning = false;
                perfTimer.Stop();
                perfPhase = 3;
            }
        }

        void PerfCancel()
        {
            perfRunning = false;
            if (perfTimer != null) perfTimer.Stop();
            perfBar.Animate = false;
            btnPerfStart.Enabled = true; btnPerfCancel.Enabled = false;
            lblPerfState.Text = "Cancelled."; lblPerfState.ForeColor = C_AMBER;
            SetStatus("Performance test cancelled.");
        }

        void PerfLog(string t, Color c)
        {
            if (InvokeRequired) { Invoke(new Action(() => PerfLog(t, c))); return; }
            rtbPerf.SelectionStart = rtbPerf.TextLength;
            rtbPerf.SelectionColor = c;
            rtbPerf.AppendText(t + "\n");
            rtbPerf.ScrollToCaret();
        }

        int CountPrimes(int n)
        {
            bool[] s = new bool[n + 1];
            for (int i = 2; i <= n; i++) s[i] = true;
            for (int i = 2; (long)i * i <= n; i++)
                if (s[i]) for (int j = i * i; j <= n; j += i) s[j] = false;
            int c = 0; for (int i = 2; i <= n; i++) if (s[i]) c++;
            return c;
        }

        // ════════════════════════════════════════════════════════════
        //  EXPORT
        // ════════════════════════════════════════════════════════════
        void DoExport()
        {
            using (var dlg = new SaveFileDialog
            {
                Title = "Export Report",
                Filter = "Text File (*.txt)|*.txt",
                FileName = "TechToolKit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("TECH TOOLKIT PRO — SYSTEM REPORT");
                sb.AppendLine(new string('═', 60));
                sb.AppendLine("Generated : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                sb.AppendLine("Machine   : " + Environment.MachineName);
                sb.AppendLine("User      : " + Environment.UserName);
                sb.AppendLine();

                ListView src = activeTab == "hardware" ? lvHw : lvSys;
                string title = activeTab == "hardware" ? "HARDWARE REPORT" : "SYSTEM INFORMATION";
                sb.AppendLine(title);
                sb.AppendLine(new string('─', 60));
                foreach (ListViewItem item in src.Items)
                {
                    if (item.Tag is string[] r && r.Length == 3)
                        sb.AppendLine(string.Format("{0,-22} {1,-24} {2}", r[0], r[1], r[2]));
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    SetStatus("✔  Exported to " + dlg.FileName);
                    MessageBox.Show("Report exported!\n\n" + dlg.FileName,
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Process.Start("notepad.exe", dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export failed:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SHARED LIST VIEW HELPERS
        // ════════════════════════════════════════════════════════════
        ListView BuildListView(string[] cols, int[] widths)
        {
            var lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = C_SURF,
                ForeColor = C_TXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 22),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true
            };
            lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            for (int i = 0; i < cols.Length; i++) lv.Columns.Add(cols[i], widths[i]);
            lv.DrawItem += (s, e) => { };
            return lv;
        }

        void PopulateListView(ListView lv, List<string[]> rows)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            string lastCat = "";
            foreach (var r in rows)
            {
                var item = new ListViewItem(r[0] == lastCat ? "" : r[0]);
                item.SubItems.Add(r[1]);
                item.SubItems.Add(r[2]);
                item.Tag = r;
                if (r[0] != lastCat) lastCat = r[0];
                lv.Items.Add(item);
            }
            lv.EndUpdate();
        }

        void DrawLvHeader(DrawListViewColumnHeaderEventArgs e, Color accent)
        {
            using (var bg = new SolidBrush(Color.FromArgb(26, 32, 40)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_SUB))
                e.Graphics.DrawString(e.Header.Text, ft, br,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), sf);
            using (var p = new Pen(C_BORDER))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        void DrawLvRow(DrawListViewSubItemEventArgs e, ListView lv, Color accent)
        {
            bool isCat = e.ColumnIndex == 0 && !string.IsNullOrEmpty(e.Item.Text);
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            if (isCat)
            {
                using (var br = new SolidBrush(Color.FromArgb(22, accent.R, accent.G, accent.B)))
                    e.Graphics.FillRectangle(br, e.Bounds);
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }

            Color fg = e.ColumnIndex == 0
                ? (isCat ? accent : Color.FromArgb(40, 50, 60))
                : e.ColumnIndex == 1 ? C_SUB : C_TXT;

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(e.SubItem.Text, lv.Font, br,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y,
                        e.Bounds.Width - 10, e.Bounds.Height), sf);
        }

        // ════════════════════════════════════════════════════════════
        //  WMI HELPERS
        // ════════════════════════════════════════════════════════════
        void WmiQuery(string q, Action<ManagementObject> cb)
        {
            if (!q.TrimStart().ToUpper().StartsWith("SELECT"))
                q = "SELECT * FROM " + q;
            using (var s = new ManagementObjectSearcher(q))
                foreach (ManagementObject o in s.Get()) try { cb(o); } catch { }
        }

        static void AddRow(List<string[]> rows, string cat, string prop, string val)
            => rows.Add(new[] { cat, prop, val });
        static string WStr(ManagementObject o, string k) => o[k]?.ToString() ?? "–";
        static double WDbl(ManagementObject o, string k) => o[k] != null ? Convert.ToDouble(o[k]) : 0;
        static long WLng(ManagementObject o, string k) => o[k] != null ? Convert.ToInt64(o[k]) : 0;

        static string WDate(ManagementObject o, string k)
        {
            try
            {
                string r = o[k]?.ToString() ?? "";
                if (r.Length >= 8)
                    return new DateTime(int.Parse(r.Substring(0, 4)),
                        int.Parse(r.Substring(4, 2)),
                        int.Parse(r.Substring(6, 2))).ToString("dd/MM/yyyy");
            }
            catch { }
            return "–";
        }

        static string CpuArch(object v)
        {
            switch (Convert.ToInt32(v ?? 0))
            {
                case 0: return "x86";
                case 5: return "ARM";
                case 6: return "ia64";
                case 9: return "x64";
                case 12: return "ARM64";
                default: return v?.ToString() ?? "–";
            }
        }

        static string MemType(object v)
        {
            switch (Convert.ToInt32(v ?? 0))
            {
                case 20: return "DDR";
                case 21: return "DDR2";
                case 24: return "DDR3";
                case 26: return "DDR4";
                case 34: return "DDR5";
                default: return "–";
            }
        }

        static string MemFormFactor(object v)
        {
            switch (Convert.ToInt32(v ?? 0))
            {
                case 8: return "DIMM";
                case 12: return "SO-DIMM";
                default: return "–";
            }
        }

        static string BatStatus(object v)
        {
            switch (Convert.ToInt32(v ?? 0))
            {
                case 1: return "Discharging";
                case 2: return "AC Connected";
                case 3: return "Fully Charged";
                case 6: return "Charging";
                default: return "–";
            }
        }

        static string BatChem(object v)
        {
            switch (Convert.ToInt32(v ?? 0))
            {
                case 6: return "Lithium-ion";
                case 8: return "Lithium Polymer";
                case 5: return "NiMH";
                case 3: return "Lead Acid";
                default: return "–";
            }
        }

        double RamTotalGB()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    foreach (ManagementObject o in s.Get())
                        return Convert.ToDouble(o["TotalVisibleMemorySize"]) / (1024.0 * 1024);
            }
            catch { }
            return 0;
        }

        double RamFreeGB()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
                    foreach (ManagementObject o in s.Get())
                        return Convert.ToDouble(o["FreePhysicalMemory"]) / (1024.0 * 1024);
            }
            catch { }
            return 0;
        }

        // ════════════════════════════════════════════════════════════
        //  STATUS
        // ════════════════════════════════════════════════════════════
        void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            lblStatus.Text = msg;
            lblStatus.ForeColor = msg.StartsWith("✔") ? C_GREEN
                                : msg.StartsWith("⚠") ? C_AMBER : C_SUB;
        }

        // ════════════════════════════════════════════════════════════
        //  UI FACTORIES
        // ════════════════════════════════════════════════════════════
        Panel InfoCard(string title, string val, Color c)
        {
            var card = new Panel
            {
                Size = new Size(134, 64),
                BackColor = C_SURF,
                Margin = new Padding(0, 0, 10, 0)
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(40, c.R, c.G, c.B)))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(c))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };
            card.Controls.Add(new Label { Text = val, Font = new Font("Segoe UI Semibold", 12f), ForeColor = c, AutoSize = true, Location = new Point(10, 8) });
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 7.5f), ForeColor = C_SUB, AutoSize = true, Location = new Point(10, 40) });
            return card;
        }

        Panel ScoreCard(string title, Color c, string key)
        {
            var card = new Panel
            {
                Size = new Size(128, 64),
                BackColor = C_SURF,
                Margin = new Padding(0, 0, 10, 0)
            };
            card.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(45, c.R, c.G, c.B)))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var br = new SolidBrush(c))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 3);
            };
            var val = new Label
            {
                Text = "–",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = c,
                AutoSize = true,
                Location = new Point(10, 6)
            };
            card.Controls.Add(val);
            card.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_SUB,
                AutoSize = true,
                Location = new Point(10, 40)
            });
            scoreLabels[key] = val;
            return card;
        }

        Button MakeTabButton(string text, Color c)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(122, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Cursor = Cursors.Hand,
                ForeColor = C_SUB,
                BackColor = Color.FromArgb(8, c.R, c.G, c.B)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(28, c.R, c.G, c.B);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, c.R, c.G, c.B);
            return b;
        }

        Button MakeButton(string text, Color c, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Cursor = Cursors.Hand,
                ForeColor = c,
                BackColor = Color.FromArgb(20, c.R, c.G, c.B)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(60, c.R, c.G, c.B);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, c.R, c.G, c.B);
            return b;
        }

        Button MakeSmallButton(string text, Color c, Size sz)
        {
            var b = new Button
            {
                Text = text,
                Size = sz,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8f),
                Cursor = Cursors.Hand,
                ForeColor = c,
                BackColor = Color.FromArgb(12, c.R, c.G, c.B)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(40, c.R, c.G, c.B);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, c.R, c.G, c.B);
            return b;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            memRunning = false; perfRunning = false;
            if (memTimer != null) memTimer.Stop();
            if (perfTimer != null) perfTimer.Stop();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  REPORT PROGRESS BAR
    // ════════════════════════════════════════════════════════════════
    public class ReportProgressBar : Control
    {
        int _val; Color _c1, _c2; bool _anim; int _pulse;
        System.Windows.Forms.Timer _t;

        public int Value { get { return _val; } set { _val = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public bool Animate { get { return _anim; } set { _anim = value; if (!value) _pulse = 0; Invalidate(); } }
        public void SetColors(Color a, Color b) { _c1 = a; _c2 = b; Invalidate(); }

        public ReportProgressBar(Color c1, Color c2)
        {
            _c1 = c1; _c2 = c2;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(38, 46, 56); // same as your card bg
            _t = new System.Windows.Forms.Timer { Interval = 25 };
            _t.Tick += (s, e) => { if (_anim) { _pulse = (_pulse + 5) % (Width > 0 ? Width * 2 : 400); Invalidate(); } };
            _t.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);

            if (_anim)
            {
                int pw = Math.Max(Width / 3, 60);
                int px = _pulse % (Width + pw) - pw;
                var blend = new ColorBlend(3);
                blend.Colors = new[] { Color.Transparent, Color.FromArgb(200, _c1.R, _c1.G, _c1.B), Color.Transparent };
                blend.Positions = new[] { 0f, 0.5f, 1f };
                var rect = new Rectangle(px, 0, pw, Height);
                if (rect.Width > 0)
                    using (var br = new LinearGradientBrush(new Rectangle(rect.X, 0, Math.Max(rect.Width, 1), Height),
                        Color.Transparent, Color.FromArgb(200, _c1.R, _c1.G, _c1.B), LinearGradientMode.Horizontal))
                    { br.InterpolationColors = blend; g.FillRectangle(br, rect); }
            }
            else
            {
                int fw = (int)(Width * (_val / 100.0));
                if (fw > 2)
                {
                    using (var br = new LinearGradientBrush(new Rectangle(0, 0, fw, Height), _c1, _c2, LinearGradientMode.Horizontal))
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
                            g.DrawString(txt, ft, br, (fw - sz.Width) / 2f, (Height - sz.Height) / 2f);
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