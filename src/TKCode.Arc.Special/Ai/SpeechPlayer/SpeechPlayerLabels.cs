namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SpeechPlayer"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; sentences that carry a value are format functions.
/// </summary>
public sealed class SpeechPlayerLabels
{
    /// <summary>The English defaults.</summary>
    public static SpeechPlayerLabels Default { get; } = new();

    /// <summary>The Read aloud button in the action row.</summary>
    public string ReadAloud { get; init; } = "Read aloud";

    /// <summary>The name of the player group.</summary>
    public string Player { get; init; } = "Read aloud player";

    /// <summary>The play button while paused or ready.</summary>
    public string Play { get; init; } = "Play";

    /// <summary>The play button while playing.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The play button once the reply has been read.</summary>
    public string PlayAgain { get; init; } = "Play again";

    /// <summary>The play button after a failure.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The play button while speech is being prepared.</summary>
    public string PreparingAudio { get; init; } = "Preparing audio";

    /// <summary>The shimmering label while a voice is prepared: "Preparing Ava's voice".</summary>
    public Func<string, string> PreparingVoice { get; init; } = voice => $"Preparing {voice}'s voice";

    /// <summary>The name of the progress bar.</summary>
    public string Progress { get; init; } = "Playback position";

    /// <summary>The progress bar's value text: "0:04 of 0:22".</summary>
    public Func<string, string, string> PositionOf { get; init; } = (time, total) => $"{time} of {total}";

    /// <summary>The speed button's name: "Playback speed, 1×".</summary>
    public Func<string, string> SpeedButton { get; init; } = speed => $"Playback speed, {speed}";

    /// <summary>The voice button's name: "Voice, Ava".</summary>
    public Func<string, string> VoiceButton { get; init; } = voice => $"Voice, {voice}";

    /// <summary>The name of the one voice offered when the player has no voices (your service's default voice).</summary>
    public string DefaultVoice { get; init; } = "Default voice";

    /// <summary>The note in the voice menu when no audio plays.</summary>
    public string SimulatedNote { get; init; } = "Simulated preview, no audio plays";

    /// <summary>The Stop reading button.</summary>
    public string StopReading { get; init; } = "Stop reading";

    /// <summary>The headline of a failure, shown with an alert icon and the reason.</summary>
    public string Failed { get; init; } = "Couldn't generate audio";

    /// <summary>The default action buttons: Copy, Good response and Bad response.</summary>
    public string Copy { get; init; } = "Copy";

    /// <inheritdoc cref="Copy"/>
    public string GoodResponse { get; init; } = "Good response";

    /// <inheritdoc cref="Copy"/>
    public string BadResponse { get; init; } = "Bad response";

    /// <summary>Announced when reading starts: voice, speed and whether audio really plays.</summary>
    public Func<string, string, bool, string> Reading { get; init; } = (voice, speed, simulated) =>
        simulated ? $"Reading with {voice} at {speed} (simulated, no audio plays)" : $"Reading with {voice} at {speed}";

    /// <summary>Announced on pause: "Paused at 0:12".</summary>
    public Func<string, string> PausedAt { get; init; } = time => $"Paused at {time}";

    /// <summary>Announced when the voice changes while reading: "Switching to Leo".</summary>
    public Func<string, string> SwitchingTo { get; init; } = voice => $"Switching to {voice}";

    /// <summary>Announced when the last word was read.</summary>
    public string FinishedReading { get; init; } = "Finished reading";

    /// <summary>Announced when the speed changes: "Speed 1.5×".</summary>
    public Func<string, string> SpeedIs { get; init; } = speed => $"Speed {speed}";

    /// <summary>Announced when the player closes.</summary>
    public string Stopped { get; init; } = "Stopped reading";

    /// <summary>Announced when a default feedback button is pressed (local only, nothing is sent).</summary>
    public Func<bool, string> FeedbackGiven { get; init; } = good =>
        good ? "Marked as a good response (simulated)" : "Marked as a bad response (simulated)";

    /// <summary>Announced when feedback is taken back.</summary>
    public string FeedbackCleared { get; init; } = "Feedback removed";

    /// <summary>The speed labels: "1×", "1.5×", "2×".</summary>
    public Func<SpeechSpeed, string> SpeedText { get; init; } = speed => speed switch
    {
        SpeechSpeed.OneAndHalf => "1.5×",
        SpeechSpeed.Twice => "2×",
        _ => "1×"
    };
}
