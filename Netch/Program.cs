using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.VisualStudio.Threading;
using Netch.Controllers;
using Netch.Enums;
using Netch.Forms;
using Netch.Services;
using Netch.Utils;
using Serilog.Events;
using SingleInstance;
#if RELEASE
using Windows.Win32.UI.WindowsAndMessaging;
#endif

namespace Netch;

public static class Program
{
    public static readonly ISingleInstanceService SingleInstance = new SingleInstanceService($"Global\\{nameof(Netch)}");

    internal static HWND ConsoleHwnd { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // handle arguments
        if (args.Contains(Constants.Parameter.ForceUpdate))
            Flags.AlwaysShowNewVersionFound = true;

        // set working directory
        Directory.SetCurrentDirectory(Global.NetchDir);

        // append .\bin to PATH
        var binPath = Path.Combine(Global.NetchDir, "bin");
        Environment.SetEnvironmentVariable("PATH", $"{Environment.GetEnvironmentVariable("PATH")};{binPath}");

#if !DEBUG
        // check if .\bin directory exists
        if (!Directory.Exists("bin") || !Directory.EnumerateFileSystemEntries("bin").Any())
        {
            i18N.Load("System");
            MessageBoxX.Show(i18N.Translate("Please extract all files then run the program!"));
            Environment.Exit(2);
        }
#endif
        // clean up old files
        Updater.CleanOld(Global.NetchDir);

        // pre-create directories
        var directories = new[] { "mode\\Custom", "data", "i18n", "logging" };
        foreach (var item in directories)
            if (!Directory.Exists(item))
                Directory.CreateDirectory(item);

        // load configuration
        Task.Run(async () =>
        {
            await Configuration.LoadAsync();
        }).ConfigureAwait(false).GetAwaiter().GetResult();

        // check if the program is already running
        if (!SingleInstance.TryStartSingleInstance())
        {
            Task.Run(async () =>
            {
                await SingleInstance.SendMessageToFirstInstanceAsync(Constants.Parameter.Show);
            }).ConfigureAwait(false).GetAwaiter().GetResult(); // 使用 ConfigureAwait(false)

            Environment.Exit(0);
            return;
        }

        SingleInstance.Received.Subscribe(SingleInstance_ArgumentsReceived);

        // archive current log and clean up old logs (> 7 days)
        if (Directory.Exists("logging"))
        {
            try
            {
                var currentLog = Path.Combine(Global.NetchDir, Constants.LogFile);
                if (File.Exists(currentLog))
                {
                    var prevLog = Path.Combine(Global.NetchDir, "logging", "application.previous.log");
                    File.Copy(currentLog, prevLog, true);
                }

                var directory = new DirectoryInfo("logging");
                foreach (var file in directory.GetFiles("*.log*"))
                {
                    if (file.Name != "application.log" && file.Name != "application.previous.log" && DateTime.Now - file.LastWriteTime > TimeSpan.FromDays(7))
                        file.Delete();
                }
            }
            catch
            {
                // ignored
            }
        }

        InitConsole();

        CreateLogger();

        Log.Information("Configuration loaded: {ServerCount} servers, {ProfileCount} profiles, Active Core: {CoreType}, Theme: {Theme}",
            Global.Settings.Server.Count,
            Global.Settings.Profiles.Count,
            Global.Settings.CoreType,
            Global.Settings.Theme);

        // load i18n
        i18N.Load(Global.Settings.Language);

        // log environment information
        LogEnvironmentAsync().Forget();
        CheckClr();
        CheckOS();

        // handle exceptions
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += Application_OnException;
        Application.ApplicationExit += Application_OnExit;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "AppDomain Unhandled Exception");
                Log.CloseAndFlush();
                MessageBox.Show($"Unhandled Fatal Error: {ex.Message}\n\n{ex.StackTrace}", @"Netch Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            var baseEx = e.Exception.GetBaseException();
            if (baseEx is SocketException or OperationCanceledException or TaskCanceledException)
            {
                // 忽略底层网络断开或取消导致的非致命任务异常，避免刷爆日志
                Log.Debug(baseEx, "Ignored unobserved network cancellation exception");
                return;
            }

            Log.Error(e.Exception, "Unobserved Task Exception");
        };

        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(Global.MainForm);
    }

    private static async Task LogEnvironmentAsync()
    {
        Log.Information("Netch Version: {Version}", $"{UpdateChecker.Owner}/{UpdateChecker.Repo}@{UpdateChecker.Version}");
        Log.Information("OS: {OSVersion}", Environment.OSVersion);
        Log.Information("SHA256: {Hash}", $"{await Utils.Utils.Sha256CheckSumAsync(Global.NetchExecutable)}");
        Log.Information("System Language: {Language}", CultureInfo.CurrentCulture.Name);

#if RELEASE
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            // TODO log level setting
            Task.Run(() => Log.Debug("Third-party Drivers:\n{Drivers}", string.Join(Constants.EOF, SystemInfo.SystemDrivers(false)))).Forget();
            Task.Run(() => Log.Debug("Running Processes: \n{Processes}", string.Join(Constants.EOF, SystemInfo.Processes(false)))).Forget();
        }
