using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One month of days for the date picker of a <see cref="BookingPill"/>, drawn by this control: weekday initials, the days,
/// the stay as a band from check-in to check-out with round ends, a preview band while the check-out is being chosen, and
/// a keyboard cursor. Days before <see cref="BookingPill.MinDate"/> are faded and cannot be picked.
/// </summary>
internal sealed class BookingPillMonth : Control
{
    private const int Rows = 6;

    private readonly TextLayout?[] _days = new TextLayout?[31];
    private readonly TextLayout?[] _mutedDays = new TextLayout?[31];
    private readonly TextLayout?[] _endDays = new TextLayout?[31];
    private readonly TextLayout?[] _weekdays = new TextLayout?[7];
    private DateOnly _month;
    private DateOnly _cursor;
    private DateOnly? _hover;
    private Pen? _focusPen;
    private bool _textBuilt;

    static BookingPillMonth()
    {
        FocusableProperty.OverrideDefaultValue<BookingPillMonth>(true);
        ClipToBoundsProperty.OverrideDefaultValue<BookingPillMonth>(false);
    }

    public BookingPillMonth(BookingPill owner)
    {
        Owner = owner;
        _month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        _cursor = DateOnly.FromDateTime(DateTime.Today);
    }

    public BookingPill Owner { get; }

    /// <summary>The first day of the month shown.</summary>
    public DateOnly Month => _month;

    /// <summary>The day the keyboard cursor is on.</summary>
    public DateOnly CursorDay => _cursor;

    /// <summary>Raised when the month shown changes, by the keys or by <see cref="ShowMonth"/>.</summary>
    public event Action? MonthChanged;

    private double Cell => Owner.DayCellSize;

    private double HeaderHeight => Math.Round(Cell * 0.7);

    private DayOfWeek FirstDay => Owner.Culture.DateTimeFormat.FirstDayOfWeek;

