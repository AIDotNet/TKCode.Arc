using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A pressable surface of the screen shell (sidebar rows, header icon buttons, list rows): a rounded fill that follows
/// hover, press and an active state, a keyboard focus ring, Enter / Space and clicks. Hover fills change at once (Arc's
/// sidebar hover is instant); touch pointers do not hover.
/// </summary>
internal class ScreenPressable : Decorator
{
    private bool _hover;
    private bool _pressed;
    private bool _active;
    private IBrush? _fill;
    private IBrush? _hoverFill;
    private IBrush? _activeFill;
    private IPen? _borderPen;
    private IPen? _focusPen;
    private double _radius = 8;

    public ScreenPressable()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = false;
    }

    /// <summary>Raised on a click, Enter or Space.</summary>
    public event Action<ScreenPressable>? Clicked;

    /// <summary>Raised on a double click.</summary>
    public event Action<ScreenPressable>? DoubleClicked;

    public IBrush? Fill { get => _fill; set { _fill = value; InvalidateVisual(); } }

    public IBrush? HoverFill { get => _hoverFill; set { _hoverFill = value; InvalidateVisual(); } }

    public IBrush? ActiveFill { get => _activeFill; set { _activeFill = value; InvalidateVisual(); } }

    public IPen? BorderPen { get => _borderPen; set { _borderPen = value; InvalidateVisual(); } }

    public IPen? FocusPen { get => _focusPen; set { _focusPen = value; InvalidateVisual(); } }

    public double Radius { get => _radius; set { _radius = value; InvalidateVisual(); } }

    /// <summary>Shows the active fill (a current page, a pressed toggle, an open event).</summary>
    public bool IsActive
    {
        get => _active;
        set
        {
            if (_active != value)
            {
                _active = value;
                InvalidateVisual();
            }
        }
    }

    public bool IsHovered => _hover;

    /// <summary>Whether a pointer hover is drawn (rows that show their own hover state turn it off).</summary>
    public bool ShowsHover { get; set; } = true;

    public void RaiseClicked() => Clicked?.Invoke(this);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        _pressed = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2 && DoubleClicked is not null)
        {
            DoubleClicked(this);
            e.Handled = true;
            return;
        }
        _pressed = true;
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        InvalidateVisual();
        var p = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(p))
        {
            e.Handled = true;
            Clicked?.Invoke(this);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && (e.Key == Key.Enter || e.Key == Key.Space) && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Clicked?.Invoke(this);
        }
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

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var fill = _active && _activeFill is not null ? _activeFill
            : (_hover || _pressed) && ShowsHover && _hoverFill is not null ? _hoverFill
            : _fill;
        if (fill is not null || _borderPen is not null)
        {
            var inset = _borderPen is null ? rect : rect.Deflate(0.5);
            context.DrawRectangle(fill ?? Brushes.Transparent, _borderPen, inset, _radius, _radius);
        }
        else
        {
            context.FillRectangle(Brushes.Transparent, rect);
        }
        if (_focusPen is not null && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, _focusPen, rect.Inflate(1), _radius + 1, _radius + 1);
        }
    }
}

/// <summary>A key cap ("⌘K", "Esc", "Arrow keys"): 11/500 text on a raised rounded box with a 1px bottom shadow.</summary>
internal sealed class ScreenKbd : Control
{
    private TextLayout? _layout;
    private readonly string _text;
    private ScreenPaint? _paint;

    public ScreenKbd(string text)
    {
        _text = text;
        IsHitTestVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }


    private void Refresh()
    {
        _paint = ScreenPaint.For(this);
        _layout?.Dispose();
        var face = new Typeface(ScreenText.Body(this), FontStyle.Normal, FontWeight.Medium);
        _layout = new TextLayout(_text, face, 11, _paint.SecondaryBrush);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = _layout is null ? 18 : Math.Max(18, _layout.WidthIncludingTrailingWhitespace + 8);
        return new Size(w, 19);
    }

