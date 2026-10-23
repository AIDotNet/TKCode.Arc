using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Which side of its anchor a <see cref="ShowpieceHoverCard"/> prefers.</summary>
internal enum ShowpieceCardSide
{
    /// <summary>To the right of the anchor, flipping to the left when it does not fit.</summary>
    Right,

    /// <summary>Centered above the anchor, flipping below when it does not fit.</summary>
    Above
}

/// <summary>How a beside-the-anchor card lines up with the anchor across the main axis.</summary>
internal enum ShowpieceCardAlign
{
    /// <summary>Top edge <see cref="ShowpieceCardPlacement.CrossOffset"/> below the anchor.</summary>
    Start,

    /// <summary>Centered on the anchor.</summary>
    Center,

    /// <summary>Bottom edge <see cref="ShowpieceCardPlacement.CrossOffset"/> above the anchor.</summary>
    End
}

/// <summary>
/// Where a <see cref="ShowpieceHoverCard"/> goes relative to its anchor (all distances in stage px). Use a preset; a
/// <c>new()</c> placement has zero gaps.
/// </summary>
internal readonly record struct ShowpieceCardPlacement
{
    public ShowpieceCardSide Side { get; init; }

    /// <summary>Distance between the anchor and the card along the main axis.</summary>
    public double Gap { get; init; }

    public ShowpieceCardAlign CrossAlign { get; init; }

    /// <summary>Offset for <see cref="ShowpieceCardAlign.Start"/> / <see cref="ShowpieceCardAlign.End"/>.</summary>
    public double CrossOffset { get; init; }

    /// <summary>Space kept from the stage edges (flip test on the main axis, clamp on the cross axis).</summary>
    public double EdgeMargin { get; init; }

    /// <summary>With <see cref="ShowpieceCardAlign.Start"/>: open above the anchor when there is no room below.</summary>
    public bool FlipCross { get; init; }

    /// <summary>Lowest top edge (Semantic zoom keeps cards under its breadcrumb: 58); otherwise <see cref="EdgeMargin"/>.</summary>
    public double CrossMinimum { get; init; }

    /// <summary>Customer galaxy: 18px right of the pointer, centered on it, flipping left; clamped 8px inside.</summary>
    public static ShowpieceCardPlacement Galaxy => new() { Side = ShowpieceCardSide.Right, Gap = 18, CrossAlign = ShowpieceCardAlign.Center, EdgeMargin = 8 };

    /// <summary>Semantic zoom: pointer + (18, 18), flipping left and up; top edge at least 58.</summary>
    public static ShowpieceCardPlacement SemanticZoom => new()
    {
        Side = ShowpieceCardSide.Right,
        Gap = 18,
        CrossAlign = ShowpieceCardAlign.Start,
        CrossOffset = 18,
        FlipCross = true,
        CrossMinimum = 58
    };

    /// <summary>Activity terrain: 22px right of the bar top, bottom 16px above it, flipping left within 8px; clamped to [8, H − h − 8].</summary>
    public static ShowpieceCardPlacement Terrain => new()
    {
        Side = ShowpieceCardSide.Right,
        Gap = 22,
        CrossAlign = ShowpieceCardAlign.End,
        CrossOffset = 16,
        EdgeMargin = 8
    };

    /// <summary>Revenue globe: centered 16px above the city.</summary>
    public static ShowpieceCardPlacement Globe => new() { Side = ShowpieceCardSide.Above, Gap = 16, FlipCross = true };

    /// <summary>
    /// The card's top-left corner for <paramref name="anchor"/> in a stage of <paramref name="stage"/> size, and whether
    /// it flipped on the main (<paramref name="flippedMain"/>) or cross (<paramref name="flippedCross"/>) axis.
    /// </summary>
    public Point Place(Point anchor, Size card, Size stage, out bool flippedMain, out bool flippedCross)
    {
        flippedMain = false;
        flippedCross = false;
        double x, y;
        var margin = EdgeMargin;
        var minTop = Math.Max(margin, CrossMinimum);
        if (Side == ShowpieceCardSide.Above)
        {
            x = anchor.X - card.Width / 2;
            y = anchor.Y - Gap - card.Height;
            if (FlipCross && y < minTop)
            {
                y = anchor.Y + Gap;
                flippedCross = true;
            }
            x = ClampAxis(x, card.Width, stage.Width, margin, margin);
            return new Point(x, y);
        }
        x = anchor.X + Gap;
        if (x + card.Width > stage.Width - margin)
        {
            x = anchor.X - Gap - card.Width;
            flippedMain = true;
        }
        x = ClampAxis(x, card.Width, stage.Width, margin, margin);
        switch (CrossAlign)
        {
            case ShowpieceCardAlign.Start:
                y = anchor.Y + CrossOffset;
                if (FlipCross && y + card.Height > stage.Height - margin)
                {
                    y = anchor.Y - CrossOffset - card.Height;
                    flippedCross = true;
                }
                break;
            case ShowpieceCardAlign.End:
                y = anchor.Y - CrossOffset - card.Height;
                break;
            default:
                y = anchor.Y - card.Height / 2;
                break;
        }
        y = ClampAxis(y, card.Height, stage.Height, minTop, margin);
        return new Point(x, y);
    }

    private static double ClampAxis(double start, double length, double stage, double low, double high)
    {
        var max = stage - length - high;
        if (max < low)
        {
            return low;
        }
        return Math.Clamp(start, low, max);
    }
}

