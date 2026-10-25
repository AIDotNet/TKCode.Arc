using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A small self-drawn glyph button: the row chevron (turns a quarter on a snappy spring) and the header's expand-all
/// toggle (the glyph swaps with a scale and a quarter turn). Hover fills a rounded square (mouse only); the chevron
/// has no tab stop because its row owns focus.
/// </summary>
internal sealed class TreeTableGlyphButton : Control
{
    private const double GlyphExtent = 16;
    private const double GlyphStroke = 1.75;

    private readonly TreeTable _owner;
    private readonly double _extent;
    private readonly double _radius;
    private MotionValue<double>? _angle;
    private MotionValue<double>? _swap;
    private Geometry? _glyph;
    private Geometry? _previous;
    private double _swapDirection;
    private double _staticAngle;
    private bool _hover;
    private bool _pressed;
    private bool _handlesKeys;
    private IBrush? _foreground;

    public TreeTableGlyphButton(TreeTable owner, double extent, double radius, bool isTabStop)
    {
        _owner = owner;
        _extent = extent;
        _radius = radius;
        _handlesKeys = isTabStop;
        Focusable = isTabStop;
        IsTabStop = isTabStop;
        Cursor = new Cursor(StandardCursorType.Arrow);
        Width = extent;
        Height = extent;
        FocusAdorner = null;
    }

    /// <summary>Raised on a click or on Enter / Space.</summary>
    public event Action? Clicked;

    /// <summary>The glyph's brush; null uses the table's muted text.</summary>
    public IBrush? GlyphBrush
    {
        get => _foreground;
        set
        {
            _foreground = value;
            InvalidateVisual();
        }
    }

    public void SetGlyph(Geometry glyph, bool animate, double direction = 1)
    {
        if (ReferenceEquals(_glyph, glyph))
        {
            return;
        }
        _previous = _glyph;
        _glyph = glyph;
        _swapDirection = direction;
        if (_previous is null || !animate || !this.IsAttachedToVisualTree())
        {
            _previous = null;
            _swap?.Jump(1);
            InvalidateVisual();
            return;
        }
        _swap ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _swap.Changed += _ => InvalidateVisual();
        _swap.Jump(0);
        var spec = ArcMotion.IsFull(this)
            ? MotionSpec.Spring(ArcSpring.Snappy)
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = _swap.AnimateTo(1, spec);
    }

    public void SetAngle(double degrees, bool animate)
    {
        if (!animate || !ArcMotion.IsFull(this) || !this.IsAttachedToVisualTree())
        {
            _angle?.Jump(degrees);
            _staticAngle = degrees;
            InvalidateVisual();
            return;
        }
        if (_angle is null)
        {
            _angle = new MotionValue<double>(this, _staticAngle, MotionVectors.Double);
            _angle.Changed += _ => InvalidateVisual();
        }
        _staticAngle = degrees;
        _ = _angle.AnimateTo(degrees, MotionSpec.Spring(ArcSpring.Snappy));
    }

    /// <summary>The angle drawn right now (tests).</summary>
    internal double CurrentAngle => _angle?.Value ?? _staticAngle;

    protected override Size MeasureOverride(Size availableSize) => new(_extent, _extent);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, _extent, _extent);
        // Transparent fill keeps the whole square hit-testable.
        context.DrawRectangle(Brushes.Transparent, null, bounds);
        var palette = _owner.Palette;
        if (_hover && palette.Foreground is { } foreground)
        {
            using (context.PushOpacity(0.08))
            {
                context.DrawRectangle(foreground, null, bounds, _radius, _radius);
            }
        }
        var brush = _foreground ?? palette.TextMuted;
        if (brush is not null)
        {
            var pen = new Pen(brush, GlyphStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var press = _pressed && ArcMotion.IsFull(this) ? 0.94 : 1;
            var swap = _swap?.Value ?? 1;
            if (_previous is not null && swap < 1)
            {
                using (context.PushOpacity(Math.Clamp(1 - swap * 2, 0, 1)))
                {
                    DrawGlyph(context, _previous, pen, 1 - 0.3 * swap, -_swapDirection * 90 * swap, press);
                }
                using (context.PushOpacity(Math.Clamp(swap, 0, 1)))
                {
                    DrawGlyph(context, _glyph, pen, 0.7 + 0.3 * swap, _swapDirection * 90 * (1 - swap), press);
                }
            }
            else
            {
                DrawGlyph(context, _glyph, pen, 1, _angle?.Value ?? _staticAngle, press);
            }
        }
        if (Classes.Contains(":focus-visible") && palette.FocusRing is { } ring)
        {
            context.DrawRectangle(null, ring, bounds.Inflate(2), _radius + 2, _radius + 2);
        }
    }

    private void DrawGlyph(DrawingContext context, Geometry? glyph, IPen pen, double scale, double angle, double press)
    {
        if (glyph is null)
        {
            return;
        }
        var center = new Point(_extent / 2, _extent / 2);
        var fit = GlyphExtent / 24 * scale * press;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateRotation(angle * Math.PI / 180) *
                     Matrix.CreateScale(fit, fit) * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        {
            // The pen thickness is in glyph units (a 24 grid), so it scales with the glyph like Lucide's strokeWidth.
            context.DrawGeometry(null, pen, glyph);
        }
    }

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
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            // The row (or the header) must not also see this press as a row click.
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var wasPressed = _pressed;
        _pressed = false;
        InvalidateVisual();
        if (wasPressed && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Clicked?.Invoke();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_handlesKeys && e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Clicked?.Invoke();
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
}
