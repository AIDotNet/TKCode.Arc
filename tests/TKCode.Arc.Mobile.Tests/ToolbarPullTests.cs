using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class ToolbarPullTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Part(Visual root, string name)
        => root.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    private static ScrollViewer TallScroller() => new() { Content = new Border { Height = 1500 } };

    private static void Pull(Window window, Point start, double distance)
    {
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 12), RawInputModifiers.LeftMouseButton);
        window.MouseMove(start + new Point(0, distance), RawInputModifiers.LeftMouseButton);
    }

    private static void Release(Window window, Point start, double distance)
    {
        Thread.Sleep(80); // a finger that rests before lifting has no speed
        window.MouseUp(start + new Point(0, distance), MouseButton.Left);
    }

    // Toolbar

    [AvaloniaFact]
    public void Toolbar_Is_44_High_With_A_Hairline_And_Items_Share_The_Width()
    {
        var first = new Button { Content = "Share" };
        var second = new Button { Content = "Edit" };
        var third = new Button { Content = "Delete" };
        var toolbar = new Toolbar { Items = { first, second, third } };
        var window = Show(new Page { Footer = toolbar, Content = new TextBlock { Text = "Body" } });
        try
        {
            toolbar.Template.ShouldNotBeNull();
            toolbar.Bounds.Height.ShouldBe(44);
            ((Border)Part(toolbar, "PART_Bar")).BorderThickness.ShouldBe(new Thickness(0, 1, 0, 0));
            var slots = new[] { first, second, third }.Select(b => b.GetVisualAncestors().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First().Bounds.Width).ToArray();
            slots[1].ShouldBe(slots[0], 0.5);
            slots[2].ShouldBe(slots[0], 0.5);
            first.Bounds.Height.ShouldBeGreaterThanOrEqualTo(43);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Slots_Place_Leading_Center_And_Trailing()
    {
        var leading = new Button { Content = "Cancel" };
        var trailing = new Button { Content = "Send" };
        var toolbar = new Toolbar { Leading = leading, Center = new TextBlock { Text = "3 selected" }, Trailing = trailing };
        var window = Show(new Page { Footer = toolbar });
        try
        {
            var bar = Part(toolbar, "PART_Bar");
            leading.TranslatePoint(default, bar)!.Value.X.ShouldBeLessThan(20);
            trailing.TranslatePoint(new Point(trailing.Bounds.Width, 0), bar)!.Value.X.ShouldBeGreaterThan(bar.Bounds.Width - 20);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Closed_Jumps_Away_Under_Reduced_Motion_And_Catches_No_Touches()
    {
        var toolbar = new Toolbar { Items = { new Button { Content = "Share" } } };
        var window = Show(new Page { Footer = toolbar }, ArcMotionLevel.Off);
        try
        {
            var bar = Part(toolbar, "PART_Bar");
            toolbar.Presence.ShouldBe(1);
            bar.Opacity.ShouldBe(1);

            toolbar.IsOpen = false;
            toolbar.Presence.ShouldBe(0);
            bar.Opacity.ShouldBe(0);
            bar.IsHitTestVisible.ShouldBeFalse();
            toolbar.Bounds.Height.ShouldBe(44); // it keeps its place, so the content does not jump

            toolbar.IsOpen = true;
            toolbar.Presence.ShouldBe(1);
            bar.Opacity.ShouldBe(1);
            bar.IsHitTestVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Closed_At_First_Frame_Starts_Hidden_Without_Motion()
    {
        var toolbar = new Toolbar { IsOpen = false, Items = { new Button { Content = "Share" } } };
        var window = Show(new Page { Footer = toolbar }, ArcMotionLevel.Full);
        try
        {
            toolbar.Presence.ShouldBe(0);
            Part(toolbar, "PART_Bar").Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Closing_At_Full_Motion_Glides_And_Can_Be_Reversed()
    {
        var toolbar = new Toolbar { Items = { new Button { Content = "Share" } } };
        var window = Show(new Page { Footer = toolbar }, ArcMotionLevel.Full);
        try
        {
            var bar = Part(toolbar, "PART_Bar");
            toolbar.IsOpen = false;
            bar.IsHitTestVisible.ShouldBeFalse(); // touches stop at once
            toolbar.Presence.ShouldBe(1, 0.01);   // but the bar has not jumped: it glides

            toolbar.IsOpen = true;                // interrupted: it turns around from where it is
            bar.IsHitTestVisible.ShouldBeTrue();
            toolbar.Presence.ShouldBeGreaterThan(0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // PullToRefresh

    [AvaloniaFact]
    public void PullToRefresh_Applies_Its_Theme_And_Hosts_The_Content()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var window = Show(pull);
        try
        {
            pull.Template.ShouldNotBeNull();
            pull.PullOffset.ShouldBe(0);
            pull.IsRefreshing.ShouldBeFalse();
            Part(pull, "PART_Spinner").IsVisible.ShouldBeFalse();
            Part(pull, "PART_Indicator").Bounds.Height.ShouldBe(56);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Content_Follows_The_Finger_With_Resistance_And_Springs_Back_When_Released_Early()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var window = Show(pull, ArcMotionLevel.Full);
        try
        {
            var start = new Point(200, 200);
            var raised = false;
            pull.RefreshRequested += (_, _) => raised = true;

            Pull(window, start, 60);
            var expected = RubberBand.Apply(60, 200, 0.8);
            pull.PullOffset.ShouldBe(expected, 0.5);
            pull.PullOffset.ShouldBeLessThan(60); // resistance
            Part(pull, "PART_Indicator").Opacity.ShouldBeGreaterThan(0);

            Release(window, start, 60);
            raised.ShouldBeFalse();
            pull.IsRefreshing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Release_Past_The_Threshold_Raises_The_Event_And_Runs_The_Command()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var commands = 0;
        object? parameter = null;
        pull.Command = new RelayCommand(p => { commands++; parameter = p; });
        pull.CommandParameter = "list";
        var window = Show(pull, ArcMotionLevel.Full);
        try
        {
            var events = 0;
            pull.RefreshRequested += (_, _) => events++;
            var start = new Point(200, 200);

            Pull(window, start, 160);
            pull.PullOffset.ShouldBeGreaterThanOrEqualTo(pull.PullThreshold);
            Release(window, start, 160);

            events.ShouldBe(1);
            commands.ShouldBe(1);
            parameter.ShouldBe("list");
            pull.IsRefreshing.ShouldBeTrue();
            Part(pull, "PART_Spinner").IsVisible.ShouldBeTrue();
            Part(pull, "PART_Arrow").IsVisible.ShouldBeFalse();
            pull.PullOffset.ShouldBeGreaterThan(0); // resting (or settling) below the indicator

            pull.IsRefreshing = false;
            Part(pull, "PART_Spinner").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Deferral_Ends_The_Refresh_When_Disposed()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        IDisposable? deferral = null;
        pull.RefreshRequested += (_, e) => deferral = e.GetDeferral();
        var window = Show(pull, ArcMotionLevel.Off);
        try
        {
            var start = new Point(200, 200);
            Pull(window, start, 160);
            Release(window, start, 160);
            pull.IsRefreshing.ShouldBeTrue();
            deferral.ShouldNotBeNull();

            deferral!.Dispose();
            Dispatcher.UIThread.RunJobs();
            pull.IsRefreshing.ShouldBeFalse();
            pull.PullOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Without_A_Deferral_Stays_Refreshing_Until_The_App_Clears_It()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var window = Show(pull, ArcMotionLevel.Off);
        try
        {
            var start = new Point(200, 200);
            Pull(window, start, 160);
            Release(window, start, 160);
            Dispatcher.UIThread.RunJobs();
            pull.IsRefreshing.ShouldBeTrue();
            pull.PullOffset.ShouldBe(56, 0.5);

            pull.IsRefreshing = false;
            pull.PullOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Passes_Through_When_The_Content_Is_Scrolled_Or_The_Gesture_Goes_Up()
    {
        var scroller = TallScroller();
        var pull = new PullToRefresh { Content = scroller };
        var window = Show(pull, ArcMotionLevel.Full);
        try
        {
            var events = 0;
            pull.RefreshRequested += (_, _) => events++;
            var start = new Point(200, 300);

            scroller.Offset = new Vector(0, 80);
            Dispatcher.UIThread.RunJobs();
            Pull(window, start, 200);
            pull.PullOffset.ShouldBe(0);
            Release(window, start, 200);
            events.ShouldBe(0);

            scroller.Offset = new Vector(0, 0);
            Dispatcher.UIThread.RunJobs();
            Pull(window, start, -150);
            pull.PullOffset.ShouldBe(0);
            Release(window, start, -150);
            events.ShouldBe(0);
            pull.IsRefreshing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Ignores_A_Sideways_Gesture()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var window = Show(pull, ArcMotionLevel.Full);
        try
        {
            var start = new Point(100, 200);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(30, 4), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Point(60, 150), RawInputModifiers.LeftMouseButton);
            pull.PullOffset.ShouldBe(0);
            Thread.Sleep(80);
            window.MouseUp(start + new Point(60, 150), MouseButton.Left);
            pull.IsRefreshing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Reduced_Motion_Does_Not_Stretch_But_Still_Refreshes()
    {
        var pull = new PullToRefresh { Content = TallScroller() };
        var window = Show(pull, ArcMotionLevel.Off);
        try
        {
            var events = 0;
            pull.RefreshRequested += (_, _) => events++;
            var start = new Point(200, 200);

            Pull(window, start, 160);
            pull.PullOffset.ShouldBe(0);                       // no stretch with the finger
            pull.PullDistance.ShouldBeGreaterThanOrEqualTo(pull.PullThreshold);

            Release(window, start, 160);
            events.ShouldBe(1);
            pull.IsRefreshing.ShouldBeTrue();
            pull.PullOffset.ShouldBe(56, 0.5);                 // jumps to rest below the indicator
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PullToRefresh_Does_Not_Start_A_Pull_While_Refreshing()
    {
        var pull = new PullToRefresh { Content = TallScroller(), IsRefreshing = true };
        var window = Show(pull, ArcMotionLevel.Off);
        try
        {
            var events = 0;
            pull.RefreshRequested += (_, _) => events++;
            pull.PullOffset.ShouldBe(56, 0.5);
            var start = new Point(200, 200);
            Pull(window, start, 200);
            Release(window, start, 200);
            events.ShouldBe(0);
            pull.PullOffset.ShouldBe(56, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
