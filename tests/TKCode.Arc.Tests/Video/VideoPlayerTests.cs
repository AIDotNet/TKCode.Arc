using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// <see cref="VideoPlayer"/> on the simulated backend with a fake clock: lifecycle (queued source, re-attach), the play
/// intent, AutoPlay rules, failure and Retry, the end and Replay, aspect ratio, full screen and localization.
/// </summary>
public class VideoPlayerTests
{
    [AvaloniaFact]
    public void A_Source_Set_Before_Attach_Waits_And_Loads_On_Attach()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        player.Player.ShouldBeNull();
        player.State.ShouldBe(VideoPlaybackState.Idle);
        scope.Show(player);
        player.Player.ShouldNotBeNull();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Duration.ShouldBe(TimeSpan.FromSeconds(60));
        player.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Changing_The_Source_Reloads_And_Clearing_It_Releases_The_Player()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var first = scope.Recording!.Last;
        player.Source = VideoTestScope.Clip(30);
        VideoTestScope.Pump();
        player.Duration.ShouldBe(TimeSpan.FromSeconds(30));
        first.Loads.ShouldBe(2);
        player.Source = null;
        VideoTestScope.Pump();
        player.Player.ShouldBeNull();
        first.IsDisposed.ShouldBeTrue();
        player.State.ShouldBe(VideoPlaybackState.Idle);
    }

    [AvaloniaFact]
    public void Reattaching_Reloads_And_Seeks_Back_Without_Playing()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        var window = scope.Show(player);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(5));
        player.Position.TotalSeconds.ShouldBe(5, 0.01);
        var first = scope.Recording!.Last;
        window.Content = null;
        VideoTestScope.Pump();
        first.IsDisposed.ShouldBeTrue();
        player.Player.ShouldBeNull();
        player.IsPlaying.ShouldBeFalse();
        window.Content = player;
        VideoTestScope.Pump();
        scope.Recording.Created.Count.ShouldBe(2);
        player.Player.ShouldBeSameAs(scope.Recording.Last);
        player.Position.TotalSeconds.ShouldBe(5, 0.01);
        player.IsPlaying.ShouldBeFalse();
        player.State.ShouldBe(VideoPlaybackState.Paused);
    }

    [AvaloniaFact]
    public void Playing_Set_Before_Loading_Plays_Once_Loaded_And_The_Backend_Writes_Back()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), IsPlaying = true };
        scope.Show(player);
        player.State.ShouldBe(VideoPlaybackState.Playing);
        VideoTestScope.Sim(player).Pause();
        VideoTestScope.Pump();
        player.IsPlaying.ShouldBeFalse();
        VideoTestScope.Sim(player).Play();
        VideoTestScope.Pump();
        player.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_End_Writes_Back_Raises_Ended_And_Replay_Starts_Over()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(10) };
        var ended = 0;
        player.Ended += (_, _) => ended++;
        scope.Show(player);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(11));
        player.State.ShouldBe(VideoPlaybackState.Ended);
        player.IsPlaying.ShouldBeFalse();
        ended.ShouldBe(1);
        player.StateLayer!.ShowReplay.ShouldBeTrue();
        player.Classes.ShouldContain(VideoPlayer.EndedPseudoClass);
        var replay = TestHelpers.Descendant<Avalonia.Controls.Button>(player.StateLayer, VideoStateLayer.ReplayPart);
        replay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, replay));
        VideoTestScope.Pump();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        player.IsPlaying.ShouldBeTrue();
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void A_Failure_Shows_The_Error_And_Retry_Loads_And_Plays()
    {
        using var scope = new VideoTestScope(recording: true);
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        VideoFailedEventArgs? failed = null;
        player.Failed += (_, e) => failed = e;
        scope.Show(player);
        player.Play();
        VideoTestScope.Sim(player).SimulateFailure(VideoErrorKind.Network);
        VideoTestScope.Pump();
        player.State.ShouldBe(VideoPlaybackState.Failed);
        player.IsPlaying.ShouldBeFalse();
        player.ErrorMessage.ShouldBe("The video could not be downloaded.");
        failed!.Kind.ShouldBe(VideoErrorKind.Network);
        player.StateLayer!.ShowError.ShouldBeTrue();
        player.StateLayer.ErrorText.ShouldBe("The video could not be downloaded.");
        player.LastAnnouncement.ShouldBe("The video could not be downloaded.");
        var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(player.StateLayer, VideoStateLayer.RetryPart);
        retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, retry));
        VideoTestScope.Pump();
        scope.Recording!.Last.Loads.ShouldBe(2);
        player.State.ShouldBe(VideoPlaybackState.Playing);
        player.ErrorMessage.ShouldBeNull();
        player.StateLayer.ShowError.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Handled_Failure_Hides_The_Built_In_Error_Layer()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        player.Failed += (_, e) => e.Handled = true;
        scope.Show(player);
        VideoTestScope.Sim(player).SimulateFailure(VideoErrorKind.Decode);
        VideoTestScope.Pump();
        player.State.ShouldBe(VideoPlaybackState.Failed);
        player.ErrorMessage.ShouldBe("The video could not be decoded.");
        player.StateLayer!.ShowError.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void AutoPlay_Starts_Only_Muted_Videos()
    {
        using var scope = new VideoTestScope();
        var loud = new VideoPlayer { Source = VideoTestScope.Clip(60), AutoPlay = true };
        scope.Show(loud);
        loud.IsPlaying.ShouldBeFalse();
        loud.State.ShouldBe(VideoPlaybackState.Paused);
        scope.Window!.Close();

        var muted = new VideoPlayer { Source = VideoTestScope.Clip(60), AutoPlay = true, IsMuted = true };
        scope.Show(muted);
        muted.IsPlaying.ShouldBeTrue();
        muted.State.ShouldBe(VideoPlaybackState.Playing);
    }

    [AvaloniaFact]
    public void A_Decorative_Muted_Loop_Does_Not_AutoPlay_Under_Reduced_Motion()
    {
        using var scope = new VideoTestScope();
        var reduced = new VideoPlayer { Source = VideoTestScope.Clip(60), AutoPlay = true, IsMuted = true, IsLooping = true };
        scope.Show(reduced, level: Motion.ArcMotionLevel.Reduced);
        reduced.IsPlaying.ShouldBeFalse();
        scope.Window!.Close();

        var off = new VideoPlayer { Source = VideoTestScope.Clip(60), AutoPlay = true, IsMuted = true, IsLooping = true };
        scope.Show(off, level: Motion.ArcMotionLevel.Off);
        off.IsPlaying.ShouldBeFalse();
        scope.Window!.Close();

        var full = new VideoPlayer { Source = VideoTestScope.Clip(60), AutoPlay = true, IsMuted = true, IsLooping = true };
        scope.Show(full, level: Motion.ArcMotionLevel.Full);
        full.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Without_A_Real_Backend_An_Ordinary_Source_Shows_The_Hint_And_Still_Plays()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoSource.FromUri(new Uri("https://example.com/intro.mp4")) };
        scope.Show(player);
        player.StateLayer!.HintText.ShouldBe("No video backend is available. Showing a test picture.");
        player.Duration.ShouldBe(SimulatedVideoBackend.FallbackDuration);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(2));
        player.Position.TotalSeconds.ShouldBe(2, 0.01);

        player.Source = VideoTestScope.Clip(20);
        VideoTestScope.Pump();
        player.StateLayer.HintText.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Poster_And_Big_Play_Show_Until_Playback_Starts()
    {
        using var scope = new VideoTestScope();
        var poster = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(16, 9), new Vector(96, 96));
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), Poster = poster };
        scope.Show(player);
        player.StateLayer!.ShowPoster.ShouldBeTrue();
        player.StateLayer.ShowBigPlay.ShouldBeTrue();
        player.AreControlsVisible.ShouldBeFalse();
        var big = TestHelpers.Descendant<Avalonia.Controls.Button>(player.StateLayer, VideoStateLayer.BigPlayPart);
        AutomationProperties.GetName(big).ShouldBe("Play");
        big.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, big));
        VideoTestScope.Pump();
        player.IsPlaying.ShouldBeTrue();
        player.StateLayer.ShowPoster.ShouldBeFalse();
        player.StateLayer.ShowBigPlay.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Scrim_Darkens_Towards_The_Control_Bar()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        var scrim = player.GetVisualDescendants().OfType<VideoScrim>().Single(s => s.Name == "PART_Scrim");
        var brush = scrim.Background.ShouldBeAssignableTo<Avalonia.Media.ILinearGradientBrush>().ShouldNotBeNull();
        // Black 0 to 0.6 (spec section 7): white controls stay readable on a white picture.
        brush.GradientStops[0].Color.A.ShouldBe((byte)0);
        brush.GradientStops[^1].Color.ShouldBe(Avalonia.Media.Color.FromArgb(153, 0, 0, 0));

        var title = player.GetVisualDescendants().OfType<VideoScrim>().Single(s => s.Name == "PART_TitleBar");
        var titleBrush = title.Background.ShouldBeAssignableTo<Avalonia.Media.ILinearGradientBrush>().ShouldNotBeNull();
        titleBrush.GradientStops[0].Color.ShouldBe(Avalonia.Media.Color.FromArgb(153, 0, 0, 0));
        titleBrush.GradientStops[^1].Color.A.ShouldBe((byte)0);
    }

    [AvaloniaFact]
    public void Stop_Pauses_Returns_To_The_Start_And_Shows_The_Poster_Again()
    {
        using var scope = new VideoTestScope();
        var poster = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(16, 9), new Vector(96, 96));
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), Poster = poster };
        scope.Show(player);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(12));
        player.Position.TotalSeconds.ShouldBe(12, 0.5);

        player.StopCommand.CanExecute(null).ShouldBeTrue();
        player.StopCommand.Execute(null);
        VideoTestScope.Pump();
        player.IsPlaying.ShouldBeFalse();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Position.ShouldBe(TimeSpan.Zero);
        VideoTestScope.Sim(player).Position.ShouldBe(TimeSpan.Zero);
        player.StateLayer!.ShowPoster.ShouldBeTrue();
        player.StateLayer.ShowBigPlay.ShouldBeTrue();
        player.AreControlsVisible.ShouldBeFalse();

        // Still loaded: playing again starts at once, from the start.
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(2));
        player.State.ShouldBe(VideoPlaybackState.Playing);
        player.Position.TotalSeconds.ShouldBe(2, 0.5);
        player.StateLayer.ShowPoster.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Stop_After_The_End_Offers_Play_Instead_Of_Replay()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(5) };
        scope.Show(player);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(6));
        player.State.ShouldBe(VideoPlaybackState.Ended);

        player.Stop();
        VideoTestScope.Pump();
        player.State.ShouldNotBe(VideoPlaybackState.Ended);
        player.Position.ShouldBe(TimeSpan.Zero);
        player.StateLayer!.ShowReplay.ShouldBeFalse();
        player.StateLayer.ShowBigPlay.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Stop_Without_A_Source_Does_Nothing()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer();
        scope.Show(player);
        player.Stop();
        player.State.ShouldBe(VideoPlaybackState.Idle);
        player.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Skip_Commands_Move_By_The_Seek_Step()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), SeekStep = TimeSpan.FromSeconds(7) };
        scope.Show(player);
        player.Play();
        scope.Advance(player, TimeSpan.FromSeconds(10));
        player.Pause();
        VideoTestScope.Pump();
        var start = player.Position;
        player.SkipForwardCommand.Execute(null);
        VideoTestScope.Pump();
        player.Position.TotalSeconds.ShouldBe(start.TotalSeconds + 7, 0.3);
        player.SkipBackwardCommand.Execute(null);
        player.SkipBackwardCommand.Execute(null);
        VideoTestScope.Pump();
        player.Position.TotalSeconds.ShouldBe(start.TotalSeconds - 7, 0.3);
    }

    [AvaloniaFact]
    public void The_Spinner_Waits_300ms_And_Buffering_Is_Announced_After_2s()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        scope.Show(player);
        player.Play();
        VideoTestScope.Sim(player).SetBuffering(true);
        VideoTestScope.Pump();
        player.State.ShouldBe(VideoPlaybackState.Buffering);
        player.StateLayer!.ShowSpinner.ShouldBeFalse();
        scope.Advance(player, TimeSpan.FromMilliseconds(299));
        player.StateLayer.ShowSpinner.ShouldBeFalse();
        scope.Advance(player, TimeSpan.FromMilliseconds(2));
        player.StateLayer.ShowSpinner.ShouldBeTrue();
        player.LastAnnouncement.ShouldNotBe("Buffering");
        scope.Advance(player, TimeSpan.FromSeconds(2));
        player.LastAnnouncement.ShouldBe("Buffering");
        VideoTestScope.Sim(player).SetBuffering(false);
        VideoTestScope.Pump();
        player.StateLayer.ShowSpinner.ShouldBeFalse();
        player.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Frame_Uses_16_9_Then_The_Video_Ratio_Or_AspectRatio()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer();
        scope.Show(player, width: 640);
        player.Bounds.Height.ShouldBe(360, 0.5);
        player.Source = VideoTestScope.Clip(10, 640, 480);
        VideoTestScope.Pump();
        player.Bounds.Height.ShouldBe(480, 0.5);
        player.AspectRatio = 2;
        VideoTestScope.Pump();
        player.Bounds.Height.ShouldBe(320, 0.5);
    }

    [AvaloniaFact]
    public void Full_Screen_Moves_The_Root_To_The_Overlay_Keeping_The_Player_And_A_Placeholder()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), Title = "Intro" };
        var changes = 0;
        player.FullScreenChanged += (_, _) => changes++;
        scope.Show(player, width: 400);
        var backend = player.Player;
        var root = player.Root!;
        var before = player.Bounds.Size;
        player.EnterFullScreen();
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeTrue();
        changes.ShouldBe(1);
        player.Player.ShouldBeSameAs(backend);
        player.FullScreenHost.ShouldNotBeNull();
        root.GetVisualParent().ShouldBeSameAs(player.FullScreenHost);
        player.IsVisualAncestorOf(root).ShouldBeFalse();
        player.Bounds.Size.ShouldBe(before);
        root.Bounds.Width.ShouldBe(scope.Window!.ClientSize.Width, 0.5);
        player.Classes.ShouldContain(VideoPlayer.FullScreenPseudoClass);
        AutomationProperties.GetName(player.FullScreenButton!).ShouldBe("Exit full screen");
        player.FullScreenButton!.IsChecked.ShouldBe(true);
        player.LastAnnouncement.ShouldBe("Full screen");
        root.CornerRadius.ShouldBe(default);

        root.Focus();
        scope.Window!.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeFalse();
        changes.ShouldBe(2);
        root.GetVisualParent().ShouldBeSameAs(player.Frame);
        player.Player.ShouldBeSameAs(backend);
        player.LastAnnouncement.ShouldBe("Exited full screen");
        root.CornerRadius.ShouldNotBe(default);
    }

    [AvaloniaFact]
    public void System_Full_Screen_Sets_And_Restores_The_Window_State()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), FullScreenMode = VideoFullScreenMode.System };
        var window = scope.Show(player);
        window.WindowState.ShouldBe(WindowState.Normal);
        player.IsFullScreen = true;
        VideoTestScope.Pump();
        window.WindowState.ShouldBe(WindowState.FullScreen);
        player.IsFullScreen = false;
        VideoTestScope.Pump();
        window.WindowState.ShouldBe(WindowState.Normal);
    }

    [AvaloniaFact]
    public void Leaving_System_Full_Screen_Through_The_Window_Leaves_The_Players_Too()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60), FullScreenMode = VideoFullScreenMode.System };
        var window = scope.Show(player);
        player.IsFullScreen = true;
        VideoTestScope.Pump();
        window.WindowState.ShouldBe(WindowState.FullScreen);

        // The window manager takes the window out of full screen (macOS green button, a shortcut).
        window.WindowState = WindowState.Maximized;
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeFalse();
        player.FullScreenHost.ShouldBeNull();
        window.WindowState.ShouldBe(WindowState.Maximized);
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_In_Full_Screen_Exits_It_First()
    {
        using var scope = new VideoTestScope();
        var player = new VideoPlayer { Source = VideoTestScope.Clip(60) };
        var window = scope.Show(player);
        player.EnterFullScreen();
        VideoTestScope.Pump();
        window.Content = null;
        VideoTestScope.Pump();
        player.IsFullScreen.ShouldBeFalse();
        player.Root!.GetVisualParent().ShouldBeSameAs(player.Frame);
    }

    [AvaloniaFact]
    public void Texts_Follow_The_Language()
    {
        using var scope = new VideoTestScope();
        var languages = ArcLanguageManager.Current!;
        var player = new VideoPlayer { Source = VideoTestScope.Clip(195) };
        scope.Show(player);
        try
        {
            languages.Set(LanguageTags.ZhCN);
            VideoTestScope.Pump();
            AutomationProperties.GetName(player.PlayButton!).ShouldBe("播放");
            Tooltip.GetTip(player.PlayButton!).ShouldBe("播放（K）");
            AutomationProperties.GetName(player.FullScreenButton!).ShouldBe("全屏");
            AutomationProperties.GetName(player.VolumeControl!.MuteButton).ShouldBe("静音");
            player.Seek(TimeSpan.FromSeconds(65));
            VideoTestScope.Pump();
            player.SeekBar!.Focus(NavigationMethod.Tab);
            VideoTestScope.Pump();
            player.SeekBar.ValueText.ShouldBe("1 分 5 秒，共 3 分 15 秒");
            player.VolumeControl.Slider.ValueText.ShouldBe("音量 100%");
            ControlAutomationPeer.CreatePeerForElement(player).GetLocalizedControlType().ShouldBe("视频播放器");
            ControlAutomationPeer.CreatePeerForElement(player).GetName().ShouldBe("视频");
            VideoTestScope.Sim(player).SimulateFailure(VideoErrorKind.Unsupported);
            VideoTestScope.Pump();
            player.ErrorMessage.ShouldBe("此设备不支持这种视频格式。");
            languages.Set(LanguageTags.ZhTW);
            VideoTestScope.Pump();
            AutomationProperties.GetName(player.PlayButton!).ShouldBe("播放");
            player.ErrorMessage.ShouldBe("此裝置不支援這種影片格式。");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
        }
    }

    [AvaloniaFact]
    public void Times_Show_As_Clock_Text()
    {
        VideoText.Clock(TimeSpan.FromSeconds(42)).ShouldBe("0:42");
        VideoText.Clock(TimeSpan.FromSeconds(195)).ShouldBe("3:15");
        VideoText.Clock(TimeSpan.FromSeconds(3725)).ShouldBe("1:02:05");
        VideoText.Spoken(TimeSpan.FromSeconds(65)).ShouldBe("1 minute 5 seconds");
        VideoText.Spoken(TimeSpan.FromSeconds(3601)).ShouldBe("1 hour 1 second");
        VideoText.Spoken(TimeSpan.Zero).ShouldBe("0 seconds");
    }
}

