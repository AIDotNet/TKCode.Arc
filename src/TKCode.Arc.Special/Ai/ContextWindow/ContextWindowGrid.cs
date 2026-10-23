using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a new arrangement of the grid reaches the screen (each changed cell recolors over 240ms after its delay).</summary>
internal enum ContextWindowWave
{
    /// <summary>At once (the first arrangement, reduced motion, a theme change).</summary>
    None,

    /// <summary>A message was appended: 9ms per cell from the first changed cell, at most 360ms.</summary>
    Append,

    /// <summary>Cells were inserted or released (a file, a compaction, any other change): 3ms per cell, at most 360ms.</summary>
    Insert,

    /// <summary>The window changed (another model): 1.4ms per cell from cell 0, no cap.</summary>
    Relayout
}

/// <summary>
/// The 200 cells of a <see cref="ContextWindow"/> and its dashed threshold line, drawn by one control on one clock (a
/// <see cref="LiveLoop"/>, Full motion only) instead of 200 elements with their own transitions: every cell keeps the
/// ink it starts from, its target kind and the time its 240ms recolor starts, evaluated in OKLab on the (.22, 1, .36, 1)
/// curve when a frame is drawn. On top of the colors: the fold sweep (opacity 1 → 0.35 at 55% → 0.5 over 720ms, each
/// segment eased, staggered oldest to newest) and the legend hover (cells of other parts fade to 0.28 in 160ms). Frames
/// allocate nothing once the brushes are cached. Under reduced motion and Off every change lands at once.
/// </summary>
internal sealed class ContextWindowGrid : Control
{
    /// <summary>Defines the <see cref="CellGap"/> property.</summary>
    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<ContextWindowGrid, double>(nameof(CellGap), 3);

    /// <summary>Defines the <see cref="CellRadius"/> property.</summary>
    public static readonly StyledProperty<double> CellRadiusProperty =
        AvaloniaProperty.Register<ContextWindowGrid, double>(nameof(CellRadius), 3);

    /// <summary>Defines the <see cref="ThresholdRowHeight"/> property.</summary>
    public static readonly StyledProperty<double> ThresholdRowHeightProperty =
        AvaloniaProperty.Register<ContextWindowGrid, double>(nameof(ThresholdRowHeight), 5);

    /// <summary>Defines the <see cref="DimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<ContextWindowGrid, double>(nameof(DimOpacity), 0.28);

