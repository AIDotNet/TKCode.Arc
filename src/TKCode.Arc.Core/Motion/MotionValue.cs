using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Motion;

/// <summary>Splits a value type into components so every component runs its own spring.</summary>
public interface IMotionVector<T> where T : struct
{
    int Dimensions { get; }

    void Write(T value, Span<double> components);

    T Read(ReadOnlySpan<double> components);
}

/// <summary>Built-in vector converters.</summary>
public static class MotionVectors
{
    public static readonly IMotionVector<double> Double = new DoubleVector();
    public static readonly IMotionVector<Point> Point = new PointVector();
    public static readonly IMotionVector<Vector> Vector = new VectorVector();
    public static readonly IMotionVector<Size> Size = new SizeVector();
    public static readonly IMotionVector<Rect> Rect = new RectVector();
    public static readonly IMotionVector<Thickness> Thickness = new ThicknessVector();
    public static readonly IMotionVector<CornerRadius> CornerRadius = new CornerRadiusVector();
    public static readonly IMotionVector<Color> Color = new ColorVector();

    private sealed class DoubleVector : IMotionVector<double>
    {
        public int Dimensions => 1;
        public void Write(double value, Span<double> c) => c[0] = value;
        public double Read(ReadOnlySpan<double> c) => c[0];
    }

    private sealed class PointVector : IMotionVector<Point>
    {
        public int Dimensions => 2;
        public void Write(Point value, Span<double> c) { c[0] = value.X; c[1] = value.Y; }
        public Point Read(ReadOnlySpan<double> c) => new(c[0], c[1]);
    }

    private sealed class VectorVector : IMotionVector<Vector>
    {
        public int Dimensions => 2;
        public void Write(Vector value, Span<double> c) { c[0] = value.X; c[1] = value.Y; }
        public Vector Read(ReadOnlySpan<double> c) => new(c[0], c[1]);
    }

    private sealed class SizeVector : IMotionVector<Size>
    {
        public int Dimensions => 2;
        public void Write(Size value, Span<double> c) { c[0] = value.Width; c[1] = value.Height; }
        public Size Read(ReadOnlySpan<double> c) => new(Math.Max(0, c[0]), Math.Max(0, c[1]));
    }

    private sealed class RectVector : IMotionVector<Rect>
    {
        public int Dimensions => 4;
        public void Write(Rect value, Span<double> c) { c[0] = value.X; c[1] = value.Y; c[2] = value.Width; c[3] = value.Height; }
        public Rect Read(ReadOnlySpan<double> c) => new(c[0], c[1], Math.Max(0, c[2]), Math.Max(0, c[3]));
    }

    private sealed class ThicknessVector : IMotionVector<Thickness>
    {
        public int Dimensions => 4;
        public void Write(Thickness value, Span<double> c) { c[0] = value.Left; c[1] = value.Top; c[2] = value.Right; c[3] = value.Bottom; }
        public Thickness Read(ReadOnlySpan<double> c) => new(c[0], c[1], c[2], c[3]);
    }

    private sealed class CornerRadiusVector : IMotionVector<CornerRadius>
    {
        public int Dimensions => 4;
        public void Write(CornerRadius value, Span<double> c) { c[0] = value.TopLeft; c[1] = value.TopRight; c[2] = value.BottomRight; c[3] = value.BottomLeft; }
        public CornerRadius Read(ReadOnlySpan<double> c) => new(Math.Max(0, c[0]), Math.Max(0, c[1]), Math.Max(0, c[2]), Math.Max(0, c[3]));
    }

    private sealed class ColorVector : IMotionVector<Color>
    {
        // Colors spring in straight sRGB with alpha; Arc uses springs on colors only for small swaps.
        public int Dimensions => 4;
        public void Write(Color value, Span<double> c) { c[0] = value.A; c[1] = value.R; c[2] = value.G; c[3] = value.B; }
        public Color Read(ReadOnlySpan<double> c) => Avalonia.Media.Color.FromArgb(ToByte(c[0]), ToByte(c[1]), ToByte(c[2]), ToByte(c[3]));
        private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
    }
}

