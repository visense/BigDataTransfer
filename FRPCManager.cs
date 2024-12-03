using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace FRPCManager
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public partial class FRPCManager : Form
    {
        private readonly TableLayoutPanel mainTableLayout;
        private readonly Panel hostInfoPanel;
        private readonly TreeView localDirTree;
        private readonly ListView localFileList;
        private readonly RichTextBox logTextBox;
        private readonly TextBox hostTextBox;
        private readonly TextBox usernameTextBox;
        private readonly TextBox passwordTextBox;
        private readonly TextBox portTextBox;
        private readonly Label hostLabel;
        private readonly Label userLabel;
        private readonly Label passwordLabel;
        private readonly Label portLabel;
        private readonly Button connectButton;
        private readonly TableLayoutPanel connectionPanel;
        private readonly TreeView remoteTreeView;
        private readonly ListView remoteFileList;
        private readonly SplitContainer mainSplitContainer;
        private readonly SplitContainer leftSplitContainer;
        private readonly SplitContainer rightSplitContainer;
        
        private SftpClient? sftpClient;
        private Process? frpcProcess;

        public FRPCManager()
        {
            try
            {
                mainTableLayout = new TableLayoutPanel();
                mainSplitContainer = new SplitContainer();
                leftSplitContainer = new SplitContainer() { Orientation = Orientation.Horizontal };
                rightSplitContainer = new SplitContainer() { Orientation = Orientation.Horizontal };
                hostInfoPanel = new Panel();
                localDirTree = new TreeView();
                localFileList = new ListView();
                logTextBox = new RichTextBox()
                {
                    Multiline = true,
                    ReadOnly = true,
                    BackColor = Color.Black,
                    ForeColor = Color.Gray,
                    Font = new Font("Consolas", 9F, FontStyle.Regular),
                    Dock = DockStyle.Fill
                };
                hostTextBox = new TextBox();
                usernameTextBox = new TextBox();
                passwordTextBox = new TextBox();
                portTextBox = new TextBox();
                hostLabel = new Label();
                userLabel = new Label();
                passwordLabel = new Label();
                portLabel = new Label();
                connectButton = new Button();
                connectionPanel = new TableLayoutPanel();
                remoteTreeView = new TreeView();
                remoteFileList = new ListView();

                this.Load += FRPCManager_Load;
                this.FormClosing += FRPCManager_FormClosing;

                InitializeUI();
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "构造函数错误");
                throw;
            }
        }

        private static class LogColors
        {
            public static readonly Color Success = Color.FromArgb(144, 238, 144);  // Light green
            public static readonly Color Error = Color.FromArgb(255, 99, 71);      // Tomato red
            public static readonly Color Warning = Color.FromArgb(255, 215, 0);    // Golden yellow
            public static readonly Color Info = Color.FromArgb(255, 165, 0);       // Orange
            public static readonly Color Default = Color.FromArgb(220, 220, 220);  // Light gray
        }

        private void AppendLog(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action<string>(AppendLog), message);
                return;
            }

            logTextBox.SelectionStart = logTextBox.TextLength;
            
            // Determine message color based on content
            Color messageColor;
            if (message.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) || 
                message.Contains("错误", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("失败", StringComparison.OrdinalIgnoreCase))
            {
                Program.Logger.Error(message);
                messageColor = LogColors.Error;
            }
            else if (message.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase) || 
                     message.Contains("警告", StringComparison.OrdinalIgnoreCase))
            {
                Program.Logger.Warning(message);
                messageColor = LogColors.Warning;
            }
            else if (message.Contains("success", StringComparison.OrdinalIgnoreCase) || 
                     message.Contains("成功", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("established", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("connected", StringComparison.OrdinalIgnoreCase))
            {
                Program.Logger.Information(message);
                messageColor = LogColors.Success;
            }
            else if (message.Contains("prepare", StringComparison.OrdinalIgnoreCase) || 
                     message.Contains("开始", StringComparison.OrdinalIgnoreCase) || 
                     message.Contains("准备", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("visitor added", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("establishing", StringComparison.OrdinalIgnoreCase))
            {
                Program.Logger.Information(message);
                messageColor = LogColors.Info;
            }
            else
            {
                Program.Logger.Information(message);
                messageColor = LogColors.Default;
            }

            // Add timestamp with default color
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            logTextBox.SelectionColor = LogColors.Default;
            logTextBox.AppendText($"[{timestamp}] ");
            
            // Add message with determined color
            logTextBox.SelectionStart = logTextBox.TextLength;
            logTextBox.SelectionColor = messageColor;
            logTextBox.AppendText($"{message}{Environment.NewLine}");
            
            // Scroll to show latest message
            logTextBox.ScrollToCaret();
        }

        private async Task ExecuteCommandWithTimeout(string command, string arguments, int timeoutMs = 5000)
        {
            using var process = new Process();
            using var cts = new CancellationTokenSource(timeoutMs);
            
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        Program.Logger.Information(e.Data);
                        AppendLog(e.Data);
                    });
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        Program.Logger.Error(e.Data);
                        AppendLog($"Error: {e.Data}");
                    });
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() =>
                {
                    if (!process.WaitForExit(timeoutMs))
                    {
                        Program.Logger.Warning($"Command execution timed out after {timeoutMs}ms: {command} {arguments}");
                        process.Kill();
                        AppendLog($"Warning: Command execution timed out after {timeoutMs}ms");
                        throw new TimeoutException($"Command timed out after {timeoutMs}ms");
                    }
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Program.Logger.Warning($"Command execution cancelled: {command} {arguments}");
                if (!process.HasExited)
                {
                    process.Kill();
                    AppendLog($"Warning: Command execution cancelled");
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "Command execution error");
                if (!process.HasExited)
                {
                    process.Kill();
                }
                AppendLog($"Error: {ex.Message}");
            }
        }

        private async Task RunFrpcProcess()
        {
            try
            {
                if (frpcProcess != null && !frpcProcess.HasExited)
                {
                    Program.Logger.Warning("FRPC进程已在运行");
                    AppendLog("Warning: FRPC进程已在运行");
                    return;
                }

                await ExecuteCommandWithTimeout("frpc.exe", "-c frpc.toml", 5000);
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "运行FRPC进程时发生错误");
                AppendLog($"Error: 运行FRPC时发生错误: {ex.Message}");
            }
        }

        private void FRPCManager_Load(object? sender, EventArgs e)
        {
            try
            {
                // 设置分隔线位置
                if (mainSplitContainer.Width > mainSplitContainer.Panel1MinSize + mainSplitContainer.Panel2MinSize)
                {
                    mainSplitContainer.SplitterDistance = mainSplitContainer.Width / 2;
                }

                if (leftSplitContainer.Height > leftSplitContainer.Panel1MinSize + leftSplitContainer.Panel2MinSize)
                {
                    leftSplitContainer.SplitterDistance = leftSplitContainer.Height / 2;
                    rightSplitContainer.SplitterDistance = rightSplitContainer.Height / 2;
                }

                // 调整列表列宽
                AdjustListViewColumns();
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "窗体加载错误");
            }
        }

        private void InitializeUI()
        {
            try
            {
                this.Text = "FRPC Manager";
                this.Size = new Size(1200, 800);
                this.MinimumSize = new Size(800, 600);

                // 主布局
                mainTableLayout.Dock = DockStyle.Fill;
                mainTableLayout.RowCount = 3;
                mainTableLayout.ColumnCount = 1;
                mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 80));
                mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
                mainTableLayout.Padding = new Padding(3);

                // 连接信息面板
                var connectionPanel = CreateConnectionPanel();
                connectionPanel.Dock = DockStyle.Fill;
                mainTableLayout.Controls.Add(connectionPanel, 0, 0);

                // 主分隔容器设置
                mainSplitContainer.Dock = DockStyle.Fill;
                mainSplitContainer.BorderStyle = BorderStyle.Fixed3D;
                mainSplitContainer.Panel1MinSize = 100;
                mainSplitContainer.Panel2MinSize = 100;

                // 左侧分隔容器设置
                leftSplitContainer.Dock = DockStyle.Fill;
                leftSplitContainer.BorderStyle = BorderStyle.Fixed3D;
                leftSplitContainer.Orientation = Orientation.Horizontal;
                leftSplitContainer.Panel1MinSize = 50;
                leftSplitContainer.Panel2MinSize = 50;

                // 右侧分隔容器设置
                rightSplitContainer.Dock = DockStyle.Fill;
                rightSplitContainer.BorderStyle = BorderStyle.Fixed3D;
                rightSplitContainer.Orientation = Orientation.Horizontal;
                rightSplitContainer.Panel1MinSize = 50;
                rightSplitContainer.Panel2MinSize = 50;

                // 初始化各个控件
                InitializeLocalTreeView();
                InitializeLocalFileList();
                InitializeRemoteTreeView();
                InitializeRemoteFileList();

                // 添加控件到分隔容器
                leftSplitContainer.Panel1.Controls.Add(localDirTree);
                leftSplitContainer.Panel2.Controls.Add(localFileList);
                rightSplitContainer.Panel1.Controls.Add(remoteTreeView);
                rightSplitContainer.Panel2.Controls.Add(remoteFileList);

                mainSplitContainer.Panel1.Controls.Add(leftSplitContainer);
                mainSplitContainer.Panel2.Controls.Add(rightSplitContainer);

                // 添加控件到主布局
                mainTableLayout.Controls.Add(mainSplitContainer, 0, 1);
                mainTableLayout.Controls.Add(logTextBox, 0, 2);

                // 添加主布局到窗体
                this.Controls.Add(mainTableLayout);

                // 添加分隔线同步移动事件
                leftSplitContainer.SplitterMoved += (s, e) =>
                {
                    try
                    {
                        if (rightSplitContainer.Height > rightSplitContainer.Panel1MinSize + rightSplitContainer.Panel2MinSize)
                        {
                            rightSplitContainer.SplitterDistance = leftSplitContainer.SplitterDistance;
                        }
                    }
                    catch { }
                };

                rightSplitContainer.SplitterMoved += (s, e) =>
                {
                    try
                    {
                        if (leftSplitContainer.Height > leftSplitContainer.Panel1MinSize + leftSplitContainer.Panel2MinSize)
                        {
                            leftSplitContainer.SplitterDistance = rightSplitContainer.SplitterDistance;
                        }
                    }
                    catch { }
                };

                // 添加窗口大小改变事件
                this.SizeChanged += (s, e) =>
                {
                    AdjustListViewColumns();
                };

                // 初始化目录树
                PopulateLocalDirTree();
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex, "UI初始化错误");
                throw;
            }
        }

        private void InitializeLocalTreeView()
        {
            localDirTree.Dock = DockStyle.Fill;
            localDirTree.HideSelection = false;
            localDirTree.ShowLines = true;
            localDirTree.ShowPlusMinus = true;
            localDirTree.ShowRootLines = true;
            localDirTree.BorderStyle = BorderStyle.None;
            localDirTree.ImageList = new ImageList();
            localDirTree.AfterSelect += LocalDirTree_AfterSelect;
        }

        private void InitializeLocalFileList()
        {
            localFileList.Dock = DockStyle.Fill;
            localFileList.View = View.Details;
            localFileList.FullRowSelect = true;
            localFileList.GridLines = true;
            localFileList.BorderStyle = BorderStyle.None;
            localFileList.SmallImageList = new ImageList();
            localFileList.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "文件名", Width = 200 },
                new ColumnHeader { Text = "文件大小", Width = 100 },
                new ColumnHeader { Text = "文件类型", Width = 100 },
                new ColumnHeader { Text = "最近修改", Width = 150 },
                new ColumnHeader { Text = "权限", Width = 80 },
                new ColumnHeader { Text = "所有者", Width = 100 }
            });
        }

        private void InitializeRemoteTreeView()
        {
            remoteTreeView.Dock = DockStyle.Fill;
            remoteTreeView.HideSelection = false;
            remoteTreeView.ShowLines = true;
            remoteTreeView.ShowPlusMinus = true;
            remoteTreeView.ShowRootLines = true;
            remoteTreeView.BorderStyle = BorderStyle.None;
            remoteTreeView.ImageList = new ImageList();
            remoteTreeView.BeforeExpand += RemoteTreeView_BeforeExpand;
            remoteTreeView.AfterSelect += RemoteTreeView_AfterSelect;
        }

        private void InitializeRemoteFileList()
        {
            remoteFileList.Dock = DockStyle.Fill;
            remoteFileList.View = View.Details;
            remoteFileList.FullRowSelect = true;
            remoteFileList.GridLines = true;
            remoteFileList.BorderStyle = BorderStyle.None;
            remoteFileList.SmallImageList = new ImageList();
            remoteFileList.Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "文件名", Width = 200 },
                new ColumnHeader { Text = "大小", Width = 100 },
                new ColumnHeader { Text = "类型", Width = 100 },
                new ColumnHeader { Text = "修改日期", Width = 150 },
                new ColumnHeader { Text = "权限", Width = 80 },
                new ColumnHeader { Text = "所有者", Width = 100 }
            });
        }

        private void PopulateLocalDirTree()
        {
            try
            {
                localDirTree.Nodes.Clear();
                string rootPath = @"C:\Users\cocodex\";
                var rootNode = new TreeNode(rootPath)
                {
                    ImageIndex = 0,
                    SelectedImageIndex = 0
                };
                PopulateTreeNode(rootNode, rootPath);
                localDirTree.Nodes.Add(rootNode);
                rootNode.Expand();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error populating directory tree: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateTreeNode(TreeNode node, string path)
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var dir in dirInfo.GetDirectories())
                {
                    var childNode = new TreeNode(dir.Name)
                    {
                        ImageIndex = 0,
                        SelectedImageIndex = 0
                    };
                    node.Nodes.Add(childNode);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载目录 {path} 时发生错误: {ex.Message}");
            }
        }

        private void UpdateFileList(string path)
        {
            localFileList.Items.Clear();
            try
            {
                DirectoryInfo di = new DirectoryInfo(path);
                foreach (var dir in di.GetDirectories())
                {
                    var item = new ListViewItem(dir.Name);
                    item.SubItems.AddRange(new[]
                    {
                        "",
                        "文件夹",
                        dir.LastWriteTime.ToString("yyyy/MM/dd"),
                        "",
                        ""
                    });
                    item.ImageIndex = 0; 
                    localFileList.Items.Add(item);
                }

                foreach (var file in di.GetFiles())
                {
                    var item = new ListViewItem(file.Name);
                    item.SubItems.AddRange(new[]
                    {
                        FormatFileSize(file.Length),
                        file.Extension,
                        file.LastWriteTime.ToString("yyyy/MM/dd"),
                        "",
                        ""
                    });
                    item.ImageIndex = 1; 
                    localFileList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void LogMessage(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => LogMessage(message)));
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            logTextBox.ScrollToCaret();
        }

        private void ConnectButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // 先关闭现有的frpc进程
                KillExistingFrpcProcesses();

                // 获取输入的连接信息
                string host = hostTextBox.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                string password = passwordTextBox.Text;
                string port = portTextBox.Text.Trim();

                // 验证输入
                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || 
                    string.IsNullOrEmpty(password) || string.IsNullOrEmpty(port))
                {
                    MessageBox.Show("请填写所有连接信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 使用现有的frpc.toml配置文件
                string configPath = Path.Combine(Application.StartupPath, "frpc.toml");
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("找不到frpc.toml配置文件！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 启动frpc进程
                string frpcPath = Path.Combine(Application.StartupPath, "frpc.exe");
                if (!File.Exists(frpcPath))
                {
                    MessageBox.Show("找不到frpc.exe！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = frpcPath,
                    Arguments = $"-c \"{configPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Application.StartupPath
                };

                frpcProcess = new Process { StartInfo = startInfo };
                frpcProcess.OutputDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AppendLog(args.Data);
                        // 当frpc连接成功后，尝试SFTP连接
                        if (args.Data.Contains("start visitor success"))
                        {
                            ConnectSftp(host, username, password, int.Parse(port));
                        }
                    }
                };
                frpcProcess.ErrorDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        AppendLog($"错误: {args.Data}");
                    }
                };

                frpcProcess.Start();
                frpcProcess.BeginOutputReadLine();
                frpcProcess.BeginErrorReadLine();

                AppendLog("正在连接到服务器...");
                connectButton.Enabled = false;
                connectButton.Text = "连接中...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"连接失败: {ex.Message}");
                connectButton.Enabled = true;
                connectButton.Text = "快速连接(Q)";
            }
        }

        private void KillExistingFrpcProcesses()
        {
            try
            {
                // 首先使用常规方式终止进程
                Process[] processes = Process.GetProcessesByName("frpc");
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true); // 使用 Kill(true) 来终止整个进程树
                            process.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"使用 Process.Kill 终止进程失败: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // 使用命令行强制终止所有相关进程
                ExecuteCommand("taskkill", "/F /T /IM frpc.exe");
                
                // 使用 netstat 检查端口，如果还在使用就强制关闭
                var netstatInfo = ExecuteCommand("netstat", "-ano | findstr :6222");
                if (!string.IsNullOrEmpty(netstatInfo))
                {
                    foreach (string line in netstatInfo.Split('\n'))
                    {
                        if (line.Contains(":6222"))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 4)
                            {
                                string pid = parts[parts.Length - 1].Trim();
                                if (int.TryParse(pid, out int processId))
                                {
                                    ExecuteCommand("taskkill", $"/F /PID {processId}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"终止进程时出错: {ex.Message}");
            }
        }

        private string ExecuteCommand(string command, string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000); // 等待最多5秒
                
                return output;
            }
            catch (Exception ex)
            {
                LogMessage($"执行命令 {command} {arguments} 失败: {ex.Message}");
                return string.Empty;
            }
        }

        private void ConnectSftp(string host, string username, string password, int port)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    try
                    {
                        var connectionInfo = new ConnectionInfo(host, port, username,
                            new PasswordAuthenticationMethod(username, password));

                        sftpClient = new SftpClient(connectionInfo);
                        sftpClient.Connect();

                        if (sftpClient.IsConnected)
                        {
                            AppendLog("SFTP连接成功！");
                            RefreshRemoteDirectory(); // 连接成功后刷新远程目录
                            connectButton.Enabled = true;
                            connectButton.Text = "快速连接(Q)";
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"SFTP连接失败: {ex.Message}");
                        MessageBox.Show($"SFTP连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        connectButton.Enabled = true;
                        connectButton.Text = "快速连接(Q)";
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"SFTP连接失败: {ex.Message}");
                this.Invoke((MethodInvoker)delegate
                {
                    connectButton.Enabled = true;
                    connectButton.Text = "快速连接(Q)";
                });
            }
        }

        private void RefreshRemoteDirectory()
        {
            if (sftpClient == null || !sftpClient.IsConnected)
            {
                AppendLog("SFTP未连接，无法刷新远程目录");
                return;
            }

            try
            {
                // 初始化图标列表
                if (remoteTreeView.ImageList == null)
                {
                    var imageList = new ImageList();
                    // 使用系统自带的文件夹和文件图标
                    Icon folderIcon = Icon.ExtractAssociatedIcon(Path.Combine(Environment.SystemDirectory, "shell32.dll")) ?? SystemIcons.Application;
                    Icon fileIcon = Icon.ExtractAssociatedIcon(Path.Combine(Environment.SystemDirectory, "shell32.dll")) ?? SystemIcons.Application;
                    imageList.Images.Add(folderIcon);
                    imageList.Images.Add(fileIcon);
                    remoteTreeView.ImageList = imageList;
                    remoteFileList.SmallImageList = imageList; // 设置相同的图标列表
                }

                remoteTreeView.BeginUpdate();
                remoteTreeView.Nodes.Clear();
                var rootNode = new TreeNode("/")
                {
                    Tag = "/",
                    ImageIndex = 0,
                    SelectedImageIndex = 0
                };
                remoteTreeView.Nodes.Add(rootNode);

                // 异步加载远程目录内容
                Task.Run(() =>
                {
                    try
                    {
                        var files = sftpClient.ListDirectory("/");
                        this.Invoke((MethodInvoker)delegate
                        {
                            foreach (var file in files)
                            {
                                if (file.Name == "." || file.Name == "..")
                                    continue;

                                var node = new TreeNode(file.Name)
                                {
                                    Tag = file.FullName,
                                    ImageIndex = file.IsDirectory ? 0 : 1,
                                    SelectedImageIndex = file.IsDirectory ? 0 : 1
                                };
                                rootNode.Nodes.Add(node);
                                
                                // 如果是目录，添加一个临时子节点
                                if (file.IsDirectory)
                                {
                                    node.Nodes.Add(new TreeNode("Loading..."));
                                }
                            }
                            rootNode.Expand();
                            AppendLog("远程目录刷新成功");
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            AppendLog($"刷新远程目录失败: {ex.Message}");
                        });
                    }
                    finally
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            remoteTreeView.EndUpdate();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"初始化远程目录树失败: {ex.Message}");
                remoteTreeView.EndUpdate();
            }
        }

        private void RemoteTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (sftpClient == null || !sftpClient.IsConnected || sender == null)
                return;

            var node = e.Node;
            if (node == null || node.Nodes.Count != 1 || node.Nodes[0].Text != "Loading...")
                return;

            e.Cancel = true;
            node.Nodes.Clear();

            try
            {
                var path = node.Tag?.ToString() ?? "/";
                var files = sftpClient.ListDirectory(path);

                foreach (var file in files)
                {
                    if (file.Name == "." || file.Name == "..")
                        continue;

                    var childNode = new TreeNode(file.Name)
                    {
                        Tag = file.FullName,
                        ImageIndex = file.IsDirectory ? 0 : 1,
                        SelectedImageIndex = file.IsDirectory ? 0 : 1
                    };
                    node.Nodes.Add(childNode);

                    if (file.IsDirectory)
                    {
                        childNode.Nodes.Add(new TreeNode("Loading..."));
                    }
                }

                e.Cancel = false;
            }
            catch (Exception ex)
            {
                AppendLog($"加载目录 {node.Tag} 失败: {ex.Message}");
                node.Nodes.Add(new TreeNode("Error loading directory"));
            }
        }

        private void RemoteTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (sftpClient == null || !sftpClient.IsConnected || e.Node == null)
                return;

            try
            {
                var path = e.Node.Tag?.ToString() ?? "/";
                UpdateRemoteFileList(path);
            }
            catch (Exception ex)
            {
                AppendLog($"加载远程文件列表失败: {ex.Message}");
            }
        }

        private void UpdateRemoteFileList(string path)
        {
            if (sftpClient == null || !sftpClient.IsConnected)
            {
                AppendLog("SFTP未连接，无法显示文件列表");
                return;
            }

            try
            {
                remoteFileList.BeginUpdate();
                remoteFileList.Items.Clear();

                var files = sftpClient.ListDirectory(path);
                foreach (var file in files)
                {
                    if (file.Name == "." || file.Name == "..")
                        continue;

                    var item = new ListViewItem(file.Name);
                    
                    // 添加文件信息
                    item.SubItems.AddRange(new[]
                    {
                        file.IsDirectory ? "<DIR>" : FormatFileSize(file.Length),
                        file.IsDirectory ? "文件夹" : (Path.GetExtension(file.Name) == "" ? "文件" : Path.GetExtension(file.Name)),
                        file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss"),
                        file.OwnerCanRead ? "r" : "-" + 
                        (file.OwnerCanWrite ? "w" : "-") + 
                        (file.OwnerCanExecute ? "x" : "-")
                    });

                    // 设置图标
                    item.ImageIndex = file.IsDirectory ? 0 : 1;
                    
                    remoteFileList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"更新远程文件列表失败: {ex.Message}");
            }
            finally
            {
                remoteFileList.EndUpdate();
            }
        }

        private void LocalDirTree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            try
            {
                var node = e.Node;
                if (node == null) return;

                string fullPath = GetFullPath(node);
                if (!Directory.Exists(fullPath)) return;

                localFileList.Items.Clear();
                var directory = new DirectoryInfo(fullPath);

                foreach (var dir in directory.GetDirectories())
                {
                    var item = new ListViewItem(dir.Name);
                    item.SubItems.Add("<DIR>");
                    item.SubItems.Add("文件夹");
                    item.SubItems.Add(dir.LastWriteTime.ToString());
                    item.SubItems.Add("");
                    item.SubItems.Add("");
                    item.ImageIndex = 0; // 使用文件夹图标
                    localFileList.Items.Add(item);
                }

                foreach (var file in directory.GetFiles())
                {
                    var item = new ListViewItem(file.Name);
                    item.SubItems.Add(FormatFileSize(file.Length));
                    item.SubItems.Add(GetFileType(file.Extension));
                    item.SubItems.Add(file.LastWriteTime.ToString());
                    item.SubItems.Add("");
                    item.SubItems.Add("");
                    item.ImageIndex = 1; // 使用文件图标
                    localFileList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading local directory: {ex.Message}");
            }
        }

        private string GetFullPath(TreeNode node)
        {
            var path = new Stack<string>();
            while (node != null)
            {
                path.Push(node.Text);
                node = node.Parent;
            }
            return Path.Combine(path.ToArray());
        }

        private string GetFileType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return "文件";
            return extension.TrimStart('.').ToUpper() + "文件";
        }

        private void FRPCManager_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                LogMessage("正在关闭程序...");

                // 先终止 frpc 进程
                if (frpcProcess != null)
                {
                    try
                    {
                        if (!frpcProcess.HasExited)
                        {
                            LogMessage("正在终止 frpc 进程...");
                            frpcProcess.Kill(true);  // 终止进程树
                            frpcProcess.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"终止 frpc 进程时出错: {ex.Message}");
                    }
                    finally
                    {
                        frpcProcess.Dispose();
                        frpcProcess = null;
                    }
                }

                // 强制终止所有 frpc 进程
                LogMessage("正在清理所有 frpc 进程...");
                ExecuteCommand("taskkill", "/F /T /IM frpc.exe");
                Thread.Sleep(1000); // 等待进程完全终止

                // 检查端口占用并强制释放
                LogMessage("正在检查端口占用...");
                var netstatInfo = ExecuteCommand("netstat", "-ano | findstr :6222");
                if (!string.IsNullOrEmpty(netstatInfo))
                {
                    foreach (string line in netstatInfo.Split('\n'))
                    {
                        if (line.Contains(":6222"))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 4)
                            {
                                string pid = parts[parts.Length - 1].Trim();
                                if (int.TryParse(pid, out int processId))
                                {
                                    LogMessage($"正在终止占用端口的进程 (PID: {processId})...");
                                    ExecuteCommand("taskkill", $"/F /PID {processId}");
                                    Thread.Sleep(500); // 给进程一些终止的时间
                                }
                            }
                        }
                    }
                }

                // 断开 SFTP 连接
                if (sftpClient != null)
                {
                    try
                    {
                        if (sftpClient.IsConnected)
                        {
                            LogMessage("正在断开 SFTP 连接...");
                            sftpClient.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"断开 SFTP 连接时出错: {ex.Message}");
                    }
                    finally
                    {
                        sftpClient.Dispose();
                        sftpClient = null;
                    }
                }

                // 最后一次检查端口
                netstatInfo = ExecuteCommand("netstat", "-ano | findstr :6222");
                if (!string.IsNullOrEmpty(netstatInfo))
                {
                    LogMessage("警告：端口 6222 仍然被占用！");
                    // 如果端口仍然被占用，可以选择取消关闭
                    if (MessageBox.Show("检测到端口 6222 仍然被占用，是否强制关闭程序？", 
                                      "警告", 
                                      MessageBoxButtons.YesNo, 
                                      MessageBoxIcon.Warning) == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                LogMessage("程序正常关闭");
            }
            catch (Exception ex)
            {
                LogMessage($"关闭程序时出错: {ex.Message}");
                if (MessageBox.Show($"关闭程序时出错：{ex.Message}\n是否强制关闭？", 
                                  "错误", 
                                  MessageBoxButtons.YesNo, 
                                  MessageBoxIcon.Error) == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void AdjustListViewColumns()
        {
            // 调整本地文件列表的列宽
            if (localFileList.Columns.Count > 0)
            {
                int totalWidth = localFileList.ClientSize.Width;
                if (totalWidth > 0)
                {
                    localFileList.Columns[0].Width = (int)(totalWidth * 0.3); // 文件名
                    localFileList.Columns[1].Width = (int)(totalWidth * 0.15); // 大小
                    localFileList.Columns[2].Width = (int)(totalWidth * 0.15); // 类型
                    localFileList.Columns[3].Width = (int)(totalWidth * 0.2); // 修改日期
                    localFileList.Columns[4].Width = (int)(totalWidth * 0.1); // 权限
                    localFileList.Columns[5].Width = (int)(totalWidth * 0.1); // 所有者
                }
            }

            // 调整远程文件列表的列宽
            if (remoteFileList.Columns.Count > 0)
            {
                int totalWidth = remoteFileList.ClientSize.Width;
                if (totalWidth > 0)
                {
                    remoteFileList.Columns[0].Width = (int)(totalWidth * 0.3);
                    remoteFileList.Columns[1].Width = (int)(totalWidth * 0.15);
                    remoteFileList.Columns[2].Width = (int)(totalWidth * 0.15);
                    remoteFileList.Columns[3].Width = (int)(totalWidth * 0.2);
                    remoteFileList.Columns[4].Width = (int)(totalWidth * 0.1);
                    remoteFileList.Columns[5].Width = (int)(totalWidth * 0.1);
                }
            }
        }

        private TableLayoutPanel CreateConnectionPanel()
        {
            var connectionPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 9,
                BackColor = SystemColors.Control
            };

            hostLabel.Text = "主机(H):";
            hostLabel.AutoSize = true;
            hostLabel.Margin = new Padding(3, 8, 3, 0);
            hostTextBox.Width = 150;
            hostTextBox.Text = "127.0.0.1";
            
            userLabel.Text = "用户名(U):";
            userLabel.AutoSize = true;
            userLabel.Margin = new Padding(3, 8, 3, 0);
            usernameTextBox.Width = 150;
            usernameTextBox.Text = "Sjchu";
            
            passwordLabel.Text = "密码(W):";
            passwordLabel.AutoSize = true;
            passwordLabel.Margin = new Padding(3, 8, 3, 0);
            passwordTextBox.Width = 150;
            passwordTextBox.Text = "Sjc123456";
            passwordTextBox.PasswordChar = '*';
            
            portLabel.Text = "端口(P):";
            portLabel.AutoSize = true;
            portLabel.Margin = new Padding(3, 8, 3, 0);
            portTextBox.Width = 80;
            portTextBox.Text = "6222";
            
            connectButton.Text = "快速连接(Q)";
            connectButton.Width = 100;
            connectButton.Height = 25;
            connectButton.Margin = new Padding(10, 3, 3, 3);
            connectButton.Click += ConnectButton_Click;

            connectionPanel.Controls.Add(hostLabel, 0, 0);
            connectionPanel.Controls.Add(hostTextBox, 1, 0);
            connectionPanel.Controls.Add(userLabel, 2, 0);
            connectionPanel.Controls.Add(usernameTextBox, 3, 0);
            connectionPanel.Controls.Add(passwordLabel, 4, 0);
            connectionPanel.Controls.Add(passwordTextBox, 5, 0);
            connectionPanel.Controls.Add(portLabel, 6, 0);
            connectionPanel.Controls.Add(portTextBox, 7, 0);
            connectionPanel.Controls.Add(connectButton, 8, 0);

            for (int i = 0; i < connectionPanel.ColumnCount; i++)
            {
                connectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            return connectionPanel;
        }
    }
}
