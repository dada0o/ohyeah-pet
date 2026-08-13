#if NET35
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace PetFriends
{
    internal static class Program
    {
        private static PetWorld _world;

        [STAThread]
        public static void Main()
        {
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata(30));

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            application.Startup += delegate
            {
                AutostartService.InitializeDefault();
                _world = new PetWorld();
                _world.Start();
            };
            application.Run();
        }
    }
}
#endif
