using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

public partial class SankeyFlow
{
    private const double DimmedOpacity = 0.3;
    private const double LabelGap = 8;
    private const double RibbonAlpha = 0.34;
    private const double BandAlpha = 0.36;
    private const int ParticleLevels = 3;
    private const double ParticleTextMargin = 3;
    private const double GoldenRatio = 0.6180339887498949;
    private const double PlasticRatio = 0.7548776662466927;

    private static readonly double[] s_particleAlpha = [0.3, 0.65, 0.95];

    private readonly List<TextLayout?> _nodeNames = new();
    private readonly List<TextLayout?> _nodeValues = new();
    private readonly List<TextLayout> _headers = new();
    private FormattedText? _empty;
    private bool _textDirty = true;
    private int _textVersion = -1;
    private int _traceStamp;
    private readonly IBrush?[] _ribbonBrushes = new IBrush?[16];
    private bool _ribbonBrushUpright;
    private int _ribbonBrushVersion = -1;
    private readonly List<Rect> _particleKeepOut = [];
    private readonly IBrush?[] _particleBrushes = new IBrush?[(4 + 1) * ParticleLevels];
    private int _particleBrushVersion = -1;

    private TextLayout MakeText(string text, double size, Color color, double maxWidth, FontWeight weight = FontWeight.Normal)
        => new(text, new Typeface(_palette.BodyFont, FontStyle.Normal, weight), size, _palette.Brush(color), TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis, textDecorations: null, FlowDirection.LeftToRight, Math.Max(1, maxWidth), double.PositiveInfinity);

    private void DisposeText()
    {
        foreach (var layout in _nodeNames)
        {
            layout?.Dispose();
        }
        foreach (var layout in _nodeValues)
        {
            layout?.Dispose();
        }
        foreach (var layout in _headers)
        {
            layout.Dispose();
        }
        _nodeNames.Clear();
        _nodeValues.Clear();
        _headers.Clear();
    }

    private void EnsureText()
    {
        var p = _palette;
        if (!_textDirty && _textVersion == p.Version)
        {
            return;
        }
        _textDirty = false;
        _textVersion = p.Version;
        DisposeText();
        _empty = ChartsBText.Make(EmptyLabel, p, p.TypeSM, p.TextMuted);
        if (_model.IsEmpty || _placement is null || _placement.Nodes.Length != _model.Nodes.Count)
        {
            return;
        }
        var count = _model.Nodes.Count;
        for (var i = 0; i < count; i++)
        {
            _nodeNames.Add(null);
            _nodeValues.Add(null);
        }
        var nameHeight = Math.Ceiling(p.TypeXS * 1.4);
        var columns = _placement.Columns;
        var spacing = columns.Length > 1 ? Math.Max(40, (_placedSize.Width - NodeThickness) / (columns.Length - 1) - NodeThickness - LabelGap * 2) : _placedSize.Width / 2;
        for (var c = 0; c < columns.Length; c++)
        {
            var members = columns[c];
            var lastBottom = double.NegativeInfinity;
            for (var i = 0; i < members.Length; i++)
            {
                var n = members[i];
                var pose = _placement.Nodes[n];
                var node = _model.Nodes[n];
                if (_upright)
                {
                    // Beneath the bar, as wide as its share of the row.
                    var room = pose.Extent + 4;
                    if (room >= 24)
                    {
                        _nodeNames[n] = MakeText(node.Label, p.TypeXS, p.Foreground, room, FontWeight.Medium);
                        if (room >= 48)
                        {
                            _nodeValues[n] = MakeText(Format(node.Value), p.TypeXS, p.TextSecondary, room);
                        }
                    }
                    continue;
                }
                // Beside the bar: two lines when the neighbours leave room, one when they barely do, none when they do not.
                var center = pose.CrossCenter;
                var nextTop = i + 1 < members.Length ? _placement.Nodes[members[i + 1]].CrossCenter - nameHeight / 2 : double.PositiveInfinity;
                if (center - nameHeight / 2 < lastBottom)
                {
                    continue;
                }
                _nodeNames[n] = MakeText(node.Label, p.TypeXS, p.Foreground, spacing, FontWeight.Medium);
                var two = center - nameHeight >= lastBottom && center + nameHeight <= nextTop;
                if (two)
                {
                    _nodeValues[n] = MakeText(Format(node.Value), p.TypeXS, p.TextSecondary, spacing);
                }
                lastBottom = center + (two ? nameHeight : nameHeight / 2);
            }
        }
        if (Columns is { Count: > 0 } names)
        {
            for (var c = 0; c < Math.Min(names.Count, columns.Length); c++)
            {
                _headers.Add(MakeText(names[c], p.TypeXS, p.TextSecondary, _upright ? _placedSize.Width - Inset * 2 : Math.Max(40, spacing + LabelGap)));
            }
        }
    }

