using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The overlapping tool marks at the start of a group header: one 20px disc per tool (the first time a tool appears; at
/// most <see cref="MaxDiscs"/>), each after the first 4px over the one before and drawn on top of it. A disc is a Surface
/// fill ringed 1px in Border and 1px more in Surface outside, so a later disc cuts a clean notch in the earlier one; the
/// tool's mark sits in the middle at 12px. A tool that joins grows in: its slot widens from 0 to 16px (20px for the first)
/// and the disc scales 0.6 → 1 on one critically damped spring (visual duration 0.42s, measured ω ≈ 12.4), while it fades
/// in over 160ms (CSS ease-out); the label after the stack glides with it. Reduced motion and Off add discs whole.
/// Decorative: automation skips it.
/// <para>
/// Drawn in code with a <see cref="MotionValue{T}"/> per joining disc because the slot width, the overlap and the scale move
/// together on one spring, and the stack's width must follow it in layout, which a ControlTheme transition cannot express.
/// </para>
/// </summary>
internal sealed class ToolCallMarkStack : Control
{
    /// <summary>At most this many discs.</summary>
    public const int MaxDiscs = 4;

    /// <summary>A disc's diameter, the overlap and the mark's size.</summary>
    public const double DiscSize = 20;

    public const double Overlap = 4;

    public const double MarkSize = 12;

    /// <summary>The joining spring: visual duration 0.42s without bounce (Arc names it the morph spring; measured ζ ≈ 1).</summary>
    internal static readonly SpringSpec JoinSpring = SpringSpec.FromVisual(0.42, 0);

    /// <summary>The disc fill and the outer ring (theme: Surface).</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<ToolCallMarkStack, IBrush?>(nameof(SurfaceBrush));

    /// <summary>The 1px line around a disc (theme: Border).</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<ToolCallMarkStack, IBrush?>(nameof(RingBrush));

    /// <summary>The marks' color when a tool gives none (theme: Foreground).</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<ToolCallMarkStack, IBrush?>(nameof(MarkBrush));

    private sealed class Disc
    {
        public required string Key { get; init; }
        public Geometry? Mark { get; set; }
        public IBrush? Brush { get; set; }
        public MotionValue<double>? Progress { get; set; }
        public MotionValue<double>? Fade { get; set; }
    }

    private readonly List<Disc> _discs = new(MaxDiscs);
    private readonly Dictionary<IBrush, IPen> _pens = new();
    private double _laidOutWidth = double.NaN;

    static ToolCallMarkStack()
    {
        AffectsRender<ToolCallMarkStack>(SurfaceBrushProperty, RingBrushProperty, MarkBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolCallMarkStack>(false);
        FocusableProperty.OverrideDefaultValue<ToolCallMarkStack>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ToolCallMarkStack>(false);
    }

    /// <inheritdoc cref="SurfaceBrushProperty"/>
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <summary>The number of discs.</summary>
    internal int Count => _discs.Count;

    /// <summary>The keys (tool names) of the discs in order.</summary>
    internal IEnumerable<string> Keys => _discs.Select(d => d.Key);

    /// <summary>Whether a disc is still joining.</summary>
    internal bool IsMoving => _discs.Any(d => d.Progress?.IsAnimating == true || d.Fade?.IsAnimating == true);

    /// <summary>The stack's width for <paramref name="count"/> whole discs.</summary>
    internal static double WidthFor(int count) => count <= 0 ? 0 : DiscSize + (count - 1) * (DiscSize - Overlap);

    /// <summary>Shows one disc per distinct tool of <paramref name="items"/> (by name, first appearance first). New tools
    /// grow in when <paramref name="animate"/> and the motion level allows; removed tools go at once.</summary>
    internal void SetMarks(IEnumerable<ToolCallItem> items, bool animate)
    {
        var wanted = new List<(string Key, ToolCallItem Item)>(MaxDiscs);
        foreach (var item in items)
        {
            var key = string.IsNullOrEmpty(item.ToolName) ? "#" + item.Id : item.ToolName;
            if (wanted.Exists(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            wanted.Add((key, item));
            if (wanted.Count == MaxDiscs)
            {
                break;
            }
        }
        var changed = _discs.RemoveAll(d => !wanted.Exists(w => string.Equals(w.Key, d.Key, StringComparison.OrdinalIgnoreCase))) > 0;
        var grow = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        for (var i = 0; i < wanted.Count; i++)
        {
            var (key, item) = wanted[i];
            var index = _discs.FindIndex(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            Disc disc;
            if (index < 0)
            {
                disc = new Disc { Key = key };
                _discs.Insert(Math.Min(i, _discs.Count), disc);
                changed = true;
                if (grow)
                {
                    disc.Progress = Value(0, measure: true);
                    disc.Fade = Value(0, measure: false);
                    _ = disc.Progress.AnimateTo(1, MotionSpec.Spring(JoinSpring));
                    _ = disc.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, AiHeightBox.EaseOut));
                }
            }
            else
            {
                disc = _discs[index];
            }
            disc.Mark = item.ToolMark;
            disc.Brush = item.ToolMarkBrush;
        }
        if (changed)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private MotionValue<double> Value(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            if (measure)
            {
                // Layout follows the slot width only while it changes by a device pixel or lands.
                var width = Math.Round(CurrentWidth() * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1));
                if (!width.Equals(_laidOutWidth) || !value.IsAnimating)
                {
                    _laidOutWidth = width;
                    InvalidateMeasure();
                }
            }
            InvalidateVisual();
        };
        return value;
    }

    private static double Progress(Disc disc) => Math.Clamp(disc.Progress?.Value ?? 1, 0, 1.2);

    private double CurrentWidth()
    {
        var width = 0.0;
        for (var i = 0; i < _discs.Count; i++)
        {
            var p = Progress(_discs[i]);
            width += i == 0 ? DiscSize * p : (DiscSize - Overlap) * p;
        }
        return Math.Max(0, width);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        _discs.Count == 0 ? default : new Size(CurrentWidth(), DiscSize);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var surface = SurfaceBrush;
        var ring = RingBrush;
        var y = Bounds.Height / 2;
        var x = 0.0;
        for (var i = 0; i < _discs.Count; i++)
        {
            var disc = _discs[i];
            var p = Progress(disc);
            // The slot: 20p wide, starting 4p over the one before (none for the first).
            var left = i == 0 ? x : x - Overlap * p;
            var slot = DiscSize * p;
            x = left + slot;
            var opacity = Math.Clamp(disc.Fade?.Value ?? 1, 0, 1);
            if (opacity <= 0.001)
            {
                continue;
            }
            var scale = 0.6 + 0.4 * p;
            var center = new Point(left + slot / 2, y);
            var r = DiscSize / 2 * scale;
            using (context.PushOpacity(opacity))
            {
                if (surface is not null)
                {
                    context.DrawEllipse(surface, null, center, r + 2 * scale, r + 2 * scale);
                }
                if (ring is not null)
                {
                    context.DrawEllipse(ring, null, center, r + scale, r + scale);
                }
                if (surface is not null)
                {
                    context.DrawEllipse(surface, null, center, r, r);
                }
                var mark = disc.Mark ?? ArcIcons.Wrench;
                var brush = disc.Brush ?? MarkBrush;
                if (brush is not null)
                {
                    var size = MarkSize * scale;
                    var matrix = Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(center.X - size / 2, center.Y - size / 2);
                    using (context.PushTransform(matrix))
                    {
                        context.DrawGeometry(null, Pen(brush), mark);
                    }
                }
            }
        }
    }

    private IPen Pen(IBrush brush)
    {
        if (!_pens.TryGetValue(brush, out var pen))
        {
            if (_pens.Count > 8)
            {
                _pens.Clear();
            }
            // Lucide's 2px stroke at 24 reads like Arc's filled 12px marks better than the 1.75 of the row marks.
            pen = new ImmutablePen(brush.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[brush] = pen;
        }
        return pen;
    }
}
