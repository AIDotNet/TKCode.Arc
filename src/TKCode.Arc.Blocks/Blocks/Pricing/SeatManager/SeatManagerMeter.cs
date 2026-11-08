using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The seat meter of <see cref="SeatManager"/> (Arc Seat manager, design reference; notes 3–6): one rounded cell per seat
/// (24 high, radius 5, 3px apart) sharing the width like CSS <c>flex-grow</c>. Members are Accent, invited seats a 38%
/// accent tint, open seats SurfaceMuted with a BorderStrong inner line, seats being added a 10% accent tint with an Accent
/// line, seats being removed a Border line at half opacity. Cells are keyed: a cell that appears grows from zero weight,
/// a cell that goes away shrinks to zero weight and is only then removed (neighbours glide because the width is shared),
/// both on <c>SpringSpec.FromVisual(0.24, 0)</c>; a cell whose kind changes only recolours, in 160 ms on (.22, 1, .36, 1).
/// More than <see cref="SeatManagerMath.MergeThreshold"/> cells merge into one segment per kind.
/// </summary>
/// <remarks>
/// Drawn in one <see cref="Render"/> with a <see cref="MotionValue{T}"/> per cell weight (springs keep their velocity
/// when a press-and-hold adds cells every 70 ms), so no layout runs while the meter moves.
/// </remarks>
internal sealed class SeatManagerMeter : Control
{
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<SeatManagerMeter, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<SeatManagerMeter, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<SeatManagerMeter, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> StrongEdgeBrushProperty =
        AvaloniaProperty.Register<SeatManagerMeter, IBrush?>(nameof(StrongEdgeBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<SeatManagerMeter, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<double> CellRadiusProperty =
        AvaloniaProperty.Register<SeatManagerMeter, double>(nameof(CellRadius), 5);

    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<SeatManagerMeter, double>(nameof(CellGap), 3);

    public static readonly StyledProperty<double> CellHeightProperty =
        AvaloniaProperty.Register<SeatManagerMeter, double>(nameof(CellHeight), 24);

    /// <summary>The cells' spring (Arc: critically damped, fitted ω ≈ 21 rad/s).</summary>
    internal static readonly SpringSpec CellSpring = SpringSpec.FromVisual(0.24, 0);

    internal static readonly TimeSpan RecolorDuration = TimeSpan.FromMilliseconds(160);

    internal readonly record struct Look(Color Fill, Color Stroke, double Opacity);

    internal sealed class Cell
    {
        public required string Key;
        public SeatManagerCellKind Kind;
        public double Target = 1;
        public bool Leaving;
        public MotionValue<double>? Weight;
        public double StaticWeight;
        public Look From;
        public Look To;
        public MotionValue<double>? Mix;
        public Color FillColor;
        public Color StrokeColor;
        public IBrush? Fill;
        public IPen? Stroke;

        public double CurrentWeight => Weight?.Value ?? StaticWeight;
    }

    private readonly List<Cell> _cells = new();
    private readonly List<SeatManagerCellKind> _kinds = new();
    private readonly Look[] _palette = new Look[5];
    private bool _paletteReady;

    static SeatManagerMeter()
    {
        AffectsMeasure<SeatManagerMeter>(CellHeightProperty);
        AffectsRender<SeatManagerMeter>(CellRadiusProperty, CellGapProperty);
    }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? StrongEdgeBrush { get => GetValue(StrongEdgeBrushProperty); set => SetValue(StrongEdgeBrushProperty, value); }
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    public double CellRadius { get => GetValue(CellRadiusProperty); set => SetValue(CellRadiusProperty, value); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }
    public double CellHeight { get => GetValue(CellHeightProperty); set => SetValue(CellHeightProperty, value); }

    /// <summary>Raised when the theme's brushes change the cells' looks (the legend dots redraw).</summary>
    public event Action? PaletteChanged;

    /// <summary>The cells drawn now, leaving ones included (tests).</summary>
    internal IReadOnlyList<Cell> Cells => _cells;

    /// <summary>The kinds last asked for, in order (tests).</summary>
    internal IReadOnlyList<SeatManagerCellKind> Kinds => _kinds;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty || change.Property == SurfaceBrushProperty ||
            change.Property == MutedBrushProperty || change.Property == StrongEdgeBrushProperty ||
            change.Property == EdgeBrushProperty)
        {
            _paletteReady = false;
            PaletteChanged?.Invoke();
            foreach (var cell in _cells)
            {
                cell.Mix?.Jump(1);
                cell.From = cell.To = default;
            }
            InvalidateVisual();
        }
    }

