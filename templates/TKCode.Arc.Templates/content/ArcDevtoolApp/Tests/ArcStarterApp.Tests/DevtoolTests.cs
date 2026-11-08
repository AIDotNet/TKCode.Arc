using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.Models;
using ArcStarterApp.SampleData;
using ArcStarterApp.ViewModels;
using ArcStarterApp.Views;
using TKCode.Arc;

namespace ArcStarterApp.Tests;

public class DevtoolTests
{
    [AvaloniaFact]
    public void Opening_a_file_adds_an_editor_tab()
    {
        var window = new MainWindow { DataContext = App.Services.GetRequiredService<MainViewModel>() };
        window.Show();
        var view = window.GetVisualDescendants().OfType<MainView>().Single();
        var dock = window.GetVisualDescendants().OfType<DockLayout>().Single();

        view.OpenFile("src/Parser.cs");
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(dock.Workspace.FindPane("doc:src/Parser.cs"));
    }

    [AvaloniaFact]
    public async Task Run_reports_the_warning_and_saves_the_edit()
    {
        SampleBuildService.Pace = TimeSpan.Zero;
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        var document = viewModel.Document("src/Parser.cs");
        document.Text += "\n// edited";
        Assert.True(document.IsModified);

        await viewModel.RunCommand.ExecuteAsync(null);

        Assert.False(document.IsModified);
        Assert.EndsWith("// edited", viewModel.Workspace.Read("src/Parser.cs"));
        var problem = Assert.Single(viewModel.Problems);
        Assert.Equal(ProblemSeverity.Warning, problem.Severity);
        Assert.StartsWith("Build succeeded", viewModel.Output[^1]);
    }
}
