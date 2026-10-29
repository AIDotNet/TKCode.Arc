using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// The real app detaches a block while its own posted jobs are still queued (a theme switch, a page change, a window that
/// closes): a job that reads tokens from a control without a theme context throws on the dispatcher and ends the app
/// ("The owner has no ThemeContext"). Each scenario below leaves with a job pending and then runs the queue.
/// </summary>
public class ShowpiecesLifecycleTests
{
    private static readonly (string Name, Func<Control> Create)[] s_blocks =
    [
        ("ExplodedView", () => new ExplodedViewPreviewDemo()),
        ("LayoutMorph", () => new LayoutMorphPreviewDemo()),
        ("CustomerGalaxy", () => new CustomerGalaxyPreviewDemo()),
        ("ActivityTerrain", () => new ActivityTerrainPreviewDemo()),
        ("RevenueGlobe", () => new RevenueGlobePreviewDemo()),
    ];

    private static Task ApplyWithoutRunningJobs(Action<ArcThemeBuilder>? configure)
    {
        ArcTheme.Manager.Apply(configure is null ? ArcThemeState.Default : ArcThemeState.Default.With(configure));
        return Task.CompletedTask;
    }

    private static void RunQueue()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs the queue; a job that throws is recorded under the block's name instead of ending the scenario.</summary>
    private static void RunQueue(string block, List<string> failures)
    {
        try
        {
            RunQueue();
        }
        catch (Exception exception)
        {
            failures.Add($"{block}: {exception.GetType().Name}: {exception.Message} at {exception.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("TKCode.Arc"))?.Trim()}");
        }
    }

    [AvaloniaFact]
    public void A_Block_That_Is_Shown_And_Closed_At_Once_Leaves_No_Failing_Job()
    {
        var failures = new List<string>();
        foreach (var (name, create) in s_blocks)
        {
            var window = new Window { Width = 900, Height = 700, Content = create() };
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            window.Show();
            window.Close();
            RunQueue(name, failures);
        }
        failures.ShouldBeEmpty(string.Join("\n", failures));
    }

    [AvaloniaFact]
    public async Task A_Theme_Switch_Followed_By_Leaving_Leaves_No_Failing_Job()
    {
        var failures = new List<string>();
        foreach (var (name, create) in s_blocks)
        {
            var window = TestHelpers.Show(create(), ArcMotionLevel.Full);
            try
            {
                window.Width = 900;
                window.Height = 700;
                RunQueue();
                await ApplyWithoutRunningJobs(TestHelpers.Dark);
                window.Content = null;
                window.Close();
                RunQueue(name, failures);
            }
            finally
            {
                window.Close();
                await ApplyWithoutRunningJobs(null);
                try
                {
                    Dispatcher.UIThread.RunJobs();
                }
                catch (Exception)
                {
                    // Already recorded by the scenario above.
                }
            }
        }
        failures.ShouldBeEmpty(string.Join("\n", failures));
    }

    [AvaloniaFact]
    public async Task A_Theme_Switch_While_The_Block_Stays_Leaves_No_Failing_Job()
    {
        var failures = new List<string>();
        foreach (var (name, create) in s_blocks)
        {
            var window = TestHelpers.Show(create(), ArcMotionLevel.Full);
            try
            {
                window.Width = 900;
                window.Height = 700;
                RunQueue();
                await ApplyWithoutRunningJobs(TestHelpers.Dark);
                RunQueue(name, failures);
                await ApplyWithoutRunningJobs(null);
                RunQueue(name, failures);
            }
            finally
            {
                window.Close();
                try
                {
                    Dispatcher.UIThread.RunJobs();
                }
                catch (Exception)
                {
                    // Already recorded by the scenario above.
                }
            }
        }
        failures.ShouldBeEmpty(string.Join("\n", failures));
    }
}
