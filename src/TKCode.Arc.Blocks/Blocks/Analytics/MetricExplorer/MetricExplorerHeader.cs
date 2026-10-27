using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The heading row: the title on the left and the small caption on the right on one line, the caption's baseline a little
/// below the title's; when they do not fit side by side the caption wraps under the title (left aligned, 4px apart).
/// </summary>
internal sealed class MetricExplorerHeader : Panel
{
    private const double RowGap = 4;
    private const double ColumnGap = 16;

    private bool _wrapped;

    private Control? Title => Children.Count > 0 ? Children[0] : null;

    private Control? Caption => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var title = Title;
        var caption = Caption;
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        title?.Measure(new Size(width, double.PositiveInfinity));
        caption?.Measure(new Size(width, double.PositiveInfinity));
        var t = title is { IsVisible: true } ? title.DesiredSize : default;
        var c = caption is { IsVisible: true } ? caption.DesiredSize : default;
        _wrapped = t.Width > 0 && c.Width > 0 && t.Width + ColumnGap + c.Width > width;
        if (_wrapped)
        {
            return new Size(Math.Max(t.Width, c.Width), t.Height + RowGap + c.Height);
        }
        var height = Math.Max(t.Height, c.Height);
        return new Size(t.Width + (c.Width > 0 && t.Width > 0 ? ColumnGap : 0) + c.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var title = Title;
        var caption = Caption;
        var t = title is { IsVisible: true } ? title.DesiredSize : default;
        var c = caption is { IsVisible: true } ? caption.DesiredSize : default;
        title?.Arrange(new Rect(0, 0, Math.Min(t.Width, finalSize.Width), t.Height));
        if (caption is null)
        {
            return finalSize;
        }
        if (_wrapped)
        {
            caption.Arrange(new Rect(0, t.Height + RowGap, Math.Min(c.Width, finalSize.Width), c.Height));
        }
        else
        {
            // Roughly baseline aligned: the smaller text sits about a pixel and a half lower than centred.
            var rowHeight = Math.Max(t.Height, c.Height);
            var top = Math.Max(0, rowHeight - c.Height + 1);
            caption.Arrange(new Rect(Math.Max(0, finalSize.Width - c.Width), top, c.Width, c.Height));
        }
        return finalSize;
    }
}