    public static readonly StyledProperty<IBrush?> SystemInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(SystemInk));
    public static readonly StyledProperty<IBrush?> ToolsInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(ToolsInk));
    public static readonly StyledProperty<IBrush?> FilesInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(FilesInk));
    public static readonly StyledProperty<IBrush?> MessagesInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(MessagesInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> FreeInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(FreeInk));
    public static readonly StyledProperty<IBrush?> FreeAfterInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(FreeAfterInk));
    public static readonly StyledProperty<IBrush?> ReserveInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(ReserveInk));
    public static readonly StyledProperty<IBrush?> LineInkProperty = AvaloniaProperty.Register<ContextWindowGrid, IBrush?>(nameof(LineInk));

    /// <summary>Seconds a cell takes to recolor.</summary>
    public const double ColorDuration = 0.24;

    /// <summary>The longest delay of an append or insert wave (seconds).</summary>
    public const double MaxWaveDelay = 0.36;

    /// <summary>The fold sweep: 720ms, low point 0.35 at 55%, resting at 0.5; at most 6ms between cells and 420ms in all.</summary>
    public const double FoldDuration = 0.72;

    public const double FoldTurn = 0.55;
    public const double FoldLow = 0.35;
    public const double FoldEnd = 0.5;
    public const double FoldStep = 0.006;
    public const double FoldSpread = 0.42;

    /// <summary>The legend hover fades cells in 160ms.</summary>
    public const double DimDuration = 0.16;

    private const int Count = ContextWindowBudget.CellCount;
    private const int PartCount = 7;
    private const int BrushCacheLimit = 4096;

    private readonly Cell[] _cells = new Cell[Count];
    private readonly ContextWindowCellInk[] _inks = new ContextWindowCellInk[8];
    private readonly Color[] _restColors = new Color[8];
    private readonly Tween[] _dims = new Tween[PartCount];
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<uint, ImmutablePen> _pens = new();
    private readonly LiveLoop _loop;
    private Color _outlineColor;
    private IPen? _linePen;
    private bool _paletteDirty = true;
    private bool _hasLayout;
    private int _thresholdRow = 8;
    private double _now;
    private bool _folding;
    private double _foldStart;
    private double _foldEnd;
    private ContextWindowPart? _hovered;

    public ContextWindowGrid()
    {
        for (var i = 0; i < Count; i++)
        {
            _cells[i] = new Cell { Kind = ContextWindowCellKind.Free };
        }
        for (var p = 0; p < PartCount; p++)
        {
            _dims[p] = new Tween(1);
        }
        // Code-driven motion: 200 cells with their own delays, a keyframed sweep and a hover dim are one drawing on one
        // clock; a ControlTheme transition cannot express per-cell delays without 200 elements.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Full };
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }

    public double CellRadius { get => GetValue(CellRadiusProperty); set => SetValue(CellRadiusProperty, value); }

    public double ThresholdRowHeight { get => GetValue(ThresholdRowHeightProperty); set => SetValue(ThresholdRowHeightProperty, value); }

    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    public IBrush? SystemInk { get => GetValue(SystemInkProperty); set => SetValue(SystemInkProperty, value); }

    public IBrush? ToolsInk { get => GetValue(ToolsInkProperty); set => SetValue(ToolsInkProperty, value); }

    public IBrush? FilesInk { get => GetValue(FilesInkProperty); set => SetValue(FilesInkProperty, value); }

    public IBrush? MessagesInk { get => GetValue(MessagesInkProperty); set => SetValue(MessagesInkProperty, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? FreeInk { get => GetValue(FreeInkProperty); set => SetValue(FreeInkProperty, value); }

    public IBrush? FreeAfterInk { get => GetValue(FreeAfterInkProperty); set => SetValue(FreeAfterInkProperty, value); }

    public IBrush? ReserveInk { get => GetValue(ReserveInkProperty); set => SetValue(ReserveInkProperty, value); }

    public IBrush? LineInk { get => GetValue(LineInkProperty); set => SetValue(LineInkProperty, value); }

    /// <summary>Raised after the colors changed (theme or accent), so the legend swatches can follow.</summary>
    internal event Action? PaletteChanged;

    /// <summary>The resting fill of a kind (transparent for the reserve).</summary>
    internal Color ColorOf(ContextWindowCellKind kind)
    {
        EnsurePalette();
        return _restColors[(int)kind];
    }

    /// <summary>The reserve's outline color.</summary>
    internal Color OutlineColor
    {
        get
        {
            EnsurePalette();
            return _outlineColor;
        }
    }

    /// <summary>The kinds the cells are heading to.</summary>
    internal ContextWindowCellKind KindAt(int index) => _cells[index].Kind;

    /// <summary>The clock (seconds since the grid was created, advancing only while something moves).</summary>
    internal double Now => _now;

    /// <summary>The row after which the threshold line is drawn.</summary>
    internal int ThresholdRow => _thresholdRow;

    /// <summary>True while the fold sweep shows.</summary>
    internal bool IsFolding => _folding;

    /// <summary>True while any cell, the sweep or the hover still moves.</summary>
    internal bool IsMoving => AnyMotion(_now);

    /// <summary>The second at which cell <paramref name="index"/> starts to recolor (tests).</summary>
    internal double StartOf(int index) => _cells[index].Start;

    /// <summary>The fold delay of cell <paramref name="index"/> in seconds, or -1 when it does not fold (tests).</summary>
    internal double FoldDelayOf(int index) => _cells[index].FoldDelay;

    /// <summary>The cell's fill color as drawn now, with its alpha (hover and fold included).</summary>
    internal Color FillAt(int index)
    {
        EnsurePalette();
        var ink = InkAt(index, _now);
        var opacity = OpacityAt(index, _now);
        return WithAlpha(ink.ToRgb(), (byte)Math.Round(Math.Clamp(ink.Alpha * opacity, 0, 1) * 255));
    }

    /// <summary>The cell's opacity now (the fold sweep or the hover dim).</summary>
    internal double OpacityAt(int index) => OpacityAt(index, _now);

    /// <summary>Advances the clock without frames (tests).</summary>
    internal void Advance(double seconds)
    {
        _now += seconds;
        InvalidateVisual();
    }

    /// <summary>
    /// Shows a new arrangement. Cells whose kind changes recolor from what they show now, each after its wave delay counted
    /// from <paramref name="start"/>; the rest keep any recolor they are in. Without Full motion everything lands at once.
    /// </summary>
    public void Apply(ContextWindowCellKind[] kinds, int thresholdRow, ContextWindowWave wave, int start)
    {
        EnsurePalette();
        var animate = wave != ContextWindowWave.None && _hasLayout && ArcMotion.IsFull(this);
        _thresholdRow = thresholdRow;
        for (var i = 0; i < Count; i++)
        {
            ref var cell = ref _cells[i];
            var kind = kinds[i];
            if (!animate)
            {
                cell.Kind = kind;
                cell.Moving = false;
                continue;
            }
            if (cell.Kind == kind)
            {
                continue;
            }
            cell.From = InkAt(i, _now);
            cell.Kind = kind;
            cell.Start = _now + Delay(wave, i, start);
            cell.Moving = true;
        }
        _hasLayout = true;
        if (animate)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    /// <summary>The delay of cell <paramref name="index"/> in a wave that starts at <paramref name="start"/> (seconds).</summary>
    public static double Delay(ContextWindowWave wave, int index, int start) => wave switch
    {
        ContextWindowWave.Append => index <= start ? 0 : Math.Min(MaxWaveDelay, 0.009 * (index - start)),
        ContextWindowWave.Insert => index <= start ? 0 : Math.Min(MaxWaveDelay, 0.003 * (index - start)),
        ContextWindowWave.Relayout => 0.0014 * index,
        _ => 0
    };

    /// <summary>
    /// Starts the fold sweep over the <paramref name="count"/> conversation cells from <paramref name="first"/>, oldest
    /// first: min(6ms, 420ms / (n − 1)) between cells. Full motion only.
    /// </summary>
    public void BeginFold(int first, int count)
    {
        for (var i = 0; i < Count; i++)
        {
            _cells[i].FoldDelay = -1;
        }
        if (count <= 0 || !ArcMotion.IsFull(this))
        {
            _folding = false;
            return;
        }
        var step = count > 1 ? Math.Min(FoldStep, FoldSpread / (count - 1)) : 0;
        for (var i = 0; i < count && first + i < Count; i++)
        {
            _cells[first + i].FoldDelay = i * step;
        }
        _folding = true;
        _foldStart = _now;
        _foldEnd = _now + (count - 1) * step + FoldDuration;
        _loop.Start();
        InvalidateVisual();
    }

    /// <summary>Ends the sweep: the cells are fully opaque again at once (the compaction lands).</summary>
    public void EndFold()
    {
        if (!_folding)
        {
            return;
        }
        _folding = false;
        for (var i = 0; i < Count; i++)
        {
            _cells[i].FoldDelay = -1;
        }
        InvalidateVisual();
    }

    /// <summary>Lights the cells of one legend row and quiets the rest (null: all at full opacity).</summary>
    public void SetHover(ContextWindowPart? part)
    {
        if (_hovered == part)
        {
            return;
        }
        _hovered = part;
        var animate = ArcMotion.IsFull(this);
        for (var p = 0; p < PartCount; p++)
        {
            var target = part is null || (int)part.Value == p ? 1.0 : DimOpacity;
            if (animate)
            {
                _dims[p].To(target, _now, DimDuration);
            }
            else
            {
                _dims[p] = new Tween(target);
            }
        }
        if (animate)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    /// <summary>Lands every recolor, the sweep's resting state and the hover at once (motion turned off mid-way).</summary>
    public void Settle()
    {
        for (var i = 0; i < Count; i++)
        {
            _cells[i].Moving = false;
        }
        for (var p = 0; p < PartCount; p++)
        {
            _dims[p] = new Tween(_dims[p].Target);
        }
        InvalidateVisual();
    }

    private bool Step(double dt)
    {
        _now += dt;
        InvalidateVisual();
        return AnyMotion(_now);
    }

    private bool AnyMotion(double now)
    {
        if (_folding && now < _foldEnd)
        {
            return true;
        }
        for (var p = 0; p < PartCount; p++)
        {
            if (_dims[p].IsMoving(now))
            {
                return true;
            }
        }
        for (var i = 0; i < Count; i++)
        {
            if (_cells[i].Moving && now < _cells[i].Start + ColorDuration)
            {
                return true;
            }
        }
        return false;
    }

    private ContextWindowCellInk InkAt(int index, double now)
    {
        ref var cell = ref _cells[index];
        var target = _inks[(int)cell.Kind];
        if (!cell.Moving)
        {
            return target;
        }
        var t = now - cell.Start;
        if (t >= ColorDuration)
        {
            cell.Moving = false;
            return target;
        }
        if (t <= 0)
        {
            return cell.From;
        }
        return ContextWindowCellInk.Lerp(cell.From, target, ArcEasings.Standard.Ease(t / ColorDuration));
    }

    private double OpacityAt(int index, double now)
    {
        ref var cell = ref _cells[index];
        if (_folding && cell.FoldDelay >= 0)
        {
            return FoldOpacity(now - _foldStart - cell.FoldDelay);
        }
        return _dims[(int)PartOf(cell.Kind)].Value(now);
    }

    /// <summary>The fold keyframes at <paramref name="t"/> seconds into a cell's sweep (fill both: 1 before, 0.5 after).</summary>
    public static double FoldOpacity(double t)
    {
        if (t <= 0)
        {
            return 1;
        }
        if (t >= FoldDuration)
        {
            return FoldEnd;
        }
        var p = t / FoldDuration;
        if (p < FoldTurn)
        {
            return 1 + (FoldLow - 1) * ArcEasings.Standard.Ease(p / FoldTurn);
        }
        return FoldLow + (FoldEnd - FoldLow) * ArcEasings.Standard.Ease((p - FoldTurn) / (1 - FoldTurn));
    }

    public static ContextWindowPart PartOf(ContextWindowCellKind kind) => kind switch
    {
        ContextWindowCellKind.System => ContextWindowPart.System,
        ContextWindowCellKind.Tools => ContextWindowPart.Tools,
        ContextWindowCellKind.Files => ContextWindowPart.Files,
        ContextWindowCellKind.Summary => ContextWindowPart.Summary,
        ContextWindowCellKind.Messages => ContextWindowPart.Messages,
        ContextWindowCellKind.Reserve => ContextWindowPart.Reserve,
        _ => ContextWindowPart.Free
    };

    /// <summary>The grid's height for a width: ten square rows, the threshold row and the gaps between all eleven.</summary>
    public static double HeightFor(double width, double gap, double thresholdRow)
    {
        var cell = CellSizeFor(width, gap);
        return ContextWindowBudget.Rows * cell + ContextWindowBudget.Rows * gap + thresholdRow;
    }

    public static double CellSizeFor(double width, double gap) =>
        Math.Max(0, (width - (ContextWindowBudget.Columns - 1) * gap) / ContextWindowBudget.Columns);

    /// <summary>The rectangle of cell <paramref name="index"/> in the grid's coordinates.</summary>
    internal Rect CellRect(int index)
    {
        var gap = CellGap;
        var size = CellSizeFor(Bounds.Width, gap);
        var row = index / ContextWindowBudget.Columns;
        var column = index % ContextWindowBudget.Columns;
        var y = row * (size + gap);
        if (row >= _thresholdRow)
        {
            y += ThresholdRowHeight + gap;
        }
        return new Rect(column * (size + gap), y, size, size);
    }

    /// <summary>The top of the threshold row.</summary>
    internal double ThresholdTop
    {
        get
        {
            var gap = CellGap;
            return _thresholdRow * (CellSizeFor(Bounds.Width, gap) + gap);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 402;
        return new Size(width, HeightFor(width, CellGap, ThresholdRowHeight));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == CellGapProperty || p == ThresholdRowHeightProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (p == CellRadiusProperty)
        {
            InvalidateVisual();
        }
        else if (p == DimOpacityProperty && _hovered is { } hovered)
        {
            _hovered = null;
            SetHover(hovered);
        }
        else if (p.OwnerType == typeof(ContextWindowGrid) && p.Name.EndsWith("Ink", StringComparison.Ordinal))
        {
            // Rebuilt here, not lazily in Render, so the legend can follow the new colors outside the render pass.
            _paletteDirty = true;
            EnsurePalette();
            PaletteChanged?.Invoke();
            InvalidateVisual();
        }
        else if (p == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
        {
            Settle();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Whatever was moving is done by the time the grid shows again.
        Settle();
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        var surface = ColorOf(SurfaceInk, Colors.White);
        var messages = ColorOf(MessagesInk, Color.Parse("#1F7DCF"));
        var free = ColorOf(FreeInk, Color.Parse("#F8F8F8"));
        _outlineColor = ColorOf(ReserveInk, Color.Parse("#C4C4C4"));
        Set(ContextWindowCellKind.System, ColorOf(SystemInk, Color.Parse("#7128A5")));
        Set(ContextWindowCellKind.Tools, ColorOf(ToolsInk, Color.Parse("#E58F00")));
        Set(ContextWindowCellKind.Files, ColorOf(FilesInk, Color.Parse("#009E84")));
        Set(ContextWindowCellKind.Summary, Oklch.MixOklab(surface, messages, 0.42));
        Set(ContextWindowCellKind.Messages, messages);
        Set(ContextWindowCellKind.Free, free);
        Set(ContextWindowCellKind.FreeAfter, ColorOf(FreeAfterInk, Oklch.MixOklab(surface, free, 0.55)));
        _inks[(int)ContextWindowCellKind.Reserve] = ContextWindowCellInk.Outlined(free);
        _restColors[(int)ContextWindowCellKind.Reserve] = Colors.Transparent;
        var line = ColorOf(LineInk, Color.Parse("#7D7D7D"));
        _linePen = new ImmutablePen(new ImmutableSolidColorBrush(line), 1, new ImmutableDashStyle([3, 2], 0));
        _brushes.Clear();
        _pens.Clear();
        // A theme or accent change lands at once.
        for (var i = 0; i < Count; i++)
        {
            _cells[i].Moving = false;
        }

        void Set(ContextWindowCellKind kind, Color color)
        {
            _inks[(int)kind] = ContextWindowCellInk.Fill(color);
            _restColors[(int)kind] = color;
        }
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        var gap = CellGap;
        var size = CellSizeFor(width, gap);
        var radius = Math.Min(CellRadius, size / 2);
        var thresholdHeight = ThresholdRowHeight;
        var now = _now;
        for (var i = 0; i < Count; i++)
        {
            var row = i / ContextWindowBudget.Columns;
            var column = i % ContextWindowBudget.Columns;
            var y = row * (size + gap);
            if (row >= _thresholdRow)
            {
                y += thresholdHeight + gap;
            }
            var rect = new Rect(column * (size + gap), y, size, size);
            var opacity = OpacityAt(i, now);
            ref var cell = ref _cells[i];
            Color fill;
            float fillAlpha;
            float outline;
            if (cell.Moving && now < cell.Start + ColorDuration)
            {
                var ink = InkAt(i, now);
                fill = ink.ToRgb();
                fillAlpha = ink.Alpha;
                outline = ink.Outline;
            }
            else
            {
                cell.Moving = false;
                var kind = (int)cell.Kind;
                fill = _restColors[kind];
                fillAlpha = _inks[kind].Alpha;
                outline = _inks[kind].Outline;
            }
            var a = (byte)Math.Round(Math.Clamp(fillAlpha * opacity, 0, 1) * 255);
            if (a > 0)
            {
                context.DrawRectangle(BrushOf(WithAlpha(fill, a)), null, rect, radius, radius);
            }
            var oa = (byte)Math.Round(Math.Clamp(outline * opacity, 0, 1) * _outlineColor.A);
            if (oa > 0)
            {
                var inset = rect.Deflate(0.5);
                var r = Math.Max(0, radius - 0.5);
                context.DrawRectangle(null, PenOf(WithAlpha(_outlineColor, oa)), inset, r, r);
            }
        }
        if (_linePen is not null)
        {
            var lineY = _thresholdRow * (size + gap) + Math.Floor(thresholdHeight / 2) + 0.5;
            context.DrawLine(_linePen, new Point(0, lineY), new Point(width, lineY));
        }
    }

    private IImmutableSolidColorBrush BrushOf(Color color)
    {
        var key = color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count >= BrushCacheLimit)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    private ImmutablePen PenOf(Color color)
    {
        var key = color.ToUInt32();
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count >= BrushCacheLimit)
            {
                _pens.Clear();
            }
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1);
            _pens[key] = pen;
        }
        return pen;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GridPeer(this);

    private struct Cell
    {
        public ContextWindowCellKind Kind;
        public ContextWindowCellInk From;
        public double Start;
        public bool Moving;
        public double FoldDelay;
    }

    /// <summary>A 160ms Standard tween of an opacity on the grid's clock.</summary>
    private struct Tween
    {
        private double _from;
        private double _to;
        private double _start;
        private double _duration;

        public Tween(double value)
        {
            _from = _to = value;
            _start = 0;
            _duration = 0;
        }

        public readonly double Target => _to;

        public readonly double Value(double now)
        {
            var t = now - _start;
            if (_duration <= 0 || t >= _duration)
            {
                return _to;
            }
            if (t <= 0)
            {
                return _from;
            }
            return _from + (_to - _from) * ArcEasings.Standard.Ease(t / _duration);
        }

        public readonly bool IsMoving(double now) => _duration > 0 && now - _start < _duration;

        public void To(double target, double now, double duration)
        {
            if (Math.Abs(target - _to) < 1e-9 && Math.Abs(Value(now) - target) < 1e-9)
            {
                return;
            }
            _from = Value(now);
            _to = target;
            _start = now;
            _duration = duration;
        }
    }

    /// <summary>The grid is one image to assistive technology, named by its full description.</summary>
    private sealed class GridPeer(ContextWindowGrid owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}
