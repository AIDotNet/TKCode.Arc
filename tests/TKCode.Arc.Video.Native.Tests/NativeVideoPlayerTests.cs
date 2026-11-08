using System.Diagnostics;
using Avalonia;
using Avalonia.Platform;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Video.Native.Tests;

/// <summary>
/// The native backend against the repository's test clip (tests/Video/Media/red-blue-2s.mp4: 320x240, 0-1 s red, 1-2 s blue).
/// Platform-neutral: every shim implements the same ABI. On macOS the library must load; elsewhere the tests skip when the
/// library (or its framework, e.g. GStreamer) is missing.
/// </summary>
[Collection(nameof(NativeVideoCollection))]
public sealed class NativeVideoPlayerTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static string ClipPath => Path.Combine(AppContext.BaseDirectory, "Media", "red-blue-2s.mp4");

    // What the player last did, for failure messages: a timeout alone does not say whether playback stalled or failed.
    // The tests in this collection run one at a time.
    private static IVideoPlayer? s_watched;
    private static string? s_lastFailure;

    [Fact]
    public void Use_selects_the_platform_backend()
    {
        var backend = RequireBackend(needsMainLoop: false);
        backend.IsReal.ShouldBeTrue();
        var expected = OperatingSystem.IsWindows() ? "mediafoundation" : OperatingSystem.IsMacOS() ? "avfoundation" : "gstreamer";
        backend.Name.ShouldBe(expected);
        backend.FrameKinds.HasFlag(VideoFrameKinds.CpuBgra).ShouldBeTrue();
        if (OperatingSystem.IsMacOS())
        {
            backend.FrameKinds.HasFlag(VideoFrameKinds.Gpu).ShouldBeTrue();
            backend.GpuHandleTypes.ShouldBe([KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef]);
        }
        ArcVideoNative.Use().ShouldBeTrue();
        ArcVideo.Backend.ShouldBeSameAs(backend);
    }

    [Fact]
    public void CanPlay_reports_mp4()
    {
        var backend = RequireBackend(needsMainLoop: false);
        backend.CanPlay("video/mp4").ShouldNotBe(VideoSupport.No);
        backend.CanPlay("video/mp4; codecs=\"avc1.42E01E, mp4a.40.2\"").ShouldNotBe(VideoSupport.No);
        backend.CanPlay("").ShouldBe(VideoSupport.No);
        backend.CanPlay("application/x-not-a-video").ShouldBe(VideoSupport.No);
        if (OperatingSystem.IsMacOS())
        {
            backend.CanPlay("video/mp4; codecs=\"avc1.42E01E\"").ShouldBe(VideoSupport.Probably);
            backend.CanPlay("video/webm").ShouldBe(VideoSupport.No);
        }
    }

    [Fact]
    public async Task Load_reports_duration_size_and_pauses()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        var states = new List<VideoPlaybackState>();
        player.StateChanged += (_, _) =>
        {
            lock (states)
            {
                states.Add(player.State);
            }
        };
        await LoadClipAsync(player);
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.Duration.TotalSeconds.ShouldBe(2.0, 0.1);
        player.NaturalSize.ShouldBe(new PixelSize(320, 240));
        player.IsSeekable.ShouldBeTrue();
        player.Position.TotalSeconds.ShouldBe(0, 0.05);
        await WaitUntilAsync(() => player.Buffered.Count > 0, "buffered ranges");
        player.Buffered[0].End.TotalSeconds.ShouldBeGreaterThan(1.5);
    }

    [Fact]
    public async Task First_cpu_frame_is_red()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var pixel = await AcquireCenterPixelAsync(player, VideoFrameKinds.CpuBgra, expectedTime: 0);
        AssertColor(pixel, b: 0, g: 0, r: 255);
    }

    [Fact]
    public async Task FrameAvailable_is_raised_after_load_and_seek()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        var frames = 0;
        player.FrameAvailable += (_, _) => Interlocked.Increment(ref frames);
        await LoadClipAsync(player);
        await WaitUntilAsync(() => Volatile.Read(ref frames) >= 1, "FrameAvailable after load");
        player.Seek(TimeSpan.FromSeconds(1.5));
        await WaitUntilAsync(() => Volatile.Read(ref frames) >= 2, "FrameAvailable after seek");
    }

    [Fact]
    public async Task Accurate_seek_shows_blue()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var positionEvents = 0;
        player.PositionChanged += (_, _) => Interlocked.Increment(ref positionEvents);
        player.Seek(TimeSpan.FromSeconds(1.5));
        var pixel = await AcquireCenterPixelAsync(player, VideoFrameKinds.CpuBgra, expectedTime: 1.5);
        AssertColor(pixel, b: 255, g: 0, r: 0);
        player.Position.TotalSeconds.ShouldBe(1.5, 0.05);
        await WaitUntilAsync(() => Volatile.Read(ref positionEvents) > 0, "PositionChanged after seek");
        player.State.ShouldBe(VideoPlaybackState.Paused);
    }

    [Fact]
    public async Task Gpu_frame_is_a_platform_image()
    {
        var backend = RequireBackend();
        if (!backend.FrameKinds.HasFlag(VideoFrameKinds.Gpu))
        {
            Assert.Skip($"{backend.Name} hands out CPU frames only.");
        }
        using var player = backend.CreatePlayer();
        player.FrameTarget = new VideoFrameTarget { GpuHandleTypes = backend.GpuHandleTypes };
        await LoadClipAsync(player);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.Gpu | VideoFrameKinds.CpuBgra);
        var got = await WaitForFrameAsync(player, request, lease =>
        {
            if (OperatingSystem.IsWindows() && lease.Kind == VideoFrameKinds.CpuBgra)
            {
                // A device without video support (a VM) plays in software and hands out CPU frames only.
                return true;
            }
            lease.Kind.ShouldBe(VideoFrameKinds.Gpu);
            lease.GpuImage.ShouldNotBeNull();
            lease.GpuImage.Handle.ShouldNotBe(0);
            lease.GpuImage.HandleDescriptor.ShouldBe(backend.GpuHandleTypes[0]);
            lease.Size.ShouldBe(new PixelSize(320, 240));
            lease.Pixels.IsEmpty.ShouldBeTrue();
            if (OperatingSystem.IsMacOS())
            {
                lease.GpuImage.HandleDescriptor.ShouldBe(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef);
                lease.GpuImageId.ShouldNotBe(0);
                lease.KeyedMutexAcquireKey.ShouldBeNull();
            }
            return true;
        });
        got.ShouldBeTrue();
    }

    [Fact]
    public async Task Cpu_frames_only_when_gpu_is_not_preferred()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer(new VideoPlayerOptions { PreferGpuFrames = false });
        player.FrameTarget = new VideoFrameTarget { GpuHandleTypes = backend.GpuHandleTypes };
        await LoadClipAsync(player);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.Gpu | VideoFrameKinds.CpuBgra);
        var got = await WaitForFrameAsync(player, request, lease =>
        {
            lease.Kind.ShouldBe(VideoFrameKinds.CpuBgra);
            lease.CpuPixelFormat.ShouldBe(VideoCpuPixelFormat.Bgra8888);
            lease.RowBytes.ShouldBeGreaterThanOrEqualTo(320 * 4);
            return true;
        });
        got.ShouldBeTrue();
    }

    [Fact]
    public async Task Plays_to_the_end()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var positions = 0;
        player.PositionChanged += (_, _) => Interlocked.Increment(ref positions);
        player.Play();
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Playing, "Playing");
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Ended, "Ended");
        player.Position.TotalSeconds.ShouldBe(2.0, 0.15);
        Volatile.Read(ref positions).ShouldBeGreaterThanOrEqualTo(4);

        // Play at the end starts over.
        player.Play();
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Playing, "Playing again");
        await WaitUntilAsync(() => player.Position < TimeSpan.FromSeconds(1.5) && player.Position > TimeSpan.Zero, "restarted from the beginning");
        player.Pause();
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Paused, "Paused");
    }

    [Fact]
    public async Task Loops()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        player.IsLooping = true;
        await LoadClipAsync(player);
        var maximum = 0.0;
        var wrapped = false;
        player.PositionChanged += (_, _) =>
        {
            var seconds = player.Position.TotalSeconds;
            if (seconds > maximum)
            {
                maximum = seconds;
            }
            else if (maximum > 1.5 && seconds < 0.5)
            {
                wrapped = true;
            }
        };
        player.Play();
        await WaitUntilAsync(() => Volatile.Read(ref wrapped), "wrapped to the start", TimeSpan.FromSeconds(8));
        player.State.ShouldNotBe(VideoPlaybackState.Ended);
        player.Pause();
    }

    [Fact]
    public async Task Plays_at_double_rate()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        player.PlaybackRate = 2;
        player.Play();
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Playing && player.Position > TimeSpan.FromSeconds(0.1), "Playing");
        var start = player.Position;
        var clock = Stopwatch.StartNew();
        await Task.Delay(400);
        var advanced = (player.Position - start).TotalSeconds / clock.Elapsed.TotalSeconds;
        player.Pause();
        advanced.ShouldBe(2.0, 0.5);
    }

    [Fact]
    public async Task A_clip_without_sound_plays()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player, "red-blue-2s-no-audio.mp4");
        player.Play();
        await WaitUntilAsync(() => player.Position > TimeSpan.FromSeconds(0.3), "a clip without sound to play");
        player.Pause();
    }

    [Fact]
    public async Task Mute_keeps_volume_and_playback()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        player.Volume = 0.5;
        player.IsMuted = true;
        await LoadClipAsync(player);
        player.Play();
        await WaitUntilAsync(() => player.Position > TimeSpan.FromSeconds(0.3), "playing while muted");
        player.IsMuted = false;
        player.Volume.ShouldBe(0.5);
        player.Pause();
    }

    [Fact]
    public async Task Missing_file_fails_with_a_kind()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        VideoErrorEventArgs? error = null;
        player.Failed += (_, e) => error = e;
        await player.LoadAsync(VideoSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Media", "missing.mp4"))).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Failed);
        await WaitUntilAsync(() => error is not null, "Failed event");
        error!.Kind.ShouldBe(VideoErrorKind.Network, error.Message);
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Garbage_file_fails()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await player.LoadAsync(VideoSource.FromBytes(new byte[4096], "video/mp4")).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Failed);
    }

    [Fact]
    public async Task Reload_after_failure_works()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await player.LoadAsync(VideoSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Media", "missing.mp4"))).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Failed);
        await LoadClipAsync(player);
        player.State.ShouldBe(VideoPlaybackState.Paused);
        player.NaturalSize.ShouldBe(new PixelSize(320, 240));
    }

    [Fact]
    public async Task Load_honors_cancellation()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => player.LoadAsync(VideoSource.FromFile(ClipPath), cancelled.Token));

        // Cancelled while the system player loads: either the load wins or it is cancelled; the player stays usable.
        using var soon = new CancellationTokenSource();
        var load = player.LoadAsync(VideoSource.FromFile(ClipPath), soon.Token);
        await soon.CancelAsync();
        try
        {
            await load.WaitAsync(s_timeout);
        }
        catch (OperationCanceledException)
        {
            player.State.ShouldBe(VideoPlaybackState.Idle);
        }
        await LoadClipAsync(player);
        player.State.ShouldBe(VideoPlaybackState.Paused);
    }

    [Fact]
    public async Task Dispose_with_callbacks_in_flight_is_safe()
    {
        var backend = RequireBackend();
        for (var i = 0; i < 10; i++)
        {
            var player = backend.CreatePlayer();
            var load = player.LoadAsync(VideoSource.FromFile(ClipPath));
            if (i % 2 == 0)
            {
                await Task.Delay(30 * i);
                player.Play();
            }
            player.Dispose();
            try
            {
                await load.WaitAsync(s_timeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
        using var survivor = backend.CreatePlayer();
        await LoadClipAsync(survivor);
        survivor.State.ShouldBe(VideoPlaybackState.Paused);
    }

    [Fact]
    public async Task Lease_must_be_returned_before_the_next_frame()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_timeout)
        {
            if (player.TryAcquireFrame(request, out var lease))
            {
                player.TryAcquireFrame(request, out _).ShouldBeFalse();
                lease.Dispose();
                return;
            }
            await Task.Delay(20);
        }
        throw new TimeoutException("no frame");
    }

    [Fact]
    public async Task Load_on_another_thread_waits_for_a_leased_frame()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
        Task? reload = null;
        await WaitUntilAsync(() => (reload = ReloadWhileLeased(player, request)) is not null, "a frame to lease");
        await reload!.WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Paused);
    }

    // Holds a CPU lease, starts a load on the thread pool and checks that it does not reset the media (which releases the
    // native frame) until the lease is returned. Null when no frame could be leased yet.
    private static Task? ReloadWhileLeased(IVideoPlayer player, in VideoFrameRequest request)
    {
        if (!player.TryAcquireFrame(request, out var lease))
        {
            return null;
        }
        Task reload;
        using (lease)
        {
            var first = lease.Pixels[0];
            reload = Task.Run(() => player.LoadAsync(VideoSource.FromFile(ClipPath)));
            Thread.Sleep(300);
            // The load clears the duration only once it holds the media, after the lease is back.
            player.Duration.TotalSeconds.ShouldBe(2.0, 0.1);
            lease.Pixels[0].ShouldBe(first);
        }
        return reload;
    }

    [Fact]
    public async Task Cpu_frames_scale_down_to_the_target_size()
    {
        var backend = RequireBackend();
        using var player = backend.CreatePlayer();
        await LoadClipAsync(player);
        var natural = await AcquireCenterPixelAsync(player, VideoFrameKinds.CpuBgra, 0);

        // 320×240 fitted into 160×160 keeps the aspect: 160×120. The test clip's first frame is a solid block, so the
        // centre pixel survives the scaling. A paused player hands out a frame once; a new frame target asks for it again.
        player.FrameTarget = new VideoFrameTarget();
        (byte B, byte G, byte R, byte A) scaled = default;
        var got = await WaitForFrameAsync(player, new VideoFrameRequest(TimeSpan.Zero, new PixelSize(160, 160), VideoFrameKinds.CpuBgra), lease =>
        {
            lease.Size.ShouldBe(new PixelSize(160, 120));
            lease.RowBytes.ShouldBeGreaterThanOrEqualTo(160 * 4);
            var offset = 60 * lease.RowBytes + 80 * 4;
            var pixels = lease.Pixels;
            scaled = (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
            return true;
        });
        got.ShouldBeTrue("no scaled frame");
        Math.Abs(scaled.B - natural.B).ShouldBeLessThanOrEqualTo(6);
        Math.Abs(scaled.G - natural.G).ShouldBeLessThanOrEqualTo(6);
        Math.Abs(scaled.R - natural.R).ShouldBeLessThanOrEqualTo(6);

        // A target larger than the picture never scales up. (The target is a record: an equal one is no change.)
        player.FrameTarget = null;
        got = await WaitForFrameAsync(player, new VideoFrameRequest(TimeSpan.Zero, new PixelSize(1920, 1080), VideoFrameKinds.CpuBgra), lease =>
        {
            lease.Size.ShouldBe(new PixelSize(320, 240));
            return true;
        });
        got.ShouldBeTrue("no frame at the natural size");
    }

    // ---- Helpers.

    private static NativeVideoBackend RequireBackend(bool needsMainLoop = true)
    {
        if (!ArcVideoNative.Use())
        {
            if (OperatingSystem.IsMacOS())
            {
                Assert.Fail("ArcVideoNative.Use() returned false on macOS: build the shim (tools/video/build-native.sh, tools/video/sync-native.sh).");
            }
            Assert.Skip("tkcode_video or its system framework is not available on this machine.");
        }
        if (needsMainLoop && OperatingSystem.IsMacOS() && !MainRunLoop.IsServiced)
        {
            Assert.Skip("AVFoundation needs the main run loop; run the test executable directly (its Main services it).");
        }
        return (NativeVideoBackend)ArcVideo.Backend;
    }

    private static async Task LoadClipAsync(IVideoPlayer player, string? file = null)
    {
        var path = file is null ? ClipPath : Path.Combine(AppContext.BaseDirectory, "Media", file);
        File.Exists(path).ShouldBeTrue(path);
        s_watched = player;
        s_lastFailure = null;
        player.Failed += (_, e) => s_lastFailure = $"{e.Kind}: {e.Message}";
        await player.LoadAsync(VideoSource.FromFile(path)).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Paused, $"load ended in {player.State}");
    }

    private static async Task<(byte B, byte G, byte R, byte A)> AcquireCenterPixelAsync(IVideoPlayer player, VideoFrameKinds kinds, double expectedTime)
    {
        (byte, byte, byte, byte) pixel = default;
        var request = new VideoFrameRequest(TimeSpan.Zero, default, kinds);
        var got = await WaitForFrameAsync(player, request, lease =>
        {
            if (Math.Abs(lease.PresentationTime.TotalSeconds - expectedTime) > 0.1)
            {
                return false;
            }
            lease.Kind.ShouldBe(VideoFrameKinds.CpuBgra);
            lease.Size.ShouldBe(new PixelSize(320, 240));
            lease.RotationDegrees.ShouldBe(0);
            var offset = lease.Size.Height / 2 * lease.RowBytes + lease.Size.Width / 2 * 4;
            var pixels = lease.Pixels;
            pixel = (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
            return true;
        });
        got.ShouldBeTrue($"no frame at {expectedTime}s");
        return pixel;
    }

    private delegate bool LeaseCheck(VideoFrameLease lease);

    private static async Task<bool> WaitForFrameAsync(IVideoPlayer player, VideoFrameRequest request, LeaseCheck check)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_timeout)
        {
            if (TryCheck(player, request, check))
            {
                return true;
            }
            await Task.Delay(20);
        }
        return false;
    }

    private static bool TryCheck(IVideoPlayer player, in VideoFrameRequest request, LeaseCheck check)
    {
        if (!player.TryAcquireFrame(request, out var lease))
        {
            return false;
        }
        using (lease)
        {
            return check(lease);
        }
    }

    private static void AssertColor((byte B, byte G, byte R, byte A) pixel, int b, int g, int r)
    {
        // H.264 (BT.601/709 limited range) shifts solid colours a little.
        const int tolerance = 24;
        Math.Abs(pixel.B - b).ShouldBeLessThanOrEqualTo(tolerance, $"B of {pixel}; {DescribeWatched()}");
        Math.Abs(pixel.G - g).ShouldBeLessThanOrEqualTo(tolerance, $"G of {pixel}; {DescribeWatched()}");
        Math.Abs(pixel.R - r).ShouldBeLessThanOrEqualTo(tolerance, $"R of {pixel}; {DescribeWatched()}");
        pixel.A.ShouldBe((byte)255);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what, TimeSpan? timeout = null)
    {
        var deadline = Stopwatch.StartNew();
        var limit = timeout ?? s_timeout;
        while (!condition())
        {
            if (deadline.Elapsed > limit)
            {
                throw new TimeoutException($"Timed out waiting for {what}; {DescribeWatched()}.");
            }
            await Task.Delay(20);
        }
    }

    private static string DescribeWatched() =>
        s_watched is { } player
            ? $"state {player.State}, position {player.Position.TotalSeconds:0.000}s, buffered ranges {player.Buffered.Count}, failure {s_lastFailure ?? "none"}"
            : "no player";
}

/// <summary>Native players share AVFoundation / Media Foundation state; run these tests one at a time.</summary>
[CollectionDefinition(nameof(NativeVideoCollection), DisableParallelization = true)]
public sealed class NativeVideoCollection;
