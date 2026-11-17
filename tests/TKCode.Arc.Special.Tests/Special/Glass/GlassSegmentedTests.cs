using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Glass;

/// <summary>
/// The glass segmented control: the lens follows the selection (stretching at full motion), the pointer taps and drags it,
/// the keyboard model wraps and skips disabled segments, and the automation tree is a tab list.
/// </summary>
public class GlassSegmentedTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static GlassSegmented Control(bool disableThird = false) => new()
    {
        Items =
        {
            new GlassSegment { Content = "Day" },
            new GlassSegment { Content = "Week" },
            new GlassSegment { Content = "Month", IsEnabled = !disableThird },
            new GlassSegment { Content = "Year" },
        }
    };

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Center(Visual target, Window window) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;

    private static GlassSegment Segment(GlassSegmented control, int index) => (GlassSegment)control.ContainerFromIndex(index)!;

    private static Rect SegmentRect(GlassSegmented control, int index)
    {
        var segment = Segment(control, index);
        return new Rect(segment.TranslatePoint(default, control.Lens!)!.Value, segment.Bounds.Size);
    }

    [AvaloniaFact]
    public void Always_has_a_selection_and_the_lens_sits_on_it()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        control.SelectedIndex.ShouldBe(0);
        Segment(control, 0).IsSelected.ShouldBeTrue();
        Segment(control, 0).Classes.ShouldContain(":selected");
        control.Lens.ShouldNotBeNull();
        control.Lens.HasTarget.ShouldBeTrue();
        control.Lens.CurrentRect.ShouldBe(SegmentRect(control, 0));
        control.Lens.CurrentOpacity.ShouldBe(1);
        Segment(control, 0).IsUnderLens.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Clicking_a_segment_selects_it_and_moves_the_lens()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        var point = Center(Segment(control, 2), window);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
        control.SelectedIndex.ShouldBe(2);
        control.Lens!.CurrentRect.ShouldBe(SegmentRect(control, 2));
        Segment(control, 2).IsUnderLens.ShouldBeTrue();
        Segment(control, 0).IsUnderLens.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Dragging_the_lens_selects_the_segment_it_is_released_over()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        var start = Center(Segment(control, 0), window);
        var end = Center(Segment(control, 3), window);
        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left);
        var middle = new Point((start.X + end.X) / 2, start.Y);
        window.MouseMove(middle, RawInputModifiers.LeftMouseButton);
        Pump();
        control.Track.IsDragging.ShouldBeTrue();
        // The lens is centred under the pointer while it is dragged.
        var lensCenter = control.Lens!.CurrentRect.Center.X;
        lensCenter.ShouldBe(window.TranslatePoint(middle, control.Lens)!.Value.X, 0.5);
        control.SelectedIndex.ShouldBe(0);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Pump();
        control.Track.IsDragging.ShouldBeFalse();
        control.SelectedIndex.ShouldBe(3);
        control.Lens.CurrentRect.ShouldBe(SegmentRect(control, 3));
        window.Close();
    }

    [AvaloniaFact]
    public void A_press_off_the_selection_that_slides_away_selects_nothing()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        var start = Center(Segment(control, 2), window);
        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left);
        var away = Center(Segment(control, 3), window);
        window.MouseMove(away, RawInputModifiers.LeftMouseButton);
        window.MouseUp(away, MouseButton.Left);
        Pump();
        control.SelectedIndex.ShouldBe(0);
        control.Track.IsDragging.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Arrows_wrap_skip_disabled_segments_and_move_the_focus()
    {
        var control = Control(disableThird: true);
        var window = TestHelpers.Show(control);
        Pump();
        Segment(control, 0).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(1);
        Segment(control, 1).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(3);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(3);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        control.SelectedIndex.ShouldBe(3);
        Pump();
        Segment(control, 3).IsFocusRingShown.ShouldBeTrue();
        // Only the selected segment is the Tab stop.
        KeyboardNavigation.GetTabOnceActiveElement(control).ShouldBe(Segment(control, 3));
        window.Close();
    }

    [AvaloniaFact]
    public void At_full_motion_the_leading_edge_runs_ahead_so_the_lens_stretches()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var control = Control();
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        Pump();
        var from = SegmentRect(control, 0);
        var to = SegmentRect(control, 3);
        control.SelectedIndex = 3;
        clock.Advance(TimeSpan.FromMilliseconds(80), Frame);
        var lens = control.Lens!.CurrentRect;
        var leading = (lens.Right - from.Right) / (to.Right - from.Right);
        var trailing = (lens.Left - from.Left) / (to.Left - from.Left);
        leading.ShouldBeGreaterThan(trailing);
        lens.Width.ShouldBeGreaterThan(Math.Max(from.Width, to.Width));
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        control.Lens.CurrentRect.Left.ShouldBe(to.Left, 0.5);
        control.Lens.CurrentRect.Right.ShouldBe(to.Right, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_fades_the_lens_to_the_new_segment_without_gliding()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var control = Control();
        var window = TestHelpers.Show(control, ArcMotionLevel.Reduced);
        Pump();
        var to = SegmentRect(control, 2);
        control.SelectedIndex = 2;
        clock.Advance(TimeSpan.FromMilliseconds(30), Frame);
        control.Lens!.CurrentOpacity.ShouldBeLessThan(1);
        // The fade out finishes, the lens lands, then it fades back in (each step continues on the dispatcher).
        for (var i = 0; i < 4; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            Dispatcher.UIThread.RunJobs();
        }
        control.Lens.CurrentRect.ShouldBe(to);
        control.Lens.CurrentOpacity.ShouldBe(1, 0.001);
        window.Close();
    }

    [AvaloniaFact]
    public void Items_source_values_become_segments()
    {
        var control = new GlassSegmented { ItemsSource = new[] { "0.5×", "1×", "2×" } };
        var window = TestHelpers.Show(control);
        Pump();
        control.ContainerFromIndex(1).ShouldBeOfType<GlassSegment>().Content.ShouldBe("1×");
        control.SelectedItem.ShouldBe("0.5×");
        window.Close();
    }

    [AvaloniaFact]
    public void Automation_is_a_tab_list_of_selectable_tabs()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        ControlAutomationPeer.CreatePeerForElement(control).GetAutomationControlType().ShouldBe(AutomationControlType.Tab);
        var peer = ControlAutomationPeer.CreatePeerForElement(Segment(control, 1));
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
        peer.GetName().ShouldBe("Week");
        var selection = peer.GetProvider<ISelectionItemProvider>()!;
        selection.IsSelected.ShouldBeFalse();
        selection.Select();
        control.SelectedIndex.ShouldBe(1);
        selection.IsSelected.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Track_and_lens_are_glass_and_turn_opaque_under_reduce_transparency()
    {
        var control = Control();
        var window = TestHelpers.Show(control);
        Pump();
        control.Pane.ShouldNotBeNull();
        control.Pane.IsSolid.ShouldBeFalse();
        control.Lens!.ReduceTransparency.ShouldBeFalse();
        control.Pane.ReduceTransparency = true;
        control.Lens.ReduceTransparency = true;
        control.Pane.IsSolid.ShouldBeTrue();
        control.Pane.IsFrosted.ShouldBeFalse();
        window.Close();
    }
}
