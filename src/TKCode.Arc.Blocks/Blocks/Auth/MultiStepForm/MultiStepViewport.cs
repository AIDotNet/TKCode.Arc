using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The step area: the steps and the success block are layers in one cell. Only <see cref="Current"/> sizes the area (its
/// natural height, or <c>MinHeight</c> when it is shorter); a layer that is leaving keeps its own height and may overhang
/// while it fades. Every visible layer is arranged at least as tall as the area, so the success block centers in it.
/// The surrounding <see cref="Primitives.SpringSizeDecorator"/> springs the height both ways (Arc's area only ever grew).
/// </summary>
internal sealed class MultiStepViewport : Panel
{
    private Control? _current;

    /// <summary>The layer that sizes the area.</summary>
    public Control? Current
    {
        get => _current;
        set
        {
            if (!ReferenceEquals(_current, value))
            {
                _current = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measure = availableSize.WithHeight(double.PositiveInfinity);
        var width = 0.0;
        foreach (var child in Children)
        {
            child.Measure(measure);
            width = Math.Max(width, child.DesiredSize.Width);
        }
        var height = _current is { IsVisible: true } current ? current.DesiredSize.Height : 0;
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, child.DesiredSize.Height)));
        }
        return finalSize;
    }
}
