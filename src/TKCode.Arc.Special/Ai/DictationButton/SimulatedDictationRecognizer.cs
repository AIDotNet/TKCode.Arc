using System.Diagnostics;

namespace TKCode.Arc;

/// <summary>
/// One phrase of a scripted dictation: the words the simulated recognizer settles on, and optionally the guess it shows
/// first (a misheard word that it then corrects, the way a real recognizer revises its interim results).
/// </summary>
/// <param name="Text">The settled words.</param>
/// <param name="Guess">The interim guess streamed before the correction, or null to stream <paramref name="Text"/> as is.</param>
public sealed record DictationPhrase(string Text, string? Guess = null);

/// <summary>
/// A recognizer that never touches the microphone: it speaks the <see cref="Scripts"/> you give it as scripted speech with
/// interim guesses, corrections and speech-like levels. Set it as the <see cref="DictationControl.Simulation"/> of a
/// <see cref="DictationControl"/> or a <see cref="DictationButton"/> (where the composer can loop it) for demos and design
/// reviews, or use it as a recognizer in tests. Each start plays the next script; with no scripts the voice stays silent.
/// </summary>
public sealed class SimulatedDictationRecognizer : IDictationRecognizer
{
    private int _next;

    /// <summary>The scripts, played in turn, one per start. Empty (the default) speaks nothing: listening shows only a quiet noise floor.</summary>
    public IReadOnlyList<IReadOnlyList<DictationPhrase>> Scripts { get; set; } = [];

    /// <summary>Rejects every start with <see cref="DictationErrorCode.NotAllowed"/>, like a blocked microphone.</summary>
    public bool Deny { get; set; }

    /// <summary>How long starting takes (asking for the microphone).</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMilliseconds(420);

    /// <summary>How long the recognizer takes to settle the last words after listening stops.</summary>
    public TimeSpan FinalizeDelay { get; set; } = TimeSpan.FromMilliseconds(650);

    /// <summary>The time between words.</summary>
    public TimeSpan WordInterval { get; set; } = TimeSpan.FromMilliseconds(210);

    /// <inheritdoc />
    public bool IsSimulated => true;

    /// <summary>While this returns true the simulation's clock stands still (the autoplay pauses off screen).</summary>
    internal Func<bool>? Paused { get; set; }

    /// <inheritdoc />
    public async Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
    {
        await DictationSimulationClock.Delay(StartDelay, Paused, cancellationToken);
        if (Deny)
        {
            throw new DictationException(DictationErrorCode.NotAllowed);
        }
        var scripts = Scripts ?? [];
        IReadOnlyList<DictationPhrase> script = scripts.Count > 0 ? scripts[_next % scripts.Count] : [];
        _next++;
        var session = new Session(this, handlers, script, cancellationToken);
        session.Begin();
        return session;
    }

    private sealed class Session : IDictationSession
    {
        private readonly SimulatedDictationRecognizer _owner;
        private readonly DictationHandlers _handlers;
        private readonly IReadOnlyList<DictationPhrase> _script;
        private readonly CancellationTokenSource _speech;
        private readonly Random _random;
        private readonly long _origin = Stopwatch.GetTimestamp();
        private string _final = string.Empty;
        private string? _speaking;
        private double _loudness = 0.6;
        private bool _voiced;

        public Session(SimulatedDictationRecognizer owner, DictationHandlers handlers, IReadOnlyList<DictationPhrase> script,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            _handlers = handlers;
            _script = script;
            _speech = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _random = new Random(script.Count * 7919 + (script.Count > 0 ? script[0].Text.Length : 0));
        }

        public void Begin()
        {
            _ = SpeakAsync();
            _ = LevelsAsync();
        }

        public async Task<string> StopAsync()
        {
            // Listening stops: the voice ends at once, the recognizer settles what it heard.
            var spoken = _speaking;
            _speech.Cancel();
            _voiced = false;
            _handlers.OnLevel(0);
            await DictationSimulationClock.Delay(_owner.FinalizeDelay, _owner.Paused, CancellationToken.None);
            if (spoken is not null)
            {
                _final = DictationText.Join(_final, spoken);
            }
            _handlers.OnTranscript(_final, string.Empty);
            _speech.Dispose();
            return _final;
        }

