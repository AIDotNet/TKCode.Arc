using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

public partial class FunnelChart
{
    private const int SamplesPerColumn = 20;
    private const double Plateau = 0.18;
    private const double TextInset = 10;
    private const double TextTop = 8;
    private const double VerticalEdge = 16;
    private const double BottomRoom = 16;
    private const double LeaveDistance = 14;
    private const double FillStrength = 0.3;
    private const double ColorSteps = 24;

    private readonly List<BandPath> _paths = new();
    private FunnelRollText[] _counts = [];
    private FunnelRollText[] _rates = [];
    private FunnelRollText[] _drops = [];
    private TextLayout?[] _labels = [];
    private TextLayout? _empty;
    private Size _size;
    private bool _vertical;
    private double _thickness;
    private double _columnExtent;
    private double _bandCenter;
    private int _textVersion = -1;
    private double _labelWidth = -1;

    /// <summary>
    /// One band's geometry, updated in place during a morph: the point arrays are rewritten and the figures told once,
    /// so a frame neither creates geometry objects nor raises a change per point.
    /// </summary>
    private sealed class BandPath
    {
        private readonly PathFigure _fillFigure = new() { IsClosed = true, IsFilled = true };
        private readonly PathFigure _upperFigure = new() { IsClosed = false, IsFilled = false };
        private readonly PathFigure _lowerFigure = new() { IsClosed = false, IsFilled = false };
        private readonly Point[] _fill;
        private readonly Point[] _upper;
        private readonly Point[] _lower;
        private bool _flip;

        public BandPath(int samples)
        {
            _fill = new Point[samples * 2 - 1];
            _upper = new Point[samples - 1];
            _lower = new Point[samples - 1];
            Fill = Geometry(_fillFigure, _fill);
            Upper = Geometry(_upperFigure, _upper);
            Lower = Geometry(_lowerFigure, _lower);
            Samples = samples;
        }

        public int Samples { get; }

        public PathGeometry Fill { get; }

        public PathGeometry Upper { get; }

        public PathGeometry Lower { get; }

        public bool IsVisible { get; private set; }

        private static PathGeometry Geometry(PathFigure figure, Point[] points)
        {
            figure.Segments!.Add(new PolyLineSegment { Points = points });
            var geometry = new PathGeometry();
            geometry.Figures!.Add(figure);
            return geometry;
        }

        public void SetEdge(int sample, Point upper, Point lower)
        {
            if (sample > 0)
            {
                _upper[sample - 1] = upper;
                _lower[sample - 1] = lower;
                _fill[sample - 1] = upper;
            }
            // Walks back along the lower edge: sample Samples - 1 comes right after the upper edge, sample 0 last.
            _fill[Samples - 1 + (Samples - 1 - sample)] = lower;
            if (sample == 0)
            {
                _upperStart = upper;
                _lowerStart = lower;
            }
        }

        private Point _upperStart;
        private Point _lowerStart;

        /// <summary>Publishes the rewritten points. The start point always changes by a sliver, which invalidates each figure.</summary>
        public void Commit(bool isVisible)
        {
            IsVisible = isVisible;
            _flip = !_flip;
            var sliver = _flip ? 1e-6 : 0;
            _fillFigure.StartPoint = new Point(_upperStart.X, _upperStart.Y + sliver);
            _upperFigure.StartPoint = new Point(_upperStart.X, _upperStart.Y + sliver);
            _lowerFigure.StartPoint = new Point(_lowerStart.X, _lowerStart.Y + sliver);
        }
    }

    private double GapFraction => _thickness > 0 ? SplitGap / _thickness : SplitGap / Math.Max(1, BandHeight);

    private double LineXS => Math.Ceiling(_palette.TypeXS * 1.5);

    private double LineBase => Math.Ceiling(_palette.TypeBase * 1.5);

    private double HeaderHeight => TextTop + LineXS * 4 + 2 + LineBase;

    internal bool IsVertical => _vertical;

