using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services. The view model talks to <see cref="IWorkspace"/> and <see cref="IBuildService"/>; the samples in
/// SampleData/ keep an invented project in memory and print a made-up build. Register your own here and delete
/// SampleData/. Everything is registered with its type written out, so the container needs no reflection over the
/// assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspace, SampleWorkspace>();
        services.AddSingleton<IBuildService, SampleBuildService>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
