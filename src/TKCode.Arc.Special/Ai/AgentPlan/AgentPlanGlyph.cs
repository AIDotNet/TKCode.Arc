using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What <see cref="AgentPlanGlyph"/> draws.</summary>
internal enum AgentPlanGlyphKind
{
    Pending,
    Active,
    Paused,
    Done,
    Failed,
    Skipped
}

/// <summary>
/// A running step's status mark, 16px centred in the 20px lead column (notes 3.2), each state its own shape so it never
/// rests on color: a ring (pending), a ring track with a quarter arc that turns once every 800ms (active), the ring with
/// two pause bars (paused), a disc with a check drawn as a stroke 45ms after it appears over 200ms (done), a disc with an
/// exclamation mark (failed), and a dashed ring with a bar (skipped). A change of state swaps the marks with the plan's
/// crossfade. Self-drawn: the spin, the stroke draw and the swap run on code clocks (a LiveLoop and MotionValues), which a
/// ControlTheme cannot express; every frame reuses cached pens and geometry.
/// </summary>
internal sealed class AgentPlanGlyph : Control
{
    public static readonly StyledProperty<AgentPlanGlyphKind> KindProperty =
        AvaloniaProperty.Register<AgentPlanGlyph, AgentPlanGlyphKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> RingInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(RingInk));
    public static readonly StyledProperty<IBrush?> TrackInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(TrackInk));
    public static readonly StyledProperty<IBrush?> ArcInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(ArcInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> SecondaryInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(SecondaryInk));
    public static readonly StyledProperty<IBrush?> SuccessInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(SuccessInk));
    public static readonly StyledProperty<IBrush?> DangerInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(DangerInk));
    public static readonly StyledProperty<IBrush?> CutoutInkProperty = AvaloniaProperty.Register<AgentPlanGlyph, IBrush?>(nameof(CutoutInk));

    private const double Box = 16;
    private const double Ring = 6.25;
    private const double Disc = 7;
    private const double Stroke = 1.5;
    private const double InkStroke = 1.6;
    private static readonly Point C = new(8, 8);

    // The check: two strokes, bounding box x 5-11, y 6-10.3, 8.8 units long.
    private static readonly AiStroke s_check = new(new Point(5, 8.2), new Point(7.1, 10.3), new Point(11, 6));

    private static readonly EllipseGeometry s_ring = new(new Rect(C.X - Ring, C.Y - Ring, 2 * Ring, 2 * Ring));
    private static readonly EllipseGeometry s_disc = new(new Rect(C.X - Disc, C.Y - Disc, 2 * Disc, 2 * Disc));
    private static readonly EllipseGeometry s_dot = new(new Rect(8 - 0.95, 11.1 - 0.95, 1.9, 1.9));
    private static readonly StreamGeometry s_arc = AiSpin.QuarterArc(C, Ring);

    private readonly AiSpin _spin;
    private readonly MotionValue<double> _swap;
    private readonly MotionValue<double> _check;
    private AgentPlanGlyphKind _previous;
    private bool _hasPrevious;
    private bool _swapFull;
    private bool _inksDirty = true;
    private IPen? _ringPen;
    private IPen? _trackPen;
    private IPen? _arcPen;
    private IPen? _mutedPen;
    private IPen? _barPen;
    private IPen? _dashPen;
    private IPen? _skipLinePen;
    private IPen? _cutoutPen;

    static AgentPlanGlyph()
    {
        AffectsRender<AgentPlanGlyph>(RingInkProperty, TrackInkProperty, ArcInkProperty, MutedInkProperty, SecondaryInkProperty,
            SuccessInkProperty, DangerInkProperty, CutoutInkProperty);
    }

    public AgentPlanGlyph()
    {
        Width = 20;
        Height = 20;
        IsHitTestVisible = false;
        _spin = new AiSpin(this, AgentPlanMotion.SpinTurnSeconds, InvalidateVisual);
        _swap = new MotionValue<double>(this, 1, MotionVectors.Double);
        _swap.Changed += _ => InvalidateVisual();
        _check = new MotionValue<double>(this, 1, MotionVectors.Double);
        _check.Changed += _ => InvalidateVisual();
    }

    public AgentPlanGlyphKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IBrush? RingInk { get => GetValue(RingInkProperty); set => SetValue(RingInkProperty, value); }
    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }
    public IBrush? ArcInk { get => GetValue(ArcInkProperty); set => SetValue(ArcInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }
    public IBrush? CutoutInk { get => GetValue(CutoutInkProperty); set => SetValue(CutoutInkProperty, value); }

    /// <summary>The spinner's angle in degrees (0 at rest).</summary>
    public double Angle => _spin.Angle;

    /// <summary>How much of the check is drawn (0-1).</summary>
    public double CheckProgress => _check.Value;

    /// <summary>True while the arc turns.</summary>
    public bool IsSpinning => _spin.IsRunning;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            OnKindChanged(change.GetOldValue<AgentPlanGlyphKind>(), change.GetNewValue<AgentPlanGlyphKind>());
        }
        else if (change.Property.Name.EndsWith("Ink", StringComparison.Ordinal))
        {
            _inksDirty = true;
        }
    }

    private void OnKindChanged(AgentPlanGlyphKind old, AgentPlanGlyphKind kind)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && level != ArcMotionLevel.Off;
        _swapFull = level == ArcMotionLevel.Full;
        if (kind == AgentPlanGlyphKind.Active && old != AgentPlanGlyphKind.Paused)
        {
            _spin.Reset();
        }
        if (animate)
        {
            _previous = old;
            _hasPrevious = true;
            _swap.Jump(0);
            _ = _swap.AnimateTo(1, MotionSpec.Tween(_swapFull ? AgentPlanMotion.SwapEnter : ArcMotionPresets.Instant, s_linear));
        }
        else
        {
            _hasPrevious = false;
            _swap.Jump(1);
        }
        if (kind == AgentPlanGlyphKind.Done && animate && _swapFull)
        {
            _check.Jump(0);
            _ = AiGlyphMotion.DrawCheck(_check, AgentPlanMotion.CheckDraw, AgentPlanMotion.CheckDelay);
        }
        else
        {
            _check.Jump(1);
        }
        UpdateSpin();
        InvalidateVisual();
    }

    private static readonly Avalonia.Animation.Easings.LinearEasing s_linear = new();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSpin();
    }

    private void UpdateSpin()
    {
        _spin.Run(Kind == AgentPlanGlyphKind.Active);
    }

    private void EnsurePens()
    {
        if (!_inksDirty)
        {
            return;
        }
        _inksDirty = false;
        _ringPen = new ImmutablePenOf(RingInk, Stroke).Pen;
        _trackPen = new ImmutablePenOf(TrackInk, Stroke).Pen;
        _arcPen = new ImmutablePenOf(ArcInk, Stroke).Pen;
        _mutedPen = new ImmutablePenOf(MutedInk, Stroke).Pen;
        _barPen = new ImmutablePenOf(SecondaryInk, Stroke).Pen;
        _skipLinePen = new ImmutablePenOf(MutedInk, Stroke).Pen;
        // SVG dashes are in px; Avalonia's are in stroke widths.
        _dashPen = new ImmutablePen(MutedInk?.ToImmutable(), Stroke, new ImmutableDashStyle([2.45 / Stroke, 2.45 / Stroke], 0), PenLineCap.Flat);
        _cutoutPen = new ImmutablePenOf(CutoutInk, InkStroke).Pen;
    }

    public override void Render(DrawingContext context)
    {
        EnsurePens();
        var offset = new Point((Bounds.Width - Box) / 2, (Bounds.Height - Box) / 2);
        var p = Math.Clamp(_swap.Value, 0, 1);
        if (_hasPrevious && p < 1)
        {
            // Old mark out in the first 60ms, new one in over ~100ms and up 3px over the whole 145ms (enter curve).
            var exit = _swapFull ? Math.Clamp(p * AgentPlanMotion.SwapEnter.TotalMilliseconds / AgentPlanMotion.SwapExit.TotalMilliseconds, 0, 1) : p;
            if (exit < 1)
            {
                using (context.PushOpacity(1 - exit))
                {
                    DrawKind(context, _previous, offset);
                }
            }
            var enter = _swapFull ? ArcEasings.Enter.Ease(Math.Clamp(p * 145.0 / 100.0, 0, 1)) : p;
            var rise = _swapFull ? AgentPlanMotion.SwapRise * (1 - ArcEasings.Enter.Ease(p)) : 0;
            using (context.PushOpacity(enter))
            {
                DrawKind(context, Kind, offset + new Point(0, rise));
            }
            return;
        }
        DrawKind(context, Kind, offset);
    }

    private void DrawKind(DrawingContext context, AgentPlanGlyphKind kind, Point offset)
    {
        using var _ = context.PushTransform(Matrix.CreateTranslation(offset.X, offset.Y));
        switch (kind)
        {
            case AgentPlanGlyphKind.Pending:
                context.DrawGeometry(null, _ringPen, s_ring);
                break;
            case AgentPlanGlyphKind.Active:
                context.DrawGeometry(null, _trackPen, s_ring);
                using (context.PushTransform(_spin.Turn(C)))
                {
                    context.DrawGeometry(null, _arcPen, s_arc);
                }
                break;
            case AgentPlanGlyphKind.Paused:
                context.DrawGeometry(null, _mutedPen, s_ring);
                context.DrawLine(_barPen!, new Point(6.6, 5.9), new Point(6.6, 10.1));
                context.DrawLine(_barPen!, new Point(9.4, 5.9), new Point(9.4, 10.1));
                break;
            case AgentPlanGlyphKind.Done:
                context.DrawGeometry(SuccessInk, null, s_disc);
                DrawCheck(context, Math.Clamp(_check.Value, 0, 1));
                break;
            case AgentPlanGlyphKind.Failed:
                context.DrawGeometry(DangerInk, null, s_disc);
                context.DrawLine(_cutoutPen!, new Point(8, 4.6), new Point(8, 8.2));
                context.DrawGeometry(CutoutInk, null, s_dot);
                break;
            case AgentPlanGlyphKind.Skipped:
                context.DrawGeometry(null, _dashPen, s_ring);
                context.DrawLine(_skipLinePen!, new Point(5.6, 8), new Point(10.4, 8));
                break;
        }
    }

    private void DrawCheck(DrawingContext context, double progress)
    {
        if (progress <= 0 || _cutoutPen is null)
        {
            return;
        }
        s_check.Draw(context, _cutoutPen, progress);
    }

    /// <summary>A round-capped, round-joined immutable pen of a brush (no pen without a brush).</summary>
    private readonly struct ImmutablePenOf(IBrush? brush, double thickness)
    {
        public IPen? Pen { get; } = brush is null
            ? null
            : new Pen(brush.ToImmutable(), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round).ToImmutable();
    }
}
