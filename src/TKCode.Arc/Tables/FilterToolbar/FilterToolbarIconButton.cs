using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A small round icon button drawn directly (chip remove, menu back): muted glyph, a fill and full-contrast glyph on
/// hover (fast standard transition), a press scale released on Arc's CSS spring, and a focus ring.
/// </summary>
internal sealed class FilterToolbarIconButton : Control
{
    public static readonly StyledProperty<IBrush?> RestForegroundProperty =
        AvaloniaProperty.Register<FilterToolbarIconButton, IBrush?>(nameof(RestForeground));

    public static readonly StyledProperty<IBrush?> ActiveForegroundProperty =
        AvaloniaProperty.Register<FilterToolbarIconButton, IBrush?>(nameof(ActiveForeground));

    public static readonly StyledProperty<IBrush?> HoverFillProperty =
        AvaloniaProperty.Register<FilterToolbarIconButton, IBrush?>(nameof(HoverFill));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<FilterToolbarIconButton, IBrush?>(nameof(FocusRingBrush));

    private readonly Geometry _glyph;
    private readonly double _glyphSize;
    private readonly double _stroke;
    private readonly double _pressScale;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _press;
    private bool _pressed;
    private ImmutablePen? _pen;
    private Color _penColor;
    private ImmutablePen? _ringPen;
    private Color _ringColor;

    static FilterToolbarIconButton()
    {
        AffectsRender<FilterToolbarIconButton>(RestForegroundProperty, ActiveForegroundProperty, HoverFillProperty, FocusRingBrushProperty);
        FocusableProperty.OverrideDefaultValue<FilterToolbarIconButton>(true);
    }

    public FilterToolbarIconButton(Geometry glyph, double extent, double glyphSize, double stroke, double pressScale,
                                   ArcDesignTokenKind hoverFill)
    {
        _glyph = glyph;
        _glyphSize = glyphSize;
        _stroke = stroke;
        _pressScale = pressScale;
        Width = extent;
        Height = extent;
        Cursor = new Cursor(StandardCursorType.Hand);
        FocusAdorner = null;
        ArcTokens.Bind(this, RestForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, ActiveForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, HoverFillProperty, hoverFill);
        ArcTokens.Bind(this, FocusRingBrushProperty, ArcDesignTokenKind.FocusRing);
    }

    public IBrush? RestForeground { get => GetValue(RestForegroundProperty); set => SetValue(RestForegroundProperty, value); }
    public IBrush? ActiveForeground { get => GetValue(ActiveForegroundProperty); set => SetValue(ActiveForegroundProperty, value); }
    public IBrush? HoverFill { get => GetValue(HoverFillProperty); set => SetValue(HoverFillProperty, value); }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <summary>Invoked by a click, Enter or Space; the argument is true for keyboard activation.</summary>
    public Action<bool>? Clicked { get; set; }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            AnimateHover(1);
        }
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
        _hover ??= Create(0);
        _ = _hover.AnimateTo(target, ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    private void AnimatePress(bool pressed)
    {
        if (!ArcMotion.IsFull(this))
        {
            _press?.Jump(1);
            return;
        }
        _press ??= Create(1);
        _ = _press.AnimateTo(pressed ? _pressScale : 1, pressed
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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
            Clicked?.Invoke(false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Clicked?.Invoke(true);
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
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var radius = Math.Min(size.Width, size.Height) / 2;
        var hover = _hover?.Value ?? 0;
        var press = _press?.Value ?? 1;
        using var scale = press < 0.9999
            ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(press, press) *
                                    Matrix.CreateTranslation(center.X, center.Y))
            : default(DrawingContext.PushedState?);
        if (hover > 0.001 && HoverFill is { } fill)
        {
            using (context.PushOpacity(hover))
            {
                context.DrawEllipse(fill, null, center, radius, radius);
            }
        }
        var rest = (RestForeground as ISolidColorBrush)?.Color ?? Colors.Gray;
        var active = (ActiveForeground as ISolidColorBrush)?.Color ?? Colors.Black;
        var color = TablesBDrawing.Lerp(rest, active, hover);
        if (_pen is null || _penColor != color)
        {
            _penColor = color;
            _pen = new ImmutablePen(new ImmutableSolidColorBrush(color), _stroke * 24 / _glyphSize, lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round);
        }
        var factor = _glyphSize / 24;
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(factor, factor) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _pen, _glyph);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRingBrush is ISolidColorBrush ring)
        {
            if (_ringPen is null || _ringColor != ring.Color)
            {
                _ringColor = ring.Color;
                _ringPen = new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), 2);
            }
            context.DrawEllipse(null, _ringPen, center, radius + 2, radius + 2);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new IconButtonPeer(this);

    private sealed class IconButtonPeer : ControlAutomationPeer, IInvokeProvider
    {
        public IconButtonPeer(FilterToolbarIconButton owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => ((FilterToolbarIconButton)Owner).Clicked?.Invoke(true);
    }
}
