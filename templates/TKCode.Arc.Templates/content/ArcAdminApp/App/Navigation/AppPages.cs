using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.Pages;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Navigation;

/// <summary>One page of the app: a stable key (what the sidebar and the command palette select by), its title and how to create the view.</summary>
public sealed record AppPage(string Key, string Title, Func<Control> Create);

/// <summary>
/// The pages of the app. The sidebar (Views/MainView.axaml) and the command palette name them by
/// <see cref="AppPage.Key"/>; to add a page, create its view in Pages/ (dotnet new tkcode-arc-page --view-model), add it
/// here and add a row with the same key to the sidebar. Each entry pairs the view with its view model, which the
/// container creates (Services/AppServices.cs): written out here, not looked up by type name, so the app has no
/// reflection and can be trimmed.
/// </summary>
public static class AppPages
{
    public const string Dashboard = "dashboard";
    public const string Customers = "customers";
    public const string Team = "team";
    public const string Billing = "billing";
    public const string Settings = "settings";

    public static IReadOnlyList<AppPage> All { get; } =
    [
        new(Dashboard, "Dashboard", () => new DashboardPage { DataContext = Get<DashboardPageViewModel>() }),
        new(Customers, "Customers", () => new CustomersPage { DataContext = Get<CustomersPageViewModel>() }),
        new(Team, "Team", () => new TeamPage { DataContext = Get<TeamPageViewModel>() }),
        new(Billing, "Billing", () => new BillingPage { DataContext = Get<BillingPageViewModel>() }),
        new(Settings, "Settings", () => new SettingsPage { DataContext = Get<SettingsPageViewModel>() }),
    ];

    public static AppPage? Find(string? key)
    {
        foreach (var page in All)
        {
            if (page.Key == key)
            {
                return page;
            }
        }
        return null;
    }

    private static T Get<T>() where T : notnull => App.Services.GetRequiredService<T>();
}
