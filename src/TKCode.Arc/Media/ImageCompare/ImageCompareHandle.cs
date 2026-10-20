using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The grab handle of an <see cref="ImageCompare"/>: a raised pill with two chevrons, focusable as the slider.
/// Drawn in Render so its shape can morph from a 40px circle to a 56 × 36 capsule while dragging (Arc animates only
/// width and height, so the chevrons never scale), the chevrons can spread and the whole glyph can turn with the
/// divider. The hit area reaches 8px beyond the pill. The owner positions it with a render transform.
/// </summary>
internal sealed class ImageCompareHandle : Control, ICustomHitTest
{
    /// <summary>Square layout box; the pill, its shadow and the focus ring are drawn around its center.</summary>
    public const double Extent = 96;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<ImageCompareHandle>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<ImageCompareHandle>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<ImageCompareHandle>();

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        Border.BoxShadowProperty.AddOwner<ImageCompareHandle>();

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ImageCompareHandle, IBrush?>(nameof(FocusRingBrush));

    private static readonly Matrix s_glyphScale = Matrix.CreateScale(16.0 / 24, 16.0 / 24);
    private Size _capsule = new(40, 40);
    private double _spread;
    private double _angle;
    private bool _keyed;
    private Pen? _borderPen;
    private IBrush? _borderPenBrush;
    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;
    private ImmutablePen? _focusPen;
    private Color _focusColor;

    static ImageCompareHandle()
    {
        AffectsRender<ImageCompareHandle>(BackgroundProperty, BorderBrushProperty, ForegroundProperty, BoxShadowProperty,
            FocusRingBrushProperty);
        FocusableProperty.OverrideDefaultValue<ImageCompareHandle>(true);
    }

    public ImageCompareHandle()
    {
        FocusAdorner = null;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Extent, Extent);

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    internal ImageCompare? Owner { get; set; }

    /// <summary>Current pill size (40 × 40 at rest).</summary>
    public Size Capsule => _capsule;

    /// <summary>How far each chevron has moved outward (0 at rest, 4 while dragging).</summary>
    public double Spread => _spread;

    /// <summary>True while the focus ring is drawn (keyboard focus, or a key pressed after pointer focus).</summary>
    public bool IsRingVisible => IsFocused && (_keyed || Classes.Contains(":focus-visible"));

    public void SetShape(Size capsule, double spread, double angle)
    {
        if (capsule == _capsule && spread == _spread && angle == _angle)
        {
            return;
        }
        _capsule = capsule;
        _spread = spread;
        _angle = angle;
        InvalidateVisual();
    }

    /// <summary>Arc data-quiet: focus that follows a pointer shows no ring until a key is pressed.</summary>
    public void ShowRing()
    {
        if (!_keyed)
        {
            _keyed = true;
            InvalidateVisual();
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
        _keyed = false;
        InvalidateVisual();
    }

    private Rect PillRect()
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        return new Rect(center.X - _capsule.Width / 2, center.Y - _capsule.Height / 2, _capsule.Width, _capsule.Height);
    }

    public bool HitTest(Point point) => PillRect().Inflate(8).Contains(point);

    public override void Render(DrawingContext context)
    {
        var pill = PillRect();
        var radius = Math.Min(pill.Width, pill.Height) / 2;
        var border = BorderBrush;
        if (border is not null && (_borderPen is null || !ReferenceEquals(_borderPenBrush, border)))
        {
            _borderPen = new Pen(border, 1);
            _borderPenBrush = border;
        }
        // The border sits inside the box (box-sizing: border-box).
        context.DrawRectangle(Background, border is null ? null : _borderPen, new RoundedRect(pill.Deflate(0.5), radius - 0.5),
            BoxShadow);

        var foreground = Foreground;
        if (foreground is not null)
        {
            if (_glyphPen is null || !ReferenceEquals(_glyphPenBrush, foreground))
            {
                _glyphPen = new Pen(foreground, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                _glyphPenBrush = foreground;
            }
            // Each 16px chevron box has -2.5px inline margins: their centers sit 5.5px either side of the middle.
            var turn = Matrix.CreateRotation(_angle * Math.PI / 180) * Matrix.CreateTranslation(pill.Center.X, pill.Center.Y);
            DrawChevron(context, ArcIcons.ChevronLeft, -5.5 - _spread, turn);
            DrawChevron(context, ArcIcons.ChevronRight, 5.5 + _spread, turn);
        }

        if (IsRingVisible && FocusRingBrush is ISolidColorBrush ring)
        {
            if (_focusPen is null || _focusColor != ring.Color)
            {
                _focusColor = ring.Color;
                _focusPen = new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), 2);
            }
            // 2px outline, 2px outside the pill.
            var outline = pill.Inflate(3);
            context.DrawRectangle(null, _focusPen, new RoundedRect(outline, radius + 3));
        }
    }

    private void DrawChevron(DrawingContext context, Geometry glyph, double offset, Matrix turn)
    {
        var matrix = Matrix.CreateTranslation(-12, -12) * s_glyphScale * Matrix.CreateTranslation(offset, 0) * turn;
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, _glyphPen, glyph);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ImageCompareHandlePeer(this);

    /// <summary>Arc: role="slider" whose value is the share of the after image, e.g. "40% after".</summary>
    private sealed class ImageCompareHandlePeer : ControlAutomationPeer, IRangeValueProvider
    {
        public ImageCompareHandlePeer(ImageCompareHandle owner)
            : base(owner)
        {
        }

        private ImageCompare? Compare => ((ImageCompareHandle)Owner).Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Compare?.Label;

        protected override string? GetItemStatusCore() => Compare is { } compare ? $"{compare.AfterShare}% after" : null;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => 100;

        public double Value => Compare?.AfterShare ?? 50;

        public double LargeChange => 10;

        public double SmallChange => 1;

        public void SetValue(double value)
        {
            if (Compare is { } compare)
            {
                compare.Position = Math.Clamp(100 - value, 0, 100);
            }
        }
    }
}
