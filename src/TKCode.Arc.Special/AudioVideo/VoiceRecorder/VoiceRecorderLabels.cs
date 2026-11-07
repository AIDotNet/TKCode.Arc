namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="VoiceRecorder"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; sentences with a time or a name are format functions (times arrive as m:ss text).
/// </summary>
public sealed class VoiceRecorderLabels
{
    /// <summary>The English defaults.</summary>
    public static VoiceRecorderLabels Default { get; } = new();

    /// <summary>The hint next to the mic while nothing is recorded.</summary>
    public string Hint { get; init; } = "Tap the mic to leave a voice note";

    /// <summary>Accessible name of the mic button.</summary>
    public string Record { get; init; } = "Record voice note";

    /// <summary>Accessible name of the stop button, with the time recorded so far (updated every second).</summary>
    public Func<string, string> StopAt { get; init; } = static time => $"Stop recording at {time}";

    /// <summary>Accessible name of the trash button while recording or reviewing.</summary>
    public string Discard { get; init; } = "Discard voice note";

    /// <summary>Accessible name of the send button.</summary>
    public string Send { get; init; } = "Send voice note";

    /// <summary>Accessible name of the review's play button.</summary>
    public string PlayPreview { get; init; } = "Play preview";

    /// <summary>Accessible name of the review's play button while the take plays.</summary>
    public string PausePreview { get; init; } = "Pause preview";

    /// <summary>Accessible name of an incoming bubble's play button: the sender and the length.</summary>
    public Func<string, string, string> PlayIncoming { get; init; } = static (from, time) => $"Play voice message from {from}, {time}";

    /// <summary>Accessible name of an incoming bubble's play button while it plays.</summary>
    public Func<string, string, string> PauseIncoming { get; init; } = static (from, time) => $"Pause voice message from {from}, {time}";

    /// <summary>Accessible name of your own bubble's play button: the length.</summary>
    public Func<string, string> PlayOwn { get; init; } = static time => $"Play voice message, {time}";

    /// <summary>Accessible name of your own bubble's play button while it plays.</summary>
    public Func<string, string> PauseOwn { get; init; } = static time => $"Pause voice message, {time}";

    /// <summary>Accessible name of the message list.</summary>
    public string MessagesList { get; init; } = "Messages";

    /// <summary>Said when recording starts.</summary>
    public string AnnounceRecording { get; init; } = "Recording";

    /// <summary>Said when recording stops (by hand or at the time limit), with the take's length.</summary>
    public Func<string, string> AnnounceRecorded { get; init; } = static time => $"Recorded {time}. Play it back or send it.";

    /// <summary>Said when a take is sent.</summary>
    public string AnnounceSent { get; init; } = "Voice note sent";

    /// <summary>Said when a take is discarded.</summary>
    public string AnnounceDiscarded { get; init; } = "Voice note discarded";
}