/// <summary>A clock that only moves when told; its timers never fire (tests call <see cref="SimulatedVideoPlayer.Tick"/>).</summary>
internal sealed class VideoPlayerTestClock : TimeProvider
{
    private long _ticks = TimeSpan.TicksPerDay;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan by) => _ticks += by.Ticks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NullTimer();

    private sealed class NullTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// Records what the player asks of its backend (seeks with their mode, plays, pauses) on top of a simulated player, so the
/// tests can check throttling and modes.
/// </summary>
internal sealed class RecordingVideoBackend(SimulatedVideoBackend inner) : IVideoBackend
{
    public List<RecordingVideoPlayer> Created { get; } = [];

    public RecordingVideoPlayer Last => Created[^1];

    /// <summary>Whether its players offer a platform view (<see cref="IVideoNativePresentation"/>), like the browser backend.</summary>
    public bool OffersNativeView { get; set; }

    public string Name => inner.Name;

    public bool IsReal => inner.IsReal;

    public VideoFrameKinds FrameKinds => inner.FrameKinds;

    public IReadOnlyList<string> GpuHandleTypes => inner.GpuHandleTypes;

    public VideoSupport CanPlay(string mediaType) => inner.CanPlay(mediaType);

    public IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null)
    {
        var player = new RecordingVideoPlayer(this, (SimulatedVideoPlayer)inner.CreatePlayer(options));
        Created.Add(player);
        return player;
    }
}

