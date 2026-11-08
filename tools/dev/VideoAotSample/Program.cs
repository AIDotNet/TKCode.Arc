using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TKCode.Arc;

namespace TKCode.Arc.VideoAotSample;

/// <summary>
/// Opens a window with a muted, auto-playing <see cref="VideoPlayer"/> on the clip given as the first argument (default:
/// tests/Video/Media/red-blue-2s.mp4 found from the working directory), waits until it has played for a while, prints the
/// backend, render path, state and position, and exits. Exit code 0 when the system player was used and time advanced;
/// 1 otherwise. Usage: VideoAotSample [clip] [--keep] (--keep leaves the window open).
/// </summary>
public static class Program
{
    private static readonly TimeSpan s_deadline = TimeSpan.FromSeconds(20);

    public static int Main(string[] args)
    {
        var clip = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? FindClip();
        if (clip is null || !File.Exists(clip))
        {
            Console.Error.WriteLine("No clip: pass a path, or run from the repository (tests/Video/Media/red-blue-2s.mp4).");
            return 1;
        }
        var native = ArcVideoNative.Use();
        Console.WriteLine($"ArcVideoNative.Use(): {native} ({ArcVideo.Backend.Name})");
        App.Clip = Path.GetFullPath(clip);
        App.Keep = args.Contains("--keep");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return native && App.Passed ? 0 : 1;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .UseArc(arc => arc.UseArcControls().UseArcVideo());

    private static string? FindClip()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Video", "Media", "red-blue-2s.mp4");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}

public sealed class App : Application
{
    public static string Clip { get; set; } = "";

    public static bool Keep { get; set; }

    public static bool Passed { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var player = new VideoPlayer
            {
                Source = VideoSource.FromFile(Clip),
                IsMuted = true,
                AutoPlay = true,
                IsLooping = true,
                Title = "AOT sample",
            };
            desktop.MainWindow = new Window { Title = "TKCode.Arc.Video AOT sample", Width = 640, Height = 420, Content = player };
            var clock = Stopwatch.StartNew();
            var furthest = TimeSpan.Zero;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) =>
            {
                var surface = player.GetVisualDescendantsOfType<VideoSurface>().FirstOrDefault();
                if (player.Position > furthest)
                {
                    furthest = player.Position;
                }
                // Time must advance (the clip loops, so the furthest position seen is checked, not the last one).
                var playedEnough = player.State == VideoPlaybackState.Playing && clock.Elapsed > TimeSpan.FromSeconds(3) && furthest > TimeSpan.FromSeconds(0.5);
                if (!playedEnough && clock.Elapsed < TimeSpan.FromSeconds(20))
                {
                    return;
                }
                timer.Stop();
                var path = surface?.RenderPath.ToString() ?? "no surface";
                Console.WriteLine($"state {player.State}, position {player.Position.TotalSeconds:0.00}s (furthest {furthest.TotalSeconds:0.00}s), duration {player.Duration.TotalSeconds:0.00}s, render path {path}");
                if (player.ErrorMessage is { } error)
                {
                    Console.WriteLine($"error: {error}");
                }
                Passed = playedEnough && player.Duration > TimeSpan.Zero;
                Console.WriteLine(Passed ? "PASS" : "FAIL");
                if (!Keep)
                {
                    desktop.Shutdown(Passed ? 0 : 1);
                }
            };
            timer.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class VisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual visual) where T : Visual =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}
