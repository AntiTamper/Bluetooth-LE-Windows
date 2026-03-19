using System;
using System.Drawing;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

namespace BLEWindows.UI
{
    public class SettingsDialog : Form
    {
        public bool OverrideTxPower { get; private set; }
        public bool RandomizeInterval { get; private set; }
        public bool StartMinimized { get; private set; }
        public bool AutoStartSpoofing { get; private set; }
        public bool StartWithWindows { get; private set; }
        public int TxPowerValue { get; private set; }

        private SiticoneTrackBar tbTxPower;
        private Label lblTxPower;

        public SettingsDialog(bool overrideTx, int txPower, bool randomize, bool startMin, bool autoStart, bool startWin)
        {
            OverrideTxPower = overrideTx;
            TxPowerValue = txPower;
            RandomizeInterval = randomize;
            StartMinimized = startMin;
            AutoStartSpoofing = autoStart;
            StartWithWindows = startWin;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Settings";
            this.Size = new Size(420, 400);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ColorTranslator.FromHtml("#0F0E12");
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            // Title bar
            Panel titleBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorTranslator.FromHtml("#191922") };
            bool drag = false; Point dragC = Point.Empty, dragF = Point.Empty;
            titleBar.MouseDown += (s, e) => { drag = true; dragC = Cursor.Position; dragF = Location; };
            titleBar.MouseMove += (s, e) => { if (drag) Location = Point.Add(dragF, new Size(Point.Subtract(Cursor.Position, new Size(dragC)))); };
            titleBar.MouseUp += (s, e) => drag = false;

            var lblTitle = new Label { Text = "Settings", Font = new Font("Segoe UI Semibold", 12), ForeColor = Color.White, AutoSize = true, Dock = DockStyle.Left, Padding = new Padding(14, 10, 0, 0) };
            var btnClose = new Label { Text = "✕", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#655D64"), AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(0, 8, 14, 0), Cursor = Cursors.Hand };
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = ColorTranslator.FromHtml("#655D64");
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);

            // Content
            FlowLayoutPanel flow = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoScroll = true, Padding = new Padding(20, 16, 20, 16)
            };
            int w = 360;

            // Override Tx Power
            var swTx = AddSwitch(flow, "Override Tx Power", w, OverrideTxPower);
            swTx.CheckedChanged += (s, e) => {
                OverrideTxPower = swTx.Checked;
                tbTxPower.Enabled = swTx.Checked;
                tbTxPower.ThumbColor = swTx.Checked ? ColorTranslator.FromHtml("#705549") : ColorTranslator.FromHtml("#29232E");
                lblTxPower.Text = swTx.Checked ? $"Tx Power: {tbTxPower.Value} dBm" : "Tx Power: OS Default";
            };

            // Tx Power slider (shown below override toggle)
            lblTxPower = new Label {
                Text = OverrideTxPower ? $"Tx Power: {TxPowerValue} dBm" : "Tx Power: OS Default",
                Font = new Font("Segoe UI", 9), ForeColor = ColorTranslator.FromHtml("#A69B97"),
                Size = new Size(w, 18), Margin = new Padding(0, 8, 0, 4)
            };
            flow.Controls.Add(lblTxPower);
            tbTxPower = new SiticoneTrackBar {
                Maximum = 20, Minimum = -40, Value = TxPowerValue,
                ThumbColor = OverrideTxPower ? ColorTranslator.FromHtml("#705549") : ColorTranslator.FromHtml("#29232E"),
                FillColor = ColorTranslator.FromHtml("#29232E"),
                Size = new Size(w, 25), Enabled = OverrideTxPower, Margin = new Padding(0, 0, 0, 12)
            };
            tbTxPower.Scroll += (s, e) => { TxPowerValue = tbTxPower.Value; lblTxPower.Text = $"Tx Power: {tbTxPower.Value} dBm"; };
            flow.Controls.Add(tbTxPower);

            flow.Controls.Add(new Panel { Size = new Size(w, 1), BackColor = ColorTranslator.FromHtml("#29232E"), Margin = new Padding(0, 4, 0, 12) });

            // Other toggles
            var swRandom = AddSwitch(flow, "Randomize Interval", w, RandomizeInterval);
            swRandom.CheckedChanged += (s, e) => RandomizeInterval = swRandom.Checked;

            var swMin = AddSwitch(flow, "Start Minimized", w, StartMinimized);
            swMin.CheckedChanged += (s, e) => StartMinimized = swMin.Checked;

            var swAuto = AddSwitch(flow, "Auto-Start Spoofing", w, AutoStartSpoofing);
            swAuto.CheckedChanged += (s, e) => AutoStartSpoofing = swAuto.Checked;

            var swWin = AddSwitch(flow, "Start with Windows", w, StartWithWindows);
            swWin.CheckedChanged += (s, e) => StartWithWindows = swWin.Checked;

            this.Controls.Add(flow);
        }

        private static SiticoneToggleSwitch AddSwitch(FlowLayoutPanel parent, string text, int w, bool initial)
        {
            var row = new TableLayoutPanel { Size = new Size(w, 30), Margin = new Padding(0, 0, 0, 8), ColumnCount = 2, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            var sw = new SiticoneToggleSwitch { Size = new Size(42, 22), Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 0), Checked = initial };
            sw.CheckedState.FillColor = ColorTranslator.FromHtml("#705549");
            sw.UncheckedState.FillColor = ColorTranslator.FromHtml("#29232E");
            var lbl = new Label { Text = text, Font = new Font("Segoe UI", 10), ForeColor = ColorTranslator.FromHtml("#A69B97"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 0, 0) };
            row.Controls.Add(sw, 0, 0); row.Controls.Add(lbl, 1, 0);
            parent.Controls.Add(row);
            return sw;
        }
    }
}
