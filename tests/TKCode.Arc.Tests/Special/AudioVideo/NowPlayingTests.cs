using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>The now playing player: layouts, the morph, playback, skips, seeking, the pull, keyboard, focus and automation.</summary>
public class NowPlayingTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static NowPlaying Create()
    {
        var player = new NowPlaying();
        NowPlayingSamples.Apply(player);
        return player;
    }

    private static Window Show(NowPlaying player, ArcMotionLevel level = ArcMotionLevel.Off, double width = 600)
    {
        var window = TestHelpers.Show(player, level);
        window.Width = width;
        window.Height = 600;
        Pump();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static Point Center(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Visual visual)
    {
        var at = Center(visual, window);
        window.MouseMove(at, RawInputModifiers.None);
        window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static List<IInputElement> TabOrder(Window window, IInputElement start, int count)
    {
        var order = new List<IInputElement>();
        var current = start;
        for (var i = 0; i < count; i++)
        {
            order.Add(current);
            Press(window, PhysicalKey.Tab);
            current = window.FocusManager!.GetFocusedElement()!;
        }
        return order;
    }

    [AvaloniaFact]
    public void Mini_bar_is_336_by_64_with_the_first_track()
    {
        var player = Create();
        var window = Show(player);
        player.Surface.Bounds.Width.ShouldBe(336);
        player.Surface.Bounds.Height.ShouldBe(64);
        player.MiniText.Title.ShouldBe("Low Orbit");
        player.MiniText.Artist.ShouldBe("Mara Quell");
        player.Artwork.Bounds.Size.ShouldBe(new Size(44, 44));
        player.Artwork.CornerRadius.TopLeft.ShouldBe(12);
        player.Surface.CornerRadius.TopLeft.ShouldBe(22);
        player.PlayButton.Bounds.Size.ShouldBe(new Size(40, 40));
        player.CollapseButton.IsVisible.ShouldBeFalse();
        player.Waveform.IsEffectivelyVisible.ShouldBeFalse();
        player.ElapsedText.Text.ShouldBe("0:00");
        player.RemainingText.Text.ShouldBe("−3:26");
        player.Waveform.ValueText.ShouldBe("0:00 of 3:26");
        player.Waveform.Peaks.Count.ShouldBe(54);
        AutomationProperties.GetName(player.MainButton).ShouldBe("Expand player, Low Orbit by Mara Quell");
        AutomationProperties.GetName(player.PlayButton).ShouldBe("Play Low Orbit");
        AutomationProperties.GetName(player.NextButton).ShouldBe("Next track");
        player.AnnouncementText.ShouldBe("Low Orbit by Mara Quell, paused");
        player.Levels.Scales.ShouldBe([0.3, 0.6, 0.42]);
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_on_the_mini_bar_expands_and_moves_focus_to_collapse_and_Escape_folds_back()
    {
        var player = Create();
        var expanded = new List<bool>();
        player.ExpandedChanged += (_, e) => expanded.Add(e.IsExpanded);
        var window = Show(player);
        player.MainButton.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        player.IsExpanded.ShouldBeTrue();
        expanded.ShouldBe([true]);
        player.ShellHeight.ShouldBe(410.8, 0.01);
        player.Surface.Bounds.Height.ShouldBe(411, 0.5);
        player.Artwork.Bounds.Size.ShouldBe(new Size(310, 192));
        player.Artwork.CornerRadius.TopLeft.ShouldBe(20);
        player.CollapseButton.IsFocused.ShouldBeTrue();
        TestHelpers.IsFocusRingVisible(player.CollapseButton).ShouldBeTrue();
        player.MainButton.IsVisible.ShouldBeFalse();
        player.PlayButton.Bounds.Size.ShouldBe(new Size(52, 52));

        Press(window, PhysicalKey.Escape);
        player.IsExpanded.ShouldBeFalse();
        expanded.ShouldBe([true, false]);
        player.MainButton.IsFocused.ShouldBeTrue();
        player.Surface.Bounds.Height.ShouldBe(64);
        window.Close();
    }

    [AvaloniaFact]
    public void Space_expands_and_the_collapse_button_folds()
    {
        var player = Create();
        var window = Show(player);
        player.MainButton.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Space);
        player.IsExpanded.ShouldBeTrue();
        Press(window, PhysicalKey.Enter);
        player.IsExpanded.ShouldBeFalse();
        player.MainButton.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Clicking_the_mini_artwork_does_not_expand()
    {
        var player = Create();
        var window = Show(player);
        Click(window, player.Artwork);
        player.IsExpanded.ShouldBeFalse();
        Click(window, player.MainButton);
        player.IsExpanded.ShouldBeTrue();
        // A pointer expand moves focus without the ring.
        player.CollapseButton.IsFocused.ShouldBeTrue();
        TestHelpers.IsFocusRingVisible(player.CollapseButton).ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Tab_order_follows_the_layout()
    {
        var player = Create();
        var window = Show(player);
        player.MainButton.Focus(NavigationMethod.Tab);
        TabOrder(window, player.MainButton, 3).ShouldBe([player.MainButton, player.PlayButton, player.NextButton]);

        player.IsExpanded = true;
        Pump();
        player.CollapseButton.Focus(NavigationMethod.Tab);
        TabOrder(window, player.CollapseButton, 5)
            .ShouldBe([player.CollapseButton, player.Waveform, player.PreviousButton, player.PlayButton, player.NextButton]);
        window.Close();
    }

    [AvaloniaFact]
    public void Play_toggles_names_announces_and_reports()
    {
        var player = Create();
        var playing = new List<bool>();
        player.PlayingChanged += (_, e) => playing.Add(e.IsPlaying);
        var window = Show(player);
        player.PlayButton.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        player.IsPlaying.ShouldBeTrue();
        playing.ShouldBe([true]);
        AutomationProperties.GetName(player.PlayButton).ShouldBe("Pause Low Orbit");
        player.Announcer.Announcement.ShouldBe("Low Orbit by Mara Quell, playing");
        player.PlayGlyph.IsPlaying.ShouldBeTrue();
        player.Levels.IsPlaying.ShouldBeTrue();
        Press(window, PhysicalKey.Space);
        player.IsPlaying.ShouldBeFalse();
        player.Announcer.Announcement.ShouldBe("Low Orbit by Mara Quell, paused");
        window.Close();
    }

    [AvaloniaFact]
    public void The_clock_runs_one_second_per_second_while_playing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Full);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(2.5), Frame);
        Pump();
        player.Position.TotalSeconds.ShouldBe(2, 0.05);
        player.ElapsedText.Text.ShouldBe("0:02");
        player.RemainingText.Text.ShouldBe("−3:24");
        player.Waveform.Fraction.ShouldBe(2.5 / 206, 0.002);
        player.Pause();
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        player.Clock.Position.ShouldBe(2.5, 0.05);
        window.Close();
    }

    [AvaloniaFact]
    public void The_clock_keeps_running_under_reduced_and_no_motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Off);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        player.Clock.Position.ShouldBe(1.5, 0.05);
        window.Close();
    }

    [AvaloniaFact]
    public void A_track_that_ends_moves_on_and_keeps_playing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var changes = new List<NowPlayingIndexChangedEventArgs>();
        player.IndexChanged += (_, e) => changes.Add(e);
        var window = Show(player, ArcMotionLevel.Off);
        player.Index = 4;
        changes.ShouldBeEmpty();
        player.SeekTo(TimeSpan.FromSeconds(183));
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        Pump();
        player.Index.ShouldBe(0);
        player.IsPlaying.ShouldBeTrue();
        changes.Count.ShouldBe(1);
        changes[0].Reason.ShouldBe(NowPlayingIndexChangeReason.Ended);
        changes[0].Direction.ShouldBe(NowPlayingSkipDirection.Forward);
        changes[0].OldIndex.ShouldBe(4);
        player.Clock.Position.ShouldBeLessThan(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Next_and_previous_wrap_and_previous_restarts_after_three_seconds()
    {
        var player = Create();
        var changes = new List<NowPlayingIndexChangedEventArgs>();
        var seeks = new List<NowPlayingSeekEventArgs>();
        player.IndexChanged += (_, e) => changes.Add(e);
        player.SeekRequested += (_, e) => seeks.Add(e);
        var window = Show(player);
        player.Previous();
        player.Index.ShouldBe(4);
        changes[^1].Direction.ShouldBe(NowPlayingSkipDirection.Backward);
        player.MiniText.Title.ShouldBe("Riverglass");
        player.Announcer.Announcement.ShouldBe("Riverglass by Mara Quell, paused");
        player.Next();
        player.Index.ShouldBe(0);
        changes[^1].Direction.ShouldBe(NowPlayingSkipDirection.Forward);
        changes[^1].Reason.ShouldBe(NowPlayingIndexChangeReason.Skip);

        player.SeekTo(TimeSpan.FromSeconds(3));
        player.Previous();
        player.Index.ShouldBe(4);

        player.SeekTo(TimeSpan.FromSeconds(3.5));
        seeks.Clear();
        player.Previous();
        player.Index.ShouldBe(4);
        player.Clock.Position.ShouldBe(0);
        seeks.Count.ShouldBe(1);
        seeks[0].Reason.ShouldBe(NowPlayingSeekReason.Restart);
        seeks[0].Position.ShouldBe(TimeSpan.Zero);
        window.Close();
    }

    [AvaloniaFact]
    public void A_restart_glides_the_shown_position_home()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Full);
        player.SeekTo(TimeSpan.FromSeconds(60));
        player.Previous();
        player.DisplayedSeconds.ShouldBe(60, 0.5);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        player.DisplayedSeconds.ShouldBeInRange(5, 55);
        clock.Advance(TimeSpan.FromMilliseconds(600), Frame);
        player.DisplayedSeconds.ShouldBe(0, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Waveform_keys_seek_by_5_and_30_seconds_and_to_the_ends()
    {
        var player = Create();
        player.IsExpanded = true;
        var seeks = new List<NowPlayingSeekEventArgs>();
        player.SeekRequested += (_, e) => seeks.Add(e);
        var window = Show(player);
        player.Index = 3;
        player.Waveform.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.ArrowRight);
        player.Clock.Position.ShouldBe(5);
        Press(window, PhysicalKey.PageUp);
        player.Clock.Position.ShouldBe(35);
        Press(window, PhysicalKey.ArrowUp);
        player.Clock.Position.ShouldBe(40);
        Press(window, PhysicalKey.PageDown);
        player.Clock.Position.ShouldBe(10);
        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.ArrowLeft);
        player.Clock.Position.ShouldBe(0);
        Press(window, PhysicalKey.ArrowLeft);
        player.Clock.Position.ShouldBe(0);
        Press(window, PhysicalKey.End);
        player.Clock.Position.ShouldBe(226);
        player.Waveform.ValueText.ShouldBe("3:46 of 3:47");
        AutomationProperties.GetItemStatus(player.Waveform).ShouldBe("3:46 of 3:47");
        Press(window, PhysicalKey.Home);
        player.Clock.Position.ShouldBe(0);
        seeks.Count.ShouldBe(9);
        seeks.ShouldAllBe(s => s.Reason == NowPlayingSeekReason.Keyboard);
        TestHelpers.IsFocusRingVisible(player.Waveform).ShouldBeFalse();
        player.Waveform.IsFocusRingShown.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_press_on_the_waveform_seeks_holds_the_clock_and_commits_on_release()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        player.IsExpanded = true;
        var seeks = new List<NowPlayingSeekEventArgs>();
        player.SeekRequested += (_, e) => seeks.Add(e);
        var window = Show(player, ArcMotionLevel.Full);
        player.Play();
        var wave = player.Waveform;
        var left = wave.TranslatePoint(new Point(0, wave.Bounds.Height / 2), window)!.Value;
        var half = left + new Point(wave.Bounds.Width / 2, 0);
        window.MouseMove(half, RawInputModifiers.None);
        window.MouseDown(half, MouseButton.Left, RawInputModifiers.None);
        Pump();
        player.Clock.Position.ShouldBe(103, 0.01);
        wave.IsScrubbing.ShouldBeTrue();
        player.Clock.IsFrozen.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        wave.Stretch.ShouldBe(1.18, 0.01);
        player.Clock.Position.ShouldBe(103, 0.01);
        player.CurrentTime = TimeSpan.FromSeconds(10);
        player.Clock.Position.ShouldBe(103, 0.01);
        var quarter = left + new Point(wave.Bounds.Width / 4, 0);
        window.MouseMove(quarter, RawInputModifiers.LeftMouseButton);
        player.Clock.Position.ShouldBe(51.5, 0.01);
        seeks.ShouldBeEmpty();
        window.MouseUp(quarter, MouseButton.Left, RawInputModifiers.None);
        Pump();
        seeks.Count.ShouldBe(1);
        seeks[0].Reason.ShouldBe(NowPlayingSeekReason.Drag);
        seeks[0].Position.TotalSeconds.ShouldBe(51.5, 0.01);
        player.Clock.IsFrozen.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
        wave.Stretch.ShouldBe(1, 0.01);
        player.Clock.Position.ShouldBe(52, 0.05);
        window.Close();
    }

    [AvaloniaFact]
    public void CurrentTime_corrects_the_clock()
    {
        var player = Create();
        var window = Show(player);
        player.CurrentTime = TimeSpan.FromSeconds(100);
        player.Clock.Position.ShouldBe(100);
        player.Position.ShouldBe(TimeSpan.FromSeconds(100));
        player.ElapsedText.Text.ShouldBe("1:40");
        window.Close();
    }

    [AvaloniaFact]
    public void The_morph_springs_the_shell_and_flies_the_shared_parts_and_reverses_with_velocity()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Full);
        player.Expand();
        clock.Advance(TimeSpan.FromMilliseconds(84), Frame);
        Pump();
        var h = player.ShellHeight;
        h.ShouldBeInRange(110, 190);
        var p = player.CurrentLayout.Progress(h);
        var art = player.SharedRect(player.Artwork);
        art.Width.ShouldBe(44 + (310 - 44) * p, 0.01);
        art.Height.ShouldBe(44 + (192 - 44) * p, 0.01);
        player.Artwork.Bounds.Width.ShouldBe(art.Width, 1);
        // The mini text is fading out, the info has not started (80ms delay plus the first frame).
        player.MainButton.Opacity.ShouldBeLessThan(1);

        clock.Advance(TimeSpan.FromMilliseconds(66), Frame);
        var before = player.ShellHeight;
        player.Collapse();
        clock.Advance(Frame, Frame);
        // Reversing keeps the upward velocity: the shell still grows for a few frames.
        player.ShellHeight.ShouldBeGreaterThan(before);
        clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
        Pump();
        player.ShellHeight.ShouldBe(64, 0.5);
        player.CollapseButton.IsVisible.ShouldBeFalse();
        player.MainButton.Opacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_expand_overshoots_slightly_and_settles_at_the_full_height()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Full);
        player.Expand();
        var peak = 0.0;
        for (var i = 0; i < 70; i++)
        {
            clock.Advance(Frame, Frame);
            peak = Math.Max(peak, player.ShellHeight);
        }
        peak.ShouldBeInRange(410.9, 412.5);
        player.ShellHeight.ShouldBe(410.8, 0.5);
        Pump();
        player.InfoText.Opacity.ShouldBe(1);
        player.Waveform.IsEffectivelyVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_tweens_the_shell_and_does_not_fly()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        var window = Show(player, ArcMotionLevel.Reduced);
        player.Expand();
        clock.Advance(TimeSpan.FromMilliseconds(80), Frame);
        Pump();
        player.ShellHeight.ShouldBeInRange(150, 400);
        player.SharedRect(player.Artwork).ShouldBe(player.CurrentLayout.FullArt);
        player.SharedRect(player.PlayButton).ShouldBe(player.CurrentLayout.FullPlay);
        clock.Advance(TimeSpan.FromMilliseconds(120), Frame);
        player.ShellHeight.ShouldBe(410.8, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Pulling_the_artwork_down_past_70_closes_and_a_short_pull_springs_back()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        player.IsExpanded = true;
        var window = Show(player, ArcMotionLevel.Full);
        var start = Center(player.Artwork, window);
        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(start + new Point(0, i * 10), RawInputModifiers.LeftMouseButton);
        }
        player.PullOffset.ShouldBe(60, 0.01);
        // Hold still so the release has no velocity.
        Thread.Sleep(120);
        window.MouseUp(start + new Point(0, 100), MouseButton.Left, RawInputModifiers.None);
        player.IsExpanded.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        player.PullOffset.ShouldBe(0, 0.5);

        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        for (var i = 1; i <= 13; i++)
        {
            window.MouseMove(start + new Point(0, i * 10), RawInputModifiers.LeftMouseButton);
        }
        player.PullOffset.ShouldBe(78, 0.01);
        window.MouseUp(start + new Point(0, 130), MouseButton.Left, RawInputModifiers.None);
        player.IsExpanded.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
        player.PullOffset.ShouldBe(0, 0.5);
        player.ShellHeight.ShouldBe(64, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Pulling_up_rubber_bands()
    {
        var player = Create();
        player.IsExpanded = true;
        var window = Show(player);
        var start = Center(player.Artwork, window);
        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(start - new Point(0, i * 10), RawInputModifiers.LeftMouseButton);
        }
        player.PullOffset.ShouldBe(-18.18, 0.01);
        window.MouseUp(start - new Point(0, 100), MouseButton.Left, RawInputModifiers.None);
        player.IsExpanded.ShouldBeTrue();
        player.PullOffset.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_narrow_viewport_drops_the_mini_next_button()
    {
        var player = Create();
        var window = Show(player, width: 340);
        player.IsCompact.ShouldBeTrue();
        player.Surface.Bounds.Width.ShouldBe(336);
        player.NextButton.IsVisible.ShouldBeFalse();
        player.PlayButton.Bounds.X.ShouldBe(336 - 2 - 10 - 40);
        player.IsExpanded = true;
        Pump();
        player.NextButton.IsVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_narrow_container_makes_the_shell_full_width()
    {
        var player = Create();
        player.IsExpanded = true;
        var holder = new Border { Width = 290, Child = player };
        var window = TestHelpers.Show(holder);
        window.Width = 600;
        window.Height = 600;
        Pump();
        player.Surface.Bounds.Width.ShouldBe(290);
        player.Artwork.Bounds.Width.ShouldBe(264);
        player.Artwork.Bounds.Height.ShouldBe(192);
        window.Close();
    }

    [AvaloniaFact]
    public void Level_bars_rest_dance_and_hold_still_under_reduced_motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var full = new NowPlayingLevels { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(full, ArcMotionLevel.Full);
        Pump();
        full.Bounds.Size.ShouldBe(new Size(13, 14));
        full.Scales.ShouldBe([0.3, 0.6, 0.42]);
        full.DrawOpacity.ShouldBe(0.55, 1e-9);
        full.IsPlaying = true;
        clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
        full.IsDancing.ShouldBeTrue();
        full.DrawOpacity.ShouldBe(1, 1e-6);
        var first = full.Scales.ToArray();
        clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
        full.Scales.ShouldNotBe(first);
        full.IsPlaying = false;
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        full.IsDancing.ShouldBeFalse();
        full.Scales[0].ShouldBe(0.3, 1e-6);
        full.Scales[1].ShouldBe(0.6, 1e-6);
        full.DrawOpacity.ShouldBe(0.55, 1e-6);
        window.Close();

        var reduced = new NowPlayingLevels { IsPlaying = true };
        window = TestHelpers.Show(reduced, ArcMotionLevel.Reduced);
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
        reduced.IsDancing.ShouldBeFalse();
        reduced.Scales.ShouldBe([0.7, 1, 0.7]);
        reduced.DrawOpacity.ShouldBe(1, 1e-6);
        window.Close();
    }

    [AvaloniaFact]
    public void Labels_replace_every_name()
    {
        var player = Create();
        player.Labels = new NowPlayingLabels
        {
            ExpandButton = "Open {0} ({1})",
            PlayButton = "Start {0}",
            NextButton = "Skip",
            Announcement = "{0} / {1}: {2}",
            Paused = "stopped",
            PositionValue = "{0} / {1}",
            RemainingSign = "-"
        };
        player.Label = "Music";
        var window = Show(player);
        AutomationProperties.GetName(player.MainButton).ShouldBe("Open Low Orbit (Mara Quell)");
        AutomationProperties.GetName(player.PlayButton).ShouldBe("Start Low Orbit");
        AutomationProperties.GetName(player.NextButton).ShouldBe("Skip");
        player.AnnouncementText.ShouldBe("Low Orbit / Mara Quell: stopped");
        player.Waveform.ValueText.ShouldBe("0:00 / 3:26");
        player.RemainingText.Text.ShouldBe("-3:26");
        window.Close();
    }

    [AvaloniaFact]
    public void Artwork_alt_text_names_the_cover_and_its_absence_hides_it()
    {
        var player = new NowPlaying
        {
            Tracks = new AvaloniaList<NowPlayingTrack>
            {
                new("a", "One", "Someone", TimeSpan.FromSeconds(30)) { ArtworkAlt = "A blue square" },
                new("b", "Two", "Someone", TimeSpan.FromSeconds(30))
            }
        };
        var window = Show(player);
        AutomationProperties.GetName(player.Artwork).ShouldBe("A blue square");
        AutomationProperties.GetAccessibilityView(player.Artwork).ShouldBe(AccessibilityView.Content);
        player.Next();
        AutomationProperties.GetAccessibilityView(player.Artwork).ShouldBe(AccessibilityView.Raw);
        window.Close();
    }

    [AvaloniaFact]
    public void A_skip_slides_the_new_cover_in_and_swaps_the_text()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var player = Create();
        player.IsExpanded = true;
        var window = Show(player, ArcMotionLevel.Full);
        player.Next();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        player.Artwork.IsSwapping.ShouldBeTrue();
        player.Artwork.OtherLayer.IsVisible.ShouldBeTrue();
        player.InfoText.Title.ShouldBe("Saltwind Road");
        clock.Advance(TimeSpan.FromMilliseconds(900), Frame);
        player.Artwork.IsSwapping.ShouldBeFalse();
        player.Artwork.OtherLayer.IsVisible.ShouldBeFalse();
        player.Artwork.CurrentLayer.Opacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void A_queue_change_keeps_the_index_in_range()
    {
        var player = Create();
        var window = Show(player);
        player.Index = 4;
        player.Tracks!.RemoveAt(4);
        player.Index.ShouldBe(3);
        player.MiniText.Title.ShouldBe("Steel and fern");
        player.Tracks.Clear();
        player.Index.ShouldBe(0);
        player.CurrentTrack.ShouldBeNull();
        player.Next();
        player.Previous();
        window.Close();
    }
}
