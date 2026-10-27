using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Where the detail panel is.</summary>
internal enum KpiPanelPhase
{
    /// <summary>No panel: the grid.</summary>
    Closed,

    /// <summary>The panel grows into (or rests at) the detail view.</summary>
    Open,

    /// <summary>The panel folds back into its card.</summary>
    Closing
}

/// <summary>
/// The card grid of a <see cref="KpiDrilldown"/>: <see cref="Columns"/> equal columns <see cref="Gap"/> apart. With several
/// columns the rows share at least <see cref="RowsMinHeight"/> equally (Arc's <c>min-height: 548</c> with <c>1fr</c> rows,
/// so three columns give two 268px rows); a single column keeps every card at its own height.
/// </summary>
internal sealed class KpiCardGrid : Panel
{
    /// <summary>Space between cards (12; compact 10).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<KpiCardGrid, double>(nameof(Gap), 12);

    /// <summary>The smallest height the rows share with two or more columns (548).</summary>
    public static readonly StyledProperty<double> RowsMinHeightProperty =
        AvaloniaProperty.Register<KpiCardGrid, double>(nameof(RowsMinHeight), 548);

    private readonly List<double> _rowHeights = new();
    private int _columns = 3;

    static KpiCardGrid()
    {
        AffectsMeasure<KpiCardGrid>(GapProperty, RowsMinHeightProperty);
    }

