using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How much an event block shows (notes §3.6), from its own size.</summary>
internal enum WeekCalendarEventLevel
{
    /// <summary>One line: the title and the start time (up to a 30 minute block).</summary>
    Small,

    /// <summary>The title over the time range (up to an hour).</summary>
    Medium,

    /// <summary>Up to three title lines, the time, the place and the people.</summary>
    Large,
}

/// <summary>
/// One timed event in the grid: a self-drawn button (notes §3.6) with the calendar's bar and fill, the title, the time, the
/// place and the attendees, dropping details as it gets smaller. It decides what to show in its own measure (its slot is
/// set by the pane), so no parent works out sizes per event and nothing loops. Past events are dimmed (<c>:past</c>);
/// the selected event (<c>:selected</c>) takes a deeper fill and a raised shadow, the one being dragged (<c>:dragging</c>)
/// a floating shadow, and a grip fades in at the bottom edge where it can be resized. Pooled by the pane; position flights
/// go through the pane's <see cref="ProjectsFlip"/>.
/// </summary>
[PseudoClasses(PastPseudoClass, SmallPseudoClass, MediumPseudoClass, LargePseudoClass, NarrowPseudoClass, SelectedPseudoClass,
    DraggingPseudoClass)]
internal sealed partial class WeekCalendarEventPresenter : AvaloniaButton
{
    public const string PastPseudoClass = ":past";
    public const string SmallPseudoClass = ":small";
    public const string MediumPseudoClass = ":medium";
    public const string LargePseudoClass = ":large";
    public const string NarrowPseudoClass = ":narrow";
    public const string SelectedPseudoClass = ":selected";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>0 at rest, 1 under the pointer or keyboard focus (the theme animates it in 160ms).</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarEventPresenter, double>(nameof(HoverAmount));

    /// <summary>The resize grip's visibility: 1 under the pointer, selected or dragged (the theme fades it in 160ms).</summary>
    internal static readonly StyledProperty<double> GripAmountProperty =
        AvaloniaProperty.Register<WeekCalendarEventPresenter, double>(nameof(GripAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _title = new();
    private readonly WeekCalendarText _time = new();
    private readonly WeekCalendarText _location = new();
    private IBrush? _fill;
    private WeekCalendarBrushes? _brushes;
    private int _paintVersion = -1;
    private IPen? _pinPen;
    private bool _isPast;
    private bool _isSelected;
    private bool _isDragging;
    private double? _previewStart;
    private double? _previewEnd;

    static WeekCalendarEventPresenter()
    {
        AffectsRender<WeekCalendarEventPresenter>(HoverAmountProperty, GripAmountProperty, IsFocusedProperty);
        ClipToBoundsProperty.OverrideDefaultValue<WeekCalendarEventPresenter>(false);
    }

    public WeekCalendarEventPresenter(WeekCalendar owner)
    {
        _owner = owner;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarEventPresenter);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    internal double GripAmount { get => GetValue(GripAmountProperty); set => SetValue(GripAmountProperty, value); }

    /// <summary>True for the calendar's selected event (<c>:selected</c>).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }
            _isSelected = value;
            PseudoClasses.Set(SelectedPseudoClass, value);
            _fill = null;
            InvalidateVisual();
        }
    }

