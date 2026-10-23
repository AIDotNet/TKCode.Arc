namespace TKCode.Arc.Primitives;

/// <summary>Cadence of a <see cref="WordPacer"/>. The defaults are Arc's measured streaming cadence.</summary>
public sealed record WordPacerOptions
{
    /// <summary>Arc's cadence: 34 ms clock, 95 words/s, up to 8 words a batch, 1 s catch-up, 600 ms drain.</summary>
    public static WordPacerOptions Default { get; } = new();

    /// <summary>
    /// The shortest time between two releases. Releases follow the caller's frames, so at 60 Hz the 34 ms default lands
    /// on every 3rd frame (50 ms) and at 120 Hz on every 5th (about 41.7 ms).
    /// </summary>
    public TimeSpan MinInterval { get; init; } = TimeSpan.FromMilliseconds(34);

    /// <summary>Reading speed: words released per second of elapsed time (4–5 words a batch at 60 Hz).</summary>
    public double WordsPerSecond { get; init; } = 95;

    /// <summary>The largest batch at reading speed (catching up and draining may release more).</summary>
    public int MaxWordsPerBatch { get; init; } = 8;

    /// <summary>
    /// The longest the shown text may lag behind: a backlog that would take longer than this at reading speed is
    /// released fast enough to be shown within it.
    /// </summary>
    public TimeSpan CatchUpWindow { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>After <see cref="WordPacer.Complete"/>, the rest is released within this time.</summary>
    public TimeSpan CompletionDrain { get; init; } = TimeSpan.FromMilliseconds(600);
}

/// <summary>
/// Arc's streaming cadence: streamed text of any chunk size goes in with <see cref="Push"/>, and every frame
/// <see cref="Tick"/> hands out whole words on a clock of at least <see cref="WordPacerOptions.MinInterval"/>, about
/// <see cref="WordPacerOptions.WordsPerSecond"/> words a second. A word is a run of non-whitespace characters with the
/// whitespace after it (leading whitespace of the text goes with the first word), and each CJK ideograph, kana or hangul
/// element is a word; the last word is held until something follows it or the stream is completed, so a release never
/// splits a word. A backlog longer than <see cref="WordPacerOptions.CatchUpWindow"/> is caught up, and after
/// <see cref="Complete"/> the rest drains within <see cref="WordPacerOptions.CompletionDrain"/>.
/// </summary>
/// <remarks>
/// Pure and single-threaded: the caller supplies the time (one clock for every pacer shown together keeps them in step)
/// and appends each release to its view, where it can fade in as one batch. A frame loop only needs to run while
/// <see cref="PendingWords"/> is above zero. A tick that releases nothing allocates nothing.
/// </remarks>
public sealed class WordPacer
{
    private readonly WordPacerOptions _options;
    // Unreleased text is _chars[_start.._end). Word ends are buffer indexes; _ends[_endHead..] are not yet released.
    private char[] _chars = [];
    private int _start;
    private int _end;
    private readonly List<int> _ends = [];
    private int _endHead;
    // The unfinished word starts at _wordStart; scanning resumes at _cursor, inside it, in phase _phase.
    private int _wordStart;
    private int _cursor;
    private ScanPhase _phase;
    private bool _completed;
    private bool _hasReleased;
    private TimeSpan _lastRelease;
    private double _carry;
    private double _catchUpRate;
    private double _drainRate;

    /// <summary>Creates a pacer with <paramref name="options"/> (defaults to <see cref="WordPacerOptions.Default"/>).</summary>
    public WordPacer(WordPacerOptions? options = null)
    {
        _options = options ?? WordPacerOptions.Default;
        if (_options.MinInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MinInterval must not be negative.");
        }
        if (!(_options.WordsPerSecond > 0) || double.IsInfinity(_options.WordsPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "WordsPerSecond must be positive and finite.");
        }
        if (_options.MaxWordsPerBatch < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxWordsPerBatch must be at least 1.");
        }
        if (_options.CatchUpWindow <= TimeSpan.Zero || _options.CompletionDrain <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CatchUpWindow and CompletionDrain must be positive.");
        }
    }

