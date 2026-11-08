using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.Navigation;
using ArcStarterApp.Services;
using ArcStarterApp.ViewModels;
using ArcStarterApp.Views;

namespace ArcStarterApp.Tests;

public class ProjectsTests
{
    [AvaloniaFact]
    public void Every_page_opens()
    {
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        var host = window.GetVisualDescendants().OfType<PageHost>().Single();

        foreach (var page in AppPages.All)
        {
            viewModel.SelectedKey = page.Key;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(page.Key, host.Current?.Key);
            Assert.NotNull(host.Child);
        }
    }

    [AvaloniaFact]
    public void Board_changes_are_kept()
    {
        var projects = App.Services.GetRequiredService<IProjectService>();
        var board = App.Services.GetRequiredService<BoardPageViewModel>();
        var card = board.Cards[0];

        board.Cards.Remove(card);
        board.Save();

        Assert.DoesNotContain(card, projects.GetCards());
    }
}