    /// <summary>True while the block is lifted by a drag (<c>:dragging</c>).</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value)
            {
                return;
            }
            _isDragging = value;
            PseudoClasses.Set(DraggingPseudoClass, value);
            _fill = null;
            InvalidateVisual();
        }
    }

    /// <summary>The times the block shows while a drag moves or resizes it (minutes of the day), or null.</summary>
    public (double Start, double End)? Preview => _previewStart is { } s && _previewEnd is { } e ? (s, e) : null;

    /// <summary>Shows <paramref name="start"/> to <paramref name="end"/> as the block's time while it is dragged (null: its own).</summary>
    public void SetPreview(double? start, double? end)
    {
        if (_previewStart == start && _previewEnd == end)
        {
            return;
        }
        _previewStart = start;
        _previewEnd = end;
        InvalidateVisual();
    }

    /// <summary>True when the block can be dragged: editing is on and the event starts on this day.</summary>
    public bool CanMove => _owner.IsEditable && Segment is { } s && !s.ContinuesBefore;

    /// <summary>True when its bottom edge can be dragged: editing is on and the event ends on this day, inside the hours shown.</summary>
    public bool CanResize => _owner.IsEditable && Segment is { } s && !s.ContinuesAfter && Math.Abs(EndMinute - s.EndMinute) < 0.01;

    /// <summary>True when the pointer at <paramref name="y"/> (block coordinates) is on the resize handle.</summary>
    public bool IsOnResizeHandle(double y) =>
        y >= Bounds.Height - WeekCalendarEditMetrics.ResizeHandleInside && y <= Bounds.Height + WeekCalendarEditMetrics.ResizeHandleOutside;

    /// <summary>The segment shown (the event's part on one day).</summary>
    public WeekCalendarSegment? Segment { get; private set; }

    /// <summary>The event.</summary>
    public WeekCalendarEvent? Event => Segment?.Event;

    /// <summary>The day column the block is in.</summary>
    public DateOnly Day => Segment?.Day ?? default;

    /// <summary>The first minute drawn (clipped to the hours shown).</summary>
    public double StartMinute { get; private set; }

    /// <summary>The minute the block ends at (clipped to the hours shown).</summary>
    public double EndMinute { get; private set; }

    /// <summary>Where the pane puts the block in its day column (fractions of the column).</summary>
    public WeekCalendarColumnSlot Slot { get; set; }

    /// <summary>True once the event has ended.</summary>
    public bool IsPast => _isPast;

    /// <summary>The detail level from the last measure.</summary>
    public WeekCalendarEventLevel Level { get; private set; } = WeekCalendarEventLevel.Large;

    /// <summary>True when the time (range) is drawn.</summary>
    public bool ShowsTime { get; private set; }

    /// <summary>True when the place is drawn.</summary>
    public bool ShowsLocation { get; private set; }

    /// <summary>True when the attendees are drawn.</summary>
    public bool ShowsAvatars { get; private set; }

    /// <summary>Title lines allowed at this size.</summary>
    public int TitleLines { get; private set; } = 1;

    /// <summary>Shows <paramref name="segment"/> from <paramref name="start"/> to <paramref name="end"/> (minutes of its day).</summary>
    public void Bind(WeekCalendarSegment segment, double start, double end)
    {
        Segment = segment;
        StartMinute = start;
        EndMinute = end;
        _brushes = null;
        _previewStart = null;
        _previewEnd = null;
        UpdateName();
        UpdatePast(_owner.CurrentNow);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Re-reads the clock: an event is past once its end is before now.</summary>
    public void UpdatePast(DateTimeOffset now)
    {
        var past = Segment is { } s && s.EndInstant <= now;
        if (past == _isPast)
        {
            return;
        }
        _isPast = past;
        PseudoClasses.Set(PastPseudoClass, past);
        _fill = null;
        InvalidateVisual();
    }

    /// <summary>The theme changed: drop cached brushes and text.</summary>
    public void OnThemeChanged()
    {
        _brushes = null;
        _fill = null;
        _title.Reset();
        _time.Reset();
        _location.Reset();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateName()
    {
        if (Segment is not { } segment)
        {
            return;
        }
        var e = segment.Event;
        if (!string.IsNullOrWhiteSpace(e.AutomationName))
        {
            AutomationProperties.SetName(this, e.AutomationName);
            return;
        }
        var culture = _owner.EffectiveCulture;
        var labels = _owner.Labels;
        var title = string.IsNullOrWhiteSpace(e.Title) ? labels.Untitled : e.Title;
        var day = WeekCalendarTime.DayName(DateOnly.FromDateTime(segment.WallStart), culture);
        var start = WeekCalendarTime.FullTime(segment.WallStart.TimeOfDay.TotalMinutes, culture);
        var end = WeekCalendarTime.FullTime(segment.WallEnd.TimeOfDay.TotalMinutes, culture);
        AutomationProperties.SetName(this, labels.TimedEvent(title, day, start, end, _owner.CalendarOf(e)?.Name, e.Location));
    }

    /// <summary>The name read by screen readers (tests).</summary>
    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HoverAmountProperty)
        {
            _fill = null;
        }
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Event is { } e)
        {
            _owner.InvokeEvent(e, this);
        }
    }

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // The bottom edge resizes (Arc: ns-resize over a 10px handle); not while dragging or read only.
        var resize = CanResize && !_isDragging && e.Pointer.Type != Avalonia.Input.PointerType.Touch &&
                     IsOnResizeHandle(e.GetPosition(this).Y);
        if (resize == _resizeCursorShown)
        {
            return;
        }
        _resizeCursorShown = resize;
        if (resize)
        {
            Cursor = s_resizeCursor ??= new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeNorthSouth);
        }
        else
        {
            ClearValue(CursorProperty);
        }
    }

    private static Avalonia.Input.Cursor? s_resizeCursor;
    private bool _resizeCursorShown;

    // Size and detail -----------------------------------------------------------------------------------------------------

    /// <summary>The detail rules of §3.6 for a block of <paramref name="size"/> (the outer slot; the body is 5 narrower).</summary>
    public static (WeekCalendarEventLevel Level, bool Time, bool Location, bool Avatars, int TitleLines) DetailFor(Size size,
        bool hasLocation, bool hasAttendees)
    {
        var bodyWidth = size.Width - WeekCalendarMetrics.EventInsetLeft - WeekCalendarMetrics.EventInsetRight;
        var height = size.Height;
        var level = height < WeekCalendarMetrics.SmallMaxHeight
            ? WeekCalendarEventLevel.Small
            : height < WeekCalendarMetrics.MediumMaxHeight ? WeekCalendarEventLevel.Medium : WeekCalendarEventLevel.Large;
        var time = level == WeekCalendarEventLevel.Small
            ? bodyWidth >= WeekCalendarMetrics.SmallTimeMinWidth
            : bodyWidth >= WeekCalendarMetrics.TimeMinWidth;
        var location = level == WeekCalendarEventLevel.Large && hasLocation && height >= WeekCalendarMetrics.LocationMinHeight &&
                       bodyWidth >= WeekCalendarMetrics.TimeMinWidth;
        var avatars = level == WeekCalendarEventLevel.Large && hasAttendees && height >= WeekCalendarMetrics.AvatarsMinHeight &&
                      bodyWidth >= WeekCalendarMetrics.AvatarsMinWidth;
        var lines = level switch
        {
            WeekCalendarEventLevel.Small => 1,
            WeekCalendarEventLevel.Medium => time ? 1 : 2,
            _ => 3,
        };
        return (level, time, location, avatars, lines);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
        var e = Event;
        var (level, time, location, avatars, lines) = DetailFor(size, !string.IsNullOrWhiteSpace(e?.Location),
            e?.Attendees is { Count: > 0 });
        Level = level;
        ShowsTime = time;
        ShowsLocation = location;
        ShowsAvatars = avatars;
        TitleLines = lines;
        // Pseudo-classes only; the theme sets nothing that changes the size, so this cannot loop.
        PseudoClasses.Set(SmallPseudoClass, level == WeekCalendarEventLevel.Small);
        PseudoClasses.Set(MediumPseudoClass, level == WeekCalendarEventLevel.Medium);
        PseudoClasses.Set(LargePseudoClass, level == WeekCalendarEventLevel.Large);
        PseudoClasses.Set(NarrowPseudoClass, !time);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    // Drawing ---------------------------------------------------------------------------------------------------------------

    private WeekCalendarBrushes Brushes()
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        if (_brushes is null || _paintVersion != paint.Version)
        {
            _brushes = paint.For(Event?.CalendarId);
            _paintVersion = paint.Version;
            _fill = null;
            _pinPen = null;
        }
        return _brushes;
    }

    private IBrush FillBrush(WeekCalendarBrushes brushes)
    {
        if (_fill is not null)
        {
            return _fill;
        }
        if (_isSelected || _isDragging)
        {
            _fill = brushes.SelectedFill;
        }
        else if (_isPast)
        {
            _fill = brushes.PastFill;
        }
        else
        {
            var hover = Math.Clamp(HoverAmount, 0, 1);
            _fill = hover <= 0.001
                ? brushes.Fill
                : hover >= 0.999
                    ? brushes.HoverFill
                    : new ImmutableSolidColorBrush(Oklch.MixOklab(brushes.Colors.Fill, brushes.Colors.HoverFill, hover));
        }
        return _fill;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || Event is not { } e)
        {
            return;
        }
        var paint = _owner.Paint;
        var brushes = Brushes();
        var body = new Rect(WeekCalendarMetrics.EventInsetLeft, 0,
            Math.Max(0, size.Width - WeekCalendarMetrics.EventInsetLeft - WeekCalendarMetrics.EventInsetRight), size.Height);
        // The whole slot is the hit target.
        context.FillRectangle(Avalonia.Media.Brushes.Transparent, new Rect(size));
        const double radius = WeekCalendarMetrics.EventRadius;
        var lifted = _isSelected || _isDragging;
        var past = _isPast && !lifted;
        // A 1px surface ring keeps touching blocks apart (Arc's 0 0 0 1px surface shadow); the selected block is raised,
        // the dragged one floats.
        var shadow = _isDragging ? paint.ShadowFloating : _isSelected ? paint.ShadowRaised : default;
        context.DrawRectangle(paint.Surface, null, new RoundedRect(body.Inflate(1), radius + 1), shadow);
        context.DrawRectangle(FillBrush(brushes), null, new RoundedRect(body, radius));
        var text = lifted ? brushes.SelectedText : past ? brushes.PastText : brushes.Text;
        var sub = lifted ? brushes.SelectedSubText : past ? brushes.PastSubText : brushes.SubText;
        using (context.PushClip(new RoundedRect(body, radius)))
        {
            context.FillRectangle(past ? brushes.PastBar : brushes.Bar,
                new Rect(body.X, body.Y, WeekCalendarMetrics.EventBarWidth, body.Height));
            DrawContent(context, e, body, text, sub, brushes);
            var grip = GripAmount * (CanResize ? 1 : 0);
            if (grip > 0.001)
            {
                // Arc's handle mark: 20 × 3, radius 2, the text color at 35%, 2px above the bottom.
                using (context.PushOpacity(0.35 * grip))
                {
                    context.DrawRectangle(text, null, new RoundedRect(new Rect(body.Center.X - 10, body.Bottom - 5, 20, 3), 2));
                }
            }
        }
        if (Classes.Contains(":focus-visible"))
        {
            // Arc's ring: 2px, 2px outside the button.
            context.DrawRectangle(null, paint.FocusRing, new RoundedRect(new Rect(size).Inflate(3), radius + 3));
        }
    }

    private void DrawContent(DrawingContext context, WeekCalendarEvent e, Rect body, IBrush text, IBrush sub, WeekCalendarBrushes brushes)
    {
        var paint = _owner.Paint;
        var culture = _owner.EffectiveCulture;
        var title = string.IsNullOrWhiteSpace(e.Title) ? _owner.Labels.Untitled : e.Title;
        var left = body.X + WeekCalendarMetrics.EventPadLeft;
        var room = Math.Max(0, body.Right - WeekCalendarMetrics.EventPadRight - left);
        if (Level == WeekCalendarEventLevel.Small)
        {
            var titleLayout = _title.Get(title, paint.BodyMedium, 12, text, double.PositiveInfinity, 1, 13.2);
            var timeText = ShowsTime ? WeekCalendarTime.ShortTime(_previewStart ?? Segment!.WallStart.TimeOfDay.TotalMinutes, culture) : null;
            var timeLayout = timeText is null ? null : _time.Get(timeText, paint.Body, 12, sub, double.PositiveInfinity, 1, 13.2);
            var titleWidth = WeekCalendarText.NaturalWidth(titleLayout);
            var timeWidth = WeekCalendarText.NaturalWidth(timeLayout);
            const double gap = 6;
            var needed = titleWidth + (timeLayout is null ? 0 : gap + timeWidth);
            if (needed > room)
            {
                // Both shrink in proportion to their own width (flex-shrink), as Arc's row does.
                var share = timeLayout is null ? room : Math.Max(0, room - gap);
                var total = titleWidth + timeWidth;
                titleWidth = total > 0 ? share * titleWidth / total : 0;
                timeWidth = total > 0 ? share * timeWidth / total : 0;
                titleLayout = _title.Get(title, paint.BodyMedium, 12, text, titleWidth, 1, 13.2);
                timeLayout = timeText is null ? null : _time.Get(timeText, paint.Body, 12, sub, timeWidth, 1, 13.2);
            }
            var y = body.Y + (body.Height - 13.2) / 2;
            titleLayout?.Draw(context, new Point(left, y));
            timeLayout?.Draw(context, new Point(left + titleWidth + gap, y));
            return;
        }
        var top = body.Y + WeekCalendarMetrics.EventPadTop;
        var titleBlock = _title.Get(title, paint.BodyMedium, 12, text, room, TitleLines, 15.6);
        titleBlock?.Draw(context, new Point(left, top));
        top += (titleBlock?.Height ?? 0) + 1;
        if (ShowsTime && Segment is { } segment)
        {
            var range = WeekCalendarTime.TimeRange(_previewStart ?? segment.StartMinute, _previewEnd ?? segment.EndMinute, culture);
            var timeLayout = _time.Get(range, paint.Body, 12, sub, room, 1, 15.6);
            timeLayout?.Draw(context, new Point(left, top));
            top += timeLayout?.Height ?? 0;
        }
        if (ShowsLocation && !string.IsNullOrWhiteSpace(e.Location))
        {
            top += 2;
            const double pin = 11;
            _pinPen ??= new Pen(sub, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var pinTop = top + (15.6 - pin) / 2;
            using (context.PushTransform(Matrix.CreateScale(pin / 24, pin / 24) * Matrix.CreateTranslation(left, pinTop)))
            {
                context.DrawGeometry(null, _pinPen, WeekCalendarGlyphs.MapPin);
            }
            var placeLayout = _location.Get(e.Location, paint.Body, 12, sub, Math.Max(0, room - pin - 4), 1, 15.6);
            placeLayout?.Draw(context, new Point(left + pin + 4, top));
        }
        if (ShowsAvatars && e.Attendees is { Count: > 0 } people)
        {
            const double d = WeekCalendarMetrics.EventAvatarSize;
            var y = body.Bottom - WeekCalendarMetrics.EventPadBottom - d / 2;
            var count = Math.Min(people.Count, WeekCalendarMetrics.EventAvatarMax);
            for (var i = 0; i < count; i++)
            {
                var center = new Point(left + d / 2 + i * (d - 5), y);
                // The ring takes the event's (not past) fill, as Arc's 2px avatar border does.
                context.DrawEllipse(brushes.Fill, null, center, d / 2 + 2, d / 2 + 2);
                paint.Avatars.Draw(context, _owner, people[i], center, d / 2, 0);
            }
        }
    }
}
