using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The toolbar's layout (notes §3.2, §8.1). Children in this order: Today, previous, next, title, view switch, Calendars,
/// New event; hidden ones take no room. One row: Today, the steps (4 closer), the title taking the rest, then the view
/// switch, Calendars and New event, 12 apart. Two rows (<see cref="IsStacked"/>): the title with Today and the steps on
/// the first, the view switch on the left and Calendars and New event on the right of the second, 10 between the rows.
/// </summary>
internal sealed class WeekCalendarToolbar : Panel
{
    /// <summary>True lays the toolbar out on two rows.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<WeekCalendarToolbar, bool>(nameof(IsStacked));

    /// <summary>Space between controls (12).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<WeekCalendarToolbar, double>(nameof(Gap), 12);

    /// <summary>Space between the two rows (10).</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<WeekCalendarToolbar, double>(nameof(RowGap), 10);

    private const int Today = 0;
    private const int Previous = 1;
    private const int Next = 2;
    private const int Title = 3;
    private const int Views = 4;
    private const int Calendars = 5;
    private const int NewEvent = 6;

    static WeekCalendarToolbar()
    {
        AffectsMeasure<WeekCalendarToolbar>(IsStackedProperty, GapProperty, RowGapProperty);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private Control? At(int index) => index < Children.Count && Children[index].IsVisible ? Children[index] : null;

    private static double W(Control? c) => c?.DesiredSize.Width ?? 0;

    private static double H(Control? c) => c?.DesiredSize.Height ?? 0;

    private double GapAfter(Control? c, double gap) => c is null ? 0 : gap;

    private double Lead() => W(At(Today)) + (At(Today) is not null && (At(Previous) ?? At(Next)) is not null ? Gap - 4 : 0)
                             + W(At(Previous)) + W(At(Next));

    private double Trail()
    {
        var items = new[] { At(Views), At(Calendars), At(NewEvent) }.Where(c => c is not null).ToList();
        return items.Sum(W) + Math.Max(0, items.Count - 1) * Gap;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        for (var i = 0; i < Children.Count; i++)
        {
            if (i != Title)
            {
                Children[i].Measure(infinite);
            }
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1200;
        if (IsStacked)
        {
            var row1Right = W(At(Today)) + GapAfter(At(Today), Gap - 6) + W(At(Previous)) + W(At(Next));
            At(Title)?.Measure(new Size(Math.Max(0, width - row1Right - Gap), double.PositiveInfinity));
            var row1 = Math.Max(H(At(Title)), Math.Max(H(At(Today)), Math.Max(H(At(Previous)), H(At(Next)))));
            var row2 = Math.Max(H(At(Views)), Math.Max(H(At(Calendars)), H(At(NewEvent))));
            return new Size(width, row1 + RowGap + row2);
        }
        var lead = Lead();
        var trail = Trail();
        var titleRoom = Math.Max(0, width - lead - trail - 2 * Gap);
        At(Title)?.Measure(new Size(titleRoom, double.PositiveInfinity));
        var height = Children.Where(c => c.IsVisible).Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max();
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (IsStacked)
        {
            var row1 = Math.Max(H(At(Title)), Math.Max(H(At(Today)), Math.Max(H(At(Previous)), H(At(Next)))));
            var row2 = Math.Max(H(At(Views)), Math.Max(H(At(Calendars)), H(At(NewEvent))));
            var x = width;
            x = PlaceFromRight(At(Next), x, 0, row1);
            x = PlaceFromRight(At(Previous), x, 0, row1);
            if (At(Today) is not null)
            {
                x -= Gap - 6;
                x = PlaceFromRight(At(Today), x, 0, row1);
            }
            Place(At(Title), 0, 0, Math.Max(0, x - Gap), row1);
            var y2 = row1 + RowGap;
            Place(At(Views), 0, y2, W(At(Views)), row2);
            var right = width;
            right = PlaceFromRight(At(NewEvent), right, y2, row2);
            if (At(Calendars) is not null)
            {
                right -= Gap;
                PlaceFromRight(At(Calendars), right, y2, row2);
            }
            return finalSize;
        }
        var height = finalSize.Height;
        var left = 0.0;
        if (At(Today) is { } today)
        {
            Place(today, left, 0, W(today), height);
            left += W(today) + ((At(Previous) ?? At(Next)) is not null ? Gap - 4 : 0);
        }
        if (At(Previous) is { } previous)
        {
            Place(previous, left, 0, W(previous), height);
            left += W(previous);
        }
        if (At(Next) is { } next)
        {
            Place(next, left, 0, W(next), height);
            left += W(next);
        }
        var end = width;
        end = PlaceFromRight(At(NewEvent), end, 0, height);
        if (At(Calendars) is not null)
        {
            end -= Gap;
            end = PlaceFromRight(At(Calendars), end, 0, height);
        }
        if (At(Views) is not null)
        {
            end -= Gap;
            end = PlaceFromRight(At(Views), end, 0, height);
        }
        var titleLeft = left + Gap;
        Place(At(Title), titleLeft, 0, Math.Max(0, end - Gap - titleLeft), height);
        return finalSize;
    }

    private static void Place(Control? child, double x, double y, double width, double rowHeight)
    {
        if (child is null)
        {
            return;
        }
        var h = Math.Min(rowHeight, child.DesiredSize.Height);
        child.Arrange(new Rect(x, y + (rowHeight - h) / 2, width, h));
    }

    private static double PlaceFromRight(Control? child, double right, double y, double rowHeight)
    {
        if (child is null)
        {
            return right;
        }
        var w = child.DesiredSize.Width;
        Place(child, right - w, y, w, rowHeight);
        return right - w;
    }
}
