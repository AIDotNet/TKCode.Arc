using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services: <see cref="IProjectService"/> answers every page (SampleData/SampleProjectService.cs makes the
/// data up). Register your own here and delete SampleData/. Everything is registered with its type written out, so the
/// container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProjectService, SampleProjectService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<BoardPageViewModel>();
        services.AddTransient<TimelinePageViewModel>();
        services.AddTransient<CalendarPageViewModel>();
        services.AddTransient<ReleasePageViewModel>();
        return services.BuildServiceProvider();
    }
}