/// <summary>Driver contract used by <see cref="MotionScheduler"/>.</summary>
internal interface IMotionDriver
{
    /// <summary>Advances by <paramref name="dt"/> seconds. Returns false when the driver is at rest.</summary>
    bool Advance(double dt);

    /// <summary>Snaps to the target immediately (owner detached or invisible).</summary>
    void Snap();

    Visual? Owner { get; }
}

/// <summary>
/// An interruptible animated value. <see cref="AnimateTo"/> called while a spring is running keeps the current
/// position and velocity, so retargeting is continuous (framer-motion semantics).
/// </summary>
public sealed class MotionValue<T> : IMotionDriver, IDisposable where T : struct
{
    private readonly IMotionVector<T> _vector;
    private readonly WeakReference<Visual> _owner;
    private readonly double[] _position;
    private readonly double[] _velocity;
    private readonly double[] _target;
    private readonly double[] _segmentStart;
    private readonly double[] _segmentVelocity;
    private readonly double[] _scratch;
    private MotionSpec _spec = MotionSpec.Instant;
    private double _elapsed;
    private double _delayRemaining;
    private bool _animating;
    private TaskCompletionSource<bool>? _completion;
    private static readonly Task<bool> s_completed = Task.FromResult(true);

    public MotionValue(Visual owner, T initial, IMotionVector<T> vector)
    {
        _owner = new WeakReference<Visual>(owner);
        _vector = vector;
        var n = vector.Dimensions;
        _position = new double[n];
        _velocity = new double[n];
        _target = new double[n];
        _segmentStart = new double[n];
        _segmentVelocity = new double[n];
        _scratch = new double[n];
        vector.Write(initial, _position);
        vector.Write(initial, _target);
    }

    /// <summary>Raised after every step and on jumps; write the value to a property or invalidate rendering here.</summary>
    public event Action<T>? Changed;

    public T Value => _vector.Read(_position);

    public T Velocity => _vector.Read(_velocity);

    public T Target => _vector.Read(_target);

    public bool IsAnimating => _animating;

    Visual? IMotionDriver.Owner => _owner.TryGetTarget(out var o) ? o : null;