    private bool ResolveVertical(double width) => Orientation switch
    {
        FunnelOrientation.Vertical => true,
        FunnelOrientation.Horizontal => false,
        _ => width < VerticalBelowWidth
    };

    private double ThicknessFor(double width, bool vertical)
        => vertical ? ChartsBMath.Clamp(width * 0.42, 96, 240) : Math.Max(1, BandHeight);

    /// <summary>Thickness of one layer at a stage's plateau, as a fraction of the band (for tests and the dots).</summary>
    internal double LayerThickness(int layer, int stage)
    {
        var index = FunnelModel.SpringIndex(layer, stage, _model.StageCount);
        return _band[index + 1] - _band[index];
    }

    /// <summary>Space between two neighbouring layers at a stage, as a fraction of the band.</summary>
    internal double LayerGap(int upper, int stage)
    {
        var count = _model.StageCount;
        return _band[FunnelModel.SpringIndex(upper + 1, stage, count)] - _band[FunnelModel.SpringIndex(upper, stage, count) + 1];
    }

    internal double SplitAmount => _band.Length > 0 ? _band[_band.Length - 1] : 0;

    internal string CountText(int stage) => _counts[stage].Text;

    internal string RateText(int stage) => _rates[stage].Text;

    internal string DropText(int stage) => _drops[stage].Text;

    /// <summary>The path of a layer's filled band, to check that a morph rewrites it in place.</summary>
    internal PathGeometry? LayerGeometry(int layer) => layer < _paths.Count ? _paths[layer].Fill : null;

