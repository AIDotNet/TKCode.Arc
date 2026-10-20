using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The value tag's follower: a soft spring (stiffness 420, damping 30, mass 0.7; Arc's <c>useSpring</c>) that trails the
/// thumb so the tag lags more the faster the thumb moves, and whose speed sets the tag's sway. Its target changes on
/// every frame of a drag, which <see cref="MotionValue{T}"/> would answer with a new task per frame, so it is a small
/// motion driver on the shared frame scheduler instead: it steps only while moving and allocates nothing per frame.
/// </summary>
internal sealed class ElasticSliderSway : IMotionDriver
{
    private static readonly SpringSpec s_spec = SpringSpec.FromPhysical(420, 30, 0.7);
    private readonly WeakReference<Visual> _owner;
    private double _position;
    private double _velocity;
    private double _target;
    private bool _running;

    public ElasticSliderSway(Visual owner) => _owner = new WeakReference<Visual>(owner);

    /// <summary>Raised after every step and jump.</summary>
    public event Action? Changed;

    public double Position => _position;

    /// <summary>Units per second (track per-mille).</summary>
    public double Velocity => _velocity;

    Visual? IMotionDriver.Owner => _owner.TryGetTarget(out var owner) ? owner : null;

    /// <summary>Retargets the follower; without <paramref name="animate"/> it jumps there and rests.</summary>
    public void Follow(double target, bool animate)
    {
        _target = target;
        if (!animate)
        {
            Jump(target);
            return;
        }
        if (_running || (Math.Abs(_position - _target) < 0.01 && Math.Abs(_velocity) < 0.1))
        {
            return;
        }
        _running = _owner.TryGetTarget(out var owner) && MotionScheduler.Register(this, owner);
        if (!_running)
        {
            Jump(target);
        }
    }

    public void Jump(double value)
    {
        _position = value;
        _target = value;
        _velocity = 0;
        Changed?.Invoke();
    }

    bool IMotionDriver.Advance(double dt)
    {
        var (x, v) = SpringSolver.Evaluate(s_spec, _position - _target, _velocity, dt);
        _position = _target + x;
        _velocity = v;
        var atRest = Math.Abs(x) < 0.05 && Math.Abs(v) < 0.5;
        if (atRest)
        {
            _position = _target;
            _velocity = 0;
            _running = false;
        }
        Changed?.Invoke();
        return !atRest;
    }

    void IMotionDriver.Snap()
    {
        _running = false;
        Jump(_target);
    }
}
