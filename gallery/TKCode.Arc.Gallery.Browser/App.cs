using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace TKCode.Arc.Gallery.Browser;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        // Popups need the root's overlay layer, which AtomUI's browser root template does not enable.
        Styles.Add(EmbeddableRootTemplate.Create());
        // The browser has one view and no windows, so the shell is the page itself.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // The star count is kept in local storage for an hour; the address bar decides the first page and follows the open one.
            GalleryGitHub.Cache = new BrowserStarCache();
            var shell = new GalleryShell(BrowserRouter.InitialRoute());
            singleView.MainView = shell;
            BrowserRouter.Attach(shell);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
