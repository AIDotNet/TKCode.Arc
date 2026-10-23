using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The error card's slot in an <see cref="ArenaAnswerView"/>: while <see cref="IsOpen"/> its height follows the card,
/// otherwise it is 0. Changes made by the owner move on the Smooth spring at full motion (Arc measured ω 13, 99% in about
/// 490ms); Reduced and Off, a new width (the card wraps again), the first measure and a <see cref="Snap"/> land at once.
/// The card keeps its own height and the slot clips it.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the target is the card's measured height, which depends on its text and the
/// pane's width, so no ControlTheme transition can name it. Core <c>SpringSizeDecorator</c> springs the same way but cannot
/// land at once in the middle of a spring, which a new round needs (spec 5.1: the answer clears without motion).
/// </para>
/// </summary>
internal sealed class ArenaAnswerErrorHost : Decorator
{
    /// <summary>Whether the slot shows the card (its height) or nothing (0).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArenaAnswerErrorHost, bool>(nameof(IsOpen));

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    private MotionValue<double>? _height;
    private double _width = double.NaN;
    private double _laidOut = double.NaN;
    private bool _snap;
    private bool _measuring;

    static ArenaAnswerErrorHost()
    {
        AffectsMeasure<ArenaAnswerErrorHost>(IsOpenProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ArenaAnswerErrorHost>(true);
    }

    /// <summary>True while the height springs.</summary>
    internal bool IsMoving => _height?.IsAnimating == true;

    /// <summary>The height on screen.</summary>
    internal double CurrentHeight => _height?.Value ?? 0;

    /// <summary>The height the slot is moving to.</summary>
    internal double TargetHeight => _height?.Target ?? 0;

    /// <summary>Makes the next measure land at its height without motion (also in the middle of a spring).</summary>
    public void Snap()
    {
        _snap = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        child?.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = IsOpen && child is not null ? child.DesiredSize.Height : 0;
        var passive = !double.IsNaN(_width) && Math.Abs(_width - availableSize.Width) > 0.5;
        _width = availableSize.Width;
        _measuring = true;
        try
        {
            if (_height is null)
            {
                _height = new MotionValue<double>(this, target, MotionVectors.Double);
                _height.Changed += OnHeightChanged;
            }
            else if (_snap || Math.Abs(_height.Target - target) > 0.25)
            {
                if (_snap || passive || !IsLoaded || !ArcMotion.IsFull(this))
                {
                    _height.Jump(target);
                }
                else
                {
                    _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
                }
            }
        }
        finally
        {
            _measuring = false;
        }
        _snap = false;
        var height = Math.Max(0, _height.Value);
        _laidOut = Math.Round(height);
        return new Size(child?.DesiredSize.Width ?? 0, Math.Min(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The card keeps its own height; the slot clips what does not fit yet.
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }

    private void OnHeightChanged(double value)
    {
        // Layout snaps to whole pixels: frames in the spring's sub-pixel tail would lay the answer out again for nothing.
        if (_measuring || (Math.Round(value) == _laidOut && _height is { } height && Math.Abs(value - height.Target) > 0.01))
        {
            return;
        }
        InvalidateMeasure();
    }
}