        private async Task SpeakAsync()
        {
            var token = _speech.Token;
            var interval = _owner.WordInterval;
            try
            {
                await Wait(TimeSpan.FromMilliseconds(260), token);
                foreach (var phrase in _script)
                {
                    var words = new List<string>();
                    DictationText.SplitWords(phrase.Guess ?? phrase.Text, words);
                    var settled = new List<string>();
                    DictationText.SplitWords(phrase.Text, settled);
                    _loudness = 0.5 + _random.NextDouble() * 0.35;
                    for (var i = 1; i <= words.Count; i++)
                    {
                        _voiced = true;
                        // Stopped now, the recognizer would settle the words heard so far, corrected.
                        _speaking = string.Join(' ', settled.Take(i));
                        _handlers.OnTranscript(_final, string.Join(' ', words.Take(i)));
                        var length = words[i - 1].Length;
                        await Wait(TimeSpan.FromMilliseconds(interval.TotalMilliseconds * (0.7 + Math.Min(length, 10) * 0.07)), token);
                    }
                    if (phrase.Guess is { } guess && guess != phrase.Text)
                    {
                        await Wait(interval, token);
                        _handlers.OnTranscript(_final, phrase.Text);
                    }
                    _speaking = phrase.Text;
                    _voiced = false;
                    await Wait(TimeSpan.FromMilliseconds(interval.TotalMilliseconds * 1.8), token);
                    _final = DictationText.Join(_final, phrase.Text);
                    _speaking = null;
                    _handlers.OnTranscript(_final, string.Empty);
                    await Wait(TimeSpan.FromMilliseconds(interval.TotalMilliseconds * 1.4), token);
                }
                _handlers.OnSpeechEnd();
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>A speech-like level: syllable pulses at about five per second while voiced, a quiet noise floor between.</summary>
        private async Task LevelsAsync()
        {
            var token = _speech.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var t = Stopwatch.GetElapsedTime(_origin).TotalSeconds;
                    double level;
                    if (_voiced)
                    {
                        var syllable = Math.Abs(Math.Sin(Math.PI * t * 4.6));
                        var wobble = 0.85 + 0.15 * Math.Sin(t * 13.1);
                        level = _loudness * (0.35 + 0.65 * syllable) * wobble + _random.NextDouble() * 0.08;
                    }
                    else
                    {
                        level = 0.02 + _random.NextDouble() * 0.04;
                    }
                    _handlers.OnLevel(level);
                    await Wait(TimeSpan.FromMilliseconds(40), token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task Wait(TimeSpan span, CancellationToken token) => DictationSimulationClock.Delay(span, _owner.Paused, token);
    }
}

/// <summary>
/// The speech source of a dictation control with neither a recognizer nor a simulation: every start fails with
/// <see cref="DictationErrorCode.Unsupported"/>, so pressing the mic says dictation is not available.
/// </summary>
internal sealed class DictationUnavailableRecognizer : IDictationRecognizer
{
    public static DictationUnavailableRecognizer Instance { get; } = new();

    public Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken) =>
        Task.FromException<IDictationSession>(new DictationException(DictationErrorCode.Unsupported));
}

/// <summary>Delays for the simulations that stand still while paused (the autoplay off screen).</summary>
internal static class DictationSimulationClock
{
    public static async Task Delay(TimeSpan span, Func<bool>? paused, CancellationToken token)
    {
        if (span <= TimeSpan.Zero)
        {
            token.ThrowIfCancellationRequested();
            return;
        }
        if (paused is null)
        {
            await Task.Delay(span, token);
            return;
        }
        var remaining = span.TotalMilliseconds;
        while (remaining > 0)
        {
            var step = Math.Min(remaining, 50);
            var before = Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromMilliseconds(step), token);
            if (!paused())
            {
                remaining -= Stopwatch.GetElapsedTime(before).TotalMilliseconds;
            }
        }
    }
}
