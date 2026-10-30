using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A cell's 16px status icon, a different shape per status so color is never the only cue: idle a faint ring, queued Lucide
/// clock, running a ring track at 20% with a quarter arc turning once every 800ms, done a check that draws itself in 240ms
/// (standard curve), failed Lucide circle-alert in Danger. A new status trades places with the old one in the same cell:
/// each fades over 120ms while scaling 0.6 ↔ 1 on a bounce-free settle spring. Reduced motion keeps the crossfade but
/// cuts the scale and the check draw, and the spinner turns at half speed; Off swaps at once and holds the spinner still.
/// Decorative: the cell's text says the status.
/// <para>
/// Drawn in code (<see cref="MotionValue{T}"/> per glyph, a <see cref="LiveLoop"/> for the spin) because two glyphs
/// overlap in one cell with their own scale, fade and stroke progress, which a ControlTheme transition cannot express.
/// </para>
/// </summary>
internal sealed class EnrichColumnStatusGlyph : Control
{
    /// <summary>The status shown.</summary>
    public static readonly StyledProperty<EnrichColumnCellStatus> StatusProperty =
        AvaloniaProperty.Register<EnrichColumnStatusGlyph, EnrichColumnCellStatus>(nameof(Status));

    /// <summary>Idle, queued and the spinner (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<EnrichColumnStatusGlyph, IBrush?>(nameof(MutedBrush));

    /// <summary>The check (theme: Success).</summary>
    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<EnrichColumnStatusGlyph, IBrush?>(nameof(SuccessBrush));

    /// <summary>The alert (theme: Danger).</summary>
    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<EnrichColumnStatusGlyph, IBrush?>(nameof(DangerBrush));

    /// <summary>The spinner's arc (theme: Accent).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<EnrichColumnStatusGlyph, IBrush?>(nameof(AccentBrush));

    /// <summary>One turn of the spinner at full motion; reduced motion takes twice as long.</summary>
    internal const double TurnSeconds = 0.8;

    /// <summary>The swap fade.</summary>
    internal static readonly TimeSpan SwapFade = TimeSpan.FromMilliseconds(120);

    /// <summary>The check's stroke draw.</summary>
    internal static readonly TimeSpan CheckDraw = TimeSpan.FromMilliseconds(240);

    private static readonly Geometry s_check = StreamGeometry.Parse("M3.25 8.25 L6.5 11.75 L12.75 4.75");
    private static readonly AiStroke s_checkStroke = new(new Point(3.25, 8.25), new Point(6.5, 11.75), new Point(12.75, 4.75));
    private static readonly StreamGeometry s_arc = AiSpin.QuarterArc(new Point(8, 8), 6);

    private readonly List<Glyph> _glyphs = new(2);
    private readonly AiSpin _spin = new(TurnSeconds);
    private readonly LiveLoop _loop;
    private readonly Dictionary<IBrush, IPen> _pens = new();
    private readonly Dictionary<IBrush, IPen> _iconPens = new();
    private IPen? _trackPen;
    private IPen? _idlePen;
    private IBrush? _trackBrush;

    private sealed class Glyph
    {
        public required EnrichColumnCellStatus Status { get; init; }
        public MotionValue<double>? Opacity { get; set; }
        public MotionValue<double>? Scale { get; set; }
        public MotionValue<double>? Draw { get; set; }
        public bool Leaving { get; set; }
    }

