using System.Windows;

namespace PetFriends;

public partial class App : System.Windows.Application
{
    private PetWorld? _world;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _world = new PetWorld();
        _world.Start();
    }
}
