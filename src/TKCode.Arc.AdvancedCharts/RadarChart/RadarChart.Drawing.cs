using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Layout and the one paint function of <see cref="RadarChart"/>.</summary>
public partial class RadarChart
{
    private const double CompactWidth = 360;
    private const double MinRadius = 24;
    private const double Inset = 4;

    /// <summary>Dash arrays (in pen thicknesses) of the line keys. There are more than hues, so a repeat of one is not a repeat of both.</summary>
    private static readonly (double[]? Dashes, string Name)[] s_lineKeys =
    [
        (null, "solid"),
        ([3, 3], "dashed"),
        ([0.01, 2.5], "dotted"),
        ([4, 2.5, 0.01, 2.5], "dash-dot"),
        ([7, 3], "long-dashed"),
        ([7, 2.5, 0.01, 2.5], "long dash-dot")
    ];

    private sealed class AxisLabel : IDisposable
    {
        public required TextLayout Rest { get; init; }
        public required TextLayout Active { get; init; }
        public double Width => Rest.WidthIncludingTrailingWhitespace;
        public double Height => Rest.Height;
        public Point Origin { get; set; }
        public Rect Box { get; set; }

        public void Dispose()
        {
            Rest.Dispose();
            Active.Dispose();
        }
    }

    private readonly List<AxisLabel> _labels = new();
    private readonly List<StreamGeometry> _polygonRings = new();
    private double _labelMaxWidth;
    private int _labelVersion = -1;
    private int _inkVersion = -1;
    private bool _labelsStale = true;
    private bool _ringsStale = true;
    private bool _compact;
    private double _gap;
    private double _radius = MinRadius;
    private Point _center;
    private Rect _webRect;
    private double _webHeight;
    private FormattedText? _emptyText;
    private int _emptyVersion = -1;

    internal static string LineKeyName(int index) => s_lineKeys[index % s_lineKeys.Length].Name;

    internal ChartsBPalette Palette => _palette;

    // ---- ink and text -----------------------------------------------------------------------------------------

    /// <summary>Brushes and pens follow the palette and <see cref="FillOpacity"/>; read here so a theme change needs no rebuild.</summary>
    internal void EnsureInk()
    {
        if (_inkVersion == _palette.Version)
        {
            return;
        }
        _inkVersion = _palette.Version;
        var fill = ChartsBMath.Clamp(FillOpacity, 0, 1);
        foreach (var shape in _shapes)
        {
            var hue = _palette.Series(shape.Index % 4);
            var dashes = s_lineKeys[shape.Index % s_lineKeys.Length].Dashes;
            shape.Fill = new ImmutableSolidColorBrush(ChartsBMath.Alpha(hue, fill));
            shape.Pen = new ImmutablePen(new ImmutableSolidColorBrush(hue), StrokeThickness,
                dashes is null ? null : new ImmutableDashStyle(dashes, 0), PenLineCap.Round, PenLineJoin.Round);
            shape.MarkerPen = _palette.Pen(hue, StrokeThickness);
        }
    }

    private void InvalidateInk()
    {
        _inkVersion = -1;
        foreach (var child in _legend.Children)
        {
            if (child is RadarChartLegendItem item)
            {
                item.InvalidateInk();
            }
        }
    }

    private void EnsureLabels(bool compact)
    {
        var maxWidth = compact ? CompactLabelWidth : LabelWidth;
        if (!_labelsStale && _labelVersion == _palette.Version && _labelMaxWidth == maxWidth)
        {
            return;
        }
        _labelsStale = false;
        _labelVersion = _palette.Version;
        _labelMaxWidth = maxWidth;
        foreach (var label in _labels)
        {
            label.Dispose();
        }
        _labels.Clear();
        var typeface = _palette.Body();
        for (var i = 0; i < _axes.Count; i++)
        {
            var side = Side(_unitX[i]);
            var alignment = side > 0 ? TextAlignment.Left : side < 0 ? TextAlignment.Right : TextAlignment.Center;
            _labels.Add(new AxisLabel
            {
                Rest = MakeLabel(_axes[i].Label, typeface, _palette.TextSecondary, alignment, compact, maxWidth),
                Active = MakeLabel(_axes[i].Label, typeface, _palette.Foreground, alignment, compact, maxWidth)
            });
        }
    }

