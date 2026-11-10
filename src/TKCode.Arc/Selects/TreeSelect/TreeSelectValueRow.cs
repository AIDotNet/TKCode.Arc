using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The value line of a <see cref="TreeSelect"/> trigger: the chips (first child) followed by the search input (second
/// child), which fills the rest of the line and keeps at least <see cref="SearchMinWidth"/>; the chips give up room
/// (their slots shrink, clipped) before the input does. A hidden child takes no room.
/// </summary>
internal sealed class TreeSelectValueRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TreeSelectValueRow, double>(nameof(Spacing), 5);

    public static readonly StyledProperty<double> SearchMinWidthProperty =
        AvaloniaProperty.Register<TreeSelectValueRow, double>(nameof(SearchMinWidth), 48);

    static TreeSelectValueRow()
    {
        AffectsMeasure<TreeSelectValueRow>(SpacingProperty, SearchMinWidthProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public double SearchMinWidth { get => GetValue(SearchMinWidthProperty); set => SetValue(SearchMinWidthProperty, value); }

    private Control? Chips => Children.Count > 0 && Children[0].IsVisible ? Children[0] : null;

    private Control? Search => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var chips = Chips;
        var search = Search;
        var available = availableSize.Width;
        var height = 0.0;
        var chipsWidth = 0.0;
        if (chips is not null)
        {
            var room = search is null || double.IsInfinity(available) ? available : Math.Max(0, available - SearchMinWidth - Spacing);
            chips.Measure(new Size(room, availableSize.Height));
            chipsWidth = chips.DesiredSize.Width;
            height = chips.DesiredSize.Height;
        }
        var width = chipsWidth;
        if (search is not null)
        {
            var gap = chips is not null && chipsWidth > 0 ? Spacing : 0;
            var room = double.IsInfinity(available) ? SearchMinWidth : Math.Max(SearchMinWidth, available - chipsWidth - gap);
            search.Measure(new Size(room, availableSize.Height));
            width += gap + Math.Max(SearchMinWidth, search.DesiredSize.Width);
            height = Math.Max(height, search.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(available) ? width : Math.Min(width, available), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var chips = Chips;
        var search = Search;
        var x = 0.0;
        if (chips is not null)
        {
            var width = search is null ? finalSize.Width : Math.Min(chips.DesiredSize.Width, Math.Max(0, finalSize.Width - SearchMinWidth - Spacing));
            chips.Arrange(new Rect(0, 0, width, finalSize.Height));
            x = width > 0 ? width + Spacing : 0;
        }
        if (search is not null)
        {
            search.Arrange(new Rect(x, 0, Math.Max(0, finalSize.Width - x), finalSize.Height));
        }
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (!ReferenceEquals(child, chips) && !ReferenceEquals(child, search))
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        return finalSize;
    }
}
