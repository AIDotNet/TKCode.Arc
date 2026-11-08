using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a <see cref="LiveCheckoutCollapse"/> opens and closes.</summary>
internal enum LiveCheckoutCollapseMotion
{
    /// <summary>Height and opacity share the smooth spring (summary rows, the discount row, errors, the decline alert).</summary>
    Spring,

    /// <summary>Height over 480 ms and opacity over 240 ms on the standard curve (the narrow summary's unfold).</summary>
    Unfold
}

/// <summary>
/// A row that opens and closes its height while its content fades (Arc's collapse wrapper). The content keeps its own
/// height and is clipped while the row moves; a closed row hides its content, so nothing in it can take focus.
/// </summary>
/// <remarks>
/// Code-driven because the height is the content's measured height, which the summary must lay out around on every frame,
/// and the content must be hidden only after the close, which a ControlTheme transition cannot report.
/// </remarks>
internal sealed class LiveCheckoutCollapse : Decorator
{
    private readonly MotionValue<double> _height;
    private readonly MotionValue<double> _fade;
    private int _generation;

    public LiveCheckoutCollapse()
    {
        _height = new MotionValue<double>(this, 1, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += v =>
        {
            if (Child is { } child)
            {
                child.Opacity = Math.Clamp(v, 0, 1);
            }
        };
    }

    /// <summary>How the row moves.</summary>
    public LiveCheckoutCollapseMotion Motion { get; set; }

    /// <summary>True while the row is open or opening.</summary>
    public bool IsOpen { get; private set; } = true;

    /// <summary>The share of the content's height that is laid out (tests).</summary>
    public double HeightFactor => _height.Value;

    /// <summary>The content's opacity (tests).</summary>
    public double FadeValue => _fade.Value;

    /// <summary>True while the height moves.</summary>
    public bool IsMoving => _height.IsAnimating;

    /// <summary>Raised when a close has finished and the content is hidden.</summary>
    public event EventHandler? Closed;

    /// <summary>Opens or closes; animated only when <paramref name="animate"/> is true and motion is full.</summary>
    public async void SetOpen(bool open, bool animate)
    {
        if (open == IsOpen && !_height.IsAnimating)
        {
            if (Child is { } shown)
            {
                shown.IsVisible = open;
            }
            return;
        }
        IsOpen = open;
        var generation = ++_generation;
        if (Child is { } child)
        {
            child.IsVisible = true;
        }
        if (!animate || !LiveCheckoutMotion.CanAnimate(this))
        {
            _height.Jump(open ? 1 : 0);
            _fade.Jump(open ? 1 : 0);
            Finish(open);
            return;
        }
        var target = open ? 1 : 0;
        Task<bool> height;
        Task<bool> fade;
        if (Motion == LiveCheckoutCollapseMotion.Unfold)
        {
            height = _height.AnimateTo(target, MotionSpec.Tween(LiveCheckoutMotion.UnfoldHeight, LiveCheckoutMotion.Standard));
            fade = _fade.AnimateTo(target, MotionSpec.Tween(LiveCheckoutMotion.UnfoldFade, LiveCheckoutMotion.Standard));
        }
        else
        {
            height = _height.AnimateTo(target, MotionSpec.Spring(LiveCheckoutMotion.Smooth));
            fade = _fade.AnimateTo(target, MotionSpec.Spring(LiveCheckoutMotion.Smooth));
        }
        await Task.WhenAll(height, fade);
        if (generation == _generation)
        {
            Finish(open);
        }
    }

    private void Finish(bool open)
    {
        if (Child is { } child)
        {
            child.IsVisible = open;
        }
        InvalidateMeasure();
        if (!open)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var factor = Math.Clamp(_height.Value, 0, 1);
        if (!child.IsVisible)
        {
            return new Size(0, 0);
        }
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * factor);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        // Clip only while the row is not fully open, so a focus ring at rest is never cut.
        ClipToBounds = _height.Value < 0.999;
        return finalSize;
    }
}