    public void ShowMonth(DateOnly any)
    {
        var month = new DateOnly(any.Year, any.Month, 1);
        if (month == _month)
        {
            return;
        }
        _month = month;
        if (_cursor.Year != month.Year || _cursor.Month != month.Month)
        {
            _cursor = Clamp(month);
        }
        MonthChanged?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Puts the cursor on a day, turning the month when needed.</summary>
    public void MoveCursor(DateOnly day)
    {
        _cursor = Clamp(day);
        ShowMonth(_cursor);
        InvalidateVisual();
    }

    private DateOnly Clamp(DateOnly day) => Owner.MinDate is { } min && day < min ? min : day;

    public void Refresh()
    {
        _textBuilt = false;
        _focusPen = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Cell * 7, HeaderHeight + Cell * Rows);

    /// <summary>The first cell of the grid: the start of the week that holds the 1st.</summary>
    private DateOnly GridStart()
    {
        var offset = ((int)_month.DayOfWeek - (int)FirstDay + 7) % 7;
        return _month.AddDays(-offset);
    }

    private Rect CellRect(int index)
    {
        var cellWidth = Bounds.Width / 7;
        return new Rect(index % 7 * cellWidth, HeaderHeight + index / 7 * Cell, cellWidth, Cell);
    }

    private DateOnly? DayAt(Point point)
    {
        if (point.Y < HeaderHeight || point.X < 0 || point.X >= Bounds.Width)
        {
            return null;
        }
        var column = (int)(point.X / (Bounds.Width / 7));
        var row = (int)((point.Y - HeaderHeight) / Cell);
        if (row is < 0 or >= Rows)
        {
            return null;
        }
        var day = GridStart().AddDays(row * 7 + column);
        return day.Month == _month.Month ? day : null;
    }

    // --- input ----------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var day = e.Pointer.Type == PointerType.Touch ? null : DayAt(e.GetPosition(this));
        if (day != _hover)
        {
            _hover = day;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover is not null)
        {
            _hover = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (DayAt(e.GetPosition(this)) is not { } day || !Owner.CanPick(day))
        {
            return;
        }
        _cursor = day;
        Focus(NavigationMethod.Pointer);
        Owner.PickDay(day);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var start = (int)FirstDay;
        switch (e.Key)
        {
            case Key.Left:
                MoveCursor(_cursor.AddDays(-1));
                break;
            case Key.Right:
                MoveCursor(_cursor.AddDays(1));
                break;
            case Key.Up:
                MoveCursor(_cursor.AddDays(-7));
                break;
            case Key.Down:
                MoveCursor(_cursor.AddDays(7));
                break;
            case Key.PageUp:
                MoveCursor(_cursor.AddMonths(-1));
                break;
            case Key.PageDown:
                MoveCursor(_cursor.AddMonths(1));
                break;
            case Key.Home:
                MoveCursor(_cursor.AddDays(-(((int)_cursor.DayOfWeek - start + 7) % 7)));
                break;
            case Key.End:
                MoveCursor(_cursor.AddDays(6 - ((int)_cursor.DayOfWeek - start + 7) % 7));
                break;
            case Key.Enter:
            case Key.Space:
                if (Owner.CanPick(_cursor))
                {
                    Owner.PickDay(_cursor);
                }
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BookingPillMonthAutomationPeer(this);

    /// <summary>The cursor day as read: "Saturday, March 14, 2026, check-in".</summary>
    internal string CursorText()
    {
        var text = _cursor.ToString("D", Owner.Culture);
        if (_cursor == Owner.CheckIn)
        {
            text += ", " + Owner.CheckInText;
        }
        else if (_cursor == Owner.CheckOut)
        {
            text += ", " + Owner.CheckOutText;
        }
        return text;
    }

    // --- drawing --------------------------------------------------------------------------------------------------

    private void BuildText()
    {
        _textBuilt = true;
        var culture = Owner.Culture;
        var face = new Typeface(Owner.FontFamily, FontStyle.Normal, FontWeight.Medium);
        var size = Owner.DayFontSize;
        var normal = Owner.Foreground;
        var muted = Owner.MutedBrush ?? normal;
        var end = Owner.EndTextBrush ?? normal;
        for (var i = 0; i < 31; i++)
        {
            var text = (i + 1).ToString(culture);
            _days[i]?.Dispose();
            _mutedDays[i]?.Dispose();
            _endDays[i]?.Dispose();
            _days[i] = new TextLayout(text, face, size, normal);
            _mutedDays[i] = new TextLayout(text, face, size, muted);
            _endDays[i] = new TextLayout(text, face, size, end);
        }
        var names = culture.DateTimeFormat.ShortestDayNames;
        for (var i = 0; i < 7; i++)
        {
            _weekdays[i]?.Dispose();
            _weekdays[i] = new TextLayout(names[((int)FirstDay + i) % 7], face, Owner.WeekdayFontSize, muted);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_textBuilt)
        {
            BuildText();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_textBuilt)
        {
            BuildText();
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (!_textBuilt)
        {
            return;
        }
        var cellWidth = Bounds.Width / 7;
        for (var i = 0; i < 7; i++)
        {
            if (_weekdays[i] is { } weekday)
            {
                weekday.Draw(context, new Point(i * cellWidth + (cellWidth - weekday.WidthIncludingTrailingWhitespace) / 2,
                    (HeaderHeight - weekday.Height) / 2));
            }
        }

        var start = GridStart();
        var checkIn = Owner.CheckIn;
        var checkOut = Owner.CheckOut;
        // While the check-out is being chosen, the band previews the stay up to the day under the pointer.
        var bandEnd = checkOut ?? (checkIn is { } from && _hover is { } h && h > from ? h : null);
        var preview = checkOut is null;
        var band = preview ? Owner.PreviewBrush : Owner.RangeBrush;
        var endFill = Owner.EndFillBrush;
        var hoverFill = Owner.HoverBrush;
        var dot = Owner.EndFillBrush;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var radius = Math.Min(cellWidth, Cell) / 2 - 2;

        for (var index = 0; index < Rows * 7; index++)
        {
            var day = start.AddDays(index);
            if (day.Month != _month.Month)
            {
                continue;
            }
            var rect = CellRect(index);
            var center = rect.Center;
            var isIn = day == checkIn;
            var isOut = day == checkOut;
            // The band: a strip through the stay, rounded at its two ends.
            if (checkIn is { } a && bandEnd is { } b && day >= a && day <= b && a != b && band is not null)
            {
                var left = day == a ? center.X : rect.X;
                var right = day == b ? center.X : rect.Right;
                var strip = new Rect(left, center.Y - radius, Math.Max(0, right - left), radius * 2);
                context.DrawRectangle(band, null, strip);
            }
            if ((isIn || isOut) && endFill is not null)
            {
                context.DrawEllipse(endFill, null, center, radius, radius);
            }
            else if (day == _hover && Owner.CanPick(day) && hoverFill is not null)
            {
                context.DrawEllipse(hoverFill, null, center, radius, radius);
            }
            var layouts = isIn || isOut ? _endDays : Owner.CanPick(day) ? _days : _mutedDays;
            if (layouts[day.Day - 1] is { } layout)
            {
                layout.Draw(context, new Point(center.X - layout.WidthIncludingTrailingWhitespace / 2, center.Y - layout.Height / 2));
            }
            if (day == today && !isIn && !isOut && dot is not null)
            {
                context.DrawEllipse(dot, null, new Point(center.X, center.Y + radius - 4), 1.75, 1.75);
            }
            if (day == _cursor && IsFocused && Classes.Contains(":focus-visible") && Owner.FocusBrush is { } focus)
            {
                _focusPen ??= new Pen(focus, 2);
                context.DrawEllipse(null, _focusPen, center, radius + 2, radius + 2);
            }
        }
    }
}

/// <summary>The month as a group named by its title, with the cursor day as help text.</summary>
internal sealed class BookingPillMonthAutomationPeer : ControlAutomationPeer
{
    public BookingPillMonthAutomationPeer(BookingPillMonth owner) : base(owner)
    {
    }

    private new BookingPillMonth Owner => (BookingPillMonth)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Calendar;

    protected override string? GetNameCore()
        => base.GetNameCore() is { Length: > 0 } name ? name : Owner.Month.ToString("MMMM yyyy", Owner.Owner.Culture);

    protected override string? GetHelpTextCore() => Owner.CursorText();
}
