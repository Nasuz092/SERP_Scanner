using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GoogleSerpScannerApp
{
    public class MainForm : Form
    {
        private Process nodeProcess;
        private Label lblStatus;
        private Label lblUrl;
        private ModernButton btnOpenBrowser;
        private ModernButton btnOpenFolder;
        private ModernButton btnRestartServer;
        private RichTextBox rtxLogs;
        private RoundedPanel statusPanel;
        private RoundedPanel terminalCard;
        private StatusPill runningPill;
        private StatusIcon statusIcon;
        private Panel headerPanel;

        private const string Host = "127.0.0.1";
        private const int Port = 4321;
        private string appDir;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
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
            statusPanel = new RoundedPanel
            {
                Location = new Point(18, 80),
                Size = new Size(724, 62),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderColor = Color.FromArgb(167, 243, 208),
                FillColor = Color.FromArgb(236, 253, 245),
                CornerRadius = 14
            };

            statusIcon = new StatusIcon
            {
                Location = new Point(16, 14),
                Size = new Size(34, 34),
                IsSuccess = true
            };

            lblStatus = new Label
            {
                Text = "Server Aktif (Online)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(6, 95, 70),
                AutoSize = true,
                Location = new Point(60, 10),
                BackColor = Color.Transparent
            };

            lblUrl = new Label
            {
                Text = "Dashboard siap diakses di: http://" + Host + ":" + Port + " \u2197",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(51, 65, 85),
                AutoSize = true,
                Location = new Point(61, 33),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            lblUrl.Click += (s, e) => OpenBrowser();

            statusPanel.Controls.Add(statusIcon);
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
            terminalCard = new RoundedPanel
            {
                Location = new Point(18, 204),
                Size = new Size(724, 306),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderColor = Color.FromArgb(226, 232, 240),
                FillColor = Color.White,
                CornerRadius = 14
            };

            // Terminal Header Prompt Badge ">_"
            TerminalBadge terminalBadge = new TerminalBadge
            {
                Location = new Point(16, 12),
                Size = new Size(24, 24)
            };

            Label lblTerminalTitle = new Label
            {
                Text = "Terminal Aktivitas Server",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(48, 14),
                BackColor = Color.Transparent
            };

            runningPill = new StatusPill
            {
                Location = new Point(622, 12),
                Size = new Size(88, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                IsRunning = true
            };

            // Dark inner console area
            Panel darkConsoleContainer = new RoundedPanel
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

            terminalCard.Controls.Add(terminalBadge);
            terminalCard.Controls.Add(lblTerminalTitle);
            terminalCard.Controls.Add(runningPill);
            terminalCard.Controls.Add(darkConsoleContainer);

            // Add all controls to form
            this.Controls.Add(headerPanel);
            this.Controls.Add(statusPanel);
            this.Controls.Add(btnOpenBrowser);
            this.Controls.Add(btnOpenFolder);
            this.Controls.Add(btnRestartServer);
            this.Controls.Add(terminalCard);

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
                statusPanel.FillColor = Color.FromArgb(236, 253, 245);
                statusPanel.BorderColor = Color.FromArgb(167, 243, 208);
                lblUrl.Text = "Dashboard siap diakses di: http://" + Host + ":" + Port + " \u2197";
                lblUrl.ForeColor = Color.FromArgb(37, 99, 235);
                statusIcon.IsSuccess = true;
                runningPill.IsRunning = true;
            }
            else
            {
                lblStatus.Text = msg;
                lblStatus.ForeColor = Color.FromArgb(159, 18, 57);
                statusPanel.FillColor = Color.FromArgb(255, 241, 242);
                statusPanel.BorderColor = Color.FromArgb(254, 205, 211);
                lblUrl.Text = "Server tidak aktif.";
                lblUrl.ForeColor = Color.FromArgb(100, 116, 139);
                statusIcon.IsSuccess = false;
                runningPill.IsRunning = false;
            }

            statusPanel.Invalidate();
            statusIcon.Invalidate();
            runningPill.Invalidate();
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
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            // Draw decorative soft light-blue wavy curves at the top-right
            using (GraphicsPath wavePath = new GraphicsPath())
            {
                Point[] pts1 = new Point[]
                {
                    new Point(w - 240, 0),
                    new Point(w - 180, 20),
                    new Point(w - 130, h - 10),
                    new Point(w, h)
                };
                wavePath.AddCurve(pts1);
                wavePath.AddLine(w, h, w, 0);
                wavePath.CloseFigure();

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(239, 246, 255))) // Light blue
                {
                    g.FillPath(brush, wavePath);
                }
            }

            using (GraphicsPath wavePath2 = new GraphicsPath())
            {
                Point[] pts2 = new Point[]
                {
                    new Point(w - 150, 0),
                    new Point(w - 95, 25),
                    new Point(w - 40, h - 5),
                    new Point(w, h)
                };
                wavePath2.AddCurve(pts2);
                wavePath2.AddLine(w, h, w, 0);
                wavePath2.CloseFigure();

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(219, 234, 254))) // Soft sky blue
                {
                    g.FillPath(brush, wavePath2);
                }
            }

            // Draw Blue Squircle Icon Badge with White Search Magnifier
            int iconX = 18;
            int iconY = 14;
            int iconSize = 46;
            using (GraphicsPath squircle = GetRoundedRect(new Rectangle(iconX, iconY, iconSize, iconSize), 12))
            {
                using (SolidBrush blueBrush = new SolidBrush(Color.FromArgb(29, 104, 242))) // Google Blue
                {
                    g.FillPath(blueBrush, squircle);
                }
            }

            // Draw Magnifying Glass inside squircle
            using (Pen pen = new Pen(Color.White, 2.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                // Circle
                g.DrawEllipse(pen, iconX + 13, iconY + 13, 14, 14);
                // Handle
                g.DrawLine(pen, iconX + 24, iconY + 24, iconX + 31, iconY + 31);
            }

            // Draw Title Text
            using (Font titleFont = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            {
                g.DrawString("Google SERP Multi-Device Scanner", titleFont, textBrush, iconX + 56, iconY + 2);
            }

            // Draw Subtitle Text
            using (Font subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                g.DrawString("Scan Halaman 1 Google (Desktop, Mobile, Tablet) \u2022 Organik, Ads, Maps", subFont, subBrush, iconX + 57, iconY + 26);
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

    public class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public int CornerRadius { get; set; }

        public RoundedPanel()
        {
            this.BorderColor = Color.FromArgb(226, 232, 240);
            this.FillColor = Color.White;
            this.CornerRadius = 12;
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

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

    public class StatusIcon : Control
    {
        public bool IsSuccess { get; set; }

        public StatusIcon()
        {
            this.IsSuccess = true;
            this.DoubleBuffered = true;
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int size = Math.Min(this.Width, this.Height) - 2;
            Color circleColor = IsSuccess ? Color.FromArgb(16, 185, 129) : Color.FromArgb(244, 63, 94);

            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 1, 1, size, size);
            }

            using (Pen pen = new Pen(Color.White, 2.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (IsSuccess)
                {
                    // Checkmark
                    Point[] pts = new Point[]
                    {
                        new Point(size / 4 + 2, size / 2 + 1),
                        new Point(size / 2 - 1, size * 3 / 4 - 2),
                        new Point(size * 3 / 4 + 2, size / 3 + 1)
                    };
                    g.DrawLines(pen, pts);
                }
                else
                {
                    // X mark
                    g.DrawLine(pen, size / 3, size / 3, size * 2 / 3, size * 2 / 3);
                    g.DrawLine(pen, size * 2 / 3, size / 3, size / 3, size * 2 / 3);
                }
            }
        }
    }

    public class StatusPill : Control
    {
        public bool IsRunning { get; set; }

        public StatusPill()
        {
            this.IsRunning = true;
            this.DoubleBuffered = true;
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            Color fill = IsRunning ? Color.FromArgb(236, 253, 245) : Color.FromArgb(255, 241, 242);
            Color border = IsRunning ? Color.FromArgb(167, 243, 208) : Color.FromArgb(254, 205, 211);
            Color textCol = IsRunning ? Color.FromArgb(4, 120, 87) : Color.FromArgb(225, 29, 72);
            Color dotCol = IsRunning ? Color.FromArgb(16, 185, 129) : Color.FromArgb(244, 63, 94);

            using (GraphicsPath path = GetRoundedRect(rect, 12))
            {
                using (SolidBrush fillBrush = new SolidBrush(fill))
                {
                    g.FillPath(fillBrush, path);
                }
                using (Pen borderPen = new Pen(border, 1.2f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Green/Red Dot
            using (SolidBrush dotBrush = new SolidBrush(dotCol))
            {
                g.FillEllipse(dotBrush, 12, (this.Height - 8) / 2, 8, 8);
            }

            // Status Text
            using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(textCol))
            {
                string text = IsRunning ? "Running" : "Stopped";
                g.DrawString(text, f, tb, 26, (this.Height - 16) / 2);
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

    public class TerminalBadge : Control
    {
        public TerminalBadge()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = GetRoundedRect(rect, 6))
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                {
                    g.FillPath(brush, path);
                }
            }

            using (Font f = new Font("Consolas", 9f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                g.DrawString(">_", f, tb, 3, 3);
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
            this.ButtonType = ButtonVariant.Primary;
            this.CornerRadius = 12;
            this.DoubleBuffered = true;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.Transparent;
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
            g.SmoothingMode = SmoothingMode.AntiAlias;

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

            // Draw icon & text
            int centerY = this.Height / 2;

            if (ButtonType == ButtonVariant.Primary)
            {
                // Primary: [Monitor Icon] Buka Dashboard di Browser [Arrow ->]
                int iconX = 24;
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    // Monitor screen
                    g.DrawRectangle(pen, iconX, centerY - 9, 18, 12);
                    // Stand base
                    g.DrawLine(pen, iconX + 5, centerY + 7, iconX + 13, centerY + 7);
                    g.DrawLine(pen, iconX + 9, centerY + 3, iconX + 9, centerY + 7);
                }

                using (Font font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + 26, centerY - 8);
                }

                // Arrow ->
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    int arrowX = this.Width - 36;
                    g.DrawLine(pen, arrowX - 8, centerY, arrowX, centerY);
                    g.DrawLine(pen, arrowX - 4, centerY - 4, arrowX, centerY);
                    g.DrawLine(pen, arrowX - 4, centerY + 4, arrowX, centerY);
                }
            }
            else if (ButtonType == ButtonVariant.SecondaryFolder)
            {
                // Folder icon
                int iconX = (this.Width - 140) / 2;
                if (iconX < 18) iconX = 18;

                using (Pen pen = new Pen(Color.FromArgb(30, 41, 59), 1.8f))
                {
                    // Folder shape
                    g.DrawLine(pen, iconX, centerY - 6, iconX + 6, centerY - 6);
                    g.DrawLine(pen, iconX + 6, centerY - 8, iconX + 12, centerY - 8);
                    g.DrawLine(pen, iconX + 12, centerY - 6, iconX + 17, centerY - 6);
                    g.DrawRectangle(pen, iconX, centerY - 6, 17, 12);
                }

                using (Font font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + 24, centerY - 7);
                }
            }
            else if (ButtonType == ButtonVariant.SecondaryRestart)
            {
                // Refresh icon (circular arrow)
                int iconX = (this.Width - 120) / 2;
                if (iconX < 18) iconX = 18;

                using (Pen pen = new Pen(Color.FromArgb(30, 41, 59), 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, iconX, centerY - 8, 14, 14, 45, 270);
                    // Arrowhead
                    g.DrawLine(pen, iconX + 11, centerY - 9, iconX + 14, centerY - 5);
                    g.DrawLine(pen, iconX + 11, centerY - 1, iconX + 14, centerY - 5);
                }

                using (Font font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(textColor))
                {
                    g.DrawString(this.Text, font, tb, iconX + 22, centerY - 7);
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
}
