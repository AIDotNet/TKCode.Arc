using System.Text;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The side panel's streaming cadence (spec 5.4, F8): streamed text of any chunk size goes in with <see cref="Push"/>, and
/// <see cref="Step"/>, called every frame, releases whole words in steps <see cref="Interval"/> apart (Arc: 50ms, the first
/// 60 Hz frame past 34ms; a frame within 4ms of the interval counts). A step shows between <see cref="MinWords"/> and <see cref="MaxWords"/> words (4–7):
/// the mean of the two per interval elapsed, the fraction carried to the next step, so a late frame shows a few more words
/// and the pace does not depend on the frame rate. A backlog longer than <see cref="CatchUpWords"/> is released at
/// <see cref="MaxWords"/> a step until it is caught up. The first step does not wait.
/// </summary>
/// <remarks>
/// The word boundaries come from Core <see cref="WordPacer"/> used as a dispenser of single words (one word per tick, no
/// clock of its own): a word is never split, its trailing whitespace goes with it, and the last word is held until
/// something follows it or the stream is completed. Pure and single-threaded; the caller supplies the time.
/// </remarks>
internal sealed class AiPanelPacer
{
    // One word per Tick: the reading speed is never the limit and a batch holds one word at most.
    private static readonly WordPacerOptions s_single = new()
    {
        MinInterval = TimeSpan.Zero,
        WordsPerSecond = 1e9,
        MaxWordsPerBatch = 1
    };

    /// <summary>
    /// A frame this close to the interval counts as on time: three 60 Hz frames add up to a hair under 50ms, and real frames
    /// jitter by a millisecond or two, which would otherwise push a step to the fourth frame now and then.
    /// </summary>
    private static readonly TimeSpan FrameSlack = TimeSpan.FromMilliseconds(4);

    private readonly StringBuilder _batch = new();
    private WordPacer _words = new(s_single);
    private bool _released;
    private TimeSpan _last;
    private double _carry;

    public AiPanelPacer(TimeSpan interval, int minWords, int maxWords)
    {
        Interval = interval < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : interval;
        MinWords = Math.Max(1, minWords);
        MaxWords = Math.Max(MinWords, maxWords);
    }

    /// <summary>The shortest time between two steps.</summary>
    public TimeSpan Interval { get; }

    /// <summary>The fewest words a step shows (when that many are ready).</summary>
    public int MinWords { get; }

    /// <summary>The most words a step shows.</summary>
    public int MaxWords { get; }

    /// <summary>A backlog of more words than this (one second at the mean pace) is caught up at <see cref="MaxWords"/> a step.</summary>
    public int CatchUpWords => (int)Math.Ceiling((MinWords + MaxWords) / 2.0 * (1000 / Interval.TotalMilliseconds));

    /// <summary>Words ready to be released.</summary>
    public int PendingWords => _words.PendingWords;

    /// <summary>True while any pushed text has not been released, the held last word included.</summary>
    public bool HasPending => _words.HasPending;

    /// <summary>True once the stream is completed and every word was released.</summary>
    public bool IsFinished => _words.IsFinished;

    /// <summary>True once <see cref="Complete"/> was called.</summary>
    public bool IsCompleted => _words.IsCompleted;

    /// <summary>Appends streamed text.</summary>
    public void Push(string text) => _words.Push(text);

    /// <summary>Ends the stream: the held last word becomes releasable.</summary>
    public void Complete() => _words.Complete();

    /// <summary>Drops everything for a new stream.</summary>
    public void Reset()
    {
        _words = new WordPacer(s_single);
        _released = false;
        _last = default;
        _carry = 0;
    }

    /// <summary>
    /// Advances the clock to <paramref name="now"/> and returns the next step's words, or null when no word is ready or the
    /// last step was less than <see cref="Interval"/> ago.
    /// </summary>
    public string? Step(TimeSpan now)
    {
        var ready = _words.PendingWords;
        if (ready == 0)
        {
            return null;
        }
        int count;
        if (!_released)
        {
            count = MinWords;
        }
        else
        {
            var elapsed = now - _last;
            if (elapsed + FrameSlack < Interval)
            {
                return null;
            }
            var exact = _carry + elapsed / Interval * ((MinWords + MaxWords) / 2.0);
            var whole = Math.Floor(exact + 1e-9);
            _carry = Math.Clamp(exact - whole, 0, 0.999999);
            count = Math.Clamp((int)whole, MinWords, MaxWords);
        }
        if (ready > CatchUpWords)
        {
            count = MaxWords;
        }
        count = Math.Min(count, ready);
        _released = true;
        _last = now;
        _batch.Clear();
        for (var i = 0; i < count; i++)
        {
            _batch.Append(_words.Tick(now));
        }
        return _batch.ToString();
    }
}
