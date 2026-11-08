using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.Pages;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Navigation;

/// <summary>One page of the app: a stable key (what the page switch selects by), its title and how to create the view.</summary>
public sealed record AppPage(string Key, string Title, Func<Control> Create);

/// <summary>
/// The pages of the app. To add one, create its view in Pages/ (dotnet new tkcode-arc-page --view-model), add it here
/// and add an option with the same key to the page switch (Views/MainView.axaml). Each entry pairs the view with its view
/// model, made by the container: written out, not looked up by type name, so the app has no reflection and can be trimmed.
/// </summary>
public static class AppPages
{
    public const string Overview = "overview";
    public const string Revenue = "revenue";
    public const string Retention = "retention";
    public const string Usage = "usage";
    public const string Status = "status";

    public static IReadOnlyList<AppPage> All { get; } =
    [
        new(Overview, "Overview", () => new OverviewPage { DataContext = Get<OverviewPageViewModel>() }),
        new(Revenue, "Revenue", () => new RevenuePage { DataContext = Get<RevenuePageViewModel>() }),
        new(Retention, "Retention", () => new RetentionPage { DataContext = Get<RetentionPageViewModel>() }),
        new(Usage, "Usage", () => new UsagePage { DataContext = Get<UsagePageViewModel>() }),
        new(Status, "Status", () => new StatusPage { DataContext = Get<StatusPageViewModel>() }),
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
