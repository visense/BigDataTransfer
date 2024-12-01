using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text;

namespace FRPCManager
{
    public partial class MainForm : Form
    {
        private Process frpcProcess;
        private System.Windows.Forms.Timer statusTimer;
        private Button toggleButton;
        private RichTextBox logTextBox;
        private NotifyIcon trayIcon;
        private bool isRunning = false;

        public MainForm()
        {
            InitializeComponents();
            InitializeTimer();
            InitializeTrayIcon();
            
            // 确保窗口显示在最前面
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void InitializeComponents()
        {
            try
            {
                // 设置窗体属性
                this.Text = "大数据中心文件传输助手";
                this.Size = new Size(500, 400);
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.Icon = new Icon(GetIconPath("1.ico"));
                this.MinimizeBox = true;
                this.ShowInTaskbar = true;

                // 创建启动/停止按钮
                toggleButton = new Button();
                toggleButton.Size = new Size(120, 40);
                toggleButton.Location = new Point((this.ClientSize.Width - 120) / 2, 20);
                toggleButton.Text = "启动服务";
                toggleButton.BackColor = Color.FromArgb(76, 175, 80);
                toggleButton.ForeColor = Color.White;
                toggleButton.FlatStyle = FlatStyle.Flat;
                toggleButton.FlatAppearance.BorderSize = 0;
                toggleButton.Font = new Font("Microsoft YaHei", 10F, FontStyle.Regular);
                toggleButton.Click += ToggleButton_Click;
                this.Controls.Add(toggleButton);

                // 创建日志文本框
                logTextBox = new RichTextBox();
                logTextBox.Location = new Point(20, toggleButton.Bottom + 20);
                logTextBox.Size = new Size(this.ClientSize.Width - 40, this.ClientSize.Height - toggleButton.Bottom - 40);
                logTextBox.ReadOnly = true;
                logTextBox.BackColor = Color.White;
                logTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular);
                logTextBox.ScrollBars = RichTextBoxScrollBars.Both;
                this.Controls.Add(logTextBox);

                // 初始化状态
                UpdateStatus(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化界面失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                trayIcon = new NotifyIcon()
                {
                    Icon = new Icon(GetIconPath("2.ico")),
                    Text = "大数据中心文件传输助手",
                    Visible = true,
                    ContextMenuStrip = new ContextMenuStrip()
                };

                // 创建托盘菜单
                var contextMenu = trayIcon.ContextMenuStrip;
                var showItem = new ToolStripMenuItem("显示主窗口");
                showItem.Click += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); };
                
                var toggleItem = new ToolStripMenuItem("启动服务");
                toggleItem.Click += ToggleButton_Click;
                
                var exitItem = new ToolStripMenuItem("退出");
                exitItem.Click += (s, e) => { 
                    StopService();
                    Application.Exit(); 
                };

                contextMenu.Items.AddRange(new ToolStripItem[] { showItem, toggleItem, exitItem });

                // 双击托盘图标显示主窗口
                trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.Activate(); };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化托盘图标失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeTimer()
        {
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1000; // 1秒
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
        }

        private void ToggleButton_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                StartService();
            }
            else
            {
                StopService();
            }
        }

        private void StartService()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(Application.StartupPath, "frpc.exe");
                startInfo.Arguments = "-c frpc.toml";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                frpcProcess = Process.Start(startInfo);
                
                // 异步读取输出
                frpcProcess.OutputDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        AppendLog(e.Data);
                };
                frpcProcess.ErrorDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        AppendLog("错误: " + e.Data);
                };

                frpcProcess.BeginOutputReadLine();
                frpcProcess.BeginErrorReadLine();

                UpdateStatus(true);
                AppendLog("服务已启动");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"启动失败: {ex.Message}");
            }
        }

        private void StopService()
        {
            try
            {
                if (frpcProcess != null)
                {
                    // 获取所有相关进程
                    var processes = Process.GetProcesses();
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (proc.ProcessName.ToLower().Contains("frpc"))
                            {
                                proc.Kill(true);  // true 参数表示同时终止子进程
                                proc.WaitForExit();
                            }
                        }
                        catch { }
                    }
                    frpcProcess = null;
                }
            }
            catch { }
            finally
            {
                frpcProcess = null;
                UpdateStatus(false);
                AppendLog("服务已停止");
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (frpcProcess != null && frpcProcess.HasExited)
            {
                StopService();
            }
        }

        private void UpdateStatus(bool running)
        {
            isRunning = running;
            try
            {
                if (running)
                {
                    trayIcon.Icon = new Icon(GetIconPath("1.ico"));
                    trayIcon.Text = "大数据中心文件传输助手 - 运行中";
                    toggleButton.Text = "停止服务";
                    toggleButton.BackColor = Color.FromArgb(244, 67, 54);
                    
                    // 更新托盘菜单
                    var toggleItem = trayIcon.ContextMenuStrip.Items[1] as ToolStripMenuItem;
                    if (toggleItem != null) toggleItem.Text = "停止服务";
                }
                else
                {
                    trayIcon.Icon = new Icon(GetIconPath("2.ico"));
                    trayIcon.Text = "大数据中心文件传输助手";
                    toggleButton.Text = "启动服务";
                    toggleButton.BackColor = Color.FromArgb(76, 175, 80);
                    
                    // 更新托盘菜单
                    var toggleItem = trayIcon.ContextMenuStrip.Items[1] as ToolStripMenuItem;
                    if (toggleItem != null) toggleItem.Text = "启动服务";
                }
            }
            catch (Exception ex)
            {
                AppendLog($"更新状态失败: {ex.Message}");
            }
        }

        private string GetIconPath(string iconName)
        {
            // 尝试在不同位置查找图标文件
            string[] possiblePaths = new string[]
            {
                // 当前目录
                Path.Combine(Application.StartupPath, iconName),
                // 上级目录
                Path.Combine(Path.GetDirectoryName(Application.StartupPath), iconName),
                // 上上级目录
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.StartupPath)), iconName),
                // 上上上级目录
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Application.StartupPath))), iconName)
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException($"找不到图标文件: {iconName}");
        }

        private void AppendLog(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => AppendLog(message)));
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            logTextBox.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;  // 取消关闭操作
                this.Hide();      // 隐藏窗口
                trayIcon.ShowBalloonTip(2000, "大数据中心文件传输助手", "程序已最小化到系统托盘", ToolTipIcon.Info);
            }
            else
            {
                StopService();
                trayIcon.Dispose();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
            base.OnResize(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
