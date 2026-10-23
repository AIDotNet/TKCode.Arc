using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>Every Semantic zoom demo loads, lays out and renders in light and dark; the data recipe holds.</summary>
public class SemanticZoomDemosTests
{
    private static IEnumerable<Func<UserControl>> Demos() =>
    [
        () => new SemanticZoomPreviewDemo(),
        () => new SemanticZoomGuidedTourDemo(),
        () => new SemanticZoomSidePanelDemo(),
        () => new SemanticZoomNarrowDemo(),
        () => new SemanticZoomYourDataDemo()
    ];

    [AvaloniaFact]
    public async Task Every_Demo_Renders_In_Light_And_Dark()
    {
        try
        {
            foreach (var dark in new[] { false, true })
            {
                await TestHelpers.ApplyAsync(dark ? new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build() : null);
                foreach (var create in Demos())
                {
                    var demo = create();
                    var window = TestHelpers.Show(demo);
                    try
                    {
                        window.Width = 900;
                        window.Height = 1100;
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                        var map = TestHelpers.Descendant<SemanticZoom>(demo);
                        var stage = map.Stage.ShouldNotBeNull();
                        stage.Layout.TeamCount.ShouldBeGreaterThan(0, demo.GetType().Name);
                        map.Scope.ShouldBe(map.Organization);
                        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(64, 64));
                        using (var context = bitmap.CreateDrawingContext())
                        {
                            stage.Render(context);
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void The_Generated_Data_Follows_The_Recipe()
    {
        var org = SemanticZoomDemoData.Create();
        org.Teams.Select(t => t.Projects.Count).ShouldBe([6, 5, 4, 4, 3]);
        org.TaskCount.ShouldBeInRange(148, 156);
        org.People.Count.ShouldBe(24);
        foreach (var project in org.AllProjects)
        {
            project.Tasks.Count.ShouldBeInRange(4, 10);
            project.BlockedTasks.ShouldBeLessThanOrEqualTo(2);
            project.Health.ShouldBe(project.BlockedTasks switch
            {
                0 => SemanticZoomHealth.OnTrack,
                1 => SemanticZoomHealth.AtRisk,
                _ => SemanticZoomHealth.OffTrack
            });
            project.Tasks.Select(t => t.Title).Distinct().Count().ShouldBe(project.Tasks.Count);
        }
        // Done tasks are due before "today", open ones after it.
        foreach (var task in org.AllTasks)
        {
            if (task.Status == SemanticZoomTaskStatus.Done)
            {
                task.Due!.Value.ShouldBeLessThan(SemanticZoomDemoData.Today);
                task.ChecklistDone.ShouldBe(task.Checklist.Count);
            }
            else
            {
                task.Due!.Value.ShouldBeGreaterThan(SemanticZoomDemoData.Today);
            }
        }
        var done = org.DoneTasks / (double)org.TaskCount;
        done.ShouldBeInRange(0.35, 0.55);
        org.Progress.ShouldBeInRange(0.45, 0.65);
        // The same data on every run.
        SemanticZoomDemoData.Create().AllTasks.Select(t => t.Title).ShouldBe(org.AllTasks.Select(t => t.Title));
    }

    [AvaloniaFact]
    public void The_Data_Demo_Is_Written_In_Xaml()
    {
        var demo = new SemanticZoomYourDataDemo();
        var window = TestHelpers.Show(demo);
        try
        {
            window.Width = 700;
            window.Height = 1000;
            Dispatcher.UIThread.RunJobs();
            var map = TestHelpers.Descendant<SemanticZoom>(demo);
            var org = map.Organization.ShouldNotBeNull();
            org.Teams.Count.ShouldBe(2);
            org.TaskCount.ShouldBe(7);
            org.BlockedTasks.ShouldBe(1);
            org.Teams[0].Projects[0].Due.ShouldBe(new DateOnly(2026, 10, 30));
            org.Teams[0].Projects[0].Tasks[2].Summary.ShouldStartWith("Blocked:");
        }
        finally
        {
            window.Close();
        }
    }
}