    private TextLayout MakeLabel(string text, Typeface typeface, Color color, TextAlignment alignment, bool wrap, double maxWidth)
        => new(text, typeface, _palette.TypeXS, _palette.Brush(color), alignment, wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, textDecorations: null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, double.NaN, 0,
            wrap ? 2 : 1);

    /// <summary>-1 for labels left of the web, 1 for labels right of it, 0 for labels centred over or under it.</summary>
    private static int Side(double x) => x > 0.2 ? 1 : x < -0.2 ? -1 : 0;

    // ---- layout -------------------------------------------------------------------------------------------------

    /// <summary>Finds the largest web, up to <see cref="Size"/>, whose labels fit the box.</summary>
    private void Fit(double width, double height)
    {
        _compact = width < CompactWidth;
        _gap = _compact ? CompactLabelGap : LabelGap;
        EnsureLabels(_compact);
        var radius = Math.Max(MinRadius, Size / 2);
        var half = width / 2 - Inset;
        for (var i = 0; i < _labels.Count; i++)
        {
            var x = Math.Abs(_unitX[i]);
            if (x < 0.01)
            {
                continue;
            }
            var reach = Side(_unitX[i]) == 0 ? _labels[i].Width / 2 : _labels[i].Width;
            radius = Math.Min(radius, (half - reach) / x - _gap);
        }
        radius = Math.Max(MinRadius, radius);
        var (above, below) = VerticalExtent(radius);
        if (!double.IsInfinity(height) && above + below + 2 * Inset > height)
        {
            var up = 0.0;
            var down = 0.0;
            for (var i = 0; i < _labels.Count; i++)
            {
                up = Math.Max(up, -_unitY[i]);
                down = Math.Max(down, _unitY[i]);
            }
            radius = Math.Max(MinRadius, radius - (above + below + 2 * Inset - height) / Math.Max(0.1, up + down));
            (above, below) = VerticalExtent(radius);
        }
        _radius = radius;
        _webHeight = above + below + 2 * Inset;
        _center = new Point(width / 2, Inset + above);
    }

    private (double Above, double Below) VerticalExtent(double radius)
    {
        var above = radius;
        var below = radius;
        for (var i = 0; i < _labels.Count; i++)
        {
            var y = _unitY[i] * (radius + _gap);
            var h = _labels[i].Height;
            above = Math.Max(above, _unitY[i] < -0.2 ? -y + h : -y + h / 2);
            below = Math.Max(below, _unitY[i] > 0.2 ? y + h : y + h / 2);
        }
        return (above, below);
    }

    private void PlaceLabels()
    {
        for (var i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            var ax = _center.X + _unitX[i] * (_radius + _gap);
            var ay = _center.Y + _unitY[i] * (_radius + _gap);
            var top = _unitY[i] < -0.2 ? ay - label.Height : _unitY[i] > 0.2 ? ay : ay - label.Height / 2;
            var side = Side(_unitX[i]);
            var originX = side > 0 ? ax : side < 0 ? ax - _labelMaxWidth : ax - _labelMaxWidth / 2;
            var boxX = side > 0 ? ax : side < 0 ? ax - label.Width : ax - label.Width / 2;
            label.Origin = new Point(originX, top);
            label.Box = new Rect(boxX, top, label.Width, label.Height);
        }
    }

    private void SyncGeometry()
    {
        foreach (var shape in _shapes)
        {
            shape.Sync(_center, _radius);
        }
        _ringsStale = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureInk();
        var empty = IsEmpty;
        var width = double.IsInfinity(availableSize.Width) ? PreferredWidth() : availableSize.Width;
        _legend.IsVisible = IsLegendVisible && !empty;
        _legend.Measure(new Size(width, double.PositiveInfinity));
        _overlay.Measure(new Size(width, double.PositiveInfinity));
        if (empty)
        {
            return new Size(width, Math.Min(availableSize.Height, 160));
        }
        var legend = LegendHeight();
        Fit(width, double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height - legend);
        return new Size(width, _webHeight + legend);
    }

