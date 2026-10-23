using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The block of an event being drawn or waiting in the quick create popover (notes B §3.1): the calendar's hover fill, its
/// 3px bar, a 1px outline at 60% and a raised shadow, "New event" over the time. It follows the drag slot by slot with no
/// motion and takes the calendar picked in the popover. Not hit, not read (the popover speaks for it).
/// </summary>
internal sealed class WeekCalendarDraftPresenter : Control
{
    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _title = new();
    private readonly WeekCalendarText _time = new();

    public WeekCalendarDraftPresenter(WeekCalendar owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        ZIndex = 7;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The day it is on.</summary>
    public DateOnly Day { get; private set; }

    /// <summary>The first minute.</summary>
    public int Start { get; private set; }

    /// <summary>The minute it ends at.</summary>
    public int End { get; private set; }

    /// <summary>The calendar whose colors it takes.</summary>
    public string? CalendarId { get; private set; }

    /// <summary>Moves the draft (no motion: Arc moves it a slot at a time).</summary>
    public void Set(DateOnly day, int start, int end, string? calendarId)
    {
        var moved = day != Day || start != Start || end != End;
        Day = day;
        Start = start;
        End = end;
        if (moved)
        {
            (Parent as Control)?.InvalidateArrange();
        }
        if (moved || calendarId != CalendarId)
        {
            CalendarId = calendarId;
            InvalidateVisual();
        }
    }

    public void OnThemeChanged()
    {
        _title.Reset();
        _time.Reset();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var brushes = paint.For(CalendarId);
        var body = new Rect(WeekCalendarMetrics.EventInsetLeft, 0,
            Math.Max(0, size.Width - WeekCalendarMetrics.EventInsetLeft - WeekCalendarMetrics.EventInsetRight), size.Height);
        const double radius = WeekCalendarMetrics.EventRadius;
        context.DrawRectangle(brushes.HoverFill, null, new RoundedRect(body, radius), paint.ShadowRaised);
        using (context.PushClip(new RoundedRect(body, radius)))
        {
            context.FillRectangle(brushes.Bar, new Rect(body.X, body.Y, WeekCalendarMetrics.EventBarWidth, body.Height));
            var left = body.X + 10;
            var room = Math.Max(0, body.Right - 8 - left);
            var title = _title.Get(_owner.Labels.DraftTitle, paint.BodyMedium, 12, brushes.Text, room, 1, 15.6);
            title?.Draw(context, new Point(left, body.Y + 5));
            var time = _time.Get(WeekCalendarTime.TimeRange(Start, End, _owner.EffectiveCulture), paint.Body, 12, brushes.SubText, room, 1, 15.6);
            time?.Draw(context, new Point(left, body.Y + 5 + 15.6 + 1));
        }
        context.DrawRectangle(null, brushes.DraftOutline, new RoundedRect(body.Deflate(0.5), radius - 0.5));
    }
}

/// <summary>Where a dragged event came from (notes B §3.2): its calendar at 6% with a dashed outline at 55%. Not hit, not read.</summary>
internal sealed class WeekCalendarDragOrigin : Control
{
    private readonly WeekCalendar _owner;

    public WeekCalendarDragOrigin(WeekCalendar owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Focusable = false;
        ZIndex = 4;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The calendar whose colors it takes.</summary>
    public string? CalendarId { get; set; }

    /// <summary>The block's rect before the drag (pane coordinates).</summary>
    public Rect Box { get; set; }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var brushes = paint.For(CalendarId);
        var body = new Rect(WeekCalendarMetrics.EventInsetLeft, 0,
            Math.Max(0, size.Width - WeekCalendarMetrics.EventInsetLeft - WeekCalendarMetrics.EventInsetRight), size.Height);
        context.DrawRectangle(brushes.OriginFill, brushes.OriginOutline, new RoundedRect(body.Deflate(0.5), WeekCalendarMetrics.EventRadius));
    }
}

/// <summary>
/// The time of a drag in a pill over the block (notes B §3.2): the foreground color with background text, 12px medium,
/// raised; centred on the day column, 26 above the block, or under it when the top of the visible hours would hide it.
/// </summary>
internal sealed class WeekCalendarDragLabel : Control
{
    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _text = new();
    private string _label = string.Empty;

    public WeekCalendarDragLabel(WeekCalendar owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Focusable = false;
        ZIndex = 13;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The text shown ("12:30 – 2 PM").</summary>
    public string Text
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The column it is centred in and the y of its top (pane coordinates).</summary>
    public Rect Column { get; set; }

    public double Top { get; set; }

    /// <summary>The pill's width for the current text.</summary>
    public double PillWidth
    {
        get
        {
            var paint = _owner.Paint;
            paint.EnsureReady();
            var layout = _text.Get(_label, paint.BodyMedium, 12, paint.Background, double.PositiveInfinity, 1, 15.6);
            return WeekCalendarText.NaturalWidth(layout) + 16;
        }
    }

    public void OnThemeChanged()
    {
        _text.Reset();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrEmpty(_label))
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var layout = _text.Get(_label, paint.BodyMedium, 12, paint.Background, double.PositiveInfinity, 1, 15.6);
        var pill = new Rect(new Size(Bounds.Width, Bounds.Height));
        context.DrawRectangle(paint.Foreground, null, new RoundedRect(pill, pill.Height / 2), paint.ShadowRaised);
        layout?.Draw(context, new Point(8, (pill.Height - 15.6) / 2));
    }
}
