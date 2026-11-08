using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using Avalonia.Threading;
using Avalonia.VisualTree;
#if (UsesDi)
using Microsoft.Extensions.DependencyInjection;
#endif
#if (HasNavigation && !IsScenario)
using ArcStarterApp.Navigation;
#endif
#if (UsesMvvm)
using ArcStarterApp.ViewModels;
#endif
using ArcStarterApp.Views;
using TKCode.Arc;

namespace ArcStarterApp.Tests;

public class MainViewTests
{
    private static MainWindow ShowMainWindow()
    {
#if (UsesDi)
        var window = new MainWindow { DataContext = App.Services.GetRequiredService<MainViewModel>() };
#elif (UsesMvvm)
        var window = new MainWindow { DataContext = new MainViewModel() };
#else
        var window = new MainWindow();
#endif
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Main_window_shows_the_main_view()
    {
        var window = ShowMainWindow();

        var view = window.GetVisualDescendants().OfType<MainView>().SingleOrDefault();
        Assert.NotNull(view);
        Assert.NotEmpty(view.GetVisualChildren());
    }
#if (HasNavigation && !IsScenario)

    [AvaloniaFact]
    public void Every_page_opens()
    {
        var window = ShowMainWindow();
        var host = window.GetVisualDescendants().OfType<PageHost>().Single();

        foreach (var page in AppPages.All)
        {
            host.Navigate(page.Key);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(page.Key, host.Current?.Key);
            Assert.NotNull(host.Child);
        }
    }
#endif
#if (UsesMvvm && HasNavigation && !IsScenario)

    [AvaloniaFact]
    public void New_project_adds_a_row()
    {
#if (UsesDi)
        var viewModel = App.Services.GetRequiredService<ProjectsPageViewModel>();
#else
        var viewModel = new ProjectsPageViewModel();
#endif
        var before = viewModel.Projects.Count;

        viewModel.AddProjectCommand.Execute(null);

        Assert.Equal(before + 1, viewModel.Projects.Count);
    }
#endif
#if (UsesSettings)

    [AvaloniaFact]
    public void A_theme_change_is_saved()
    {
        ShowMainWindow();

        ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("\"Dark\"", TestApp.Settings.Json);
    }
#endif
}
