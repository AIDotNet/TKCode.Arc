using Avalonia.Controls;
using ArcStarterApp.Pages;

namespace ArcStarterApp.Navigation;

/// <summary>One page of the app: a stable key (what the navigation selects by), the title the top bar shows and how to create the view.</summary>
public sealed record AppPage(string Key, string Title, Func<Control> Create);

/// <summary>
/// The pages of the app. The navigation (the sidebar in Views/MainWindow.axaml) names them by <see cref="AppPage.Key"/>;
/// to add a page, create its view in Pages/, add it here and add a row with the same key to the navigation.
/// </summary>
public static class AppPages
{
    public const string Home = "home";
    public const string Projects = "projects";
    public const string Settings = "settings";

    public static IReadOnlyList<AppPage> All { get; } =
    [
        new(Home, "Home", () => new HomePage()),
        new(Projects, "Projects", () => new ProjectsPage()),
        new(Settings, "Settings", () => new SettingsPage()),
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
}
