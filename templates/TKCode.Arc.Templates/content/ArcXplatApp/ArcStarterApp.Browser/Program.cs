using Avalonia;
using Avalonia.Browser;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif

namespace ArcStarterApp.Browser;

internal static class Program
{
    // "out" is the element of wwwroot/index.html the app draws into.
    private static Task Main(string[] args) => BuildAvaloniaApp().StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
    {
#if (UsesSettings)
        // A page has no settings file: keep the preferences in the browser's local storage for this site.
        AppSettings.Store = new BrowserSettingsStore();
#endif
        return AppBuilder.Configure<App>()
#if (NeedsCjkFont)
                         .WithCjkFont()
#endif
                         .UseArcApp();
    }
}
