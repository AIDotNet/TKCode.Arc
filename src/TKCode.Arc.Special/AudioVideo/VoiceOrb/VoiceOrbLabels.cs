namespace TKCode.Arc;

/// <summary>
/// The accessible names a <see cref="VoiceOrb"/> gives itself, one per state (English defaults). Replace the object, or
/// set <see cref="VoiceOrb.Label"/> for one fixed name.
/// </summary>
public sealed class VoiceOrbLabels
{
    /// <summary>The name while idle.</summary>
    public string Idle { get; init; } = "Voice assistant is idle";

    /// <summary>The name while listening.</summary>
    public string Listening { get; init; } = "Listening";

    /// <summary>The name while thinking.</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The name while speaking.</summary>
    public string Speaking { get; init; } = "Speaking";

    /// <summary>The name while listening with the microphone muted.</summary>
    public string Muted { get; init; } = "Muted";

    /// <summary>The name for <paramref name="state"/>, or <see cref="Muted"/> when muted while listening.</summary>
    public string For(VoiceOrbState state, bool isMuted) => state switch
    {
        VoiceOrbState.Listening => isMuted ? Muted : Listening,
        VoiceOrbState.Thinking => Thinking,
        VoiceOrbState.Speaking => Speaking,
        _ => Idle
    };
}

/// <summary>
/// The text of a <see cref="VoiceControls"/> bar (English defaults): button names, the start label and the timer's
/// spoken name.
/// </summary>
public sealed class VoiceControlsLabels
{
    /// <summary>The start button's text and name.</summary>
    public string Start { get; init; } = "Start voice chat";

    /// <summary>The mute button's name while the microphone is on.</summary>
    public string Mute { get; init; } = "Mute microphone";

    /// <summary>The mute button's name while the microphone is muted.</summary>
    public string Unmute { get; init; } = "Unmute microphone";

    /// <summary>The end button's name.</summary>
    public string End { get; init; } = "End voice chat";

    /// <summary>
    /// The timer's spoken name for whole minutes and seconds. The default says "1 minute 5 seconds" and leaves out zero
    /// minutes ("12 seconds").
    /// </summary>
    public Func<int, int, string> ClockName { get; init; } = DefaultClockName;

    /// <summary>The timer's text: m:ss ("0:07", "12:30"), h:mm:ss from one hour.</summary>
    public static string FormatClock(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }
        var total = (long)Math.Floor(elapsed.TotalSeconds);
        var hours = total / 3600;
        var minutes = total / 60 % 60;
        var seconds = total % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{total / 60}:{seconds:00}";
    }

    private static string DefaultClockName(int minutes, int seconds)
    {
        var s = seconds == 1 ? "1 second" : $"{seconds} seconds";
        if (minutes == 0)
        {
            return s;
        }
        var m = minutes == 1 ? "1 minute" : $"{minutes} minutes";
        return m + " " + s;
    }
}
