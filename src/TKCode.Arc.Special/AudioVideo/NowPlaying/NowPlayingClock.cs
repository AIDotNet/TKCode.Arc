namespace TKCode.Arc;

/// <summary>
/// The player's own clock: it moves one second per second while running, takes corrections from real audio
/// (<see cref="Correct"/>, ignored while frozen by a scrub), and reports the end of the track. It holds no timer: the
/// player steps it from a frame loop, tests step it directly. A candidate to share with the voice recorder's playback.
/// </summary>
internal sealed class NowPlayingClock
{
    private double _position;
    private double _duration;

    /// <summary>The position in seconds, 0 to <see cref="Duration"/>.</summary>
    public double Position => _position;

    /// <summary>The track length in seconds.</summary>
    public double Duration => _duration;

    /// <summary>True while a scrub holds the clock: it neither moves nor takes corrections.</summary>
    public bool IsFrozen { get; set; }

    /// <summary>Loads a track: the length and a position of zero.</summary>
    public void Load(double duration)
    {
        _duration = double.IsFinite(duration) ? Math.Max(0, duration) : 0;
        _position = 0;
    }

    /// <summary>Moves the position by <paramref name="dt"/> seconds; returns true when this step reached the end.</summary>
    public bool Advance(double dt)
    {
        if (IsFrozen || _duration <= 0 || !(dt > 0))
        {
            return false;
        }
        var before = _position;
        _position = Math.Min(_duration, _position + dt);
        return before < _duration && _position >= _duration;
    }

    /// <summary>Puts the position at <paramref name="seconds"/> (clamped), frozen or not: a seek.</summary>
    public void Seek(double seconds) =>
        _position = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, _duration) : _position;

    /// <summary>Takes a position reported by real audio; ignored while frozen. Returns whether it was taken.</summary>
    public bool Correct(double seconds)
    {
        if (IsFrozen)
        {
            return false;
        }
        Seek(seconds);
        return true;
    }
}
