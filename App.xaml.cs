using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PetFriends;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\PetFriends-dada0o-7C9F79D5";
    private PetWorld? _world;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            RuntimeLog.WriteException("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            RuntimeLog.WriteException("TaskScheduler", args.Exception);
        RuntimeLog.Write("Bootstrap entered.");

        try
        {
            // Transparent, topmost WPF windows are more reliable in software mode
            // across older display drivers and injected overlay software.
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            base.OnStartup(e);
            RuntimeLog.Write("WPF application startup completed.");

            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
            if (!_ownsSingleInstanceMutex)
            {
                RuntimeLog.Write("A second launch was ignored because the desktop pet is already running.");
                Shutdown();
                return;
            }

            PreloadUiAutomationProvider();
            RuntimeLog.Write($"Started v{typeof(App).Assembly.GetName().Version} on Windows {Compat.WindowsVersion}; safe rendering: true.");
            AutostartService.InitializeDefault();
            if (Compat.IsLegacyWindows)
            {
                Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(Timeline),
                    new FrameworkPropertyMetadata(30));
            }

            RuntimeLog.Write("Creating desktop pet windows.");
            _world = new PetWorld();
            _world.Start();
            RuntimeLog.Write("Startup completed.");
        }
        catch (Exception exception)
        {
            RuntimeLog.WriteException("Startup", exception);
            System.Windows.MessageBox.Show(
                $"桌宠启动失败。请把下面的日志文件发给开发者：\n\n{RuntimeLog.FilePath}",
                "小欧公爵和小耶牧师桌宠",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void PreloadUiAutomationProvider()
    {
        try
        {
            Assembly.Load(new AssemblyName("UIAutomationProvider"));
            RuntimeLog.Write("UIAutomationProvider loaded.");
        }
        catch (Exception exception)
        {
            // Some assistive or overlay software requests WPF automation as soon
            // as the first transparent window appears. Log the missing provider;
            // the dispatcher guard below keeps the pets usable without automation.
            RuntimeLog.WriteException("UIAutomationProvider preload", exception);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        RuntimeLog.WriteException("Dispatcher", args.Exception);
        if (!IsUiAutomationLoadFailure(args.Exception)) return;

        RuntimeLog.Write("Ignored a UI automation provider load failure to keep the desktop pet running.");
        args.Handled = true;
    }

    private static bool IsUiAutomationLoadFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is FileNotFoundException fileNotFound &&
                (fileNotFound.FileName?.StartsWith("UIAutomationProvider", StringComparison.OrdinalIgnoreCase) == true ||
                 fileNotFound.Message.Contains("UIAutomationProvider", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RuntimeLog.Write("Exited normally.");
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
