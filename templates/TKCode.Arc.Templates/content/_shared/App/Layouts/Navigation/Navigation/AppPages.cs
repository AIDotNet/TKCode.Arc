using Avalonia.Controls;
#if (UsesI18n)
using ArcStarterApp.Localization;
#endif
using ArcStarterApp.Pages;
#if (UsesDi)
using Microsoft.Extensions.DependencyInjection;
#endif
#if (UsesMvvm)
using ArcStarterApp.ViewModels;
#endif

namespace ArcStarterApp.Navigation;

#if (UsesI18n)
/// <summary>
/// One page of the app: a stable key (what the navigation selects by), the title the top bar shows (a text of
/// Localization/, read in the current language) and how to create the view.
/// </summary>
public sealed record AppPage(string Key, AppLangResourceKind TitleText, Func<Control> Create)
{
    /// <summary>The title in the current language.</summary>
    public string Title => AppLangResources.Get(TitleText);
}
#else
/// <summary>One page of the app: a stable key (what the navigation selects by), the title the top bar shows and how to create the view.</summary>
public sealed record AppPage(string Key, string Title, Func<Control> Create);
#endif

/// <summary>
/// The pages of the app. The navigation (the sidebar in Views/MainView.axaml) names them by <see cref="AppPage.Key"/>;
/// to add a page, create its view in Pages/ (dotnet new tkcode-arc-page), add it here and add a row with the same key to
/// the navigation.
#if (UsesMvvm)
/// Each entry also pairs the view with its view model: written out here, not looked up by type name, so the app has no
/// reflection and can be trimmed.
#endif
/// </summary>
public static class AppPages
{
    public const string Home = "home";
    public const string Projects = "projects";
    public const string Settings = "settings";

    public static IReadOnlyList<AppPage> All { get; } =
    [
        new(Home, @@kind:Home@@, () => new HomePage()),
#if (UsesDi)
        new(Projects, @@kind:Projects@@, () => new ProjectsPage { DataContext = App.Services.GetRequiredService<ProjectsPageViewModel>() }),
        new(Settings, @@kind:Settings@@, () => new SettingsPage { DataContext = App.Services.GetRequiredService<SettingsPageViewModel>() }),
#elif (UsesMvvm)
        new(Projects, @@kind:Projects@@, () => new ProjectsPage { DataContext = new ProjectsPageViewModel() }),
        new(Settings, @@kind:Settings@@, () => new SettingsPage { DataContext = new SettingsPageViewModel() }),
#else
        new(Projects, @@kind:Projects@@, () => new ProjectsPage()),
        new(Settings, @@kind:Settings@@, () => new SettingsPage()),
#endif
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
