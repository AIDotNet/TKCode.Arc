using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The easings the Analytics notes measured (CSS names in brackets).</summary>
internal static class AnalyticsEasings
{
    /// <summary>(.22, 1, .36, 1): colours, exits, count-ups, crossfades (Arc's standard ease).</summary>
    public static readonly Easing Standard = ArcEasings.Standard;

    /// <summary>(.16, 1, .3, 1): entering text, ticks and panels (Arc's enter ease).</summary>
    public static readonly Easing Enter = ArcEasings.Enter;

    /// <summary>CSS <c>ease-out</c> (0, 0, .58, 1): leaving ticks, veil, tooltips appearing.</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>Arc's <c>--ease-spring</c> curve for 580ms (chip swatch scale, arrow rotation, rank bars).</summary>
    public static readonly Easing CssSpring = ArcEasings.CssSpring;
}

/// <summary>
/// A time-based tween evaluated by a block's own frame loop (no scheduler, no allocation): start it with
/// <see cref="Start"/>, call <see cref="Advance"/> with each frame's seconds and read <see cref="Value"/>. A restart
/// begins from the current value (Arc's interrupted WAAPI animations start from the on-screen opacity).
/// </summary>
internal struct AnalyticsTween
{
    private double _from;
    private double _elapsed;
    private double _delay;
    private double _duration;
    private Easing? _easing;

    /// <summary>A tween resting at <paramref name="value"/>.</summary>
    public AnalyticsTween(double value)
    {
        Value = value;
        Target = value;
        _from = value;
        _elapsed = 0;
        _delay = 0;
        _duration = 0;
        _easing = null;
        IsRunning = false;
    }

    /// <summary>Current value.</summary>
    public double Value;

    /// <summary>End value.</summary>
    public double Target;

    /// <summary>True until the end value is reached.</summary>
    public bool IsRunning;

    /// <summary>
    /// Runs from the current value to <paramref name="target"/> in <paramref name="duration"/> after
    /// <paramref name="delay"/>. A zero duration jumps (after the delay).
    /// </summary>
    public void Start(double target, TimeSpan duration, Easing? easing = null, TimeSpan delay = default)
    {
        _from = Value;
        Target = target;
        _elapsed = 0;
        _delay = Math.Max(0, delay.TotalSeconds);
        _duration = Math.Max(0, duration.TotalSeconds);
        _easing = easing ?? AnalyticsEasings.Standard;
        IsRunning = Math.Abs(target - Value) > 1e-9 || _delay > 0;
        if (!IsRunning)
        {
            Value = target;
        }
    }

    /// <summary>Sets the value at once and stops.</summary>
    public void Jump(double value)
    {
        Value = value;
        Target = value;
        _from = value;
        IsRunning = false;
    }

    /// <summary>Advances by <paramref name="dt"/> seconds; returns true while running.</summary>
    public bool Advance(double dt)
    {
        if (!IsRunning)
        {
            return false;
        }
        _elapsed += Math.Max(0, dt);
        var t = _elapsed - _delay;
        if (t < 0)
        {
            return true;
        }
        if (_duration <= 0 || t >= _duration)
        {
            Value = Target;
            IsRunning = false;
            return false;
        }
        var p = _easing?.Ease(t / _duration) ?? t / _duration;
        Value = _from + (Target - _from) * p;
        return true;
    }
}
