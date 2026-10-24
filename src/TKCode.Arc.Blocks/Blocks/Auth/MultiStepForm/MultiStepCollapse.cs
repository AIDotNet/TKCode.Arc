using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Holds the form's buttons and folds them away after the last step: they fade out in 120 ms (standard curve) while the
/// height closes on Arc's smooth spring (visual duration .4, no bounce; fitted ω 13.0 on the preview, so the card shrinks
/// by the buttons' 60 px), then the row is hidden. <see cref="Expand"/> brings it back (a restart): the height opens on the
/// same spring while the row fades in over 240 ms. Reduced motion and Off fold and unfold at once. Clips only while it
/// moves or is closed, so a focus ring at rest is never cut.
/// </summary>
/// <remarks>
/// Code-driven because the height is a measured value the card must lay out around while it shrinks, and the row must be
/// hidden only once both the fade and the spring are done, which a ControlTheme transition cannot report.
/// </remarks>
internal sealed class MultiStepCollapse : Decorator
{
    private readonly MotionValue<double> _height;
    private readonly MotionValue<double> _fade;
    private readonly RectangleGeometry _clip = new();
    private int _generation;

    public MultiStepCollapse()
    {
        _height = new MotionValue<double>(this, 1, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += value =>
        {
            if (Child is { } child)
            {
                child.Opacity = Math.Clamp(value, 0, 1);
            }
        };
    }

    /// <summary>False once folding has started (input is off at once).</summary>
    public bool IsExpanded { get; private set; } = true;

    /// <summary>The share of the row's height that is laid out (1 open, 0 closed).</summary>
    internal double HeightFactor => _height.Value;

    /// <summary>Folds the row away (animated when <paramref name="animate"/> is true and motion is full).</summary>
    public async void Collapse(bool animate)
    {
        if (!IsExpanded)
        {
            return;
        }
        IsExpanded = false;
        IsHitTestVisible = false;
        var generation = ++_generation;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _fade.Jump(0);
            _height.Jump(0);
            IsVisible = false;
            return;
        }
        var fade = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        var height = _height.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth));
        await fade;
        await height;
        if (generation == _generation && !IsExpanded)
        {
            IsVisible = false;
        }
    }

    /// <summary>Unfolds the row again (a restart).</summary>
    public void Expand(bool animate)
    {
        if (IsExpanded)
        {
            return;
        }
        IsExpanded = true;
        IsHitTestVisible = true;
        _generation++;
        IsVisible = true;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _fade.Jump(1);
            _height.Jump(1);
            return;
        }
        _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        _ = _height.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
    }

    /// <summary>Sets the state without motion (first show).</summary>
    public void SetExpanded(bool expanded)
    {
        _generation++;
        IsExpanded = expanded;
        IsHitTestVisible = expanded;
        IsVisible = expanded;
        _fade.Jump(expanded ? 1 : 0);
        _height.Jump(expanded ? 1 : 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize);
        var factor = Math.Clamp(_height.Value, 0, 1);
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * factor);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        var moving = _height.Value < 1 - 1e-3;
        if (moving)
        {
            // Wide enough sideways for the buttons' focus rings; cut at the laid-out height.
            _clip.Rect = new Rect(-8, -8, finalSize.Width + 16, finalSize.Height + 8);
            Clip = _clip;
            InvalidateVisual();
        }
        else
        {
            Clip = null;
        }
        return finalSize;
    }
}
