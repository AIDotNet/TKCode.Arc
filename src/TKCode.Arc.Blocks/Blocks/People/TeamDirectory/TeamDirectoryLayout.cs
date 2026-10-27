using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The header row: the title (first child) and the search box (second child). Normally the title sits at the left and a
/// <see cref="SearchWidth"/>-wide search box at the right, both centred on the row; compact, the title takes a line of its
/// own and the search box spans the width under it, <see cref="CompactGap"/> below.
/// </summary>
internal sealed class TeamDirectoryHeader : Panel
{
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<TeamDirectoryHeader>();

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<TeamDirectoryHeader, bool>(nameof(IsCompact));

    public static readonly StyledProperty<double> SearchWidthProperty =
        AvaloniaProperty.Register<TeamDirectoryHeader, double>(nameof(SearchWidth), 240);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TeamDirectoryHeader, double>(nameof(Gap), 16);

    public static readonly StyledProperty<double> CompactGapProperty =
        AvaloniaProperty.Register<TeamDirectoryHeader, double>(nameof(CompactGap), 10);

    static TeamDirectoryHeader()
    {
        AffectsMeasure<TeamDirectoryHeader>(PaddingProperty, IsCompactProperty, SearchWidthProperty, GapProperty, CompactGapProperty);
    }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public double SearchWidth { get => GetValue(SearchWidthProperty); set => SetValue(SearchWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double CompactGap { get => GetValue(CompactGapProperty); set => SetValue(CompactGapProperty, value); }

    private Control? Title => Children.Count > 0 ? Children[0] : null;

    private Control? Search => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var inner = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - padding.Left - padding.Right);
        var title = Title;
        var search = Search;
        double height;
        if (IsCompact)
        {
            title?.Measure(new Size(inner, double.PositiveInfinity));
            search?.Measure(new Size(inner, double.PositiveInfinity));
            height = (title?.DesiredSize.Height ?? 0) + (search is null ? 0 : CompactGap + search.DesiredSize.Height);
        }
        else
        {
            var searchWidth = search is null ? 0 : Math.Min(SearchWidth, inner);
            search?.Measure(new Size(searchWidth, double.PositiveInfinity));
            var titleWidth = search is null ? inner : Math.Max(0, inner - searchWidth - Gap);
            title?.Measure(new Size(titleWidth, double.PositiveInfinity));
            height = Math.Max(title?.DesiredSize.Height ?? 0, search?.DesiredSize.Height ?? 0);
        }
        var width = double.IsInfinity(availableSize.Width)
            ? (title?.DesiredSize.Width ?? 0) + (search?.DesiredSize.Width ?? 0) + Gap + padding.Left + padding.Right
            : availableSize.Width;
        return new Size(width, height + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var inner = new Rect(padding.Left, padding.Top, Math.Max(0, finalSize.Width - padding.Left - padding.Right),
            Math.Max(0, finalSize.Height - padding.Top - padding.Bottom));
        var title = Title;
        var search = Search;
        if (IsCompact)
        {
            var titleHeight = title?.DesiredSize.Height ?? 0;
            title?.Arrange(new Rect(inner.X, inner.Y, inner.Width, titleHeight));
            search?.Arrange(new Rect(inner.X, inner.Y + titleHeight + CompactGap, inner.Width, search.DesiredSize.Height));
            return finalSize;
        }
        var searchWidth = search is null ? 0 : Math.Min(SearchWidth, inner.Width);
        if (search is not null)
        {
            var h = search.DesiredSize.Height;
            search.Arrange(new Rect(inner.Right - searchWidth, inner.Y + (inner.Height - h) / 2, searchWidth, h));
        }
        if (title is not null)
        {
            var h = title.DesiredSize.Height;
            var w = search is null ? inner.Width : Math.Max(0, inner.Width - searchWidth - Gap);
            title.Arrange(new Rect(inner.X, inner.Y + (inner.Height - h) / 2, w, h));
        }
        return finalSize;
    }
}

/// <summary>
/// The body under the filter: the list pane (first child) and the profile pane (second child). Two columns, the list
/// max(<see cref="ListMinWidth"/>, <see cref="ListFraction"/> of the width) wide and the body at least
/// <see cref="MinBodyHeight"/> high; stacked, the list on top at full width and the profile under it, as high as they are.
/// </summary>
internal sealed class TeamDirectoryBody : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<TeamDirectoryBody, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> ListMinWidthProperty =
        AvaloniaProperty.Register<TeamDirectoryBody, double>(nameof(ListMinWidth), 196);

    public static readonly StyledProperty<double> ListFractionProperty =
        AvaloniaProperty.Register<TeamDirectoryBody, double>(nameof(ListFraction), 0.36);

    public static readonly StyledProperty<double> MinBodyHeightProperty =
        AvaloniaProperty.Register<TeamDirectoryBody, double>(nameof(MinBodyHeight), 432);

    static TeamDirectoryBody()
    {
        AffectsMeasure<TeamDirectoryBody>(IsStackedProperty, ListMinWidthProperty, ListFractionProperty, MinBodyHeightProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double ListMinWidth { get => GetValue(ListMinWidthProperty); set => SetValue(ListMinWidthProperty, value); }

    public double ListFraction { get => GetValue(ListFractionProperty); set => SetValue(ListFractionProperty, value); }

    public double MinBodyHeight { get => GetValue(MinBodyHeightProperty); set => SetValue(MinBodyHeightProperty, value); }

    private Control? List => Children.Count > 0 ? Children[0] : null;

    private Control? Detail => Children.Count > 1 ? Children[1] : null;

    /// <summary>The list column's width for a body <paramref name="width"/> wide (two columns).</summary>
    internal double ListWidthFor(double width) => Math.Min(width, Math.Max(ListMinWidth, width * ListFraction));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 760 : availableSize.Width;
        var list = List;
        var detail = Detail;
        if (IsStacked)
        {
            list?.Measure(new Size(width, double.PositiveInfinity));
            detail?.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, (list?.DesiredSize.Height ?? 0) + (detail?.DesiredSize.Height ?? 0));
        }
        var listWidth = ListWidthFor(width);
        list?.Measure(new Size(listWidth, double.PositiveInfinity));
        detail?.Measure(new Size(Math.Max(0, width - listWidth), double.PositiveInfinity));
        var height = Math.Max(MinBodyHeight, Math.Max(list?.DesiredSize.Height ?? 0, detail?.DesiredSize.Height ?? 0));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var list = List;
        var detail = Detail;
        if (IsStacked)
        {
            var listHeight = list?.DesiredSize.Height ?? 0;
            list?.Arrange(new Rect(0, 0, finalSize.Width, listHeight));
            detail?.Arrange(new Rect(0, listHeight, finalSize.Width, Math.Max(0, finalSize.Height - listHeight)));
            return finalSize;
        }
        var listWidth = ListWidthFor(finalSize.Width);
        list?.Arrange(new Rect(0, 0, listWidth, finalSize.Height));
        detail?.Arrange(new Rect(listWidth, 0, Math.Max(0, finalSize.Width - listWidth), finalSize.Height));
        return finalSize;
    }
}
