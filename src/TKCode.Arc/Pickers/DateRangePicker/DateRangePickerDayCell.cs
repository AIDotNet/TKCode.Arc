using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Resolved colors and fonts for drawing range months (shared by every pane of one picker).</summary>
internal sealed class DateRangePickerPalette
{
    public int Version { get; set; }
    public IBrush Accent { get; set; } = Brushes.Transparent;
    public IBrush AccentSubtle { get; set; } = Brushes.Transparent;
    public Color AccentForeground { get; set; } = Colors.White;
    public Color Foreground { get; set; } = Colors.Black;
    public Color Muted { get; set; } = Colors.Gray;
    public IBrush HoverFill { get; set; } = Brushes.Transparent;
    public FontFamily FontFamily { get; set; } = FontFamily.Default;
    public double FontSize { get; set; } = 14;
    public double SmallFontSize { get; set; } = 12;
    public FontWeight RegularWeight { get; set; } = FontWeight.Normal;
    public FontWeight MediumWeight { get; set; } = FontWeight.Medium;
}

/// <summary>
/// One day of a range month, drawn directly: an inner disc (the cell minus 3px) that tints on hover or keyboard focus
/// and shrinks to 0.9 while pressed, the number (accent-foreground on the range ends, flipping 40ms after the thumb
/// arrives), today's dot, and the disabled fade. The range band and the solid ends are drawn by the pane underneath.
/// </summary>
internal sealed class DateRangePickerDayCell : Control
{
    private readonly DateRangePickerMonthPane _pane;
    private FormattedText? _text;
    private int _textDay;
    private bool _textEdge;
    private int _textVersion = -1;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _press;
    private MotionValue<double>? _edge;
    private ImmutableSolidColorBrush? _brush;
    private ImmutableSolidColorBrush? _dot;
    private bool _pressed;

    public DateRangePickerDayCell(DateRangePickerMonthPane pane)
    {
        _pane = pane;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public DateOnly Date { get; private set; }
    public bool IsDisabled { get; private set; }
    public bool IsToday { get; private set; }
    public bool IsEdge { get; private set; }
    public bool IsInRange { get; private set; }

    public void Configure(DateOnly date, bool disabled, bool today, bool edge, bool inRange, bool tabStop, CultureInfo culture, bool animate)
    {
        var dateChanged = date != Date;
        Date = date;
        IsDisabled = disabled;
        IsToday = today;
        IsInRange = inRange;
        IsTabStop = tabStop;
        Cursor = disabled ? Cursor.Default : new Cursor(StandardCursorType.Hand);
        if (dateChanged)
        {
            AutomationProperties.SetName(this, date.ToDateTime(TimeOnly.MinValue).ToString(culture.DateTimeFormat.LongDatePattern, culture));
            _hover?.Jump(0);
            _press?.Jump(1);
        }
        if (edge != IsEdge || dateChanged)
        {
            IsEdge = edge;
            _edge ??= Create(edge ? 1 : 0);
            if (animate && !dateChanged && IsLoaded && ArcMotion.IsFull(this))
            {
                // The number flips just after the end thumb glides in (Arc: transition-delay 40ms).
                _ = _edge.AnimateTo(edge ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, edge ? TimeSpan.FromMilliseconds(40) : TimeSpan.Zero));
            }
            else
            {
                _edge.Jump(edge ? 1 : 0);
            }
        }
        AutomationProperties.SetItemStatus(this, today ? "today" : string.Empty);
        IsEnabled = !disabled;
        InvalidateVisual();
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsDisabled || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        Hover(1);
        _pane.OnDayHovered(this);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hover(0);
        if (_pressed)
        {
            _pressed = false;
            Press(false);
        }
    }

    private void Hover(double target)
    {
        _hover ??= Create(0);
        _ = _hover.AnimateTo(target, ArcMotion.IsFull(this)
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsDisabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        Press(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        Press(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _pane.OnDayPicked(this);
        }
    }

    private void Press(bool pressed)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _press ??= Create(1);
        _ = _press.AnimateTo(pressed ? 0.9 : 1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space && !IsDisabled)
        {
            e.Handled = true;
            _pane.OnDayPicked(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _pane.OnDayFocused(this);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DayPeer(this);

    public override void Render(DrawingContext context)
    {
        var palette = _pane.Palette;
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var inner = Math.Max(0, Math.Min(size.Width, size.Height) / 2 - 3);
        var scale = _press?.Value ?? 1;
        var edge = Math.Clamp(_edge?.Value ?? 0, 0, 1);
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        var tint = IsEdge || IsDisabled ? 0 : Math.Max(_hover?.Value ?? 0, focusVisible ? 1 : 0);
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            if (tint > 0.001)
            {
                using (context.PushOpacity(tint))
                {
                    context.DrawEllipse(palette.HoverFill, null, center, inner, inner);
                }
            }
        }
        EnsureText(palette);
        if (_text is null)
        {
            return;
        }
        var rest = IsDisabled ? palette.Muted : palette.Foreground;
        var color = PickerDrawing.Mix(rest, palette.AccentForeground, edge);
        _text.SetForegroundBrush(PickerDrawing.Brush(ref _brush, color));
        using (context.PushOpacity(IsDisabled ? 0.5 : 1))
        {
            context.DrawText(_text, new Point(center.X - _text.Width / 2, center.Y - _text.Height / 2));
            if (IsToday)
            {
                // A 4px dot 5px above the bottom: accent, or accent-foreground on a range end.
                var dot = PickerDrawing.Mix(PickerDrawing.ColorOf(palette.Accent), palette.AccentForeground, edge);
                context.DrawEllipse(PickerDrawing.Brush(ref _dot, dot), null, new Point(center.X, size.Height - 5 - 2), 2, 2);
            }
        }
    }

    private void EnsureText(DateRangePickerPalette palette)
    {
        if (_text is not null && _textDay == Date.Day && _textEdge == IsEdge && _textVersion == palette.Version)
        {
            return;
        }
        _textDay = Date.Day;
        _textEdge = IsEdge;
        _textVersion = palette.Version;
        var typeface = new Typeface(palette.FontFamily, FontStyle.Normal, IsEdge ? palette.MediumWeight : palette.RegularWeight);
        _text = new FormattedText(Date.Day.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, palette.FontSize, null);
    }

    /// <summary>Grid cell peer: a button whose selection state reports "in range" (Arc: aria-selected).</summary>
    private sealed class DayPeer : ControlAutomationPeer, ISelectionItemProvider, IInvokeProvider
    {
        public DayPeer(DateRangePickerDayCell owner) : base(owner)
        {
        }

        private DateRangePickerDayCell Cell => (DateRangePickerDayCell)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public bool IsSelected => Cell.IsInRange;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select() => Cell._pane.OnDayPicked(Cell);

        public void Invoke() => Cell._pane.OnDayPicked(Cell);
    }
}
