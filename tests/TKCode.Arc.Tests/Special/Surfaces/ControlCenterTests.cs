using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The control center: tiles, details, focus handling, the dial, the meters, motion and the breakpoints.</summary>
public class ControlCenterTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>11:23 UTC, local time UTC.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ControlCenter Create(out List<ControlCenterStateChangedEventArgs> changes)
    {
        var center = new ControlCenter { TimeProvider = new FixedClock(new DateTimeOffset(2026, 10, 27, 11, 23, 0, TimeSpan.Zero)) };
        ControlCenterSamples.Apply(center);
        var list = new List<ControlCenterStateChangedEventArgs>();
        center.StateChanged += (_, e) => list.Add(e);
        changes = list;
        return center;
    }

    private static Window Show(ControlCenter center, ArcMotionLevel level = ArcMotionLevel.Off, double width = 600)
    {
        var window = TestHelpers.Show(center, level);
        window.Width = width;
        window.Height = 500;
        Pump();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static string StatusOf(ControlCenterSwap swap) => ((TextBlock)swap.Current!).Text!;

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static Point Center(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void Panel_is_360_by_396_with_the_sample_state()
    {
        var center = Create(out _);
        var window = Show(center);
        center.Bounds.Width.ShouldBe(360);
        center.Bounds.Height.ShouldBe(396);
        center.FocusTile.Bounds.Width.ShouldBe(338);
        center.FocusTile.Bounds.Height.ShouldBe(84);
        center.NotificationsTile.Bounds.Width.ShouldBe(165);
        center.NotificationsTile.Bounds.Height.ShouldBe(104);
        center.VolumeMeter.Bounds.Width.ShouldBe(326);
        center.VolumeMeter.Bounds.Height.ShouldBe(30);
        center.FocusTitleText.Text.ShouldBe("Focus");
        StatusOf(center.FocusStatus).ShouldBe("Off");
        StatusOf(center.NotificationsStatus).ShouldBe("2 of 3 channels");
        StatusOf(center.PresentingStatus).ShouldBe("Off");
        center.NotificationsTile.IsChecked.ShouldBe(true);
        center.FocusTile.IsChecked.ShouldBe(false);
        center.VolumeValueText.Text.ShouldBe("64%");
        center.TextSizeValueText.Text.ShouldBe("100%");
        center.IsFlush.ShouldBeFalse();
        center.IsCompact.ShouldBeFalse();
        AutomationProperties.GetName(center.NotificationsTile).ShouldBe("Notifications, 2 of 3 channels");
        window.Close();
    }

    [AvaloniaFact]
    public void Focus_tile_starts_the_first_mode_and_ends_the_session()
    {
        var center = Create(out var changes);
        var window = Show(center);
        center.FocusTile.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        center.FocusSession.ShouldNotBeNull();
        center.FocusSession!.ModeId.ShouldBe("drafting");
        center.FocusSession.Minutes.ShouldBe(50);
        center.FocusTitleText.Text.ShouldBe("Drafting");
        StatusOf(center.FocusStatus).ShouldBe("Until 12:13 PM");
        center.FocusTile.IsChecked.ShouldBe(true);
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.FocusStarted);
        changes[^1].State.Focus.ShouldBe(center.FocusSession);

        // The session does not expire: two hours later it still reads the same.
        ((FixedClock)center.TimeProvider).Now = ((FixedClock)center.TimeProvider).Now.AddHours(2);
        StatusOf(center.FocusStatus).ShouldBe("Until 12:13 PM");

        Press(window, PhysicalKey.Enter);
        center.FocusSession.ShouldBeNull();
        StatusOf(center.FocusStatus).ShouldBe("Off");
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.FocusEnded);
        window.Close();
    }

    [AvaloniaFact]
    public void Notifications_and_presenting_toggle_and_channels_count()
    {
        var center = Create(out var changes);
        var window = Show(center);
        center.Channels[2].IsOn = true;
        StatusOf(center.NotificationsStatus).ShouldBe("3 of 3 channels");
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.ChannelToggled);
        changes[^1].State.Channels["uploads"].ShouldBeTrue();
        center.NotificationsTile.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Space);
        center.AreNotificationsOn.ShouldBeFalse();
        StatusOf(center.NotificationsStatus).ShouldBe("Paused");
        center.ChannelList.IsDimmed.ShouldBeTrue();
        // Channels keep their switches while paused.
        center.Channels[0].IsOn.ShouldBeTrue();
        center.AreNotificationsOn = true;
        foreach (var channel in center.Channels)
        {
            channel.IsOn = false;
        }
        center.NotificationsTile.IsChecked.ShouldBe(true);
        StatusOf(center.NotificationsStatus).ShouldBe("0 of 3 channels");

        center.PresentingTile.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        center.IsPresenting.ShouldBeTrue();
        StatusOf(center.PresentingStatus).ShouldBe("Popups held back");
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.PresentingToggled);
        window.Close();
    }

    [AvaloniaFact]
    public void Chevron_opens_a_modal_detail_that_traps_focus_and_returns_it()
    {
        var center = Create(out _);
        var window = Show(center);
        ControlCenterDetail? opened = null;
        center.DetailOpened += (_, e) => opened = e.Detail;
        center.FocusChevron.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        opened.ShouldBe(ControlCenterDetail.Focus);
        center.ActiveDetail.ShouldBe(ControlCenterDetail.Focus);
        center.Frame.IsVisible.ShouldBeTrue();
        center.Scrim.IsVisible.ShouldBeTrue();
        center.CloseButton.IsFocused.ShouldBeTrue();
        center.Main.IsHitTestVisible.ShouldBeFalse();
        center.DetailTitle.Text.ShouldBe("Focus");
        center.StartButton.IsVisible.ShouldBeTrue();
        center.EndButton.IsVisible.ShouldBeFalse();

        // Tab cycles Close → selected mode → dial → Start session → Close.
        Press(window, PhysicalKey.Tab);
        center.ModeBar.Buttons[0].IsFocused.ShouldBeTrue();
        Press(window, PhysicalKey.Tab);
        center.Dial.IsFocused.ShouldBeTrue();
        Press(window, PhysicalKey.Tab);
        center.StartButton.IsFocused.ShouldBeTrue();
        Press(window, PhysicalKey.Tab);
        center.CloseButton.IsFocused.ShouldBeTrue();
        Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        center.StartButton.IsFocused.ShouldBeTrue();

        Press(window, PhysicalKey.Escape);
        center.ActiveDetail.ShouldBe(ControlCenterDetail.None);
        center.Frame.IsVisible.ShouldBeFalse();
        center.Main.IsHitTestVisible.ShouldBeTrue();
        center.FocusChevron.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Focus_detail_edits_are_discarded_on_close_and_applied_by_start()
    {
        var center = Create(out var changes);
        var window = Show(center);
        center.StartFocus();
        center.OpenDetail(ControlCenterDetail.Focus);
        Pump();
        center.EndButton.IsVisible.ShouldBeTrue();
        center.UpdateButton.IsVisible.ShouldBeTrue();
        center.ModeBar.SelectedIndex.ShouldBe(0);
        center.Dial.IntValue.ShouldBe(50);
        center.Dial.UntilText.ShouldBe("Until 12:13 PM");

        // Arrow keys move the radio selection (one tab stop) and the dial jumps to the mode's default.
        center.ModeBar.Buttons[0].Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);
        center.ModeBar.SelectedIndex.ShouldBe(1);
        center.ModeBar.Buttons[1].IsFocused.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(center.ModeBar.Buttons[0]).ShouldBeFalse();
        center.Dial.IntValue.ShouldBe(30);
        ((TextBlock)center.ModeDescription.Current!).Text.ShouldBe("Short block to read and comment on work");
        center.FocusSession!.ModeId.ShouldBe("drafting");

        center.CloseDetail();
        center.OpenDetail(ControlCenterDetail.Focus);
        Pump();
        center.ModeBar.SelectedIndex.ShouldBe(0);
        center.Dial.IntValue.ShouldBe(50);

        center.ModeBar.Buttons[0].Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.End);
        center.ModeBar.SelectedIndex.ShouldBe(2);
        center.Dial.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.PageUp);
        center.Dial.IntValue.ShouldBe(105);
        center.UpdateButton.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        center.FocusSession!.ModeId.ShouldBe("recharge");
        center.FocusSession.Minutes.ShouldBe(105);
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.FocusUpdated);
        center.ActiveDetail.ShouldBe(ControlCenterDetail.None);
        center.FocusChevron.IsFocused.ShouldBeTrue();

        center.OpenDetail(ControlCenterDetail.Focus);
        Pump();
        center.EndButton.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        center.FocusSession.ShouldBeNull();
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.FocusEnded);
        window.Close();
    }

    [AvaloniaFact]
    public void Dial_keys_and_value_text()
    {
        var center = Create(out _);
        var window = Show(center);
        center.OpenDetail(ControlCenterDetail.Focus);
        Pump();
        var dial = center.Dial;
        dial.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);
        dial.IntValue.ShouldBe(55);
        Press(window, PhysicalKey.ArrowDown);
        dial.IntValue.ShouldBe(50);
        Press(window, PhysicalKey.PageDown);
        dial.IntValue.ShouldBe(35);
        Press(window, PhysicalKey.Home);
        dial.IntValue.ShouldBe(5);
        Press(window, PhysicalKey.ArrowLeft);
        dial.IntValue.ShouldBe(5);
        Press(window, PhysicalKey.End);
        dial.IntValue.ShouldBe(120);
        dial.ShownNumber.ShouldBe(120);
        AutomationProperties.GetHelpText(dial).ShouldBe("120 minutes, until 1:23 PM");
        AutomationProperties.GetName(dial).ShouldBe("Session length");
        center.DraftMinutes.ShouldBe(120);
        window.Close();
    }

    [AvaloniaFact]
    public void Dial_turns_relative_to_the_grab_and_a_tap_changes_nothing()
    {
        var center = Create(out _);
        var window = Show(center);
        center.OpenDetail(ControlCenterDetail.Focus);
        Pump();
        var dial = center.Dial;
        var c = Center(dial, window);
        // A tap at 3 o'clock does not jump to 30 minutes.
        window.MouseDown(c + new Vector(60, 0), MouseButton.Left);
        window.MouseUp(c + new Vector(60, 0), MouseButton.Left);
        Pump();
        dial.IntValue.ShouldBe(50);

        // Grab at 3 o'clock, turn to 6 o'clock: +90° = +30 minutes.
        window.MouseDown(c + new Vector(60, 0), MouseButton.Left);
        foreach (var deg in new[] { 100.0, 120, 140, 160, 180 })
        {
            var a = deg * Math.PI / 180;
            window.MouseMove(c + new Vector(60 * Math.Sin(a), -60 * Math.Cos(a)), RawInputModifiers.LeftMouseButton);
            Pump();
        }
        dial.IntValue.ShouldBe(80);
        dial.KnobMinutes.ShouldBe(80, 0.5);
        // Keep turning past 12 o'clock: it keeps counting into the overshoot instead of wrapping to 0.
        foreach (var deg in new[] { 220.0, 260, 300, 340, 20, 60 })
        {
            var a = deg * Math.PI / 180;
            window.MouseMove(c + new Vector(60 * Math.Sin(a), -60 * Math.Cos(a)), RawInputModifiers.LeftMouseButton);
            Pump();
        }
        dial.IntValue.ShouldBe(120);
        dial.ShownNumber.ShouldBe(120);
        // 80 + 240° / 3 = 160 raw: 120° past the limit, the knob only ~25° past.
        ((dial.KnobMinutes - 120) * 3).ShouldBe(ControlCenterMath.DialKnobMinutes(160) * 3 - 360, 1);
        window.MouseUp(c + new Vector(52, -30), MouseButton.Left);
        Pump();
        dial.IntValue.ShouldBe(120);
        dial.KnobMinutes.ShouldBe(120);
        window.Close();
    }

    [AvaloniaFact]
    public void Meters_follow_keys_drags_and_taps()
    {
        var center = Create(out var changes);
        var window = Show(center);
        var meter = center.VolumeMeter;
        meter.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);
        center.Volume.ShouldBe(65);
        changes[^1].Reason.ShouldBe(ControlCenterChangeReason.VolumeChanged);
        Press(window, PhysicalKey.PageDown);
        center.Volume.ShouldBe(61);
        Press(window, PhysicalKey.Home);
        center.Volume.ShouldBe(0);
        center.VolumeValueText.Text.ShouldBe("0%");
        center.VolumeIcon.CurrentKey.ShouldBe(ControlCenterIcons.VolumeX);
        Press(window, PhysicalKey.End);
        center.VolumeIcon.CurrentKey.ShouldBe(ArcIcons.Volume2);
        AutomationProperties.GetHelpText(meter).ShouldBe("100%");

        center.TextSizeMeter.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);
        center.TextSize.ShouldBe(105);
        Press(window, PhysicalKey.End);
        center.TextSize.ShouldBe(130);
        center.TextPreview.RenderTransform!.Value.M11.ShouldBe(1.3, 1e-6);

        // A tap jumps to the tapped value on release.
        var left = meter.TranslatePoint(new Point(0, 15), window)!.Value;
        window.MouseDown(left + new Vector(326 * 0.25, 0), MouseButton.Left);
        center.Volume.ShouldBe(100);
        window.MouseUp(left + new Vector(326 * 0.25, 0), MouseButton.Left);
        Pump();
        center.Volume.ShouldBe(25);
        center.VolumeIcon.CurrentKey.ShouldBe(ControlCenterIcons.Volume1);

        // A drag is relative to the grab (grabbed at 75%, moved 32.6px = +10) and stretches past the end.
        var grab = left + new Vector(326 * 0.75, 0);
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab + new Vector(32.6 * i / 4, 0), RawInputModifiers.LeftMouseButton);
            Pump();
        }
        center.Volume.ShouldBe(35);
        window.MouseMove(grab + new Vector(326 * 0.75 + 22.4, 0), RawInputModifiers.LeftMouseButton);
        Pump();
        center.Volume.ShouldBe(100);
        meter.Stretch.ShouldBe(4.04, 0.05);
        window.MouseUp(grab + new Vector(326 * 0.75 + 22.4, 0), MouseButton.Left);
        Pump();
        meter.Stretch.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Full_motion_swells_the_meter_and_morphs_the_detail_from_the_tile()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var center = Create(out _);
        var window = Show(center, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);

        var meter = center.VolumeMeter;
        var p = Center(meter, window);
        window.MouseDown(p, MouseButton.Left);
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        Pump();
        meter.Swell.ShouldBe(1.18, 0.01);
        window.MouseUp(p, MouseButton.Left);
        clock.Advance(TimeSpan.FromMilliseconds(600), Frame);
        Pump();
        meter.Swell.ShouldBe(1, 0.01);

        center.OpenDetail(ControlCenterDetail.Notifications);
        center.MorphProgress.ShouldBe(0, 0.01);
        var tile = center.NotificationsTile.TranslatePoint(default, center.Frame)!.Value;
        center.Frame.FrameRect.X.ShouldBe(tile.X, 0.5);
        center.Frame.FrameRect.Width.ShouldBe(165, 0.5);
        center.NotificationsTileHost.Opacity.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(120), Frame);
        Pump();
        center.MorphProgress.ShouldBeInRange(0.2, 0.8);
        center.Frame.FrameRect.Width.ShouldBeInRange(170, 340);
        // The radius stays 22 while the box grows (no scale).
        center.Frame.RenderTransform.ShouldBeNull();
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        Pump();
        center.MorphProgress.ShouldBe(1, 0.001);
        center.Frame.FrameRect.Size.ShouldBe(new Size(342, 378));
        center.BodyOpacity.ShouldBe(1, 0.001);

        center.CloseDetail();
        center.IsClosing.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        Pump();
        center.NotificationsTileHost.Opacity.ShouldBeGreaterThan(0.2);
        center.Frame.Opacity.ShouldBe(1);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        Pump();
        center.ShownDetail.ShouldBe(ControlCenterDetail.None);
        center.Frame.IsVisible.ShouldBeFalse();
        center.NotificationsTileHost.Opacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_viewports_tighten_and_lay_the_panel_flush()
    {
        var center = Create(out _);
        var window = Show(center, width: 360);
        center.IsCompact.ShouldBeTrue();
        center.IsFlush.ShouldBeTrue();
        center.PanelSurface.IsFlush.ShouldBeTrue();
        center.Dial.Width.ShouldBe(150);
        center.Main.Margin.ShouldBe(new Thickness(6));
        window.Width = 420;
        Pump();
        center.IsCompact.ShouldBeFalse();
        center.IsFlush.ShouldBeTrue();
        window.Width = 600;
        Pump();
        center.IsFlush.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Notifications_detail_switches_and_people()
    {
        var center = Create(out _);
        var window = Show(center);
        center.OpenDetail(ControlCenterDetail.Notifications);
        Pump();
        center.DetailTitle.Text.ShouldBe("Notifications");
        center.PeopleRow.IsVisible.ShouldBeTrue();
        center.PeopleText.Text.ShouldBe("Always let through: Mara, Oskar and Ines");
        center.AllSwitch.IsChecked = false;
        center.AreNotificationsOn.ShouldBeFalse();
        center.ChannelList.IsDimmed.ShouldBeTrue();
        var builds = center.ChannelList.Children[2].GetLogicalDescendants().OfType<Switch>().First();
        builds.IsChecked = true;
        center.Channels[2].IsOn.ShouldBeTrue();
        center.People.Clear();
        center.PeopleRow.IsVisible.ShouldBeFalse();
        window.Close();
    }
}