/// <summary>
/// A card that follows a point inside a Showpiece stage (hovered dot, bar, city, item): content from
/// <see cref="ContentControl.Content"/> / <see cref="ContentControl.ContentTemplate"/> on an Arc floating surface
/// (<c>SurfaceRaised</c>, 1px <c>Border</c>, radius 12, <c>ShadowFloating</c>), placed beside <see cref="Anchor"/> by
/// <see cref="Placement"/> with edge flips. Opening fades in (<see cref="FadeInDuration"/>, standard curve) and grows from
/// <see cref="EnterScale"/> (580ms CSS spring) at the corner nearest the anchor, landing at its position without sliding;
/// while open it follows a moving anchor on <see cref="PositionSpring"/> (or jumps when null); closing fades out where it
/// is and shrinks back. Reduced motion keeps only the fades; Off jumps. Put it in a panel that exactly covers the stage
/// (stage coordinates) with no hit testing.
/// </summary>
/// <remarks>
/// Code-driven motion: the card jumps on its first appearance, then chases an anchor that moves every frame with a
/// velocity-keeping spring, and its scale origin follows the flips; ControlTheme transitions restart from rest and cannot
/// make the first move a jump.
/// </remarks>
[PseudoClasses(OpenPseudoClass, FlippedPseudoClass)]
internal sealed class ShowpieceHoverCard : ContentControl
{
    public const string OpenPseudoClass = ":open";
    public const string FlippedPseudoClass = ":flipped";

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, bool>(nameof(IsOpen));

    public static readonly StyledProperty<Point> AnchorProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, Point>(nameof(Anchor));

    public static readonly StyledProperty<ShowpieceCardPlacement> PlacementProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, ShowpieceCardPlacement>(nameof(Placement), ShowpieceCardPlacement.Galaxy);

    public static readonly StyledProperty<SpringSpec?> PositionSpringProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, SpringSpec?>(nameof(PositionSpring));

    public static readonly StyledProperty<TimeSpan> FadeInDurationProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, TimeSpan>(nameof(FadeInDuration), ArcMotionPresets.Fast);

    public static readonly StyledProperty<TimeSpan> FadeOutDurationProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, TimeSpan>(nameof(FadeOutDuration), ArcMotionPresets.Fast);

    public static readonly StyledProperty<double> EnterScaleProperty =
        AvaloniaProperty.Register<ShowpieceHoverCard, double>(nameof(EnterScale), 0.96);

    /// <summary>Shown (fading in) or hidden (fading out).</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>The point the card stays next to, in the coordinates of the panel that holds it.</summary>
    public Point Anchor { get => GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }

    /// <summary>Side, gaps, alignment and flips (default <see cref="ShowpieceCardPlacement.Galaxy"/>).</summary>
    public ShowpieceCardPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>
    /// Spring that follows a moving anchor while open (Activity terrain <see cref="ShowpieceSprings.CardFollow"/>, Revenue
    /// globe <see cref="ArcMotionPresets.Snappy"/>); null jumps with the anchor (Customer galaxy, Semantic zoom).
    /// </summary>
    public SpringSpec? PositionSpring { get => GetValue(PositionSpringProperty); set => SetValue(PositionSpringProperty, value); }

    /// <summary>Fade-in time (default 160ms; Activity terrain 120ms).</summary>
    public TimeSpan FadeInDuration { get => GetValue(FadeInDurationProperty); set => SetValue(FadeInDurationProperty, value); }

    /// <summary>Fade-out time (default 160ms).</summary>
    public TimeSpan FadeOutDuration { get => GetValue(FadeOutDurationProperty); set => SetValue(FadeOutDurationProperty, value); }

    /// <summary>Scale the card grows from and shrinks back to (default 0.96).</summary>
    public double EnterScale { get => GetValue(EnterScaleProperty); set => SetValue(EnterScaleProperty, value); }

    /// <summary>The card flipped to the other side of its anchor (main axis).</summary>
    public bool IsFlipped { get; private set; }

    /// <summary>The card flipped across the anchor (cross axis).</summary>
    public bool IsCrossFlipped { get; private set; }

    /// <summary>Where the card is heading (top-left, container px).</summary>
    public Point TargetPosition => _position?.Target ?? _placed;

    /// <summary>Where the card is drawn now (top-left, container px).</summary>
    public Point CurrentPosition => _position?.Value ?? _placed;

    /// <summary>Current opacity of the card (tests).</summary>
    internal double CurrentOpacity => _opacity?.Value ?? 0;

    /// <summary>Current scale of the card (tests).</summary>
    internal double CurrentScale => _scaleValue?.Value ?? 1;