    private double PreferredWidth()
    {
        EnsureLabels(false);
        var widest = 0.0;
        foreach (var label in _labels)
        {
            widest = Math.Max(widest, label.Width);
        }
        return Size + 2 * (LabelGap + widest + Inset);
    }

    private double LegendHeight() => _legend.IsVisible ? _legend.DesiredSize.Height + LegendGap : 0;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var legend = LegendHeight();
        _overlay.Arrange(new Rect(finalSize));
        if (IsEmpty)
        {
            _legend.Arrange(default);
            SyncTooltip(finalSize);
            return finalSize;
        }
        Fit(finalSize.Width, finalSize.Height - legend);
        var spare = Math.Max(0, finalSize.Height - _webHeight - legend) / 2;
        _center = _center.WithY(_center.Y + spare);
        _webRect = new Rect(0, spare, finalSize.Width, _webHeight);
        _legend.Arrange(_legend.IsVisible ? new Rect(0, spare + _webHeight + LegendGap, finalSize.Width, _legend.DesiredSize.Height) : default);
        PlaceLabels();
        SyncGeometry();
        SyncTooltip(finalSize);
        return finalSize;
    }

    // ---- painting -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The only paint function: a resting chart, a morph frame and an unfolding frame all draw through here. Geometry
    /// moves in place and the unfold is a transform, so a frame allocates nothing.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }
        var p = _palette;
        EnsureInk();
        if (IsEmpty)
        {
            if (_emptyText is null || _emptyVersion != p.Version)
            {
                _emptyText = ChartsBText.Make(EmptyLabel, p, p.TypeSM, p.TextMuted);
                _emptyVersion = p.Version;
            }
            context.DrawText(_emptyText, new Point((Bounds.Width - _emptyText.Width) / 2, (Bounds.Height - _emptyText.Height) / 2));
            return;
        }
        if (!_reveal.IsRevealed)
        {
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        var unfold = Math.Max(0, _unfold.Value);
        if (unfold > 0.001)
        {
            var scale = Matrix.CreateTranslation(-_center.X, -_center.Y) * Matrix.CreateScale(unfold, unfold) * Matrix.CreateTranslation(_center.X, _center.Y);
            using (context.PushTransform(scale))
            {
                PaintGrid(context);
                PaintShapes(context);
                PaintMarkers(context);
            }
        }
        var active = ActiveIndex;
        using (context.PushOpacity(ChartsBMath.Clamp(unfold, 0, 1)))
        {
            for (var i = 0; i < _labels.Count; i++)
            {
                (i == active ? _labels[i].Active : _labels[i].Rest).Draw(context, _labels[i].Origin);
            }
        }
    }

    private void PaintGrid(DrawingContext context)
    {
        var p = _palette;
        var rings = Math.Max(1, Rings);
        var ringPen = p.Pen(p.Border, 1);
        var outerPen = p.Pen(p.BorderStrong, 1);
        if (GridShape == RadarGridShape.Polygon)
        {
            EnsurePolygonRings(rings);
            for (var i = 0; i < _polygonRings.Count; i++)
            {
                context.DrawGeometry(null, i == _polygonRings.Count - 1 ? outerPen : ringPen, _polygonRings[i]);
            }
        }
        else
        {
            for (var i = 1; i <= rings; i++)
            {
                var r = _radius * i / rings;
                context.DrawEllipse(null, i == rings ? outerPen : ringPen, _center, r, r);
            }
        }
        for (var i = 0; i < _unitX.Length; i++)
        {
            context.DrawLine(ringPen, _center, new Point(_center.X + _unitX[i] * _radius, _center.Y + _unitY[i] * _radius));
        }
    }

    private void EnsurePolygonRings(int rings)
    {
        if (!_ringsStale && _polygonRings.Count == rings)
        {
            return;
        }
        _ringsStale = false;
        _polygonRings.Clear();
        for (var ring = 1; ring <= rings; ring++)
        {
            var r = _radius * ring / rings;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(_center.X + _unitX[0] * r, _center.Y + _unitY[0] * r), false);
                for (var i = 1; i < _unitX.Length; i++)
                {
                    ctx.LineTo(new Point(_center.X + _unitX[i] * r, _center.Y + _unitY[i] * r));
                }
                ctx.EndFigure(true);
            }
            _polygonRings.Add(geometry);
        }
    }

    private void PaintShapes(DrawingContext context)
    {
        var dim = _dim.Value;
        var forward = dim > 0.001 && _forward >= 0 && _forward < _shapes.Count ? _forward : -1;
        for (var i = 0; i < _shapes.Count; i++)
        {
            if (i == forward)
            {
                continue;
            }
            using (context.PushOpacity(1 - 0.6 * dim))
            {
                context.DrawGeometry(_shapes[i].Fill, _shapes[i].Pen, _shapes[i].Geometry);
            }
        }
        if (forward >= 0)
        {
            context.DrawGeometry(_shapes[forward].Fill, _shapes[forward].Pen, _shapes[forward].Geometry);
        }
    }

    /// <summary>The reading: a brighter spoke and a marker on each profile, gliding along the outlines between axes.</summary>
    private void PaintMarkers(DrawingContext context)
    {
        var presence = _markerFade.Value;
        var count = _unitX.Length;
        if (presence <= 0.001 || count == 0)
        {
            return;
        }
        var p = _palette;
        var position = _axisPosition.Value % count;
        if (position < 0)
        {
            position += count;
        }
        var from = (int)Math.Floor(position) % count;
        var to = (from + 1) % count;
        var t = position - Math.Floor(position);
        using (context.PushOpacity(presence))
        {
            var angle = (from + t) * 2 * Math.PI / count;
            context.DrawLine(p.Pen(p.TextMuted, 1), _center,
                new Point(_center.X + Math.Sin(angle) * _radius, _center.Y - Math.Cos(angle) * _radius));
            var surface = p.Brush(p.Surface);
            foreach (var shape in _shapes)
            {
                var x = ChartsBMath.Mix(shape.NowX[from], shape.NowX[to], t);
                var y = ChartsBMath.Mix(shape.NowY[from], shape.NowY[to], t);
                context.DrawEllipse(surface, shape.MarkerPen, new Point(_center.X + x * _radius, _center.Y + y * _radius), MarkerRadius, MarkerRadius);
            }
        }
    }

    /// <summary>Above the legend: the keyboard focus ring and the tooltip with its line keys.</summary>
    private void PaintOverlay(DrawingContext context)
    {
        var p = _palette;
        if (IsFocused && Classes.Contains(":focus-visible") && !IsEmpty)
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(_webRect.Deflate(1), p.RadiusControl));
        }
        var at = _tipAt.Value;
        using (context.PushTransform(Matrix.CreateTranslation(at.X, at.Y)))
        {
            _tooltip.Draw(context, p);
            var presence = _tipFade.Value;
            if (presence > 0.001)
            {
                PaintTipKeys(context, presence);
            }
        }
    }

    /// <summary>
    /// The line keys of the tooltip's rows. <see cref="ChartsBTooltip"/> draws swatches only, so its rows reserve the room
    /// (<c>Indent</c>) and the keys go on top, under the same fade and scale as the bubble.
    /// </summary>
    private void PaintTipKeys(DrawingContext context, double presence)
    {
        var size = _tooltip.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var scale = 0.96 + 0.04 * presence;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
        using (context.PushOpacity(presence))
        {
            var y = _tooltip.Padding.Top;
            for (var i = 0; i < _tipRows.Count; i++)
            {
                var row = _tipRows[i];
                y += (i > 0 ? _tooltip.RowGap : 0) + row.MarginTop;
                var height = Math.Max(row.MinHeight, Math.Max(row.Left?.Height ?? 0, row.Right?.Height ?? 0));
                if (_tipPens[i] is { } pen)
                {
                    var keyY = y + height / 2;
                    context.DrawLine(pen, new Point(_tooltip.Padding.Left, keyY), new Point(_tooltip.Padding.Left + LineKeyWidth, keyY));
                }
                y += height;
            }
        }
    }
}