internal sealed class RecordingVideoPlayer(RecordingVideoBackend backend, SimulatedVideoPlayer inner) : IVideoPlayer, IVideoNativePresentation, IVideoEmbeddedTextTracks
{
    public SimulatedVideoPlayer Inner { get; } = inner;

    // ---- Embedded text tracks (the tests set the media's tracks and send cues).

    public IReadOnlyList<VideoEmbeddedTextTrackInfo> EmbeddedTextTracks { get; private set; } = [];

    public event EventHandler? EmbeddedTextTracksChanged;

    public event EventHandler<VideoEmbeddedCueEventArgs>? EmbeddedCue;

    /// <summary>The indexes given to <see cref="SelectEmbeddedTextTrack"/>, in order.</summary>
    public List<int> EmbeddedSelections { get; } = [];

    public void SelectEmbeddedTextTrack(int index) => EmbeddedSelections.Add(index);

    public void SimulateEmbeddedTracks(params VideoEmbeddedTextTrackInfo[] tracks)
    {
        EmbeddedTextTracks = tracks;
        EmbeddedTextTracksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SimulateEmbeddedCue(int track, double start, double? end, string text) =>
        EmbeddedCue?.Invoke(this, new VideoEmbeddedCueEventArgs(track, TimeSpan.FromSeconds(start), end is { } e ? TimeSpan.FromSeconds(e) : null, text));

    /// <summary>The platform views handed out (plain borders standing in for the browser's video element).</summary>
    public List<Control> NativeViews { get; } = [];

    /// <summary>The track last given to <see cref="SetNativeCaptions"/>.</summary>
    public VideoTextTrack? NativeCaptions { get; private set; }

    public bool CanPresentNatively => backend.OffersNativeView && !IsDisposed;

    public Control CreateNativeView()
    {
        if (!CanPresentNatively)
        {
            throw new NotSupportedException();
        }
        var view = new Border { Background = Avalonia.Media.Brushes.DarkSlateBlue };
        NativeViews.Add(view);
        return view;
    }

    public void SetNativeCaptions(VideoTextTrack? track) => NativeCaptions = track;

    public List<(TimeSpan Position, VideoSeekMode Mode)> Seeks { get; } = [];

    public int Plays { get; private set; }

    public int Pauses { get; private set; }

    public int Loads { get; private set; }

    public bool IsDisposed { get; private set; }

    public IVideoBackend Backend => backend;

    public VideoPlaybackState State => Inner.State;

    public TimeSpan Position => Inner.Position;

    public TimeSpan Duration => Inner.Duration;

    public PixelSize NaturalSize => Inner.NaturalSize;

    public IReadOnlyList<VideoTimeRange> Buffered => Inner.Buffered;

    public bool IsSeekable => Inner.IsSeekable;

    public double Volume { get => Inner.Volume; set => Inner.Volume = value; }

    public bool IsMuted { get => Inner.IsMuted; set => Inner.IsMuted = value; }

    public bool IsLooping { get => Inner.IsLooping; set => Inner.IsLooping = value; }

    public double PlaybackRate { get => Inner.PlaybackRate; set => Inner.PlaybackRate = value; }

    public VideoFrameTarget? FrameTarget { get => Inner.FrameTarget; set => Inner.FrameTarget = value; }

    public Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        Loads++;
        return Inner.LoadAsync(source, cancellationToken);
    }

