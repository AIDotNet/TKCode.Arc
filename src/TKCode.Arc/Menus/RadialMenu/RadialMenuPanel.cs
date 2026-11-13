using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The items panel of a <see cref="RadialMenu"/>: every visible <see cref="RadialMenuItem"/> covers the whole ring square
/// and is told which sector it occupies; hidden items take no sector.
/// </summary>
internal sealed class RadialMenuPanel : Panel
{
    private RadialMenu? _menu;

    private RadialMenu? Menu => _menu ??= this.FindAncestorOfType<RadialMenu>();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _menu = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var menu = Menu;
        var side = menu?.LayoutSide ?? 0;
        var size = new Size(side, side);
        var visible = 0;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                visible++;
            }
        }
        var index = 0;
        foreach (var child in Children)
        {
            if (child is RadialMenuItem item && menu is not null && item.IsVisible)
            {
                var slot = RadialMenuLayout.Slot(index++, visible, menu.StartAngle, menu.SweepAngle);
                item.SetLayout(slot, menu.EffectiveInnerRadius, menu.EffectiveOuterRadius, Math.Max(0, menu.SectorGap),
                               visible == 1 && RadialMenuLayout.IsFullCircle(menu.SweepAngle), menu.IsItemLabelVisible);
            }
            child.Measure(size);
        }
        menu?.OnItemsLaidOut();
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}
