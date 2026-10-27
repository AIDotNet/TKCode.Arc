using Avalonia;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Springs measured in the Analytics notes (fitted from frame captures, mass 1). Core presets are re-exported under the
/// names the notes use, and measured springs that match no Core preset are built with <see cref="SpringSpec.FromVisual"/>
/// or <see cref="SpringSpec.FromPhysical"/>. Use them with <see cref="MotionValue{T}"/> (<c>MotionSpec.Spring(…)</c>),
/// <see cref="AnalyticsSpringChannel"/> or <see cref="AnalyticsSpringArray"/>.
/// </summary>
internal static class AnalyticsSprings
{
    /// <summary>
    /// Core Smooth (k 171, c 26.2, no bounce): data morphs, y domains, rolling digits, share bars, most segmented thumbs
    /// (KPI drilldown, MRR waterfall, Usage forecast, Horizon chart, Metric explorer), KPI underline (Metrics dashboard).
    /// </summary>
    public static readonly SpringSpec Smooth = ArcMotionPresets.Smooth;

    /// <summary>Core Quick (k 439, c 41.9): Metrics dashboard range thumb (ω ≈ 20), Journey flow thumb, small switch thumb.</summary>
    public static readonly SpringSpec Quick = ArcMotionPresets.Quick;

    /// <summary>Core Gentle (k 340, c 34): Revenue explorer brush snap and preset pill.</summary>
    public static readonly SpringSpec Gentle = ArcMotionPresets.Gentle;

    /// <summary>Core Snappy (bounce 0.12): Usage forecast budget handle.</summary>
    public static readonly SpringSpec Snappy = ArcMotionPresets.Snappy;

    /// <summary>KPI drilldown card → detail panel (size and position): visualDuration 0.5, bounce 0 (k 110, c 21).</summary>
    public static readonly SpringSpec Expand = SpringSpec.FromVisual(0.5, 0);

    /// <summary>Journey flow head-count morph: visualDuration 0.62, bounce 0 (k 71, c 16.9).</summary>
    public static readonly SpringSpec Flow = SpringSpec.FromVisual(0.62, 0);

    /// <summary>MRR waterfall selection marker: k 1100, c 80 (about 400ms to rest, no overshoot).</summary>
    public static readonly SpringSpec MarkerSnap = SpringSpec.FromPhysical(1100, 80);

    /// <summary>Revenue explorer readout panel x (k 380, c 35, ζ ≈ 0.9).</summary>
    public static readonly SpringSpec ReadoutGlide = SpringSpec.FromPhysical(380, 35);

    /// <summary>Cohort retention tooltip position: k 600, c 48 (≈ 250ms to 99%).</summary>
    public static readonly SpringSpec ReadoutFollow = SpringSpec.FromPhysical(600, 48);

    /// <summary>Uptime status hover card between bars: k 925, c 58 (≈ 240ms to 99%).</summary>
    public static readonly SpringSpec CardFollow = SpringSpec.FromPhysical(925, 58);

    /// <summary>Horizon chart band fold: visualDuration 0.6, bounce 0.</summary>
    public static readonly SpringSpec Fold = SpringSpec.FromVisual(0.6, 0);

    /// <summary>Horizon chart row reorder: visualDuration 0.55, bounce 0.</summary>
    public static readonly SpringSpec Reorder = SpringSpec.FromVisual(0.55, 0);

    /// <summary>Horizon chart scrub line: visualDuration 0.16, bounce 0 (k ≈ 1000, c ≈ 63).</summary>
    public static readonly SpringSpec Scrub = SpringSpec.FromVisual(0.16, 0);

    /// <summary>Cohort retention heatmap ↔ curves master progress: k 72, c 15.3 (ζ ≈ 0.9).</summary>
    public static readonly SpringSpec CohortMorph = SpringSpec.FromPhysical(72, 15.3);
}

