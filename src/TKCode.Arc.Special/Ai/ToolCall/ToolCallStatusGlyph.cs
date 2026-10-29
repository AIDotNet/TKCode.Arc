using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A call's 16px status icon, a different shape per status so color is never the only cue: running draws a ring track at
/// 20% with a quarter arc turning once every 800ms (linear); success a check that draws itself once (330ms on the
/// standard curve, 50ms after it appears); error Lucide circle-alert in Danger; approval Lucide shield-question-mark in
/// Accent; denied Lucide ban in TextMuted. A new status trades places with the old one in the same cell, both at once:
/// each fades over 120ms (CSS ease-out) while scaling 0.6 ↔ 1 on a critically damped spring (measured ω ≈ 20, no
/// overshoot). The first status shows as it is. Reduced motion and Off swap at once, draw the check whole and hold the
/// spinner still. Decorative: the row's name says the status.
/// <para>
/// Drawn in code (<see cref="MotionValue{T}"/> per glyph, a <see cref="LiveLoop"/> for the spin) because the two glyphs
/// overlap in one cell with their own scale, fade and stroke progress, which a ControlTheme transition cannot express.
/// </para>
/// </summary>
internal sealed class ToolCallStatusGlyph : Control
{
    /// <summary>The status shown.</summary>
    public static readonly StyledProperty<ToolCallStatus> StatusProperty =
        AvaloniaProperty.Register<ToolCallStatusGlyph, ToolCallStatus>(nameof(Status), ToolCallStatus.Success);

    /// <summary>Spinner and ban (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<ToolCallStatusGlyph, IBrush?>(nameof(MutedBrush));

    /// <summary>The check (theme: Success).</summary>
    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<ToolCallStatusGlyph, IBrush?>(nameof(SuccessBrush));

    /// <summary>The alert (theme: Danger).</summary>
    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<ToolCallStatusGlyph, IBrush?>(nameof(DangerBrush));

    /// <summary>The shield (theme: Accent).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ToolCallStatusGlyph, IBrush?>(nameof(AccentBrush));

    /// <summary>One turn of the spinner.</summary>
    internal const double TurnSeconds = 0.8;

    /// <summary>The swap fade.</summary>
    internal static readonly TimeSpan SwapFade = TimeSpan.FromMilliseconds(120);

    /// <summary>The check's stroke draw and its wait.</summary>
    internal static readonly TimeSpan CheckDraw = TimeSpan.FromMilliseconds(330);

    internal static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>The swap's scale spring (visual duration 0.26s, no bounce: ω ≈ 20).</summary>
    internal static readonly SpringSpec SwapSpring = AiGlyphMotion.Settle;

    // The check in a 16 × 16 box, 9.5 × 7 as measured.
    private static readonly Geometry s_check = StreamGeometry.Parse("M3.25 8.25 L6.5 11.75 L12.75 4.75");
    private static readonly AiStroke s_checkStroke = new(new Point(3.25, 8.25), new Point(6.5, 11.75), new Point(12.75, 4.75));
    private static readonly Geometry s_shield = ArcIcons.Parse(
        "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M9.1 9a3 3 0 0 1 5.82 1c0 2-3 3-3 3 M12 17h.01");

    private readonly List<Glyph> _glyphs = new(2);
    private readonly AiSpin _spin;
    private readonly Dictionary<IBrush, IPen> _pens = new();
    private readonly Dictionary<IBrush, IPen> _iconPens = new();
    private IPen? _trackPen;
    private IBrush? _trackBrush;
    private static readonly StreamGeometry s_arc = AiSpin.QuarterArc(new Point(8, 8), 6);

    private sealed class Glyph
    {
        public required ToolCallStatus Status { get; init; }
        public MotionValue<double>? Opacity { get; set; }
        public MotionValue<double>? Scale { get; set; }
        public MotionValue<double>? Draw { get; set; }
        public bool Leaving { get; set; }
    }

