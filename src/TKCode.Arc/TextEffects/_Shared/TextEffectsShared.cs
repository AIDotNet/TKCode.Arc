using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Heading level of a text effect; H1–H3 set <c>AutomationProperties.HeadingLevel</c> 1–3.</summary>
public enum TextLevel
{
    H1,
    H2,
    H3,

    /// <summary>A paragraph: body type and a softer blur.</summary>
    Paragraph
}

/// <summary>
/// P21 continuous loop for the TextEffects category (the shimmer sweep). Runs on the shared
/// <see cref="MotionScheduler"/> frame batch and is paused by it when the owner is detached or hidden.
/// </summary>
internal sealed class TextEffectsLoop : IMotionDriver
{
    private readonly WeakReference<Visual> _owner;
    private readonly Func<double, bool> _step;
    private bool _running;

    public TextEffectsLoop(Visual owner, Func<double, bool> step)
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

    void IMotionDriver.Snap() => _running = false;
}

/// <summary>Cached blur effects (quantized to 0.25px) so per-frame blurs never allocate.</summary>
internal static class TextEffectsBlur
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

/// <summary>Timing helpers shared by the staggered text reveals (P20).</summary>
internal static class TextEffectsTiming
{
    /// <summary>Eased progress of a track that starts after <paramref name="delay"/> and lasts <paramref name="duration"/>.</summary>
    public static double Track(double elapsed, double delay, double duration, Avalonia.Animation.Easings.Easing easing)
    {
        var p = (elapsed - delay) / Math.Max(duration, 1e-6);
        if (p <= 0)
        {
            return 0;
        }
        return p >= 1 ? 1 : easing.Ease(p);
    }
}
