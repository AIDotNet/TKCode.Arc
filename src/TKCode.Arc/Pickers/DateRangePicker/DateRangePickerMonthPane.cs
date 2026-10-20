using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a range month pane needs from its panel.</summary>
internal interface IDateRangePickerMonthHost
{
    DateRangePickerPalette Palette { get; }
    void OnDayHovered(DateOnly date);
    void OnDayPicked(DateOnly date);
    void OnDayFocused(DateOnly date);
}

/// <summary>
/// One month of the range picker: its title, two-letter weekday heads, six weeks of day cells, and under them the
/// range highlight: one band per week that stretches from the edge the range arrives from (STRETCH spring, 0.3s,
/// bounce 0.04) and a solid end disc at each side of the range that glides from day to day (GLIDE, 0.3s, bounce 0.1)
/// and only fades when that end leaves this month. All of it is drawn here; a hover only moves motion values.
/// </summary>
internal sealed class DateRangePickerMonthPane : Control
{
    public const double TitleHeight = 32;
    public const double WeekdayHeight = 28;
    public const int Cells = 42;

    internal static readonly SpringSpec Stretch = SpringSpec.FromVisual(0.3, 0.04);
    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.3, 0.1);

    private readonly IDateRangePickerMonthHost _host;
    private readonly DateRangePickerDayCell[] _cells = new DateRangePickerDayCell[Cells];
    private readonly Band[] _bands = new Band[6];
    private readonly EndDisc _start;
    private readonly EndDisc _end;
    private readonly FormattedText?[] _weekdays = new FormattedText?[7];
    private FormattedText? _title;
    private string _titleText = string.Empty;
    private string[] _weekdayNames = new string[7];
    private int _textVersion = -1;
    private DateOnly _gridStart;
    private double _cell = 36;

    public DateRangePickerMonthPane(IDateRangePickerMonthHost host)
    {
        _host = host;
        for (var i = 0; i < Cells; i++)
        {
            var cell = new DateRangePickerDayCell(this);
            _cells[i] = cell;
            VisualChildren.Add(cell);
            LogicalChildren.Add(cell);
        }
        for (var i = 0; i < 6; i++)
        {
            _bands[i] = new Band(this);
        }
        _start = new EndDisc(this);
        _end = new EndDisc(this);
        ClipToBounds = false;
    }

    public DateOnly Month { get; private set; }

    public DateRangePickerPalette Palette => _host.Palette;

    public IReadOnlyList<DateRangePickerDayCell> DayCells => _cells;

    public double CellSize
    {
        get => _cell;
        set
        {
            if (Math.Abs(value - _cell) > 0.01)
            {
                _cell = value;
                InvalidateMeasure();
            }
        }
    }

    public DateRangePickerDayCell? FindCell(DateOnly date)
    {
        foreach (var cell in _cells)
        {
            if (cell.IsVisible && cell.Date == date)
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>
    /// Shows <paramref name="month"/> with <paramref name="range"/> highlighted. A new month places the highlight
    /// at once (a fresh pane in Arc); the same month animates bands and discs to the new range.
    /// </summary>
    public void Configure(DateOnly month, DayOfWeek firstDay, DateRange? range, DateOnly? tabbable, DateOnly today,
                          DateOnly? min, DateOnly? max, CultureInfo culture, bool animate)
    {
        var monthChanged = month != Month || CalendarMath.GridStart(month, firstDay) != _gridStart;
        Month = month;
        _gridStart = CalendarMath.GridStart(month, firstDay);
        var motion = animate && !monthChanged && IsLoaded && ArcMotion.IsFull(this);
        UpdateTexts(month, culture);
        for (var i = 0; i < Cells; i++)
        {
            var date = _gridStart.AddDays(i);
            var cell = _cells[i];
            var inMonth = CalendarMath.SameMonth(date, month);
            cell.IsVisible = inMonth;
            if (!inMonth)
            {
                cell.IsTabStop = false;
                continue;
            }
            var disabled = (min is { } lo && date < lo) || (max is { } hi && date > hi);
            var edge = range is { } r && (date == r.Start || date == r.End);
            var inRange = range is { } rr && rr.Contains(date);
            cell.Configure(date, disabled, date == today, edge, inRange, tabbable == date, culture, animate && !monthChanged);
        }
        UpdateHighlight(range, motion, monthChanged);
        AutomationProperties.SetName(this, _titleText);
        InvalidateVisual();
    }

    private void UpdateTexts(DateOnly month, CultureInfo culture)
    {
        var title = month.ToDateTime(TimeOnly.MinValue).ToString(culture.DateTimeFormat.YearMonthPattern, culture);
        var palette = Palette;
        var fontChanged = _textVersion != palette.Version;
        if (title != _titleText || fontChanged || _title is null)
        {
            _titleText = title;
            _title = new FormattedText(title, culture, FlowDirection.LeftToRight,
                new Typeface(palette.FontFamily, FontStyle.Normal, palette.MediumWeight), palette.FontSize,
                new SolidColorBrush(palette.Foreground));
        }
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)_gridStart.DayOfWeek + i) % 7);
            var abbreviated = culture.DateTimeFormat.GetAbbreviatedDayName(day);
            var name = abbreviated.Length > 2 ? abbreviated[..2] : abbreviated;
            if (name != _weekdayNames[i] || fontChanged || _weekdays[i] is null)
            {
                _weekdayNames[i] = name;
                _weekdays[i] = new FormattedText(name, culture, FlowDirection.LeftToRight,
                    new Typeface(palette.FontFamily, FontStyle.Normal, palette.RegularWeight), palette.SmallFontSize,
                    new SolidColorBrush(palette.Muted));
            }
        }
        _textVersion = palette.Version;
    }

    /// <summary>Each week's slice of the range (in cells), collapsed at the edge the range would grow in from.</summary>
    private void UpdateHighlight(DateRange? range, bool motion, bool monthChanged)
    {
        for (var row = 0; row < 6; row++)
        {
            (double Left, double Width)? segment = null;
            if (range is { } r)
            {
                DateOnly? first = null;
                DateOnly? last = null;
                for (var col = 0; col < 7; col++)
                {
                    var date = _gridStart.AddDays(row * 7 + col);
                    if (CalendarMath.SameMonth(date, Month))
                    {
                        first ??= date;
                        last = date;
                    }
                }
                if (first is { } rowFirst && last is { } rowLast)
                {
                    var from = r.Start > rowFirst ? r.Start : rowFirst;
                    var to = r.End < rowLast ? r.End : rowLast;
                    if (from <= to)
                    {
                        var a = Column(from);
                        var b = Column(to);
                        segment = (a, b - a + 1);
                    }
                    else if (rowLast < r.Start)
                    {
                        segment = (Column(rowLast) + 1, 0);
                    }
                    else
                    {
                        segment = (Column(rowFirst), 0);
                    }
                }
            }
            _bands[row].Set(segment, motion, monthChanged);
        }
        _start.Set(range is { } s && CalendarMath.SameMonth(s.Start, Month) ? CellOf(s.Start) : null, motion, monthChanged);
        _end.Set(range is { } e && e.End != e.Start && CalendarMath.SameMonth(e.End, Month) ? CellOf(e.End) : null, motion, monthChanged);
    }

    private int Column(DateOnly date) => (date.DayNumber - _gridStart.DayNumber) % 7;

    private Point CellOf(DateOnly date)
    {
        var index = date.DayNumber - _gridStart.DayNumber;
        return new Point(index % 7, index / 7);
    }

    internal void OnDayHovered(DateRangePickerDayCell cell) => _host.OnDayHovered(cell.Date);

    internal void OnDayPicked(DateRangePickerDayCell cell) => _host.OnDayPicked(cell.Date);

    internal void OnDayFocused(DateRangePickerDayCell cell) => _host.OnDayFocused(cell.Date);

    private double WeeksTop => TitleHeight + WeekdayHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var cell in _cells)
        {
            cell.Measure(new Size(_cell, _cell));
        }
        return new Size(_cell * 7, WeeksTop + _cell * 6);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Cells; i++)
        {
            _cells[i].Arrange(new Rect(i % 7 * _cell, WeeksTop + i / 7 * _cell, _cell, _cell));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var width = _cell * 7;
        if (_title is { } title)
        {
            context.DrawText(title, new Point((width - title.Width) / 2, (TitleHeight - title.Height) / 2));
        }
        for (var i = 0; i < 7; i++)
        {
            if (_weekdays[i] is { } weekday)
            {
                context.DrawText(weekday, new Point(i * _cell + (_cell - weekday.Width) / 2, TitleHeight + (WeekdayHeight - weekday.Height) / 2));
            }
        }
        var palette = Palette;
        for (var row = 0; row < 6; row++)
        {
            var band = _bands[row];
            var opacity = band.Opacity;
            var w = band.Width * _cell;
            if (opacity <= 0.001 || w <= 0.01)
            {
                continue;
            }
            var rect = new Rect(band.Left * _cell, WeeksTop + row * _cell + 3, w, _cell - 6);
            using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
            {
                context.DrawRectangle(palette.AccentSubtle, null, new RoundedRect(rect, rect.Height / 2));
            }
        }
        DrawDisc(context, _start, palette);
        DrawDisc(context, _end, palette);
    }

    private void DrawDisc(DrawingContext context, EndDisc disc, DateRangePickerPalette palette)
    {
        var opacity = disc.Opacity;
        if (opacity <= 0.001)
        {
            return;
        }
        var position = disc.Position;
        var center = new Point(position.X * _cell + _cell / 2, WeeksTop + position.Y * _cell + _cell / 2);
        var r = Math.Max(0, _cell / 2 - 3) * disc.Scale;
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.DrawEllipse(palette.Accent, null, center, r, r);
        }
    }

    /// <summary>One week's slice of the range: left edge and width in cells, and its opacity.</summary>
    private sealed class Band
    {
        private readonly MotionValue<double> _left;
        private readonly MotionValue<double> _width;
        private readonly MotionValue<double> _opacity;
        private bool _wasEmpty = true;

        public Band(Visual owner)
        {
            _left = Create(owner, 0);
            _width = Create(owner, 0);
            _opacity = Create(owner, 0);
        }

        private static MotionValue<double> Create(Visual owner, double initial)
        {
            var value = new MotionValue<double>(owner, initial, MotionVectors.Double);
            value.Changed += _ => owner.InvalidateVisual();
            return value;
        }

        public double Left => _left.Value;
        public double Width => _width.Value;
        public double Opacity => _opacity.Value;

        public void Set((double Left, double Width)? segment, bool motion, bool reset)
        {
            if (segment is not { } s)
            {
                _wasEmpty = true;
                if (motion)
                {
                    _ = _opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
                }
                else
                {
                    _opacity.Jump(0);
                }
                return;
            }
            if (_wasEmpty || !motion || reset)
            {
                _left.Jump(s.Left);
                _width.Jump(s.Width);
            }
            else
            {
                _ = _left.AnimateTo(s.Left, MotionSpec.Spring(Stretch));
                _ = _width.AnimateTo(s.Width, MotionSpec.Spring(Stretch));
            }
            _wasEmpty = false;
            if (motion)
            {
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Standard));
            }
            else
            {
                _opacity.Jump(1);
            }
        }
    }

    /// <summary>A solid end of the range: glides between days, fades and shrinks to 0.8 when the end leaves the month.</summary>
    private sealed class EndDisc
    {
        private readonly MotionValue<Point> _position;
        private readonly MotionValue<double> _opacity;
        private readonly MotionValue<double> _scale;
        private bool _hidden = true;

        public EndDisc(Visual owner)
        {
            _position = new MotionValue<Point>(owner, default, MotionVectors.Point);
            _position.Changed += _ => owner.InvalidateVisual();
            _opacity = new MotionValue<double>(owner, 0, MotionVectors.Double);
            _opacity.Changed += _ => owner.InvalidateVisual();
            _scale = new MotionValue<double>(owner, 0.8, MotionVectors.Double);
            _scale.Changed += _ => owner.InvalidateVisual();
        }

        public Point Position => _position.Value;
        public double Opacity => _opacity.Value;
        public double Scale => _scale.Value;

        public void Set(Point? cell, bool motion, bool reset)
        {
            if (cell is not { } c)
            {
                _hidden = true;
                if (motion)
                {
                    var fade = MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard);
                    _ = _opacity.AnimateTo(0, fade);
                    _ = _scale.AnimateTo(0.8, fade);
                }
                else
                {
                    _opacity.Jump(0);
                    _scale.Jump(0.8);
                }
                return;
            }
            if (_hidden || !motion || reset)
            {
                _position.Jump(c);
            }
            else
            {
                _ = _position.AnimateTo(c, MotionSpec.Spring(Glide));
            }
            _hidden = false;
            if (motion)
            {
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Standard));
                _ = _scale.AnimateTo(1, MotionSpec.Spring(Glide));
            }
            else
            {
                _opacity.Jump(1);
                _scale.Jump(1);
            }
        }
    }
}
