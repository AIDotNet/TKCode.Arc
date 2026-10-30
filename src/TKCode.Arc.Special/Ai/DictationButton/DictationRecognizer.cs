using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A speech source for <see cref="DictationControl"/> and <see cref="DictationButton"/>: the app's adapter for a platform
/// recognizer or a speech service. TKCode.Arc records nothing itself; the recognizer supplies audio levels and words.
/// <para>
/// <see cref="StartAsync"/> completes once audio flows (throw a <see cref="DictationException"/> with
/// <see cref="DictationErrorCode.NotAllowed"/> for a blocked microphone). While it runs, report through the
/// <see cref="DictationHandlers"/>: the cumulative transcript, the input level (0 to 1, as often as you like), the end of an
/// utterance and errors. The token is cancelled when the person cancels: release the microphone then. To finish, the
/// control calls <see cref="IDictationSession.StopAsync"/> and inserts the text it returns.
/// </para>
/// </summary>
public interface IDictationRecognizer
{
    /// <summary>Starts listening. Completes once audio flows; cancel <paramref name="cancellationToken"/> to abort.</summary>
    Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken);

    /// <summary>True for a recognizer that only simulates speech (the control then says nothing is recorded).</summary>
    bool IsSimulated => false;
}

/// <summary>A running dictation returned by <see cref="IDictationRecognizer.StartAsync"/>.</summary>
public interface IDictationSession
{
    /// <summary>Stops listening, lets the recognizer settle the last words and returns the final text of the session.</summary>
    Task<string> StopAsync();
}

/// <summary>
/// The callbacks a <see cref="IDictationRecognizer"/> reports through. Each may be called from any thread; calls that
/// arrive after the session ended are ignored.
/// </summary>
public sealed class DictationHandlers
{
    private readonly Action<DictationTranscript> _transcript;
    private readonly Action<double> _level;
    private readonly Action _speechEnd;
    private readonly Action<DictationErrorCode> _error;

    /// <summary>Creates handlers that forward to the given callbacks (the control makes its own; tests and adapters may too).</summary>
    public DictationHandlers(Action<DictationTranscript> transcript, Action<double> level, Action speechEnd, Action<DictationErrorCode> error)
    {
        _transcript = transcript;
        _level = level;
        _speechEnd = speechEnd;
        _error = error;
    }

    /// <summary>Reports the cumulative transcript of this session: the settled words and the current guess after them.</summary>
    public void OnTranscript(string final, string interim) => OnUiThread(() => _transcript(new DictationTranscript(final ?? string.Empty, interim ?? string.Empty)));

    /// <summary>Reports the input level, 0 (silence) to 1 (loud). Cheap: it stores a number the waveform reads per frame.</summary>
    public void OnLevel(double level) => _level(double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0);

    /// <summary>Reports that the recognizer detected the end of an utterance.</summary>
    public void OnSpeechEnd() => OnUiThread(_speechEnd);

    /// <summary>Reports a failure while listening; the dictation stops and shows the error.</summary>
    public void OnError(DictationErrorCode code) => OnUiThread(() => _error(code));

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
