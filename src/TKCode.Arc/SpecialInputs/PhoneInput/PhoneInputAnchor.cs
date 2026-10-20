using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Holds the closed country trigger's place in the phone shell. Its width is what the trigger wants (12px + flag + code
/// + 36px for the chevron) and springs to a new width when the calling code changes length (Arc's <c>WIDTH</c>:
/// 0.42s, bounce 0.16). The child is arranged at the current width, left aligned. Reduced and Off jump.
/// </summary>
internal sealed class PhoneInputAnchor : Decorator
{
    /// <summary>Arc's <c>WIDTH = physical(.42, .16)</c>.</summary>
    internal static readonly SpringSpec WidthSpring = SpringSpec.FromVisual(0.42, 0.16);

    private MotionValue<double>? _width;
    private double _target = -1;

    /// <summary>The width the closed trigger wants.</summary>
    public double TargetWidth => Math.Max(0, _target);

    /// <summary>The width shown now (mid spring while the code changes length).</summary>
    public double CurrentWidth => _width?.Value ?? TargetWidth;

    /// <summary>Raised when the target width changes (the open surface follows it while it folds back).</summary>
    public event EventHandler? TargetChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var target = Math.Ceiling(child.DesiredSize.Width - 0.01);
        if (_width is null)
        {
            _width = new MotionValue<double>(this, target, MotionVectors.Double);
            _width.Changed += _ => InvalidateMeasure();
            _target = target;
        }
        else if (Math.Abs(target - _target) > 0.5)
        {
            _target = target;
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(WidthSpring));
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
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
