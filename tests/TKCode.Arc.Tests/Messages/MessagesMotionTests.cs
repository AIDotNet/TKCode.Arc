using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

/// <summary>Runs the animated paths frame by frame under full motion (ManualFrameClock).</summary>
public class MessagesMotionTests
{
    private static void Run(Window window, ManualFrameClock clock, double seconds)
    {
        var steps = (int)Math.Ceiling(seconds / 0.05);
        for (var i = 0; i < steps; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Pump(window);
        }
    }

    /// <summary>Motion completions hop through the thread pool back to the UI thread; give them a moment.</summary>
    private static void Pump(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Text_Swap_Keeps_The_Old_Copy_Until_It_Has_Left()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var swap = new MessagesTextSwap { Text = "Saving", Width = 200 };
        var window = TestHelpers.Show(new StackPanel { Children = { swap } }, ArcMotionLevel.Full);
        try
        {
            swap.Text = "Saved";
            Dispatcher.UIThread.RunJobs();
            swap.ExitingCount.ShouldBe(1);
            swap.CurrentBlock!.Text.ShouldBe("Saved");
            swap.CurrentBlock.Opacity.ShouldBeLessThan(0.5);
            Run(window, clock, 0.5);
            swap.ExitingCount.ShouldBe(0);
            swap.CurrentBlock.Opacity.ShouldBe(1);
            swap.CurrentBlock.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Height_And_Chip_Width_Spring_To_New_Content()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var alert = new Alert { Title = "Ready", Content = "Short.", Width = 360 };
        var card = new MetricCard { Label = "Orders", Change = "+1%", Width = 300 };
        var window = TestHelpers.Show(new StackPanel { Children = { alert, card } }, ArcMotionLevel.Full);
        try
        {
            Run(window, clock, 0.5);
            var copy = TestHelpers.Descendant<SpringSizeDecorator>(alert, "PART_CopyFrame");
            var chip = card.GetVisualDescendants().OfType<SpringSizeDecorator>().First(d => d.Child is MessagesTextSwap { Name: "PART_Change" });
            var height = copy.Bounds.Height;
            var width = chip.Bounds.Width;
            alert.Content = "A much longer description that wraps onto a second line for sure, and even a third line.";
            card.Change = "+1,234,567%";
            Pump(window);
            // The first frame still shows the old size; the springs then carry it to the new one.
            copy.Bounds.Height.ShouldBe(height, 0.5);
            chip.Bounds.Width.ShouldBe(width, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Pump(window);
            copy.Bounds.Height.ShouldBeGreaterThan(height + 1);
            copy.IsMoving.ShouldBeTrue();
            Run(window, clock, 1.2);
            copy.IsMoving.ShouldBeFalse();
            chip.Bounds.Width.ShouldBeGreaterThan(width * 2);
            // A passive change (narrower window) follows at once.
            alert.Width = 300;
            Pump(window);
            copy.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alert_Dismiss_Collapses_On_A_Spring_Then_Raises_Dismissed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var alert = new Alert { Title = "Payment failed", IsDismissible = true, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(new StackPanel { Children = { alert } }, ArcMotionLevel.Full);
        try
        {
            Run(window, clock, 0.1);
            var height = alert.Bounds.Height;
            var dismissed = 0;
            alert.Dismissed += (_, _) => dismissed++;
            alert.Dismiss();
            Run(window, clock, 0.15);
            dismissed.ShouldBe(0);
            alert.Bounds.Height.ShouldBeLessThan(height);
            alert.Bounds.Height.ShouldBeGreaterThan(0);
            Run(window, clock, 1.2);
            dismissed.ShouldBe(1);
            alert.Bounds.Height.ShouldBe(0, 0.5);
            alert.IsOpen = true;
            Run(window, clock, 1.2);
            alert.Bounds.Height.ShouldBe(height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Rises_In_And_Sinks_Out()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var toast = new Toast { Title = "Changes saved", Duration = TimeSpan.Zero };
        var window = TestHelpers.Show(new StackPanel { Children = { toast } }, ArcMotionLevel.Full);
        try
        {
            Run(window, clock, 1.0);
            toast.IsShowing.ShouldBeTrue();
            var check = TestHelpers.Descendant<TKCode.Arc.Primitives.StrokeDrawGlyph>(toast);
            check.Progress.ShouldBe(1);
            toast.Close();
            Run(window, clock, 0.6);
            window.UpdateLayout();
            toast.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Stack_Enters_Fans_Out_And_Removes_Leaving_Toasts()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var stack = new ToastStack();
        var window = TestHelpers.Show(new Grid { Children = { new Border(), stack } }, ArcMotionLevel.Full);
        window.Width = 800;
        window.Height = 600;
        try
        {
            Run(window, clock, 0.1);
            stack.Show(new ToastOptions { Title = "First" });
            stack.Show(new ToastOptions { Title = "Second", Kind = ToastKind.Loading });
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var items = stack.Panel!.Present;
            // A new toast starts its own height below the edge and rises.
            items[0].LiftY.ShouldBeGreaterThan(0);
            Run(window, clock, 1.2);
            items[0].LiftY.ShouldBe(0, 0.5);
            items[1].LiftY.ShouldBe(-14, 0.5);
            items[1].DepthScale.ShouldBe(0.95, 0.005);

            window.MouseMove(stack.Panel.ListRect.Center);
            Run(window, clock, 1.2);
            stack.IsExpanded.ShouldBeTrue();
            items[1].LiftY.ShouldBe(-(items[0].NaturalHeight + 12), 0.5);

            var leaving = items[0];
            stack.Dismiss(leaving.Id);
            Dispatcher.UIThread.RunJobs();
            stack.Panel.Children.ShouldContain(leaving);
            Run(window, clock, 0.6);
            stack.Panel.Children.ShouldNotContain(leaving);
            stack.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swiping_A_Stacked_Toast_Right_Throws_It_Away()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var stack = new ToastStack();
        var window = TestHelpers.Show(new Grid { Children = { new Border(), stack } }, ArcMotionLevel.Full);
        window.Width = 800;
        window.Height = 600;
        try
        {
            stack.Show(new ToastOptions { Title = "Swipe me" });
            Run(window, clock, 1.2);
            var rect = stack.Panel!.ListRect;
            var start = new Point(rect.X + 120, rect.Center.Y);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0));
            window.MouseMove(start + new Point(80, 0));
            window.MouseMove(start + new Point(200, 0));
            window.MouseUp(start + new Point(200, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.Count.ShouldBe(0);
            Run(window, clock, 0.6);
            stack.Panel.Children.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Announcement_Faces_Swap_And_The_Ring_Advances()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = new AnnouncementBar { Storage = new InMemoryMessageStorage(), IsNavigationVisible = true, Interval = TimeSpan.FromSeconds(6) };
        bar.Messages.Add(new Announcement { Id = "a", Message = "First message" });
        bar.Messages.Add(new Announcement { Id = "b", Message = "Second message, which is a little longer than the first one" });
        var window = TestHelpers.Show(new StackPanel { Children = { bar } }, ArcMotionLevel.Full);
        window.Width = 900;
        try
        {
            Run(window, clock, 0.5);
            bar.IsRunning.ShouldBeTrue();
            var ring = bar.GetVisualDescendants().OfType<AnnouncementBarRing>().First();
            ring.Progress.ShouldBeGreaterThan(0);
            bar.Next();
            Dispatcher.UIThread.RunJobs();
            bar.Viewport!.ExitingCount.ShouldBe(1);
            Run(window, clock, 1.0);
            bar.Viewport.ExitingCount.ShouldBe(0);
            bar.Viewport.CurrentFace!.Announcement!.Id.ShouldBe("b");
            bar.Dismiss();
            Run(window, clock, 1.2);
            bar.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Consent_Surface_Springs_Between_Shapes()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var consent = new CookieConsent { Storage = new InMemoryMessageStorage() };
        var window = TestHelpers.Show(new Grid { Height = 520, Children = { new Border(), consent } }, ArcMotionLevel.Full);
        window.Width = 900;
        window.Height = 600;
        try
        {
            Run(window, clock, 1.0);
            consent.Surface!.Shape.Width.ShouldBe(660, 0.5);
            consent.OpenPreferences();
            Run(window, clock, 0.1);
            consent.Surface.Shape.Width.ShouldBeGreaterThan(420);
            consent.Surface.Shape.Width.ShouldBeLessThan(660);
            Run(window, clock, 1.5);
            consent.Surface.Shape.Width.ShouldBe(420, 0.5);
            consent.Surface.ShapeRadius.ShouldBe(26, 0.1);
            consent.Reset();
            consent.Consent = new Dictionary<string, bool> { ["analytics"] = false };
            Run(window, clock, 1.5);
            consent.Mode.ShouldBe(CookieConsentMode.Chip);
            consent.Surface.Shape.Width.ShouldBe(44, 0.5);
            consent.Surface.ShapeRadius.ShouldBe(22, 0.1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Glyph_Swap_Pops_The_New_Tone_In()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var glyph = new MessagesGlyphSwap { Data = ArcIcons.Info };
        var window = TestHelpers.Show(new StackPanel { Children = { glyph } }, ArcMotionLevel.Full);
        try
        {
            glyph.Data = ArcIcons.Check;
            Dispatcher.UIThread.RunJobs();
            glyph.CurrentLayer!.Opacity.ShouldBeLessThan(1);
            Run(window, clock, 0.8);
            glyph.CurrentLayer.Opacity.ShouldBe(1);
            glyph.GetVisualChildren().Count().ShouldBe(1);
            glyph.IsSpinner = true;
            Run(window, clock, 0.8);
            glyph.CurrentLayer.IsSpinner.ShouldBeTrue();
            glyph.CurrentLayer.IsSpinning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
