using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Hosts the gliding selection pill and the tab triggers in one coordinate space (the pill scrolls with the
/// list). After every arrange it tells <see cref="Tabs"/> so the pill can follow passive layout changes.
/// </summary>
internal sealed class TabsListPanel : Panel
{
    public Action? Arranged { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        Arranged?.Invoke();
        return finalSize;
    }
}
