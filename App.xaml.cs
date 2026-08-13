using System.Windows;
using System.Windows.Media.Animation;

namespace PetFriends;

public partial class App : System.Windows.Application
{
    private PetWorld? _world;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Compat.IsLegacyWindows)
        {
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata(30));
        }
        _world = new PetWorld();
        _world.Start();
    }
}