    /// <summary>The cadence in use.</summary>
    public WordPacerOptions Options => _options;

    /// <summary>Whole words ready to be released (the held last word counts once <see cref="Complete"/> was called).</summary>
    public int PendingWords => _ends.Count - _endHead;

    /// <summary>True while any pushed text has not been released yet, including a held last word.</summary>
    public bool HasPending => _end > _start;

    /// <summary>True once <see cref="Complete"/> was called.</summary>
    public bool IsCompleted => _completed;

    /// <summary>True when the stream is completed and everything has been released.</summary>
    public bool IsFinished => _completed && _end == _start;

    /// <summary>Appends streamed text. Ignored when empty or after <see cref="Complete"/>.</summary>
    public void Push(string text)
    {
        if (string.IsNullOrEmpty(text) || _completed)
        {
            return;
        }
        Append(text);
        Scan();
    }

    /// <summary>Ends the stream: the held last word becomes releasable and the rest drains within <see cref="WordPacerOptions.CompletionDrain"/>.</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        _completed = true;
        if (_end > _wordStart)
        {
            _ends.Add(_end);
            _wordStart = _end;
            _cursor = _end;
            _phase = ScanPhase.Lead;
        }
    }

    /// <summary>
    /// Advances the clock to <paramref name="now"/> (call it every frame) and returns the next batch of whole words, or
    /// null when no word is ready or less than <see cref="WordPacerOptions.MinInterval"/> has passed since the last
    /// release. The first release of a stream does not wait. A batch holds <c>floor(carry + elapsed × rate)</c> words,
    /// at least one and at most <see cref="WordPacerOptions.MaxWordsPerBatch"/> at reading speed; the fraction carries
    /// over to the next batch.
    /// </summary>
    /// <param name="now">The current time on a monotonic clock (any origin).</param>
    public string? Tick(TimeSpan now)
    {
        var backlog = PendingWords;
        if (backlog == 0)
        {
            return null;
        }
        // The first release counts one MinInterval of reading time (about 3 words at the default speed).
        var elapsed = _options.MinInterval;
        if (_hasReleased)
        {
            elapsed = now - _lastRelease;
            if (elapsed < _options.MinInterval)
            {
                return null;
            }
        }
        var count = BatchSize(backlog, elapsed.TotalSeconds);
        _hasReleased = true;
        _lastRelease = now;
        var text = Release(count);
        if (PendingWords == 0)
        {
            // The backlog is shown: a later one starts at reading speed again.
            _catchUpRate = 0;
        }
        return text;
    }

    /// <summary>Clears everything for a new stream (the next release does not wait).</summary>
    public void Reset()
    {
        _chars = [];
        _start = _end = 0;
        _ends.Clear();
        _endHead = 0;
        _wordStart = _cursor = 0;
        _phase = ScanPhase.Lead;
        _completed = false;
        _hasReleased = false;
        _lastRelease = default;
        _carry = 0;
        _catchUpRate = 0;
        _drainRate = 0;
    }

    private int BatchSize(int backlog, double elapsed)
    {
        var speed = _options.WordsPerSecond;
        // Catching up and draining keep their rate until the backlog is shown, so it drains evenly within the window
        // instead of slowing down as it shrinks.
        var window = _options.CatchUpWindow.TotalSeconds;
        if (backlog > speed * window)
        {
            _catchUpRate = Math.Max(_catchUpRate, backlog / window);
        }
        if (_completed)
        {
            _drainRate = Math.Max(_drainRate, backlog / _options.CompletionDrain.TotalSeconds);
        }
        var rate = Math.Max(speed, Math.Max(_catchUpRate, _drainRate));
        var exact = _carry + elapsed * rate;
        // The epsilon keeps 0.6 s × (n / 0.6 s) from flooring to n − 1.
        var whole = Math.Floor(exact + 1e-9);
        _carry = Math.Clamp(exact - whole, 0, 0.999999);
        var words = whole >= backlog ? backlog : (int)whole;
        return rate > speed ? Math.Clamp(words, 1, backlog) : Math.Clamp(words, 1, Math.Min(_options.MaxWordsPerBatch, backlog));
    }

