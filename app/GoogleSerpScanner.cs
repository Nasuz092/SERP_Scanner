using System;
using System.Diagnostics;
using System.Drawing;
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
        private Button btnOpenBrowser;
        private Button btnOpenFolder;
        private Button btnRestartServer;
        private TextBox txtLogs;
        private Panel statusPanel;
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
            this.Size = new Size(680, 520);
            this.MinimumSize = new Size(580, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 28, 36);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

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
            catch
            {
                // Fallback safe ignore
            }

            // Header Panel
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(17, 21, 28),
                Padding = new Padding(18, 12, 18, 10)
            };

            Label lblTitle = new Label
            {
                Text = "Google SERP Multi-Device Scanner",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(16, 12)
            };

            Label lblSubtitle = new Label
            {
                Text = "Scan Halaman 1 Google (Desktop, Mobile, Tablet) • Organik, Ads & Maps",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(18, 36)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);

            // Status Card
            statusPanel = new Panel
            {
                Location = new Point(18, 78),
                Size = new Size(628, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(30, 41, 59)
            };

            lblStatus = new Label
            {
                Text = "Menyiapkan Server...",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(251, 191, 36),
                AutoSize = true,
                Location = new Point(16, 10)
            };

            lblUrl = new Label
            {
                Text = "Alamat Web: http://" + Host + ":" + Port,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(17, 32)
            };

            statusPanel.Controls.Add(lblStatus);
            statusPanel.Controls.Add(lblUrl);

            // Button Toolbar
            btnOpenBrowser = new Button
            {
                Text = "Buka Dashboard di Browser",
                Location = new Point(18, 144),
                Size = new Size(240, 40),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };
            btnOpenBrowser.FlatAppearance.BorderSize = 0;
            btnOpenBrowser.Click += (s, e) => OpenBrowser();

            btnOpenFolder = new Button
            {
                Text = "Buka Folder Data",
                Location = new Point(266, 144),
                Size = new Size(160, 40),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };
            btnOpenFolder.FlatAppearance.BorderSize = 0;
            btnOpenFolder.Click += (s, e) => OpenFolder();

            btnRestartServer = new Button
            {
                Text = "Restart Server",
                Location = new Point(434, 144),
                Size = new Size(140, 40),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };
            btnRestartServer.FlatAppearance.BorderSize = 0;
            btnRestartServer.Click += (s, e) => RestartServer();

            // Logs Label
            Label lblLogTitle = new Label
            {
                Text = "Terminal Aktivitas Server:",
                Location = new Point(18, 196),
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point)
            };

            // Log Textbox
            txtLogs = new TextBox
            {
                Location = new Point(18, 218),
                Size = new Size(628, 245),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Add all controls
            this.Controls.Add(headerPanel);
            this.Controls.Add(statusPanel);
            this.Controls.Add(btnOpenBrowser);
            this.Controls.Add(btnOpenFolder);
            this.Controls.Add(btnRestartServer);
            this.Controls.Add(lblLogTitle);
            this.Controls.Add(txtLogs);

            this.FormClosing += MainForm_FormClosing;
        }

        private void AppendLog(string message)
        {
            if (txtLogs.IsDisposed) return;
            if (txtLogs.InvokeRequired)
            {
                txtLogs.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLogs.AppendText(string.Format("[{0}] {1}\r\n", timestamp, message));
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
                lblStatus.ForeColor = Color.FromArgb(52, 211, 153);
                statusPanel.BackColor = Color.FromArgb(20, 50, 40);
                lblUrl.Text = "Dashboard siap diakses di: http://" + Host + ":" + Port;
            }
            else
            {
                lblStatus.Text = msg;
                lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
                statusPanel.BackColor = Color.FromArgb(60, 25, 25);
                lblUrl.Text = "Server tidak aktif.";
            }
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
                    // Kill process tree
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/F /T /PID " + nodeProcess.Id,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process p = Process.Start(psi);
                    if (p != null) p.WaitForExit(2000);
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
                Process.Start("explorer.exe", appDir);
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
}
