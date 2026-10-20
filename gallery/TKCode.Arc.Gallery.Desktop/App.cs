using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace TKCode.Arc.Gallery.Desktop;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new GalleryWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
