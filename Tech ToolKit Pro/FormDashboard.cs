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
    public partial class FormDashboard : Form
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
        static readonly Color C_TXTSUB = Color.FromArgb(139, 148, 158);

        // ════════════════════════════════════════════════════════════
        //  CONTROLS
        // ════════════════════════════════════════════════════════════
        Panel topBar;
        Label lblTitle, lblClock;

        // Metric cards
        Panel cpuCard, ramCard, diskCard;
        RingGauge cpuRing, ramRing, diskRing;
        BarMeter cpuBar, ramBar, diskBar;
        SparkChart cpuSpark, ramSpark;
        Label cpuBig, ramBig, diskBig;
        Label cpuSub, ramSub, diskSub;
        Label cpuTag, ramTag, diskTag;

        // Process table
        Panel tablePanel;
        Label lblProcs, lblProcCount;
        ListView procView;

        // ════════════════════════════════════════════════════════════
        //  DATA
        // ════════════════════════════════════════════════════════════
        System.Windows.Forms.Timer ticker;
        ManagementObjectSearcher wCpu, wOs, wDisk;
        Queue<float> cpuQ = new Queue<float>();
        Queue<float> ramQ = new Queue<float>();
        const int QLEN = 60;

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════
        public FormDashboard()
        {
            wCpu = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            wOs = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            wDisk = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");

            SetupUI();

            ticker = new System.Windows.Forms.Timer();
            ticker.Interval = 2000;
            ticker.Tick += (s, e) => Refresh_Data();
            ticker.Start();

            Refresh_Data();
        }

        // ════════════════════════════════════════════════════════════
        //  UI SETUP
        // ════════════════════════════════════════════════════════════
        void SetupUI()
        {
            Text = "Dashboard";
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
                // left accent stripe
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, 4, 52), C_BLUE, C_GREEN,
                    LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(br, 0, 0, 4, 32);
            };

            lblTitle = new Label
            {
                Text = "LIVE  SYSTEM  DASHBOARD",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            lblClock = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_TXTSUB,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Blinking dot
            var dot = new Label
            {
                Text = "●  LIVE",
                Font = new Font("Segoe UI Semibold", 7.5f),
                ForeColor = C_GREEN,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            topBar.Controls.AddRange(new Control[] { lblTitle, dot, lblClock });
            topBar.Resize += (s, e) =>
            {
                dot.Location = new Point(topBar.Width - dot.Width - 130, 18);
                lblClock.Location = new Point(topBar.Width - 120, 18);
            };

            // ── Card row ──────────────────────────────────────────────
            var cardRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 162,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cardRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            cpuCard = BuildCard(C_BLUE, "CPU", out cpuRing, out cpuBar, out cpuBig, out cpuSub, out cpuTag, out cpuSpark, true);
            ramCard = BuildCard(C_GREEN, "MEMORY", out ramRing, out ramBar, out ramBig, out ramSub, out ramTag, out ramSpark, true);
            diskCard = BuildCard(C_AMBER, "DISK", out diskRing, out diskBar, out diskBig, out diskSub, out diskTag, out _, false);

            cpuCard.Dock = ramCard.Dock = diskCard.Dock = DockStyle.Fill;
            cardRow.Controls.Add(cpuCard, 0, 0);
            cardRow.Controls.Add(ramCard, 1, 0);
            cardRow.Controls.Add(diskCard, 2, 0);

            // ── Process table ─────────────────────────────────────────
            tablePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(10, 8, 10, 10)
            };

            lblProcs = new Label
            {
                Text = "ACTIVE  PROCESSES",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_TXTSUB,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            lblProcCount = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_BLUE,
                AutoSize = true,
                Location = new Point(175, 1)
            };

            procView = new ListView
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
            procView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left | AnchorStyles.Right;

            // Columns
            procView.Columns.Add("Process", 275);
            procView.Columns.Add("PID", 175);
            procView.Columns.Add("CPU %", 180);
            procView.Columns.Add("RAM (MB)", 187);
            procView.Columns.Add("Threads", 180);
            procView.Columns.Add("Status", 112);

            procView.DrawColumnHeader += DrawHeader;
            procView.DrawItem += (s, e) => { };
            procView.DrawSubItem += DrawRow;

            tablePanel.Controls.AddRange(new Control[] { lblProcs, lblProcCount, procView });
            tablePanel.Resize += (s, e) =>
                procView.Size = new Size(tablePanel.Width - 20, tablePanel.Height - 28);

            // ── Assemble ──────────────────────────────────────────────
            Controls.Add(tablePanel);
            Controls.Add(cardRow);
            Controls.Add(topBar);
        }

        // ════════════════════════════════════════════════════════════
        //  CARD FACTORY
        // ════════════════════════════════════════════════════════════
        Panel BuildCard(Color accent, string title,
            out RingGauge ring, out BarMeter bar,
            out Label big, out Label sub,
            out Label tag, out SparkChart spark,
            bool withSpark)
        {
            var card = new Panel { BackColor = C_SURF, Margin = new Padding(4) };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                // top accent bar
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, card.Width, 3),
                    accent, Color.FromArgb(180, accent.R, accent.G, accent.B),
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 0, card.Width, 3);
                // border
                using (var p = new Pen(Color.FromArgb(45, accent.R, accent.G, accent.B), 1))
                    g.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            tag = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = accent,
                AutoSize = true,
                Location = new Point(105, 10)
            };

            big = new Label
            {
                Text = "–",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = C_TXT,
                AutoSize = true,
                Location = new Point(103, 26)
            };

            sub = new Label
            {
                Text = "–",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = C_TXTSUB,
                AutoSize = true,
                Location = new Point(105, 68)
            };

            ring = new RingGauge(accent)
            {
                Size = new Size(88, 88),
                Location = new Point(8, 30)
            };

            // Use local variables for controls that will be captured by lambdas
            var localBar = new BarMeter(accent)
            {
                Size = new Size(10, 10),   // resized on card.Resize
                Location = new Point(105, 84),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            SparkChart localSpark = null;
            if (withSpark)
            {
                localSpark = new SparkChart(accent)
                {
                    Size = new Size(10, 32),
                    Location = new Point(105, 95),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
            }

            card.Controls.AddRange(new Control[] { tag, big, sub, ring, localBar });
            if (localSpark != null) card.Controls.Add(localSpark);

            card.Resize += (s, e) =>
            {
                int w = card.Width - 118;
                localBar.Size = new Size(w, 7);
                if (localSpark != null)
                {
                    localSpark.Size = new Size(w, 32);
                }
            };

            // assign out parameters from locals
            bar = localBar;
            spark = localSpark;

            return card;
        }

        // ════════════════════════════════════════════════════════════
        //  DATA REFRESH
        // ════════════════════════════════════════════════════════════
        void Refresh_Data()
        {
            try
            {
                // ── CPU ───────────────────────────────────────────────
                float cpu = 0;
                foreach (ManagementObject o in wCpu.Get())
                    cpu = Convert.ToSingle(o["LoadPercentage"]);

                cpuQ.Enqueue(cpu);
                if (cpuQ.Count > QLEN) cpuQ.Dequeue();

                cpuRing.Value = cpu;
                cpuBar.Value = cpu;
                cpuBig.Text = string.Format("{0:0}%", cpu);
                cpuSub.Text = string.Format("Utilisation  {0:0.0}%", cpu);
                cpuSpark.Data = cpuQ.ToArray();

                // ── RAM ───────────────────────────────────────────────
                float rPct = 0;
                string rStr = "";
                foreach (ManagementObject o in wOs.Get())
                {
                    double tot = Convert.ToDouble(o["TotalVisibleMemorySize"]) / (1024.0 * 1024);
                    double free = Convert.ToDouble(o["FreePhysicalMemory"]) / (1024.0 * 1024);
                    double used = tot - free;
                    rPct = (float)(used / tot * 100);
                    rStr = string.Format("{0:0.1} / {1:0.1} GB", used, tot);
                }
                ramQ.Enqueue(rPct);
                if (ramQ.Count > QLEN) ramQ.Dequeue();

                ramRing.Value = rPct;
                ramBar.Value = rPct;
                ramBig.Text = rStr;
                ramSub.Text = string.Format("In use  {0:0.0}%", rPct);
                ramSpark.Data = ramQ.ToArray();

                // ── Disk ──────────────────────────────────────────────
                float dPct = 0;
                string dStr = "";
                foreach (ManagementObject o in wDisk.Get())
                {
                    double sz = Convert.ToDouble(o["Size"]) / (1024.0 * 1024 * 1024);
                    double fr = Convert.ToDouble(o["FreeSpace"]) / (1024.0 * 1024 * 1024);
                    double used = sz - fr;
                    dPct = (float)(used / sz * 100);
                    dStr = string.Format("{0:0.0} / {1:0.0} GB", used, sz);
                    break;
                }
                diskRing.Value = dPct;
                diskBar.Value = dPct;
                diskBig.Text = dStr;
                diskSub.Text = string.Format("C:  {0:0.0}%  used", dPct);

                // ── Processes ─────────────────────────────────────────
                var rows = Process.GetProcesses()
                    .Select(p =>
                    {
                        long mem = 0; int thr = 0;
                        try { mem = p.WorkingSet64 / (1024 * 1024); thr = p.Threads.Count; }
                        catch { }
                        return new { p.ProcessName, p.Id, Mem = mem, Thr = thr, p.Responding };
                    })
                    .OrderByDescending(r => r.Mem)
                    .Take(60).ToList();

                long maxMem = rows.Count > 0 ? rows.Max(r => r.Mem) : 1;

                procView.BeginUpdate();
                procView.Items.Clear();
                foreach (var r in rows)
                {
                    var item = new ListViewItem(r.ProcessName);
                    item.SubItems.Add(r.Id.ToString());
                    item.SubItems.Add("–");                          // CPU % col
                    item.SubItems.Add(r.Mem.ToString() + " MB");     // RAM col
                    item.SubItems.Add(r.Thr.ToString());
                    item.SubItems.Add(r.Responding ? "Running" : "Not Responding");
                    float rPctBar = maxMem > 0 ? (float)r.Mem / maxMem * 100f : 0f;
                    item.Tag = new float[] { 0f, rPctBar };
                    item.ForeColor = r.Responding ? C_TXT : C_RED;
                    procView.Items.Add(item);
                }
                procView.EndUpdate();

                lblProcCount.Text = string.Format("· {0} processes", rows.Count);
                lblClock.Text = string.Format("{0:HH:mm:ss}", DateTime.Now);
                topBar.Invalidate();
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — column header
        // ════════════════════════════════════════════════════════════
        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Color.FromArgb(28, 34, 42)))
                e.Graphics.FillRectangle(bg, e.Bounds);

            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var ft = new Font("Segoe UI Semibold", 8f))
            using (var br = new SolidBrush(C_TXTSUB))
            {
                var rc = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
                e.Graphics.DrawString(e.Header.Text, ft, br, rc, sf);
            }
            using (var p = new Pen(C_BORDER, 1))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ════════════════════════════════════════════════════════════
        //  OWNER DRAW — row cells
        // ════════════════════════════════════════════════════════════
        void DrawRow(object sender, DrawListViewSubItemEventArgs e)
        {
            // Row background
            Color bg = e.Item.Selected
                ? Color.FromArgb(33, 58, 88)
                : (e.ItemIndex % 2 == 0 ? C_SURF : C_SURF2);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            // Columns 2 (CPU%) and 3 (RAM) — draw progress bar
            if ((e.ColumnIndex == 2 || e.ColumnIndex == 3) && e.Item.Tag is float[] pcts)
            {
                float pct = e.ColumnIndex == 2 ? pcts[0] : pcts[1];
                Color barCol = e.ColumnIndex == 2 ? C_BLUE : C_GREEN;

                // track
                var track = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + e.Bounds.Height - 8, e.Bounds.Width - 8, 5);
                using (var br = new SolidBrush(Color.FromArgb(40, 50, 62)))
                    e.Graphics.FillRectangle(br, track);

                // fill
                int fw = (int)(track.Width * (pct / 100f));
                if (fw > 1)
                {
                    Color fc = pct > 80 ? C_RED : pct > 55 ? C_AMBER : barCol;
                    using (var br = new LinearGradientBrush(
                        new Rectangle(track.X, track.Y, Math.Max(fw, 1), track.Height),
                        Color.FromArgb(160, fc.R, fc.G, fc.B), fc,
                        LinearGradientMode.Horizontal))
                        e.Graphics.FillRectangle(br, new Rectangle(track.X, track.Y, fw, track.Height));
                }

                // label
                string txt = e.ColumnIndex == 2
                    ? string.Format("{0:0.0}%", pct)
                    : e.SubItem.Text;

                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                using (var br = new SolidBrush(C_TXT))
                {
                    var rc = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height - 7);
                    e.Graphics.DrawString(txt, procView.Font, br, rc, sf);
                }
                return;
            }

            // Status column
            Color fg = C_TXTSUB;
            if (e.ColumnIndex == 0) fg = C_TXT;
            if (e.ColumnIndex == 5) fg = e.SubItem.Text == "Running" ? C_GREEN : C_RED;

            using (var sf = new StringFormat
            {
                Alignment = e.ColumnIndex == 0 ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            using (var br = new SolidBrush(fg))
            {
                var rc = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                e.Graphics.DrawString(e.SubItem.Text, procView.Font, br, rc, sf);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CLEANUP
        // ════════════════════════════════════════════════════════════
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (ticker != null) ticker.Stop();
            if (wCpu != null) wCpu.Dispose();
            if (wOs != null) wOs.Dispose();
            if (wDisk != null) wDisk.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  RING GAUGE — arc-style circular meter
    // ════════════════════════════════════════════════════════════════
    public class RingGauge : Control
    {
        float _v; Color _c;
        public float Value { get { return _v; } set { _v = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public RingGauge(Color c)
        {
            _c = c;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int m = 7;
            var rc = new Rectangle(m, m, Width - m * 2, Height - m * 2);

            // track
            using (var p = new Pen(Color.FromArgb(38, 46, 56), 9))
                g.DrawArc(p, rc, 135, 270);

            // fill — color shifts green → amber → red
            float sw = (_v / 100f) * 270f;
            Color fc = _v > 80 ? Color.FromArgb(248, 81, 73)
                     : _v > 55 ? Color.FromArgb(255, 163, 72) : _c;

            using (var p = new Pen(fc, 9) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                if (sw > 0) g.DrawArc(p, rc, 135, sw);

            // center value
            string txt = string.Format("{0:0}%", _v);
            using (var ft = new Font("Segoe UI Semibold", 11f))
            using (var br = new SolidBrush(Color.FromArgb(230, 237, 243)))
            {
                var sz = g.MeasureString(txt, ft);
                g.DrawString(txt, ft, br, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  BAR METER — slim gradient progress bar
    // ════════════════════════════════════════════════════════════════
    public class BarMeter : Control
    {
        float _v; Color _c;
        public float Value { get { return _v; } set { _v = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public BarMeter(Color c)
        {
            _c = c;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new SolidBrush(Color.FromArgb(38, 46, 56)))
                g.FillRectangle(br, 0, 0, Width, Height);

            int fw = (int)(Width * (_v / 100f));
            if (fw > 1)
            {
                Color fc = _v > 80 ? Color.FromArgb(248, 81, 73)
                         : _v > 55 ? Color.FromArgb(255, 163, 72) : _c;
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, fw, Height),
                    Color.FromArgb(150, fc.R, fc.G, fc.B), fc,
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, new Rectangle(0, 0, fw, Height));
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  SPARK CHART — 60-second history line
    // ════════════════════════════════════════════════════════════════
    public class SparkChart : Control
    {
        float[] _d = new float[0]; Color _c;
        public float[] Data { get { return _d; } set { _d = value; Invalidate(); } }
        public SparkChart(Color c)
        {
            _c = c;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_d.Length < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float step = (float)Width / (_d.Length - 1);
            var pts = _d.Select((v, i) =>
                new PointF(i * step, Height - (v / 100f) * Height)).ToArray();

            // filled area
            var fill = new List<PointF> { new PointF(0, Height) };
            fill.AddRange(pts);
            fill.Add(new PointF(Width, Height));

            using (var path = new GraphicsPath())
            {
                path.AddLines(fill.ToArray());
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(55, _c.R, _c.G, _c.B),
                    Color.FromArgb(0, _c.R, _c.G, _c.B),
                    LinearGradientMode.Vertical))
                    g.FillPath(br, path);
            }

            // line
            using (var p = new Pen(_c, 1.5f))
                g.DrawLines(p, pts);
        }
    }
}








