    public void Play()
    {
        Plays++;
        Inner.Play();
    }

    public void Pause()
    {
        Pauses++;
        Inner.Pause();
    }

    public void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
    {
        Seeks.Add((position, mode));
        Inner.Seek(position, mode);
    }

    public bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease) => Inner.TryAcquireFrame(request, out lease);

    public event EventHandler? StateChanged { add => Inner.StateChanged += ForwardSender(value); remove { } }

    public event EventHandler? PositionChanged { add => Inner.PositionChanged += ForwardSender(value); remove { } }

    public event EventHandler? DurationChanged { add => Inner.DurationChanged += ForwardSender(value); remove { } }

    public event EventHandler? NaturalSizeChanged { add => Inner.NaturalSizeChanged += ForwardSender(value); remove { } }

    public event EventHandler? BufferedChanged { add => Inner.BufferedChanged += ForwardSender(value); remove { } }

    public event EventHandler? FrameAvailable { add => Inner.FrameAvailable += ForwardSender(value); remove { } }

    public event EventHandler<VideoErrorEventArgs>? Failed
    {
        add
        {
            if (value is not null)
            {
                Inner.Failed += (_, e) => value(this, e);
            }
        }
        remove { }
    }

    private EventHandler ForwardSender(EventHandler? handler) => (_, e) => handler?.Invoke(this, e);

    public void Dispose()
    {
        IsDisposed = true;
        Inner.Dispose();
    }
}