    private IBrush RibbonBrush(int sourceColumn, int targetColumn)
    {
        var p = _palette;
        if (_ribbonBrushVersion != p.Version || _ribbonBrushUpright != _upright)
        {
            _ribbonBrushVersion = p.Version;
            _ribbonBrushUpright = _upright;
            Array.Clear(_ribbonBrushes);
        }
        var from = sourceColumn % 4;
        var to = targetColumn % 4;
        ref var brush = ref _ribbonBrushes[from * 4 + to];
        // Relative to each ribbon's own bounds, so one brush serves every ribbon and every morph frame.
        brush ??= new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, ChartsBMath.Alpha(p.Series(from), RibbonAlpha)), new ImmutableGradientStop(1, ChartsBMath.Alpha(p.Series(to), RibbonAlpha))],
            startPoint: _upright ? new RelativePoint(0.5, 0, RelativeUnit.Relative) : new RelativePoint(0, 0.5, RelativeUnit.Relative),
            endPoint: _upright ? new RelativePoint(0.5, 1, RelativeUnit.Relative) : new RelativePoint(1, 0.5, RelativeUnit.Relative));
        return brush;
    }

    private StreamGeometry Ribbon(in SankeyLinkPose pose, double thick)
    {
        var half = thick / 2;
        var reach = pose.Reach;
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(At(pose.M0, pose.C0 - half), true);
        ctx.CubicBezierTo(At(pose.M0 + reach, pose.C0 - half), At(pose.M1 - reach, pose.C1 - half), At(pose.M1, pose.C1 - half));
        ctx.LineTo(At(pose.M1, pose.C1 + half));
        ctx.CubicBezierTo(At(pose.M1 - reach, pose.C1 + half), At(pose.M0 + reach, pose.C0 + half), At(pose.M0, pose.C0 + half));
        ctx.EndFigure(true);
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        EnsureText();
        if (_model.IsEmpty && _linkItems.Count == 0)
        {
            context.DrawText(_empty!, new Point((width - _empty!.Width) / 2, (Bounds.Height - _empty.Height) / 2));
            return;
        }
        if (_placement is null)
        {
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        _particleKeepOut.Clear();
        // Stacked, a stage's name sits where the ribbons run, so it draws over them; side by side it sits above them.
        if (!_upright || !_reveal.IsRevealed)
        {
            DrawHeaders(context);
        }
        if (!_reveal.IsRevealed)
        {
            return;
        }
        var dim = _dim.Value;
        var trace = dim > 0.001 ? _trace : null;
        DrawRibbons(context, dim);
        if (trace is not null)
        {
            DrawTraceBands(context, trace, dim);
        }
        DrawNodes(context, trace, dim);
        if (_upright)
        {
            DrawHeaders(context);
        }
    }

    private void DrawHeaders(DrawingContext context)
    {
        if (_headers.Count == 0 || _placement is null)
        {
            return;
        }
        var columnCount = _model.ColumnCount;
        var run = Math.Max(0, _placedSize.Width - Inset * 2 - NodeThickness);
        for (var c = 0; c < _headers.Count; c++)
        {
            var layout = _headers[c];
            if (_upright)
            {
                var row = c < _placement.Columns.Length && _placement.Columns[c].Length > 0 ? _placement.Nodes[_placement.Columns[c][0]].Main : 0;
                var at = new Point(Inset, row - layout.Height - 2);
                layout.Draw(context, at);
                KeepParticlesOff(at, layout.WidthIncludingTrailingWhitespace, layout.Height);
                continue;
            }
            var main = Inset + (columnCount == 1 ? run / 2 : run * c / (columnCount - 1));
            var x = c == 0 ? main : c == columnCount - 1 ? main + NodeThickness - layout.WidthIncludingTrailingWhitespace : main + NodeThickness / 2 - layout.WidthIncludingTrailingWhitespace / 2;
            layout.Draw(context, new Point(x, 0));
        }
    }

    private void DrawRibbons(DrawingContext context, double dim)
    {
        using var dimmed = context.PushOpacity(1 - (1 - DimmedOpacity) * dim);
        foreach (var item in _linkItems)
        {
            if (item.Drawn.Thick <= 0.01)
            {
                continue;
            }
            if (item.FillStamp != _stamp)
            {
                item.Fill = Ribbon(item.Drawn, item.Drawn.Thick);
                item.FillStamp = _stamp;
            }
            context.DrawGeometry(RibbonBrush(item.SourceColumn, item.TargetColumn), null, item.Fill!);
        }
    }

    /// <summary>The traced share of each ribbon: a band in the accent color, poured outward from what was picked.</summary>
    private void DrawTraceBands(DrawingContext context, SankeyFlowTrace trace, double dim)
    {
        var p = _palette;
        var brush = p.Brush(ChartsBMath.Alpha(p.Accent, BandAlpha));
        var front = ChartsBMath.Clamp(_pour.Value, 0, 1) * trace.MaxHop;
        var stamp = _stamp + (_traceStamp << 16);
        using var opacity = context.PushOpacity(dim);
        for (var i = 0; i < _model.Links.Count && i < trace.LinkFlow.Length; i++)
        {
            var flow = trace.LinkFlow[i];
            if (flow <= 0)
            {
                continue;
            }
            var item = _linkItems[i];
            var local = trace.LinkHop[i] == 0 ? 1 : ChartsBMath.Clamp(front - (trace.LinkHop[i] - 1), 0, 1);
            if (local <= 0.001)
            {
                continue;
            }
            if (item.BandStamp != stamp)
            {
                item.Band = Ribbon(item.Drawn, item.Drawn.Thick * Math.Min(1, flow / _model.Links[i].Value));
                item.BandStamp = stamp;
            }
            var pose = item.Drawn;
            if (local >= 0.999 || pose.M1 < pose.M0)
            {
                using (context.PushOpacity(pose.M1 < pose.M0 ? local : 1))
                {
                    context.DrawGeometry(brush, null, item.Band!);
                }
                continue;
            }
            // Clipped along the flow, so a partly poured ribbon needs no new geometry.
            var span = pose.M1 - pose.M0;
            var start = trace.LinkUpstream[i] ? pose.M1 - span * local : pose.M0;
            var end = trace.LinkUpstream[i] ? pose.M1 : pose.M0 + span * local;
            var clip = _upright ? new Rect(-1e5, start, 2e5, end - start) : new Rect(start, -1e5, end - start, 2e5);
            using (context.PushClip(clip))
            {
                context.DrawGeometry(brush, null, item.Band!);
            }
        }
    }

    private void DrawNodes(DrawingContext context, SankeyFlowTrace? trace, double dim)
    {
        var p = _palette;
        var radius = NodeRadius;
        var front = trace is null ? 0 : ChartsBMath.Clamp(_pour.Value, 0, 1) * trace.MaxHop;
        for (var i = 0; i < _nodeItems.Count; i++)
        {
            var item = _nodeItems[i];
            var pose = item.Drawn;
            var rect = RectOf(pose);
            var flow = trace is not null && i < trace.NodeFlow.Length ? trace.NodeFlow[i] : 0;
            var opacity = trace is not null && flow <= 0 ? 1 - (1 - DimmedOpacity) * dim : 1;
            using (context.PushOpacity(opacity))
            {
                if (pose.Extent > 0.01)
                {
                    context.DrawRectangle(p.Brush(p.Series(item.Column % 4)), null, new RoundedRect(rect, radius));
                    if (flow > 0 && i < _model.Nodes.Count)
                    {
                        DrawNodeBand(context, trace!, pose, flow / Math.Max(1e-9, _model.Nodes[i].Value), i, front, dim, radius);
                    }
                }
            }
        }
        if (_focusVisible && IsFocused && _keyNode >= 0 && _keyNode < _nodeItems.Count)
        {
            var ring = RectOf(_nodeItems[_keyNode].Drawn).Inflate(3);
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(ring, radius + 3));
        }
    }

    /// <summary>The top layer: labels above the particles so dots never cross the text, then the tooltip.</summary>
    private void PaintOverlay(DrawingContext context)
    {
        if (Bounds.Width > 0 && _placement is not null && _reveal.IsRevealed && !_model.IsEmpty)
        {
            EnsureText();
            var dim = _dim.Value;
            var trace = dim > 0.001 ? _trace : null;
            using var fade = context.PushOpacity(_reveal.Fade);
            for (var i = 0; i < _model.Nodes.Count && i < _nodeNames.Count && i < _nodeItems.Count; i++)
            {
                var dimmed = trace is not null && i < trace.NodeFlow.Length && trace.NodeFlow[i] <= 0;
                using (context.PushOpacity(dimmed ? 1 - (1 - DimmedOpacity) * dim : 1))
                {
                    DrawNodeLabel(context, i, _nodeItems[i].Drawn);
                }
            }
        }
        _tooltip.Draw(context, _palette);
    }

    /// <summary>The traced part of a node in the accent color, centered like the ribbons that carry it.</summary>
    private void DrawNodeBand(DrawingContext context, SankeyFlowTrace trace, in SankeyNodePose pose, double fraction, int index, double front, double dim, double radius)
    {
        var hop = trace.NodeHop[index];
        var local = hop == 0 ? 1 : ChartsBMath.Clamp(front - (hop - 1), 0, 1);
        if (local <= 0.001)
        {
            return;
        }
        var band = pose;
        band.Extent = pose.Extent * Math.Min(1, fraction);
        band.Cross = pose.CrossCenter - band.Extent / 2;
        using (context.PushOpacity(dim * local))
        {
            context.DrawRectangle(_palette.Brush(_palette.Accent), null, new RoundedRect(RectOf(band), radius));
        }
    }

    private void DrawNodeLabel(DrawingContext context, int index, in SankeyNodePose pose)
    {
        var name = _nodeNames[index];
        if (name is null)
        {
            return;
        }
        var value = _nodeValues[index];
        var height = name.Height + (value?.Height ?? 0);
        if (_upright)
        {
            var center = pose.CrossCenter;
            var y = pose.Main + pose.Thickness + 3;
            var nameAt = new Point(ChartsBMath.Clamp(center - name.WidthIncludingTrailingWhitespace / 2, 0, Math.Max(0, _placedSize.Width - name.WidthIncludingTrailingWhitespace)), y);
            name.Draw(context, nameAt);
            KeepParticlesOff(nameAt, name.WidthIncludingTrailingWhitespace, name.Height);
            if (value is not null)
            {
                var valueAt = new Point(ChartsBMath.Clamp(center - value.WidthIncludingTrailingWhitespace / 2, 0, Math.Max(0, _placedSize.Width - value.WidthIncludingTrailingWhitespace)), y + name.Height);
                value.Draw(context, valueAt);
                KeepParticlesOff(valueAt, value.WidthIncludingTrailingWhitespace, value.Height);
            }
            return;
        }
        var last = _model.ColumnCount > 1 && _model.Nodes[index].Column == _model.ColumnCount - 1;
        var top = pose.CrossCenter - height / 2;
        var nameX = last ? pose.Main - LabelGap - name.WidthIncludingTrailingWhitespace : pose.Main + pose.Thickness + LabelGap;
        name.Draw(context, new Point(nameX, top));
        KeepParticlesOff(new Point(nameX, top), name.WidthIncludingTrailingWhitespace, name.Height);
        if (value is not null)
        {
            var valueX = last ? pose.Main - LabelGap - value.WidthIncludingTrailingWhitespace : nameX;
            value.Draw(context, new Point(valueX, top + name.Height));
            KeepParticlesOff(new Point(valueX, top + name.Height), value.WidthIncludingTrailingWhitespace, value.Height);
        }
    }

    /// <summary>Marks drawn text so the particle layer, which paints above it, leaves it readable.</summary>
    private void KeepParticlesOff(Point at, double width, double height) =>
        _particleKeepOut.Add(new Rect(at, new Size(width, height)).Inflate(ParticleTextMargin));

    /// <summary>Whether a particle at <paramref name="point"/> would land on text.</summary>
    internal static bool IsKeptOut(List<Rect> keepOut, Point point)
    {
        foreach (var rect in keepOut)
        {
            if (rect.Contains(point))
            {
                return true;
            }
        }
        return false;
    }

    // ---- particles (a layer of its own, so a frame repaints only the dots) ------------------------------------------

    private void EnsureParticleBrushes()
    {
        var p = _palette;
        if (_particleBrushVersion == p.Version)
        {
            return;
        }
        _particleBrushVersion = p.Version;
        for (var level = 0; level < ParticleLevels; level++)
        {
            for (var color = 0; color < 4; color++)
            {
                _particleBrushes[color * ParticleLevels + level] = new ImmutableSolidColorBrush(ChartsBMath.Alpha(p.Series(color), s_particleAlpha[level]));
            }
            // Solid accent dots on the translucent accent band of a trace.
            _particleBrushes[4 * ParticleLevels + level] = new ImmutableSolidColorBrush(ChartsBMath.Alpha(p.Accent, s_particleAlpha[level]));
        }
    }

    /// <summary>Where a ribbon's particle is at a moment: along the ribbon by its phase, across it by a fixed offset.</summary>
    private static void SampleParticle(in SankeyLinkPose pose, double seed, double speed, int particle, double clock, double spread, out double main, out double cross, out int level)
    {
        var t = (seed + particle * GoldenRatio + clock * speed) % 1;
        var lateral = ((seed * 7 + particle * PlasticRatio) % 1 - 0.5) * spread;
        main = pose.MainAt(t);
        cross = pose.CrossAt(t) + lateral;
        var edge = Math.Min(t, 1 - t);
        level = edge < 0.04 ? 0 : edge < 0.1 ? 1 : 2;
    }

    /// <summary>Walks every particle of the frame like <see cref="DrawParticles"/> does and returns how many there are; allocates nothing.</summary>
    internal int SampleParticles(double clock)
    {
        var count = 0;
        var sum = 0.0;
        for (var i = 0; i < _model.Links.Count; i++)
        {
            var item = _linkItems[i];
            for (var k = 0; k < item.Particles; k++)
            {
                SampleParticle(item.Drawn, item.Seed, item.Speed, k, clock, item.Drawn.Thick * 0.8, out var main, out var cross, out _);
                sum += main + cross;
                count++;
            }
        }
        return sum >= 0 || sum < 0 ? count : 0;
    }

    /// <summary>The planned particle count of a ribbon.</summary>
    internal int ParticleCountOf(int link) => _linkItems[link].Particles;

    private void DrawParticles(DrawingContext context)
    {
        if (!_drawingParticles || _modelDirty || _linkItems.Count < _model.Links.Count)
        {
            return;
        }
        EnsureParticleBrushes();
        var radius = ParticleRadius;
        var trace = _traced is not null ? _trace : null;
        var linkCount = _model.Links.Count;
        for (var i = 0; i < linkCount; i++)
        {
            var item = _linkItems[i];
            if (item.Particles == 0)
            {
                continue;
            }
            var spread = item.Drawn.Thick * 0.8;
            var palette = item.SourceColumn % 4;
            var count = item.Particles;
            if (trace is not null)
            {
                var flow = trace.LinkFlow[i];
                if (flow <= 0)
                {
                    continue;
                }
                var fraction = Math.Min(1, flow / _model.Links[i].Value);
                spread *= fraction;
                palette = 4;
                count = Math.Max(1, (int)Math.Ceiling(count * fraction));
            }
            for (var k = 0; k < count; k++)
            {
                SampleParticle(item.Drawn, item.Seed, item.Speed, k, _clock, spread, out var main, out var cross, out var level);
                var at = At(main, cross);
                if (!IsKeptOut(_particleKeepOut, at))
                {
                    context.DrawEllipse(_particleBrushes[palette * ParticleLevels + level], null, at, radius, radius);
                }
            }
        }
    }
}