#endif
    }

    private static void CheckClr()
    {
        var framework = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (framework == null)
        {
            Log.Warning("TargetFrameworkAttribute null");
            return;
        }

        var frameworkName = new FrameworkName(framework);

        if (frameworkName.Version.Major != Environment.Version.Major)
        {
            Log.Information("CLR: {Version}", Environment.Version);
            Flags.NoSupport = true;
            if (!Global.Settings.NoSupportDialog)
                MessageBoxX.Show(
                    i18N.TranslateFormat("{0} won't get developers' support, Please do not report any issues or seek help from developers.",
                        "CLR " + Environment.Version),
                    LogLevel.WARNING);
        }
    }

    private static void CheckOS()
    {
        if (Environment.OSVersion.Version.Build < 17763)
        {
            Flags.NoSupport = true;
            if (!Global.Settings.NoSupportDialog)
                MessageBoxX.Show(
                    i18N.TranslateFormat("{0} won't get developers' support, Please do not report any issues or seek help from developers.",
                        Environment.OSVersion),
                    LogLevel.WARNING);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handlerRoutine, bool add);

    private delegate bool ConsoleCtrlDelegate(int ctrlType);
    private static ConsoleCtrlDelegate? _consoleCtrlHandler;

    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x00000000;

    private static void InitConsole()
    {
        PInvoke.AllocConsole();

        ConsoleHwnd = PInvoke.GetConsoleWindow();

        // 1. 禁用控制台右上角的 [X] 关闭按钮，防止用户误触直接关闭整个 Netch 主程序
        var hMenu = GetSystemMenu((IntPtr)ConsoleHwnd.Value, false);
        if (hMenu != IntPtr.Zero)
        {
            DeleteMenu(hMenu, SC_CLOSE, MF_BYCOMMAND);
        }

        // 2. 注册控制台关闭事件处理程序：若收到 CTRL_CLOSE_EVENT，转为隐藏控制台，阻止进程被 Windows 强制终止
        _consoleCtrlHandler = ctrlType =>
        {
            if (ctrlType == 2) // CTRL_CLOSE_EVENT
            {
                PInvoke.ShowWindow(ConsoleHwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
                return true; // 返回 true 表示已拦截处理，严禁退出进程
            }
            return false;
        };
        SetConsoleCtrlHandler(_consoleCtrlHandler, true);

#if RELEASE
        // hide console window
        PInvoke.ShowWindow(ConsoleHwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
#endif
    }

    public static void CreateLogger()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Verbose()
#else
            .MinimumLevel.Debug()
#endif
            .WriteTo.Async(c => c.File(Path.Combine(Global.NetchDir, Constants.LogFile),
                outputTemplate: Constants.OutputTemplate,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024))
            .WriteTo.Console(outputTemplate: Constants.OutputTemplate)
            .MinimumLevel.Override(@"Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .CreateLogger();
    }

    private static void Application_OnException(object sender, ThreadExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled error");
    }

    private static void Application_OnExit(object? sender, EventArgs eventArgs)
    {
        Log.CloseAndFlush();
    }

    private static void SingleInstance_ArgumentsReceived((string, Action<string>) receive)
    {
        var (arg, endFunc) = receive;
        if (arg == Constants.Parameter.Show)
        {
            Utils.Utils.ActivateVisibleWindows();
        }

        endFunc(string.Empty);
    }
}