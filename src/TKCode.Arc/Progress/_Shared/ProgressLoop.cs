using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// P21 continuous loop for the Progress category (skeleton pulse, live dot). Runs on the shared
/// <see cref="MotionScheduler"/> frame batch, so a loop costs one callback per frame and nothing while stopped.
/// The scheduler pauses it when the owner is detached or hidden; the owner restarts it with <see cref="Start"/>.
/// </summary>
internal sealed class ProgressLoop : IMotionDriver
{
    private readonly WeakReference<Visual> _owner;
    private readonly Func<double, bool> _step;
    private bool _running;

    /// <param name="owner">Visual whose attachment and visibility gate the loop.</param>
    /// <param name="step">Advances by the given seconds; returns false when the loop has finished.</param>
    public ProgressLoop(Visual owner, Func<double, bool> step)
    {
        _owner = new WeakReference<Visual>(owner);
        _step = step;
    }

    public bool IsRunning => _running;

    Visual? IMotionDriver.Owner => _owner.TryGetTarget(out var owner) ? owner : null;

    public void Start()
    {
        if (_running || !_owner.TryGetTarget(out var owner))
        {
            return;
        }
        _running = MotionScheduler.Register(this, owner);
    }

    public void Stop() => _running = false;

    bool IMotionDriver.Advance(double dt)
    {
        if (!_running)
        {
            return false;
        }
        if (!_step(dt))
        {
            _running = false;
        }
        return _running;
    }

    // Detached or hidden: pause where it is (the phase is kept by the owner).
    void IMotionDriver.Snap() => _running = false;
}

/// <summary>Cached blur effects (quantized to 0.25px) so per-frame blurs in Render never allocate.</summary>
internal static class ProgressBlur
{
    private static readonly ImmutableBlurEffect[] s_effects = CreateEffects();

    private static ImmutableBlurEffect[] CreateEffects()
    {
        var effects = new ImmutableBlurEffect[41];
        for (var i = 0; i < effects.Length; i++)
        {
            effects[i] = new ImmutableBlurEffect(i * 0.25);
        }
        return effects;
    }

    /// <summary>A blur of about <paramref name="radius"/> px, or null when it is too small to see.</summary>
    public static IEffect? Get(double radius)
    {
        var index = (int)Math.Round(radius * 4);
        if (index <= 0)
        {
            return null;
        }
        return s_effects[Math.Min(index, s_effects.Length - 1)];
    }
}

/// <summary>Color helpers for render-based progress parts.</summary>
internal static class ProgressColors
{
    public static Color Of(IBrush? brush) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : Colors.Transparent;

    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
