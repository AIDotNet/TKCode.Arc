using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Plays a reasoning into a <see cref="ReasoningBlock"/> the way a model streams it, for demos, prototypes and tests:
/// the block thinks, the seconds count, and <see cref="Reasoning"/> arrives three words every 45ms; then the block
/// finishes with the time it took. Nothing is sent to a model.
/// <para>
/// Set it as the block's <see cref="ReasoningBlock.Simulation"/>. The block plays it while it has no reasoning, steps,
/// status or time of its own, starts it over each time it takes it up, and advances it only while it is on screen; Stop
/// on the block stops it. Use one simulation per block. It has no timer of its own: without a block, call
/// <see cref="Advance"/> with the time that passed.
/// </para>
/// </summary>
public sealed class ReasoningBlockSimulation : AvaloniaObject
{
    /// <summary>Words that arrive per tick.</summary>
    private const int WordsPerTick = 3;

    /// <summary>Seconds between ticks.</summary>
    private const double TickSeconds = 0.045;

    /// <summary>
    /// The whole reasoning to stream, written the way <see cref="ReasoningBlock.Reasoning"/> reads it (blank lines split
    /// paragraphs, <c>**Heading**</c> starts a step). A change takes effect the next time the simulation starts.
    /// </summary>
    public static readonly StyledProperty<string?> ReasoningProperty =
        AvaloniaProperty.Register<ReasoningBlockSimulation, string?>(nameof(Reasoning));

    private string[] _words = [];
    private int _shown;
    private double _sinceTick;
    private double _elapsed;
    private string _text = string.Empty;
    private ReasoningStatus _status = ReasoningStatus.Done;
    private bool _started;

    /// <summary>Creates a simulation with no reasoning; set <see cref="Reasoning"/>.</summary>
    public ReasoningBlockSimulation()
    {
    }

    /// <inheritdoc cref="ReasoningProperty"/>
    public string? Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <summary>Where the played block is: <see cref="ReasoningStatus.Thinking"/> while streaming, then Done or Stopped.</summary>
    public ReasoningStatus Status => _status;

    /// <summary>The reasoning streamed so far.</summary>
    public string Text => _text;

    /// <summary>How long the simulated model has thought.</summary>
    public TimeSpan Elapsed => TimeSpan.FromSeconds(_elapsed);

    /// <summary>Whether it has started since it was created.</summary>
    public bool HasStarted => _started;

    /// <summary>Whether it is streaming.</summary>
    public bool IsPlaying => _status == ReasoningStatus.Thinking;

    /// <summary>Raised when the text, the status or the shown second changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised once the last word is in and the simulated model has stopped thinking.</summary>
    public event EventHandler? Finished;

    /// <summary>Starts over: the block thinks with no text and the words stream in from the first.</summary>
    public void Restart()
    {
        _words = string.IsNullOrEmpty(Reasoning) ? [] : Reasoning.Split(' ');
        _shown = 0;
        _sinceTick = 0;
        _elapsed = 0;
        _text = string.Empty;
        _status = ReasoningStatus.Thinking;
        _started = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops where it is, as the person pressing Stop does: the status becomes Stopped and the text stays.</summary>
    public void Stop()
    {
        if (_status != ReasoningStatus.Thinking)
        {
            return;
        }
        _status = ReasoningStatus.Stopped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the simulation on by <paramref name="time"/>; returns whether anything on screen changed. The thinking time
    /// stops at the tick that finishes, however much time is passed.
    /// </summary>
    public bool Advance(TimeSpan time)
    {
        if (_status != ReasoningStatus.Thinking || time <= TimeSpan.Zero)
        {
            return false;
        }
        var second = (long)_elapsed;
        var remaining = time.TotalSeconds;
        var changed = false;
        while (remaining > 0 && _status == ReasoningStatus.Thinking)
        {
            var step = Math.Min(remaining, TickSeconds - _sinceTick);
            remaining -= step;
            _elapsed += step;
            _sinceTick += step;
            if (_sinceTick < TickSeconds - 1e-9)
            {
                continue;
            }
            _sinceTick = 0;
            changed = true;
            if (_shown >= _words.Length)
            {
                _status = ReasoningStatus.Done;
            }
            else
            {
                _shown = Math.Min(_words.Length, _shown + WordsPerTick);
                _text = string.Join(' ', _words, 0, _shown);
            }
        }
        changed |= (long)_elapsed != second;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        if (_status == ReasoningStatus.Done)
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
        return changed;
    }
}
