using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One month on the strip: 6 fixed weeks of day cells (4px gaps) over the month's selected disc. The disc is
/// placed by week and weekday rather than measured, so it glides on a straight line between any two days,
/// keeps gliding while the strip slides, and only grows in or fades out when the selection enters or
/// leaves this month.
/// </summary>
internal sealed class CalendarMonthPane : Avalonia.Controls.Control
{
    public const double Gap = 4;
    public const int Cells = 42;

    private readonly CalendarDayCell[] _cells = new CalendarDayCell[Cells];
    private readonly Calendar _owner;
    private MotionValue<Point>? _disc;
    private MotionValue<double>? _discOpacity;
    private MotionValue<double>? _discScale;
    private MotionValue<double>? _discPress;
    private int _discCell = -1;
    private bool _discShown;

    public CalendarMonthPane(Calendar owner)
    {
        _owner = owner;
        for (var i = 0; i < Cells; i++)
        {
            var cell = new CalendarDayCell(this);
            _cells[i] = cell;
            VisualChildren.Add(cell);
            LogicalChildren.Add(cell);
        }
        ClipToBounds = false;
    }

    public int MonthIndex { get; private set; } = int.MinValue;

    public DateOnly Month { get; private set; }

    public bool IsPresent { get; private set; }

    public CalendarPalette? Palette => _owner.Palette;

    public IReadOnlyList<CalendarDayCell> DayCells => _cells;

    public CalendarDayCell? FindCell(DateOnly date)
    {
        foreach (var cell in _cells)
        {
            if (cell.Date == date)
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>Configures all days for <paramref name="monthIndex"/>; <paramref name="handoff"/> carries the selection number delays.</summary>
    public void Configure(int monthIndex, bool present, CalendarSelectionHandoff? handoff)
    {
        var monthChanged = monthIndex != MonthIndex;
        MonthIndex = monthIndex;
        Month = CalendarMath.FromIndex(monthIndex);
        IsPresent = present;
        IsHitTestVisible = present;
        var start = CalendarMath.GridStart(Month, _owner.FirstDayOfWeek);
        var culture = _owner.EffectiveCulture;
        var tabbable = present ? _owner.TabbableDate(Month) : (DateOnly?)null;
        var selected = _owner.SelectedDate;
        var today = _owner.Today;
        for (var i = 0; i < Cells; i++)
        {
            var date = start.AddDays(i);
            var isSelected = selected == date;
            TimeSpan? delay = null;
            if (handoff is { } h && !monthChanged)
            {
                if (date == h.To)
                {
                    delay = h.ToDelay;
                }
                else if (date == h.From)
                {
                    delay = h.FromDelay;
                }
            }
            _cells[i].Configure(date, !CalendarMath.SameMonth(date, Month), _owner.IsDateDisabled(date), date == today,
                isSelected, tabbable == date, culture, delay);
        }
        var cellIndex = selected is { } s ? s.DayNumber - start.DayNumber : -1;
        UpdateDisc(cellIndex is >= 0 and < Cells ? cellIndex : -1, animate: !monthChanged);
    }

    private void UpdateDisc(int cell, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var motion = animate && IsLoaded && level == ArcMotionLevel.Full;
        _disc ??= CreateValue(new Point(0, 0), MotionVectors.Point);
        _discOpacity ??= CreateValue(0.0, MotionVectors.Double);
        _discScale ??= CreateValue(0.6, MotionVectors.Double);
        if (cell < 0)
        {
            _discCell = -1;
            if (!_discShown)
            {
                return;
            }
            _discShown = false;
            if (motion)
            {
                var fade = MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard);
                _ = _discOpacity.AnimateTo(0, fade);
                _ = _discScale.AnimateTo(0.6, fade);
            }
            else
            {
                _discOpacity.Jump(0);
                _discScale.Jump(0.6);
            }
            return;
        }
        var target = new Point(cell % 7, cell / 7);
        if (!_discShown || !motion)
        {
            _disc.Jump(target);
        }
        else if (cell != _discCell)
        {
            _ = _disc.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
        }
        _discCell = cell;
        if (motion)
        {
            _ = _discOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
            _ = _discScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _discOpacity.Jump(1);
            _discScale.Jump(1);
        }
        _discShown = true;
    }

    public void SetDiscPressed(bool pressed)
    {
        _discPress ??= CreateValue(1.0, MotionVectors.Double);
        _ = _discPress.AnimateTo(pressed ? 0.94 : 1, pressed
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(580), ArcEasings.CssSpring));
    }

    private MotionValue<T> CreateValue<T>(T initial, IMotionVector<T> vector) where T : struct
    {
        var value = new MotionValue<T>(this, initial, vector);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    internal void OnDayActivated(CalendarDayCell cell) => _owner.OnDayActivated(cell.Date);

    internal void OnDayFocused(CalendarDayCell cell) => _owner.OnDayFocused(cell.Date);

    public static double CellSize(double width) => Math.Max(0, (width - Gap * 6) / 7);

    public static double HeightFor(double width) => CellSize(width) * 6 + Gap * 5;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Calendar.DefaultWidth : availableSize.Width;
        var cell = CellSize(width);
        foreach (var c in _cells)
        {
            c.Measure(new Size(cell, cell));
        }
        return new Size(width, HeightFor(width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cell = CellSize(finalSize.Width);
        for (var i = 0; i < Cells; i++)
        {
            var col = i % 7;
            var row = i / 7;
            _cells[i].Arrange(new Rect(col * (cell + Gap), row * (cell + Gap), cell, cell));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var palette = Palette;
        if (palette is null || _discOpacity is null || _disc is null || _discOpacity.Value <= 0.001)
        {
            return;
        }
        var cell = CellSize(Bounds.Width);
        var position = _disc.Value;
        var center = new Point(position.X * (cell + Gap) + cell / 2, position.Y * (cell + Gap) + cell / 2);
        var radius = cell / 2 * (_discScale?.Value ?? 1) * (_discPress?.Value ?? 1);
        var selected = _discCell >= 0 ? _cells[_discCell] : null;
        var hover = selected?.HoverProgress ?? 0;
        using (context.PushOpacity(Math.Clamp(_discOpacity.Value, 0, 1)))
        {
            context.DrawEllipse(palette.SelectionFill, null, center, radius, radius);
            if (hover > 0.001)
            {
                using (context.PushOpacity(hover))
                {
                    context.DrawEllipse(palette.SelectionHoverFill, null, center, radius, radius);
                }
            }
        }
    }
}

/// <summary>Number color delays when the selection moves: the day it leaves waits longer on short hops, the day it lands on longer on long ones.</summary>
internal readonly record struct CalendarSelectionHandoff(DateOnly? From, DateOnly? To, TimeSpan FromDelay, TimeSpan ToDelay)
{
    public static CalendarSelectionHandoff? Create(DateOnly? from, DateOnly? to, DayOfWeek firstDay)
    {
        if (from is not { } a || to is not { } b)
        {
            return null;
        }
        var days = b.DayNumber - a.DayNumber;
        var columns = CalendarMath.Column(b, firstDay) - CalendarMath.Column(a, firstDay);
        var distance = Math.Sqrt(columns * columns + Math.Pow((days - columns) / 7.0, 2));
        if (distance <= 0)
        {
            return null;
        }
        var toDelay = Math.Min(260, 95 + 85 * Math.Log(distance));
        var fromDelay = Math.Max(20, 130 / distance);
        return new CalendarSelectionHandoff(a, b, TimeSpan.FromMilliseconds(Math.Round(fromDelay)),
            TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, toDelay))));
    }
}
