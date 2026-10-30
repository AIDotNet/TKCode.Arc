using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The press of the feedback buttons: the button scales to 0.94 at once when pressed and springs back to 1 when released
/// (<see cref="ArcSpring.Snappy"/>). Code-driven because a ControlTheme transition runs both ways on the same curve, and the
/// press is instant while the release is a spring. Reduced motion and Off drop the scale.
/// </summary>
internal sealed class FeedbackPress
{
    /// <summary>How far a press scales the button.</summary>
    internal const double PressedScale = 0.94;

    private readonly Control _owner;
    private readonly ScaleTransform _scale = new(1, 1);
    private MotionValue<double>? _value;

    public FeedbackPress(Control owner)
    {
        _owner = owner;
        owner.RenderTransformOrigin = RelativePoint.Center;
        owner.RenderTransform = _scale;
    }

    /// <summary>The scale drawn now.</summary>
    public double Scale => _scale.ScaleX;

    public void Press()
    {
        if (!ArcMotion.IsFull(_owner))
        {
            return;
        }
        Value.Jump(PressedScale);
        Apply(PressedScale);
    }

    public void Release()
    {
        if (_value is null || Math.Abs(_value.Value - 1) < 0.0001)
        {
            return;
        }
        if (!ArcMotion.IsFull(_owner))
        {
            _value.Jump(1);
            Apply(1);
            return;
        }
        _ = _value.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private MotionValue<double> Value
    {
        get
        {
            if (_value is null)
            {
                _value = new MotionValue<double>(_owner, 1, MotionVectors.Double);
                _value.Changed += Apply;
            }
            return _value;
        }
    }

    private void Apply(double scale)
    {
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }
}

/// <summary>
/// A thumb of <see cref="FeedbackActions"/>: a toggle button (it reports pressed) whose state the owner sets. A press never
/// flips <see cref="ToggleButton.IsChecked"/> itself; the owner works out the new rating and checks the thumb.
/// </summary>
internal sealed class FeedbackThumb : ToggleButton
{
    /// <summary>The 24 × 24 glyph (thumbs up or down).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<FeedbackThumb, Geometry?>(nameof(Glyph));

    private readonly FeedbackPress _press;

    public FeedbackThumb()
    {
        _press = new FeedbackPress(this);
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <summary>The press scale drawn now.</summary>
    internal double PressScale => _press.Scale;

    protected override Type StyleKeyOverride => typeof(FeedbackThumb);

    /// <summary>The owner decides the state.</summary>
    protected override void Toggle()
    {
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEffectivelyEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _press.Press();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _press.Release();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _press.Release();
    }
}

/// <summary>
/// The glyph of a thumb: the outline is always drawn, and the filled shape lies on top of it at <see cref="FillAmount"/>
/// opacity (the theme fades it over 160ms), so filling never moves or resizes anything.
/// </summary>
internal sealed class FeedbackThumbGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<FeedbackThumbGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<FeedbackThumbGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> FillAmountProperty =
        AvaloniaProperty.Register<FeedbackThumbGlyph, double>(nameof(FillAmount));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<FeedbackThumbGlyph>();

    private Pen? _pen;
    private IBrush? _penBrush;

    static FeedbackThumbGlyph()
    {
        AffectsMeasure<FeedbackThumbGlyph>(SizeProperty);
        AffectsRender<FeedbackThumbGlyph>(DataProperty, FillAmountProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FeedbackThumbGlyph>(false);
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double FillAmount { get => GetValue(FillAmountProperty); set => SetValue(FillAmountProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var x = (Bounds.Width - 24 * scale) / 2;
        var y = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(null, _pen, data);
            var fill = Math.Clamp(FillAmount, 0, 1);
            if (fill > 0.001)
            {
                using (context.PushOpacity(fill))
                {
                    context.DrawGeometry(brush, _pen, data);
                }
            }
        }
    }
}

/// <summary>
/// A reason chip: a toggle button that reports pressed. Selected chips invert to a solid fill, so the state never relies on
/// hue. Coarse pointers make it 32px tall instead of 28.
/// </summary>
internal sealed class FeedbackChip : ToggleButton
{
    internal const string CoarsePseudoClass = ":coarse";

    protected override Type StyleKeyOverride => typeof(FeedbackChip);

    /// <summary>Makes the chip taller for touch.</summary>
    internal void SetCoarse(bool coarse) => PseudoClasses.Set(CoarsePseudoClass, coarse);
}

/// <summary>An icon action of the reply row (regenerate), drawn like the thumbs.</summary>
internal sealed class FeedbackRowButton : AvaloniaButton
{
    /// <summary>The 24 × 24 glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<FeedbackRowButton, Geometry?>(nameof(Glyph));

    private readonly FeedbackPress _press;

    public FeedbackRowButton()
    {
        _press = new FeedbackPress(this);
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(FeedbackRowButton);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEffectivelyEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _press.Press();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _press.Release();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _press.Release();
    }
}
