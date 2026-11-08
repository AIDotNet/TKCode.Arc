using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services: <see cref="IAnalyticsService"/> answers every page (SampleData/SampleAnalyticsService.cs makes the
/// figures up). Register your own here and delete SampleData/. Everything is registered with its type written out, so
/// the container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAnalyticsService, SampleAnalyticsService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<OverviewPageViewModel>();
        services.AddTransient<RevenuePageViewModel>();
        services.AddTransient<RetentionPageViewModel>();
        services.AddTransient<UsagePageViewModel>();
        services.AddTransient<StatusPageViewModel>();
        return services.BuildServiceProvider();
    }
}
