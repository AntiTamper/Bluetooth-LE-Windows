using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using BLEWindows.Models;
using BLEWindows.Core;
using Siticone.Desktop.UI.WinForms;
using System.Threading.Tasks;
using System.IO;

namespace BLEWindows.UI
{
    public class MainForm : Form
    {
        // Controls
        private TrackBar tbInterval, tbNetTxPower;
        private Label lblInterval, lblNetTxPower, lblStatus;
        private SiticoneButton btnStartStop;
        private NotifyIcon trayIcon;
        private FlowLayoutPanel deviceListPanel;
        private List<SiticoneCheckBox> allCbs = new();

        // Group data
        private Dictionary<string, List<SiticoneCheckBox>> groupCbs = new();
        private Dictionary<string, SiticoneCheckBox> groupSelectAll = new();
        private string activeGroup = "Apple";

        // State
        private BleSpoofer _spoofer;
        private AppConfig _config;
        private bool isSpoofing;

        // Settings state (synced with dialog)
        private bool _overrideTx, _randomizeInterval, _startMinimized, _autoStartSpoofing, _startWithWindows;
        private int _txPower;

        public MainForm()
        {
            _config = AppConfig.Load();
            _spoofer = new BleSpoofer();
            _spoofer.OnSpoofingChanged += (s, msg) => {
                if (InvokeRequired) Invoke(() => UpdateStatus(msg)); else UpdateStatus(msg);
            };
            _spoofer.OnError += (s, ex) => {
                if (InvokeRequired) Invoke(() => ShowError(ex)); else ShowError(ex);
            };
            BuildUI();
            ApplyConfig();
            FormClosing += (s, e) => { _spoofer.StopSpoofing(); SaveConfig(); trayIcon.Visible = false; };

            // Anti-debug
            Task.Run(async () => {
                while (true) {
                    if (System.Diagnostics.Debugger.IsAttached) Environment.Exit(0);
                    foreach (var p in System.Diagnostics.Process.GetProcesses()) {
                        try { string n = p.ProcessName.ToLower();
                            if (n.Contains("dnspy") || n.Contains("x64dbg") || n.Contains("ida64") ||
                                n.Contains("wireshark") || n.Contains("fiddler") || n.Contains("processhacker"))
                                Environment.Exit(0);
                        } catch { }
                    }
                    await Task.Delay(3000);
                }
            });
        }

        private void UpdateStatus(string msg)
        {
            lblStatus.Text = "Status: " + (msg.Length > 40 ? msg[..40] + "…" : msg);
            lblStatus.ForeColor = msg == "Idle" ? ColorTranslator.FromHtml("#655D64")
                : msg.Contains("Error") ? Color.FromArgb(230, 80, 80)
                : Color.FromArgb(80, 200, 120);
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show($"Exception: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblStatus.Text = "Status: Error"; lblStatus.ForeColor = Color.FromArgb(230, 80, 80);
        }

        // ═══════════════════════════════════════════════════════════════
        // UI — Wireframe layout: Groups | Devices | Sliders | BottomBar
        // ═══════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Size = new Size(1100, 700);
            this.MinimumSize = new Size(950, 600);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorTranslator.FromHtml("#0F0E12");
            this.ForeColor = Color.White;
            this.DoubleBuffered = true;

            var resizer = new SiticoneBorderlessForm {
                ContainerControl = this, DragForm = false, BorderRadius = 0, HasFormShadow = true
            };

            // Icon
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                var pen = new Pen(Color.White, 3);
                g.DrawLine(pen, 16, 4, 16, 28); g.DrawLine(pen, 16, 4, 24, 10);
                g.DrawLine(pen, 24, 10, 8, 22); g.DrawLine(pen, 8, 10, 24, 22);
                g.DrawLine(pen, 24, 22, 16, 28);
            }
            this.Icon = Icon.FromHandle(bmp.GetHicon());

            string alias = AppDomain.CurrentDomain.FriendlyName.Replace(".exe", "");
            trayIcon = new NotifyIcon { Icon = this.Icon, Visible = true, Text = alias };
            trayIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; };

