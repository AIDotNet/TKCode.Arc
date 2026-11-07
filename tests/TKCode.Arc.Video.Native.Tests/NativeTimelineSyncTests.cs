using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Video.Native.Tests;

/// <summary>ABI 2: timeline semaphores for GPU frames (Metal imports IOSurfaces only that way) and frame redelivery.</summary>
[Collection(nameof(NativeVideoCollection))]
public sealed partial class NativeTimelineSyncTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static string ClipPath => Path.Combine(AppContext.BaseDirectory, "Media", "red-blue-2s.mp4");

    [Fact]
    public async Task Gpu_frame_has_a_metal_shared_event_already_signalled()
    {
        var player = await LoadGpuPlayerAsync();
        using (player)
        {
            var sync = (IVideoTimelineSync)player;
            var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.Gpu);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < s_timeout)
            {
                if (player.TryAcquireFrame(request, out var lease))
                {
                    using (lease)
                    {
                        sync.TryGetTimelineSync(lease.GpuImageId + 1, out _, out _, out _).ShouldBeFalse("wrong image id");
                        sync.TryGetTimelineSync(lease.GpuImageId, out var semaphore, out var wait, out var signal).ShouldBeTrue();
                        semaphore.HandleDescriptor.ShouldBe(KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent);
                        semaphore.Handle.ShouldNotBe(0);
                        wait.ShouldBeGreaterThan(0UL);
                        signal.ShouldBe(wait + 1);
                        SignaledValue(semaphore.Handle).ShouldBe(wait);

                        // Asking again for the same frame gives the same values.
                        sync.TryGetTimelineSync(lease.GpuImageId, out var again, out var wait2, out var signal2).ShouldBeTrue();
                        again.ShouldBeSameAs(semaphore);
                        (wait2, signal2).ShouldBe((wait, signal));

                        // The compositor's GPU signal, done here from the CPU.
                        SetSignaledValue(semaphore.Handle, signal);
                    }
                    return;
                }
                await Task.Delay(20);
            }
            throw new TimeoutException("no GPU frame");
        }
    }

    [Fact]
    public async Task Timeline_values_only_move_forward_while_playing()
    {
        var player = await LoadGpuPlayerAsync();
        using (player)
        {
            var sync = (IVideoTimelineSync)player;
            var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.Gpu);
            var last = new Dictionary<nint, ulong>();
            var frames = 0;
            player.Play();
            var deadline = Stopwatch.StartNew();
            while (frames < 20 && deadline.Elapsed < s_timeout)
            {
                if (player.TryAcquireFrame(request, out var lease))
                {
                    using (lease)
                    {
                        sync.TryGetTimelineSync(lease.GpuImageId, out var semaphore, out var wait, out var signal).ShouldBeTrue();
                        if (last.TryGetValue(semaphore.Handle, out var previous))
                        {
                            wait.ShouldBeGreaterThan(previous);
                        }
                        last[semaphore.Handle] = signal;
                        SetSignaledValue(semaphore.Handle, signal);
                        frames++;
                    }
                }
                await Task.Delay(16);
            }
            player.Pause();
            frames.ShouldBeGreaterThanOrEqualTo(20);
            // One event per pooled image: a few, not one per frame.
            last.Count.ShouldBeLessThan(frames);
        }
    }

    [Fact]
    public async Task Cpu_frame_needs_no_semaphore()
    {
        var player = await LoadGpuPlayerAsync();
        using (player)
        {
            var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
            var got = false;
            var deadline = Stopwatch.StartNew();
            while (!got && deadline.Elapsed < s_timeout)
            {
                if (player.TryAcquireFrame(request, out var lease))
                {
                    using (lease)
                    {
                        ((IVideoTimelineSync)player).TryGetTimelineSync(lease.GpuImageId, out _, out _, out _).ShouldBeFalse();
                        got = true;
                    }
                }
                await Task.Delay(20);
            }
            got.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task New_frame_target_redelivers_the_current_frame()
    {
        var player = await LoadGpuPlayerAsync();
        using (player)
        {
            var request = new VideoFrameRequest(TimeSpan.Zero, default, VideoFrameKinds.CpuBgra);
            TimeSpan first = default;
            var deadline = Stopwatch.StartNew();
            var got = false;
            while (!got && deadline.Elapsed < s_timeout)
            {
                got = TryTake(player, request, out first);
                if (!got)
                {
                    await Task.Delay(20);
                }
            }
            got.ShouldBeTrue("first frame");
            TryTake(player, request, out _).ShouldBeFalse("paused: nothing new");

            player.FrameTarget = new VideoFrameTarget { GpuHandleTypes = [] };
            TryTake(player, request, out var again).ShouldBeTrue("redelivered after the target changed");
            again.ShouldBe(first);
            TryTake(player, request, out _).ShouldBeFalse("redelivery is one-shot");
        }
    }

    private static bool TryTake(IVideoPlayer player, in VideoFrameRequest request, out TimeSpan time)
    {
        time = default;
        if (!player.TryAcquireFrame(request, out var lease))
        {
            return false;
        }
        time = lease.PresentationTime;
        lease.Dispose();
        return true;
    }

    private static async Task<IVideoPlayer> LoadGpuPlayerAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Skip("Timeline semaphores are a macOS (Metal) feature.");
        }
        if (!ArcVideoNative.Use())
        {
            Assert.Fail("ArcVideoNative.Use() returned false on macOS.");
        }
        if (!MainRunLoop.IsServiced)
        {
            Assert.Skip("AVFoundation needs the main run loop; run the test executable directly.");
        }
        var backend = ArcVideo.Backend;
        var player = backend.CreatePlayer();
        player.ShouldBeAssignableTo<IVideoTimelineSync>();
        player.FrameTarget = new VideoFrameTarget { GpuHandleTypes = backend.GpuHandleTypes };
        await player.LoadAsync(VideoSource.FromFile(ClipPath)).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Paused);
        return player;
    }

    // MTLSharedEvent.signaledValue through the Objective-C runtime.
    private static ulong SignaledValue(nint sharedEvent) => GetUInt64(sharedEvent, sel_registerName("signaledValue"));

    private static void SetSignaledValue(nint sharedEvent, ulong value) => SetUInt64(sharedEvent, sel_registerName("setSignaledValue:"), value);

    [LibraryImport("/usr/lib/libobjc.A.dylib", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial ulong GetUInt64(nint receiver, nint selector);

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial void SetUInt64(nint receiver, nint selector, ulong value);
}
