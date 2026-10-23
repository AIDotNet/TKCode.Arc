using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The standings' body: the rows and the empty text stacked in one place, where only <see cref="Current"/> gives the size.
/// The other layer keeps its own height at the top while it fades out, so the height spring around the body starts at
/// the swap (rows 180 → empty 39) instead of after the fade.
/// </summary>
internal sealed class ArenaStandingsBody : Panel
{
    private Control? _current;

    /// <summary>The layer that sizes the body (the rows or the empty text).</summary>
    public Control? Current
    {
        get => _current;
        set
        {
            if (ReferenceEquals(_current, value))
            {
                return;
            }
            _current = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var childSize = new Size(availableSize.Width, double.PositiveInfinity);
        var width = 0.0;
        foreach (var child in Children)
        {
            child.Measure(childSize);
            width = Math.Max(width, child.DesiredSize.Width);
        }
        var height = _current is { IsVisible: true } current ? current.DesiredSize.Height : 0;
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}