            var rng = new Random();
            this.Text = new string(Enumerable.Range(0, rng.Next(8, 16))
                .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"[rng.Next(62)]).ToArray());

            this.SuspendLayout();

            // ════════════════════════════════════════════
            //  TITLE BAR (Dock.Top, 38px)
            // ════════════════════════════════════════════
            Panel titleBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = ColorTranslator.FromHtml("#191922") };
            bool drag = false; Point dragC = Point.Empty, dragF = Point.Empty;
            titleBar.MouseDown += (s, e) => { drag = true; dragC = Cursor.Position; dragF = Location; };
            titleBar.MouseMove += (s, e) => { if (drag) Location = Point.Add(dragF, new Size(Point.Subtract(Cursor.Position, new Size(dragC)))); };
            titleBar.MouseUp += (s, e) => drag = false;

            var lbTitle = new Label {
                Text = "BLE Advertiser", Font = new Font("Segoe UI Semibold", 11),
                ForeColor = ColorTranslator.FromHtml("#A69B97"), AutoSize = true,
                Dock = DockStyle.Left, Padding = new Padding(10, 8, 0, 0)
            };
            lbTitle.MouseDown += (s, e) => { drag = true; dragC = Cursor.Position; dragF = Location; };
            lbTitle.MouseMove += (s, e) => { if (drag) Location = Point.Add(dragF, new Size(Point.Subtract(Cursor.Position, new Size(dragC)))); };
            lbTitle.MouseUp += (s, e) => drag = false;

            var cbClose = new SiticoneControlBox { Dock = DockStyle.Right, IconColor = Color.FromArgb(140, 130, 135), FillColor = Color.Transparent };
            cbClose.HoverState.FillColor = Color.FromArgb(200, 50, 50); cbClose.HoverState.IconColor = Color.White;
            var cbMax = new SiticoneControlBox { ControlBoxType = Siticone.Desktop.UI.WinForms.Enums.ControlBoxType.MaximizeBox, Dock = DockStyle.Right, IconColor = Color.FromArgb(140, 130, 135), FillColor = Color.Transparent };
            cbMax.HoverState.FillColor = ColorTranslator.FromHtml("#29232E");
            var cbMin = new SiticoneControlBox { ControlBoxType = Siticone.Desktop.UI.WinForms.Enums.ControlBoxType.MinimizeBox, Dock = DockStyle.Right, IconColor = Color.FromArgb(140, 130, 135), FillColor = Color.Transparent };
            cbMin.HoverState.FillColor = ColorTranslator.FromHtml("#29232E");
            titleBar.Controls.AddRange(new Control[] { lbTitle, cbMin, cbMax, cbClose });