    public int Columns
    {
        get => _columns;
        set
        {
            if (_columns != value)
            {
                _columns = Math.Max(1, value);
                InvalidateMeasure();
            }
        }
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double RowsMinHeight { get => GetValue(RowsMinHeightProperty); set => SetValue(RowsMinHeightProperty, value); }

    /// <summary>Row heights after the last measure (tests).</summary>
    internal IReadOnlyList<double> RowHeights => _rowHeights;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1100;
        var columns = _columns;
        var cellWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var rows = (Children.Count + columns - 1) / columns;
        _rowHeights.Clear();
        for (var r = 0; r < rows; r++)
        {
            _rowHeights.Add(0);
        }
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(cellWidth, double.PositiveInfinity));
            var r = i / columns;
            _rowHeights[r] = Math.Max(_rowHeights[r], child.DesiredSize.Height);
        }
        if (columns > 1 && rows > 0)
        {
            // 1fr rows: every row is as tall as the tallest, and together they fill the minimum height.
            var tallest = _rowHeights.Max();
            var share = (RowsMinHeight - Gap * (rows - 1)) / rows;
            var height = Math.Max(tallest, share);
            for (var r = 0; r < rows; r++)
            {
                _rowHeights[r] = height;
            }
        }
        var total = _rowHeights.Sum() + Gap * Math.Max(0, rows - 1);
        return new Size(width, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = _columns;
        var cellWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var y = 0.0;
        for (var r = 0; r < _rowHeights.Count; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                var i = r * columns + c;
                if (i >= Children.Count)
                {
                    break;
                }
                Children[i].Arrange(new Rect(c * (cellWidth + Gap), y, cellWidth, _rowHeights[r]));
            }
            y += _rowHeights[r] + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// The detail panel's surface: border (1px Border), radius 14, background from Surface to SurfaceRaised (480ms) and the
/// floating shadow, drawn at the panel's current (animated) size; its child clips the content.
/// </summary>
internal sealed class KpiDetailFrame : Decorator
{
    private readonly AnalyticsPalette _palette = new();

    public KpiDetailFrame()
    {
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>0 = Surface, 1 = SurfaceRaised.</summary>
    public double Raise { get; set; } = 1;

    /// <summary>Strength of the floating shadow (0–1).</summary>
    public double ShadowStrength { get; set; } = 1;

    /// <summary>Corner radius (14, the card's).</summary>
    public double Radius { get; set; } = 14;

    private void RefreshPalette()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(availableSize);
        return Child?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var fill = AnalyticsColors.Lerp(p.Surface, p.SurfaceRaised, Raise);
        var shadows = Scale(p.ShadowFloating, ShadowStrength);
        context.DrawRectangle(p.Brush(fill), p.Pen(p.Border, 1, cap: PenLineCap.Flat), new RoundedRect(rect.Deflate(0.5), Radius - 0.5), shadows);
    }

    private static BoxShadows Scale(BoxShadows shadows, double strength)
    {
        if (strength >= 0.999 || shadows.Count == 0)
        {
            return shadows;
        }
        if (strength <= 0.001)
        {
            return default;
        }
        var first = Fade(shadows[0], strength);
        if (shadows.Count == 1)
        {
            return new BoxShadows(first);
        }
        var rest = new BoxShadow[shadows.Count - 1];
        for (var i = 1; i < shadows.Count; i++)
        {
            rest[i - 1] = Fade(shadows[i], strength);
        }
        return new BoxShadows(first, rest);
    }

    private static BoxShadow Fade(BoxShadow shadow, double strength)
    {
        var c = shadow.Color;
        shadow.Color = Color.FromArgb((byte)Math.Round(c.A * strength), c.R, c.G, c.B);
        return shadow;
    }
}

/// <summary>
/// Clips the detail content to the panel's animated box while laying it out once at the panel's final width
/// (<see cref="ContentWidth"/>): the content never reflows while the panel grows or folds (notes 9).
/// </summary>
internal sealed class KpiDetailClip : Decorator
{
    public KpiDetailClip()
    {
        ClipToBounds = true;
    }

    /// <summary>The width the content is laid out at.</summary>
    public double ContentWidth { get; set; } = double.NaN;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(ContentWidth) ? ContentWidth : availableSize.Width;
        Child?.Measure(new Size(width, double.PositiveInfinity));
        return Child?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var width = double.IsFinite(ContentWidth) ? ContentWidth : finalSize.Width;
            child.Arrange(new Rect(0, 0, width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The card area of a <see cref="KpiDrilldown"/>: it lays out the card grid, the veil over it and the detail panel, and
/// runs the card → detail expansion (notes 6.1, 6.2, 6.10, 6.11). The panel's real width, height and position ride one
/// spring (<see cref="AnalyticsSprings.Expand"/>: visualDuration 0.5, no bounce, k ≈ 110, c ≈ 21) from the card's box to
/// the area inset by 8 and back; the content keeps its final layout and is clipped; the area's height follows the panel
/// (max of the grid and the panel + 16) every frame. Opening: the card hides at once, the veil fades in (240ms), the
/// panel's body fades in after 120ms over 480ms (.16, 1, .3, 1), the surface warms to SurfaceRaised over 480ms. Closing:
/// the body fades out in 120ms, the shadow over 480ms, the veil in 240ms, and the panel is removed when its spring rests
/// (the card shows again in that frame). Reduced motion: the panel appears at its final box, fading in 160ms and out
/// 160ms. Off: everything jumps.
/// </summary>
/// <remarks>
/// Why code-driven: the box chases a target that can move every frame (the card when closing, the area's width when the
/// block is resized, the content height when a range change rewraps a line) and an interrupt must keep the velocity; a
/// ControlTheme transition restarts from rest and cannot drive layout positions of a sibling.
/// </remarks>
internal sealed class KpiDrilldownStage : Panel
{
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _x = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _y = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _w = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _h = new(0, 0.05, 0.5);
    private AnalyticsTween _body = new(1);
    private AnalyticsTween _raise = new(1);
    private AnalyticsTween _shadow = new(1);
    private AnalyticsTween _fade = new(1);
    private bool _pendingOpen;
    private bool _instantOpen;

    /// <inheritdoc cref="StagePadding"/>
    public static readonly StyledProperty<double> StagePaddingProperty =
        AvaloniaProperty.Register<KpiDrilldownStage, double>(nameof(StagePadding), 16);

    /// <inheritdoc cref="Inset"/>
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<KpiDrilldownStage, double>(nameof(Inset), 8);

    static KpiDrilldownStage()
    {
        AffectsMeasure<KpiDrilldownStage>(StagePaddingProperty, InsetProperty);
    }

    public KpiDrilldownStage()
    {
        // Function, not decoration: the panel must finish opening or closing even when the block scrolls away.
        _loop = new LiveLoop(this, Tick) { MinimumLevel = ArcMotionLevel.Reduced, TracksViewport = false };
    }

    /// <summary>Raised when the panel has folded back and been removed.</summary>
    public event EventHandler? Closed;

    /// <summary>Padding around the grid (16; compact 12).</summary>
    public double StagePadding { get => GetValue(StagePaddingProperty); set => SetValue(StagePaddingProperty, value); }

    /// <summary>The panel's inset inside the area (8).</summary>
    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    /// <summary>The parts that fade with the panel's body (description, chart, legend, breakdown, the "vs" line).</summary>
    public IReadOnlyList<Visual> BodyParts { get; set; } = [];

    public KpiPanelPhase Phase { get; private set; }

    /// <summary>The card the panel grew from.</summary>
    public KpiCard? Source { get; private set; }

    internal KpiCardGrid? Grid => Children.OfType<KpiCardGrid>().FirstOrDefault();

    internal AnalyticsVeil? Veil => Children.OfType<AnalyticsVeil>().FirstOrDefault();

    internal KpiDetailFrame? Frame => Children.OfType<KpiDetailFrame>().FirstOrDefault();

    internal KpiDetailClip? ContentClip => Frame?.Child as KpiDetailClip;

    /// <summary>The panel's box now (tests).</summary>
    internal Rect PanelRect => new(_x.Value, _y.Value, Math.Max(0, _w.Value), Math.Max(0, _h.Value));

    /// <summary>The box the panel heads to.</summary>
    internal Rect PanelTarget => new(_x.Target, _y.Target, _w.Target, _h.Target);

    internal double BodyOpacity => _body.Value;

    internal double PanelOpacity => _fade.Value;

    internal bool IsMoving => _x.IsMoving || _y.IsMoving || _w.IsMoving || _h.IsMoving || _body.IsRunning || _fade.IsRunning;

    /// <summary>Grows the panel out of <paramref name="source"/> (or turns a closing panel back).</summary>
    public void Open(KpiCard source, bool instant = false)
    {
        _instantOpen = instant;
        var reopening = Phase == KpiPanelPhase.Closing && ReferenceEquals(source, Source);
        if (Source is { } old && !ReferenceEquals(old, source))
        {
            old.Opacity = 1;
        }
        Source = source;
        source.Opacity = 0;
        var wasClosed = Phase == KpiPanelPhase.Closed;
        Phase = KpiPanelPhase.Open;
        if (Frame is { } frame)
        {
            frame.IsVisible = true;
        }
        if (Veil is { } veil)
        {
            veil.IsShown = true;
        }
        _pendingOpen = wasClosed || !reopening;
        if (reopening)
        {
            var level = ArcMotion.GetEffectiveLevel(this);
            StartTween(ref _body, 1, ArcMotionPresets.Considered, AnalyticsEasings.Enter, TimeSpan.Zero, level);
            StartTween(ref _shadow, 1, ArcMotionPresets.Considered, AnalyticsEasings.Standard, TimeSpan.Zero, level);
            StartTween(ref _raise, 1, ArcMotionPresets.Considered, AnalyticsEasings.Standard, TimeSpan.Zero, level);
            StartTween(ref _fade, 1, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut, TimeSpan.Zero, level);
        }
        InvalidateMeasure();
        InvalidateArrange();
        _loop.Start();
    }

    /// <summary>Folds the panel back into its card.</summary>
    public void Close()
    {
        if (Phase != KpiPanelPhase.Open)
        {
            return;
        }
        Phase = KpiPanelPhase.Closing;
        _pendingOpen = false;
        if (Veil is { } veil)
        {
            veil.IsShown = false;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            Finish();
            return;
        }
        if (level == ArcMotionLevel.Full)
        {
            StartTween(ref _body, 0, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut, TimeSpan.Zero, level);
            StartTween(ref _shadow, 0, ArcMotionPresets.Considered, AnalyticsEasings.Standard, TimeSpan.Zero, level);
            StartTween(ref _raise, 0, ArcMotionPresets.Considered, AnalyticsEasings.Standard, TimeSpan.Zero, level);
            var source = SourceRect();
            _x.SpringTo(source.X, AnalyticsSprings.Expand);
            _y.SpringTo(source.Y, AnalyticsSprings.Expand);
            _w.SpringTo(source.Width, AnalyticsSprings.Expand);
            _h.SpringTo(source.Height, AnalyticsSprings.Expand);
        }
        else
        {
            StartTween(ref _fade, 0, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut, TimeSpan.Zero, level);
        }
        ApplyVisuals();
        _loop.Start();
    }

    /// <summary>Removes the panel at once (data gone, template change).</summary>
    public void Reset()
    {
        if (Phase != KpiPanelPhase.Closed)
        {
            Finish(raise: false);
        }
    }

    private static void StartTween(ref AnalyticsTween tween, double target, TimeSpan duration, Easing easing, TimeSpan delay, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            tween.Jump(target);
        }
        else
        {
            tween.Start(target, duration, easing, delay);
        }
    }

    private Rect SourceRect()
    {
        if (Source is not { } card || Grid is not { } grid)
        {
            return new Rect(Inset, Inset, Math.Max(0, Bounds.Width - 2 * Inset), 0);
        }
        var b = card.Bounds;
        return new Rect(grid.Bounds.X + b.X, grid.Bounds.Y + b.Y, b.Width, b.Height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1152;
        var grid = Grid;
        var gridHeight = 2 * StagePadding;
        if (grid is not null)
        {
            grid.Measure(new Size(Math.Max(0, width - 2 * StagePadding), double.PositiveInfinity));
            gridHeight += grid.DesiredSize.Height;
        }
        Veil?.Measure(new Size(width, gridHeight));
        var height = gridHeight;
        if (Frame is { } frame && Phase != KpiPanelPhase.Closed)
        {
            if (ContentClip is { } clip)
            {
                clip.ContentWidth = Math.Max(0, width - 2 * Inset);
            }
            frame.Measure(new Size(Math.Max(0, width - 2 * Inset), double.PositiveInfinity));
            var panel = _pendingOpen ? 0 : _h.Value;
            height = Math.Max(gridHeight, panel + 2 * Inset);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Grid?.Arrange(new Rect(StagePadding, StagePadding, Math.Max(0, finalSize.Width - 2 * StagePadding), Grid.DesiredSize.Height));
        Veil?.Arrange(new Rect(finalSize));
        if (Frame is not { } frame || Phase == KpiPanelPhase.Closed)
        {
            return finalSize;
        }
        var target = new Rect(Inset, Inset, Math.Max(0, finalSize.Width - 2 * Inset), frame.DesiredSize.Height);
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = level == ArcMotionLevel.Full && this.IsAttachedToVisualTree();
        if (_pendingOpen)
        {
            _pendingOpen = false;
            if (_instantOpen)
            {
                level = ArcMotionLevel.Off;
                animate = false;
                _instantOpen = false;
            }
            var source = SourceRect();
            if (animate)
            {
                Jump(source);
                SpringTo(target);
                _body.Jump(0);
                _body.Start(1, ArcMotionPresets.Considered, AnalyticsEasings.Enter, TimeSpan.FromMilliseconds(120));
                _raise.Jump(0);
                _raise.Start(1, ArcMotionPresets.Considered, AnalyticsEasings.Standard);
                _shadow.Jump(1);
                _fade.Jump(1);
            }
            else
            {
                Jump(target);
                _raise.Jump(1);
                _shadow.Jump(1);
                if (level == ArcMotionLevel.Reduced)
                {
                    _fade.Jump(0);
                    _fade.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.EaseOut);
                    _body.Jump(0);
                    _body.Start(1, TimeSpan.FromMilliseconds(100), AnalyticsEasings.EaseOut);
                }
                else
                {
                    _fade.Jump(1);
                    _body.Jump(1);
                }
            }
            ApplyVisuals();
            _loop.Start();
            // The area's height follows the panel: measure again with the panel's first box.
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateMeasure, Avalonia.Threading.DispatcherPriority.Render);
        }
        else
        {
            var goal = Phase == KpiPanelPhase.Closing && level == ArcMotionLevel.Full ? SourceRect() : target;
            if (!SameTarget(goal))
            {
                if (animate)
                {
                    SpringTo(goal);
                    _loop.Start();
                }
                else if (Phase == KpiPanelPhase.Open)
                {
                    Jump(goal);
                }
            }
        }
        frame.Arrange(PanelRect);
        return finalSize;
    }

    private bool SameTarget(Rect goal) =>
        Math.Abs(_x.Target - goal.X) < 0.01 && Math.Abs(_y.Target - goal.Y) < 0.01 &&
        Math.Abs(_w.Target - goal.Width) < 0.01 && Math.Abs(_h.Target - goal.Height) < 0.01;

    private void Jump(Rect rect)
    {
        _x.Jump(rect.X);
        _y.Jump(rect.Y);
        _w.Jump(rect.Width);
        _h.Jump(rect.Height);
    }

    private void SpringTo(Rect rect)
    {
        _x.SpringTo(rect.X, AnalyticsSprings.Expand);
        _y.SpringTo(rect.Y, AnalyticsSprings.Expand);
        _w.SpringTo(rect.Width, AnalyticsSprings.Expand);
        _h.SpringTo(rect.Height, AnalyticsSprings.Expand);
    }

    private bool Tick(double dt)
    {
        if (Phase == KpiPanelPhase.Closed)
        {
            return false;
        }
        var moving = _x.Step(dt) | _y.Step(dt) | _w.Step(dt) | _h.Step(dt);
        var fading = _body.Advance(dt) | _raise.Advance(dt) | _shadow.Advance(dt) | _fade.Advance(dt);
        ApplyVisuals();
        InvalidateMeasure();
        InvalidateArrange();
        if (Phase == KpiPanelPhase.Closing)
        {
            var reduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
            var done = reduced ? !_fade.IsRunning : !moving;
            if (done)
            {
                Finish();
                return false;
            }
        }
        return moving || fading || _pendingOpen;
    }

    private void ApplyVisuals()
    {
        var body = Math.Clamp(_body.Value, 0, 1);
        foreach (var part in BodyParts)
        {
            part.Opacity = body;
        }
        if (Frame is { } frame)
        {
            frame.Opacity = Math.Clamp(_fade.Value, 0, 1);
            frame.Raise = Math.Clamp(_raise.Value, 0, 1);
            frame.ShadowStrength = Math.Clamp(_shadow.Value, 0, 1);
            frame.InvalidateVisual();
        }
    }

    private void Finish(bool raise = true)
    {
        Phase = KpiPanelPhase.Closed;
        _pendingOpen = false;
        Jump(SourceRect());
        _body.Jump(1);
        _fade.Jump(1);
        _raise.Jump(1);
        _shadow.Jump(1);
        ApplyVisuals();
        if (Frame is { } frame)
        {
            frame.IsVisible = false;
        }
        if (Veil is { } veil)
        {
            veil.IsShown = false;
        }
        if (Source is { } card)
        {
            card.Opacity = 1;
        }
        Source = null;
        _loop.Stop();
        InvalidateMeasure();
        if (raise)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