    static ToolCallStatusGlyph()
    {
        AffectsRender<ToolCallStatusGlyph>(MutedBrushProperty, SuccessBrushProperty, DangerBrushProperty, AccentBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolCallStatusGlyph>(false);
        FocusableProperty.OverrideDefaultValue<ToolCallStatusGlyph>(false);
    }

    public ToolCallStatusGlyph()
    {
        _spin = new AiSpin(this, TurnSeconds, InvalidateVisual);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public ToolCallStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="MutedBrushProperty"/>
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <inheritdoc cref="SuccessBrushProperty"/>
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    /// <inheritdoc cref="DangerBrushProperty"/>
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>The spinner's rotation in degrees.</summary>
    internal double Angle => _spin.Angle;

    /// <summary>Whether the spinner turns now.</summary>
    internal bool IsSpinning => _spin.IsRunning;

    /// <summary>How many glyphs are drawn (2 during a swap).</summary>
    internal int GlyphCount => _glyphs.Count;

    /// <summary>The shown glyph's opacity, scale and stroke progress.</summary>
    internal (double Opacity, double Scale, double Draw) Current
    {
        get
        {
            var glyph = _glyphs.LastOrDefault(g => !g.Leaving);
            return glyph is null ? (0, 0, 0) : (glyph.Opacity?.Value ?? 1, glyph.Scale?.Value ?? 1, glyph.Draw?.Value ?? 1);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            Swap(change.GetNewValue<ToolCallStatus>());
        }
        else if (change.Property == MutedBrushProperty)
        {
            _trackPen = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_glyphs.Count == 0)
        {
            _glyphs.Add(new Glyph { Status = Status });
        }
        UpdateSpin();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Settle every swap in flight: away from a window motion would only jump later.
        _glyphs.RemoveAll(g => g.Leaving);
        foreach (var glyph in _glyphs)
        {
            glyph.Opacity?.Jump(1);
            glyph.Scale?.Jump(1);
            glyph.Draw?.Jump(1);
        }
    }

    private void Swap(ToolCallStatus status)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level != ArcMotionLevel.Full || _glyphs.Count == 0)
        {
            foreach (var old in _glyphs)
            {
                old.Opacity?.Stop();
                old.Scale?.Stop();
                old.Draw?.Stop();
            }
            _glyphs.Clear();
            _glyphs.Add(new Glyph { Status = status });
            UpdateSpin();
            InvalidateVisual();
            return;
        }
        var easing = ToolCallCollapsible.EaseOut;
        foreach (var old in _glyphs)
        {
            if (old.Leaving)
            {
                continue;
            }
            old.Leaving = true;
            old.Opacity ??= Value(1);
            old.Scale ??= Value(1);
            var leaving = old;
            _ = old.Opacity.AnimateTo(0, MotionSpec.Tween(SwapFade, easing));
            _ = old.Scale.AnimateTo(0.6, MotionSpec.Spring(SwapSpring)).ContinueWith(_ =>
                Dispatcher.Post(() => Remove(leaving)), TaskScheduler.Default);
        }
        var glyph = new Glyph { Status = status, Opacity = Value(0), Scale = Value(0.6) };
        _glyphs.Add(glyph);
        _ = glyph.Opacity.AnimateTo(1, MotionSpec.Tween(SwapFade, easing));
        _ = glyph.Scale.AnimateTo(1, MotionSpec.Spring(SwapSpring));
        if (status == ToolCallStatus.Success)
        {
            glyph.Draw = Value(0);
            _ = AiGlyphMotion.DrawCheck(glyph.Draw, CheckDraw, CheckDelay);
        }
        UpdateSpin();
        InvalidateVisual();
    }

    private void Remove(Glyph glyph)
    {
        // A leaving glyph stays (invisible) until its scale spring settles, as Arc keeps the node until then.
        if (glyph.Leaving && glyph.Scale?.IsAnimating != true && _glyphs.Remove(glyph))
        {
            UpdateSpin();
            InvalidateVisual();
        }
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private void UpdateSpin()
    {
        _spin.Run(_glyphs.Any(g => g.Status == ToolCallStatus.Running));
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var unit = size / 16.0;
        foreach (var glyph in _glyphs)
        {
            var opacity = Math.Clamp(glyph.Opacity?.Value ?? 1, 0, 1);
            if (opacity <= 0.001)
            {
                continue;
            }
            var scale = glyph.Scale?.Value ?? 1;
            var matrix = Matrix.CreateTranslation(-8, -8) * Matrix.CreateScale(scale * unit, scale * unit) *
                         Matrix.CreateTranslation(origin.X + size / 2, origin.Y + size / 2);
            using (context.PushOpacity(opacity))
            using (context.PushTransform(matrix))
            {
                DrawGlyph(context, glyph);
            }
        }
    }

    private void DrawGlyph(DrawingContext context, Glyph glyph)
    {
        switch (glyph.Status)
        {
            case ToolCallStatus.Running:
                DrawSpinner(context);
                break;
            case ToolCallStatus.Success:
                DrawCheck(context, Math.Clamp(glyph.Draw?.Value ?? 1, 0, 1));
                break;
            case ToolCallStatus.Error:
                DrawIcon(context, ArcIcons.CircleAlert, DangerBrush);
                break;
            case ToolCallStatus.Approval:
                DrawIcon(context, s_shield, AccentBrush);
                break;
            default:
                DrawIcon(context, ArcIcons.Ban, MutedBrush);
                break;
        }
    }

    private void DrawSpinner(DrawingContext context)
    {
        if (MutedBrush is not { } brush)
        {
            return;
        }
        if (_trackPen is null || !ReferenceEquals(_trackBrush, brush))
        {
            _trackBrush = brush;
            var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _trackPen = new ImmutablePen(new ImmutableSolidColorBrush(color, 0.2), 1.75);
        }
        context.DrawEllipse(null, _trackPen, new Point(8, 8), 6, 6);
        // s_arc: a quarter of the r = 6 circle from 12 o'clock clockwise, turned by the spin.
        using (context.PushTransform(_spin.Turn(new Point(8, 8))))
        {
            context.DrawGeometry(null, Pen(brush, 1.75, _pens), s_arc);
        }
    }

    private void DrawCheck(DrawingContext context, double progress)
    {
        if (SuccessBrush is not { } brush || progress <= 0)
        {
            return;
        }
        var pen = Pen(brush, 1.75, _pens);
        if (progress >= 0.999)
        {
            context.DrawGeometry(null, pen, s_check);
            return;
        }
        s_checkStroke.Draw(context, pen, progress);
    }

    private void DrawIcon(DrawingContext context, Geometry icon, IBrush? brush)
    {
        if (brush is null)
        {
            return;
        }
        // Lucide's 24 box in the 16 cell; the stroke is 1.75 in the icon's own units, as Arc draws it.
        using (context.PushTransform(Matrix.CreateScale(16.0 / 24, 16.0 / 24)))
        {
            context.DrawGeometry(null, Pen(brush, 1.75, _iconPens), icon);
        }
    }

    private static IPen Pen(IBrush brush, double thickness, Dictionary<IBrush, IPen> cache)
    {
        if (!cache.TryGetValue(brush, out var pen))
        {
            if (cache.Count > 8)
            {
                cache.Clear();
            }
            pen = new ImmutablePen(brush.ToImmutable(), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            cache[brush] = pen;
        }
        return pen;
    }
}
