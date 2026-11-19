using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Shows one control as a visual child only, leaving its logical parent alone. The ribbon moves the same content between
/// places (the selected tab between the ribbon and the pop-up of a collapsed ribbon, a group's commands between the group and
/// its drop-down) while the owner stays its logical parent, so bindings, commands and resources keep working.
/// </summary>
internal sealed class RibbonVisualSlot : Control
{
    private Control? _child;

    public Control? Child => _child;

    /// <summary>Shows <paramref name="child"/> here, taking it from the slot that showed it before.</summary>
    public void SetChild(Control? child)
    {
        if (ReferenceEquals(_child, child))
        {
            return;
        }
        if (_child is not null)
        {
            VisualChildren.Remove(_child);
        }
        _child = child;
        if (child is not null)
        {
            if (child.GetVisualParentSlot() is { } previous && !ReferenceEquals(previous, this))
            {
                previous.SetChild(null);
            }
            VisualChildren.Add(child);
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_child is null)
        {
            return default;
        }
        _child.Measure(availableSize);
        return _child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _child?.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

internal static class RibbonVisualSlotExtensions
{
    public static RibbonVisualSlot? GetVisualParentSlot(this Control control) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualParent(control) as RibbonVisualSlot;
}
