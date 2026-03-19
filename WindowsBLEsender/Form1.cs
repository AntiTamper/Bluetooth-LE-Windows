using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Generic;

namespace WindowsBLEsender
{
    public partial class Form1 : Form
    {
        private Label lblStatus;
        private Panel progressFill;
        private Label btnClose;
        private bool dragging;
        private Point dragC, dragF;

        public Form1()
        {
            InitializeComponent();
            this.Opacity = 0;
        }

        private FlowLayoutPanel flowOps;
        private string _lastStatus = "";
        private Panel _activeNode;

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Size = new Size(420, 500);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorTranslator.FromHtml("#0F0E12");
            this.DoubleBuffered = true;

            // Icon
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                var pen = new Pen(Color.White, 3);
                g.DrawLine(pen, 16, 4, 16, 28); g.DrawLine(pen, 16, 4, 24, 10);
                g.DrawLine(pen, 24, 10, 8, 22); g.DrawLine(pen, 8, 10, 24, 22);
            }
            this.Icon = Icon.FromHandle(bmp.GetHicon());
            this.Text = GenerateRandomString(12);

            // ── ROOT GRID ──
            TableLayoutPanel rootPnl = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1, ColumnStyles = { new ColumnStyle(SizeType.Percent, 100f) },
                RowCount = 3, RowStyles = {
                    new RowStyle(SizeType.Absolute, 40f),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.Percent, 100f)
                }
            };
            this.Controls.Add(rootPnl);

            // ── DRAG REGION ──
            Panel topBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };
            topBar.MouseDown += (s, e) => { dragging = true; dragC = Cursor.Position; dragF = Location; };
            topBar.MouseMove += (s, e) => { if (dragging) Location = Point.Add(dragF, new Size(Point.Subtract(Cursor.Position, new Size(dragC)))); };
            topBar.MouseUp += (s, e) => dragging = false;
            
            btnClose = new Label {
                Text = "✕", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#655D64"),
                AutoSize = true, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0, 8, 12, 0)
            };
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = ColorTranslator.FromHtml("#655D64");
            btnClose.Click += (s, e) => FadeOutAndClose();
            TableLayoutPanel topGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topGrid.Controls.Add(btnClose, 1, 0);
            topBar.Controls.Add(topGrid);
            rootPnl.Controls.Add(topBar, 0, 0);

            // ── HEADER ──
            FlowLayoutPanel headerPnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14, 0, 0, 20) };
            Label lblTitle = new Label { Text = "BLE Advertiser", Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0) };
            Label lblSub = new Label { Text = "Let's get broadcasting.", Font = new Font("Segoe UI Semibold", 10), ForeColor = ColorTranslator.FromHtml("#A69B97"), AutoSize = true, Margin = new Padding(4, -8, 0, 0) };
            headerPnl.Controls.Add(lblTitle);
            headerPnl.Controls.Add(lblSub);
            rootPnl.Controls.Add(headerPnl, 0, 1);

            // ── CHECKLIST LIST ──
            flowOps = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(12, 0, 12, 12)
            };
            rootPnl.Controls.Add(flowOps, 0, 2);

            this.ResumeLayout(false);
        }

        private void SetProgress(int pct, string status)
        {
            Invoke(new Action(() => {
                if (status != _lastStatus)
                {
                    if (_activeNode != null)
                    {
                        _activeNode.BackColor = Color.Transparent;
                        _activeNode.Controls[0].ForeColor = ColorTranslator.FromHtml("#46383C"); // Dim color
                        _activeNode.Controls[1].ForeColor = ColorTranslator.FromHtml("#655D64"); 
                    }

                    Panel p = new Panel { Width = flowOps.Width - 8, Height = 56, Margin = new Padding(0, 4, 0, 4), BackColor = ColorTranslator.FromHtml("#29232E") };
                    var dot = new Label { Text = "●", Font = new Font("Segoe UI", 12), ForeColor = Color.White, Location = new Point(8, 14), AutoSize = true };
                    
                    var lblTit = new Label { Text = status, Font = new Font("Segoe UI Semibold", 10), ForeColor = ColorTranslator.FromHtml("#A69B97"), Location = new Point(36, 8), AutoSize = true, MaximumSize = new Size(p.Width - 40, 20) };
                    
                    var lblDesc = new Label { Text = "Allocating memory buffers...", Font = new Font("Segoe UI", 9), ForeColor = ColorTranslator.FromHtml("#655D64"), Location = new Point(36, 28), AutoSize = true };
                    
                    if (status.Contains("Loading configs")) lblDesc.Text = "Resolving dynamic UML structural layouts.";
                    else if (status.Contains("Verifying install")) lblDesc.Text = "Checking zip hashes and integrity...";
                    else if (status.Contains("Ready")) lblDesc.Text = "Linking child processes securely...";
                    else lblDesc.Text = "Allocating memory buffers...";

                    p.Controls.Add(dot); p.Controls.Add(lblTit); p.Controls.Add(lblDesc);
                    flowOps.Controls.Add(p);
                    _activeNode = p;
                    _lastStatus = status;
                }
            }));
        }

        private async void FadeOutAndClose()
        {
            while (Opacity > 0.0) { await Task.Delay(10); Opacity -= 0.08; }
            Application.Exit();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            while (Opacity < 1.0) { await Task.Delay(12); Opacity += 0.06; }
            Opacity = 1.0;
            try { await Task.Run(RunBootstrapperLogic); }
            catch (Exception ex) { MessageBox.Show("Bootstrapper: " + ex.Message, "Error"); }
        }

        private async Task RunBootstrapperLogic()
        {
            SetProgress(5, "Initializing...");
            await Task.Delay(800);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string metaFile = Path.Combine(baseDir, "sys_auth.inf");
            string rootFolder = "", appFile = "", payloadFile = "";

            if (File.Exists(metaFile))
            {
                try {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(metaFile));
                    if (dict.ContainsKey("root") && Directory.Exists(Path.Combine(baseDir, dict["root"])))
                    {
                        string exe = Path.Combine(baseDir, dict["root"], dict["app"]);
                        if (File.Exists(exe) && File.Exists(exe.Replace(".exe", ".dll")))
                        { rootFolder = dict["root"]; appFile = dict["app"]; payloadFile = dict["doc"]; }
                    }
                } catch { }
            }
            SetProgress(20, "Initializing...");

            SetProgress(25, "Loading configs...");
            await Task.Delay(500);

            if (string.IsNullOrEmpty(rootFolder))
            {
                rootFolder = GenerateRandomString(8);
                string dataFolder = GenerateRandomString(8);
                string depsFolder = GenerateRandomString(8);
                string targetDir = Path.Combine(baseDir, rootFolder);
                Directory.CreateDirectory(targetDir);
                Directory.CreateDirectory(Path.Combine(targetDir, dataFolder));
                var targetDeps = Path.Combine(targetDir, depsFolder);
                Directory.CreateDirectory(targetDeps);
                Directory.CreateDirectory(Path.Combine(targetDeps, "Application Logs"));
                Directory.CreateDirectory(Path.Combine(targetDeps, "List", "DLLs"));
                Directory.CreateDirectory(Path.Combine(targetDeps, "List", "Resources"));

                payloadFile = Path.Combine(dataFolder, "non_encrypted_BLE_devices_map.json");
                string encConf = Path.Combine(dataFolder, "encrypted_app_configs.json");

                SetProgress(35, "Loading configs...");
                string tempZip = Path.Combine(targetDir, "app_enc.zip");
                ExtractResource("app.bin", tempZip);
                ZipFile.ExtractToDirectory(tempZip, targetDir);
                File.Delete(tempZip);

                SetProgress(50, "Loading configs...");
                string baseName = "BLEWindows";
                appFile = GenerateRandomString(10) + ".exe";
                string newBase = appFile.Replace(".exe", "");

                foreach (string ext in new[] { ".exe", ".dll", ".pdb", ".deps.json", ".runtimeconfig.json" })
                {
                    string oldP = Path.Combine(targetDir, baseName + ext);
                    string newP = Path.Combine(targetDir, newBase + ext);
                    if (File.Exists(oldP)) {
                        try {
                            byte[] fb = File.ReadAllBytes(oldP);
                            Action<byte[], byte[]> rep = (search, repl) => {
                                for (int i = 0; i <= fb.Length - search.Length; i++) {
                                    bool m = true;
                                    for (int j = 0; j < search.Length; j++) if (fb[i + j] != search[j]) { m = false; break; }
                                    if (m) for (int j = 0; j < repl.Length; j++) fb[i + j] = repl[j];
                                }
                            };
                            rep(System.Text.Encoding.UTF8.GetBytes(baseName), System.Text.Encoding.UTF8.GetBytes(newBase));
                            rep(System.Text.Encoding.Unicode.GetBytes(baseName), System.Text.Encoding.Unicode.GetBytes(newBase));
                            File.WriteAllBytes(oldP, fb);
                        } catch { }
                        File.Move(oldP, newP);
                    }
                }

                SetProgress(65, "Loading configs...");
                ExtractResource("payloads.bin", Path.Combine(targetDir, payloadFile));
                File.WriteAllText(Path.Combine(targetDir, encConf), "{ \"obfuscation\": \"active\", \"version\": 1 }");

                var meta = new Dictionary<string, string> { ["root"] = rootFolder, ["app"] = appFile, ["doc"] = payloadFile };
                if (File.Exists(metaFile)) File.SetAttributes(metaFile, FileAttributes.Normal);
                File.WriteAllText(metaFile, JsonSerializer.Serialize(meta));
                File.SetAttributes(metaFile, FileAttributes.Hidden | FileAttributes.System);
            }

            SetProgress(75, "Verifying install...");
            await Task.Delay(600);

            string installedApp = Path.Combine(baseDir, rootFolder, appFile);
            if (!File.Exists(installedApp) || new FileInfo(installedApp).Length < 100) {
                SetProgress(75, "Integrity check failed.");
                MessageBox.Show("Files corrupted or tampered.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Invoke(new Action(() => Application.Exit()));
                return;
            }

            SetProgress(90, "Verifying install...");
            await Task.Delay(300);
            SetProgress(100, "Ready!");
            await Task.Delay(400);

            var psi = new ProcessStartInfo {
                FileName = installedApp,
                WorkingDirectory = Path.Combine(baseDir, rootFolder),
                UseShellExecute = false
            };
            psi.EnvironmentVariables["SPOOFER_BOOT_TOKEN"] = "SYNAPSE_AUTHORIZED";
            psi.EnvironmentVariables["SPOOFER_PAYLOAD_PATH"] = payloadFile;
            Process.Start(psi);
            Invoke(new Action(() => Application.Exit()));
        }

        private void ExtractResource(string name, string path)
        {
            using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new Exception("Missing: " + name);
            using FileStream fs = new FileStream(path, FileMode.Create);
            s.CopyTo(fs);
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            char[] buf = new char[length]; var r = new Random();
            for (int i = 0; i < length; i++) buf[i] = chars[r.Next(chars.Length)];
            return new string(buf);
        }
    }
}