/// <summary>
/// One spring-driven scalar advanced by explicit time steps. Each <see cref="Step"/> evaluates the closed-form damped
/// oscillator from the current position and velocity, so it is exact for any step length, and a retarget keeps the
/// velocity. Use it inside a block's single frame loop when several values must move in lockstep with clamps between
/// them (a ControlTheme transition cannot express a chase toward a target that moves every frame).
/// </summary>
internal struct AnalyticsSpringChannel
{
    /// <summary>A channel resting at <paramref name="value"/>.</summary>
    public AnalyticsSpringChannel(double value, double restDelta = 0.001, double restSpeed = 0.01)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        Spec = AnalyticsSprings.Smooth;
        RestDelta = restDelta;
        RestSpeed = restSpeed;
        IsMoving = false;
    }

    /// <summary>Current value.</summary>
    public double Value;

    /// <summary>Current velocity (units per second).</summary>
    public double Velocity;

    /// <summary>The value the spring heads to.</summary>
    public double Target;

    /// <summary>The spring of the current motion.</summary>
    public SpringSpec Spec;

    /// <summary>Distance to the target under which the channel may come to rest.</summary>
    public double RestDelta;

    /// <summary>Speed under which the channel may come to rest.</summary>
    public double RestSpeed;

    /// <summary>A spring is running.</summary>
    public bool IsMoving;

    /// <summary>Sets the value at once and stops (velocity 0).</summary>
    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        IsMoving = false;
    }

    /// <summary>Stops where it is.</summary>
    public void Stop()
    {
        Target = Value;
        Velocity = 0;
        IsMoving = false;
    }

    /// <summary>Springs to <paramref name="target"/> from the current value, keeping the current velocity.</summary>
    public void SpringTo(double target, in SpringSpec spec)
    {
        Target = target;
        Spec = spec;
        IsMoving = Math.Abs(Value - target) > RestDelta || Math.Abs(Velocity) > RestSpeed;
        if (!IsMoving)
        {
            Value = target;
            Velocity = 0;
        }
    }

    /// <summary>Advances by <paramref name="dt"/> seconds; returns true while still moving.</summary>
    public bool Step(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        if (dt <= 0)
        {
            return true;
        }
        var (x, v) = SpringSolver.Evaluate(Spec, Value - Target, Velocity, dt);
        Value = Target + x;
        Velocity = v;
        if (Math.Abs(x) <= RestDelta && Math.Abs(v) <= RestSpeed)
        {
            Value = Target;
            Velocity = 0;
            IsMoving = false;
        }
        return IsMoving;
    }
}

/// <summary>
/// N analytic springs moved by one <see cref="LiveLoop"/>: a series' y values, bar ends, share bar fills, per-region
/// fills. <see cref="SetTargets(ReadOnlySpan{double})"/> retargets every element (velocities carry); a different count
/// reshapes the current values by linear resampling first, so a 30-point line morphs into a 90-point one from where it
/// is. Under reduced or no motion, or while the owner is detached, targets are applied at once. <see cref="Changed"/>
/// fires after each frame (invalidate the plot there), <see cref="Settled"/> when every element rests.
/// </summary>
/// <remarks>
/// Why code-driven: every element is a coordinate inside one self-drawn geometry (a ControlTheme transition animates
/// properties, not array entries), and a retarget mid-flight must keep each element's velocity.
/// </remarks>
internal sealed class AnalyticsSpringArray : IDisposable
{
    private readonly Visual _owner;
    private readonly LiveLoop _loop;
    private double[] _values = [];
    private double[] _velocities = [];
    private double[] _targets = [];
    private int _count;

