using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceOrbCallBarDemo : UserControl
{
    // Invented lines for the two speakers.
    private static readonly string[] s_lines =
    [
        "Can you find a quiet table for two on Friday?",
        "There is one at Juniper Hall at eight, by the window.",
        "Perfect, book it and add it to my calendar.",
        "Done. It is on your calendar with a reminder an hour before."
    ];

    private readonly List<string> _entries = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _line;
    private int _words;

    public VoiceOrbCallBarDemo()
    {
        InitializeComponent();
        _clock.Tick += (_, _) => Controls.Elapsed += TimeSpan.FromSeconds(1);
        DetachedFromVisualTree += (_, _) => _clock.Stop();
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

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        Controls.Elapsed = TimeSpan.Zero;
        _clock.Start();
        _line = 0;
        _words = 0;
        NewTurn();
        Write("StartRequested");
    }

    private void OnEnd(object? sender, RoutedEventArgs e)
    {
        _clock.Stop();
        Transcript.Turn++;
        Transcript.Speaker = VoiceTranscriptSpeaker.System;
        Transcript.Text = $"Call ended after {VoiceControlsLabels.FormatClock(Controls.Elapsed)}.";
        Transcript.IsComplete = true;
        Write("EndRequested");
    }

    private void OnMute(object? sender, VoiceControlsMuteChangedEventArgs e) => Write($"MuteChanged: {(e.IsMuted ? "muted" : "on")}");

    private void OnWord(object? sender, RoutedEventArgs e)
    {
        if (!Controls.IsActive)
        {
            return;
        }
        var words = s_lines[_line % s_lines.Length].Split(' ');
        if (_words >= words.Length)
        {
            OnTurn(sender, e);
            return;
        }
        _words++;
        Transcript.Text = string.Join(' ', words, 0, _words);
        Transcript.IsComplete = _words == words.Length;
    }

    private void OnTurn(object? sender, RoutedEventArgs e)
    {
        if (!Controls.IsActive)
        {
            return;
        }
        _line++;
        _words = 0;
        NewTurn();
    }

    private void NewTurn()
    {
        Transcript.Turn++;
        Transcript.Speaker = _line % 2 == 0 ? VoiceTranscriptSpeaker.User : VoiceTranscriptSpeaker.Assistant;
        Transcript.Text = "";
        Transcript.IsComplete = false;
    }
}
