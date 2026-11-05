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

            try
            {
                RunApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Terjadi kesalahan saat memulai aplikasi:\n\n" + ex.ToString(),
                    "Google SERP Scanner - Kesalahan Startup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication()
        {
            // Set native WebView2Loader.dll location after AssemblyResolve is registered
            if (Directory.Exists(libDir))
            {
                try
                {
                    CoreWebView2Environment.SetLoaderDllFolderPath(libDir);
                }
                catch { }
            }

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
        private TerminalForm terminalForm;

        private const string Host = "127.0.0.1";
        private const int Port = 4321;
        private string appDir;
        private bool isServerRunning = false;
        private bool isWebViewReady = false;

        public MainForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            InitializeComponent();
            terminalForm = new TerminalForm();
            StartNodeServer();
            InitializeWebViewAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Google SERP Scanner";
            this.ClientSize = new Size(1320, 860);
            this.MinimumSize = new Size(400, 320);
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

            // 1. Loading Splash Panel
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
            this.Controls.Add(loadingPanel);

            // 2. Embedded WebView2 Control
            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            this.Controls.Add(webView);
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

                webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        string msg = e.TryGetWebMessageAsString();
                        if (string.IsNullOrEmpty(msg)) return;

                        if (msg == "open-folder")
                        {
                            this.BeginInvoke(new Action(OpenFolder));
                        }
                        else if (msg == "restart-server")
                        {
                            this.BeginInvoke(new Action(RestartServer));
                        }
                        else if (msg == "toggle-terminal")
                        {
                            this.BeginInvoke(new Action(ToggleTerminalWindow));
                        }
                        else if (msg == "open-browser")
                        {
                            this.BeginInvoke(new Action(OpenExternalBrowser));
                        }
                    }
                    catch { }
                };

                webView.NavigationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        loadingPanel.Visible = false;
                        webView.Visible = true;
                        webView.BringToFront();
                    }
                    else
                    {
                        // Retry after short delay if server was still binding
                        ThreadPool.QueueUserWorkItem((state) =>
                        {
                            Thread.Sleep(800);
                            NavigateToDashboard();
                        });
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
            // Server status displayed in web dashboard
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
