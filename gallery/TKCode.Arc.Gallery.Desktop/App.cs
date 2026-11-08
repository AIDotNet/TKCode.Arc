using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Desktop;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            GalleryHost.Kind = GalleryHostKind.Desktop;
            GalleryUpdates.Store = new FileUpdateStore();
            var window = new GalleryWindow();
            window.Opened += (_, _) =>
            {
                MacDockIcon.Apply();
                _ = NotifyUpdateAsync(window);
            };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Unless turned off in About, checks at startup and shows a toast with a download button when GitHub has a newer release.</summary>
    private static async Task NotifyUpdateAsync(GalleryWindow window)
    {
        if (!GalleryUpdates.AutoCheck)
        {
            return;
        }
        await GalleryUpdates.CheckAsync();
        if (GalleryUpdates.Available is not { } release)
        {
            return;
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var download = new ToastAction(GalleryText.Get(GalleryShellLangResourceKind.UpdateDownload),
                                           id => { _ = window.Launcher.LaunchUriAsync(release.DownloadUrl); });
            Toast.Show(window, GalleryText.Format(GalleryShellLangResourceKind.UpdateAvailable, release.Version),
                       action: download, duration: TimeSpan.FromSeconds(30));
        });
    }
}
