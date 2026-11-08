using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ArcStarterApp.Pages;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Navigation;

/// <summary>One page of the app: a stable key (what the sidebar selects by), its title and how to create the view.</summary>
public sealed record AppPage(string Key, string Title, Func<Control> Create);

/// <summary>
/// The pages of the app. To add one, create its view in Pages/ (dotnet new tkcode-arc-page --view-model), add it here
/// and add a sidebar row with the same key (Views/MainView.axaml). Each entry pairs the view with its view model, made
/// by the container: written out, not looked up by type name, so the app has no reflection and can be trimmed.
/// </summary>
public static class AppPages
{
    public const string Board = "board";
    public const string Timeline = "timeline";
    public const string Calendar = "calendar";
    public const string Release = "release";

    public static IReadOnlyList<AppPage> All { get; } =
    [
        new(Board, "Board", () => new BoardPage { DataContext = Get<BoardPageViewModel>() }),
        new(Timeline, "Timeline", () => new TimelinePage { DataContext = Get<TimelinePageViewModel>() }),
        new(Calendar, "Calendar", () => new CalendarPage { DataContext = Get<CalendarPageViewModel>() }),
        new(Release, "Release", () => new ReleasePage { DataContext = Get<ReleasePageViewModel>() }),
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
