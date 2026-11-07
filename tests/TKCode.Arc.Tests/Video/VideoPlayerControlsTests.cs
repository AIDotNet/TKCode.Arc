using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// The control bar and input of <see cref="VideoPlayer"/>: every auto-hide rule, click vs double click, touch taps and
/// skips, every key of spec section 11, the seek bar (drag throttle and modes, chapters, value texts), menus and names.
/// </summary>
public class VideoPlayerControlsTests
{
    private static (VideoTestScope Scope, VideoPlayer Player, Window Window) Playing(double seconds = 60, bool recording = false, double width = 640)
    {
        var scope = new VideoTestScope(recording);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(seconds) };
        var window = scope.Show(player, width);
        player.Play();
        VideoTestScope.Pump();
        return (scope, player, window);
    }

    private static Point At(Visual visual, Point local, Window window) => visual.TranslatePoint(local, window)!.Value;

    private static Point Center(Visual visual, Window window) => At(visual, new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window);

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        VideoTestScope.Pump();
    }

    [AvaloniaFact]
    public void The_Bar_Hides_After_Two_Idle_Seconds_Of_Playback()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.AreControlsVisible.ShouldBeTrue();
        player.Classes.ShouldContain(VideoPlayer.ControlsVisiblePseudoClass);
        scope.Advance(player, TimeSpan.FromSeconds(1.9));
        player.AreControlsVisible.ShouldBeTrue();
        scope.Advance(player, TimeSpan.FromSeconds(0.2));
        player.AreControlsVisible.ShouldBeFalse();
        player.Classes.ShouldNotContain(VideoPlayer.ControlsVisiblePseudoClass);
        player.CaptionLayer!.BottomInset.ShouldBe(0);
        // Activity brings it back and restarts the countdown.
        player.ShowControlsNow();
        player.AreControlsVisible.ShouldBeTrue();
        player.CaptionLayer.BottomInset.ShouldBeGreaterThan(0);
        scope.Advance(player, TimeSpan.FromSeconds(2.1));
        player.AreControlsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pointer_Movement_Shows_The_Bar()
    {
        var (scope, player, window) = Playing();
        using var _ = scope;
        scope.Advance(player, TimeSpan.FromSeconds(3));
        player.AreControlsVisible.ShouldBeFalse();
        window.MouseMove(Center(player, window));
        VideoTestScope.Pump();
        player.AreControlsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Bar_Stays_While_Paused_And_Before_The_First_Play_It_Is_Hidden()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        player.AreControlsVisible.ShouldBeFalse();
        player.Play();
        VideoTestScope.Pump();
        player.Pause();
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromSeconds(10));
        player.AreControlsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Bar_Stays_While_The_Pointer_Is_Over_It()
    {
        var (scope, player, window) = Playing();
        using var _ = scope;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.MouseMove(Center(player.PlayButton!, window));
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromSeconds(5));
        player.AreControlsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Bar_Stays_While_A_Menu_Is_Open()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.SpeedButton!.ToggleMenu(MenuOpenFocus.Surface);
        VideoTestScope.Pump();
        player.SpeedButton.IsMenuOpen.ShouldBeTrue();
        scope.Advance(player, TimeSpan.FromSeconds(5));
        player.AreControlsVisible.ShouldBeTrue();
        player.SpeedButton.CloseMenu();
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromSeconds(2.1));
        player.AreControlsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Bar_Stays_While_Focus_Is_Inside_It()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.PlayButton!.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromSeconds(5));
        player.AreControlsVisible.ShouldBeTrue();
        player.Root!.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromSeconds(2.1));
        player.AreControlsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ShowControls_Always_And_Never_Override_The_Rules()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.ShowControls = VideoControlsVisibility.Always;
        scope.Advance(player, TimeSpan.FromSeconds(5));
        player.AreControlsVisible.ShouldBeTrue();
        player.ShowControls = VideoControlsVisibility.Never;
        player.Pause();
        VideoTestScope.Pump();
        player.AreControlsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Pointer_Hides_With_The_Bar()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.Root!.Cursor.ShouldBeNull();
        scope.Advance(player, TimeSpan.FromSeconds(3));
        player.AreControlsVisible.ShouldBeFalse();
        player.Root.Cursor.ShouldNotBeNull();
        player.ShowControlsNow();
        player.Root.Cursor.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Click_Toggles_Play_After_200ms_And_A_Double_Click_Toggles_Full_Screen_Instead()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        var window = scope.Show(player);
        player.Play();
        player.Pause();
        VideoTestScope.Pump();
        var point = At(player, new Point(120, 60), window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        VideoTestScope.Pump();
        player.IsPlaying.ShouldBeFalse();
        scope.Advance(player, TimeSpan.FromMilliseconds(199));
        player.IsPlaying.ShouldBeFalse();
        scope.Advance(player, TimeSpan.FromMilliseconds(2));
        player.IsPlaying.ShouldBeTrue();

        scope.Advance(player, TimeSpan.FromSeconds(1));
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        VideoTestScope.Pump();
        scope.Advance(player, TimeSpan.FromMilliseconds(300));
        player.IsFullScreen.ShouldBeTrue();
        player.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Taps_Show_The_Bar_Then_Toggle_And_Side_Double_Taps_Skip_And_Add_Up()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        scope.Advance(player, TimeSpan.FromSeconds(20));
        player.AreControlsVisible.ShouldBeFalse();
        // Hidden bar: a tap only shows it.
        player.OnTap(0);
        scope.Advance(player, TimeSpan.FromMilliseconds(250));
        player.AreControlsVisible.ShouldBeTrue();
        player.IsPlaying.ShouldBeTrue();
        // Shown bar: a tap toggles.
        player.OnTap(0);
        scope.Advance(player, TimeSpan.FromMilliseconds(250));
        player.IsPlaying.ShouldBeFalse();
        var start = player.Position;
        // Double tap on the right: +10 s, a third tap +10 s more.
        player.OnTap(1);
        player.OnTap(1);
        VideoTestScope.Pump();
        player.Position.ShouldBe(start + TimeSpan.FromSeconds(10));
        player.StateLayer!.SkipForwardText.ShouldBe("+10s");
        player.OnTap(1);
        VideoTestScope.Pump();
        player.Position.ShouldBe(start + TimeSpan.FromSeconds(20));
        player.StateLayer.SkipForwardText.ShouldBe("+20s");
        player.IsPlaying.ShouldBeFalse();
        // The streak ends; a double tap on the left goes back 10 s.
        scope.Advance(player, TimeSpan.FromSeconds(1));
        player.OnTap(-1);
        player.OnTap(-1);
        VideoTestScope.Pump();
        player.Position.ShouldBe(start + TimeSpan.FromSeconds(10));
        player.StateLayer.SkipBackText.ShouldBe("−10s");
        // A double tap in the middle toggles full screen.
        scope.Advance(player, TimeSpan.FromSeconds(1));
        player.OnTap(0);
        player.OnTap(0);
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Every_Key_Of_The_Table_Works()
    {
        var (scope, player, window) = Playing(seconds: 100);
        using var _ = scope;
        player.Pause();
        player.Seek(TimeSpan.FromSeconds(50));
        player.Root!.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();

        Key(window, PhysicalKey.Space);
        player.IsPlaying.ShouldBeTrue();
        Key(window, PhysicalKey.K);
        player.IsPlaying.ShouldBeFalse();
        Key(window, PhysicalKey.ArrowLeft);
        player.Position.ShouldBe(TimeSpan.FromSeconds(45));
        Key(window, PhysicalKey.ArrowRight);
        Key(window, PhysicalKey.ArrowRight);
        player.Position.ShouldBe(TimeSpan.FromSeconds(55));
        Key(window, PhysicalKey.J);
        player.Position.ShouldBe(TimeSpan.FromSeconds(45));
        Key(window, PhysicalKey.L);
        player.Position.ShouldBe(TimeSpan.FromSeconds(55));
        player.Volume = 0.5;
        Key(window, PhysicalKey.ArrowUp);
        player.Volume.ShouldBe(0.55, 1e-9);
        Key(window, PhysicalKey.ArrowDown);
        Key(window, PhysicalKey.ArrowDown);
        player.Volume.ShouldBe(0.45, 1e-9);
        Key(window, PhysicalKey.M);
        player.IsMuted.ShouldBeTrue();
        player.Player!.IsMuted.ShouldBeTrue();
        Key(window, PhysicalKey.M);
        player.IsMuted.ShouldBeFalse();
        Key(window, PhysicalKey.Period, RawInputModifiers.Shift);
        player.PlaybackRate.ShouldBe(1.25);
        player.Player.PlaybackRate.ShouldBe(1.25);
        Key(window, PhysicalKey.Comma, RawInputModifiers.Shift);
        Key(window, PhysicalKey.Comma, RawInputModifiers.Shift);
        player.PlaybackRate.ShouldBe(0.75);
        Key(window, PhysicalKey.Digit3);
        player.Position.ShouldBe(TimeSpan.FromSeconds(30));
        Key(window, PhysicalKey.Digit0);
        player.Position.ShouldBe(TimeSpan.Zero);
        Key(window, PhysicalKey.End);
        player.Position.ShouldBe(TimeSpan.FromSeconds(100));
        Key(window, PhysicalKey.Home);
        player.Position.ShouldBe(TimeSpan.Zero);

        var english = new VideoTextTrack { Kind = VideoTextTrackKind.Captions, Language = "en-US", Label = "English" };
        player.TextTracks.Add(english);
        Key(window, PhysicalKey.C);
        player.SelectedTextTrack.ShouldBeSameAs(english);
        player.LastAnnouncement.ShouldBe("Captions on: English");
        Key(window, PhysicalKey.C);
        player.SelectedTextTrack.ShouldBeNull();
        player.LastAnnouncement.ShouldBe("Captions off");

        Key(window, PhysicalKey.F);
        player.IsFullScreen.ShouldBeTrue();
        Key(window, PhysicalKey.F);
        player.IsFullScreen.ShouldBeFalse();
        Key(window, PhysicalKey.F);
        Key(window, PhysicalKey.Escape);
        player.IsFullScreen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Space_On_A_Button_Is_The_Buttons_Own_And_Keys_With_Ctrl_Are_Ignored()
    {
        var (scope, player, window) = Playing();
        using var _ = scope;
        player.FullScreenButton!.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();
        Key(window, PhysicalKey.Space);
        // Space pressed the full-screen button; it did not also pause.
        player.IsFullScreen.ShouldBeTrue();
        player.IsPlaying.ShouldBeTrue();
        player.ExitFullScreen();
        player.Root!.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();
        Key(window, PhysicalKey.K, RawInputModifiers.Control);
        player.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Arrow_Keys_On_The_Sliders_Belong_To_The_Sliders()
    {
        var (scope, player, window) = Playing(seconds: 100);
        using var _ = scope;
        player.Pause();
        player.Seek(TimeSpan.FromSeconds(50));
        VideoTestScope.Pump();
        player.SeekBar!.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowRight);
        player.Position.ShouldBe(TimeSpan.FromSeconds(55));
        Key(window, PhysicalKey.ArrowUp);
        player.Position.ShouldBe(TimeSpan.FromSeconds(60));
        player.Volume.ShouldBe(1);
        player.Volume = 0.5;
        player.VolumeControl!.Slider.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowLeft);
        player.Volume.ShouldBe(0.45, 1e-9);
        player.Position.ShouldBe(TimeSpan.FromSeconds(60));
        player.VolumeControl.IsExpanded.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Dragging_The_Seek_Bar_Pauses_Previews_Fast_At_Most_Every_100ms_And_Lands_Accurately()
    {
        var (scope, player, window) = Playing(seconds: 100, recording: true);
        using var _ = scope;
        var recorder = scope.Recording!.Last;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var seek = player.SeekBar!;
        var y = seek.Bounds.Height / 2;
        window.MouseDown(At(seek, new Point(seek.Bounds.Width * 0.2, y), window), MouseButton.Left);
        VideoTestScope.Pump();
        seek.IsDragging.ShouldBeTrue();
        recorder.Inner.State.ShouldBe(VideoPlaybackState.Paused);
        player.IsPlaying.ShouldBeTrue();
        recorder.Seeks.Count.ShouldBe(1);
        recorder.Seeks[^1].Mode.ShouldBe(VideoSeekMode.Fast);
        window.MouseMove(At(seek, new Point(seek.Bounds.Width * 0.3, y), window), RawInputModifiers.LeftMouseButton);
        window.MouseMove(At(seek, new Point(seek.Bounds.Width * 0.4, y), window), RawInputModifiers.LeftMouseButton);
        VideoTestScope.Pump();
        // Within 100 ms of the first preview: the next waits.
        recorder.Seeks.Count.ShouldBe(1);
        player.SeekTipText.ShouldBe("0:40");
        seek.IsDragging.ShouldBeTrue();
        scope.Advance(player, TimeSpan.FromMilliseconds(100));
        seek.IsDragging.ShouldBeTrue();
        recorder.Seeks.Count.ShouldBe(2);
        recorder.Seeks[^1].Mode.ShouldBe(VideoSeekMode.Fast);
        recorder.Seeks[^1].Position.TotalSeconds.ShouldBe(40, 0.5);
        window.MouseUp(At(seek, new Point(seek.Bounds.Width * 0.5, y), window), MouseButton.Left);
        VideoTestScope.Pump();
        recorder.Seeks[^1].Mode.ShouldBe(VideoSeekMode.Accurate);
        recorder.Seeks[^1].Position.TotalSeconds.ShouldBe(40, 0.5);
        seek.IsDragging.ShouldBeFalse();
        recorder.Inner.State.ShouldBe(VideoPlaybackState.Playing);
        player.IsPlaying.ShouldBeTrue();
        seek.ValueText.ShouldBe("40 seconds of 1 minute 40 seconds");
    }

    [AvaloniaFact]
    public void Chapters_Cut_The_Bar_Name_The_Tip_And_Page_Keys_Jump_Between_Them()
    {
        var (scope, player, window) = Playing(seconds: 100);
        using var _ = scope;
        var chapters = new VideoTextTrack { Kind = VideoTextTrackKind.Chapters };
        chapters.SetCues([
            new VideoCue { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(30), Text = "Intro" },
            new VideoCue { Start = TimeSpan.FromSeconds(30), End = TimeSpan.FromSeconds(70), Text = "Setup" },
            new VideoCue { Start = TimeSpan.FromSeconds(70), End = TimeSpan.FromSeconds(100), Text = "Wrap-up" },
        ]);
        player.TextTracks.Add(chapters);
        player.Pause();
        player.Seek(TimeSpan.FromSeconds(40));
        VideoTestScope.Pump();
        player.SeekBar!.Chapters.Count.ShouldBe(3);
        player.SeekBar.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.PageDown);
        player.Position.ShouldBe(TimeSpan.FromSeconds(70));
        Key(window, PhysicalKey.PageUp);
        player.Position.ShouldBe(TimeSpan.FromSeconds(30));
        Key(window, PhysicalKey.PageUp);
        player.Position.ShouldBe(TimeSpan.Zero);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var seek = player.SeekBar;
        window.MouseMove(At(seek, new Point(seek.Bounds.Width * 0.5, seek.Bounds.Height / 2), window));
        VideoTestScope.Pump();
        player.SeekTipText.ShouldBe("Setup · 0:50");
    }

    [AvaloniaFact]
    public void Without_Chapters_Page_Keys_Move_A_Tenth()
    {
        var (scope, player, window) = Playing(seconds: 100);
        using var _ = scope;
        player.Pause();
        player.Seek(TimeSpan.FromSeconds(40));
        player.SeekBar!.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.PageUp);
        player.Position.ShouldBe(TimeSpan.FromSeconds(50));
        Key(window, PhysicalKey.PageDown);
        Key(window, PhysicalKey.PageDown);
        player.Position.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [AvaloniaFact]
    public void The_Seek_Value_Text_Says_Of_Only_On_Focus_And_Drag_End()
    {
        var (scope, player, _) = Playing(seconds: 195);
        using var _ = scope;
        player.Pause();
        player.Seek(TimeSpan.FromSeconds(65));
        VideoTestScope.Pump();
        player.SeekBar!.ValueText.ShouldBe("1 minute 5 seconds");
        player.SeekBar.Focus(NavigationMethod.Tab);
        VideoTestScope.Pump();
        player.SeekBar.ValueText.ShouldBe("1 minute 5 seconds of 3 minutes 15 seconds");
        AutomationProperties.GetItemStatus(player.SeekBar).ShouldBe("1 minute 5 seconds of 3 minutes 15 seconds");
        AutomationProperties.GetName(player.SeekBar).ShouldBe("Seek");
        // Playback with focus elsewhere: the running value has no "of".
        player.Root!.Focus(NavigationMethod.Tab);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(2));
        player.SeekBar.ValueText.ShouldBe("1 minute 7 seconds");
    }

    [AvaloniaFact]
    public void Buttons_Are_Named_Toggles_With_Shortcuts()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        var play = player.PlayButton!;
        AutomationProperties.GetName(play).ShouldBe("Pause");
        Tooltip.GetTip(play).ShouldBe("Pause (K)");
        AutomationProperties.GetAcceleratorKey(play).ShouldBe("K");
        ControlAutomationPeer.CreatePeerForElement(play).ShouldBeAssignableTo<Avalonia.Automation.Provider.IToggleProvider>();
        play.IsChecked.ShouldBe(true);
        player.Pause();
        VideoTestScope.Pump();
        AutomationProperties.GetName(play).ShouldBe("Play");
        play.IsChecked.ShouldBe(false);
        var mute = player.VolumeControl!.MuteButton;
        AutomationProperties.GetName(mute).ShouldBe("Mute");
        player.Volume = 0.6;
        player.VolumeControl.Slider.ValueText.ShouldBe("Volume 60%");
        player.IsMuted = true;
        AutomationProperties.GetName(mute).ShouldBe("Unmute");
        mute.IsChecked.ShouldBe(true);
        player.VolumeControl.Slider.ValueText.ShouldBe("Muted");
        Tooltip.GetTip(player.FullScreenButton!).ShouldBe("Full screen (F)");
        var peer = ControlAutomationPeer.CreatePeerForElement(player);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetLocalizedControlType().ShouldBe("Video player");
        peer.GetName().ShouldBe("Video");
        player.Title = "Product tour";
        peer.GetName().ShouldBe("Product tour");
    }

    [AvaloniaFact]
    public void Clicking_The_Buttons_Plays_Mutes_And_Goes_Full_Screen()
    {
        var (scope, player, window) = Playing();
        using var _ = scope;
        player.PlayButton!.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.Enter);
        player.IsPlaying.ShouldBeFalse();
        player.VolumeControl!.MuteButton.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.Enter);
        player.IsMuted.ShouldBeTrue();
        player.FullScreenButton!.Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.Enter);
        player.IsFullScreen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Speed_And_Captions_Menus_List_The_Choices_And_Apply_Them()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        var english = new VideoTextTrack { Kind = VideoTextTrackKind.Captions, Language = "en-US", Label = "English", IsDefault = true };
        var chinese = new VideoTextTrack { Kind = VideoTextTrackKind.Subtitles, Language = "zh-CN" };
        player.TextTracks.Add(english);
        player.TextTracks.Add(chinese);
        player.TextTracks.Add(new VideoTextTrack { Kind = VideoTextTrackKind.Chapters });
        VideoTestScope.Pump();
        player.SelectedTextTrack.ShouldBeSameAs(english);
        player.CaptionLayer!.Track.ShouldBeSameAs(english);
        player.CaptionsButton!.IsVisible.ShouldBeTrue();

        player.SpeedButton!.ToggleMenu(MenuOpenFocus.Surface);
        VideoTestScope.Pump();
        var rows = player.SpeedButton.Menu!.Items.OfType<MenuEntry>().ToArray();
        rows.Select(r => r.Header).ShouldBe(["0.5×", "0.75×", "Normal", "1.25×", "1.5×", "2×"]);
        rows[2].IsChecked.ShouldBeTrue();
        rows[4].PerformClick();
        VideoTestScope.Pump();
        player.PlaybackRate.ShouldBe(1.5);
        player.LastAnnouncement.ShouldBe("Speed 1.5×");

        player.CaptionsButton.ToggleMenu(MenuOpenFocus.Surface);
        VideoTestScope.Pump();
        var captionRows = player.CaptionsButton.Menu!.Items.OfType<MenuEntry>().ToArray();
        captionRows.Select(r => r.Header).ShouldBe(["Off", "English", System.Globalization.CultureInfo.GetCultureInfo("zh-CN").NativeName, "75%", "100%", "150%", "200%"]);
        captionRows[1].IsChecked.ShouldBeTrue();
        captionRows[2].PerformClick();
        VideoTestScope.Pump();
        player.SelectedTextTrack.ShouldBeSameAs(chinese);
        player.CaptionsButton.ToggleMenu(MenuOpenFocus.Surface);
        VideoTestScope.Pump();
        player.CaptionsButton.Menu.Items.OfType<MenuEntry>().ToArray()[5].PerformClick();
        VideoTestScope.Pump();
        player.CaptionScale.ShouldBe(1.5);
        player.CaptionLayer.CaptionScale.ShouldBe(1.5);
    }

    [AvaloniaFact]
    public void The_Wheel_Over_The_Volume_Changes_It_By_5_Percent()
    {
        var (scope, player, window) = Playing();
        using var _ = scope;
        player.Volume = 0.5;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var mute = player.VolumeControl!.MuteButton;
        window.MouseWheel(Center(mute, window), new Vector(0, 1));
        VideoTestScope.Pump();
        player.Volume.ShouldBe(0.55, 1e-9);
        window.MouseWheel(Center(mute, window), new Vector(0, -1));
        window.MouseWheel(Center(mute, window), new Vector(0, -1));
        VideoTestScope.Pump();
        player.Volume.ShouldBe(0.45, 1e-9);
    }

    [AvaloniaFact]
    public void The_Compact_Layout_Starts_Below_420()
    {
        var (scope, player, _) = Playing(width: 360);
        using var _ = scope;
        player.Classes.ShouldContain(VideoPlayer.CompactPseudoClass);
        player.VolumeControl!.IsCompact.ShouldBeTrue();
        player.Width = 640;
        VideoTestScope.Pump();
        player.Classes.ShouldNotContain(VideoPlayer.CompactPseudoClass);
    }

    [AvaloniaFact]
    public void Commands_Drive_The_Player()
    {
        var (scope, player, _) = Playing();
        using var _ = scope;
        player.PlayPauseCommand.Execute(null);
        player.IsPlaying.ShouldBeFalse();
        player.ToggleMuteCommand.Execute(null);
        player.IsMuted.ShouldBeTrue();
        player.ToggleFullScreenCommand.Execute(null);
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeTrue();
        player.ToggleFullScreenCommand.Execute(null);
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Playing_Moves_The_Position_Time_Text_And_Captions_Position()
    {
        var (scope, player, _) = Playing(seconds: 195);
        using var _ = scope;
        scope.Advance(player, TimeSpan.FromSeconds(65));
        player.Position.TotalSeconds.ShouldBe(65, 0.01);
        TestHelpers.Descendant<TextBlock>(player, VideoPlayer.TimePart).Text.ShouldBe("1:05 / 3:15");
        player.CaptionLayer!.Position.TotalSeconds.ShouldBe(65, 0.01);
        player.SeekBar!.Value.ShouldBe(65, 0.01);
    }
}