    private string Release(int count)
    {
        _endHead += count;
        var end = _ends[_endHead - 1];
        var text = new string(_chars, _start, end - _start);
        _start = end;
        if (_start == _end)
        {
            Rebase();
        }
        return text;
    }

    private void Append(string text)
    {
        if (_end + text.Length > _chars.Length)
        {
            var live = _end - _start;
            var target = _chars;
            if (live + text.Length > _chars.Length)
            {
                target = new char[Math.Max(Math.Max(_chars.Length * 2, 256), live + text.Length)];
            }
            Array.Copy(_chars, _start, target, 0, live);
            _chars = target;
            Rebase();
        }
        text.CopyTo(0, _chars, _end, text.Length);
        _end += text.Length;
    }

    /// <summary>Moves every index so the unreleased text starts at 0 and drops released word ends.</summary>
    private void Rebase()
    {
        var offset = _start;
        _ends.RemoveRange(0, _endHead);
        _endHead = 0;
        if (offset == 0)
        {
            return;
        }
        for (var i = 0; i < _ends.Count; i++)
        {
            _ends[i] -= offset;
        }
        _start = 0;
        _end -= offset;
        _wordStart -= offset;
        _cursor -= offset;
    }

    /// <summary>
    /// Finds the words completed by the text appended since the last scan. A word is complete once anything follows
    /// its core (whitespace, or the next word); its trailing whitespace is taken as far as it has arrived.
    /// </summary>
    private void Scan()
    {
        var span = _chars.AsSpan(0, _end);
        while (true)
        {
            if (_phase == ScanPhase.Lead)
            {
                SkipSpace(span);
                if (_cursor >= _end)
                {
                    return;
                }
                var length = WordText.ElementLength(span[_cursor..]);
                if (WordText.Classify(span.Slice(_cursor, length)) == WordElementKind.Cjk)
                {
                    // The element may still take a combining mark; it is a whole word once anything follows it.
                    if (_cursor + length >= _end)
                    {
                        return;
                    }
                    _cursor += length;
                    _phase = ScanPhase.Trail;
                }
                else
                {
                    _phase = ScanPhase.Word;
                }
            }
            if (_phase == ScanPhase.Word)
            {
                while (_cursor < _end)
                {
                    var length = WordText.ElementLength(span[_cursor..]);
                    if (WordText.Classify(span.Slice(_cursor, length)) != WordElementKind.Word)
                    {
                        break;
                    }
                    _cursor += length;
                }
                if (_cursor >= _end)
                {
                    return;
                }
                _phase = ScanPhase.Trail;
            }
            SkipSpace(span);
            _ends.Add(_cursor);
            _wordStart = _cursor;
            _phase = ScanPhase.Lead;
            if (_cursor >= _end)
            {
                return;
            }
        }
    }

    private void SkipSpace(ReadOnlySpan<char> span)
    {
        while (_cursor < _end)
        {
            var length = WordText.ElementLength(span[_cursor..]);
            if (WordText.Classify(span.Slice(_cursor, length)) != WordElementKind.Space)
            {
                return;
            }
            _cursor += length;
        }
    }

    private enum ScanPhase
    {
        /// <summary>Before the word's core (skipping leading whitespace).</summary>
        Lead,

        /// <summary>Inside a run of word characters.</summary>
        Word,

        /// <summary>After the core (taking trailing whitespace).</summary>
        Trail
    }
}