/// <summary>Sets <see cref="ArcVideo.Backend"/> for one test and restores the previous one.</summary>
internal sealed class VideoTestScope : IDisposable
{
    private readonly IVideoBackend _previous;

    public VideoTestScope(bool recording = false)
    {
        _previous = ArcVideo.Backend;
        Clock = new VideoPlayerTestClock();
        Simulated = new SimulatedVideoBackend(Clock);
        Recording = recording ? new RecordingVideoBackend(Simulated) : null;
        ArcVideo.Backend = (IVideoBackend?)Recording ?? Simulated;
    }

    public VideoPlayerTestClock Clock { get; }

    public SimulatedVideoBackend Simulated { get; }

    public RecordingVideoBackend? Recording { get; }

    public Window? Window { get; private set; }

    public static VideoSource Clip(double seconds = 60, int width = 1280, int height = 720) =>
        VideoSource.FromSimulated(new PixelSize(width, height), TimeSpan.FromSeconds(seconds));

    public Window Show(VideoPlayer player, double width = 640, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        player.Width = width;
        player.Clock = Clock;
        var window = TestHelpers.Show(player, level);
        window.Width = width + 100;
        window.Height = 720;
        Window = window;
        Pump();
        return window;
    }

    /// <summary>The simulated player under <paramref name="player"/>.</summary>
    public static SimulatedVideoPlayer Sim(VideoPlayer player) => player.Player switch
    {
        SimulatedVideoPlayer simulated => simulated,
        RecordingVideoPlayer recording => recording.Inner,
        _ => throw new InvalidOperationException("no simulated player"),
    };

    /// <summary>Moves the clock, lets the simulated player notice, and runs the player's due timers and the dispatcher.</summary>
    public void Advance(VideoPlayer player, TimeSpan by)
    {
        Clock.Advance(by);
        if (player.Player is not null)
        {
            Sim(player).Tick();
        }
        player.RunDueTimers();
        Pump();
    }

    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        Window?.Close();
        ArcVideo.Backend = _previous;
    }
}
