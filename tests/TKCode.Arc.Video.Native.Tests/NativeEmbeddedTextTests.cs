using System.Collections.Concurrent;
using System.Diagnostics;
using Shouldly;

namespace TKCode.Arc.Video.Native.Tests;

/// <summary>
/// Embedded text tracks (completion-plan.md, embedded subtitles): the clips from tools/video/make-text-clip.sh carry an
/// English and a Chinese track with two cues each, as MP4 timed text (tx3g) and as Matroska SRT. AVFoundation reads MP4
/// only; GStreamer reads both. Windows has the exports but reads no tracks yet, so it skips.
/// </summary>
[Collection(nameof(NativeVideoCollection))]
public class NativeEmbeddedTextTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(15);

    private static string Clip(string extension) => Path.Combine(AppContext.BaseDirectory, "Media", "captions-2s." + extension);

    [Fact]
    public Task Mp4_timed_text_tracks_are_listed_and_deliver_their_cues() => CheckClipAsync("mp4");

    [Fact]
    public Task Matroska_text_tracks_are_listed_and_deliver_their_cues()
    {
        if (OperatingSystem.IsMacOS())
        {
            Assert.Skip("AVFoundation does not open Matroska files.");
        }
        return CheckClipAsync("mkv");
    }

    [Fact]
    public async Task Nothing_is_delivered_until_a_track_is_selected_and_loading_again_clears_the_tracks()
    {
        using var player = RequirePlayer();
        var cues = new ConcurrentQueue<VideoEmbeddedCueEventArgs>();
        player.EmbeddedCue += (_, e) => cues.Enqueue(e);
        await LoadAsync(player, Clip("mp4"));
        await WaitUntilAsync(() => player.EmbeddedTextTracks.Count == 2, "the text tracks");
        player.IsMuted = true;
        player.Play();
        await Task.Delay(1200);
        player.Pause();
        cues.Where(c => c.Text.Length > 0).ShouldBeEmpty();

        await LoadAsync(player, Path.Combine(AppContext.BaseDirectory, "Media", "red-blue-2s.mp4"));
        await WaitUntilAsync(() => player.EmbeddedTextTracks.Count == 0, "the tracks to clear");
    }

    private static async Task CheckClipAsync(string extension)
    {
        using var player = RequirePlayer();
        var changed = 0;
        player.EmbeddedTextTracksChanged += (_, _) => Interlocked.Increment(ref changed);
        var cues = new ConcurrentQueue<VideoEmbeddedCueEventArgs>();
        player.EmbeddedCue += (_, e) => cues.Enqueue(e);
        await LoadAsync(player, Clip(extension));
        await WaitUntilAsync(() => player.EmbeddedTextTracks.Count == 2, "the text tracks");
        Volatile.Read(ref changed).ShouldBeGreaterThan(0);

        var tracks = player.EmbeddedTextTracks;
        tracks.Select(t => t.Index).ShouldBe([0, 1]);
        tracks[0].Language.ShouldNotBeNull().ShouldStartWith("en");
        tracks[1].Language.ShouldNotBeNull().ShouldBeOneOf("zh", "chi", "zho", "zh-Hans", "zh-CN");

        player.SelectEmbeddedTextTrack(0);
        player.IsMuted = true;
        player.Play();
        await WaitUntilAsync(() => player.State == VideoPlaybackState.Ended || player.Position > TimeSpan.FromSeconds(1.95), "the end", TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        var shown = cues.Where(c => c.Text.Length > 0).ToArray();
        shown.ShouldAllBe(c => c.Track == 0);
        var hello = shown.FirstOrDefault(c => c.Text.Contains("Hello", StringComparison.Ordinal)).ShouldNotBeNull($"cues: {Describe(shown)}");
        // GStreamer's MP4 demuxer drops the tx3g styling (qtdemux hands out plain text); Matroska SRT and AVFoundation keep it.
        var styled = !(extension == "mp4" && ArcVideo.Backend.Name == "gstreamer");
        hello.Text.ShouldBe(styled ? "Hello <b>bold</b>" : "Hello bold", $"cues: {Describe(shown)}");
        hello.Start.TotalSeconds.ShouldBe(0.2, 0.05);
        if (hello.End is { } end)
        {
            end.TotalSeconds.ShouldBe(0.9, 0.05);
        }
        else
        {
            // Open-ended (AVFoundation): the next call, empty when the cue goes, ends it.
            cues.ShouldContain(c => c.Start.TotalSeconds > 0.85 && c.Start.TotalSeconds < 1.0, $"cues: {Describe(cues)}");
        }
        var second = shown.FirstOrDefault(c => c.Text.Contains("Second", StringComparison.Ordinal)).ShouldNotBeNull($"cues: {Describe(shown)}");
        second.Text.ShouldBe(styled ? "Second <i>cue</i>" : "Second cue");
        second.Start.TotalSeconds.ShouldBe(1.1, 0.05);
    }

    private static string Describe(IEnumerable<VideoEmbeddedCueEventArgs> cues) =>
        string.Join("; ", cues.Select(c => $"[{c.Track}] {c.Start.TotalSeconds:0.000}-{c.End?.TotalSeconds.ToString("0.000") ?? "open"} '{c.Text}'"));

    private static IVideoEmbeddedTextTracks2 RequirePlayer()
    {
        if (!ArcVideoNative.Use())
        {
            if (OperatingSystem.IsMacOS())
            {
                Assert.Fail("ArcVideoNative.Use() returned false on macOS: build the shim (tools/video/build-native.sh, tools/video/sync-native.sh).");
            }
            Assert.Skip("tkcode_video or its system framework is not available on this machine.");
        }
        if (OperatingSystem.IsMacOS() && !MainRunLoop.IsServiced)
        {
            Assert.Skip("AVFoundation needs the main run loop; run the test executable directly (its Main services it).");
        }
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Media Foundation text tracks are not read yet (tkv_player_text_track_count is always 0).");
        }
        var player = ArcVideo.Backend.CreatePlayer();
        if (player is not IVideoEmbeddedTextTracks)
        {
            player.Dispose();
            Assert.Skip("This backend does not read embedded text tracks.");
        }
        return new IVideoEmbeddedTextTracks2(player);
    }

    private static async Task LoadAsync(IVideoEmbeddedTextTracks2 player, string path)
    {
        File.Exists(path).ShouldBeTrue(path);
        await player.Player.LoadAsync(VideoSource.FromFile(path)).WaitAsync(s_timeout);
        player.State.ShouldBe(VideoPlaybackState.Paused, $"load ended in {player.State}");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what, TimeSpan? timeout = null)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > (timeout ?? s_timeout))
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }
            await Task.Delay(20);
        }
    }

    /// <summary>The player and its embedded text interface in one handle.</summary>
    private sealed class IVideoEmbeddedTextTracks2(IVideoPlayer player) : IDisposable
    {
        private readonly IVideoEmbeddedTextTracks _text = (IVideoEmbeddedTextTracks)player;

        public IVideoPlayer Player { get; } = player;

        public IReadOnlyList<VideoEmbeddedTextTrackInfo> EmbeddedTextTracks => _text.EmbeddedTextTracks;

        public VideoPlaybackState State => Player.State;

        public TimeSpan Position => Player.Position;

        public bool IsMuted
        {
            set => Player.IsMuted = value;
        }

        public event EventHandler? EmbeddedTextTracksChanged
        {
            add => _text.EmbeddedTextTracksChanged += value;
            remove => _text.EmbeddedTextTracksChanged -= value;
        }

        public event EventHandler<VideoEmbeddedCueEventArgs>? EmbeddedCue
        {
            add => _text.EmbeddedCue += value;
            remove => _text.EmbeddedCue -= value;
        }

        public void SelectEmbeddedTextTrack(int index) => _text.SelectEmbeddedTextTrack(index);

        public void Play() => Player.Play();

        public void Pause() => Player.Pause();

        public void Dispose() => Player.Dispose();
    }
}