            // ════════════════════════════════════════════
            //  BOTTOM BAR (Dock.Bottom, 68px)
            //  [Settings] [Start/Stop] [Status]
            // ════════════════════════════════════════════
            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = ColorTranslator.FromHtml("#191922") };
            Panel sepBottom = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#29232E") };
            Panel bottomContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };
            bottomBar.Controls.Add(bottomContent);
            bottomBar.Controls.Add(sepBottom);

            var btnSettings = new SiticoneButton {
                Text = "⚙  Settings", Font = new Font("Segoe UI Semibold", 10),
                FillColor = ColorTranslator.FromHtml("#29232E"), ForeColor = ColorTranslator.FromHtml("#A69B97"),
                BorderRadius = 6, Dock = DockStyle.Left, Width = 140, Cursor = Cursors.Hand
            };
            btnSettings.HoverState.FillColor = ColorTranslator.FromHtml("#46383C");
            btnSettings.HoverState.ForeColor = Color.White;
            btnSettings.Click += BtnSettings_Click;

            lblStatus = new Label {
                Text = "Status: Idle", Font = new Font("Segoe UI", 10),
                ForeColor = ColorTranslator.FromHtml("#655D64"), AutoSize = false,
                Dock = DockStyle.Right, Width = 220, TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };

            Panel spacer1 = new Panel { Dock = DockStyle.Left, Width = 16 };
            Panel spacer2 = new Panel { Dock = DockStyle.Right, Width = 16 };

            btnStartStop = new SiticoneButton {
                Text = "● START SPOOFING", Font = new Font("Segoe UI Semibold", 12),
                FillColor = ColorTranslator.FromHtml("#46383C"), ForeColor = Color.White,
                BorderRadius = 8, Dock = DockStyle.Fill, Cursor = Cursors.Hand
            };
            btnStartStop.HoverState.FillColor = ColorTranslator.FromHtml("#705549");
            btnStartStop.Click += BtnStartStop_Click;

            bottomContent.Controls.Add(btnStartStop);
            bottomContent.Controls.Add(spacer2);
            bottomContent.Controls.Add(lblStatus);
            bottomContent.Controls.Add(spacer1);
            bottomContent.Controls.Add(btnSettings);

            // ════════════════════════════════════════════
            //  RIGHT SLIDERS (Dock.Right, 150px)
            // ════════════════════════════════════════════
            Panel sliderPanel = new Panel { Dock = DockStyle.Right, Width = 150, BackColor = ColorTranslator.FromHtml("#191922"), Padding = new Padding(12, 16, 12, 16) };
            Splitter splitRight = new Splitter { Dock = DockStyle.Right, Width = 4, BackColor = ColorTranslator.FromHtml("#29232E"), Cursor = Cursors.SizeWE };

            var sliderGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            sliderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            sliderGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));
            sliderGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblInterval = new Label { Text = "Interval\n1500 ms", Font = new Font("Segoe UI Semibold", 9), ForeColor = ColorTranslator.FromHtml("#655D64"), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            lblNetTxPower = new Label { Text = "TX Power\nHighest", Font = new Font("Segoe UI Semibold", 9), ForeColor = ColorTranslator.FromHtml("#655D64"), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            
            tbInterval = new TrackBar {
                Orientation = Orientation.Vertical, Maximum = 5000, Minimum = 200, Value = 1500, 
                TickStyle = TickStyle.None, Dock = DockStyle.Fill, Margin = new Padding(12, 8, 12, 8)
            };
            tbInterval.Scroll += (s, e) => {
                lblInterval.Text = $"Interval\n{tbInterval.Value} ms";
                _spoofer.SetTimerInterval(tbInterval.Value);
            };

            tbNetTxPower = new TrackBar {
                Orientation = Orientation.Vertical, Maximum = 5, Minimum = 1, Value = 5,
                TickStyle = TickStyle.None, Dock = DockStyle.Fill, Margin = new Padding(12, 8, 12, 8)
            };
            tbNetTxPower.Scroll += (s, e) => {
                string[] n = { "", "Lowest", "Low", "Medium", "High", "Highest" };
                lblNetTxPower.Text = $"TX Power\n{n[tbNetTxPower.Value]}";
            };

            sliderGrid.Controls.Add(lblInterval, 0, 0);
            sliderGrid.Controls.Add(lblNetTxPower, 1, 0);
            sliderGrid.Controls.Add(tbInterval, 0, 1);
            sliderGrid.Controls.Add(tbNetTxPower, 1, 1);
            sliderPanel.Controls.Add(sliderGrid);

            // ════════════════════════════════════════════
            //  LEFT GROUP LIST (Dock.Left, 220px)
            // ════════════════════════════════════════════
            Panel groupPanel = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = ColorTranslator.FromHtml("#191922"), Padding = new Padding(8, 16, 8, 16) };
            Splitter splitLeft = new Splitter { Dock = DockStyle.Left, Width = 4, BackColor = ColorTranslator.FromHtml("#29232E"), Cursor = Cursors.SizeWE };

            FlowLayoutPanel groupFlow = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(0)
            };

            string[] groups = { "Apple", "Windows", "Android / Misc" };
            foreach (string grp in groups)
            {
                groupCbs[grp] = new List<SiticoneCheckBox>();
                var grpPanel = MakeGroupButton(grp, groupPanel.Width - 16);
                groupFlow.Controls.Add(grpPanel);
            }
            groupPanel.Controls.Add(groupFlow);
            groupFlow.Resize += (s, e) => {
                foreach (Control c in groupFlow.Controls)
                    c.Width = groupFlow.ClientSize.Width - groupFlow.Padding.Horizontal;
            };

            // ════════════════════════════════════════════
            //  CENTER DEVICE LIST (Dock.Fill)
            // ════════════════════════════════════════════
            deviceListPanel = new FlowLayoutPanel {
                Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = ColorTranslator.FromHtml("#0F0E12"), Padding = new Padding(24, 16, 24, 16)
            };

            // ════════════════════════════════════════════
            //  ASSEMBLY (reverse dock stacking)
            // ════════════════════════════════════════════
            this.Controls.Add(deviceListPanel);  // Fill (center)
            this.Controls.Add(splitRight);       // Right Splitter
            this.Controls.Add(sliderPanel);      // Right sliders
            this.Controls.Add(splitLeft);        // Left Splitter
            this.Controls.Add(groupPanel);       // Left groups
            this.Controls.Add(bottomBar);        // Bottom
            this.Controls.Add(titleBar);         // Top

            this.ResumeLayout(false);

            // ── Populate devices into groups ──
            foreach (var d in PayloadDatabase.Devices)
            {
                var cb = MakeDeviceCheckBox(d);
                allCbs.Add(cb);
                string grp = GetGroupKey(d);
                if (groupCbs.ContainsKey(grp))
                    groupCbs[grp].Add(cb);
            }

            // Show initial group
            ShowGroup("Apple");

            // Tray minimize
            Resize += (s, e) => {
                if (WindowState == FormWindowState.Minimized) { Hide(); ShowInTaskbar = false; }
                else ShowInTaskbar = true;
            };

            // Auto behaviors
            Load += async (s, e) => {
                ApplyConfig();
                if (_autoStartSpoofing) { await Task.Delay(500); BtnStartStop_Click(null, null); }
                if (_startMinimized) { WindowState = FormWindowState.Minimized; Hide(); ShowInTaskbar = false; }
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // Group selection
        // ═══════════════════════════════════════════════════════════════

        private string GetGroupKey(BleDeviceDef d)
        {
            if (d.Category.Contains("Apple") || d.Category.Contains("Airpods")) return "Apple";
            if (d.Category.Contains("Windows") || d.Category.Contains("Microsoft")) return "Windows";
            return "Android / Misc";
        }

        private Panel MakeGroupButton(string groupName, int width)
        {
            Panel p = new Panel {
                Size = new Size(width, 48), Margin = new Padding(0, 4, 0, 4),
                BackColor = groupName == activeGroup ? ColorTranslator.FromHtml("#29232E") : Color.Transparent,
                Cursor = Cursors.Hand, Tag = groupName,
                Padding = new Padding(12, 0, 0, 0)
            };

            var chk = new SiticoneCheckBox {
                AutoSize = false, Size = new Size(32, 48),
                Dock = DockStyle.Left, Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 0, 0)
            };
            chk.CheckedState.FillColor = ColorTranslator.FromHtml("#705549");
            chk.CheckedState.BorderColor = ColorTranslator.FromHtml("#705549");
            chk.CheckedState.BorderRadius = 3;
            chk.UncheckedState.BorderColor = ColorTranslator.FromHtml("#46383C");
            chk.UncheckedState.FillColor = Color.Transparent;
            chk.UncheckedState.BorderRadius = 3;

            var lbl = new Label {
                Text = groupName, Font = new Font("Segoe UI Semibold", 11),
                ForeColor = Color.White, AutoSize = false,
                Dock = DockStyle.Fill, Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            groupSelectAll[groupName] = chk;

            // Checkbox = select all in this group
            chk.CheckedChanged += (s, e) => {
                if (groupCbs.ContainsKey(groupName))
                    foreach (var cb in groupCbs[groupName]) cb.Checked = chk.Checked;
            };

            // Click panel/label = switch to this group's device list
            p.Click += (s, e) => ShowGroup(groupName);
            lbl.Click += (s, e) => ShowGroup(groupName);

            p.Controls.Add(lbl);
            p.Controls.Add(chk);
            return p;
        }

        private void ShowGroup(string groupName)
        {
            activeGroup = groupName;
            deviceListPanel.SuspendLayout();
            deviceListPanel.Controls.Clear();

            if (groupCbs.ContainsKey(groupName))
                foreach (var cb in groupCbs[groupName])
                    deviceListPanel.Controls.Add(cb);

            deviceListPanel.ResumeLayout(true);

            // Highlight active group button
            foreach (var ctrl in this.Controls)
            {
                if (ctrl is Panel panel)
                {
                    foreach (Control child in panel.Controls)
                    {
                        if (child is FlowLayoutPanel flow)
                        {
                            foreach (Control grpCtrl in flow.Controls)
                            {
                                if (grpCtrl is Panel grpPanel && grpPanel.Tag is string tag)
                                    grpPanel.BackColor = tag == groupName
                                        ? ColorTranslator.FromHtml("#29232E")
                                        : Color.Transparent;
                            }
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UI Helpers
        // ═══════════════════════════════════════════════════════════════

        private static SiticoneCheckBox MakeDeviceCheckBox(BleDeviceDef d)
        {
            var cb = new SiticoneCheckBox {
                Text = "  " + d.Name, Tag = d, Font = new Font("Segoe UI", 11),
                ForeColor = ColorTranslator.FromHtml("#D0C8C5"), AutoSize = true,
                Margin = new Padding(4, 6, 4, 6), Cursor = Cursors.Hand
            };
            cb.CheckedState.FillColor = ColorTranslator.FromHtml("#705549");
            cb.CheckedState.BorderColor = ColorTranslator.FromHtml("#705549");
            cb.CheckedState.BorderRadius = 3;
            cb.UncheckedState.BorderColor = ColorTranslator.FromHtml("#46383C");
            cb.UncheckedState.FillColor = Color.Transparent;
            cb.UncheckedState.BorderRadius = 3;
            return cb;
        }

        // ═══════════════════════════════════════════════════════════════
        // Config / Logic
        // ═══════════════════════════════════════════════════════════════

        private void ApplyConfig()
        {
            tbInterval.Value = Math.Clamp(_config.Interval, tbInterval.Minimum, tbInterval.Maximum);
            lblInterval.Text = $"Interval\n{tbInterval.Value} ms";
            _spoofer.SetTimerInterval(tbInterval.Value);

            tbNetTxPower.Value = Math.Clamp(_config.NetTxPowerIndex + 1, 1, 5);
            string[] n = { "", "Lowest", "Low", "Medium", "High", "Highest" };
            lblNetTxPower.Text = $"TX Power\n{n[tbNetTxPower.Value]}";

            _overrideTx = _config.OverrideTxPower;
            _txPower = _config.TxPower;
            _randomizeInterval = false;
            _startMinimized = _config.StartMinimized;
            _autoStartSpoofing = _config.AutoStartSpoofing;
            _startWithWindows = _config.StartWithWindows;

            ApplySettingsToSpoofer();

            if (_config.SelectedDevices != null)
                foreach (var cb in allCbs)
                    if (cb.Tag is BleDeviceDef def && _config.SelectedDevices.Contains($"{def.Category}: {def.Name}"))
                        cb.Checked = true;
        }

        private void ApplySettingsToSpoofer()
        {
            _spoofer.TargetTxPower = _overrideTx ? (short)_txPower : null;
            _spoofer.RandomizeInterval = _randomizeInterval;
        }

        private void SaveConfig()
        {
            _config.Interval = tbInterval.Value;
            _config.OverrideTxPower = _overrideTx;
            _config.TxPower = _txPower;
            _config.StartMinimized = _startMinimized;
            _config.AutoStartSpoofing = _autoStartSpoofing;
            _config.StartWithWindows = _startWithWindows;
            _config.NetTxPowerIndex = tbNetTxPower.Value - 1;
            _config.SelectedDevices = allCbs
                .Where(cb => cb.Checked && cb.Tag is BleDeviceDef)
                .Select(cb => { var d = (BleDeviceDef)cb.Tag; return $"{d.Category}: {d.Name}"; })
                .ToList();
            _config.Save();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using var dlg = new SettingsDialog(_overrideTx, _txPower, _randomizeInterval, _startMinimized, _autoStartSpoofing, _startWithWindows);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _overrideTx = dlg.OverrideTxPower;
                _txPower = dlg.TxPowerValue;
                _randomizeInterval = dlg.RandomizeInterval;
                _startMinimized = dlg.StartMinimized;
                _autoStartSpoofing = dlg.AutoStartSpoofing;
                bool oldWin = _startWithWindows;
                _startWithWindows = dlg.StartWithWindows;
                ApplySettingsToSpoofer();
                if (oldWin != _startWithWindows) SetStartup(_startWithWindows);
            }
        }

        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (isSpoofing) {
                _spoofer.StopSpoofing();
                btnStartStop.Text = "● START SPOOFING";
                btnStartStop.FillColor = ColorTranslator.FromHtml("#46383C");
                btnStartStop.HoverState.FillColor = ColorTranslator.FromHtml("#705549");
                isSpoofing = false;
                return;
            }

            var selected = allCbs.Where(cb => cb.Checked && cb.Tag is BleDeviceDef).Select(cb => (BleDeviceDef)cb.Tag).ToList();
            if (selected.Count == 0) { MessageBox.Show("Select at least one device.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string pwr = tbNetTxPower.Value switch { 1 => "1. Lowest", 2 => "2. Medium-low", 3 => "3. Medium", 4 => "4. Medium-High", _ => "5. Highest" };

            btnStartStop.Text = "STARTING...";
            btnStartStop.Enabled = false;

            await Task.Run(async () => {
                await NetAdapterManager.EnableBluetoothAsync();
                await NetAdapterManager.SetTransmitPowerAsync(pwr);
            });

            _spoofer.StartRandomSpoofer(selected);
            btnStartStop.Text = "■ STOP SPOOFING";
            btnStartStop.FillColor = Color.FromArgb(180, 50, 50);
            btnStartStop.HoverState.FillColor = Color.FromArgb(210, 70, 70);
            btnStartStop.Enabled = true;
            isSpoofing = true;
        }

        private void SetStartup(bool enable)
        {
            try {
                if (enable) {
                    if (string.IsNullOrEmpty(_config.ServiceName)) {
                        _config.ServiceName = RandStr(12); _config.ServiceDesc = RandStr(24); SaveConfig();
                    }
                    string parent = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')).FullName;
                    string[] exes = Directory.GetFiles(parent, "*.exe");
                    if (exes.Length == 0) return;
                    Cmd($"sc create \"{_config.ServiceName}\" binPath= \"cmd.exe /c start \\\"\\\" \\\"{exes[0]}\\\"\" type= own start= auto DisplayName= \"{_config.ServiceName}\"");
                    Cmd($"sc description \"{_config.ServiceName}\" \"{_config.ServiceDesc}\"");
                } else if (!string.IsNullOrEmpty(_config.ServiceName)) {
                    Cmd($"sc delete \"{_config.ServiceName}\"");
                }
            } catch { }
        }

        private static void Cmd(string c) {
            var p = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + c) { CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, Verb = "runas" };
            System.Diagnostics.Process.Start(p)?.WaitForExit();
        }

        private static string RandStr(int len) {
            const string ch = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var r = new Random(); return new string(Enumerable.Range(0, len).Select(_ => ch[r.Next(ch.Length)]).ToArray());
        }
    }
}
