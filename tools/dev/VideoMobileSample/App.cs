using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc;

namespace TKCode.Arc.VideoMobileSample;

/// <summary>
/// Plays Assets/captions-2s.mp4 (from tools/video/make-text-clip.sh) muted and looping with its first embedded text track
/// selected, then writes one line to the console (the iOS simulator's console, Android's logcat):
/// <c>VIDEO-SAMPLE: PASS backend=... state=... furthest=... frames=... cues=...</c> or <c>VIDEO-SAMPLE: FAIL ...</c>.
/// PASS needs the system backend, time to advance, frames to be drawn and at least one cue from the media.
/// </summary>
public sealed class App : Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        var native = ArcVideoNative.Use();
        var player = new VideoPlayer
        {
            Source = VideoSource.Parse("avares://TKCode.Arc.VideoMobileSample/Assets/captions-2s.mp4"),
            IsMuted = true,
            AutoPlay = true,
            IsLooping = true,
            Title = "Mobile sample",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var status = new TextBlock { Margin = new Thickness(16), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var root = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Children = { player, status } };
        switch (ApplicationLifetime)
        {
            case ISingleViewApplicationLifetime single:
                single.MainView = root;
                break;
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window { Width = 480, Height = 400, Content = root };
                break;
        }
        Report(player, status, native);
        base.OnFrameworkInitializationCompleted();
    }

    private static void Report(VideoPlayer player, TextBlock status, bool native)
    {
        var clock = Stopwatch.StartNew();
        var furthest = TimeSpan.Zero;
        var cues = 0;
        var reported = false;
        ((System.Collections.Specialized.INotifyCollectionChanged)player.TextTracks).CollectionChanged += (_, _) =>
        {
            if (player.SelectedTextTrack is null && player.TextTracks.FirstOrDefault(t => t.IsEmbedded) is { } track)
            {
                track.CuesChanged += (_, _) => cues = track.Cues.Count;
                player.SelectedTextTrack = track;
            }
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            if (player.Position > furthest)
            {
                furthest = player.Position;
            }
            var frames = player.GetVisualDescendants().OfType<VideoSurface>().FirstOrDefault()?.RenderPath.ToString() ?? "none";
            var passed = native && player.State == VideoPlaybackState.Playing && furthest > TimeSpan.FromSeconds(0.5) && cues > 0 && frames != "None";
            var line = $"backend={ArcVideo.Backend.Name} state={player.State} furthest={furthest.TotalSeconds:0.00}s frames={frames} tracks={player.TextTracks.Count} cues={cues} error={player.ErrorMessage ?? "-"}";
            status.Text = line;
            if (reported || (!passed && clock.Elapsed < TimeSpan.FromSeconds(25)))
            {
                return;
            }
            reported = true;
            Console.WriteLine($"VIDEO-SAMPLE: {(passed ? "PASS" : "FAIL")} {line}");
        };
        timer.Start();
    }
}

internal static class VisualTreeExtensions
{
    public static IEnumerable<Visual> GetVisualDescendants(this Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual);
}
