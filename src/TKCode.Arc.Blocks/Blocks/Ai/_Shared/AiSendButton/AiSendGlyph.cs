using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How <see cref="AiSendGlyph"/> goes between the arrow and the stop square.</summary>
internal enum AiSendGlyphSwap
{
    /// <summary>One outline morphs on a spring (the AI composer's send button).</summary>
    Morph,

    /// <summary>The arrow fades out in 100 ms while a smaller-cornered square fades in over 160 ms (the AI chat's send button).</summary>
    Crossfade
}

/// <summary>
/// The mark of <see cref="AiSendButton"/>: an up arrow that turns into a solid stop square while a reply is generated,
/// and back. Both are filled outlines in Lucide's 24-unit box: the arrow is the outline of Lucide ArrowUp stroked at 2
/// with round caps and joins (so at rest it looks exactly like the stroked icon), the square is 11 × 11 px with a 3 px
/// radius at the default 20 px size. With <see cref="AiSendGlyphSwap.Morph"/> one <see cref="PathMorph"/> carries the
/// outline between them on Arc's measured springs (arrow to square 0.32 s / bounce 0.05, back 0.31 s / 0.05), so a quick
/// send and stop reverses from the shape on screen; Reduced motion and Off swap the shapes at once (PathMorph jumps
/// springs there). With <see cref="AiSendGlyphSwap.Crossfade"/> the square is 10.5 px with 1.25 px corners (the chat's
/// measured stop mark) and the two marks crossfade without scale or blur (old 100 ms, new 160 ms, standard curve; Reduced
/// 120 ms linear; Off at once). The first shape never animates.
/// </summary>
internal sealed class AiSendGlyph : Control
{
    /// <summary>True shows the stop square, false the arrow.</summary>
    public static readonly StyledProperty<bool> IsStopProperty =
        AvaloniaProperty.Register<AiSendGlyph, bool>(nameof(IsStop));

    /// <summary>Rendered size in px (default 20, Arc's ArrowUp).</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AiSendGlyph, double>(nameof(Size), 20);

    /// <summary>The fill of the outline.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AiSendGlyph>();

    /// <summary>How the arrow and the square swap (default <see cref="AiSendGlyphSwap.Morph"/>).</summary>
    public static readonly StyledProperty<AiSendGlyphSwap> SwapProperty =
        AvaloniaProperty.Register<AiSendGlyph, AiSendGlyphSwap>(nameof(Swap));

