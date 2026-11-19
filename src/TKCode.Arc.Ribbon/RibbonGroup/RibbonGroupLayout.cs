using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Root of a <see cref="RibbonGroup"/>'s template: holds the expanded group (first child) and the button it collapses into
/// (second child). Both are always measured, so the group knows the width of either form; only the current one is
/// arranged, the other gets an empty rectangle.
/// </summary>
internal sealed class RibbonGroupLayout : Panel
{
    private RibbonGroup? Group => TemplatedParent as RibbonGroup;

    protected override Size MeasureOverride(Size availableSize)
    {
        var collapsed = Group?.IsCollapsed == true;
        var desired = default(Size);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(availableSize);
            if ((i == 1) == collapsed)
            {
                desired = child.DesiredSize;
            }
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var collapsed = Group?.IsCollapsed == true;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange((i == 1) == collapsed ? new Rect(finalSize) : default);
        }
        return finalSize;
    }
}