    /// <summary>Shows <paramref name="kinds"/>, one cell per seat in order (merged per kind above the threshold).</summary>
    public void Show(IReadOnlyList<SeatManagerCellKind> kinds)
    {
        _kinds.Clear();
        _kinds.AddRange(kinds);
        var animate = IsLoaded && ArcMotion.IsFull(this);
        var wanted = new List<(string Key, SeatManagerCellKind Kind, double Weight)>();
        if (kinds.Count > SeatManagerMath.MergeThreshold)
        {
            foreach (var kind in Enum.GetValues<SeatManagerCellKind>())
            {
                var count = kinds.Count(k => k == kind);
                if (count > 0)
                {
                    wanted.Add(("k" + kind, kind, count));
                }
            }
        }
        else
        {
            for (var i = 0; i < kinds.Count; i++)
            {
                wanted.Add(("c" + i, kinds[i], 1));
            }
        }

        var existing = new Dictionary<string, Cell>(StringComparer.Ordinal);
        foreach (var cell in _cells)
        {
            existing[cell.Key] = cell;
        }
        var next = new List<Cell>(wanted.Count + 4);
        foreach (var (key, kind, weight) in wanted)
        {
            if (existing.Remove(key, out var cell))
            {
                if (cell.Kind != kind)
                {
                    Recolor(cell, kind, animate);
                }
                if (cell.Leaving || cell.Target != weight)
                {
                    cell.Leaving = false;
                    SetWeight(cell, weight, animate);
                }
            }
            else
            {
                cell = new Cell { Key = key, Kind = kind, StaticWeight = animate ? 0 : weight };
                if (animate)
                {
                    SetWeight(cell, weight, true);
                }
                cell.Target = weight;
            }
            next.Add(cell);
        }
        // Cells that are no longer wanted shrink to nothing where they are, then go.
        foreach (var gone in existing.Values)
        {
            var index = _cells.IndexOf(gone);
            if (animate && gone.CurrentWeight > 0.001)
            {
                if (!gone.Leaving)
                {
                    gone.Leaving = true;
                    SetWeight(gone, 0, true);
                }
                next.Insert(Math.Min(index, next.Count), gone);
            }
        }
        _cells.Clear();
        _cells.AddRange(next);
        InvalidateVisual();
    }

    private void SetWeight(Cell cell, double target, bool animate)
    {
        cell.Target = target;
        if (!animate)
        {
            cell.Weight?.Jump(target);
            cell.StaticWeight = target;
            return;
        }
        if (cell.Weight is null)
        {
            cell.Weight = new MotionValue<double>(this, cell.StaticWeight, MotionVectors.Double);
            cell.Weight.Changed += _ => InvalidateVisual();
        }
        RemoveWhenGone(cell, cell.Weight.AnimateTo(target, MotionSpec.Spring(CellSpring)));
    }

    private async void RemoveWhenGone(Cell cell, Task<bool> settling)
    {
        if (await settling && cell.Leaving)
        {
            _cells.Remove(cell);
            InvalidateVisual();
        }
    }

    private void Recolor(Cell cell, SeatManagerCellKind kind, bool animate)
    {
        EnsurePalette();
        var from = CurrentLook(cell);
        cell.Kind = kind;
        cell.From = from;
        cell.To = _palette[(int)kind];
        if (!animate)
        {
            cell.Mix?.Jump(1);
            return;
        }
        if (cell.Mix is null)
        {
            cell.Mix = new MotionValue<double>(this, 0, MotionVectors.Double);
            cell.Mix.Changed += _ => InvalidateVisual();
        }
        else
        {
            cell.Mix.Jump(0);
        }
        _ = cell.Mix.AnimateTo(1, MotionSpec.Tween(RecolorDuration, ArcEasings.Standard));
    }

    private Look CurrentLook(Cell cell)
    {
        EnsurePalette();
        var target = _palette[(int)cell.Kind];
        if (cell.Mix is null || cell.From == default)
        {
            return target;
        }
        var t = Math.Clamp(cell.Mix.Value, 0, 1);
        return t >= 1 ? target : Lerp(cell.From, target, t);
    }

