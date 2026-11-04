using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GoogleSerpScannerApp
{
    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static string appDir;
        private static string libDir;

        [STAThread]
        public static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            appDir = AppDomain.CurrentDomain.BaseDirectory;
            libDir = Path.Combine(appDir, "app", "lib");

            // Resolve WebView2 dependencies from app/lib/ folder
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string asmName = new AssemblyName(args.Name).Name + ".dll";
                    string asmPath = Path.Combine(libDir, asmName);
                    if (File.Exists(asmPath))
                    {
                        return Assembly.LoadFrom(asmPath);
                    }
                }
                catch { }
                return null;
            };

            // Set native WebView2Loader.dll location
            if (Directory.Exists(libDir))
            {
                try
                {
                    CoreWebView2Environment.SetLoaderDllFolderPath(libDir);
                }
                catch { }
            }

            RunApplication();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication()
        {
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
    }

    public class MainForm : Form
    {
        private Process nodeProcess;
        private WebView2 webView;
        private Panel loadingPanel;
        private Label lblLoadingText;
        private ProgressBar progressBar;
        private TopBarPanel topBar;
        private StatusPillLabel lblStatusPill;
        private TerminalForm terminalForm;

        private const string Host = "127.0.0.1";
        private const int Port = 4321;
        private string appDir;
        private bool isServerRunning = false;
        private bool isWebViewReady = false;

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            AppIcons.Initialize(appDir);
            InitializeComponent();
            terminalForm = new TerminalForm();
            StartNodeServer();
            InitializeWebViewAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Google SERP Scanner - Windows Application";
            this.ClientSize = new Size(1320, 860);
            this.MinimumSize = new Size(1024, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.ForeColor = Color.FromArgb(15, 23, 42);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.DoubleBuffered = true;
            this.FormClosing += MainForm_FormClosing;

            // Set Window Icon
            try
            {
                string[] candidateIcoPaths = new string[]
                {
                    Path.Combine(appDir, "app.ico"),
                    Path.Combine(appDir, "app", "app.ico"),
                    Path.Combine(appDir, "app", "public", "icons", "logo.png")
                };
                foreach (string p in candidateIcoPaths)
                {
                    if (File.Exists(p) && p.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Icon = new Icon(p);
                        break;
                    }
                }
                if (this.Icon == null)
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch { }

            // 1. Top Bar Navigation Panel
            topBar = new TopBarPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.White
            };

            // App Title in Top Bar
            Label lblAppTitle = new Label
            {
                Text = "Google SERP Scanner",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(18, 14),
                BackColor = Color.Transparent
            };
            topBar.Controls.Add(lblAppTitle);

            // Server Status Pill
            lblStatusPill = new StatusPillLabel
            {
                Text = "Menyiapkan Server...",
                IsOnline = false,
                Location = new Point(190, 12),
                Size = new Size(160, 28)
            };
            topBar.Controls.Add(lblStatusPill);

            // Top Bar Action Buttons (Right-Aligned)
            int btnY = 10;
            int rightEdge = this.ClientSize.Width - 18;

            // Button: Buka di Browser Luar
            ModernButton btnExternalBrowser = new ModernButton
            {
                Text = "Browser Luar",
                ButtonType = ModernButton.ButtonVariant.SecondaryBrowser,
                Size = new Size(128, 32),
                Location = new Point(rightEdge - 128, btnY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                CornerRadius = 8
            };
            btnExternalBrowser.Click += (s, e) => OpenExternalBrowser();
            topBar.Controls.Add(btnExternalBrowser);

            // Button: Log Terminal
            ModernButton btnTerminal = new ModernButton
            {
                Text = "Log Terminal",
                ButtonType = ModernButton.ButtonVariant.SecondaryTerminal,
                Size = new Size(125, 32),
                Location = new Point(rightEdge - 128 - 133, btnY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                CornerRadius = 8
            };
            btnTerminal.Click += (s, e) => ToggleTerminalWindow();
            topBar.Controls.Add(btnTerminal);

            // Button: Restart Server
            ModernButton btnRestart = new ModernButton
            {
                Text = "Restart Server",
                ButtonType = ModernButton.ButtonVariant.SecondaryRestart,
                Size = new Size(130, 32),
                Location = new Point(rightEdge - 128 - 133 - 138, btnY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                CornerRadius = 8
            };
            btnRestart.Click += (s, e) => RestartServer();
            topBar.Controls.Add(btnRestart);

            // Button: Buka Folder Data
            ModernButton btnFolder = new ModernButton
            {
                Text = "Folder Data",
                ButtonType = ModernButton.ButtonVariant.SecondaryFolder,
                Size = new Size(125, 32),
                Location = new Point(rightEdge - 128 - 133 - 138 - 133, btnY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                CornerRadius = 8
            };
            btnFolder.Click += (s, e) => OpenFolder();
            topBar.Controls.Add(btnFolder);

            this.Controls.Add(topBar);

            // 2. Main Content Container
            Panel contentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            this.Controls.Add(contentContainer);

            // 3. Loading Splash Panel
            loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            lblLoadingText = new Label
            {
                Text = "Memulai server Google SERP Scanner dan memuat dashboard...",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(600, 36),
                Dock = DockStyle.None
            };

            progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Size = new Size(320, 8)
            };

            loadingPanel.Resize += (s, e) =>
            {
                lblLoadingText.Location = new Point((loadingPanel.Width - lblLoadingText.Width) / 2, (loadingPanel.Height / 2) - 40);
                progressBar.Location = new Point((loadingPanel.Width - progressBar.Width) / 2, (loadingPanel.Height / 2) + 5);
            };

            loadingPanel.Controls.Add(lblLoadingText);
            loadingPanel.Controls.Add(progressBar);
            contentContainer.Controls.Add(loadingPanel);

            // 4. Embedded WebView2 Control
            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            contentContainer.Controls.Add(webView);
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(appDir, "app", "data", "webview2");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, null);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;

                webView.NavigationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        loadingPanel.Visible = false;
                        webView.Visible = true;
                        webView.BringToFront();
                    }
                };

                isWebViewReady = true;

                // If server is already ready, navigate
                if (isServerRunning)
                {
                    NavigateToDashboard();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Gagal menginisialisasi WebView2: " + ex.Message);
                lblLoadingText.Text = "WebView2 runtime tidak terdeteksi. Silakan instal Microsoft Edge WebView2.";
                progressBar.Visible = false;
            }
        }

        private void NavigateToDashboard()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(NavigateToDashboard));
                return;
            }

            if (isWebViewReady && webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate("http://" + Host + ":" + Port + "/");
            }
        }

        private void StartNodeServer()
        {
            StopNodeServer();

            string serverScript = Path.Combine(appDir, "app", "server.js");
            if (!File.Exists(serverScript))
            {
                serverScript = Path.Combine(appDir, "server.js");
            }

            if (!File.Exists(serverScript))
            {
                AppendLog("File server.js tidak ditemukan di: " + serverScript);
                SetStatus(false, "server.js Hilang");
                return;
            }

            string workingDir = Path.GetDirectoryName(serverScript);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "node.exe",
                    Arguments = "\"" + serverScript + "\"",
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
                    SetStatus(false, "Server Offline");
                };

                bool started = nodeProcess.Start();
                if (started)
                {
                    ChildProcessTracker.AddProcess(nodeProcess);
                    nodeProcess.BeginOutputReadLine();
                    nodeProcess.BeginErrorReadLine();
                    AppendLog("Node.js server berhasil dimulai (PID: " + nodeProcess.Id + ")");
                    SetStatus(true, "Server Aktif (Online)");

                    // Wait in background until server port is responsive, then navigate
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        bool ready = WaitForServerReady(10000);
                        if (ready)
                        {
                            isServerRunning = true;
                            NavigateToDashboard();
                        }
                    });
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

        private bool WaitForServerReady(int maxTimeoutMs)
        {
            int elapsed = 0;
            int interval = 250;
            while (elapsed < maxTimeoutMs)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + Host + ":" + Port + "/");
                    request.Timeout = 800;
                    request.Method = "GET";
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if ((int)response.StatusCode == 200)
                        {
                            return true;
                        }
                    }
                }
                catch { }

                Thread.Sleep(interval);
                elapsed += interval;
            }
            return true;
        }

        private void StopNodeServer()
        {
            if (nodeProcess != null && !nodeProcess.HasExited)
            {
                try
                {
                    int pid = nodeProcess.Id;
                    ProcessStartInfo killPsi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/F /T /PID " + pid,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (Process killProc = Process.Start(killPsi))
                    {
                        if (killProc != null) killProc.WaitForExit(1000);
                    }
                }
                catch { }

                try
                {
                    if (!nodeProcess.HasExited) nodeProcess.Kill();
                    nodeProcess.WaitForExit(1000);
                }
                catch { }

                nodeProcess.Dispose();
                nodeProcess = null;
            }
            isServerRunning = false;
        }

        private void RestartServer()
        {
            AppendLog("Memulai ulang server...");
            loadingPanel.Visible = true;
            loadingPanel.BringToFront();
            lblLoadingText.Text = "Memulai ulang server dan memuat dashboard...";
            StartNodeServer();
        }

        private void OpenExternalBrowser()
        {
            try
            {
                Process.Start("http://" + Host + ":" + Port);
            }
            catch (Exception ex)
            {
                AppendLog("Gagal membuka browser luar: " + ex.Message);
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

        private void ToggleTerminalWindow()
        {
            if (terminalForm == null || terminalForm.IsDisposed)
            {
                terminalForm = new TerminalForm();
            }

            if (terminalForm.Visible)
            {
                terminalForm.Hide();
            }
            else
            {
                terminalForm.Show(this);
            }
        }

        private void SetStatus(bool isOnline, string text)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetStatus(isOnline, text)));
                return;
            }
            lblStatusPill.Text = text;
            lblStatusPill.IsOnline = isOnline;
            lblStatusPill.Invalidate();
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            string line = "[" + timeStamp + "] " + message;

            if (terminalForm != null && !terminalForm.IsDisposed)
            {
                terminalForm.AppendLogLine(line);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopNodeServer();
            if (webView != null)
            {
                try
                {
                    webView.Dispose();
                }
                catch { }
            }
        }
    }

    public class TopBarPanel : Panel
    {
        public TopBarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(Color.White))
            {
                g.FillRectangle(bg, 0, 0, this.Width, this.Height);
            }
            // Bottom hairline border
            using (Pen pen = new Pen(Color.FromArgb(226, 232, 240), 1f))
            {
                g.DrawLine(pen, 0, this.Height - 1, this.Width, this.Height - 1);
            }
        }
    }

    public class StatusPillLabel : Control
    {
        public bool IsOnline { get; set; }

        public StatusPillLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width - 1;
            int h = this.Height - 1;

            Color bgColor = IsOnline ? Color.FromArgb(236, 253, 245) : Color.FromArgb(254, 242, 242);
            Color borderColor = IsOnline ? Color.FromArgb(167, 243, 208) : Color.FromArgb(254, 202, 202);
            Color textColor = IsOnline ? Color.FromArgb(6, 95, 70) : Color.FromArgb(153, 27, 27);
            Color dotColor = IsOnline ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68);

            Rectangle rect = new Rectangle(0, 0, w, h);
            using (GraphicsPath path = GetRoundedRect(rect, h / 2))
            {
                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Dot
            int dotSize = 7;
            int dotX = 10;
            int dotY = (h - dotSize) / 2;
            using (SolidBrush dotBrush = new SolidBrush(dotColor))
            {
                g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
            }

            // Text
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                g.DrawString(this.Text, this.Font, textBrush, dotX + dotSize + 6, (h - 14) / 2);
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

    public class TerminalForm : Form
    {
        private RichTextBox rtxLogs;

        public TerminalForm()
        {
            this.Text = "Terminal Aktivitas Server - Google SERP Scanner";
            this.Size = new Size(720, 440);
            this.MinimumSize = new Size(540, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.ForeColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

            rtxLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(11, 17, 32),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            this.Controls.Add(rtxLogs);
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        public void AppendLogLine(string line)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendLogLine(line)));
                return;
            }

            rtxLogs.AppendText(line + Environment.NewLine);
            rtxLogs.SelectionStart = rtxLogs.Text.Length;
            rtxLogs.ScrollToCaret();
        }
    }

    public class ModernButton : Button
    {
        public enum ButtonVariant
        {
            Primary,
            SecondaryFolder,
            SecondaryRestart,
            SecondaryTerminal,
            SecondaryBrowser
        }

        public ButtonVariant ButtonType { get; set; }
        public int CornerRadius { get; set; }
        private bool isHovered = false;
        private bool isPressed = false;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.ButtonType = ButtonVariant.SecondaryFolder;
            this.CornerRadius = 8;
            this.DoubleBuffered = true;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
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
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush parentBg = new SolidBrush(Color.White))
            {
                g.FillRectangle(parentBg, 0, 0, this.Width, this.Height);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            Color bg = isPressed ? Color.FromArgb(241, 245, 249) : (isHovered ? Color.FromArgb(248, 250, 252) : Color.White);
            Color border = isHovered ? Color.FromArgb(203, 213, 225) : Color.FromArgb(226, 232, 240);
            Color textColor = Color.FromArgb(30, 41, 59);

            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(border, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            int centerY = this.Height / 2;
            int iconSize = 16;
            int startX = 12;
            Bitmap iconToDraw = null;

            if (ButtonType == ButtonVariant.SecondaryFolder)
            {
                iconToDraw = AppIcons.FolderSlate;
            }
            else if (ButtonType == ButtonVariant.SecondaryRestart)
            {
                iconToDraw = AppIcons.RestartSlate;
            }
            else if (ButtonType == ButtonVariant.SecondaryTerminal)
            {
                iconToDraw = AppIcons.TerminalSlate;
            }
            else if (ButtonType == ButtonVariant.SecondaryBrowser)
            {
                iconToDraw = AppIcons.ComputerSlate;
            }

            if (iconToDraw != null)
            {
                try
                {
                    g.DrawImage(iconToDraw, new Rectangle(startX, centerY - (iconSize / 2), iconSize, iconSize));
                }
                catch { }
            }

            int textX = iconToDraw != null ? startX + iconSize + 7 : startX;
            using (SolidBrush tb = new SolidBrush(textColor))
            {
                g.DrawString(this.Text, this.Font, tb, textX, centerY - 8);
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
        public static Bitmap ComputerSlate { get; private set; }
        public static Bitmap FolderSlate { get; private set; }
        public static Bitmap RestartSlate { get; private set; }
        public static Bitmap TerminalSlate { get; private set; }

        private const string B64_COMPUTER = "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AGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0AzCEAAGhNAMwhAABoTQDMIQAAaE0A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";
        private const string B64_FOLDER = "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";
        private const string B64_RESTART = "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";
        private const string B64_TERMINAL = "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";

        public static void Initialize(string appDir)
        {
            ComputerSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "computer_slate.png"), B64_COMPUTER);
            FolderSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "folder_slate.png"), B64_FOLDER);
            RestartSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "restart_slate.png"), B64_RESTART);
            TerminalSlate = LoadOrDecode(Path.Combine(appDir, "app", "public", "icons", "terminal_slate.png"), B64_TERMINAL);
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

    public static class ChildProcessTracker
    {
        private static IntPtr s_jobHandle;

        static ChildProcessTracker()
        {
            try
            {
                s_jobHandle = CreateJobObject(IntPtr.Zero, null);
                var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                };
                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = info
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                    SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length);
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch { }
        }

        public static void AddProcess(Process process)
        {
            if (s_jobHandle != IntPtr.Zero && process != null)
            {
                try
                {
                    AssignProcessToJobObject(s_jobHandle, process.Handle);
                }
                catch { }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
    }

    public enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }
}
