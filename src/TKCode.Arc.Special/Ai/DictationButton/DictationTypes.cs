using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a dictation is: idle, starting, listening, processing, then idle again, or denied and error.</summary>
public enum DictationStatus
{
    /// <summary>Nothing is recorded; the mic waits.</summary>
    Idle,

    /// <summary>The recognizer is starting (asking for the microphone).</summary>
    Starting,

    /// <summary>Audio flows: the pill shows the waveform and the timer, words stream in.</summary>
    Listening,

    /// <summary>Listening stopped; the recognizer settles the last words before they are inserted.</summary>
    Processing,

    /// <summary>The microphone is blocked (<see cref="DictationErrorCode.NotAllowed"/>).</summary>
    Denied,

    /// <summary>Dictation failed for another reason; see <see cref="DictationErrorCode"/>.</summary>
    Error
}

/// <summary>Why a dictation could not start or stopped early.</summary>
public enum DictationErrorCode
{
    /// <summary>The person or the system blocked the microphone. The status becomes <see cref="DictationStatus.Denied"/>.</summary>
    NotAllowed,

    /// <summary>Speech recognition is not available on this platform or in this app.</summary>
    Unsupported,

    /// <summary>The recognizer heard no speech.</summary>
    NoSpeech,

    /// <summary>No microphone was found, or it could not be opened.</summary>
    AudioCapture,

    /// <summary>The speech service could not be reached.</summary>
    Network,

    /// <summary>Any other failure.</summary>
    Failed
}

/// <summary>
/// The live transcript of the current dictation: <see cref="Final"/> holds the words the recognizer has settled,
/// <see cref="Interim"/> its current guess for the words after them (show it muted; it may still change).
/// </summary>
/// <param name="Final">The settled words of this session, in order.</param>
/// <param name="Interim">The words the recognizer is still guessing.</param>
public readonly record struct DictationTranscript(string Final, string Interim)
{
    /// <summary>No words yet.</summary>
    public static DictationTranscript Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>True when neither part has text.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Final) && string.IsNullOrWhiteSpace(Interim);

    /// <summary>Both parts joined by a space, trimmed.</summary>
    public string Text => DictationText.Join(Final, Interim);
}

/// <summary>
/// Throw it from <see cref="IDictationRecognizer.StartAsync"/> to say why dictation could not start
/// (<see cref="DictationErrorCode.NotAllowed"/> for a blocked microphone).
/// </summary>
public sealed class DictationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DictationException(DictationErrorCode code, string? message = null, Exception? innerException = null)
        : base(message ?? code.ToString(), innerException)
    {
        Code = code;
    }

    /// <summary>Why dictation failed.</summary>
    public DictationErrorCode Code { get; }
}

/// <summary>The model label at the start of the <see cref="DictationButton"/> toolbar: a mark and a name.</summary>
public sealed class DictationModel
{
    /// <summary>Creates an empty label; set <see cref="Name"/>.</summary>
    public DictationModel()
    {
    }

    /// <summary>Creates a label.</summary>
    public DictationModel(string name, Geometry? mark = null)
    {
        Name = name;
        Mark = mark;
    }

    /// <summary>The model's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A 24 × 24 stroked glyph before the name; the sparkle when null.</summary>
    public Geometry? Mark { get; set; }
}

/// <summary>Arguments of the transcript events: the live, cumulative transcript of the current session.</summary>
public sealed class DictationTranscriptEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DictationTranscriptEventArgs(RoutedEvent? routedEvent, DictationTranscript transcript)
        : base(routedEvent)
    {
        Transcript = transcript;
    }

    /// <summary>The transcript so far.</summary>
    public DictationTranscript Transcript { get; }

    /// <summary>The settled words.</summary>
    public string Final => Transcript.Final;

    /// <summary>The words still being guessed (show them muted).</summary>
    public string Interim => Transcript.Interim;
}

/// <summary>Arguments of the commit events: the final text of a dictation.</summary>
public sealed class DictationCommittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DictationCommittedEventArgs(RoutedEvent? routedEvent, string text)
        : base(routedEvent)
    {
        Text = text;
    }

    /// <summary>The final text, trimmed.</summary>
    public string Text { get; }

    /// <summary>How many words it has.</summary>
    public int WordCount => DictationText.CountWords(Text);
}

/// <summary>Arguments of the status events: the new status and, for denied and error, why.</summary>
public sealed class DictationStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DictationStatusChangedEventArgs(RoutedEvent? routedEvent, DictationStatus status, DictationErrorCode? error)
        : base(routedEvent)
    {
        Status = status;
        Error = error;
    }

    /// <summary>The status now.</summary>
    public DictationStatus Status { get; }

    /// <summary>The error code for <see cref="DictationStatus.Denied"/> and <see cref="DictationStatus.Error"/>, otherwise null.</summary>
    public DictationErrorCode? Error { get; }
}

/// <summary>
/// Arguments of <see cref="DictationButton.SendRequested"/>. Set <see cref="Completion"/> to the task that sends the
/// message: the send button shows a spinner until it finishes and the field clears when it succeeds. When no handler sets
/// it (or marks the event handled), sending is simulated.
/// </summary>
public sealed class DictationSendEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DictationSendEventArgs(RoutedEvent? routedEvent, string text)
        : base(routedEvent)
    {
        Text = text;
    }

    /// <summary>The message to send.</summary>
    public string Text { get; }

    /// <summary>The sending task; the field clears when it completes without an exception.</summary>
    public Task? Completion { get; set; }
}

/// <summary>Word helpers shared by the dictation parts.</summary>
internal static class DictationText
{
    /// <summary>Joins two pieces of text with one space, skipping empty ones, trimmed.</summary>
    public static string Join(string? first, string? second)
    {
        var a = first?.Trim() ?? string.Empty;
        var b = second?.Trim() ?? string.Empty;
        if (a.Length == 0)
        {
            return b;
        }
        return b.Length == 0 ? a : a + " " + b;
    }

    /// <summary>Counts the words (runs of non-space characters).</summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    /// <summary>The words of <paramref name="text"/> (split on white space), appended to <paramref name="into"/>.</summary>
    public static void SplitWords(string? text, List<string> into)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var space = i == text.Length || char.IsWhiteSpace(text[i]);
            if (space)
            {
                if (start >= 0)
                {
                    into.Add(text.Substring(start, i - start));
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
    }
}