    /// <summary>
    /// Animates to <paramref name="target"/>. Honors the effective motion level of the owner: Off jumps,
    /// Reduced jumps for springs (callers wanting a fade should animate opacity separately).
    /// </summary>
    public Task<bool> AnimateTo(T target, MotionSpec spec, T? initialVelocity = null)
    {
        var owner = _owner.TryGetTarget(out var o) ? o : null;
        if (owner is null || spec is MotionSpec.InstantSpec)
        {
            Jump(target);
            return s_completed;
        }
        var level = ArcMotion.GetEffectiveLevel(owner);
        if (level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && spec is MotionSpec.SpringMotion))
        {
            Jump(target);
            return s_completed;
        }
        // Pointer handlers retarget on every move: the same target with the same spec keeps the run going as it is.
        if (_animating && initialVelocity is null && _completion is { } running && _spec.Equals(spec))
        {
            _vector.Write(target, _scratch);
            if (_scratch.AsSpan().SequenceEqual(_target))
            {
                return running.Task;
            }
        }
        _vector.Write(target, _target);
        if (initialVelocity is { } iv)
        {
            _vector.Write(iv, _velocity);
        }
        _position.CopyTo(_segmentStart, 0);
        _velocity.CopyTo(_segmentVelocity, 0);
        _spec = spec;
        _elapsed = 0;
        _delayRemaining = spec.Delay.TotalSeconds;
        _completion?.TrySetResult(false);
        _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_animating)
        {
            _animating = true;
            if (!MotionScheduler.Register(this, owner))
            {
                Jump(target);
                return s_completed;
            }
        }
        return _completion.Task;
    }

    /// <summary>Sets the value immediately and stops; velocity becomes zero.</summary>
    public void Jump(T value)
    {
        _vector.Write(value, _position);
        _vector.Write(value, _target);
        Array.Clear(_velocity);
        StopInternal(true);
        Changed?.Invoke(value);
    }

    /// <summary>Stops at the current value.</summary>
    public void Stop()
    {
        _position.CopyTo(_target, 0);
        Array.Clear(_velocity);
        StopInternal(false);
    }

    void IMotionDriver.Snap()
    {
        // A value that was stopped stays registered until the next frame; it has nothing left to finish, and jumping
        // would fire Changed again with the stopped value (a layer that was reset on detach would turn half transparent).
        if (_animating)
        {
            Jump(Target);
        }
    }

    bool IMotionDriver.Advance(double dt)
    {
        if (!_animating)
        {
            return false;
        }
        if (_delayRemaining > 0)
        {
            _delayRemaining -= dt;
            if (_delayRemaining > 0)
            {
                return true;
            }
            dt = -_delayRemaining;
        }
        _elapsed += dt;
        var atRest = true;
        switch (_spec)
        {
            case MotionSpec.SpringMotion spring:
            {
                for (var i = 0; i < _position.Length; i++)
                {
                    var x0 = _segmentStart[i] - _target[i];
                    var (x, v) = SpringSolver.Evaluate(spring.Spec, x0, _segmentVelocity[i], _elapsed);
                    _position[i] = _target[i] + x;
                    _velocity[i] = v;
                    var granular = Math.Abs(x0) < 5;
                    var restDelta = granular ? 0.005 : 0.5;
                    var restSpeed = granular ? 0.01 : 2;
                    if (Math.Abs(x) > restDelta || Math.Abs(v) > restSpeed)
                    {
                        atRest = false;
                    }
                }
                break;
            }
            case MotionSpec.TweenMotion tween:
            {
                var duration = Math.Max(tween.Duration.TotalSeconds, 1e-6);
                var p = Math.Min(1, _elapsed / duration);
                var eased = tween.Easing.Ease(p);
                for (var i = 0; i < _position.Length; i++)
                {
                    var previous = _position[i];
                    _position[i] = _segmentStart[i] + (_target[i] - _segmentStart[i]) * eased;
                    _velocity[i] = dt > 0 ? (_position[i] - previous) / dt : 0;
                }
                atRest = p >= 1;
                break;
            }
        }
        if (atRest)
        {
            _target.CopyTo(_position, 0);
            Array.Clear(_velocity);
            Changed?.Invoke(Value);
            StopInternal(true);
            return false;
        }
        Changed?.Invoke(Value);
        return true;
    }

    private void StopInternal(bool reachedTarget)
    {
        _animating = false;
        var completion = _completion;
        _completion = null;
        completion?.TrySetResult(reachedTarget);
    }

    public void Dispose() => StopInternal(false);
}

/// <summary>How a <see cref="MotionValue{T}"/> moves to its target.</summary>
public abstract record MotionSpec(TimeSpan Delay)
{
    public static readonly MotionSpec Instant = new InstantSpec();

    // Presets without a delay are shared: callers build them on every pointer move.
    private static readonly MotionSpec[] s_presets = Enum.GetValues<ArcSpring>().Select(s => (MotionSpec)new SpringMotion(ArcMotionPresets.Get(s), default)).ToArray();

    public static MotionSpec Spring(ArcSpring spring, TimeSpan delay = default)
        => delay == default && (int)spring >= 0 && (int)spring < s_presets.Length ? s_presets[(int)spring] : new SpringMotion(ArcMotionPresets.Get(spring), delay);

    public static MotionSpec Spring(SpringSpec spec, TimeSpan delay = default) => new SpringMotion(spec, delay);

    public static MotionSpec Tween(TimeSpan duration, Avalonia.Animation.Easings.Easing easing, TimeSpan delay = default)
        => new TweenMotion(duration, easing, delay);

    public sealed record InstantSpec() : MotionSpec(TimeSpan.Zero);

    public sealed record SpringMotion(SpringSpec Spec, TimeSpan SpringDelay) : MotionSpec(SpringDelay);

    public sealed record TweenMotion(TimeSpan Duration, Avalonia.Animation.Easings.Easing Easing, TimeSpan TweenDelay) : MotionSpec(TweenDelay);
}