    private static readonly Easing s_standard = ArcEasings.Standard;

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new();
    private MotionValue<Point>? _position;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _scaleValue;
    private Point _placed;
    private bool _shown;
    private Size _lastSize;

    static ShowpieceHoverCard()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ShowpieceHoverCard>(false);
        FocusableProperty.OverrideDefaultValue<ShowpieceHoverCard>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<ShowpieceHoverCard>(Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<ShowpieceHoverCard>(Avalonia.Layout.VerticalAlignment.Top);
    }

    public ShowpieceHoverCard()
    {
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        Opacity = 0;
        IsVisible = false;
    }

    protected override Type StyleKeyOverride => typeof(ShowpieceHoverCard);

    private ArcMotionLevel Level => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (IsOpen)
            {
                Open();
            }
            else
            {
                Close();
            }
        }
        else if (change.Property == AnchorProperty || change.Property == PlacementProperty)
        {
            if (_shown)
            {
                MoveToAnchor(jump: false);
            }
        }
    }

    private void EnsureValues()
    {
        if (_position is not null)
        {
            return;
        }
        _position = new MotionValue<Point>(this, _placed, MotionVectors.Point);
        _position.Changed += p =>
        {
            _translate.X = p.X;
            _translate.Y = p.Y;
        };
        _opacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity.Changed += OnOpacityChanged;
        _scaleValue = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scaleValue.Changed += s =>
        {
            _scale.ScaleX = s;
            _scale.ScaleY = s;
        };
    }

    private void OnOpacityChanged(double value)
    {
        Opacity = Math.Clamp(value, 0, 1);
        if (!_shown && value <= 0.001)
        {
            IsVisible = false;
        }
    }

    private void Open()
    {
        EnsureValues();
        var wasShown = _shown;
        _shown = true;
        IsVisible = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        var level = Level;
        // A card that is not visible yet lands where it belongs; one that is still fading out moves on from there.
        MoveToAnchor(jump: !wasShown && _opacity!.Value <= 0.001);
        if (level == ArcMotionLevel.Off)
        {
            _opacity!.Jump(1);
            _scaleValue!.Jump(1);
            return;
        }
        _ = _opacity!.AnimateTo(1, MotionSpec.Tween(FadeInDuration, s_standard));
        if (level == ArcMotionLevel.Full)
        {
            if (!wasShown && _opacity.Value <= 0.001)
            {
                _scaleValue!.Jump(EnterScale);
            }
            _ = _scaleValue!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
        else
        {
            _scaleValue!.Jump(1);
        }
    }

    private void Close()
    {
        if (!_shown || _opacity is null || _scaleValue is null)
        {
            return;
        }
        _shown = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        var level = Level;
        _position?.Stop();
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
            _scaleValue.Jump(1);
            return;
        }
        _ = _opacity.AnimateTo(0, MotionSpec.Tween(FadeOutDuration, s_standard));
        if (level == ArcMotionLevel.Full)
        {
            _ = _scaleValue.AnimateTo(EnterScale, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
    }

    /// <summary>Sets anchor and opens in one go, landing without a slide when the card was hidden.</summary>
    public void ShowAt(Point anchor)
    {
        SetCurrentValue(AnchorProperty, anchor);
        SetCurrentValue(IsOpenProperty, true);
    }

    private Size ContainerSize => (this.GetVisualParent() ?? Parent as Visual)?.Bounds.Size ?? default;

    private void MoveToAnchor(bool jump)
    {
        EnsureValues();
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = DesiredSize;
        }
        var target = Placement.Place(Anchor, size, ContainerSize, out var flipped, out var crossFlipped);
        IsFlipped = flipped;
        IsCrossFlipped = crossFlipped;
        PseudoClasses.Set(FlippedPseudoClass, flipped);
        RenderTransformOrigin = OriginFor(Placement, flipped, crossFlipped);
        _placed = target;
        if (jump || PositionSpring is not { } spring || Level != ArcMotionLevel.Full)
        {
            _position!.Jump(target);
        }
        else
        {
            _ = _position!.AnimateTo(target, MotionSpec.Spring(spring));
        }
    }

    private static RelativePoint OriginFor(in ShowpieceCardPlacement placement, bool flipped, bool crossFlipped)
    {
        if (placement.Side == ShowpieceCardSide.Above)
        {
            return new RelativePoint(0.5, crossFlipped ? 0 : 1, RelativeUnit.Relative);
        }
        var x = flipped ? 1 : 0;
        var y = placement.CrossAlign switch
        {
            ShowpieceCardAlign.Start => crossFlipped ? 1 : 0,
            ShowpieceCardAlign.End => 1,
            _ => 0.5
        };
        return new RelativePoint(x, y, RelativeUnit.Relative);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_shown && size != _lastSize)
        {
            // New content or first layout: place again with the real size (a first appearance still lands in place).
            _lastSize = size;
            var jump = (_opacity?.Value ?? 0) < 0.05;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_shown)
                {
                    MoveToAnchor(jump);
                }
            }, Avalonia.Threading.DispatcherPriority.Render);
        }
        return size;
    }
}
