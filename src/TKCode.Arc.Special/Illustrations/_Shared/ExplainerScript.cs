namespace TKCode.Arc;

/// <summary>
/// The beats of an explainer's story: actions at story times in seconds, built once from the data. The story clock
/// passes over them in order and runs each beat it crosses. Beats marked as stops are where "Next step" halts. Restart =
/// <see cref="Rewind"/> plus resetting the scene; a replay of the same data runs exactly the same way.
/// </summary>
internal sealed class ExplainerScript
{
    private readonly List<Beat> _beats = new();
    private int _next;

    private readonly record struct Beat(double At, Action Act, bool IsStop);

    /// <summary>The time of the last beat (the end of the story), or a later end set by the builder.</summary>
    public double Length { get; set; }

    /// <summary>The number of beats.</summary>
    public int Count => _beats.Count;

    /// <summary>The index of the next beat to run (0 after <see cref="Rewind"/>; <see cref="Count"/> at the end).</summary>
    public int NextIndex => _next;

    /// <summary>True when every beat has run.</summary>
    public bool IsFinished => _next >= _beats.Count;

    /// <summary>Forgets every beat.</summary>
    public void Clear()
    {
        _beats.Clear();
        _next = 0;
        Length = 0;
    }

    /// <summary>
    /// Adds <paramref name="act"/> at <paramref name="seconds"/>; beats at the same time run in the order they were
    /// added. A <paramref name="isStop"/> beat is a place where stepping halts once it has run.
    /// </summary>
    public void At(double seconds, Action act, bool isStop = false)
    {
        var index = _beats.FindLastIndex(b => b.At <= seconds) + 1;
        _beats.Insert(index, new Beat(seconds, act, isStop));
        Length = Math.Max(Length, seconds);
    }

    /// <summary>Starts over at the first beat (the caller resets its scene).</summary>
    public void Rewind() => _next = 0;

    /// <summary>
    /// Runs every beat up to and including <paramref name="time"/>. Returns true when a stop beat ran (stepping then
    /// halts at that beat's time).
    /// </summary>
    public bool RunTo(double time)
    {
        var stopped = false;
        while (_next < _beats.Count && _beats[_next].At <= time)
        {
            var beat = _beats[_next++];
            beat.Act();
            stopped |= beat.IsStop;
        }
        return stopped;
    }

    /// <summary>The time of the next stop beat after the beats already run, or null when none is left.</summary>
    public double? NextStopTime()
    {
        for (var i = _next; i < _beats.Count; i++)
        {
            if (_beats[i].IsStop)
            {
                return _beats[i].At;
            }
        }
        return null;
    }

    /// <summary>The time of the next beat to run, or null at the end.</summary>
    public double? NextBeatTime() => _next < _beats.Count ? _beats[_next].At : null;
}