    /// <param name="owner">The visual whose attachment and motion level gate the motion (usually the plot).</param>
    /// <param name="spec">The spring (default <see cref="AnalyticsSprings.Smooth"/>).</param>
    public AnalyticsSpringArray(Visual owner, SpringSpec? spec = null)
    {
        _owner = owner;
        Spec = spec ?? AnalyticsSprings.Smooth;
        // Function, not decoration: the loop must also finish motions after the level drops (it then snaps).
        _loop = new LiveLoop(owner, Advance) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>Raised after every frame that moved the values (and after a jump).</summary>
    public event Action? Changed;

    /// <summary>Raised once when every element has come to rest.</summary>
    public event Action? Settled;

    /// <summary>The spring of the next retarget.</summary>
    public SpringSpec Spec { get; set; }

    /// <summary>Distance under which an element may rest (in value units; default 0.001).</summary>
    public double RestDelta { get; set; } = 0.001;

    /// <summary>Speed under which an element may rest (value units per second; default 0.01).</summary>
    public double RestSpeed { get; set; } = 0.01;

    /// <summary>Elements.</summary>
    public int Count => _count;

    /// <summary>Current values (read in Render).</summary>
    public ReadOnlySpan<double> Values => _values.AsSpan(0, _count);

    /// <summary>Targets.</summary>
    public ReadOnlySpan<double> Targets => _targets.AsSpan(0, _count);

    /// <summary>True while any element moves.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Current value of element <paramref name="index"/>.</summary>
    public double this[int index] => _values[index];

    /// <summary>Sets values and targets at once (first data, reduced motion), velocities 0.</summary>
    public void Jump(ReadOnlySpan<double> values)
    {
        EnsureCapacity(values.Length);
        _count = values.Length;
        values.CopyTo(_values);
        values.CopyTo(_targets);
        Array.Clear(_velocities, 0, _count);
        var wasMoving = IsMoving;
        IsMoving = false;
        _loop.Stop();
        Changed?.Invoke();
        if (wasMoving)
        {
            Settled?.Invoke();
        }
    }

    /// <summary>Springs every element to its new target; a new count reshapes the current state first.</summary>
    public void SetTargets(ReadOnlySpan<double> targets)
    {
        if (_count == 0 || !ShouldAnimate())
        {
            Jump(targets);
            return;
        }
        if (targets.Length != _count)
        {
            Reshape(targets.Length);
        }
        targets.CopyTo(_targets);
        IsMoving = true;
        _loop.Start();
    }

    /// <summary>Springs one element (the others keep their motion).</summary>
    public void SetTarget(int index, double target)
    {
        if (!ShouldAnimate())
        {
            _values[index] = target;
            _targets[index] = target;
            _velocities[index] = 0;
            Changed?.Invoke();
            return;
        }
        _targets[index] = target;
        IsMoving = true;
        _loop.Start();
    }

    /// <summary>
    /// Changes the count by resampling values and velocities linearly over the element index (first and last kept), so a
    /// morph to a different number of points starts from the shape on screen.
    /// </summary>
    public void Reshape(int count)
    {
        count = Math.Max(0, count);
        if (count == _count)
        {
            return;
        }
        var oldCount = _count;
        var values = new double[Math.Max(count, 1)];
        var velocities = new double[Math.Max(count, 1)];
        var targets = new double[Math.Max(count, 1)];
        AnalyticsCurves.ResampleLinear(_values.AsSpan(0, oldCount), values.AsSpan(0, count));
        AnalyticsCurves.ResampleLinear(_velocities.AsSpan(0, oldCount), velocities.AsSpan(0, count));
        AnalyticsCurves.ResampleLinear(_targets.AsSpan(0, oldCount), targets.AsSpan(0, count));
        _values = values;
        _velocities = velocities;
        _targets = targets;
        _count = count;
    }

    /// <summary>Stops at the current values.</summary>
    public void Stop()
    {
        _values.AsSpan(0, _count).CopyTo(_targets);
        Array.Clear(_velocities, 0, _count);
        IsMoving = false;
        _loop.Stop();
    }

    /// <summary>Advances by <paramref name="dt"/> seconds (the loop calls it; tests may too). Returns true while moving.</summary>
    public bool Advance(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        if (!ArcMotion.IsFull(_owner))
        {
            Jump(_targets.AsSpan(0, _count).ToArray());
            return false;
        }
        var moving = false;
        for (var i = 0; i < _count; i++)
        {
            var x0 = _values[i] - _targets[i];
            var v0 = _velocities[i];
            if (Math.Abs(x0) <= RestDelta && Math.Abs(v0) <= RestSpeed)
            {
                _values[i] = _targets[i];
                _velocities[i] = 0;
                continue;
            }
            var (x, v) = SpringSolver.Evaluate(Spec, x0, v0, dt);
            _values[i] = _targets[i] + x;
            _velocities[i] = v;
            moving = true;
        }
        Changed?.Invoke();
        if (!moving)
        {
            IsMoving = false;
            Settled?.Invoke();
        }
        return moving;
    }

    /// <summary>Stops the loop and unhooks it from the owner.</summary>
    public void Dispose() => _loop.Dispose();

    private bool ShouldAnimate() => _owner.IsAttachedToVisualTree() && ArcMotion.IsFull(_owner);

    private void EnsureCapacity(int count)
    {
        if (_values.Length >= count)
        {
            return;
        }
        Array.Resize(ref _values, count);
        Array.Resize(ref _velocities, count);
        Array.Resize(ref _targets, count);
    }
}
