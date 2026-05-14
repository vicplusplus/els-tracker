using System.Windows;
using ElsTracker.Services;

namespace ElsTracker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ClassCatalog.Load();
        ThemeService.Load();
        EmoteService.Load();
        base.OnStartup(e);
    }
}
