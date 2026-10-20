using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// First-view reveal (P18) for team B's charts: fires once when <c>threshold</c> of the owner is inside its viewport.
/// With motion Off the owner is revealed at once. <see cref="Fade"/> is the whole-chart opacity: under Reduced motion
/// the reveal is a short fade instead of the chart's own entrance.
/// </summary>
internal sealed class ChartsBReveal
{
    private readonly Control _owner;
    private readonly double _threshold;
    private readonly Action<bool> _onReveal;
    private ViewportVisibility? _visibility;
    private MotionValue<double>? _fade;

    /// <param name="owner">The chart.</param>
    /// <param name="threshold">Visible fraction that triggers the reveal (Arc: 0.25 to 0.5 per chart).</param>
    /// <param name="onReveal">Called once; the argument says whether the full entrance should play.</param>
    public ChartsBReveal(Control owner, double threshold, Action<bool> onReveal)
    {
        _owner = owner;
        _threshold = threshold;
        _onReveal = onReveal;
    }

    public bool IsRevealed { get; private set; }

    /// <summary>1 except while a Reduced-motion reveal fades the chart in (0 before it is seen).</summary>
    public double Fade => _fade?.Value ?? 1;

    public void Attach()
    {
        if (IsRevealed || _visibility is not null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (level == ArcMotionLevel.Off)
        {
            Reveal(false);
            return;
        }
        if (level == ArcMotionLevel.Reduced && _fade is null)
        {
            _fade = new MotionValue<double>(_owner, 0, MotionVectors.Double);
            _fade.Changed += _ => _owner.InvalidateVisual();
        }
        _visibility = new ViewportVisibility(_owner, _threshold, once: true);
        _visibility.VisibilityChanged += OnVisibilityChanged;
    }

    public void Detach()
    {
        if (_visibility is null)
        {
            return;
        }
        _visibility.VisibilityChanged -= OnVisibilityChanged;
        _visibility.Dispose();
        _visibility = null;
    }

    /// <summary>Reveals now (for example when data arrives in a chart that is already on screen).</summary>
    public void RevealNow() => Reveal(ArcMotion.IsFull(_owner));

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (visible)
        {
            Reveal(ArcMotion.IsFull(_owner));
        }
    }

    private void Reveal(bool animate)
    {
        if (IsRevealed)
        {
            return;
        }
        IsRevealed = true;
        Detach();
        if (_fade is not null)
        {
            if (ArcMotion.GetEffectiveLevel(_owner) == ArcMotionLevel.Off)
            {
                _fade.Jump(1);
            }
            else
            {
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
        }
        _onReveal(animate);
        _owner.InvalidateVisual();
    }
}
