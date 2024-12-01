using System;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace FRPCManager
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                File.AppendAllText("startup.log", $"[{DateTime.Now}] 程序开始启动\n");

                Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                File.AppendAllText("startup.log", "已设置异常处理\n");

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                File.AppendAllText("startup.log", "准备创建主窗体\n");
                var mainForm = new FRPCManager();
                File.AppendAllText("startup.log", "主窗体已创建\n");

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                string errorMessage = $"程序启动错误:\n{ex.Message}\n\n详细信息:\n{ex.StackTrace}";
                MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.AppendAllText("startup.log", $"[{DateTime.Now}]\n{errorMessage}\n\n");
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string errorMessage = $"线程异常:\n{e.Exception.Message}\n\n详细信息:\n{e.Exception.StackTrace}";
            MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            File.AppendAllText("startup.log", $"[{DateTime.Now}] Thread Exception:\n{errorMessage}\n\n");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string errorMessage = $"未处理的异常:\n{ex?.Message}\n\n详细信息:\n{ex?.StackTrace}";
            MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            File.AppendAllText("startup.log", $"[{DateTime.Now}] Unhandled Exception:\n{errorMessage}\n\n");
        }
    }
}
