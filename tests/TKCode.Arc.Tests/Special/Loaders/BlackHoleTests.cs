using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>The black hole control: queue, hiding, motion levels, events, origin, and the preview mailbox.</summary>
public class BlackHoleTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (BlackHole Hole, StackPanel Rows, Border[] Items) Build()
    {
        var items = Enumerable.Range(0, 4).Select(i => new Border { Height = 60, Background = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 4) }).ToArray();
        var rows = new StackPanel();
        foreach (var item in items)
        {
            rows.Children.Add(item);
        }
        var hole = new BlackHole { Content = rows, Width = 400, Height = 280, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        return (hole, rows, items);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public void Defaults_match_the_measured_preview()
    {
        var hole = new BlackHole();
        hole.Duration.ShouldBe(TimeSpan.FromSeconds(1.3));
        hole.ReleaseSpeed.ShouldBe(0.92);
        hole.HoleRadius.ShouldBe(5.5);
        hole.ShockwaveRadius.ShouldBe(90);
        hole.IsRunning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task With_motion_off_items_hide_and_return_at_once()
    {
        var (hole, _, items) = Build();
        var window = TestHelpers.Show(hole);
        var events = new List<string>();
        hole.TransitionStarted += (_, e) => events.Add($"start {e.Direction} {e.Count}");
        hole.TransitionCompleted += (_, e) => events.Add($"end {e.Direction} {e.Count}");
        var swallow = hole.SwallowAsync([items[1], items[2]]);
        swallow.IsCompletedSuccessfully.ShouldBeTrue();
        items[1].Opacity.ShouldBe(0);
        items[2].Opacity.ShouldBe(0);
        items[0].Opacity.ShouldBe(1);
        hole.IsRunning.ShouldBeFalse();
        await hole.ReleaseAsync([items[1], items[2]]);
        items[1].Opacity.ShouldBe(1);
        items[2].Opacity.ShouldBe(1);
        events.ShouldBe(["start Swallow 2", "end Swallow 2", "start Release 2", "end Release 2"]);
        window.Close();
    }

    [AvaloniaFact]
    public void Targets_outside_the_content_are_ignored()
    {
        var (hole, _, _) = Build();
        var window = TestHelpers.Show(hole);
        var started = 0;
        hole.TransitionStarted += (_, _) => started++;
        var stranger = new Border();
        hole.SwallowAsync([stranger]).IsCompletedSuccessfully.ShouldBeTrue();
        started.ShouldBe(0);
        stranger.Opacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_fades_in_two_hundred_milliseconds()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (hole, _, items) = Build();
        var window = TestHelpers.Show(hole, ArcMotionLevel.Reduced);
        var task = hole.SwallowAsync([items[0]]);
        hole.IsRunning.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        items[0].Opacity.ShouldBe(0.5, 0.1);
        task.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(150), Frame);
        task.IsCompletedSuccessfully.ShouldBeTrue();
        items[0].Opacity.ShouldBe(0);
        hole.IsRunning.ShouldBeFalse();

        // Release: hidden at once, a few frames to settle, then a 200 ms fade back in.
        var release = hole.ReleaseAsync([items[0]]);
        items[0].Opacity.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        release.IsCompletedSuccessfully.ShouldBeTrue();
        items[0].Opacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Full_motion_runs_the_effect_for_the_duration_and_restores_the_content()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (hole, _, items) = Build();
        var window = TestHelpers.Show(hole, ArcMotionLevel.Full);
        Pump();
        var task = hole.SwallowAsync([items[1], items[3]]);
        hole.IsRunning.ShouldBeTrue();
        if (hole.Renderer is { } renderer)
        {
            // The hole opens at the centre of the two items (y 64…124 and 192…252).
            renderer.Center.X.ShouldBe(200, 0.5);
            renderer.Center.Y.ShouldBe(158, 0.5);
            renderer.VertexCount.ShouldBeGreaterThan(0);
            hole.Stage!.IsVisible.ShouldBeTrue();
            hole.ContentHost!.Opacity.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(1000), Frame);
            task.IsCompleted.ShouldBeFalse();
            hole.Stage.Frames.ShouldBeGreaterThan(50);
            // Frames after the first are painted on the worker thread, one in flight.
            hole.Worker.ShouldNotBeNull();
            hole.Worker.Painted.ShouldBeGreaterThan(50);
        }
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        task.IsCompletedSuccessfully.ShouldBeTrue();
        hole.IsRunning.ShouldBeFalse();
        hole.Stage!.IsVisible.ShouldBeFalse();
        hole.ContentHost!.Opacity.ShouldBe(1);
        hole.Renderer.ShouldBeNull();
        hole.Worker.ShouldBeNull();
        items[1].Opacity.ShouldBe(0);
        items[3].Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_release_asked_for_during_a_swallow_waits_its_turn()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (hole, _, items) = Build();
        var window = TestHelpers.Show(hole, ArcMotionLevel.Reduced);
        var order = new List<string>();
        hole.TransitionStarted += (_, e) => order.Add($"start {e.Direction}");
        hole.TransitionCompleted += (_, e) => order.Add($"end {e.Direction}");
        var swallow = hole.SwallowAsync([items[0]]);
        var release = hole.ReleaseAsync([items[2]]);
        hole.PendingTransitions.ShouldBe(2);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        order.ShouldBe(["start Swallow"]);
        clock.Advance(TimeSpan.FromMilliseconds(150), Frame);
        swallow.IsCompletedSuccessfully.ShouldBeTrue();
        hole.IsRunning.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
        release.IsCompletedSuccessfully.ShouldBeTrue();
        order.ShouldBe(["start Swallow", "end Swallow", "start Release", "end Release"]);
        hole.IsRunning.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Leaving_the_tree_ends_the_run()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (hole, _, items) = Build();
        var host = new Panel { Children = { hole } };
        var window = TestHelpers.Show(host, ArcMotionLevel.Reduced);
        var task = hole.SwallowAsync([items[0]]);
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        task.IsCompleted.ShouldBeFalse();
        host.Children.Remove(hole);
        task.IsCompletedSuccessfully.ShouldBeTrue();
        items[0].Opacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void The_mailbox_selects_deletes_and_undoes_by_keyboard()
    {
        var mailbox = new BlackHoleMailbox();
        var window = TestHelpers.Show(mailbox);
        var rows = mailbox.GetVisualDescendantsOf<BlackHoleMailboxRow>().ToList();
        rows.Count.ShouldBe(5);
        rows.Select(r => r.IsSelected).ShouldBe([false, true, false, true, false]);
        AutomationName(mailbox.DeleteButton).ShouldBe("Delete 2");

        // Space toggles the focused row.
        rows[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        rows[0].IsSelected.ShouldBeTrue();
        AutomationName(mailbox.DeleteButton).ShouldBe("Delete 3");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rows[0].IsSelected.ShouldBeFalse();

        // Delete on a row deletes the selection (motion off: at once).
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Pump();
        mailbox.GetVisualDescendantsOf<BlackHoleMailboxRow>().Count().ShouldBe(3);
        var status = mailbox.GetVisualDescendantsOf<TextBlock>().First(t => t.Text?.Contains("removed") == true);
        status.Text.ShouldBe("2 messages removed");
        mailbox.DeleteButton.IsEffectivelyVisible.ShouldBeFalse();

        // Undo puts them back at their places, unselected and visible.
        _ = mailbox.UndoAsync();
        Pump();
        var back = mailbox.GetVisualDescendantsOf<BlackHoleMailboxRow>().ToList();
        back.Count.ShouldBe(5);
        back.ShouldBe(rows);
        back.All(r => !r.IsSelected && r.Opacity == 1).ShouldBeTrue();
        window.Close();
    }

    private static string? AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control);
}

internal static class BlackHoleTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOf<T>(this Visual root) where T : Visual =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
