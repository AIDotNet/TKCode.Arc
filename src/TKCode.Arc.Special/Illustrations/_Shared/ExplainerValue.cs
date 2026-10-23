using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A scalar animated on an explainer's story clock (seconds of simulated time): a tween on an easing or a spring that
/// starts from the current value and velocity. A struct evaluated on demand, so animating allocates nothing, needs no
/// per-value frame callbacks, and pauses, steps and scales with the story's speed for free (a MotionValue runs on wall
/// time instead, so it is only for UI chrome).
/// </summary>
internal struct ExplainerValue
{
    private double _from;
    private double _to;
    private double _velocity;
    private double _start;
    private double _duration;
    private SpringSpec _spring;
    private Easing? _easing;
    private bool _isSpring;

    /// <summary>A value resting at <paramref name="value"/>.</summary>
    public ExplainerValue(double value)
    {
        _from = _to = value;
        _duration = 0;
    }

    /// <summary>The value it is heading to.</summary>
    public double Target => _to;

    /// <summary>The value at story time <paramref name="now"/>.</summary>
    public double Value(double now)
    {
        var t = now - _start;
        if (_isSpring)
        {
            if (t <= 0)
            {
                return _from;
            }
            var (x, _) = SpringSolver.Evaluate(_spring, _from - _to, _velocity, t);
            return _to + x;
        }
        if (_duration <= 0 || t >= _duration)
        {
            return _to;
        }
        if (t <= 0)
        {
            return _from;
        }
        var p = t / _duration;
        return _from + (_to - _from) * (_easing?.Ease(p) ?? p);
    }

    private double Velocity(double now)
    {
        if (_isSpring)
        {
            var t = Math.Max(0, now - _start);
            return SpringSolver.Evaluate(_spring, _from - _to, _velocity, t).Velocity;
        }
        return 0;
    }

    /// <summary>True while it still moves at <paramref name="now"/>.</summary>
    public bool IsMoving(double now)
    {
        if (_isSpring)
        {
            var t = now - _start;
            if (t < 0)
            {
                return true;
            }
            var (x, v) = SpringSolver.Evaluate(_spring, _from - _to, _velocity, t);
            return Math.Abs(x) > 0.0005 || Math.Abs(v) > 0.005;
        }
        return now - _start < _duration;
    }

    /// <summary>Rests at <paramref name="value"/> at once.</summary>
    public void Jump(double value)
    {
        _from = _to = value;
        _velocity = 0;
        _duration = 0;
        _isSpring = false;
    }

    /// <summary>Tweens from the current value to <paramref name="to"/>, starting after <paramref name="delay"/>.</summary>
    public void Tween(double to, double now, double duration, Easing? easing = null, double delay = 0)
    {
        _from = Value(now);
        _to = to;
        _start = now + delay;
        _duration = duration;
        _easing = easing ?? ArcEasings.Standard;
        _velocity = 0;
        _isSpring = false;
    }

    /// <summary>Springs to <paramref name="to"/> keeping the current velocity.</summary>
    public void Spring(double to, double now, in SpringSpec spring, double delay = 0)
    {
        var velocity = Velocity(now);
        _from = Value(now);
        _to = to;
        _start = now + delay;
        _spring = spring;
        _velocity = velocity;
        _isSpring = true;
    }
}
