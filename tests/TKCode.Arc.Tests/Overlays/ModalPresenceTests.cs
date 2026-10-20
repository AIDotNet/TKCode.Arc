using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class ModalPresenceTests
{
    [AvaloniaFact]
    public void Exit_Plays_Before_Removal_And_A_Reopen_Reverses_Mid_Exit()
    {
        var anchor = new Border();
        var window = TestHelpers.Show(anchor, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var panel = new Border { Width = 200, Height = 100, Background = Avalonia.Media.Brushes.White };
            var modal = new ModalPresence(panel, anchor) { Entrance = ModalEntrance.Rise };
            modal.Open().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            modal.State.ShouldBe(ModalState.Opening);
            modal.Host.Scrim.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            modal.State.ShouldBe(ModalState.Open);
            modal.Host.Scrim.Opacity.ShouldBe(1, 0.001);

            modal.Close();
            modal.State.ShouldBe(ModalState.Closing);
            modal.Host.IsHitTestVisible.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(64));
            Dispatcher.UIThread.RunJobs();
            modal.Host.GetVisualParent().ShouldNotBeNull();
            var midExit = modal.Host.PanelHost.Opacity;
            midExit.ShouldBeGreaterThan(0);
            midExit.ShouldBeLessThan(1);

            modal.Open().ShouldBeTrue();
            modal.State.ShouldBe(ModalState.Opening);
            modal.Host.IsHitTestVisible.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            modal.State.ShouldBe(ModalState.Open);
            modal.Host.PanelHost.Opacity.ShouldBe(1, 0.001);

            var closed = 0;
            modal.Closed += (_, _) => closed++;
            modal.Close();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            modal.State.ShouldBe(ModalState.Closed);
            modal.Host.GetVisualParent().ShouldBeNull();
            closed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backdrop_Blur_Follows_The_Scrim_And_Respects_Reduce_Transparency()
    {
        var anchor = new Border();
        var window = TestHelpers.Show(anchor);
        try
        {
            var modal = new ModalPresence(new Border { Width = 100, Height = 100 }, anchor) { BackdropBlur = 7 };
            modal.Open();
            Dispatcher.UIThread.RunJobs();
            var layers = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.VisualLayerManager>().First(m => ReferenceEquals(m.Child, anchor));
            layers.Child!.Effect.ShouldBeOfType<Avalonia.Media.BlurEffect>().Radius.ShouldBe(7, 0.01);
            modal.Close();
            Dispatcher.UIThread.RunJobs();
            layers.Child.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Is_Trapped_In_The_Topmost_Modal()
    {
        var outside = new TextBox();
        var window = TestHelpers.Show(new StackPanel { Children = { outside } });
        try
        {
            outside.Focus();
            var inside = new TextBox();
            var modal = new ModalPresence(new StackPanel { Width = 100, Children = { inside } }, outside);
            modal.Open();
            Dispatcher.UIThread.RunJobs();
            inside.IsFocused.ShouldBeTrue();
            outside.Focus();
            outside.IsFocused.ShouldBeFalse();
            inside.IsFocused.ShouldBeTrue();
            modal.Close();
            Dispatcher.UIThread.RunJobs();
            outside.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_Requests_Can_Be_Cancelled()
    {
        var anchor = new Border();
        var window = TestHelpers.Show(anchor);
        try
        {
            var modal = new ModalPresence(new Border { Width = 50, Height = 50 }, anchor);
            modal.Dismissing += (_, e) => e.Cancel = e.Reason == DismissReason.PressOutside;
            modal.Open();
            modal.Dismiss(DismissReason.PressOutside).ShouldBeFalse();
            modal.IsOpen.ShouldBeTrue();
            modal.Dismiss(DismissReason.Escape).ShouldBeTrue();
            modal.IsOpen.ShouldBeFalse();
            modal.CloseReason.ShouldBe(DismissReason.Escape);
        }
        finally
        {
            window.Close();
        }
    }
}
