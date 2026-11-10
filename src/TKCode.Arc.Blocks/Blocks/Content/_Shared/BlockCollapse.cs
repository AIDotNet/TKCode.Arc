using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A decorator whose height opens and closes on a spring, with separate specs for each direction (Arc's <c>height: 0</c> to
/// <c>"auto"</c> rows and panels). It clips only while it moves; closed, the child is hidden and leaves the tab order.
/// </summary>
/// <remarks>
/// Code-driven because the height follows the child's measured size. <c>SpringSizeDecorator</c> takes spring presets only; the
/// changelog closes on its own <c>{ visualDuration: 0.28, bounce: 0 }</c>.
/// </remarks>
internal sealed class BlockCollapse : Decorator
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<BlockCollapse, bool>(nameof(IsExpanded), true);

    private MotionValue<double>? _motion;
    private double _amount = 1;

    /// <summary>Whether the child shows.</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>The spring that opens it (Arc's smooth).</summary>
    public MotionSpec OpenSpec { get; set; } = MotionSpec.Spring(ArcSpring.Smooth);

    /// <summary>The spring that closes it (Arc's smooth).</summary>
    public MotionSpec CloseSpec { get; set; } = MotionSpec.Spring(ArcSpring.Smooth);

    /// <summary>The child's opacity follows the height (Arc's rows animate both on one spring).</summary>
    public bool FadesChild { get; set; }

    /// <summary>How open it is, 0 to 1.</summary>
    public double Amount => _amount;

    /// <summary>Raised when the motion to the current state settled.</summary>
    public event Action<bool>? Settled;

    /// <summary>Jumps to <paramref name="expanded"/> without motion.</summary>
    public void SetInstantly(bool expanded)
    {
        SetCurrentValue(IsExpandedProperty, expanded);
        _motion?.Jump(expanded ? 1 : 0);
        Apply(expanded ? 1 : 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            Run(change.GetNewValue<bool>());
        }
        else if (change.Property == ChildProperty)
        {
            Apply(_amount);
        }
    }

    private async void Run(bool expanded)
    {
        if (_motion is null)
        {
            _motion = new MotionValue<double>(this, _amount, MotionVectors.Double);
            _motion.Changed += Apply;
        }
        if (expanded && Child is { } child)
        {
            child.IsVisible = true;
        }
        var arrived = await _motion.AnimateTo(expanded ? 1 : 0, expanded ? OpenSpec : CloseSpec);
        if (arrived && expanded == IsExpanded)
        {
            Apply(expanded ? 1 : 0);
            Settled?.Invoke(expanded);
        }
    }

    private void Apply(double amount)
    {
        _amount = Math.Clamp(amount, 0, 1);
        ClipToBounds = _amount < 1;
        if (Child is { } child)
        {
            child.IsVisible = _amount > 0 || IsExpanded;
            if (FadesChild)
            {
                child.Opacity = _amount;
            }
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * _amount);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}
