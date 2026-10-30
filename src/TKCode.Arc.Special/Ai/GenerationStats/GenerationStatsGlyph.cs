using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status icon beside the status word of <see cref="GenerationStats"/>, a different shape per status so color is never
/// the only cue: waiting and streaming draw a ring track at 20% with a quarter arc turning once every 800ms (linear; the arc
/// is the accent while streaming); done a check in Success that draws itself once (330ms on the standard curve, 50ms after
/// it appears) when <see cref="PlayCheck"/> is called; stopped Lucide square and idle a small hollow circle in TextMuted;
/// failed Lucide circle-alert in Danger. Reduced motion and Off: the arc holds still and the check is drawn whole.
/// Decorative: the word beside it says the status.
/// <para>
/// Drawn in code (<see cref="AiSpin"/> on a <see cref="LiveLoop"/>, a <see cref="MotionValue{T}"/> for the check) because
/// a ControlTheme cannot turn a drawn arc or cut a stroke.
/// </para>
/// </summary>
internal sealed class GenerationStatsGlyph : Control
{
    /// <summary>The status shown.</summary>
    public static readonly StyledProperty<GenerationStatus> StatusProperty =
        AvaloniaProperty.Register<GenerationStatsGlyph, GenerationStatus>(nameof(Status));

    /// <summary>The spinner's track, the square and the idle circle.</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<GenerationStatsGlyph, IBrush?>(nameof(MutedBrush));

    /// <summary>The streaming arc.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<GenerationStatsGlyph, IBrush?>(nameof(AccentBrush));

    /// <summary>The check.</summary>
    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<GenerationStatsGlyph, IBrush?>(nameof(SuccessBrush));

    /// <summary>The alert.</summary>
    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<GenerationStatsGlyph, IBrush?>(nameof(DangerBrush));

    /// <summary>One turn of the spinner, in seconds.</summary>
    internal const double TurnSeconds = 0.8;

    /// <summary>The check's draw.</summary>
    internal static readonly TimeSpan CheckDraw = TimeSpan.FromMilliseconds(330);

    /// <summary>The wait before the check draws.</summary>
    internal static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(50);

    private static readonly Geometry s_check = StreamGeometry.Parse("M3.25 8.25 L6.5 11.75 L12.75 4.75");
    private static readonly AiStroke s_checkStroke = new(new Point(3.25, 8.25), new Point(6.5, 11.75), new Point(12.75, 4.75));
    private static readonly StreamGeometry s_arc = AiSpin.QuarterArc(new Point(8, 8), 6);

    private readonly AiSpin _spin;
    private readonly Dictionary<IBrush, IPen> _pens = new();
    private readonly Dictionary<IBrush, IPen> _iconPens = new();
    private MotionValue<double>? _draw;
    private IPen? _trackPen;
    private IBrush? _trackBrush;

    public GenerationStatsGlyph()
    {
        _spin = new AiSpin(this, TurnSeconds, InvalidateVisual);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public GenerationStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="MutedBrushProperty"/>
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="SuccessBrushProperty"/>
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    /// <inheritdoc cref="DangerBrushProperty"/>
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    /// <summary>How much of the check is drawn (1 when it is whole).</summary>
    internal double CheckProgress => _draw?.Value ?? 1;

    /// <summary>True while the spinner turns.</summary>
    internal bool IsSpinning => _spin.IsRunning;

    static GenerationStatsGlyph()
    {
        AffectsRender<GenerationStatsGlyph>(StatusProperty, MutedBrushProperty, AccentBrushProperty, SuccessBrushProperty, DangerBrushProperty);
    }

    /// <summary>Draws the check once (full motion only; otherwise it stays whole).</summary>
    public void PlayCheck()
    {
        if (Status != GenerationStatus.Done || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        if (_draw is null)
        {
            _draw = new MotionValue<double>(this, 0, MotionVectors.Double);
            _draw.Changed += _ => InvalidateVisual();
        }
        _draw.Jump(0);
        _ = AiGlyphMotion.DrawCheck(_draw, CheckDraw, CheckDelay);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            _spin.Run(Spins);
        }
        else if (change.Property == MutedBrushProperty)
        {
            _trackPen = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _spin.Run(Spins);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _draw?.Jump(1);
    }

    private bool Spins => Status is GenerationStatus.Waiting or GenerationStatus.Streaming;

    protected override Size MeasureOverride(Size availableSize) => new(14, 14);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var unit = size / 16.0;
        var matrix = Matrix.CreateScale(unit, unit) *
                     Matrix.CreateTranslation((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        using (context.PushTransform(matrix))
        {
            switch (Status)
            {
                case GenerationStatus.Waiting:
                    DrawSpinner(context, MutedBrush);
                    break;
                case GenerationStatus.Streaming:
                    DrawSpinner(context, AccentBrush ?? MutedBrush);
                    break;
                case GenerationStatus.Done:
                    DrawCheck(context, Math.Clamp(CheckProgress, 0, 1));
                    break;
                case GenerationStatus.Error:
                    DrawIcon(context, ArcIcons.CircleAlert, DangerBrush);
                    break;
                case GenerationStatus.Stopped:
                    DrawIcon(context, ArcIcons.Square, MutedBrush);
                    break;
                default:
                    if (MutedBrush is { } muted)
                    {
                        context.DrawEllipse(null, Pen(muted, 1.75, _pens), new Point(8, 8), 4, 4);
                    }
                    break;
            }
        }
    }

    private void DrawSpinner(DrawingContext context, IBrush? arc)
    {
        if (MutedBrush is not { } muted || arc is null)
        {
            return;
        }
        if (_trackPen is null || !ReferenceEquals(_trackBrush, muted))
        {
            _trackBrush = muted;
            var color = muted is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _trackPen = new ImmutablePen(new ImmutableSolidColorBrush(color, 0.2), 1.75);
        }
        context.DrawEllipse(null, _trackPen, new Point(8, 8), 6, 6);
        using (context.PushTransform(_spin.Turn(new Point(8, 8))))
        {
            context.DrawGeometry(null, Pen(arc, 1.75, _pens), s_arc);
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
            context.DrawGeometry(null, Pen(brush, 2, _iconPens), icon);
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
