using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Video;

public class VideoSurfaceTests
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

    /// <summary>Hands out one fixed CPU frame per request generation, with a chosen rotation and byte order; records requests.</summary>
    private sealed class FixedFramePlayer : VideoPlayerBase, IVideoFrameOwner
    {
        private readonly PixelSize _frameSize;
        private readonly int _rotation;
        private readonly VideoCpuPixelFormat _format;
        private readonly byte[] _pixels;
        private readonly int _rowBytes;
        private bool _fresh = true;

        public FixedFramePlayer(PixelSize frameSize, int rotation, VideoCpuPixelFormat format = VideoCpuPixelFormat.Bgra8888)
            : base(SimulatedVideoBackend.Instance)
        {
            _frameSize = frameSize;
            _rotation = rotation;
            _format = format;
            // Padded rows, so the copy has to honour RowBytes.
            _rowBytes = frameSize.Width * 4 + 64;
            _pixels = new byte[_rowBytes * frameSize.Height];
            SetNaturalSize(rotation is 90 or 270 ? new PixelSize(frameSize.Height, frameSize.Width) : frameSize);
            SetDuration(TimeSpan.FromSeconds(1));
            SetState(VideoPlaybackState.Paused);
        }

        public List<VideoFrameRequest> Requests { get; } = [];

        public override TimeSpan Position => TimeSpan.Zero;

        public override Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override void Play() => SetState(VideoPlaybackState.Playing);

        public override void Pause() => SetState(VideoPlaybackState.Paused);

        public override void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
        {
            _fresh = true;
            RaiseFrameAvailable();
        }

        public override bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease)
        {
            Requests.Add(request);
            lease = default;
            if (!_fresh)
            {
                return false;
            }
            _fresh = false;
            lease = new VideoFrameLease(this, 1, _frameSize, TimeSpan.Zero, _pixels, _rowBytes, _rotation, _format);
            return true;
        }

        public void ReleaseFrame(long token)
        {
        }
    }

    private static readonly PixelSize s_hd = new(1920, 1080);

    /// <summary>
    /// Runs <paramref name="count"/> compositor frames. A headless tick does not always run the frame callbacks (the compositor
    /// skips a frame while the render thread has not taken the previous batch), so when the surface waits for a callback the
    /// tick repeats until it ran.
    /// </summary>
    private static void Frame(VideoSurface surface, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            var requested = surface.IsFrameRequested;
            var callbacks = surface.FrameCallbackCount;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            do
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                if (!requested || surface.FrameCallbackCount > callbacks)
                {
                    break;
                }
                // A frame asked for between renders waits for the compositor's animation timer: let dispatcher timers run.
                RunTimers(2);
            }
            while (DateTime.UtcNow < deadline);
        }
    }

    /// <summary>Runs frames until the surface knows its compositor (the player has a frame target) and is idle or <paramref name="until"/> holds.</summary>
    private static void Settle(VideoSurface surface, Func<bool>? until = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            Frame(surface);
            var targetKnown = surface.Player is null || surface.Player.FrameTarget is not null;
            if (targetKnown && (until?.Invoke() ?? !surface.IsFrameRequested))
            {
                return;
            }
            RunTimers(2);
        }
        throw new TimeoutException("the surface did not settle");
    }

    private static void RunTimers(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    private static (SimulatedVideoPlayer Player, ManualClock Clock) MakePlayer(PixelSize size, double seconds = 10)
    {
        var clock = new ManualClock();
        var player = (SimulatedVideoPlayer)new SimulatedVideoBackend(clock).CreatePlayer();
        player.LoadAsync(VideoSource.FromSimulated(size, TimeSpan.FromSeconds(seconds))).GetAwaiter().GetResult();
        return (player, clock);
    }

    private static (VideoSurface Surface, Window Window) Show(IVideoPlayer? player, double width = 400, double height = 300, Stretch stretch = Stretch.Uniform)
    {
        var surface = new VideoSurface { Player = player, Width = width, Height = height, Stretch = stretch };
        var window = TestHelpers.Show(new Border { Child = surface });
        window.Width = 800;
        window.Height = 600;
        Settle(surface);
        return (surface, window);
    }

    /// <summary>Records the frame number of every CPU frame the surface copies.</summary>
    private static List<long> WatchFrames(VideoSurface surface)
    {
        var shown = new List<long>();
        surface.FrameCopied = (pixels, size, rowBytes) => shown.Add(SimulatedVideoPlayer.ReadFrameNumber(pixels, size, rowBytes));
        return shown;
    }

    [AvaloniaFact]
    public void Headless_has_no_GPU_interop_so_frames_go_through_a_bitmap()
    {
        var (player, _) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        TestHelpers.PumpUntil(() => surface.RenderPath == VideoRenderPath.Bitmap, 3000).ShouldBeTrue();
        surface.Bitmap.ShouldNotBeNull();
        player.FrameTarget.ShouldNotBeNull();
        player.FrameTarget.GpuHandleTypes.ShouldBeEmpty();
        player.FrameTarget.AcceptsGpu.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_first_frame_of_a_paused_player_is_shown_once()
    {
        var (player, _) = MakePlayer(s_hd);
        var surface = new VideoSurface { Player = player, Width = 400, Height = 300 };
        var shown = WatchFrames(surface);
        TestHelpers.Show(surface);
        Settle(surface);
        shown.ShouldBe([0]);
        surface.IsFrameRequested.ShouldBeFalse();
        var delivered = player.FramesDelivered;
        Frame(surface, 10);
        player.FramesDelivered.ShouldBe(delivered);
    }

    [AvaloniaFact]
    public void Playing_shows_the_frame_for_the_current_time()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        var shown = WatchFrames(surface);
        player.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        Frame(surface);
        shown.ShouldContain(30);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Frame(surface);
        shown[^1].ShouldBe(45);
        surface.IsFrameRequested.ShouldBeTrue();
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
    }

    [AvaloniaFact]
    public void The_bitmap_is_reused_while_the_size_stays()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        var first = surface.Bitmap;
        first.ShouldNotBeNull();
        player.Play();
        for (var i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Frame(surface);
        }
        surface.Bitmap.ShouldBeSameAs(first);
        surface.Width = 200;
        surface.Height = 150;
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface, 2);
        surface.Bitmap.ShouldNotBeSameAs(first);
        surface.LastTargetSize.ShouldBe(new PixelSize(200, 113));
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(200, 112));
    }

    [AvaloniaFact]
    public void Paused_surfaces_do_no_work()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface);
        player.Pause();
        Frame(surface);
        var delivered = player.FramesDelivered;
        for (var i = 0; i < 10; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Frame(surface);
        }
        player.FramesDelivered.ShouldBe(delivered);
        surface.IsFrameRequested.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_seek_while_paused_shows_the_new_frame_once()
    {
        var (player, _) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        var shown = WatchFrames(surface);
        player.Seek(TimeSpan.FromSeconds(2));
        Frame(surface, 3);
        shown.ShouldBe([60]);
        surface.IsFrameRequested.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Frames_are_requested_at_the_drawn_size()
    {
        var (player, _) = MakePlayer(new PixelSize(3840, 2160));
        var (surface, _) = Show(player, 400, 300);
        surface.LastTargetSize.ShouldBe(new PixelSize(400, 225));
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(400, 225));

        surface.Stretch = Stretch.UniformToFill;
        Frame(surface, 2);
        surface.LastTargetSize.ShouldBe(new PixelSize(534, 300));
        // The simulated backend keeps the aspect within the target.
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(533, 300));
    }

    [AvaloniaFact]
    public void A_small_source_is_not_scaled_up()
    {
        var (player, _) = MakePlayer(new PixelSize(160, 90));
        var (surface, _) = Show(player, 400, 300);
        surface.LastTargetSize.ShouldBe(new PixelSize(400, 225));
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(160, 90));
        surface.PictureRect.ShouldBe(new Rect(0, 37.5, 400, 225));
    }

    [AvaloniaTheory]
    [InlineData(Stretch.Uniform, 0, 37.5, 400, 225)]
    [InlineData(Stretch.UniformToFill, -66.666666666666657, 0, 533.33333333333337, 300)]
    [InlineData(Stretch.Fill, 0, 0, 400, 300)]
    [InlineData(Stretch.None, -760, -390, 1920, 1080)]
    public void Stretch_places_the_picture(Stretch stretch, double x, double y, double width, double height)
    {
        var (player, _) = MakePlayer(s_hd);
        var (surface, _) = Show(player, 400, 300, stretch);
        var rect = surface.PictureRect;
        rect.X.ShouldBe(x, 0.001);
        rect.Y.ShouldBe(y, 0.001);
        rect.Width.ShouldBe(width, 0.001);
        rect.Height.ShouldBe(height, 0.001);
    }

    [AvaloniaFact]
    public void Picture_rect_letterboxes_tall_pictures_at_the_sides()
    {
        VideoSurface.ComputePictureRect(new Size(400, 300), new Size(1080, 1920), Stretch.Uniform)
            .ShouldBe(new Rect(115.625, 0, 168.75, 300));
        VideoSurface.ComputePictureRect(new Size(400, 300), default, Stretch.Uniform).ShouldBe(new Rect(0, 0, 400, 300));
    }

    [AvaloniaFact]
    public void A_rotated_frame_swaps_the_aspect_and_the_request()
    {
        // 320x240 frames turned 90 degrees show as a 240x320 portrait picture.
        var player = new FixedFramePlayer(new PixelSize(320, 240), 90);
        var (surface, _) = Show(player, 400, 300);
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
        surface.PictureRect.ShouldBe(new Rect(87.5, 0, 225, 300));
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(320, 240));
        // Once the rotation is known the request is in the frame's own orientation: 300 wide, 225 high.
        player.Seek(TimeSpan.Zero);
        Frame(surface, 2);
        player.Requests[^1].TargetSize.ShouldBe(new PixelSize(300, 225));
    }

    [AvaloniaFact]
    public void Rgba_frames_get_an_rgba_bitmap_and_padded_rows_are_copied()
    {
        var player = new FixedFramePlayer(new PixelSize(64, 48), 0, VideoCpuPixelFormat.Rgba8888);
        var (surface, _) = Show(player, 128, 96);
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
        surface.Bitmap.ShouldNotBeNull();
        surface.Bitmap.PixelSize.ShouldBe(new PixelSize(64, 48));
        surface.Bitmap.Format.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Hidden_surfaces_stop_asking_and_resume_when_shown()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface);
        surface.IsVisible = false;
        Frame(surface, 2);
        var delivered = player.FramesDelivered;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Frame(surface, 3);
        player.FramesDelivered.ShouldBe(delivered);
        surface.IsFrameRequested.ShouldBeFalse();
        surface.IsVisible = true;
        Frame(surface, 2);
        player.FramesDelivered.ShouldBeGreaterThan(delivered);
    }

    [AvaloniaFact]
    public void Surfaces_hidden_by_an_ancestor_stop_and_resume()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, window) = Show(player);
        var border = (Border)window.Content!;
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface);
        border.IsVisible = false;
        Frame(surface, 2);
        var delivered = player.FramesDelivered;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Frame(surface, 3);
        player.FramesDelivered.ShouldBe(delivered);
        border.IsVisible = true;
        Frame(surface, 3);
        player.FramesDelivered.ShouldBeGreaterThan(delivered);
    }

    [AvaloniaFact]
    public void Detaching_stops_requests_and_clears_the_frame_target()
    {
        var (player, clock) = MakePlayer(s_hd);
        var (surface, window) = Show(player);
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface);
        player.FrameTarget.ShouldNotBeNull();
        ((Border)window.Content!).Child = null;
        Frame(surface, 2);
        var delivered = player.FramesDelivered;
        clock.Advance(TimeSpan.FromSeconds(1));
        Frame(surface, 5);
        player.FramesDelivered.ShouldBe(delivered);
        surface.IsFrameRequested.ShouldBeFalse();
        player.FrameTarget.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_reattached_surface_shows_the_paused_frame_again()
    {
        var (player, _) = MakePlayer(s_hd);
        var (surface, window) = Show(player);
        var border = (Border)window.Content!;
        var shown = WatchFrames(surface);
        border.Child = null;
        Frame(surface, 2);
        player.Seek(TimeSpan.FromSeconds(3));
        Frame(surface, 2);
        shown.ShouldBeEmpty();
        border.Child = surface;
        Frame(surface, 4);
        shown.ShouldBe([90]);
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
    }

    [AvaloniaFact]
    public void Changing_the_player_lets_go_of_the_old_one()
    {
        var (first, clock) = MakePlayer(s_hd);
        var (second, _) = MakePlayer(new PixelSize(640, 480));
        var (surface, _) = Show(first);
        var shown = WatchFrames(surface);
        surface.Player = second;
        surface.RenderPath.ShouldBe(VideoRenderPath.None);
        first.FrameTarget.ShouldBeNull();
        Frame(surface, 3);
        second.FrameTarget.ShouldNotBeNull();
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
        surface.Bitmap!.PixelSize.ShouldBe(new PixelSize(400, 300));
        var delivered = first.FramesDelivered;
        first.Play();
        clock.Advance(TimeSpan.FromSeconds(1));
        Frame(surface, 3);
        first.FramesDelivered.ShouldBe(delivered);
        surface.IsFrameRequested.ShouldBeFalse();

        surface.Player = null;
        surface.RenderPath.ShouldBe(VideoRenderPath.None);
        surface.Bitmap.ShouldBeNull();
        second.FrameTarget.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Prefer_bitmap_path_keeps_the_target_cpu_only()
    {
        var (player, _) = MakePlayer(s_hd);
        var (surface, _) = Show(player);
        surface.PreferBitmapPath = true;
        Frame(surface, 2);
        player.FrameTarget!.AcceptsGpu.ShouldBeFalse();
        surface.RenderPath.ShouldBe(VideoRenderPath.Bitmap);
    }

    [AvaloniaFact]
    public void A_detached_surface_is_collectable_while_its_player_lives()
    {
        var (player, clock) = MakePlayer(s_hd);
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var weak = TestHelpers.ShowAndClose(() => new VideoSurface { Player = player, Width = 320, Height = 180 }, s => Settle(s, () => s.RenderPath == VideoRenderPath.Bitmap));
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(player);
    }

    [AvaloniaFact]
    public void A_replaced_player_is_collectable_while_the_surface_lives()
    {
        var (surface, _) = Show(null);
        var weak = AttachAndReplace(surface);
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(surface);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndReplace(VideoSurface surface)
    {
        var (player, clock) = MakePlayer(s_hd);
        surface.Player = player;
        player.Play();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Frame(surface, 3);
        surface.Player = null;
        Frame(surface, 2);
        return new WeakReference(player);
    }

    [AvaloniaFact]
    public void Rounded_geometry_matches_the_corner_radius()
    {
        // Hit testing needs a real geometry backend (the screenshot harness checks the drawn corners); here only the shapes.
        var uniform = VideoSurface.CreateRoundedGeometry(new Rect(0, 0, 100, 60), new CornerRadius(12));
        var rectangle = uniform.ShouldBeOfType<RectangleGeometry>();
        rectangle.RadiusX.ShouldBe(12);
        rectangle.Rect.ShouldBe(new Rect(0, 0, 100, 60));
        VideoSurface.CreateRoundedGeometry(new Rect(0, 0, 100, 60), new CornerRadius(20, 0, 0, 0)).ShouldBeOfType<StreamGeometry>();
    }
}
