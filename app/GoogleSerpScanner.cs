using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GoogleSerpScannerApp
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private Process nodeProcess;
        private Label lblStatus;
        private Label lblUrl;
        private ModernButton btnOpenBrowser;
        private ModernButton btnOpenFolder;
        private ModernButton btnRestartServer;
        private RichTextBox rtxLogs;
        private StatusCardPanel statusPanel;
        private TerminalCardPanel terminalCard;
        private HeaderBannerPanel headerPanel;

        private const string Host = "127.0.0.1";
        private const int Port = 4321;
        private string appDir;

        [STAThread]
        public static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(
                    "Terjadi kesalahan UI:\n\n" + e.Exception.Message,
                    "Google SERP Scanner Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show(
                    "Terjadi kesalahan sistem:\n\n" + e.ExceptionObject.ToString(),
                    "Google SERP Scanner Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            Application.Run(new MainForm());
        }

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            AppIcons.Initialize(appDir);
            InitializeComponent();
            StartNodeServer();
        }

        private void InitializeComponent()
        {
            this.Text = "Google SERP Scanner - Windows Application";
            this.ClientSize = new Size(760, 530);
            this.MinimumSize = new Size(720, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.ForeColor = Color.FromArgb(15, 23, 42);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.DoubleBuffered = true;

            // Set Application Window Icon (Google Search Magnifier)
            try
            {
                string[] candidateIcoPaths = new string[]
                {
                    Path.Combine(appDir, "app.ico"),
                    Path.Combine(appDir, "app", "app.ico"),
                    Path.Combine(appDir, "core", "app.ico"),
                    Path.Combine(appDir, "app", "public", "icons", "logo.png"),
                    Path.Combine(appDir, "public", "icons", "logo.png")
                };
                bool iconLoaded = false;
                foreach (string p in candidateIcoPaths)
                {
                    if (File.Exists(p) && p.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            this.Icon = new Icon(p);
                            iconLoaded = true;
                            break;
                        }
                        catch { }
                    }
                }
                if (!iconLoaded)
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch { }

            // 1. Header Panel with Decorative Light Blue Curved Wave
            headerPanel = new HeaderBannerPanel
            {
                Location = new Point(0, 0),
                Size = new Size(760, 74),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            // 2. Status Card (Light Green Mint Background)
            statusPanel = new StatusCardPanel
            {
                Location = new Point(18, 80),
                Size = new Size(724, 62),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderColor = Color.FromArgb(167, 243, 208),
                FillColor = Color.FromArgb(236, 253, 245),
                CornerRadius = 14,
                IsRunning = true
            };

            lblStatus = new Label
            {
                Text = "Server Aktif (Online)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(6, 95, 70),
                AutoSize = true,
                Location = new Point(60, 10),
                BackColor = Color.FromArgb(236, 253, 245)
            };

            lblUrl = new Label
            {
                Text = "Dashboard siap diakses di: http://" + Host + ":" + Port + " \u2197",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(37, 99, 235),
                AutoSize = true,
                Location = new Point(61, 33),
                BackColor = Color.FromArgb(236, 253, 245),
                Cursor = Cursors.Hand
            };
            lblUrl.Click += (s, e) => OpenBrowser();

            statusPanel.Controls.Add(lblStatus);
            statusPanel.Controls.Add(lblUrl);

            // 3. Action Buttons Row
            btnOpenBrowser = new ModernButton
            {
                Text = "Buka Dashboard di Browser",
                ButtonType = ModernButton.ButtonVariant.Primary,
                Location = new Point(18, 150),
                Size = new Size(295, 46),
                CornerRadius = 12,
                Cursor = Cursors.Hand
            };
            btnOpenBrowser.Click += (s, e) => OpenBrowser();

            btnOpenFolder = new ModernButton
            {
                Text = "Buka Folder Data",
                ButtonType = ModernButton.ButtonVariant.SecondaryFolder,
                Location = new Point(323, 150),
                Size = new Size(205, 46),
                CornerRadius = 12,
                Cursor = Cursors.Hand
            };
            btnOpenFolder.Click += (s, e) => OpenFolder();

            btnRestartServer = new ModernButton
            {
                Text = "Restart Server",
                ButtonType = ModernButton.ButtonVariant.SecondaryRestart,
                Location = new Point(538, 150),
                Size = new Size(204, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CornerRadius = 12,
                Cursor = Cursors.Hand
            };
            btnRestartServer.Click += (s, e) => RestartServer();

            // 4. Terminal Section Card (White Card with Dark Console Inside)
            terminalCard = new TerminalCardPanel
            {
                Location = new Point(18, 204),
                Size = new Size(724, 306),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderColor = Color.FromArgb(226, 232, 240),
                FillColor = Color.White,
                CornerRadius = 14,
                IsRunning = true
            };

            Label lblTerminalTitle = new Label
            {
                Text = "Terminal Aktivitas Server",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(48, 14),
                BackColor = Color.White
            };

            // Dark inner console area
            Panel darkConsoleContainer = new DarkConsoleContainer
            {
                Location = new Point(12, 44),
                Size = new Size(700, 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Color.FromArgb(11, 17, 32),
                BorderColor = Color.FromArgb(30, 41, 59),
                CornerRadius = 10,
                Padding = new Padding(8)
            };

            rtxLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(11, 17, 32),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            darkConsoleContainer.Controls.Add(rtxLogs);

            terminalCard.Controls.Add(lblTerminalTitle);
            terminalCard.Controls.Add(darkConsoleContainer);

            // Add all controls to form
            this.Controls.Add(headerPanel);
            this.Controls.Add(statusPanel);
            this.Controls.Add(btnOpenBrowser);
            this.Controls.Add(btnOpenFolder);
            this.Controls.Add(btnRestartServer);
            this.Controls.Add(terminalCard);

            this.Shown += (s, e) =>
            {
                try
                {
                    this.BringToFront();
                    this.Activate();
                }
                catch { }
            };

            this.FormClosing += MainForm_FormClosing;
        }

        private void AppendLog(string message)
        {
            if (rtxLogs == null || rtxLogs.IsDisposed) return;
            if (rtxLogs.InvokeRequired)
            {
                rtxLogs.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            // Format timestamp in cyan
            rtxLogs.SelectionStart = rtxLogs.TextLength;
            rtxLogs.SelectionLength = 0;
            rtxLogs.SelectionColor = Color.FromArgb(56, 189, 248);
            rtxLogs.AppendText(string.Format("[{0}] ", timestamp));

            // Colorize specific messages
            if (message.Contains("siap berjalan di Windows"))
            {
                rtxLogs.SelectionColor = Color.FromArgb(74, 222, 128); // Bright Green
            }
            else if (message.StartsWith("==="))
            {
                rtxLogs.SelectionColor = Color.FromArgb(71, 85, 105); // Slate mute
            }
            else if (message.Contains("http://"))
            {
                rtxLogs.SelectionColor = Color.FromArgb(96, 165, 250); // Light blue
            }
            else if (message.Contains("ERROR") || message.Contains("Gagal") || message.Contains("Error"))
            {
                rtxLogs.SelectionColor = Color.FromArgb(248, 113, 113); // Rose
            }
            else
            {
                rtxLogs.SelectionColor = Color.FromArgb(226, 232, 240); // White slate
            }

            rtxLogs.AppendText(message + "\r\n");
            rtxLogs.SelectionStart = rtxLogs.TextLength;
            rtxLogs.ScrollToCaret();
        }

        private void SetStatus(bool running, string msg)
        {
            if (statusPanel.IsDisposed) return;
            if (statusPanel.InvokeRequired)
            {
                statusPanel.BeginInvoke(new Action<bool, string>(SetStatus), running, msg);
                return;
            }

            if (running)
            {
                lblStatus.Text = "Server Aktif (Online)";
                lblStatus.ForeColor = Color.FromArgb(6, 95, 70);
                lblStatus.BackColor = Color.FromArgb(236, 253, 245);
                statusPanel.FillColor = Color.FromArgb(236, 253, 245);
                statusPanel.BorderColor = Color.FromArgb(167, 243, 208);
                statusPanel.IsRunning = true;

                lblUrl.Text = "Dashboard siap diakses di: http://" + Host + ":" + Port + " \u2197";
                lblUrl.ForeColor = Color.FromArgb(37, 99, 235);
                lblUrl.BackColor = Color.FromArgb(236, 253, 245);

                terminalCard.IsRunning = true;
            }
            else
            {
                lblStatus.Text = msg;
                lblStatus.ForeColor = Color.FromArgb(159, 18, 57);
                lblStatus.BackColor = Color.FromArgb(255, 241, 242);
                statusPanel.FillColor = Color.FromArgb(255, 241, 242);
                statusPanel.BorderColor = Color.FromArgb(254, 205, 211);
                statusPanel.IsRunning = false;

                lblUrl.Text = "Server tidak aktif.";
                lblUrl.ForeColor = Color.FromArgb(100, 116, 139);
                lblUrl.BackColor = Color.FromArgb(255, 241, 242);

                terminalCard.IsRunning = false;
            }

            statusPanel.Invalidate();
            terminalCard.Invalidate();
        }

        private void StartNodeServer()
        {
            StopNodeServer();
            AppendLog("Memeriksa Node.js dan menyiapkan server...");

            try
            {
                string workingDir = appDir;
                string[] subdirs = new string[] { "app", "core", "system" };
                foreach (string sub in subdirs)
                {
                    string candidate = Path.Combine(appDir, sub, "server.js");
                    if (File.Exists(candidate))
                    {
                        workingDir = Path.Combine(appDir, sub);
                        break;
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "server.js",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                nodeProcess = new Process();
                nodeProcess.StartInfo = psi;
                nodeProcess.EnableRaisingEvents = true;

                nodeProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendLog(e.Data);
                        if (e.Data.Contains("siap berjalan") || e.Data.Contains(Port.ToString()))
                        {
                            SetStatus(true, "Server Aktif");
                        }
                    }
                };

                nodeProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendLog("ERROR: " + e.Data);
                    }
                };

                nodeProcess.Exited += (s, e) =>
                {
                    AppendLog("Server Node.js telah berhenti.");
                    SetStatus(false, "Server Berhenti");
                };

                bool started = nodeProcess.Start();
                if (started)
                {
                    nodeProcess.BeginOutputReadLine();
                    nodeProcess.BeginErrorReadLine();
                    AppendLog("Node.js server berhasil dimulai (PID: " + nodeProcess.Id + ")");
                    SetStatus(true, "Server Aktif");

                    // Auto open browser after 1.5 seconds
                    new Thread(() =>
                    {
                        Thread.Sleep(1500);
                        OpenBrowser();
                    }).Start();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Gagal memulai Node.js: " + ex.Message);
                SetStatus(false, "Gagal Menjalankan Node");
                MessageBox.Show(
                    "Gagal memulai Node.js server!\nPastikan Node.js terinstal di komputer Anda.\n\nError: " + ex.Message,
                    "Kesalahan Startup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void StopNodeServer()
        {
            if (nodeProcess != null && !nodeProcess.HasExited)
            {
                try
                {
                    nodeProcess.Kill();
                    nodeProcess.WaitForExit(1000);
                }
                catch { }

                try
                {
                    if (!nodeProcess.HasExited) nodeProcess.Kill();
                }
                catch { }

                nodeProcess.Dispose();
                nodeProcess = null;
            }
        }

        private void RestartServer()
        {
            AppendLog("Memulai ulang server...");
            StartNodeServer();
        }

        private void OpenBrowser()
        {
            try
            {
                Process.Start("http://" + Host + ":" + Port);
            }
            catch (Exception ex)
            {
                AppendLog("Gagal membuka browser: " + ex.Message);
            }
        }

        private void OpenFolder()
        {
            try
            {
                string setupDir = Path.Combine(appDir, "setup - DO NOT DELETE");
                if (Directory.Exists(setupDir))
                {
                    Process.Start("explorer.exe", setupDir);
                }
                else
                {
                    Process.Start("explorer.exe", appDir);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Gagal membuka folder: " + ex.Message);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopNodeServer();
        }
    }

    // ==========================================
    // Custom UI Components for Pixel-Perfect UI
    // ==========================================

    public class HeaderBannerPanel : Panel
    {
        public HeaderBannerPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;

            // Clear background
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(248, 250, 252)))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            // Draw decorative soft light-blue wavy curves using smooth cubic beziers
            using (GraphicsPath wavePath = new GraphicsPath())
            {
                wavePath.AddBezier(w - 240, 0, w - 170, 15, w - 120, h - 15, w, h);
                wavePath.AddLine(w, h, w, 0);
                wavePath.CloseFigure();

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(239, 246, 255)))
                {
                    g.FillPath(brush, wavePath);
                }
            }

            using (GraphicsPath wavePath2 = new GraphicsPath())
            {
                wavePath2.AddBezier(w - 150, 0, w - 90, 20, w - 40, h - 10, w, h);
                wavePath2.AddLine(w, h, w, 0);
                wavePath2.CloseFigure();

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(219, 234, 254)))
                {
                    g.FillPath(brush, wavePath2);
                }
            }

            int startX = 18;
            int startY = 15;

            // Draw Title Text
            using (Font titleFont = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            {
                g.DrawString("Google SERP Multi-Device Scanner", titleFont, textBrush, startX, startY);
            }

            // Draw Subtitle Text
            using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                g.DrawString("Scan Halaman 1 Google (Desktop, Mobile, Tablet) \u2022 Organik, Ads, Maps", subFont, subBrush, startX, startY + 25);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class StatusCardPanel : Panel
    {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public int CornerRadius { get; set; }
        public bool IsRunning { get; set; }

        public StatusCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BorderColor = Color.FromArgb(167, 243, 208);
            this.FillColor = Color.FromArgb(236, 253, 245);
            this.CornerRadius = 14;
            this.IsRunning = true;
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Draw rounded background & border
            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush fillBrush = new SolidBrush(FillColor))
                {
                    g.FillPath(fillBrush, path);
                }
                using (Pen borderPen = new Pen(BorderColor, 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw status circle directly inside OnPaint
            int iconX = 16;
            int iconY = (this.Height - 34) / 2;
            int iconSize = 34;

            Color circleColor = IsRunning ? Color.FromArgb(16, 185, 129) : Color.FromArgb(244, 63, 94);
            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, iconX, iconY, iconSize, iconSize);
            }

            using (Pen pen = new Pen(Color.White, 2.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (IsRunning)
                {
                    // Checkmark
                    Point[] pts = new Point[]
                    {
                        new Point(iconX + 9, iconY + 17),
                        new Point(iconX + 15, iconY + 23),
                        new Point(iconX + 25, iconY + 12)
                    };
                    g.DrawLines(pen, pts);
                }
                else
                {
                    // X
                    g.DrawLine(pen, iconX + 11, iconY + 11, iconX + 23, iconY + 23);
                    g.DrawLine(pen, iconX + 23, iconY + 11, iconX + 11, iconY + 23);
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class TerminalCardPanel : Panel
    {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public int CornerRadius { get; set; }
        public bool IsRunning { get; set; }

        public TerminalCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BorderColor = Color.FromArgb(226, 232, 240);
            this.FillColor = Color.White;
            this.CornerRadius = 14;
            this.IsRunning = true;
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush fillBrush = new SolidBrush(FillColor))
                {
                    g.FillPath(fillBrush, path);
                }
                using (Pen borderPen = new Pen(BorderColor, 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw Terminal prompt badge background
            int badgeX = 16;
            int badgeY = 12;
            int badgeSize = 25;
            using (GraphicsPath badgePath = GetRoundedRect(new Rectangle(badgeX, badgeY, badgeSize, badgeSize), 6))
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                {
                    g.FillPath(brush, badgePath);
                }
            }

            // Draw user-provided terminal icon (terminal_white.png) inside badge
            try
            {
                if (AppIcons.TerminalWhite != null)
                {
                    g.DrawImage(AppIcons.TerminalWhite, new Rectangle(badgeX + 4, badgeY + 4, 17, 17));
                }
                else
                {
                    using (Font f = new Font("Consolas", 9f, FontStyle.Bold))
                    using (SolidBrush tb = new SolidBrush(Color.White))
                    {
                        g.DrawString(">_", f, tb, badgeX + 3, badgeY + 4);
                    }
                }
            }
            catch { }

            // Draw Running / Stopped pill badge directly on top right
            int pillW = 90;
            int pillH = 26;
            int pillX = this.Width - pillW - 14;
            int pillY = 11;

            Color fill = IsRunning ? Color.FromArgb(236, 253, 245) : Color.FromArgb(255, 241, 242);
            Color border = IsRunning ? Color.FromArgb(167, 243, 208) : Color.FromArgb(254, 205, 211);
            Color textCol = IsRunning ? Color.FromArgb(4, 120, 87) : Color.FromArgb(225, 29, 72);
            Color dotCol = IsRunning ? Color.FromArgb(16, 185, 129) : Color.FromArgb(244, 63, 94);

            using (GraphicsPath pillPath = GetRoundedRect(new Rectangle(pillX, pillY, pillW, pillH), 13))
            {
                using (SolidBrush pb = new SolidBrush(fill))
                {
                    g.FillPath(pb, pillPath);
                }
                using (Pen pp = new Pen(border, 1.2f))
                {
                    g.DrawPath(pp, pillPath);
                }
            }

            // Dot
            using (SolidBrush dotBrush = new SolidBrush(dotCol))
            {
                g.FillEllipse(dotBrush, pillX + 11, pillY + (pillH - 8) / 2, 8, 8);
            }

            // Text
            using (Font pf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush ptb = new SolidBrush(textCol))
            {
                string text = IsRunning ? "Running" : "Stopped";
                g.DrawString(text, pf, ptb, pillX + 25, pillY + (pillH - 16) / 2);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class DarkConsoleContainer : Panel
    {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public int CornerRadius { get; set; }

        public DarkConsoleContainer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BorderColor = Color.FromArgb(30, 41, 59);
            this.FillColor = Color.FromArgb(11, 17, 32);
            this.CornerRadius = 10;
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush fillBrush = new SolidBrush(FillColor))
                {
                    g.FillPath(fillBrush, path);
                }
                using (Pen borderPen = new Pen(BorderColor, 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernButton : Button
    {
        public enum ButtonVariant
        {
            Primary,
            SecondaryFolder,
            SecondaryRestart
        }

        public ButtonVariant ButtonType { get; set; }
        public int CornerRadius { get; set; }
        private bool isHovered = false;
        private bool isPressed = false;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.ButtonType = ButtonVariant.Primary;
            this.CornerRadius = 12;
            this.DoubleBuffered = true;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            isPressed = false;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            isPressed = true;
            this.Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            isPressed = false;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Clear button background to match form
            using (SolidBrush parentBg = new SolidBrush(Color.FromArgb(248, 250, 252)))
            {
                g.FillRectangle(parentBg, 0, 0, this.Width, this.Height);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            Color bg;
            Color border;
            Color textColor;

            if (ButtonType == ButtonVariant.Primary)
            {
                if (isPressed) bg = Color.FromArgb(29, 78, 216);
                else if (isHovered) bg = Color.FromArgb(37, 99, 235);
                else bg = Color.FromArgb(29, 104, 242);

                border = bg;
                textColor = Color.White;
            }
            else
            {
                if (isPressed) bg = Color.FromArgb(241, 245, 249);
                else if (isHovered) bg = Color.FromArgb(248, 250, 252);
                else bg = Color.White;

                border = isHovered ? Color.FromArgb(203, 213, 225) : Color.FromArgb(226, 232, 240);
                textColor = Color.FromArgb(30, 41, 59);
            }

            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }

                if (ButtonType != ButtonVariant.Primary)
                {
                    using (Pen pen = new Pen(border, 1.3f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // Draw user-provided icons & text
            int centerY = this.Height / 2;

            if (ButtonType == ButtonVariant.Primary)
            {
                // Primary: [computer_white.png] Buka Dashboard di Browser [Arrow ->]
                int iconSize = 20;
                int iconX = 22;
                int iconY = centerY - iconSize / 2;

                try
                {
                    if (AppIcons.ComputerWhite != null)
                    {
                        g.DrawImage(AppIcons.ComputerWhite, new Rectangle(iconX, iconY, iconSize, iconSize));
                    }
                }
                catch { }

                using (Font font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + iconSize + 10, centerY - 8);
                }

                // Arrow ->
                using (Pen pen = new Pen(Color.White, 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    int arrowX = this.Width - 32;
                    g.DrawLine(pen, arrowX - 8, centerY, arrowX, centerY);
                    g.DrawLine(pen, arrowX - 4, centerY - 4, arrowX, centerY);
                    g.DrawLine(pen, arrowX - 4, centerY + 4, arrowX, centerY);
                }
            }
            else if (ButtonType == ButtonVariant.SecondaryFolder)
            {
                // Secondary Folder: [folder_slate.png] Buka Folder Data
                int iconSize = 19;
                int iconX = (this.Width - 146) / 2;
                if (iconX < 18) iconX = 18;
                int iconY = centerY - iconSize / 2;

                try
                {
                    if (AppIcons.FolderSlate != null)
                    {
                        g.DrawImage(AppIcons.FolderSlate, new Rectangle(iconX, iconY, iconSize, iconSize));
                    }
                }
                catch { }

                using (Font font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + iconSize + 9, centerY - 7);
                }
            }
            else if (ButtonType == ButtonVariant.SecondaryRestart)
            {
                // Secondary Restart: [restart_slate.png] Restart Server
                int iconSize = 19;
                int iconX = (this.Width - 130) / 2;
                if (iconX < 18) iconX = 18;
                int iconY = centerY - iconSize / 2;

                try
                {
                    if (AppIcons.RestartSlate != null)
                    {
                        g.DrawImage(AppIcons.RestartSlate, new Rectangle(iconX, iconY, iconSize, iconSize));
                    }
                }
                catch { }

                using (Font font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + iconSize + 9, centerY - 7);
                }
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    public static class AppIcons
    {
        public static Bitmap ComputerWhite { get; private set; }
        public static Bitmap FolderSlate { get; private set; }
        public static Bitmap RestartSlate { get; private set; }
        public static Bitmap TerminalWhite { get; private set; }

        private const string B64_COMPUTER = "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AGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaG0rAXBhnWKyn68zQAdJvjrJ85P8ZJJfSPK2JL/pdaCv8ZmOz3Z8xuOz/qp6HKCDsVfVzWuyC+sM0yU5v04x2a/UGWBXktwxyRlJLq8LldV8IMmLk9yhHh/YlbFX1YU62Xl1humSvKVOMdlv1BlgF5L83SRX1QXKzlyZ5AfqcYJdSHJxXaCT/dc6w3TL13Frek+dAdaU5Pgkr6kLkzZ+ahyjetxgTUl+uy7Myc6tM0yX5PV1ismuqDPAWpLcLskFdVHSzq8luW09frCWJB+si3Ky19UZpkvyijrFZNeOk3CdA2ZLctQOgpdb7qfrMYQ1JDlh2avW9LI6x3RJ/kmdYgUPqXPAbEmeXRci7f1gPY4wW5KH1oW4ghfUOaZL8ow6xQqeVOeAmZLcNcnH60Kkvf+d5C71eMJMSZ5cF+IKTqtzTJfksXWKFbywzgEzJXl5XYTsjR+vxxNmSvKiughX8Jg6x3RJHlynWMF/rnPALElus/xPkv00jt1x9bjCLOOSvLoIV3D/Osd0y6+ir6uTTHb1OCnXWWCGUdZ1AbJ3Hl2PK8yQ5Nhlj1rT+MHhbi593dFd0L6+zgEz7OBKFw7ey+txhRmSPLIuvhVcVudYTZI312lW8Pw6B8ywg5tdcfDeVI8rzLCjK+N2t76T/EidZgWX1DlghuUhNOy3t9fjCjMkeWddfCv4Z3WO1SR5Sp1mJV9RZ4GDNr5eqwuPvbO7r0jZjCSn1IW3kt1dGp/kpDrNSn60zgIHbXkELftt/WelszlJXloX3kruVWdZ1bhHf51oBePfPKbOAgcpyTl14bF33liPKxyksRcl+cO68Fbw+3WW1SX52TrVSp5SZ4GDlOQlddGxd36sHlc4SEm+qy66lby+zrK65dnouzB+cHFUnQcOSpJH1UXH3nlEPa5wUJYHhb2rLrqVPL3Os7ok965TrejxdR44KEmOTvKhuujYG1eOY1iPKxyUJN9ZF91Kxk34vqzOsxNJ3lOnW8nbfQvATDu6tzcHw7NDmGb5D8I76qJbyaV1np3Z8d9Kv6/OAwclyR18C7CXPpjk9vV4wkHZ4Z+/hz5XwiV5eJ1uRR8ej2ytM8FBSfK0uuho77vqcYSDkuRuST5aF92KHlpn2qkk760TrujsOg8cpCQvq4uOtl5ajx8cpCT/ri66Ff1unWfnxlcSdcoVjScifWudCQ7Kcq3vG+rCo53/6B4hzJTksTt4Eu717e72vzcmyf12/KH8cZJ71rngoCyX/Jy+BCe9jHPPmX71z0zjznvLn513Zazzv1TnaiHJeXXalZ2v/plt+R/Ab9XFx8682zeAzJbk2CQX1MW3srfWudrY4R2Rru9f1LngoC1/Enh6kgt9I7ATn0/y60n+juhnDUn+ZV2EO/DUOlcbSY5bLr/ZtefU2WCWJPdYrhR4YZKfWH4r4HXwr/HZjs/4u5PcvR4HmCXJc+smswPjeQPH1dlaSfKP6tQ7MP5O8r11NgD4QizB2eFbvufW2dpJ8heSfLJOvgOfS/IddT4AOBLLrX7HXrJrn0hy5zpfSzt8NnI1/k74/XU+ALgpy53+xh7SQZ87/92ccWe+JFfXd7BDZ9YZAeCGLJf7dvHxJHepM7aW5J/Xd7Fjr3FvcABuzPLsj9fVzWPHzqhztpfki5eb83Qyrt0+uc4KwLYlOSXJ79RNY8fGI63vVGfdC0meUd9NA59KcprHCAOwPNZ37FV/UjeLBvb3N2zLzVLeWd9RE5ck+Zo6MwDbkOQrk1xUN4cm3rH3t7ZO8qgm11DekGuWuzvt1w8sALjFlh+qv7LRr/yrsWc+os69l5K8ur67ZsZ9C87yMCGAw2vcQXL8qG75ZX1nr6yz763xI4YkV9R32ND4G9C43ehD6nsAYD8tX/W/qunf+avLk9yxvoe9luQxO35c8Bfq0iQ/lORL63sBoLfxje5yD/9xLt8X46v/R9f3cigkeXl9t3visiRnJzl13Oq4vi8Admvc5yXJt4wbvyX5zca/PbspL63v69BIcnzjqwKO1PjRyPuS/OISNON2kU8c1ZbkYUkekOS+Xl5eXl4H+hrn1nGOHefacc4dl+6Nc/A4F49zctcf9B2p/9H+aX+3VpL7JPlIfecAsFEfS3K/ul8eSkm+fU+/ngGAgzT2wm+r++ShluRF9VMAgI15Qd0fD71xK94kr62fBABsxL+ve+NmJLlNkrfWTwQADrnzD/2P/m5Okjsv9+UHgC34ny4pXyS5W5L31E8IAA6Zd49nEdR9cNPGHfeS/K/6SQHAIfE77i57I5Lc4xDcKAgAqvEtt83/poxH8ya5uH5yALCnfsPf/I/Qck/nc+snCAB75pwkJ9R9jpuQ5JjxTOT6SQLAnjhr7GV1f+MIJXlmks/WTxUAmvpMktPqfsYtsDwB6gP1EwaAZq5I8vV1H+NWWK4Q+C/1kwaAJt6c5O51/+IALM8POC3Jp+qnDgA78ukkzxp7VN23OGBJviLJRfUIAMDKLkzyoLpPMdH1vg34RD0aADDZ/0lyepKj6/7ESpLcM8nZSa6tRwcADth1Sd6Q5KS6H7EjSb7WnwUAmOjXk3xN3X9oIsm3JHl7PWoAcAtdmuTUut/Q0PL7gCcuP84AgFvigiSP9+v+PZXkEUl+Lsnn6pEFgGLsFeNv/F9X9xP2VJITl19svrcebQA27/eSnJHk3nX/4BBJcvJyoC+rKwCAzRi37R0P7HmUr/k3Zly/Oe7ZnOSfJrnYpYQAh9o4x79t+Q/g17mGnz+X5K5JnpDkJcsPCN1yGGB/jXP4uHTvzOXHfHep5324QeM5zkkekORJSV6c5HXLYhpfG/m2AGD3xrl4nJPHL/Zfm+RFSf5Gkvv7Hz7TLN8YPDDJNyz3HxivEQunenmt+HpBPSOuYPybdQ4vr5mvcW79s/PsOOeO/5zdtZ6XATZjudR1bY+ocwAAKxIAALBBAgAANkgAAMAGCQAA2CABAAAbJAAAYIMEAABskAAAgA0SAACwQQIAADZIAADABgkAANggAQAAGyQAAGCDBAAAbJAAAIANEgAAsEECAAA2SAAAwAYJAADYIAEAABskAABggwQAAGyQAACADRIAALBBAgAANkgAAMAGCQAA2CABAAAbJAAAYIMEAABskAAAgA0SAACwQQIAADZIAADABgkAANggAQAAGyQAAGCDBAAAbJAAAIANEgAAcAslOSXJC5O8NclvJ/l43fHYW+NYjmP6K0lekOQv1+MPwMYkeUKSd9cdg0PvXUkeX9cDAIdcki9PckHdFdicX0tyUl0fABxCy9/Mr6w7AZv14STfVNcJAIfIONEn+UzdAdi8sSa+sa4XAA6BJPdJ8sf1zA+LjyS5X103AOyxJEcnuaSe8aG4OMlRdf0AsKeS/M16pocb8dS6fgDYQ0mOTfL+epaHG3FZkmPqOgJgzyT55nqGh5vxV+o6AmDPJHlFPbvDzXhZXUcA7Bl3+uMWuLSuIwD2jPv6cwt8rK4jAPZIkhPqmR2O0Al1PQGwJ5LcrZ7V4Qjdta4nAPbEuKlLks/WMzvcjHFrYDcEAthnSS6vZ3e4Ge+v6wiAPZPk3Hp2h5txTl1HAOyZJE+vZ3e4GX+7riMA9kySeyS5pp7h4UZ8bvx4tK4jAPZQkp+sZ3m4Ea+q6weAPbV8C3B1PdND8ckkJ9b1A8AeS/KMeraH4rS6bgA4BJK8up7xYfETdb0AcEgkuU2S19czP5v300mOresFgEMmybOSXFt3ATbnuiRnuOsfwIYkeXiSX607Apvx1iQPq+sCgI1I8ugk/zbJVXWH4NAZx/jfjGNe1wEAG5Xk6CQnJ3lsku9NcrrXoXiNYzmO6Ti2R9fjDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALAx/xcbO3GAmClbVgAAAABJRU5ErkJggg==";
        private const string B64_FOLDER = "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";
        private const string B64_RESTART = "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";
        private const string B64_TERMINAL = "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";

        public static void Initialize(string appDir)
        {
            ComputerWhite = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "computer_white.png"), B64_COMPUTER);
            FolderSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "folder_slate.png"), B64_FOLDER);
            RestartSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "restart_slate.png"), B64_RESTART);
            TerminalWhite = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "terminal_white.png"), B64_TERMINAL);
        }

        private static Bitmap LoadOrDecode(string filePath, string b64)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    using (MemoryStream ms = new MemoryStream(bytes))
                    {
                        using (Bitmap temp = new Bitmap(ms))
                        {
                            return new Bitmap(temp);
                        }
                    }
                }
            }
            catch { }

            try
            {
                byte[] bytes = Convert.FromBase64String(b64);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (Bitmap temp = new Bitmap(ms))
                    {
                        return new Bitmap(temp);
                    }
                }
            }
            catch { return null; }
        }
    }
}