    /// <inheritdoc cref="IsStopProperty"/>
    public bool IsStop { get => GetValue(IsStopProperty); set => SetValue(IsStopProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="SwapProperty"/>
    public AiSendGlyphSwap Swap { get => GetValue(SwapProperty); set => SetValue(SwapProperty, value); }

    /// <summary>
    /// Lucide ArrowUp ("M5 12l7-7 7 7 M12 19V5") stroked at 2 with round caps and joins, as one closed outline: the stem's
    /// round foot, up its left side to the inner edge of the left arm, the left cap, the outer edges with the round apex,
    /// the right cap, and back down the stem. 0.70711 = 1/√2.
    /// </summary>
    internal const string ArrowData =
        "M13 19A1 1 0 0 1 11 19L11 7.41421L5.70711 12.70711A1 1 0 0 1 4.29289 11.29289L11.29289 4.29289" +
        "A1 1 0 0 1 12.70711 4.29289L19.70711 11.29289A1 1 0 0 1 18.29289 12.70711L13 7.41421Z";

    /// <summary>The stop square: 11 px with a 3 px radius at 20 px, i.e. 13.2 units with 3.6 in the 24-unit box, centred.</summary>
    internal const string StopData =
        "M9 5.4H15A3.6 3.6 0 0 1 18.6 9V15A3.6 3.6 0 0 1 15 18.6H9A3.6 3.6 0 0 1 5.4 15V9A3.6 3.6 0 0 1 9 5.4Z";

    /// <summary>
    /// The crossfade's stop square, as measured on the chat's stop button: 10.5 px with 1.25 px corners (Lucide Square
    /// filled at 13 px comes out at about that size), written for the 18 px box of the chat's arrow (14 units, 1.67 corners).
    /// </summary>
    internal const string SolidStopData =
        "M6.67 5H17.33A1.67 1.67 0 0 1 19 6.67V17.33A1.67 1.67 0 0 1 17.33 19H6.67A1.67 1.67 0 0 1 5 17.33V6.67A1.67 1.67 0 0 1 6.67 5Z";

    /// <summary>The crossfade: the leaving mark's fade.</summary>
    internal static readonly TimeSpan CrossfadeExit = TimeSpan.FromMilliseconds(100);

    /// <summary>The crossfade: the coming mark's fade.</summary>
    internal static readonly TimeSpan CrossfadeEnter = TimeSpan.FromMilliseconds(160);

    /// <summary>Arrow to square: spring(0.32, 0.05), fitted to Arc's stop square (spec 5.6).</summary>
    internal static readonly SpringSpec ToStopSpring = SpringSpec.FromVisual(0.32, 0.05);

    /// <summary>Square to arrow: spring(0.31, 0.05), fitted to Arc's returning arrow (spec 5.7).</summary>
    internal static readonly SpringSpec ToArrowSpring = SpringSpec.FromVisual(0.31, 0.05);

    // Fine curves (0.1 units) and dense sampling: the arrow's caps are only 1 unit round, and the in-between outlines are
    // sampled by arc length, so 160 points keep its inner corners sharp while it moves.
    private static readonly PathMorphShape s_arrow = PathMorphShape.Parse(ArrowData, 0.1);
    private static readonly PathMorphShape s_stop = PathMorphShape.Parse(StopData, 0.1);
    private const int Samples = 160;

    // The crossfade's two marks never change shape: parsed once, shared by every glyph.
    private static readonly Geometry s_arrowGeometry = Geometry.Parse(ArrowData);
    private static readonly Geometry s_solidStopGeometry = Geometry.Parse(SolidStopData);

    private readonly PathMorph _morph;
    private Geometry? _geometry;
    private MotionValue<double>? _arrowOpacity;
    private MotionValue<double>? _stopOpacity;
    private double _arrowShown = 1;
    private double _stopShown;

    static AiSendGlyph()
    {
        AffectsMeasure<AiSendGlyph>(SizeProperty);
        AffectsRender<AiSendGlyph>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiSendGlyph>(false);
    }

    public AiSendGlyph()
    {
        _morph = new PathMorph(this, Samples);
        _morph.Changed += OnMorphChanged;
        _morph.Jump(s_arrow);
    }

    /// <summary>True while the outline is between the two shapes.</summary>
    internal bool IsMorphing => _morph.IsAnimating;

    /// <summary>The shape shown at rest or being morphed to: the stop square or the arrow.</summary>
    internal bool ShowsStop => ReferenceEquals(_morph.Target, s_stop);

    /// <summary>The outline engine, for tests.</summary>
    internal PathMorph Morph => _morph;

    /// <summary>The crossfade's arrow opacity (1 while the arrow shows), for tests.</summary>
    internal double ArrowOpacity => _arrowShown;

    /// <summary>The crossfade's square opacity (1 while the square shows), for tests.</summary>
    internal double StopOpacity => _stopShown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsStopProperty)
        {
            var stop = change.GetNewValue<bool>();
            var shape = stop ? s_stop : s_arrow;
            if (Swap == AiSendGlyphSwap.Crossfade)
            {
                // The outline only records which mark is meant; the two fixed marks fade.
                _morph.Jump(shape);
                Crossfade(stop);
                return;
            }
            if (!IsLoaded)
            {
                _morph.Jump(shape);
                return;
            }
            _ = _morph.MorphTo(shape, MotionSpec.Spring(stop ? ToStopSpring : ToArrowSpring));
        }
        else if (change.Property == SwapProperty)
        {
            // A new swap style starts from the mark that is meant, at rest.
            _morph.Jump(IsStop ? s_stop : s_arrow);
            JumpCrossfade(IsStop);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The crossfade (code-driven: the two marks share one control and one render pass, with an exit shorter than the
    /// entrance, so each needs its own opacity curve; a ControlTheme transition would need two child visuals per glyph).
    /// </summary>
    private void Crossfade(bool stop)
    {
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            JumpCrossfade(stop);
            InvalidateVisual();
            return;
        }
        _arrowOpacity ??= CreateOpacity(_arrowShown, v => _arrowShown = v);
        _stopOpacity ??= CreateOpacity(_stopShown, v => _stopShown = v);
        var (leaving, coming) = stop ? (_arrowOpacity, _stopOpacity) : (_stopOpacity, _arrowOpacity);
        if (level == ArcMotionLevel.Reduced)
        {
            var linear = MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing());
            _ = leaving.AnimateTo(0, linear);
            _ = coming.AnimateTo(1, linear);
            return;
        }
        _ = leaving.AnimateTo(0, MotionSpec.Tween(CrossfadeExit, ArcEasings.Standard));
        _ = coming.AnimateTo(1, MotionSpec.Tween(CrossfadeEnter, ArcEasings.Standard));
    }

    private void JumpCrossfade(bool stop)
    {
        _arrowOpacity?.Jump(stop ? 0 : 1);
        _stopOpacity?.Jump(stop ? 1 : 0);
        _arrowShown = stop ? 0 : 1;
        _stopShown = stop ? 1 : 0;
    }

    private MotionValue<double> CreateOpacity(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            apply(Math.Clamp(v, 0, 1));
            InvalidateVisual();
        };
        return value;
    }

    // A morph frame builds one StreamGeometry (PathMorph's contract, like MorphGlyph); it runs only for the ~0.4 s of a
    // state change, never while the button rests.
    private void OnMorphChanged()
    {
        _geometry = _morph.BuildGeometry();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            if (Swap == AiSendGlyphSwap.Crossfade)
            {
                Draw(context, brush, s_arrowGeometry, _arrowShown);
                Draw(context, brush, s_solidStopGeometry, _stopShown);
                return;
            }
            if (_geometry is { } geometry)
            {
                context.DrawGeometry(brush, null, geometry);
            }
        }
    }

    private static void Draw(DrawingContext context, IBrush brush, Geometry geometry, double opacity)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        if (opacity >= 0.999)
        {
            context.DrawGeometry(brush, null, geometry);
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(brush, null, geometry);
        }
    }
}