    internal static Look Lerp(Look a, Look b, double t) => new(
        LerpColor(a.Fill, b.Fill, t), LerpColor(a.Stroke, b.Stroke, t), a.Opacity + (b.Opacity - a.Opacity) * t);

    private static Color LerpColor(Color a, Color b, double t)
    {
        static byte L(byte x, byte y, double t) => (byte)Math.Round(x + (y - x) * t);
        return Color.FromArgb(L(a.A, b.A, t), L(a.R, b.R, t), L(a.G, b.G, t), L(a.B, b.B, t));
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>The look of each kind, from the theme's brushes (Arc's color-mix tints computed in OKLab).</summary>
    internal Look LookOf(SeatManagerCellKind kind)
    {
        EnsurePalette();
        return _palette[(int)kind];
    }

    private void EnsurePalette()
    {
        if (_paletteReady)
        {
            return;
        }
        var accent = ColorOf(AccentBrush, Color.FromRgb(0x33, 0x33, 0x33));
        var surface = ColorOf(SurfaceBrush, Colors.White);
        var muted = ColorOf(MutedBrush, Color.FromRgb(0xF4, 0xF4, 0xF4));
        var strong = ColorOf(StrongEdgeBrush, Color.FromRgb(0xC8, 0xC8, 0xC8));
        var edge = ColorOf(EdgeBrush, Color.FromRgb(0xE4, 0xE4, 0xE4));
        static Color Clear(Color c) => Color.FromArgb(0, c.R, c.G, c.B);
        var invited = Oklch.MixOklab(surface, accent, 0.38);
        var adding = Oklch.MixOklab(surface, accent, 0.10);
        _palette[(int)SeatManagerCellKind.Member] = new Look(accent, Clear(accent), 1);
        _palette[(int)SeatManagerCellKind.Invited] = new Look(invited, Clear(invited), 1);
        _palette[(int)SeatManagerCellKind.Open] = new Look(muted, strong, 1);
        _palette[(int)SeatManagerCellKind.Adding] = new Look(adding, accent, 1);
        _palette[(int)SeatManagerCellKind.Removing] = new Look(Clear(surface), edge, 0.5);
        _paletteReady = true;
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, CellHeight);

    public override void Render(DrawingContext context)
    {
        var count = _cells.Count;
        if (count == 0)
        {
            return;
        }
        var total = 0.0;
        foreach (var cell in _cells)
        {
            total += Math.Max(0, cell.CurrentWeight);
        }
        if (total <= 0.0001)
        {
            return;
        }
        var gap = CellGap;
        var height = Math.Min(CellHeight, Bounds.Height);
        var top = (Bounds.Height - height) / 2;
        // CSS flex: every cell keeps its 1.5px side paddings, the rest of the row is shared by weight.
        var free = Math.Max(0, Bounds.Width + gap - gap * count);
        var x = 0.0;
        var radius = CellRadius;
        foreach (var cell in _cells)
        {
            var width = free * Math.Max(0, cell.CurrentWeight) / total;
            if (width >= 0.5)
            {
                var look = CurrentLook(cell);
                if (cell.Fill is null || cell.FillColor != look.Fill)
                {
                    cell.FillColor = look.Fill;
                    cell.Fill = new ImmutableSolidColorBrush(look.Fill);
                }
                if (cell.Stroke is null || cell.StrokeColor != look.Stroke)
                {
                    cell.StrokeColor = look.Stroke;
                    cell.Stroke = new ImmutablePen(new ImmutableSolidColorBrush(look.Stroke), 1);
                }
                var r = Math.Min(radius, Math.Min(width, height) / 2);
                var rect = new Rect(x, top, width, height);
                using (context.PushOpacity(look.Opacity))
                {
                    context.DrawRectangle(cell.Fill, null, new RoundedRect(rect, r));
                    if (look.Stroke.A > 0 && width > 1)
                    {
                        context.DrawRectangle(null, cell.Stroke, new RoundedRect(rect.Deflate(0.5), Math.Max(0, r - 0.5)));
                    }
                }
            }
            x += width + gap;
        }
    }
}
