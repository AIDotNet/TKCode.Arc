using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// P16 morph driver: an array of values that each run their own spring towards a target array, driven by the
/// shared <see cref="MotionScheduler"/> frame. Retargeting mid-flight keeps every component's position and
/// velocity (like a <see cref="MotionValue{T}"/> per point, without one object per point). The closed-form
/// spring is evaluated once per frame and applied to all components, so a frame allocates nothing.
/// </summary>
internal sealed class ChartSpringArray : IMotionDriver
{
    private readonly WeakReference<Visual> _owner;
    private double[] _position = [];
    private double[] _velocity = [];
    private double[] _target = [];
    private double[] _start = [];
    private double[] _startVelocity = [];
    private SpringSpec _spec = ArcMotionPresets.Smooth;
    private double _elapsed;
    private double _restDelta = 0.001;
    private bool _animating;

    public ChartSpringArray(Visual owner) => _owner = new WeakReference<Visual>(owner);

    /// <summary>Raised after every frame step and after jumps.</summary>
    public event Action? Changed;

    /// <summary>Raised once when the springs come to rest at the target.</summary>
    public event Action? Settled;

    public int Length { get; private set; }

    public bool IsAnimating => _animating;

    /// <summary>Current (on-screen) values.</summary>
    public ReadOnlySpan<double> Values => _position.AsSpan(0, Length);

    /// <summary>Current velocities (units per second).</summary>
    public ReadOnlySpan<double> Velocities => _velocity.AsSpan(0, Length);

    public double this[int index] => _position[index];

    Visual? IMotionDriver.Owner => _owner.TryGetTarget(out var owner) ? owner : null;

    /// <summary>
    /// Re-grids the array: <paramref name="remap"/> receives the old values and velocities and writes the new
    /// ones (for example by interpolating onto a new x grid). Runs once per data change.
    /// </summary>
    public void Reshape(int length, RemapAction remap)
    {
        var position = new double[length];
        var velocity = new double[length];
        remap(_position.AsSpan(0, Length), _velocity.AsSpan(0, Length), position, velocity);
        _position = position;
        _velocity = velocity;
        _target = new double[length];
        _start = new double[length];
        _startVelocity = new double[length];
        position.CopyTo(_target, 0);
        Length = length;
    }

    public delegate void RemapAction(ReadOnlySpan<double> oldValues, ReadOnlySpan<double> oldVelocities, Span<double> values, Span<double> velocities);

    /// <summary>Sets every value at once and stops.</summary>
    public void Jump(ReadOnlySpan<double> values)
    {
        EnsureLength(values.Length);
        values.CopyTo(_position);
        values.CopyTo(_target);
        Array.Clear(_velocity);
        _animating = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Springs every component to <paramref name="target"/> from where it is now, keeping its velocity. Under
    /// Reduced or Off motion (or when detached) the values jump.
    /// </summary>
    public void AnimateTo(ReadOnlySpan<double> target, SpringSpec spec)
    {
        EnsureLength(target.Length);
        var owner = _owner.TryGetTarget(out var o) ? o : null;
        if (owner is null || ArcMotion.GetEffectiveLevel(owner) != ArcMotionLevel.Full)
        {
            Jump(target);
            Settled?.Invoke();
            return;
        }
        target.CopyTo(_target);
        _position.AsSpan(0, Length).CopyTo(_start);
        _velocity.AsSpan(0, Length).CopyTo(_startVelocity);
        var largest = 0.0;
        for (var i = 0; i < Length; i++)
        {
            largest = Math.Max(largest, Math.Abs(_start[i] - _target[i]));
        }
        if (largest < 1e-12 && !HasVelocity())
        {
            Jump(target);
            Settled?.Invoke();
            return;
        }
        _restDelta = Math.Max(largest, 1e-9) * 0.0005;
        _spec = spec;
        _elapsed = 0;
        if (!_animating)
        {
            _animating = true;
            if (!MotionScheduler.Register(this, owner))
            {
                Jump(target);
                Settled?.Invoke();
            }
        }
    }

    private bool HasVelocity()
    {
        for (var i = 0; i < Length; i++)
        {
            if (Math.Abs(_velocity[i]) > 1e-9)
            {
                return true;
            }
        }
        return false;
    }

    private void EnsureLength(int length)
    {
        if (length == Length)
        {
            return;
        }
        Reshape(length, static (_, _, values, _) => values.Clear());
    }

    void IMotionDriver.Snap()
    {
        _target.AsSpan(0, Length).CopyTo(_position);
        Array.Clear(_velocity);
        _animating = false;
        Changed?.Invoke();
        Settled?.Invoke();
    }

    bool IMotionDriver.Advance(double dt)
    {
        if (!_animating)
        {
            return false;
        }
        _elapsed += dt;
        // The oscillator is linear in (x0, v0): evaluate the two basis solutions once, combine per component.
        var (a1, c1) = SpringSolver.Evaluate(_spec, 1, 0, _elapsed);
        var (a2, c2) = SpringSolver.Evaluate(_spec, 0, 1, _elapsed);
        var atRest = true;
        var speedLimit = _restDelta * 2 / 0.016;
        for (var i = 0; i < Length; i++)
        {
            var x0 = _start[i] - _target[i];
            var v0 = _startVelocity[i];
            var x = a1 * x0 + a2 * v0;
            var v = c1 * x0 + c2 * v0;
            _position[i] = _target[i] + x;
            _velocity[i] = v;
            if (Math.Abs(x) > _restDelta || Math.Abs(v) > speedLimit)
            {
                atRest = false;
            }
        }
        if (atRest)
        {
            _target.AsSpan(0, Length).CopyTo(_position);
            Array.Clear(_velocity);
            _animating = false;
            Changed?.Invoke();
            Settled?.Invoke();
            return false;
        }
        Changed?.Invoke();
        return true;
    }
}
