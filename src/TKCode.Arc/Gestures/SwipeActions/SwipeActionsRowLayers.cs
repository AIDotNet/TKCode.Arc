using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The revealed actions of a <see cref="SwipeActionsRow"/>, drawn in one <see cref="Render"/> pass (no control per action).
/// Every action spans the full row from the edge it hides behind and slides in by its share of the revealed width
/// (outer actions above inner ones), so the actions share the space evenly, widen together past the resting width and the
/// covering action can stretch across the row. The glyph rides the centre of its share (icon from 0.6 scale, label once
/// the share is wide enough), then hugs the content edge while its action covers the row. A tap on a revealed action runs
/// it. Hidden from assistive technology and the tab order: the row's More actions menu is the accessible path.
/// </summary>
internal sealed class SwipeActionsRowLayers : Control, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> NeutralFillProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(NeutralFill));

    public static readonly StyledProperty<IBrush?> NeutralInkProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(NeutralInk));

    public static readonly StyledProperty<IBrush?> AccentFillProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(AccentFill));

    public static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(AccentInk));

    public static readonly StyledProperty<IBrush?> DangerFillProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(DangerFill));

    public static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, IBrush?>(nameof(DangerInk));

    public static readonly StyledProperty<double> ActionExtentProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, double>(nameof(ActionExtent), 76);

    public static readonly StyledProperty<double> GlyphExtentProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, double>(nameof(GlyphExtent), 20);

    public static readonly StyledProperty<double> GlyphGapProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, double>(nameof(GlyphGap), 3);

    public static readonly StyledProperty<double> GlyphStrokeProperty =
        AvaloniaProperty.Register<SwipeActionsRowLayers, double>(nameof(GlyphStroke), 1.75);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SwipeActionsRowLayers>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SwipeActionsRowLayers>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SwipeActionsRowLayers>();

    public IBrush? NeutralFill { get => GetValue(NeutralFillProperty); set => SetValue(NeutralFillProperty, value); }
    public IBrush? NeutralInk { get => GetValue(NeutralInkProperty); set => SetValue(NeutralInkProperty, value); }
    public IBrush? AccentFill { get => GetValue(AccentFillProperty); set => SetValue(AccentFillProperty, value); }
    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? DangerFill { get => GetValue(DangerFillProperty); set => SetValue(DangerFillProperty, value); }
    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }
    public double ActionExtent { get => GetValue(ActionExtentProperty); set => SetValue(ActionExtentProperty, value); }
    public double GlyphExtent { get => GetValue(GlyphExtentProperty); set => SetValue(GlyphExtentProperty, value); }
    public double GlyphGap { get => GetValue(GlyphGapProperty); set => SetValue(GlyphGapProperty, value); }
    public double GlyphStroke { get => GetValue(GlyphStrokeProperty); set => SetValue(GlyphStrokeProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    private sealed class LabelCache
    {
        public string? Text;
        public IBrush? Ink;
        public FormattedText? Layout;
    }

    private readonly Dictionary<SwipeAction, LabelCache> _labels = new();
    private readonly Dictionary<IBrush, Pen> _pens = new();
    private double _penThickness = -1;
    private (SwipeActionSide Side, int Index)? _pressed;

    static SwipeActionsRowLayers()
    {
        AffectsRender<SwipeActionsRowLayers>(NeutralFillProperty, NeutralInkProperty, AccentFillProperty, AccentInkProperty,
            DangerFillProperty, DangerInkProperty, ActionExtentProperty, GlyphExtentProperty, GlyphGapProperty, GlyphStrokeProperty,
            FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        FocusableProperty.OverrideDefaultValue<SwipeActionsRowLayers>(false);
    }

    public SwipeActionsRowLayers()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    internal SwipeActionsRow? Row { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            _labels.Clear();
        }
    }

    /// <summary>Drops cached label layouts (actions changed).</summary>
    internal void Reset()
    {
        _labels.Clear();
        InvalidateVisual();
    }

    private (IBrush? Fill, IBrush? Ink) BrushesOf(SwipeActionTone tone) => tone switch
    {
        SwipeActionTone.Accent => (AccentFill, AccentInk),
        SwipeActionTone.Danger => (DangerFill, DangerInk),
        _ => (NeutralFill, NeutralInk)
    };

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    /// <summary>Geometry of one layer at the current offset and cover progress.</summary>
    private readonly struct Layer
    {
        public Layer(Rect fill, double glyphCenter, double iconReveal, double labelReveal)
        {
            Fill = fill;
            GlyphCenter = glyphCenter;
            IconReveal = iconReveal;
            LabelReveal = labelReveal;
        }

        public Rect Fill { get; }
        public double GlyphCenter { get; }
        public double IconReveal { get; }
        public double LabelReveal { get; }
    }

    /// <summary>
    /// Arc's layer maths: rank 0 sits against the content and the highest rank at the outer edge; each layer's inner edge
    /// is <c>(count − rank) × revealed / count</c> from the outer edge, the covering layer grows to the whole revealed
    /// width and the ranks above it slide away.
    /// </summary>
    private Layer Compute(SwipeActionSide side, int rank, int count, double offset, int? coverRank, double cover, double width, double height)
    {
        var direction = side == SwipeActionSide.Leading ? 1 : -1;
        var revealed = Math.Max(0, offset * direction);
        var share = revealed / count;
        var start = (count - rank) * revealed / count;
        var inset = coverRank == rank ? start + cover * (revealed - start)
            : coverRank is { } covering && rank > covering ? start * (1 - cover)
            : start;
        var extent = ActionExtent;
        var glyphShift = coverRank == rank ? share / 2 + cover * (extent / 2 - share / 2) : share / 2;
        var anchor = side == SwipeActionSide.Leading ? inset : width - inset;
        var center = anchor - direction * glyphShift;
        var ownIcon = Clamp01((share - extent * 0.25) / (extent * 0.55));
        var ownLabel = Clamp01((share - extent * 0.72) / (extent * 0.24));
        double icon, label;
        if (coverRank == rank)
        {
            icon = Math.Max(ownIcon, cover);
            label = Math.Max(ownLabel, cover);
        }
        else if (coverRank is not null)
        {
            icon = ownIcon * (1 - cover);
            label = ownLabel * (1 - cover);
        }
        else
        {
            icon = ownIcon;
            label = ownLabel;
        }
        var fill = side == SwipeActionSide.Leading ? new Rect(0, 0, Math.Max(0, inset), height) : new Rect(width - inset, 0, Math.Max(0, inset), height);
        return new Layer(fill, center, icon, label);
    }

    private int? CoverRank(SwipeActionsRow row, SwipeActionSide side, int count)
    {
        if (row.Covering is not { } covering || covering.Side != side || covering.Index >= count)
        {
            return null;
        }
        return side == SwipeActionSide.Leading ? count - 1 - covering.Index : covering.Index;
    }

    public override void Render(DrawingContext context)
    {
        var row = Row;
        if (row is null)
        {
            return;
        }
        var offset = row.Offset;
        if (Math.Abs(offset) < 0.01)
        {
            return;
        }
        var size = Bounds.Size;
        using (context.PushClip(new Rect(size)))
        {
            Draw(context, row, SwipeActionSide.Leading, offset, size);
            Draw(context, row, SwipeActionSide.Trailing, offset, size);
        }
    }

    private void Draw(DrawingContext context, SwipeActionsRow row, SwipeActionSide side, double offset, Size size)
    {
        var actions = row.ActionsOf(side);
        var count = actions.Count;
        var direction = side == SwipeActionSide.Leading ? 1 : -1;
        if (count == 0 || offset * direction <= 0)
        {
            return;
        }
        var coverRank = CoverRank(row, side, count);
        var cover = row.CoverProgress;
        for (var rank = 0; rank < count; rank++)
        {
            var action = actions[side == SwipeActionSide.Leading ? count - 1 - rank : rank];
            var layer = Compute(side, rank, count, offset, coverRank, cover, size.Width, size.Height);
            var (fill, ink) = BrushesOf(action.Tone);
            if (layer.Fill.Width > 0 && fill is not null)
            {
                context.DrawRectangle(fill, null, layer.Fill);
            }
            if (ink is null)
            {
                continue;
            }
            DrawGlyph(context, action, ink, layer, size.Height);
        }
    }

    private void DrawGlyph(DrawingContext context, SwipeAction action, IBrush ink, in Layer layer, double height)
    {
        var label = LabelOf(action, ink);
        var glyph = GlyphExtent;
        var labelHeight = label?.Height ?? 0;
        var top = (height - (glyph + GlyphGap + labelHeight)) / 2;
        if (action.Icon is { } icon && layer.IconReveal > 0.001)
        {
            var scale = glyph / 24 * (0.6 + 0.4 * layer.IconReveal);
            var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) *
                         Matrix.CreateTranslation(layer.GlyphCenter, top + glyph / 2);
            using (context.PushOpacity(layer.IconReveal))
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, PenOf(ink), icon);
            }
        }
        if (label is not null && layer.LabelReveal > 0.001)
        {
            using (context.PushOpacity(layer.LabelReveal))
            {
                context.DrawText(label, new Point(layer.GlyphCenter - label.WidthIncludingTrailingWhitespace / 2, top + glyph + GlyphGap));
            }
        }
    }

    private Pen PenOf(IBrush ink)
    {
        if (Math.Abs(_penThickness - GlyphStroke) > double.Epsilon)
        {
            _pens.Clear();
            _penThickness = GlyphStroke;
        }
        if (!_pens.TryGetValue(ink, out var pen))
        {
            pen = new Pen(ink, GlyphStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[ink] = pen;
        }
        return pen;
    }

    private FormattedText? LabelOf(SwipeAction action, IBrush ink)
    {
        if (string.IsNullOrEmpty(action.Label))
        {
            return null;
        }
        if (!_labels.TryGetValue(action, out var cache))
        {
            cache = new LabelCache();
            _labels[action] = cache;
        }
        if (cache.Layout is null || !ReferenceEquals(cache.Ink, ink) || cache.Text != action.Label)
        {
            cache.Text = action.Label;
            cache.Ink = ink;
            cache.Layout = new FormattedText(action.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize > 0 ? FontSize : 12, ink);
        }
        return cache.Layout;
    }

    /// <summary>The revealed action under <paramref name="point"/>: the topmost layer whose visible fill contains it.</summary>
    internal (SwipeActionSide Side, int Index)? ActionAt(Point point)
    {
        if (Row is not { } row || Math.Abs(row.Offset) < 0.5)
        {
            return null;
        }
        var side = row.Offset > 0 ? SwipeActionSide.Leading : SwipeActionSide.Trailing;
        var actions = row.ActionsOf(side);
        var count = actions.Count;
        var coverRank = CoverRank(row, side, count);
        for (var rank = count - 1; rank >= 0; rank--)
        {
            var layer = Compute(side, rank, count, row.Offset, coverRank, row.CoverProgress, Bounds.Width, Bounds.Height);
            if (layer.Fill.Contains(point))
            {
                return (side, side == SwipeActionSide.Leading ? count - 1 - rank : rank);
            }
        }
        return null;
    }

    public bool HitTest(Point point) => ActionAt(point) is not null;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = ActionAt(e.GetPosition(this));
        if (_pressed is not null)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressed;
        _pressed = null;
        if (pressed is { } target && ActionAt(e.GetPosition(this)) == target)
        {
            e.Handled = true;
            Row?.Commit(target.Side, target.Index, 0);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = null;
    }
}