/// <summary>Batches all active motion values of a TopLevel on one animation frame callback.</summary>
public static class MotionScheduler
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TopLevel, Batch> s_batches = new();

    /// <summary>Optional clock override (tests).</summary>
    internal static IFrameClock? ClockOverride { get; set; }

    internal static int ActiveCount
    {
        get
        {
            var count = 0;
            foreach (var pair in s_batches)
            {
                count += pair.Value.Count;
            }
            return count;
        }
    }

    internal static bool Register(IMotionDriver driver, Visual owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            return false;
        }
        var batch = s_batches.GetValue(topLevel, static t => new Batch(t));
        batch.Add(driver);
        return true;
    }

    private sealed class Batch
    {
        private readonly WeakReference<TopLevel> _topLevel;
        private readonly HashSet<IMotionDriver> _drivers = new();
        private readonly List<IMotionDriver> _snapshot = new();
        private bool _requested;
        private TimeSpan? _last;

        public Batch(TopLevel topLevel) => _topLevel = new WeakReference<TopLevel>(topLevel);

        public int Count => _drivers.Count;

        public void Add(IMotionDriver driver)
        {
            _drivers.Add(driver);
            Request();
        }

        private void Request()
        {
            if (_requested || !_topLevel.TryGetTarget(out var topLevel))
            {
                return;
            }
            _requested = true;
            var clock = ClockOverride;
            if (clock is not null)
            {
                clock.RequestFrame(OnFrame);
            }
            else
            {
                topLevel.RequestAnimationFrame(OnFrame);
            }
        }

        private void OnFrame(TimeSpan timestamp)
        {
            _requested = false;
            var dt = _last is { } last ? (timestamp - last).TotalSeconds : 1.0 / 60.0;
            _last = timestamp;
            if (dt <= 0)
            {
                dt = 1.0 / 60.0;
            }
            _snapshot.Clear();
            _snapshot.AddRange(_drivers);
            foreach (var driver in _snapshot)
            {
                var owner = driver.Owner;
                if (owner is null || !owner.IsAttachedToVisualTree() || !owner.IsEffectivelyVisible)
                {
                    driver.Snap();
                    _drivers.Remove(driver);
                    continue;
                }
                // Every driver solves its curve in closed form from the elapsed time, so a long gap (window restored)
                // is one step: sub-stepping would only raise Changed again for the same frame.
                var keep = driver.Advance(Math.Min(dt, 0.25));
                if (!keep)
                {
                    _drivers.Remove(driver);
                }
            }
            if (_drivers.Count > 0)
            {
                Request();
            }
            else
            {
                _last = null;
            }
        }
    }
}

internal static class VisualAttachExtensions
{
    public static bool IsAttachedToVisualTree(this Visual visual) => visual.GetVisualRootSafe() is not null;

    private static object? GetVisualRootSafe(this Visual visual) => TopLevel.GetTopLevel(visual);
}

/// <summary>Frame source abstraction (tests use <see cref="ManualFrameClock"/>).</summary>
public interface IFrameClock
{
    void RequestFrame(Action<TimeSpan> callback);
}

/// <summary>Deterministic frame clock for tests: frames run only when <see cref="Advance"/> is called.</summary>
public sealed class ManualFrameClock : IFrameClock
{
    private readonly List<Action<TimeSpan>> _pending = new();
    private TimeSpan _now;

    public int RequestCount { get; private set; }

    public void RequestFrame(Action<TimeSpan> callback)
    {
        RequestCount++;
        _pending.Add(callback);
    }

    /// <summary>Advances time and runs one frame per <paramref name="frameStep"/> until <paramref name="duration"/> elapses.</summary>
    public void Advance(TimeSpan duration, TimeSpan? frameStep = null)
    {
        var step = frameStep ?? TimeSpan.FromMilliseconds(1000.0 / 60.0);
        var end = _now + duration;
        while (_now < end)
        {
            _now += step;
            var callbacks = _pending.ToArray();
            _pending.Clear();
            foreach (var callback in callbacks)
            {
                callback(_now);
            }
        }
    }

    /// <summary>Installs this clock for all motion values (tests only). Dispose to restore.</summary>
    public IDisposable Install()
    {
        MotionScheduler.ClockOverride = this;
        return new Restore();
    }

    private sealed class Restore : IDisposable
    {
        public void Dispose() => MotionScheduler.ClockOverride = null;
    }
}
