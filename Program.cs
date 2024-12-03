using System;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using Serilog;
using Serilog.Events;

namespace FRPCManager
{
    internal static class Program
    {
        public static ILogger Logger { get; private set; } = null!;

        [STAThread]
        static void Main()
        {
            try
            {
                ConfigureLogging();
                Logger.Information("程序开始启动");

                Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                Logger.Information("已设置异常处理");

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                Logger.Information("准备创建主窗体");
                var mainForm = new FRPCManager();
                Logger.Information("主窗体已创建");

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                string errorMessage = $"程序启动错误:\n{ex.Message}\n\n详细信息:\n{ex.StackTrace}";
                MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error(ex, "程序启动错误");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureLogging()
        {
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
                .WriteTo.File("logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Logger = Logger;
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string errorMessage = $"线程异常:\n{e.Exception.Message}";
            MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Logger.Error(e.Exception, "线程异常");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string errorMessage = $"未处理的异常:\n{ex?.Message}";
            MessageBox.Show(errorMessage, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Logger.Fatal(ex, "未处理的异常");
        }
    }
}
