using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services. Register a service here and take it as a constructor parameter of a view model; the container
/// creates the view models (App.cs for the main one, Navigation/AppPages.cs for the pages). Everything is registered
/// with its type written out, so the container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
#if (HasNavigation)
        services.AddSingleton<IProjectStore, SampleProjectStore>();
#endif
        services.AddSingleton<MainViewModel>();
#if (HasNavigation)
        services.AddTransient<ProjectsPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
#endif
        return services.BuildServiceProvider();
    }
}
