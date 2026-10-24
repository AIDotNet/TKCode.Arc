using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Immutable;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The run state at the end of a <see cref="WorkflowNodeCard"/> (Arc <c>.state</c>): an empty ring while waiting, a
/// spinning ring while running, the time and a check when done, a minus when skipped, an alert mark when failed. A change
/// swaps the marks like Arc's <c>AnimatePresence popLayout</c>: the new mark grows from 0.6 while the old one shrinks
/// away, both on the snappy spring; only the new mark takes room.
/// </summary>
/// <remarks>
/// Code-driven motion: the outgoing mark is a snapshot of a state that no longer exists (a ControlTheme can only style
/// the current one), and the spinner is a continuous loop.
/// </remarks>
internal sealed class WorkflowStateMark : Control
{
    public static readonly StyledProperty<WorkflowNodeState> StateProperty =
        AvaloniaProperty.Register<WorkflowStateMark, WorkflowNodeState>(nameof(State));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<WorkflowStateMark, string?>(nameof(Meta));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowStateMark, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<WorkflowStateMark, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<WorkflowStateMark, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<WorkflowStateMark, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<WorkflowStateMark, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(Gap), 6);

    public static readonly StyledProperty<double> RingSizeProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(RingSize), 10);

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(RingThickness), 1.5);

    public static readonly StyledProperty<double> SpinnerSizeProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(SpinnerSize), 13);

    public static readonly StyledProperty<double> SpinnerTrackShareProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(SpinnerTrackShare), 0.22);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WorkflowStateMark, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WorkflowStateMark, double>(nameof(FontSize), 12);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    /// <summary>One turn of the running ring in seconds (Arc <c>animation: spin .7s linear infinite</c>).</summary>
    private const double TurnSeconds = 0.7;

    private readonly LiveLoop _spin;
    private MotionValue<double>? _swap;
    private Mark _current;
    private Mark? _outgoing;
    private double _angle;

    // Cached per brush and thickness, so a spinning frame allocates nothing.
    private IPen? _ringPen;
    private IPen? _trackPen;
    private IPen? _arcPen;
    private IPen? _successPen;
    private IPen? _dangerPen;
    private IPen? _mutedPen;
    private StreamGeometry? _arc;
    private double _arcRadius;

    static WorkflowStateMark()
    {
        // Decorative: the card's name says it all.
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowStateMark>(AccessibilityView.Raw);
        AffectsMeasure<WorkflowStateMark>(MetaProperty, GlyphSizeProperty, GapProperty, RingSizeProperty, SpinnerSizeProperty,
            FontFamilyProperty, FontSizeProperty);
        AffectsRender<WorkflowStateMark>(AccentBrushProperty, SuccessBrushProperty, DangerBrushProperty, MutedBrushProperty,
            RingBrushProperty, RingThicknessProperty, SpinnerTrackShareProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowStateMark>(false);
    }

    public WorkflowStateMark()
    {
        _current = new Mark(WorkflowNodeState.Idle, null);
        _spin = new LiveLoop(this, Spin);
    }

    public WorkflowNodeState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>The time shown before the check when done ("118 ms").</summary>
    public string? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? SuccessBrush
    {
        get => GetValue(SuccessBrushProperty);
        set => SetValue(SuccessBrushProperty, value);
    }

    public IBrush? DangerBrush
    {
        get => GetValue(DangerBrushProperty);
        set => SetValue(DangerBrushProperty, value);
    }

    public IBrush? MutedBrush
    {
        get => GetValue(MutedBrushProperty);
        set => SetValue(MutedBrushProperty, value);
    }

    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double RingSize
    {
        get => GetValue(RingSizeProperty);
        set => SetValue(RingSizeProperty, value);
    }

    public double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public double SpinnerSize
    {
        get => GetValue(SpinnerSizeProperty);
        set => SetValue(SpinnerSizeProperty, value);
    }

    public double SpinnerTrackShare
    {
        get => GetValue(SpinnerTrackShareProperty);
        set => SetValue(SpinnerTrackShareProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>True while the old and new marks are swapping.</summary>
    internal bool IsSwapping => _outgoing is not null;

    /// <summary>The ring's rotation in degrees (tests).</summary>
    internal double Angle => _angle;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            SwapTo(new Mark(State, State == WorkflowNodeState.Done ? Meta : null));
        }
        else if (change.Property == MetaProperty && State == WorkflowNodeState.Done)
        {
            // The time arrives with the state or just after it: update the mark in place.
            _current.Dispose();
            _current = new Mark(State, Meta);
            InvalidateMeasure();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == MutedBrushProperty)
        {
            _current.DropText();
            _outgoing?.DropText();
        }
        if (change.Property == AccentBrushProperty || change.Property == SuccessBrushProperty ||
            change.Property == DangerBrushProperty || change.Property == MutedBrushProperty ||
            change.Property == RingBrushProperty || change.Property == RingThicknessProperty ||
            change.Property == SpinnerTrackShareProperty)
        {
            _ringPen = _trackPen = _arcPen = _successPen = _dangerPen = _mutedPen = null;
        }
    }

    private void SwapTo(Mark next)
    {
        _outgoing?.Dispose();
        _outgoing = null;
        var previous = _current;
        _current = next;
        UpdateSpin();
        InvalidateMeasure();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || previous.State == next.State)
        {
            previous.Dispose();
            InvalidateVisual();
            return;
        }
        _outgoing = previous;
        _swap ??= CreateSwap();
        _swap.Jump(0);
        // Full: the snappy spring scales and fades; reduced: a short fade only.
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Spring(ArcSpring.Snappy)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = Finish(_swap.AnimateTo(1, spec));
    }

    private MotionValue<double> CreateSwap()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private async Task Finish(Task<bool> animation)
    {
        if (await animation && _outgoing is { } outgoing)
        {
            outgoing.Dispose();
            _outgoing = null;
            UpdateSpin();
            InvalidateVisual();
        }
    }

    private void UpdateSpin()
    {
        if (_current.State == WorkflowNodeState.Running || _outgoing?.State == WorkflowNodeState.Running)
        {
            _spin.Start();
        }
        else
        {
            _spin.Stop();
        }
    }

    private bool Spin(double seconds)
    {
        _angle = (_angle + seconds / TurnSeconds * 360) % 360;
        InvalidateVisual();
        return true;
    }

    protected override Size MeasureOverride(Size availableSize) => MarkSize(_current);

    private Size MarkSize(Mark mark)
    {
        var glyph = GlyphSize;
        switch (mark.State)
        {
            case WorkflowNodeState.Idle:
                return default;
            case WorkflowNodeState.Pending:
                return new Size(RingSize, glyph);
            case WorkflowNodeState.Running:
                return new Size(SpinnerSize, Math.Max(glyph, SpinnerSize));
            case WorkflowNodeState.Done when TextOf(mark) is { } text:
                return new Size(text.WidthIncludingTrailingWhitespace + Gap + glyph, Math.Max(glyph, text.Height));
            default:
                return new Size(glyph, glyph);
        }
    }

    private TextLayout? TextOf(Mark mark)
    {
        if (string.IsNullOrEmpty(mark.Meta))
        {
            return null;
        }
        return mark.Text ??= new TextLayout(mark.Meta, new Typeface(FontFamily), FontSize, MutedBrush ?? Brushes.Gray,
            TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 1, s_tabular);
    }

    public override void Render(DrawingContext context)
    {
        var progress = _outgoing is null || _swap is null ? 1 : _swap.Value;
        var full = ArcMotion.IsFull(this);
        if (_outgoing is { } outgoing)
        {
            var fade = Math.Clamp(1 - progress, 0, 1);
            DrawMark(context, outgoing, fade, full ? 1 - 0.4 * progress : 1);
        }
        DrawMark(context, _current, Math.Clamp(progress, 0, 1), _outgoing is not null && full ? 0.6 + 0.4 * progress : 1);
    }

    /// <summary>Draws a mark right-aligned and vertically centered, scaled about its own center.</summary>
    private void DrawMark(DrawingContext context, Mark mark, double opacity, double scale)
    {
        if (mark.State == WorkflowNodeState.Idle || opacity <= 0.001)
        {
            return;
        }
        var size = MarkSize(mark);
        var bounds = Bounds.Size;
        var origin = new Point(bounds.Width - size.Width, (bounds.Height - size.Height) / 2);
        var center = new Point(origin.X + size.Width / 2, origin.Y + size.Height / 2);
        using var _ = context.PushOpacity(opacity);
        using var __ = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                             Matrix.CreateTranslation(center.X, center.Y));
        var glyph = GlyphSize;
        var glyphRect = new Rect(origin.X + size.Width - glyph, origin.Y + (size.Height - glyph) / 2, glyph, glyph);
        switch (mark.State)
        {
            case WorkflowNodeState.Pending:
            {
                var r = RingSize / 2;
                var c = new Point(origin.X + r, origin.Y + size.Height / 2);
                _ringPen ??= RingBrush is { } ring ? new ImmutablePen(ring.ToImmutable(), RingThickness) : null;
                if (_ringPen is not null)
                {
                    context.DrawEllipse(null, _ringPen, c, r - RingThickness / 2, r - RingThickness / 2);
                }
                break;
            }
            case WorkflowNodeState.Running:
                DrawSpinner(context, new Point(origin.X + SpinnerSize / 2, origin.Y + size.Height / 2));
                break;
            case WorkflowNodeState.Done:
                if (TextOf(mark) is { } text)
                {
                    text.Draw(context, new Point(origin.X, origin.Y + (size.Height - text.Height) / 2));
                }
                DrawGlyph(context, ArcIcons.Check, glyphRect, _successPen ??= GlyphPen(SuccessBrush));
                break;
            case WorkflowNodeState.Skipped:
                DrawGlyph(context, ArcIcons.Minus, glyphRect, _mutedPen ??= GlyphPen(MutedBrush));
                break;
            case WorkflowNodeState.Failed:
                DrawGlyph(context, ArcIcons.CircleAlert, glyphRect, _dangerPen ??= GlyphPen(DangerBrush));
                break;
        }
    }

    /// <summary>Arc's spinner: a full ring in the accent at 22% under a quarter in the accent (<c>border-top-color</c>).</summary>
    private void DrawSpinner(DrawingContext context, Point center)
    {
        if (AccentBrush is not ISolidColorBrush accent)
        {
            return;
        }
        var thickness = RingThickness;
        var radius = SpinnerSize / 2 - thickness / 2;
        _trackPen ??= new ImmutablePen(
            new ImmutableSolidColorBrush(Oklch.WithAlpha(accent.Color, accent.Color.A / 255.0 * SpinnerTrackShare)), thickness);
        _arcPen ??= new ImmutablePen(new ImmutableSolidColorBrush(accent.Color), thickness);
        context.DrawEllipse(null, _trackPen, center, radius, radius);
        if (_arc is null || _arcRadius != radius)
        {
            // A CSS border-top on a round box covers the quarter from 315° to 45° around the top.
            _arcRadius = radius;
            _arc = new StreamGeometry();
            using var g = _arc.Open();
            var start = -135 * Math.PI / 180;
            var end = -45 * Math.PI / 180;
            g.BeginFigure(new Point(radius * Math.Cos(start), radius * Math.Sin(start)), false);
            g.ArcTo(new Point(radius * Math.Cos(end), radius * Math.Sin(end)), new Size(radius, radius), 0, false,
                SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        using var _ = context.PushTransform(Matrix.CreateRotation(_angle * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y));
        context.DrawGeometry(null, _arcPen, _arc);
    }

    private static IPen? GlyphPen(IBrush? brush)
        => brush is null ? null : new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    /// <summary>A 24-unit Lucide glyph at <paramref name="rect"/> with Lucide's 2-unit stroke (lucide-react <c>strokeWidth={2}</c>).</summary>
    private static void DrawGlyph(DrawingContext context, Geometry glyph, Rect rect, IPen? pen)
    {
        if (pen is null)
        {
            return;
        }
        var scale = rect.Width / 24;
        using var _ = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(rect.X, rect.Y));
        context.DrawGeometry(null, pen, glyph);
    }

    /// <summary>A state with its time, and the text laid out for it.</summary>
    private sealed class Mark(WorkflowNodeState state, string? meta) : IDisposable
    {
        public WorkflowNodeState State { get; } = state;

        public string? Meta { get; } = meta;

        public TextLayout? Text { get; set; }

        public void DropText()
        {
            Text?.Dispose();
            Text = null;
        }

        public void Dispose() => DropText();
    }
}
