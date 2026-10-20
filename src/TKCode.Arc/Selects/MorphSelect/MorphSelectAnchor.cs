using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Holds the closed <see cref="MorphSelect"/> trigger's place. Its width is the chosen label's width (plus the
/// trigger's padding) and follows label changes on a spring that widens with a little bounce and narrows without
/// overshoot (dipping under the closed width would ellipsize the label). The child is left aligned and arranged at
/// the current width, so the label ellipsizes while the edge catches up instead of shifting.
/// </summary>
internal sealed class MorphSelectAnchor : Decorator
{
    public static readonly StyledProperty<double> MaxContentWidthProperty =
        AvaloniaProperty.Register<MorphSelectAnchor, double>(nameof(MaxContentWidth), 320);

    public static readonly StyledProperty<bool> AlignEndProperty =
        AvaloniaProperty.Register<MorphSelectAnchor, bool>(nameof(AlignEnd));

    /// <summary>Upper bound of the closed width (Arc: min(20rem, viewport − 2rem)).</summary>
    public double MaxContentWidth { get => GetValue(MaxContentWidthProperty); set => SetValue(MaxContentWidthProperty, value); }

    public bool AlignEnd { get => GetValue(AlignEndProperty); set => SetValue(AlignEndProperty, value); }

    private MotionValue<double>? _width;
    private double _target = -1;

    static MorphSelectAnchor()
    {
        AffectsMeasure<MorphSelectAnchor>(MaxContentWidthProperty);
        AffectsArrange<MorphSelectAnchor>(AlignEndProperty);
    }

    /// <summary>The width the closed trigger wants (rounded up so the label never ellipsizes at rest).</summary>
    public double TargetWidth => Math.Max(0, _target);

    public double CurrentWidth => _width?.Value ?? TargetWidth;

    /// <summary>Raised when the target width changes (the popup surface follows it while closing).</summary>
    public event EventHandler? TargetChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var max = Math.Min(MaxContentWidth, availableSize.Width);
        var target = Math.Ceiling(Math.Min(child.DesiredSize.Width, max) - 0.01);
        if (_width is null)
        {
            _width = new MotionValue<double>(this, target, MotionVectors.Double);
            _width.Changed += _ => InvalidateMeasure();
            _target = target;
        }
        else if (Math.Abs(target - _target) > 0.5)
        {
            var narrowing = target < _width.Value;
            _target = target;
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(narrowing ? MorphSelectSurface.Narrow : MorphSelectSurface.Widen));
            }
            else
            {
                _width.Jump(target);
            }
            TargetChanged?.Invoke(this, EventArgs.Empty);
        }
        return new Size(Math.Max(0, _width.Value), child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}
