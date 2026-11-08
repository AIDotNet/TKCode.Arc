using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Shouldly;

namespace TKCode.Arc.Video.Native.Tests;

/// <summary>
/// Frame throughput of the system player (design/components/video/video.md section 14, completion-plan.md A6): plays a clip
/// and pulls frames the way <see cref="VideoSurface"/> does, for the GPU path, the CPU path at the natural size and the CPU
/// path scaled to 640×360, and reports frames per second and the per-frame cost on the pulling thread (acquire, the copy a
/// bitmap upload needs, release). A measurement, not a check: it runs only when TKV_PERF_CLIP names a clip (for example a
/// 1080p60 H.264 MP4) and writes its table to TKV_PERF_OUT when set, else to the test output.
/// </summary>
[Collection(nameof(NativeVideoCollection))]
public class NativeFrameThroughputTests
{
    private static readonly TimeSpan s_window = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Measure_frame_throughput()
    {
        var clip = Environment.GetEnvironmentVariable("TKV_PERF_CLIP");
        if (string.IsNullOrEmpty(clip))
        {
            Assert.Skip("Set TKV_PERF_CLIP to a clip to measure frame throughput.");
        }
        File.Exists(clip).ShouldBeTrue(clip);
        if (!ArcVideoNative.Use())
        {
            Assert.Skip("tkcode_video or its system framework is not available on this machine.");
        }
        if (OperatingSystem.IsMacOS() && !MainRunLoop.IsServiced)
        {
            Assert.Skip("AVFoundation needs the main run loop; run the test executable directly.");
        }
        var backend = (NativeVideoBackend)ArcVideo.Backend;
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"{backend.Name} on {Environment.OSVersion} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}), clip {Path.GetFileName(clip)}");
        report.AppendLine("| path | frames/s | cost per frame avg (ms) | p95 (ms) | max (ms) | frame size |");
        report.AppendLine("|---|---|---|---|---|---|");
        if (backend.GpuHandleTypes.Count > 0)
        {
            report.AppendLine(await MeasureAsync(backend, clip, "GPU", VideoFrameKinds.Gpu, default));
        }
        report.AppendLine(await MeasureAsync(backend, clip, "CPU natural", VideoFrameKinds.CpuBgra, default));
        report.AppendLine(await MeasureAsync(backend, clip, "CPU 640×360", VideoFrameKinds.CpuBgra, new PixelSize(640, 360)));
        var text = report.ToString();
        if (Environment.GetEnvironmentVariable("TKV_PERF_OUT") is { Length: > 0 } output)
        {
            File.WriteAllText(output, text);
        }
        TestContext.Current.TestOutputHelper?.WriteLine(text);
    }

    private static async Task<string> MeasureAsync(NativeVideoBackend backend, string clip, string name, VideoFrameKinds kinds, PixelSize target)
    {
        using var player = backend.CreatePlayer();
        if (kinds == VideoFrameKinds.Gpu)
        {
            player.FrameTarget = new VideoFrameTarget { GpuHandleTypes = backend.GpuHandleTypes };
        }
        player.IsMuted = true;
        await player.LoadAsync(VideoSource.FromFile(clip)).WaitAsync(TimeSpan.FromSeconds(15));
        player.Play();
        var request = new VideoFrameRequest(TimeSpan.Zero, target, kinds);
        var costs = new List<double>();
        var copy = Array.Empty<byte>();
        var lastTime = TimeSpan.MinValue;
        var size = default(PixelSize);
        var warmUp = Stopwatch.StartNew();
        while (warmUp.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(4);
        }
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < s_window)
        {
            var start = Stopwatch.GetTimestamp();
            if (player.TryAcquireFrame(request, out var lease))
            {
                using (lease)
                {
                    if (lease.PresentationTime != lastTime)
                    {
                        lastTime = lease.PresentationTime;
                        size = lease.Size;
                        if (lease.Kind == VideoFrameKinds.CpuBgra)
                        {
                            var bytes = lease.Size.Height * lease.Size.Width * 4;
                            if (copy.Length < bytes)
                            {
                                copy = new byte[bytes];
                            }
                            // The bitmap path copies each row into the WriteableBitmap.
                            for (var y = 0; y < lease.Size.Height; y++)
                            {
                                lease.Pixels.Slice(y * lease.RowBytes, lease.Size.Width * 4).CopyTo(copy.AsSpan(y * lease.Size.Width * 4));
                            }
                        }
                        costs.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    }
                }
            }
            await Task.Delay(4);
        }
        player.Pause();
        if (costs.Count == 0)
        {
            return $"| {name} | no frames | | | | |";
        }
        costs.Sort();
        var fps = costs.Count / s_window.TotalSeconds;
        var p95 = costs[(int)Math.Min(costs.Count - 1, Math.Ceiling(costs.Count * 0.95) - 1)];
        return string.Create(CultureInfo.InvariantCulture, $"| {name} | {fps:0.0} | {costs.Average():0.000} | {p95:0.000} | {costs[^1]:0.000} | {size.Width}×{size.Height} |");
    }
}
