using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A caption that follows the spoken word and a log of the player's events.</summary>
public partial class SpeechPlayerEventsDemo : UserControl
{
    private readonly List<string> _lines = [];

    public SpeechPlayerEventsDemo()
    {
        InitializeComponent();
        Player.Played += (_, e) => Add($"Played at {e.Time:m\\:ss} with {e.Voice.Name}, {Speed(e.Speed)}");
        Player.Paused += (_, e) => Add($"Paused at {e.Time:m\\:ss} on word {e.WordIndex + 1}");
        Player.Seeked += (_, e) => Add($"Seeked to {e.Time:m\\:ss}");
        Player.Ended += (_, _) => Add("Ended");
        Player.VoiceChanged += (_, e) => Add($"Voice changed to {e.Voice.Name}");
        Player.SpeedChanged += (_, e) => Add($"Speed changed to {Speed(e.Speed)}");
        Player.Closed += (_, _) =>
        {
            Caption.Text = "Caption: (not reading)";
            Add("Closed");
        };
        Player.WordChanged += (_, e) => Caption.Text = $"Caption: {e.Word}";
    }

    private static string Speed(SpeechSpeed speed) => speed switch
    {
        SpeechSpeed.OneAndHalf => "1.5×",
        SpeechSpeed.Twice => "2×",
        _ => "1×"
    };

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join("\n", _lines);
    }
}