    static EnrichColumnStatusGlyph()
    {
        AffectsRender<EnrichColumnStatusGlyph>(MutedBrushProperty, SuccessBrushProperty, DangerBrushProperty, AccentBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<EnrichColumnStatusGlyph>(false);
        FocusableProperty.OverrideDefaultValue<EnrichColumnStatusGlyph>(false);
    }

    public EnrichColumnStatusGlyph()
    {
        // The spinner runs at full and reduced motion (slower there); Off holds it still.
        _loop = new LiveLoop(this, dt =>
        {
            _spin.Advance(ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? dt : dt / 2);
            InvalidateVisual();
            return true;
        })
        { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <inheritdoc cref="StatusProperty"/>
    public EnrichColumnCellStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

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
    internal bool IsSpinning => _loop.IsRunning;

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

    /// <summary>Shows <paramref name="status"/> at once (a recycled row taking a new record).</summary>
    public void SetInstantly(EnrichColumnCellStatus status)
    {
        _instant = true;
        try
        {
            SetCurrentValue(StatusProperty, status);
            Reset(status);
        }
        finally
        {
            _instant = false;
        }
    }

    private bool _instant;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty && !_instant)
        {
            Swap(change.GetNewValue<EnrichColumnCellStatus>());
        }
        else if (change.Property == MutedBrushProperty)
        {
            _trackPen = null;
            _idlePen = null;
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
        _loop.Stop();
        _glyphs.RemoveAll(g => g.Leaving);
        foreach (var glyph in _glyphs)
        {
            glyph.Opacity?.Jump(1);
            glyph.Scale?.Jump(1);
            glyph.Draw?.Jump(1);
        }
    }

    private void Reset(EnrichColumnCellStatus status)
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
    }

    private void Swap(EnrichColumnCellStatus status)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || _glyphs.Count == 0)
        {
            Reset(status);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var easing = AiHeightBox.EaseOut;
        foreach (var old in _glyphs)
        {
            if (old.Leaving)
            {
                continue;
            }
            old.Leaving = true;
            old.Opacity ??= Value(1);
            var leaving = old;
            var fade = old.Opacity.AnimateTo(0, MotionSpec.Tween(SwapFade, easing));
            if (full)
            {
                old.Scale ??= Value(1);
                fade = old.Scale.AnimateTo(0.6, MotionSpec.Spring(AiGlyphMotion.Settle));
            }
            _ = fade.ContinueWith(_ => Dispatcher.Post(() => Remove(leaving)), TaskScheduler.Default);
        }
        var glyph = new Glyph { Status = status, Opacity = Value(0) };
        _glyphs.Add(glyph);
        _ = glyph.Opacity.AnimateTo(1, MotionSpec.Tween(SwapFade, easing));
        if (full)
        {
            glyph.Scale = Value(0.6);
            _ = glyph.Scale.AnimateTo(1, MotionSpec.Spring(AiGlyphMotion.Settle));
            if (status == EnrichColumnCellStatus.Done)
            {
                glyph.Draw = Value(0);
                _ = AiGlyphMotion.DrawCheck(glyph.Draw, CheckDraw, TimeSpan.Zero);
            }
        }
        UpdateSpin();
        InvalidateVisual();
    }

    private void Remove(Glyph glyph)
    {
        if (glyph.Leaving && glyph.Scale?.IsAnimating != true && glyph.Opacity?.IsAnimating != true && _glyphs.Remove(glyph))
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
        if (_glyphs.Any(g => g.Status == EnrichColumnCellStatus.Running))
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
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
            case EnrichColumnCellStatus.Running:
                DrawSpinner(context);
                break;
            case EnrichColumnCellStatus.Done:
                DrawCheck(context, Math.Clamp(glyph.Draw?.Value ?? 1, 0, 1));
                break;
            case EnrichColumnCellStatus.Failed:
                DrawIcon(context, ArcIcons.CircleAlert, DangerBrush);
                break;
            case EnrichColumnCellStatus.Queued:
                DrawIcon(context, ArcIcons.Clock, MutedBrush);
                break;
            default:
                DrawIdle(context);
                break;
        }
    }

    private void EnsureTrack(IBrush brush)
    {
        if (_trackPen is not null && ReferenceEquals(_trackBrush, brush))
        {
            return;
        }
        _trackBrush = brush;
        var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
        _trackPen = new ImmutablePen(new ImmutableSolidColorBrush(color, 0.2), 1.75);
        _idlePen = new ImmutablePen(new ImmutableSolidColorBrush(color, 0.45), 1.5, dashStyle: new ImmutableDashStyle([1.5, 2], 0),
            lineCap: PenLineCap.Round);
    }

    private void DrawIdle(DrawingContext context)
    {
        if (MutedBrush is not { } brush)
        {
            return;
        }
        EnsureTrack(brush);
        context.DrawEllipse(null, _idlePen, new Point(8, 8), 5.5, 5.5);
    }

    private void DrawSpinner(DrawingContext context)
    {
        if (MutedBrush is not { } brush)
        {
            return;
        }
        EnsureTrack(brush);
        context.DrawEllipse(null, _trackPen, new Point(8, 8), 6, 6);
        using (context.PushTransform(_spin.Turn(new Point(8, 8))))
        {
            context.DrawGeometry(null, Pen(AccentBrush ?? brush, 1.75, _pens), s_arc);
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