    // ---- layout -------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        var vertical = ResolveVertical(width);
        var height = vertical
            ? Math.Max(160, _model.StageCount * VerticalRowHeight)
            : HeaderHeight + HeaderGap + ThicknessFor(width, false) + BottomRoom;
        var size = new Size(width, height);
        _flow.Measure(size);
        _overlay.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ApplyLayout(finalSize);
        var bounds = new Rect(finalSize);
        _flow.Arrange(bounds);
        _overlay.Arrange(bounds);
        return finalSize;
    }

    private void ApplyLayout(Size size)
    {
        var vertical = ResolveVertical(size.Width);
        var thickness = ThicknessFor(size.Width, vertical);
        var count = Math.Max(1, _model.StageCount);
        var extent = vertical ? VerticalRowHeight : size.Width / count;
        var center = vertical ? VerticalEdge + thickness / 2 : HeaderHeight + HeaderGap + thickness / 2;
        var moved = size != _size || vertical != _vertical || thickness != _thickness || extent != _columnExtent || center != _bandCenter;
        if (!moved)
        {
            return;
        }
        var thicknessChanged = thickness != _thickness;
        _size = size;
        _vertical = vertical;
        _thickness = thickness;
        _columnExtent = extent;
        _bandCenter = center;
        if (thicknessChanged)
        {
            RefreshTargets();
        }
        UpdateGeometry();
        SyncTooltip();
    }

    private Rect ColumnRect(double at)
        => _vertical
            ? new Rect(0, at * _columnExtent, _size.Width, _columnExtent)
            : new Rect(at * _columnExtent, 0, _columnExtent, _size.Height);

    private Point Map(double along, double across)
        => _vertical
            ? new Point(_bandCenter + across * _thickness, along * _columnExtent)
            : new Point(along * _columnExtent, _bandCenter + across * _thickness);

    // ---- band geometry ------------------------------------------------------------------------------------------

    private void EnsurePaths()
    {
        var samples = _model.StageCount * SamplesPerColumn + 1;
        if (_paths.Count == _model.LayerCount && (_paths.Count == 0 || _paths[0].Samples == samples))
        {
            return;
        }
        _paths.Clear();
        if (_model.StageCount == 0)
        {
            return;
        }
        for (var k = 0; k < _model.LayerCount; k++)
        {
            _paths.Add(new BandPath(samples));
        }
    }

    /// <summary>A layer's edges at a position along the band: flat around each column's centre, an S-curve between them.</summary>
    private void Edges(ReadOnlySpan<double> band, int layer, double along, out double low, out double high)
    {
        var count = _model.StageCount;
        var position = along - 0.5;
        var left = (int)Math.Floor(position);
        var a = FunnelModel.SpringIndex(layer, Math.Clamp(left, 0, count - 1), count);
        if (left < 0 || left >= count - 1)
        {
            low = band[a];
            high = band[a + 1];
            return;
        }
        var b = FunnelModel.SpringIndex(layer, left + 1, count);
        var t = Math.Clamp((position - left - Plateau) / (1 - 2 * Plateau), 0, 1);
        var eased = t * t * (3 - 2 * t);
        low = band[a] + (band[b] - band[a]) * eased;
        high = band[a + 1] + (band[b + 1] - band[a + 1]) * eased;
    }

    private void UpdateGeometry()
    {
        if (_size.Width <= 0 || _paths.Count == 0 || _band.Length != _model.SpringLength)
        {
            return;
        }
        var band = _band.Values;
        for (var layer = 0; layer < _paths.Count; layer++)
        {
            var path = _paths[layer];
            var widest = 0.0;
            for (var j = 0; j < path.Samples; j++)
            {
                Edges(band, layer, (double)j / SamplesPerColumn, out var low, out var high);
                widest = Math.Max(widest, high - low);
                path.SetEdge(j, Map(j / (double)SamplesPerColumn, low), Map(j / (double)SamplesPerColumn, high));
            }
            path.Commit(widest * _thickness > 0.2);
        }
    }

    private double QuantizedAmount => Math.Round(Math.Clamp(SplitAmount, 0, 1) * ColorSteps) / ColorSteps;

    private Color LayerColor(int layer, double amount) => ChartsBMath.Lerp(_palette.Series(0), _palette.Series(layer % 4), amount);

    // ---- numbers ------------------------------------------------------------------------------------------------

    private TextLayout Make(string text, double size, Color color, FontWeight weight = FontWeight.Normal, double maxWidth = double.PositiveInfinity,
                            int lines = 1, FontFeatureCollection? features = null)
        => new(text, _palette.Body(weight), size, _palette.Brush(color), TextAlignment.Left,
            lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap, lines > 1 ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            textDecorations: null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, double.NaN, 0, lines, features);

    /// <summary>
    /// Brings the count, rate and drop-off texts up to date. Only texts that changed are rebuilt, and those roll. A rebuild
    /// also runs from <see cref="Render"/>, so it must not touch anything that invalidates the visual.
    /// </summary>
    private void SyncNumbers(bool roll, bool rebuild = false)
    {
        var count = _model.StageCount;
        if (_counts.Length != count)
        {
            _counts = NewRow(count);
            _rates = NewRow(count);
            _drops = NewRow(count);
            rebuild = true;
            roll = false;
        }
        if (count == 0)
        {
            return;
        }
        var view = ResolvedView;
        var empty = _model.IsEmpty(view);
        var p = _palette;
        var changed = false;
        for (var i = 0; i < count; i++)
        {
            var value = empty ? 0 : _model.CountAt(view, i);
            var rate = empty ? 0 : _model.FromPrevious(view, i);
            var drop = empty ? 0 : _model.DropOff(view, i);
            var countText = Format(value);
            var rateText = ChartsBMath.Percent(rate);
            var dropText = i == 0 ? string.Empty : "↓ " + Format(drop);
            if (rebuild || _counts[i].Text != countText)
            {
                _counts[i].Set(countText, value, Make(countText, p.TypeBase, p.Foreground, FontWeight.Medium, features: DataGridPalette.TabularNumbers), roll && !rebuild);
                changed = true;
            }
            if (rebuild || _rates[i].Text != rateText)
            {
                _rates[i].Set(rateText, rate, Make(rateText, p.TypeXS, p.TextSecondary, features: DataGridPalette.TabularNumbers), roll && !rebuild);
                changed = true;
            }
            if (rebuild || _drops[i].Text != dropText)
            {
                _drops[i].Set(dropText, drop, Make(dropText, p.TypeXS, p.TextMuted, features: DataGridPalette.TabularNumbers), roll && !rebuild);
                changed = true;
            }
        }
        if (changed && roll && !rebuild)
        {
            _roll.Jump(0);
            _ = _roll.AnimateTo(1, MotionSpec.Spring(s_rollSpring));
        }
    }

    private static FunnelRollText[] NewRow(int count)
    {
        var row = new FunnelRollText[count];
        for (var i = 0; i < count; i++)
        {
            row[i] = new FunnelRollText();
        }
        return row;
    }

    private double LabelWidth => _vertical
        ? Math.Max(40, _size.Width - (VerticalEdge * 2 + _thickness) - TextInset)
        : Math.Max(20, _columnExtent - TextInset * 2);

    private void EnsureText()
    {
        var width = LabelWidth;
        var stale = _textVersion != _palette.Version;
        if (stale)
        {
            _textVersion = _palette.Version;
            SyncNumbers(roll: false, rebuild: true);
            _empty?.Dispose();
            _empty = Make(EmptyLabel, _palette.TypeSM, _palette.TextMuted);
        }
        if (!stale && Math.Abs(width - _labelWidth) < 0.5 && _labels.Length == _model.StageCount && _labelsFor == _model)
        {
            return;
        }
        _labelWidth = width;
        _labelsFor = _model;
        foreach (var label in _labels)
        {
            label?.Dispose();
        }
        _labels = new TextLayout?[_model.StageCount];
        for (var i = 0; i < _labels.Length; i++)
        {
            _labels[i] = Make(StageName(_model.Stages[i]), _palette.TypeXS, _palette.TextSecondary, maxWidth: width, lines: 2);
        }
    }

    private FunnelModel? _labelsFor;

    // ---- drawing ------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (_size.Width <= 0)
        {
            return;
        }
        EnsureText();
        if (IsEmptyView)
        {
            var empty = _empty!;
            empty.Draw(context, new Point((_size.Width - empty.Width) / 2, (_size.Height - empty.Height) / 2));
            return;
        }
        var p = _palette;
        using var fade = context.PushOpacity(_reveal.Fade);
        var highlight = _highlightOpacity.Value;
        if (highlight > 0.002)
        {
            using (context.PushOpacity(highlight))
            {
                context.DrawRectangle(p.Brush(ChartsBMath.Alpha(p.Foreground, 0.06)), null, new RoundedRect(ColumnRect(_highlightAt.Value), HighlightRadius));
            }
        }
        DrawSeparators(context);
        DrawBand(context);
        DrawText(context);
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(new Rect(_size).Inflate(2), HighlightRadius + 2));
        }
    }

    private void DrawSeparators(DrawingContext context)
    {
        var pen = _palette.Pen(_palette.BorderSubtle, 1);
        for (var i = 1; i < _model.StageCount; i++)
        {
            var at = Math.Round(i * _columnExtent) + 0.5;
            if (_vertical)
            {
                context.DrawLine(pen, new Point(0, at), new Point(_size.Width, at));
            }
            else
            {
                context.DrawLine(pen, new Point(at, 0), new Point(at, _size.Height - BottomRoom / 2));
            }
        }
    }

    private void DrawBand(DrawingContext context)
    {
        if (!_reveal.IsRevealed || _paths.Count == 0)
        {
            return;
        }
        var p = _palette;
        var amount = QuantizedAmount;
        var last = _paths.Count - 1;
        for (var k = 0; k < _paths.Count; k++)
        {
            var path = _paths[k];
            if (!path.IsVisible)
            {
                continue;
            }
            var stroke = LayerColor(k, amount);
            var fill = ChartsBMath.MixIn(stroke, FillStrength, p.Surface);
            // Inner edges start in the fill color, which also hides the seam between the layers of the total view.
            var inner = ChartsBMath.Lerp(fill, stroke, amount);
            context.DrawGeometry(p.Brush(fill), null, path.Fill);
            context.DrawGeometry(null, p.Pen(k == 0 ? stroke : inner, 1.5, PenLineCap.Round), path.Upper);
            context.DrawGeometry(null, p.Pen(k == last ? stroke : inner, 1.5, PenLineCap.Round), path.Lower);
        }
    }

    private void DrawText(DrawingContext context)
    {
        var p = _palette;
        var roll = _roll.Value;
        var line = LineXS;
        var big = LineBase;
        var rollBy = p.TypeXS * 0.8;
        for (var i = 0; i < _model.StageCount; i++)
        {
            var label = _labels[i]!;
            if (_vertical)
            {
                var x = VerticalEdge * 2 + _thickness;
                var y = i * _columnExtent + TextTop;
                label.Draw(context, new Point(x, y));
                y += label.Height + 2;
                _counts[i].Draw(context, x, y, roll, rollBy);
                y += big;
                _rates[i].Draw(context, x, y, roll, rollBy);
                _drops[i].Draw(context, x + p.TypeXS * 3.6, y, roll, rollBy);
            }
            else
            {
                var x = i * _columnExtent + TextInset;
                using (context.PushClip(new Rect(i * _columnExtent, 0, _columnExtent, HeaderHeight)))
                {
                    var y = TextTop;
                    label.Draw(context, new Point(x, y));
                    y += line * 2 + 2;
                    _counts[i].Draw(context, x, y, roll, rollBy);
                    y += big;
                    _rates[i].Draw(context, x, y, roll, rollBy);
                    y += line;
                    _drops[i].Draw(context, x, y, roll, rollBy);
                }
            }
        }
    }

    /// <summary>Paints the stream on its own surface, so a dot frame does not record the chart again.</summary>
    private void DrawDots(DrawingContext context)
    {
        if (!_flowLoop.IsActive || !_flowLoop.CanRun || _band.Length != _model.SpringLength || _model.StageCount == 0 || IsEmptyView)
        {
            return;
        }
        var p = _palette;
        var band = _band.Values;
        var amount = QuantizedAmount;
        var total = amount <= 0;
        var stages = (double)_model.StageCount;
        var radius = DotRadius;
        var lastLayer = _model.LayerCount - 1;
        var dots = _sim.Dots;
        for (var i = 0; i < dots.Length; i++)
        {
            ref readonly var dot = ref dots[i];
            if (!dot.IsAlive)
            {
                continue;
            }
            var along = Math.Min(dot.Along, stages);
            Edges(band, dot.Layer, along, out var low, out var high);
            var across = low + (high - low) * dot.Lane;
            var push = 0.0;
            // Fades in at the mouth of the band and out at its end.
            var opacity = Math.Min(1, Math.Min(along / 0.2, (stages - along) / 0.2));
            if (dot.Leaving >= 0)
            {
                var t = Math.Min(1, dot.Leaving / FunnelFlowSim.LeaveSeconds);
                var outward = dot.Lane < 0.5 ? -1 : 1;
                var edge = outward < 0 ? low : high;
                if (total)
                {
                    // All layers read as one band: leave through its outer edge.
                    Edges(band, outward < 0 ? 0 : lastLayer, along, out var outerLow, out var outerHigh);
                    edge = outward < 0 ? outerLow : outerHigh;
                }
                across += (edge - across) * Math.Min(1, t * 2);
                push = outward * LeaveDistance * t;
                opacity = Math.Min(opacity, 1 - t);
            }
            if (opacity <= 0.01)
            {
                continue;
            }
            var point = Map(along, across);
            point = _vertical ? new Point(point.X + push, point.Y) : new Point(point.X, point.Y + push);
            var brush = p.Brush(LayerColor(dot.Layer, amount));
            if (opacity >= 0.99)
            {
                context.DrawEllipse(brush, null, point, radius, radius);
            }
            else
            {
                using (context.PushOpacity(opacity))
                {
                    context.DrawEllipse(brush, null, point, radius, radius);
                }
            }
        }
    }
}
