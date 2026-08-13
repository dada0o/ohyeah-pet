using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PetFriends;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\PetFriends-dada0o-7C9F79D5";
    private PetWorld? _world;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Windows 11 display drivers can be unstable with animated, transparent,
        // topmost WPF windows. Keep this tiny app off the D3D rendering path.
        if (Compat.IsWindows11OrLater)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            RuntimeLog.Write("A second launch was ignored because the desktop pet is already running.");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) => RuntimeLog.WriteException("Dispatcher", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            RuntimeLog.WriteException("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            RuntimeLog.WriteException("TaskScheduler", args.Exception);
        RuntimeLog.Write($"Started v{typeof(App).Assembly.GetName().Version} on Windows {Compat.WindowsVersion}; safe rendering: {Compat.UseSafeRendering}.");
        AutostartService.InitializeDefault();
        if (Compat.IsLegacyWindows)
        {
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata(30));
        }
        _world = new PetWorld();
        _world.Start();
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
