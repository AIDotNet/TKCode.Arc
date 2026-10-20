using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// One day of a calendar month pane. Rendered directly (no template): hover fill, press scale, the number
/// (whose color hands off to the selection color with a delay so it flips as the disc passes), today's dot
/// and the keyboard focus ring. The selected disc itself is drawn by the pane underneath.
/// </summary>
internal sealed class CalendarDayCell : Control
{
    private readonly CalendarMonthPane _pane;
    private FormattedText? _text;
    private int _textDay;
    private bool _textBold;
    private MotionValue<double>? _selectedness;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _press;
    private bool _pressed;

    public CalendarDayCell(CalendarMonthPane pane)
    {
        _pane = pane;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public DateOnly Date { get; private set; }
    public bool IsOutside { get; private set; }
    public bool IsDisabled { get; private set; }
    public bool IsToday { get; private set; }
    public bool IsSelected { get; private set; }

    /// <summary>0 = resting number color, 1 = selected number color.</summary>
    private double Selectedness => _selectedness?.Value ?? (IsSelected ? 1 : 0);

    public double HoverProgress => _hover?.Value ?? 0;

    public void Configure(DateOnly date, bool outside, bool disabled, bool today, bool selected, bool tabStop,
                          CultureInfo culture, TimeSpan? selectionDelay)
    {
        var dateChanged = date != Date;
        Date = date;
        IsOutside = outside;
        IsDisabled = disabled;
        IsToday = today;
        IsTabStop = tabStop;
        Cursor = disabled ? Cursor.Default : new Cursor(StandardCursorType.Hand);
        if (dateChanged)
        {
            AutomationProperties.SetName(this, date.ToString("D", culture));
            _selectedness?.Jump(selected ? 1 : 0);
            _hover?.Jump(0);
        }
        if (selected != IsSelected)
        {
            IsSelected = selected;
            if (selectionDelay is { } delay && !dateChanged && IsLoaded && ArcMotion.IsFull(this))
            {
                _selectedness ??= CreateValue(selected ? 0 : 1);
                _ = _selectedness.AnimateTo(selected ? 1 : 0,
                    MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
            }
            else
            {
                _selectedness?.Jump(selected ? 1 : 0);
            }
        }
        AutomationProperties.SetHelpText(this, selected ? "selected" : today ? "today" : string.Empty);
        InvalidateVisual();
    }

    private MotionValue<double> CreateValue(double initial)
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
        AnimateHover(1);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        AnimateHover(0);
        if (_pressed)
        {
            _pressed = false;
            AnimatePress(false);
        }
    }

    private void AnimateHover(double target)
    {
        _hover ??= CreateValue(0);
        var spec = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off
            ? MotionSpec.Instant
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _hover.AnimateTo(target, spec);
        if (IsSelected)
        {
            _pane.InvalidateVisual();
        }
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
        AnimatePress(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        AnimatePress(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _pane.OnDayActivated(this);
        }
    }

    private void AnimatePress(bool pressed)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _press ??= CreateValue(1);
        // Press: 100ms on the standard curve; release: Arc's CSS spring curve.
        _ = _press.AnimateTo(pressed ? 0.94 : 1, pressed
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(580), ArcEasings.CssSpring));
        if (IsSelected)
        {
            _pane.SetDiscPressed(pressed);
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

    public override void Render(DrawingContext context)
    {
        var palette = _pane.Palette;
        if (palette is null)
        {
            return;
        }
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var radius = Math.Min(size.Width, size.Height) / 2;
        var scale = _press?.Value ?? 1;
        using var transform = context.PushTransform(
            Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y));
        var hover = HoverProgress;
        if (hover > 0.001 && !IsSelected)
        {
            using (context.PushOpacity(hover))
            {
                context.DrawEllipse(palette.HoverFill, null, center, radius, radius);
            }
        }
        var bold = IsSelected || IsToday;
        EnsureText(palette, bold);
        if (_text is null)
        {
            return;
        }
        var rest = IsOutside || IsDisabled ? palette.MutedColor : palette.ForegroundColor;
        var color = Lerp(rest, palette.SelectedForegroundColor, Math.Clamp(Selectedness, 0, 1));
        var brush = new ImmutableSolidColorBrush(color);
        _text.SetForegroundBrush(brush);
        var opacity = IsDisabled ? (IsOutside ? 0.3 : 0.42) : IsOutside && !IsSelected ? 0.8 : 1;
        var origin = new Point(center.X - _text.Width / 2, center.Y - _text.Height / 2);
        using (context.PushOpacity(opacity))
        {
            context.DrawText(_text, origin);
            if (IsToday)
            {
                // A 4px dot 4px under the number's line box, in the number's color.
                var lineBottom = center.Y + palette.FontSize / 2;
                context.DrawEllipse(brush, null, new Point(center.X, lineBottom + 4 + 2), 2, 2);
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && palette.FocusRing is { } ring)
        {
            context.DrawEllipse(null, ring, center, radius + 2, radius + 2);
        }
    }

    private void EnsureText(CalendarPalette palette, bool bold)
    {
        if (_text is not null && _textDay == Date.Day && _textBold == bold && palette.Version == _textVersion)
        {
            return;
        }
        _textDay = Date.Day;
        _textBold = bold;
        _textVersion = palette.Version;
        var typeface = new Typeface(palette.FontFamily, FontStyle.Normal, bold ? palette.BoldWeight : palette.RegularWeight);
        _text = new FormattedText(Date.Day.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, palette.FontSize, null);
    }

    private int _textVersion = -1;

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}

/// <summary>Resolved colors and font for drawing day cells (shared by all panes of one calendar).</summary>
internal sealed class CalendarPalette
{
    public int Version { get; set; }
    public IBrush HoverFill { get; set; } = Brushes.Transparent;
    public Color ForegroundColor { get; set; }
    public Color MutedColor { get; set; }
    public Color SelectedForegroundColor { get; set; }
    public IBrush SelectionFill { get; set; } = Brushes.Transparent;
    public IBrush SelectionHoverFill { get; set; } = Brushes.Transparent;
    public IPen? FocusRing { get; set; }
    public FontFamily FontFamily { get; set; } = FontFamily.Default;
    public double FontSize { get; set; } = 14;
    public FontWeight RegularWeight { get; set; } = FontWeight.Normal;
    public FontWeight BoldWeight { get; set; } = FontWeight.Medium;
}
