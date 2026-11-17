using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="UndoCountdownButton"/> and its shared <see cref="UndoClock"/>: the countdown at several moments on a manual
/// clock, restart, cancel and press, a single <c>Elapsed</c>, one timer for every countdown, leaving and rejoining the tree,
/// the still ring at reduced motion and the accessible name. No test waits in real time: time moves by hand and the
/// clock is ticked directly.
/// </summary>
public class UndoCountdownButtonTests
{
    /// <summary>A time source that moves only when told.</summary>
    internal sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(double seconds) => Now += TimeSpan.FromSeconds(seconds);
    }

    private sealed class Rig
    {
        public ManualTime Time { get; } = new();
        public UndoClock Clock { get; }
        public UndoCountdownButton Button { get; }
        public Window Window { get; }
        public int ElapsedCount { get; private set; }
        public int ClickCount { get; private set; }

        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, Control? host = null)
        {
            Clock = new UndoClock(Time);
            Button = Create(Clock);
            Button.Elapsed += (_, _) => ElapsedCount++;
            Button.Click += (_, _) => ClickCount++;
            Window = TestHelpers.Show(host ?? Button, level);
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Moves time on and ticks the clock, as the timer would.</summary>
        public void Advance(double seconds)
        {
            Time.Advance(seconds);
            Clock.Tick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static UndoCountdownButton Create(UndoClock clock) => new()
    {
        Text = "Undo",
        SecondsText = s => $"{s} s",
        SecondsName = s => s == 1 ? "1 second left" : $"{s} seconds left",
        Clock = clock
    };

    [AvaloniaFact]
    public void Before_Start_It_Shows_A_Full_Window()
    {
        var rig = new Rig();
        var button = rig.Button;
        Assert.False(button.IsRunning);
        Assert.Equal(1, button.Progress);
        Assert.Equal(TimeSpan.FromSeconds(5), button.Remaining);
        Assert.Equal(5, button.Seconds);
        Assert.Equal("5 s", button.SecondsLabel);
        Assert.Equal(0, rig.Clock.Count);

        button.Duration = TimeSpan.FromSeconds(8);
        Assert.Equal(8, button.Seconds);
        Assert.Equal("8 s", button.SecondsLabel);
    }

    [AvaloniaFact]
    public void Counts_Down_Whole_Seconds_And_Drains_Linearly()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Start();
        Assert.True(button.IsRunning);
        Assert.Equal(5, button.Seconds);
        Assert.Equal(1, button.Progress);

        rig.Advance(0.4);
        Assert.Equal(5, button.Seconds);
        Assert.Equal(0.92, button.Progress, 3);
        Assert.Equal(TimeSpan.FromSeconds(4.6), button.Remaining);

        rig.Advance(0.6);
        Assert.Equal(4, button.Seconds);
        Assert.Equal("4 s", button.SecondsLabel);
        Assert.Equal(0.8, button.Progress, 3);

        rig.Advance(2.5);
        Assert.Equal(2, button.Seconds);
        Assert.Equal(0.3, button.Progress, 3);

        rig.Advance(0.7);
        Assert.Equal(1, button.Seconds);
        Assert.Equal("1 s", button.SecondsLabel);
        Assert.Equal(0.16, button.Progress, 3);
        Assert.Equal(0, rig.ElapsedCount);
    }

    [AvaloniaFact]
    public void Elapsed_Is_Raised_Once_And_The_Button_Disables_Itself()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Start();
        rig.Advance(4.9);
        Assert.Equal(0, rig.ElapsedCount);
        Assert.True(button.IsEffectivelyEnabled);

        rig.Advance(0.1);
        Assert.Equal(1, rig.ElapsedCount);
        Assert.False(button.IsRunning);
        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal(0, button.Progress);
        Assert.Equal(TimeSpan.Zero, button.Remaining);
        // The last moment still reads "1 s" (the bar folds away from here), never "0 s".
        Assert.Equal(1, button.Seconds);
        Assert.Equal(0, rig.Clock.Count);
        Assert.False(rig.Clock.IsTimerRunning);

        rig.Advance(3);
        Assert.Equal(1, rig.ElapsedCount);

        // A new window enables it again.
        button.Start();
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(5, button.Seconds);
    }

    [AvaloniaFact]
    public void Elapsed_Comes_While_The_Button_Can_Still_Hold_The_Focus()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Focus(NavigationMethod.Tab);
        var focusedInHandler = false;
        var enabledInHandler = false;
        button.Elapsed += (_, _) =>
        {
            focusedInHandler = button.IsFocused;
            enabledInHandler = button.IsEffectivelyEnabled;
        };
        button.Start();
        rig.Advance(5);
        Assert.True(focusedInHandler);
        Assert.True(enabledInHandler);
        Assert.False(button.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void A_Handler_That_Starts_A_New_Window_Keeps_The_Button_Enabled()
    {
        var rig = new Rig();
        var button = rig.Button;
        var restarts = 0;
        button.Elapsed += (_, _) =>
        {
            if (restarts++ == 0)
            {
                button.Start();
            }
        };
        button.Start();
        rig.Advance(5);
        Assert.True(button.IsRunning);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(5, button.Seconds);
        Assert.Equal(1, rig.Clock.Count);
    }

    [AvaloniaFact]
    public void Start_While_Running_Restarts_The_Window()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Start();
        rig.Advance(3);
        Assert.Equal(2, button.Seconds);

        button.Start();
        Assert.Equal(5, button.Seconds);
        Assert.Equal(1, button.Progress);
        Assert.Equal(1, rig.Clock.Count);

        rig.Advance(4.9);
        Assert.Equal(0, rig.ElapsedCount);
        rig.Advance(0.1);
        Assert.Equal(1, rig.ElapsedCount);
    }

    [AvaloniaFact]
    public void Cancel_Stops_Without_Elapsed()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Start();
        rig.Advance(2);
        button.Cancel();
        Assert.False(button.IsRunning);
        Assert.Equal(0, rig.Clock.Count);
        Assert.False(rig.Clock.IsTimerRunning);
        // It stays where it stopped.
        Assert.Equal(3, button.Seconds);

        rig.Advance(10);
        Assert.Equal(0, rig.ElapsedCount);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(3, button.Seconds);

        // Cancel without a window does nothing.
        button.Cancel();
        Assert.False(button.IsRunning);
    }

    [AvaloniaFact]
    public void A_Press_Clicks_And_Closes_The_Window_So_Elapsed_Never_Follows()
    {
        var rig = new Rig();
        var button = rig.Button;
        button.Start();
        rig.Advance(1);
        button.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, rig.ClickCount);
        Assert.False(button.IsRunning);
        Assert.Equal(0, rig.Clock.Count);

        rig.Advance(10);
        Assert.Equal(0, rig.ElapsedCount);
    }

    [AvaloniaFact]
    public void Every_Countdown_Shares_One_Timer_That_Runs_Only_While_Needed()
    {
        var time = new ManualTime();
        var clock = new UndoClock(time);
        var first = Create(clock);
        var second = Create(clock);
        var elapsed = new List<UndoCountdownButton>();
        first.Elapsed += (s, _) => elapsed.Add((UndoCountdownButton)s!);
        second.Elapsed += (s, _) => elapsed.Add((UndoCountdownButton)s!);
        var window = TestHelpers.Show(new StackPanel { Children = { first, second } });
        Assert.False(clock.IsTimerRunning);

        first.Start();
        Assert.Equal(1, clock.Count);
        Assert.True(clock.IsTimerRunning);

        time.Advance(1);
        second.Start();
        Assert.Equal(2, clock.Count);
        Assert.True(clock.IsTimerRunning);

        // One tick updates both.
        time.Advance(1.5);
        clock.Tick();
        Assert.Equal(3, first.Seconds);
        Assert.Equal(4, second.Seconds);

        time.Advance(2.5);
        clock.Tick();
        Assert.Equal([first], elapsed);
        Assert.Equal(1, clock.Count);
        Assert.True(clock.IsTimerRunning);

        second.Cancel();
        Assert.Equal(0, clock.Count);
        Assert.False(clock.IsTimerRunning);
        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public void Countdowns_Use_The_Shared_Clock_By_Default()
    {
        var button = new UndoCountdownButton { Text = "Undo" };
        Assert.Same(UndoClock.Shared, button.Clock);
        TestHelpers.Show(button);
        button.Start();
        Assert.True(UndoClock.Shared.IsRegistered(button));
        Assert.True(UndoClock.Shared.IsTimerRunning);
        button.Cancel();
        Assert.False(UndoClock.Shared.IsRegistered(button));
    }

    [AvaloniaFact]
    public void A_Countdown_That_Ends_During_A_Tick_May_Cancel_Another()
    {
        var time = new ManualTime();
        var clock = new UndoClock(time);
        var first = Create(clock);
        var second = Create(clock);
        first.Elapsed += (_, _) => second.Cancel();
        TestHelpers.Show(new StackPanel { Children = { first, second } });
        first.Start();
        second.Start();
        time.Advance(5);
        clock.Tick();
        Assert.False(first.IsRunning);
        Assert.False(second.IsRunning);
        Assert.Equal(0, clock.Count);
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Pauses_Ticking_But_Not_Time()
    {
        var host = new Border();
        var rig = new Rig(host: host);
        var button = rig.Button;
        host.Child = button;
        Dispatcher.UIThread.RunJobs();
        button.Start();
        rig.Advance(1);
        Assert.Equal(4, button.Seconds);

        host.Child = null;
        Assert.True(button.IsRunning);
        Assert.Equal(0, rig.Clock.Count);
        Assert.False(rig.Clock.IsTimerRunning);

        rig.Advance(2);
        Assert.Equal(4, button.Seconds);

        host.Child = button;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, rig.Clock.Count);
        Assert.Equal(2, button.Seconds);
        Assert.Equal(0.4, button.Progress, 3);

        // The window ends while away: back in the tree it shows the end at once, and the next tick raises Elapsed.
        host.Child = null;
        rig.Advance(5);
        host.Child = button;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, button.Progress);
        Assert.Equal(0, rig.ElapsedCount);
        rig.Advance(0.1);
        Assert.Equal(1, rig.ElapsedCount);
        Assert.Equal(0, rig.Clock.Count);
    }

    [AvaloniaFact]
    public void Starting_Out_Of_The_Tree_Registers_On_Attach()
    {
        var time = new ManualTime();
        var clock = new UndoClock(time);
        var button = Create(clock);
        button.Start();
        Assert.True(button.IsRunning);
        Assert.Equal(0, clock.Count);
        time.Advance(1.2);
        TestHelpers.Show(button);
        Assert.Equal(1, clock.Count);
        Assert.Equal(4, button.Seconds);
    }

    [AvaloniaFact]
    public void The_Ring_Drains_At_Full_Motion()
    {
        var rig = new Rig(ArcMotionLevel.Full);
        var button = rig.Button;
        var ring = button.Ring!;
        Assert.Equal(0, ring.ChaseRate);
        button.Start();
        Assert.Equal(1, ring.Value);
        rig.Advance(2);
        Assert.Equal(0.6, ring.Value, 3);
        Assert.Equal(0.6, ring.DisplayedValue, 3);
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void The_Ring_Stays_Full_And_Still_Below_Full_Motion_While_The_Seconds_Run(ArcMotionLevel level)
    {
        var rig = new Rig(level);
        var button = rig.Button;
        var ring = button.Ring!;
        button.Start();
        rig.Advance(2);
        Assert.Equal(1, ring.Value);
        Assert.Equal(1, ring.DisplayedValue);
        Assert.Equal(0.6, button.Progress, 3);
        Assert.Equal(3, button.Seconds);
        rig.Advance(3);
        Assert.Equal(1, rig.ElapsedCount);
        Assert.Equal(1, ring.Value);
    }

    [AvaloniaFact]
    public void A_Change_Of_Motion_Level_Updates_The_Ring()
    {
        var rig = new Rig(ArcMotionLevel.Full);
        var button = rig.Button;
        button.Start();
        rig.Advance(1);
        Assert.Equal(0.8, button.Ring!.Value, 3);
        ArcMotion.SetLevel(rig.Window, ArcMotionLevel.Reduced);
        Assert.Equal(1, button.Ring.Value);
    }

    [AvaloniaFact]
    public void The_Accessible_Name_Carries_The_Seconds_Left()
    {
        var rig = new Rig();
        var button = rig.Button;
        Assert.Equal("Undo, 5 seconds left", AutomationProperties.GetName(button));
        button.Start();
        rig.Advance(1);
        Assert.Equal("Undo, 4 seconds left", AutomationProperties.GetName(button));
        rig.Advance(3.5);
        Assert.Equal("Undo, 1 second left", AutomationProperties.GetName(button));

        button.SecondsName = null;
        Assert.Equal("Undo", AutomationProperties.GetName(button));
        button.Text = "Restore";
        Assert.Equal("Restore", AutomationProperties.GetName(button));
    }

    [AvaloniaFact]
    public void Without_A_Format_The_Seconds_Show_As_A_Number()
    {
        var button = new UndoCountdownButton { Duration = TimeSpan.FromSeconds(3) };
        Assert.Equal("3", button.SecondsLabel);
    }

    [AvaloniaFact]
    public void Geometry_Matches_The_Reference()
    {
        var rig = new Rig();
        var button = rig.Button;
        rig.Window.UpdateLayout();
        Assert.Equal(30, button.Bounds.Height);
        var ring = button.Ring!;
        Assert.Equal(12.25, ring.Bounds.Width);
        Assert.Equal(1.75, ring.Thickness);
        Assert.False(ring.IsRoundCap);
        Assert.Equal(-90, ring.StartAngle);
        // The ring's 14px slot starts after the 1px border and 10px of padding.
        var slot = ring.TranslatePoint(default, button)!.Value;
        Assert.Equal(11 + 0.875, slot.X, 3);
    }

    [AvaloniaFact]
    public void The_Mouse_Sets_And_Clears_The_Hover_Look()
    {
        var rig = new Rig();
        var button = rig.Button;
        rig.Window.UpdateLayout();
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseMove(center, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(UndoCountdownButton.HoverPseudoClass, button.Classes);
        rig.Window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(UndoCountdownButton.HoverPseudoClass, button.Classes);
    }

    [AvaloniaFact]
    public void Running_Sets_A_Pseudo_Class()
    {
        var rig = new Rig();
        var button = rig.Button;
        Assert.DoesNotContain(UndoCountdownButton.RunningPseudoClass, button.Classes);
        button.Start();
        Assert.Contains(UndoCountdownButton.RunningPseudoClass, button.Classes);
        rig.Advance(5);
        Assert.DoesNotContain(UndoCountdownButton.RunningPseudoClass, button.Classes);
    }

    [AvaloniaFact]
    public void Elapsed_Bubbles_As_A_Routed_Event()
    {
        var host = new Border();
        var rig = new Rig(host: host);
        host.Child = rig.Button;
        Dispatcher.UIThread.RunJobs();
        RoutedEventArgs? seen = null;
        host.AddHandler(UndoCountdownButton.ElapsedEvent, (_, e) => seen = e);
        rig.Button.Start();
        rig.Advance(5);
        Assert.NotNull(seen);
        Assert.Same(rig.Button, seen!.Source);
    }
}
