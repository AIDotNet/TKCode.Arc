using Avalonia;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Video;

public class SimulatedVideoPlayerTests
{
    /// <summary>A clock that moves only when told to; its timers never fire (tests call <see cref="SimulatedVideoPlayer.Tick"/>).</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _ticks = 1_000_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new InertTimer();

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>Queues posted callbacks so a test can tell which ran on this context and when.</summary>
    private sealed class QueueContext : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = [];

        public override void Post(SendOrPostCallback d, object? state) => Posted.Add((d, state));

        public void Drain()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                while (Posted.Count > 0)
                {
                    var (callback, state) = Posted[0];
                    Posted.RemoveAt(0);
                    callback(state);
                }
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }

    private static (SimulatedVideoPlayer Player, ManualClock Clock) Make(double seconds = 2, int width = 320, int height = 240)
    {
        var clock = new ManualClock();
        var player = (SimulatedVideoPlayer)new SimulatedVideoBackend(clock).CreatePlayer();
        player.LoadAsync(VideoSource.FromSimulated(new PixelSize(width, height), TimeSpan.FromSeconds(seconds))).GetAwaiter().GetResult();
        return (player, clock);
    }

    private static long ShownFrame(SimulatedVideoPlayer player, PixelSize target = default)
    {
        var request = new VideoFrameRequest(TimeSpan.Zero, target, VideoFrameKinds.CpuBgra);
        if (!player.TryAcquireFrame(in request, out var lease))
        {
            return -2;
        }
        try
        {
            return SimulatedVideoPlayer.ReadFrameNumber(lease.Pixels, lease.Size, lease.RowBytes);
        }
        finally
        {
            lease.Dispose();
        }
    }

    [AvaloniaFact]
    public void Load_ends_paused_at_zero_with_size_and_length()
    {
        var (player, _) = Make();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Position.ShouldBe(TimeSpan.Zero);
        player.Duration.ShouldBe(TimeSpan.FromSeconds(2));
        player.NaturalSize.ShouldBe(new PixelSize(320, 240));
        player.IsSeekable.ShouldBeTrue();
        player.Buffered.ShouldBe([new VideoTimeRange(TimeSpan.Zero, TimeSpan.FromSeconds(2))]);
        ShownFrame(player).ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_plain_source_loads_as_the_fallback_clip()
    {
        var player = new SimulatedVideoBackend(new ManualClock()).CreatePlayer();
        player.LoadAsync(VideoSource.FromUri(new Uri("https://example.invalid/clip.mp4"))).GetAwaiter().GetResult();
        player.Duration.ShouldBe(SimulatedVideoBackend.FallbackDuration);
        player.NaturalSize.ShouldBe(SimulatedVideoBackend.FallbackSize);
    }

    [AvaloniaFact]
    public void Nothing_plays_before_loading()
    {
        var player = new SimulatedVideoBackend(new ManualClock()).CreatePlayer();
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Idle);
        player.Seek(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.Zero);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        player.TryAcquireFrame(in request, out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Clock_runs_only_while_playing()
    {
        var (player, clock) = Make();
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.Zero);
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(500));
        ShownFrame(player).ShouldBe(15);
        player.Pause();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [AvaloniaFact]
    public void The_same_frame_is_handed_out_once()
    {
        var (player, clock) = Make();
        ShownFrame(player).ShouldBe(0);
        ShownFrame(player).ShouldBe(-2);
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        ShownFrame(player).ShouldBe(-2);
        clock.Advance(TimeSpan.FromMilliseconds(30));
        ShownFrame(player).ShouldBe(1);
        player.FramesDelivered.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_frame_lent_out_is_not_lent_again_until_released()
    {
        var (player, clock) = Make();
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        player.TryAcquireFrame(in request, out var held).ShouldBeTrue();
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        player.TryAcquireFrame(in request, out _).ShouldBeFalse();
        held.Dispose();
        player.TryAcquireFrame(in request, out var next).ShouldBeTrue();
        next.Dispose();
    }

    [AvaloniaFact]
    public void Frames_shrink_to_the_target_size()
    {
        var (player, _) = Make(width: 3840, height: 2160);
        var request = new VideoFrameRequest(TimeSpan.Zero, new PixelSize(400, 300), VideoFrameKinds.CpuBgra);
        player.TryAcquireFrame(in request, out var lease).ShouldBeTrue();
        lease.Size.ShouldBe(new PixelSize(400, 225));
        lease.RowBytes.ShouldBe(1600);
        lease.CpuPixelFormat.ShouldBe(VideoCpuPixelFormat.Bgra8888);
        SimulatedVideoPlayer.ReadFrameNumber(lease.Pixels, lease.Size, lease.RowBytes).ShouldBe(0);
        lease.Dispose();
    }

    [AvaloniaFact]
    public void A_GPU_only_request_gets_nothing()
    {
        var (player, _) = Make();
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.Gpu);
        player.TryAcquireFrame(in request, out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_new_frame_target_hands_out_the_current_frame_again()
    {
        var (player, _) = Make();
        ShownFrame(player).ShouldBe(0);
        ShownFrame(player).ShouldBe(-2);
        player.FrameTarget = new VideoFrameTarget();
        ShownFrame(player).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Accurate_seek_lands_on_the_time_and_fast_seek_on_the_second()
    {
        var (player, _) = Make(seconds: 10);
        var frames = 0;
        player.FrameAvailable += (_, _) => frames++;
        player.Seek(TimeSpan.FromMilliseconds(2500));
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(2500));
        ShownFrame(player).ShouldBe(75);
        player.Seek(TimeSpan.FromMilliseconds(4700), VideoSeekMode.Fast);
        player.Position.ShouldBe(TimeSpan.FromSeconds(4));
        ShownFrame(player).ShouldBe(120);
        frames.ShouldBe(2);
        player.Seek(TimeSpan.FromSeconds(-3));
        player.Position.ShouldBe(TimeSpan.Zero);
        player.Seek(TimeSpan.FromSeconds(30));
        player.Position.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [AvaloniaFact]
    public void Seeking_while_playing_keeps_playing_from_there()
    {
        var (player, clock) = Make(seconds: 10);
        var frames = 0;
        player.FrameAvailable += (_, _) => frames++;
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Seek(TimeSpan.FromSeconds(5));
        player.Position.ShouldBe(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(5500));
        player.State.ShouldBe(VideoPlaybackState.Playing);
        frames.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Playing_to_the_end_ends_and_play_starts_over()
    {
        var (player, clock) = Make();
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(1900));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Ended);
        player.Position.ShouldBe(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.FromSeconds(2));
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void Seeking_back_from_the_end_pauses_there()
    {
        var (player, clock) = Make();
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(3));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Ended);
        player.Seek(TimeSpan.FromSeconds(1));
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Position.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [AvaloniaFact]
    public void Looping_wraps_and_never_ends()
    {
        var (player, clock) = Make();
        player.IsLooping = true;
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(4500));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [AvaloniaFact]
    public void Turning_looping_off_keeps_the_position_within_the_clip()
    {
        var (player, clock) = Make();
        player.IsLooping = true;
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(4500));
        player.IsLooping = false;
        player.Position.ShouldBe(TimeSpan.FromMilliseconds(500));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Playing);
        clock.Advance(TimeSpan.FromMilliseconds(1600));
        player.Tick();
        player.State.ShouldBe(VideoPlaybackState.Ended);
    }

    [AvaloniaFact]
    public void Rate_changes_apply_from_now()
    {
        var (player, clock) = Make(seconds: 20);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(2));
        player.PlaybackRate = 2;
        player.Position.ShouldBe(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.FromSeconds(4));
        player.PlaybackRate = 0.5;
        clock.Advance(TimeSpan.FromSeconds(2));
        player.Position.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [AvaloniaFact]
    public void Rate_is_clamped()
    {
        var (player, _) = Make();
        player.PlaybackRate = 10;
        player.PlaybackRate.ShouldBe(VideoPlayerBase.MaxPlaybackRate);
        player.PlaybackRate = 0.1;
        player.PlaybackRate.ShouldBe(VideoPlayerBase.MinPlaybackRate);
        player.PlaybackRate = double.NaN;
        player.PlaybackRate.ShouldBe(1);
        player.PlaybackRate = -2;
        player.PlaybackRate.ShouldBe(1);
        player.Volume = 3;
        player.Volume.ShouldBe(1);
        player.Volume = -1;
        player.Volume.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Buffering_stops_the_clock_and_resumes()
    {
        var (player, clock) = Make(seconds: 10);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        player.SetBuffering(true);
        player.State.ShouldBe(VideoPlaybackState.Buffering);
        clock.Advance(TimeSpan.FromSeconds(3));
        player.Position.ShouldBe(TimeSpan.FromSeconds(1));
        player.SetBuffering(false);
        player.State.ShouldBe(VideoPlaybackState.Playing);
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [AvaloniaFact]
    public void Play_while_starved_waits_and_pause_leaves_buffering()
    {
        var (player, clock) = Make(seconds: 10);
        player.SetBuffering(true);
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Buffering);
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Position.ShouldBe(TimeSpan.Zero);
        player.Pause();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.SetBuffering(false);
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Playing);
    }

    [AvaloniaFact]
    public void Failure_stops_playback_until_loaded_again()
    {
        var (player, clock) = Make(seconds: 10);
        VideoErrorEventArgs? error = null;
        player.Failed += (_, e) => error = e;
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        player.SimulateFailure(VideoErrorKind.Decode, "bad data");
        player.State.ShouldBe(VideoPlaybackState.Failed);
        error.ShouldNotBeNull();
        error.Kind.ShouldBe(VideoErrorKind.Decode);
        error.Message.ShouldBe("bad data");
        player.Play();
        player.State.ShouldBe(VideoPlaybackState.Failed);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        player.TryAcquireFrame(in request, out _).ShouldBeFalse();
        player.LoadAsync(VideoSource.FromSimulated(new PixelSize(320, 240), TimeSpan.FromSeconds(10))).GetAwaiter().GetResult();
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void Load_reports_its_states_and_honours_cancellation()
    {
        var player = new SimulatedVideoBackend(new ManualClock()).CreatePlayer();
        var states = new List<VideoPlaybackState>();
        player.StateChanged += (_, _) => states.Add(player.State);
        player.LoadAsync(VideoSource.FromSimulated(new PixelSize(64, 48), TimeSpan.FromSeconds(1))).GetAwaiter().GetResult();
        states.ShouldBe([VideoPlaybackState.Loading, VideoPlaybackState.Paused]);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Should.Throw<OperationCanceledException>(() => player.LoadAsync(VideoSource.FromSimulated(new PixelSize(64, 48), TimeSpan.FromSeconds(1)), cancelled.Token));
    }

    [AvaloniaFact]
    public void Events_are_raised_on_the_creating_context()
    {
        var previous = SynchronizationContext.Current;
        var context = new QueueContext();
        SimulatedVideoPlayer player;
        ManualClock clock;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            clock = new ManualClock();
            player = (SimulatedVideoPlayer)new SimulatedVideoBackend(clock).CreatePlayer();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        var seen = new List<string>();
        player.StateChanged += (_, _) => seen.Add($"state:{player.State}:{SynchronizationContext.Current == context}");
        player.DurationChanged += (_, _) => seen.Add($"duration:{SynchronizationContext.Current == context}");
        player.FrameAvailable += (_, _) => seen.Add($"frame:{SynchronizationContext.Current == context}");

        // Called from another context: nothing runs inline, everything is queued on the player's.
        player.LoadAsync(VideoSource.FromSimulated(new PixelSize(64, 48), TimeSpan.FromSeconds(1))).GetAwaiter().GetResult();
        seen.ShouldBeEmpty();
        context.Posted.Count.ShouldBeGreaterThan(0);
        context.Drain();
        seen.ShouldContain("duration:True");
        seen.ShouldContain("frame:True");
        seen.ShouldContain("state:Paused:True");
        seen.ShouldAllBe(s => s.EndsWith(":True"));
    }

    [AvaloniaFact]
    public void Disposed_players_hand_out_nothing_and_raise_nothing()
    {
        var (player, clock) = Make();
        var raised = 0;
        player.StateChanged += (_, _) => raised++;
        player.Dispose();
        player.Dispose();
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        player.TryAcquireFrame(in request, out _).ShouldBeFalse();
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        player.Tick();
        raised.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_frame_number_reads_back_at_any_size()
    {
        var (player, clock) = Make(seconds: 100, width: 1280, height: 720);
        player.Seek(TimeSpan.FromSeconds(60));
        ShownFrame(player, new PixelSize(160, 90)).ShouldBe(1800);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        ShownFrame(player, new PixelSize(640, 360)).ShouldBe(1830);
    }
}
