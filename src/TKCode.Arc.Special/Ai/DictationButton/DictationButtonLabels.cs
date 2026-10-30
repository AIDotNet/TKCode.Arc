using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="DictationButton"/> and <see cref="DictationControl"/> show or say, in English by default. Replace
/// the whole object to localize or reword; counts, times and errors are format functions.
/// </summary>
public sealed class DictationButtonLabels
{
    /// <summary>The English defaults.</summary>
    public static DictationButtonLabels Default { get; } = new();

    /// <summary>Accessible name and tooltip of the mic.</summary>
    public string Dictate { get; init; } = "Dictate";

    /// <summary>Accessible name and tooltip of the pill's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>Accessible name and tooltip of the pill's insert button.</summary>
    public string Insert { get; init; } = "Insert";

    /// <summary>Accessible name and tooltip of the insert button while the last words settle.</summary>
    public string Transcribing { get; init; } = "Transcribing";

    /// <summary>The button that retries after a denied or failed start.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>Accessible name and tooltip of the send button.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>Accessible name of the composer's field.</summary>
    public string Field { get; init; } = "Message";

    /// <summary>Accessible name of the elapsed time.</summary>
    public string Elapsed { get; init; } = "Recording time";

    /// <summary>The key cap of the insert hint.</summary>
    public string InsertKey { get; init; } = "Enter";

    /// <summary>The words after the insert key cap.</summary>
    public string InsertHint { get; init; } = "to insert";

    /// <summary>The key cap of the cancel hint.</summary>
    public string CancelKey { get; init; } = "Esc";

    /// <summary>The words after the cancel key cap.</summary>
    public string CancelHint { get; init; } = "to cancel";

    /// <summary>The elapsed time in the pill: "0:07", "1:05".</summary>
    public Func<TimeSpan, string> Time { get; init; } = static span =>
    {
        var seconds = Math.Max(0, (long)Math.Floor(span.TotalSeconds));
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    };

    /// <summary>What an error says in the toolbar, with where to fix it.</summary>
    public Func<DictationErrorCode, string> Error { get; init; } = static code => code switch
    {
        DictationErrorCode.NotAllowed => "Microphone blocked in system settings",
        DictationErrorCode.Unsupported => "Dictation isn't available here",
        DictationErrorCode.NoSpeech => "No speech heard, try again closer to the mic",
        DictationErrorCode.AudioCapture => "No microphone found",
        DictationErrorCode.Network => "Speech service unreachable, check your connection",
        _ => "Dictation stopped unexpectedly",
    };

    /// <summary>The short form of an error, for a narrow composer (under 400px).</summary>
    public Func<DictationErrorCode, string> ErrorShort { get; init; } = static code => code switch
    {
        DictationErrorCode.NotAllowed => "Mic blocked",
        DictationErrorCode.Unsupported => "Unavailable",
        DictationErrorCode.NoSpeech => "No speech heard",
        DictationErrorCode.AudioCapture => "No microphone",
        DictationErrorCode.Network => "Offline",
        _ => "Dictation failed",
    };

    /// <summary>Announced when listening starts.</summary>
    public string ListeningAnnouncement { get; init; } = "Listening. Press Enter to insert, Escape to cancel.";

    /// <summary>Announced when listening starts with the simulated recognizer.</summary>
    public string SimulatedAnnouncement { get; init; } = "Listening to a simulated voice, nothing is recorded. Press Enter to insert, Escape to cancel.";

    /// <summary>Announced while the last words settle.</summary>
    public string TranscribingAnnouncement { get; init; } = "Transcribing";

    /// <summary>Announced after an insert, from the word count.</summary>
    public Func<int, string> InsertedAnnouncement { get; init; } = static words =>
        words == 1 ? "Inserted 1 word" : string.Create(CultureInfo.InvariantCulture, $"Inserted {words} words");

    /// <summary>Announced when the dictation is cancelled.</summary>
    public string CancelledAnnouncement { get; init; } = "Dictation cancelled";

    /// <summary>Announced when a message has been sent.</summary>
    public string SentAnnouncement { get; init; } = "Message sent";

    /// <summary>Announced for an error, with how to fix it.</summary>
    public Func<DictationErrorCode, string> ErrorAnnouncement { get; init; } = static code => code switch
    {
        DictationErrorCode.NotAllowed => "Microphone blocked. Allow microphone access in system settings, then try again.",
        DictationErrorCode.Unsupported => "Dictation isn't available on this device.",
        DictationErrorCode.NoSpeech => "No speech heard. Move closer to the microphone and try again.",
        DictationErrorCode.AudioCapture => "No microphone found. Connect one and try again.",
        DictationErrorCode.Network => "The speech service is unreachable. Check your connection and try again.",
        _ => "Dictation stopped unexpectedly. Try again.",
    };
}
