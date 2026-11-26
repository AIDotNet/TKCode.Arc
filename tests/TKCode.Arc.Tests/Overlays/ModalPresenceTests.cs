using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        // A plain window: the blur goes on the content of its layer manager (Arc's base Window theme), under the overlay.
        var window = new Window { Content = anchor, Width = 400, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var modal = new ModalPresence(new Border { Width = 100, Height = 100 }, anchor) { BackdropBlur = 7 };
            modal.Open();
            Dispatcher.UIThread.RunJobs();
            var layers = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.VisualLayerManager>().Single(m => m.Name == "PART_VisualLayerManager");
            layers.Child!.GetVisualDescendants().ShouldContain(anchor);
            // A blurred snapshot at the bottom of the modal's layer, not an Effect on the content.
            layers.Child!.Effect.ShouldBeNull();
            var layer = (Panel)modal.Host.GetVisualParent()!;
            var backdrop = layer.Children[0].ShouldBeOfType<ModalBackdrop>();
            backdrop.Radius.ShouldBe(7, 0.01);
            backdrop.Opacity.ShouldBe(1, 0.01);
            backdrop.IsHitTestVisible.ShouldBeFalse();
            backdrop.ContentSize.ShouldBe(layers.Child.Bounds.Size);
            modal.Close();
            Dispatcher.UIThread.RunJobs();
            layer.Children.OfType<ModalBackdrop>().ShouldBeEmpty();
            layers.Child.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backdrop_Is_Taken_Again_When_The_Content_Is_Resized()
    {
        var anchor = new Border();
        var window = new Window { Content = anchor, Width = 400, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var modal = new ModalPresence(new Border { Width = 100, Height = 100 }, anchor) { BackdropBlur = 7 };
            modal.Open();
            Dispatcher.UIThread.RunJobs();
            var layer = (Panel)modal.Host.GetVisualParent()!;
            var first = layer.Children.OfType<ModalBackdrop>().Single();
            first.ContentSize.Width.ShouldBe(400, 0.5);

            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            var second = layer.Children.OfType<ModalBackdrop>().Single();
            second.ShouldNotBeSameAs(first);
            second.ContentSize.Width.ShouldBe(500, 0.5);
            second.Width.ShouldBe(500, 0.5);

            modal.Close();
            Dispatcher.UIThread.RunJobs();
            layer.Children.OfType<ModalBackdrop>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stacked_Modals_Share_One_Backdrop_Blurred_By_The_Strongest()
    {
        var anchor = new Border();
        var window = new Window { Content = anchor, Width = 400, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var drawer = new ModalPresence(new Border { Width = 100, Height = 100 }, anchor) { BackdropBlur = 4 };
            drawer.Open();
            Dispatcher.UIThread.RunJobs();
            var layer = (Panel)drawer.Host.GetVisualParent()!;
            layer.Children.OfType<ModalBackdrop>().Single().Radius.ShouldBe(4, 0.01);

            var dialog = new ModalPresence(new Border { Width = 50, Height = 50 }, anchor) { BackdropBlur = 7 };
            dialog.Open();
            Dispatcher.UIThread.RunJobs();
            var backdrop = layer.Children.OfType<ModalBackdrop>().Single();
            backdrop.Radius.ShouldBe(7, 0.01);
            layer.Children[0].ShouldBeSameAs(backdrop);

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            // Still blurred for the drawer: 4 of the 7 the snapshot was blurred with.
            layer.Children.OfType<ModalBackdrop>().Single().Opacity.ShouldBe(4.0 / 7, 0.01);

            drawer.Close();
            Dispatcher.UIThread.RunJobs();
            layer.Children.OfType<ModalBackdrop>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Overlay_Host_Bounds_The_Modal_And_Skips_The_Backdrop_Blur()
    {
        var anchor = new Border();
        var sibling = new TextBlock { Text = "Stage" };
        var host = new ArcOverlayHost { Width = 300, Height = 200, Children = { sibling, anchor } };
        var window = TestHelpers.Show(new StackPanel { Children = { host } });
        try
        {
            ArcOverlayHost.FindHost(anchor).ShouldBe(host);
            ArcOverlayLayer.Find(anchor).ShouldBe(host.Layer);
            ArcOverlayLayer.Find(host).ShouldBe(OverlayLayer.GetOverlayLayer(host));
            host.GetVisualChildren().Last().ShouldBe(host.Layer);

            var modal = new ModalPresence(new Border { Width = 100, Height = 100 }, anchor) { BackdropBlur = 7 };
            modal.Open().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            modal.Host.GetVisualParent().ShouldBe(host.Layer);
            modal.LayerSize.ShouldBe(new Size(300, 200));
            modal.Host.Bounds.Size.ShouldBe(new Size(300, 200));
            host.Layer.Bounds.ShouldBe(new Rect(0, 0, 300, 200));
            sibling.Effect.ShouldBeNull();
            anchor.Effect.ShouldBeNull();

            // A child added after the layer still renders below it.
            var late = new Border();
            host.Children.Add(late);
            host.GetVisualChildren().Last().ShouldBe(host.Layer);

            modal.Close();
            Dispatcher.UIThread.RunJobs();
            modal.Dispose();
            host.Layer.Children.Count.ShouldBe(0);
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