    public override void Render(DrawingContext context)
    {
        if (_paint is null || _layout is null)
        {
            return;
        }
        var box = new Rect(0, 0, Bounds.Width, 18);
        var shadow = _paint.IsDark ? new ImmutableSolidColorBrush(Color.FromArgb(128, 0, 0, 0)) : _paint.KbdBorder;
        context.DrawRectangle(shadow, null, box.Translate(new Vector(0, 1)), 5, 5);
        context.DrawRectangle(_paint.IsDark ? _paint.RaisedBrush : _paint.SurfaceBrush, new ImmutablePen(_paint.KbdBorder, 1), box.Deflate(0.5), 5, 5);
        _layout.Draw(context, new Point((Bounds.Width - _layout.WidthIncludingTrailingWhitespace) / 2, (18 - _layout.Height) / 2));
    }
}

/// <summary>Text helpers: the screen's fonts and sizes (Inter 13/18.2 body, Geist titles).</summary>
internal static class ScreenText
{
    public static FontFamily Body(Control owner) =>
        owner.TryFindResource(ArcDesignTokenKind.BodyFontFamily, out var v) && v is FontFamily f ? f : FontFamily.Default;

    public static FontFamily Display(Control owner) =>
        owner.TryFindResource(ArcDesignTokenKind.DisplayFontFamily, out var v) && v is FontFamily f ? f : FontFamily.Default;

    /// <summary>A single-line text with an ellipsis.</summary>
    public static TextBlock Line(string? text, double size, IBrush? brush, FontWeight weight = FontWeight.Normal, double lineHeight = 0)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = brush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            LetterSpacing = -0.01 * size,
        };
        if (lineHeight > 0)
        {
            block.LineHeight = lineHeight;
        }
        return block;
    }

    /// <summary>A wrapping paragraph.</summary>
    public static TextBlock Para(string? text, double size, IBrush? brush, double lineHeight)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = lineHeight,
            LetterSpacing = -0.01 * size,
        };
    }

    /// <summary>A Geist title.</summary>
    public static TextBlock Title(Control owner, string? text, double size, double lineHeight, double tracking, IBrush? brush)
    {
        var block = Line(text, size, brush, FontWeight.Medium, lineHeight);
        block.FontFamily = Display(owner);
        block.LetterSpacing = tracking;
        return block;
    }
}

/// <summary>Builders for the shell's small buttons.</summary>
internal static class ScreenButtons
{
    /// <summary>A 32 × 32 icon button (no fill at rest, hover fill, optional pressed/active fill).</summary>
    public static ScreenPressable Icon(ScreenPaint paint, Geometry glyph, string name, Action onClick, double glyphSize = 16,
                                       IBrush? foreground = null, bool toggle = false, double size = 32)
    {
        var button = new ScreenPressable
        {
            Width = size,
            Height = size,
            Radius = 8,
            HoverFill = paint.HoverFill,
            ActiveFill = toggle ? paint.ActiveFill : null,
            FocusPen = paint.FocusPen,
            Child = new ArcGlyph
            {
                Data = glyph,
                Size = glyphSize,
                StrokeThickness = 1.75,
                Foreground = foreground ?? paint.SecondaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, null);
        button.Clicked += _ => onClick();
        return button;
    }

    /// <summary>
    /// A 32 high text button of the header: outline (raised fill, 1px Border, r8, padding 0 12) or primary (foreground fill,
    /// background text). An optional leading glyph.
    /// </summary>
    public static ScreenPressable Text(ScreenPaint paint, string text, Action onClick, bool primary = false, Geometry? glyph = null,
                                       bool iconOnly = false)
    {
        var fg = primary ? paint.InverseText : paint.ForegroundBrush;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (glyph is not null)
        {
            row.Children.Add(new ArcGlyph { Data = glyph, Size = 16, StrokeThickness = 1.75, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
        }
        if (!iconOnly)
        {
            row.Children.Add(ScreenText.Line(text, 13, fg));
        }
        var button = new ScreenPressable
        {
            Height = 32,
            MinWidth = 32,
            Radius = 8,
            Padding = iconOnly ? new Thickness(0) : new Thickness(12, 0),
            Fill = primary ? paint.InverseBrush : (paint.IsDark ? paint.RaisedBrush : paint.SurfaceBrush),
            HoverFill = primary ? new ImmutableSolidColorBrush(Oklch.MixOklab(paint.Foreground, paint.Surface, 0.12)) : paint.ButtonHover,
            BorderPen = primary ? null : paint.BorderPen,
            FocusPen = paint.FocusPen,
            Child = row,
        };
        if (iconOnly)
        {
            button.Width = 32;
        }
        AutomationProperties.SetName(button, text);
        button.Clicked += _ => onClick();
        return button;
    }
}
