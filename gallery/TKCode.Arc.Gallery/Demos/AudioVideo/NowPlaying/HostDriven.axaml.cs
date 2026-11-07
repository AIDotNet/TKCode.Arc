using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class NowPlayingHostDrivenDemo : UserControl
{
    private readonly List<string> _entries = new();

    public NowPlayingHostDrivenDemo()
    {
        InitializeComponent();
        // Episodes of an invented show; the covers reuse the sample drawings, the waveform comes from each id.
        Player.Tracks = new AvaloniaList<NowPlayingTrack>
        {
            new("ep-41", "Ep. 41 · Quiet machines", "Field Notes Radio", TimeSpan.FromMinutes(38) + TimeSpan.FromSeconds(12))
            {
                Artwork = NowPlayingSamples.Cover(2), ArtworkAlt = "A ring of warm light in the dark"
            },
            new("ep-42", "Ep. 42 · The long commute", "Field Notes Radio", TimeSpan.FromMinutes(41) + TimeSpan.FromSeconds(5))
            {
                Artwork = NowPlayingSamples.Cover(3), ArtworkAlt = "Sharp grey facets in hard light"
            },
            new("ep-43", "Ep. 43 · Tide tables", "Field Notes Radio", TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(47))
            {
                Artwork = NowPlayingSamples.Cover(4), ArtworkAlt = "Teal currents swirling in deep water"
            }
        };
    }

    private void Write(string text)
    {
        _entries.Add(text);
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }

    private static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private void OnToggle(object? sender, RoutedEventArgs e) => Player.TogglePlay();

    private void OnNext(object? sender, RoutedEventArgs e) => Player.Next();

    private void OnJump(object? sender, RoutedEventArgs e) => Player.CurrentTime = TimeSpan.FromMinutes(10);

    private void OnFold(object? sender, RoutedEventArgs e) => Player.ToggleExpanded();

    private void OnIndexChanged(object? sender, NowPlayingIndexChangedEventArgs e) =>
        Write($"Episode {e.NewIndex + 1} ({e.Reason}, {e.Direction})");

    private void OnPlayingChanged(object? sender, NowPlayingPlayingChangedEventArgs e) => Write(e.IsPlaying ? "Playing" : "Paused");

    private void OnSeekRequested(object? sender, NowPlayingSeekEventArgs e) => Write($"Seek to {Clock(e.Position)} ({e.Reason})");

    private void OnExpandedChanged(object? sender, NowPlayingExpandedChangedEventArgs e) => Write(e.IsExpanded ? "Opened" : "Folded");
}
