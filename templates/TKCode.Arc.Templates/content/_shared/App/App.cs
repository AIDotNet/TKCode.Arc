using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
#if (UsesDi)
using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.Services;
#endif
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif
#if (UsesMvvm)
using ArcStarterApp.ViewModels;
#endif
using ArcStarterApp.Views;

namespace ArcStarterApp;

/// <summary>
/// The application. Arc needs nothing here: no base class, no App.axaml and no styles. UseArcApp() (AppSetup.cs)
/// registers the themes, and Arc's base theme styles the window and the Avalonia controls Arc's templates use.
/// </summary>
public class App : Application
{
#if (UsesDi)
    /// <summary>The app's services (Services/AppServices.cs), built when the app starts.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

#endif
    public override void OnFrameworkInitializationCompleted()
    {
#if (UsesDi)
        Services = AppServices.Build();
#endif
#if (UsesSettings)
        // Every theme change (the top bar's switch, the Settings page, your own ArcTheme.Apply calls) is saved.
        AppSettings.Current.TrackTheme();
#endif
        switch (ApplicationLifetime)
        {
            // A desktop window.
            case IClassicDesktopStyleApplicationLifetime desktop:
#if (UsesMvvm)
                desktop.MainWindow = new MainWindow { DataContext = CreateMainViewModel() };
#else
                desktop.MainWindow = new MainWindow();
#endif
                break;
            // A browser page or a phone screen: the content without a window.
            case ISingleViewApplicationLifetime singleView:
#if (UsesMvvm)
                singleView.MainView = new MainView { DataContext = CreateMainViewModel() };
#else
                singleView.MainView = new MainView();
#endif
                break;
        }
        base.OnFrameworkInitializationCompleted();
    }
#if (UsesMvvm)

#if (UsesDi)
    private static MainViewModel CreateMainViewModel() => Services.GetRequiredService<MainViewModel>();
#else
    private static MainViewModel CreateMainViewModel() => new();
#endif
#endif
}
