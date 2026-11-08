using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Services;

/// <summary>
/// The app's services. The pages talk to the interfaces in Services/; the sample implementations in SampleData/ answer
/// without a server. Register your own implementations here instead and delete SampleData/. Everything is registered
/// with its type written out, so the container needs no reflection over the assembly and the app can be trimmed.
/// </summary>
public static class AppServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService, SampleAuthService>();
        services.AddSingleton<ICustomerService, SampleCustomerService>();
        services.AddSingleton<IWorkspaceService, SampleWorkspaceService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardPageViewModel>();
        services.AddTransient<CustomersPageViewModel>();
        services.AddTransient<TeamPageViewModel>();
        services.AddTransient<BillingPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        return services.BuildServiceProvider();
    }
}
