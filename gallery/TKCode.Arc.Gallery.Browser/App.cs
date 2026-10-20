using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace TKCode.Arc.Gallery.Browser;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        // The browser has one view and no windows, so the shell is the page itself.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new GalleryShell();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
