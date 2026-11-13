using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal sealed partial class BranchGraphStage
{
    private const double TextSize = 13;
    private const double SmallSize = 12;
    private const double MergeIconSize = 14;
    private const double MergeIconGap = 8;
    private const double PillHeight = 22;
    private const double PillPadding = 8;
    private const double PillDot = 6;
    private const double PillGap = 4;
    private const double PillLead = 8;
    private const double AvatarSize = 20;
    private const double HashWidth = 64;
    private const double DateWidth = 88;
    private const double DotRadius = 5;
    private const double HaloRadius = 8;
    private const double MergeRadius = 4.25;
    private const double LineWidth = 2;
    private const double RingWidth = 1.5;
    private const double DetailLeft = 72;
    private const double DetailOffset = 4;
    private const double DetailPadX = 16;
    private const double DetailPadY = 12;
    private const double DetailGap = 8;
    private const double DetailMargin = 8;
    private const double DiffBlock = 7;
    private const double DiffBlockGap = 2;

    /// <summary>Card widths (outer card = stage width + 18) from which the date, author and hash columns show.</summary>
    internal const double DateFrom = 720;
    internal const double AuthorFrom = 560;
    internal const double HashFrom = 400;

    private readonly ExplainerTextCache _text = new(1024);
    private readonly Dictionary<Color, IPen> _pens = new();
    private readonly Dictionary<Color, Pen> _drawPens = new();
    private readonly Dictionary<Color, IPen> _ringPens = new();
    private readonly Dictionary<long, ExplainerPath> _paths = new();
    private readonly Dictionary<long, (Point From, Point To, bool MergeIn)> _pathKeys = new();
    private readonly Dictionary<string, IBrush> _avatarBrushes = new();
    private IPen? _focusPen;
    private IPen? _haloPenless;
    private IPen? _iconPen;
    private IPen? _avatarRingPen;
    private IPen? _cardBorderPen;
    private IPen? _rowBorderPen;
    private IPen? _stageBorderPen;
    private Color _iconColor;

    private static readonly Geometry s_mergeIcon = ArcIcons.Parse(
        // Lucide git-merge, ISC.
        "M15 18a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M3 6a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M6 21V9a9 9 0 0 0 9 9");

    private static readonly Geometry s_commitIcon = ArcIcons.Parse(
        // Lucide git-commit-horizontal, ISC.
        "M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M3 12L9 12 M15 12L21 12");

    private void ClearCaches()
    {
        _text.Clear();
        _pens.Clear();
        _drawPens.Clear();
        _ringPens.Clear();
        _avatarBrushes.Clear();
        _focusPen = _haloPenless = _iconPen = _avatarRingPen = _cardBorderPen = _rowBorderPen = _stageBorderPen = null;
    }

    private bool ShowsDate => Bounds.Width + 18 >= DateFrom;

    private bool ShowsAuthor => Bounds.Width + 18 >= AuthorFrom;

    private bool ShowsHash => Bounds.Width + 18 >= HashFrom;

    private bool IsNarrow => Bounds.Width + 18 < AuthorFrom;

    private Typeface Body => new(FontFamily);

    private Typeface Medium => new(FontFamily, FontStyle.Normal, FontWeight.Medium);

    private Typeface Mono => new(MonoFontFamily ?? FontFamily);

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    /// <summary>A branch's color: lane colors 1, 2, 4, 3 by the branch's order (the first follows the accent).</summary>
    private Color BranchColor(int index) => ColorOf((index % 4) switch
    {
        0 => Lane1,
        1 => Lane2,
        2 => Lane4,
        _ => Lane3
    });

    private static Color Mix(Color a, Color b, double t) => Oklch.MixOklab(a, b, t);

    private static Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)Math.Round(c.A * Math.Clamp(alpha, 0, 1)), c.R, c.G, c.B);

    private IPen SolidPen(Color color)
    {
        if (!_pens.TryGetValue(color, out var pen))
        {
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), LineWidth, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[color] = pen;
        }
        return pen;
    }

    private Pen DrawPen(Color color)
    {
        if (!_drawPens.TryGetValue(color, out var pen))
        {
            pen = new Pen(new ImmutableSolidColorBrush(color), LineWidth, new DashStyle(), PenLineCap.Flat, PenLineJoin.Round);
            _drawPens[color] = pen;
        }
        return pen;
    }

    private IPen RingPen(Color color, double width)
    {
        if (!_ringPens.TryGetValue(color, out var pen) || Math.Abs(pen.Thickness - width) > 0.01)
        {
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), width);
            _ringPens[color] = pen;
        }
        return pen;
    }

    private static IPen PenOf(ref IPen? cache, IBrush? brush, double width)
    {
        if (cache is null || !ReferenceEquals(cache.Brush, brush))
        {
            cache = brush is null ? null : new ImmutablePen(brush.ToImmutable(), width);
        }
        return cache ?? new ImmutablePen(Brushes.Transparent, width);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var stageRadius = StageRadius.TopLeft;
        context.DrawRectangle(StageInk, PenOf(ref _stageBorderPen, StageBorderInk, 1), bounds.Deflate(0.5), stageRadius, stageRadius);
        if (_model is null || _layout is null)
        {
            return;
        }
        var width = bounds.Width;
        var order = DrawOrder();
        // Rows that sit in the list, then the lifted ones above them.
        foreach (var i in order)
        {
            if (_poses[i].Lift <= 0.001)
            {
                DrawRow(context, i, width, lifted: false);
            }
        }
        foreach (var i in order)
        {
            if (_poses[i].Lift > 0.001)
            {
                DrawRow(context, i, width, lifted: true);
            }
        }
        DrawGraph(context);
        DrawCard(context, width);
        if (IsFocused && Classes.Contains(":focus-visible") && FocusInk is { } focus)
        {
            var ring = bounds.Inflate(3);
            context.DrawRectangle(null, PenOf(ref _focusPen, focus, 2), ring, stageRadius + 3, stageRadius + 3);
        }
    }

    /// <summary>Items to draw, lowest first: rows by their lift so a lifted row covers the rest.</summary>
    private int[] DrawOrder()
    {
        var count = 0;
        for (var i = 0; i < _poses.Length; i++)
        {
            if (_poses[i].IsShown)
            {
                count++;
            }
        }
        var order = new int[count];
        var k = 0;
        for (var i = 0; i < _poses.Length; i++)
        {
            if (_poses[i].IsShown)
            {
                order[k++] = i;
            }
        }
        Array.Sort(order, (a, b) => _poses[a].Lift.CompareTo(_poses[b].Lift));
        return order;
    }

    private double RowTextOpacity(int i) => 1 - (1 - DimText) * _dim[i];

    private void DrawRow(DrawingContext context, int i, double width, bool lifted)
    {
        var pose = _poses[i];
        var item = _model!.Items[i];
        var top = RowTop(pose.RowY);
        var rect = new Rect(1 + SideInset, top, Math.Max(0, width - 2 - SideInset * 2), RowHeight);
        var radius = Math.Min(RowRadius.TopLeft, RowHeight / 2);
        var scale = 1 + BranchGraphMotion.LiftRowScale * pose.Lift;
        using var transform = context.PushTransform(Matrix.CreateTranslation(-rect.Center.X, -rect.Center.Y) * Matrix.CreateScale(scale, scale) *
            Matrix.CreateTranslation(rect.Center.X, rect.Center.Y));
        if (lifted && RaisedInk is { } raised)
        {
            using (context.PushOpacity(pose.Lift * pose.RowOpacity))
            {
                context.DrawRectangle(raised, null, new RoundedRect(rect, radius), CardShadow);
                context.DrawRectangle(null, PenOf(ref _rowBorderPen, StageBorderInk, 1), rect.Deflate(0.5), radius - 0.5, radius - 0.5);
            }
        }
        if (_fill[i] > 0.001 && HoverInk is { } hover)
        {
            using (context.PushOpacity(_fill[i] * pose.RowOpacity))
            {
                context.DrawRectangle(hover, null, rect, radius, radius);
            }
        }
        using (context.PushOpacity(pose.RowOpacity * RowTextOpacity(i)))
        {
            DrawRowContent(context, i, item, rect);
        }
    }

    private void DrawRowContent(DrawingContext context, int i, BranchGraphItem item, Rect rect)
    {
        var cy = rect.Center.Y;
        var narrow = IsNarrow;
        var gap = narrow ? 8 : 12;
        var right = rect.Right - (narrow ? 8 : 12);
        var muted = ColorOf(MutedInk);
        if (ShowsDate)
        {
            var date = _text.GetTrimmed(item.Date, Body, SmallSize, muted, DateWidth);
            date.Draw(context, new Point(right - date.Width, cy - date.Height / 2));
            right -= DateWidth + gap;
        }
        if (ShowsHash)
        {
            DrawHash(context, i, right - HashWidth, cy);
            right -= HashWidth + gap;
        }
        if (ShowsAuthor)
        {
            DrawAvatar(context, item, new Point(right - AvatarSize / 2, cy), AvatarSize / 2);
            right -= AvatarSize + gap;
        }
        var x = 1 + GutterOf(_model!.LaneCount);
        if (item.IsMerge)
        {
            DrawIcon(context, s_mergeIcon, new Point(x, cy - MergeIconSize / 2), MergeIconSize, muted);
            x += MergeIconSize + MergeIconGap;
        }
        // Pills sit at the end of the message column; the message takes what is left.
        var pills = _pills[i];
        var pillsWidth = 0.0;
        foreach (var pill in pills)
        {
            pillsWidth += PillWidth(pill) + PillGap;
        }
        if (pills.Length > 0)
        {
            pillsWidth += PillLead - PillGap;
        }
        var messageWidth = Math.Max(24, right - x - pillsWidth);
        var message = _text.GetTrimmed(item.Message, Body, TextSize, ColorOf(TextInk), messageWidth);
        message.Draw(context, new Point(x, cy - message.Height / 2));
        if (pills.Length > 0)
        {
            var q = _pillStart[i] > _t ? 0 : ArcEasings.Standard.Ease(BranchGraphMotion.Clamp01((_t - _pillStart[i]) / BranchGraphMotion.TagSeconds));
            if (double.IsNegativeInfinity(_pillStart[i]))
            {
                q = 1;
            }
            if (q > 0.001)
            {
                var px = Math.Max(x + message.Width + PillLead, right - pillsWidth + PillLead) - 4 * (1 - q);
                using (context.PushOpacity(q))
                {
                    foreach (var pill in pills)
                    {
                        px = DrawPill(context, pill, px, cy) + PillGap;
                    }
                }
            }
        }
    }

    private double PillWidth(in BranchGraphPill pill)
    {
        var text = _text.Get(pill.Text, Medium, SmallSize, Colors.Black);
        return PillPadding * 2 + PillDot + 6 + text.Width;
    }

    private double DrawPill(DrawingContext context, in BranchGraphPill pill, double x, double cy)
    {
        var lane = BranchColor(pill.ColorIndex);
        var ink = Mix(lane, ColorOf(TextInk), 0.2);
        var text = _text.Get(pill.Text, Medium, SmallSize, ink);
        var rect = new Rect(x, cy - PillHeight / 2, PillPadding * 2 + PillDot + 6 + text.Width, PillHeight);
        context.DrawRectangle(_text.Brush(lane, 0.12), null, rect, PillHeight / 2, PillHeight / 2);
        context.DrawEllipse(_text.Brush(lane), null, new Point(rect.X + PillPadding + PillDot / 2, cy), PillDot / 2, PillDot / 2);
        text.Draw(context, new Point(rect.X + PillPadding + PillDot + 6, cy - text.Height / 2));
        return rect.Right;
    }

    private void DrawHash(DrawingContext context, int i, double x, double cy)
    {
        var muted = ColorOf(MutedInk);
        var s = _t - _flashStart[i];
        var color = muted;
        var alpha = 1.0;
        var dy = 0.0;
        if (s >= 0 && s < BranchGraphMotion.HashFlashSeconds && _model is not null)
        {
            var accent = BranchColor(TargetColor());
            var u = s / BranchGraphMotion.HashFlashSeconds;
            if (u < 0.3)
            {
                var k = ArcEasings.Standard.Ease(u / 0.3);
                alpha = k;
                dy = 3 * (1 - k);
                color = accent;
            }
            else
            {
                // Quantized so the text cache keeps a handful of colors.
                var k = Math.Round(ArcEasings.Standard.Ease((u - 0.3) / 0.7) * 12) / 12;
                color = Mix(accent, muted, k);
            }
        }
        var hash = _text.GetTrimmed(_hash[i], Mono, SmallSize, color, HashWidth);
        using (context.PushOpacity(alpha))
        {
            hash.Draw(context, new Point(x, cy - hash.Height / 2 + dy));
        }
    }

    private int TargetColor()
    {
        if (_model is null)
        {
            return 0;
        }
        foreach (var item in _model.Items)
        {
            if (item.Commit is not null && item.Branch == _model.Target)
            {
                return item.ColorIndex;
            }
        }
        return 0;
    }

    private void DrawAvatar(DrawingContext context, BranchGraphItem item, Point center, double radius)
    {
        var circle = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        if (item.Avatar is { } image)
        {
            using (context.PushGeometryClip(new EllipseGeometry(circle)))
            {
                context.DrawImage(image, circle);
            }
        }
        else
        {
            context.DrawEllipse(AvatarBrush(item), null, center, radius, radius);
        }
        context.DrawEllipse(null, PenOf(ref _avatarRingPen, StageBorderInk, 1), center, radius + 0.5, radius + 0.5);
    }

    /// <summary>A soft two-color disc for an author without a picture: hues from <see cref="BranchGraphCommit.AuthorColor"/> or the name.</summary>
    private IBrush AvatarBrush(BranchGraphItem item)
    {
        var key = item.Author + "|" + item.AuthorColor;
        if (_avatarBrushes.TryGetValue(key, out var brush))
        {
            return brush;
        }
        double hue;
        if (item.AuthorColor is { } color)
        {
            hue = color.ToHsv().H;
        }
        else
        {
            var hash = 2166136261u;
            foreach (var ch in item.Author)
            {
                hash = (hash ^ ch) * 16777619u;
            }
            hue = hash % 360;
        }
        var a = HsvColor.ToRgb(hue, 0.6, 0.9);
        var b = HsvColor.ToRgb((hue + 70) % 360, 0.55, 0.8);
        brush = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, a), new ImmutableGradientStop(1, b)],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
        _avatarBrushes[key] = brush;
        return brush;
    }

    private void DrawIcon(DrawingContext context, Geometry icon, Point topLeft, double size, Color color)
    {
        if (_iconPen is null || _iconColor != color)
        {
            _iconColor = color;
            _iconPen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.75, null, PenLineCap.Round, PenLineJoin.Round);
        }
        var scale = size / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(topLeft.X, topLeft.Y)))
        {
            context.DrawGeometry(null, _iconPen, icon);
        }
    }

    // ---- the graph -------------------------------------------------------------------------------------------------------

    private double NodeOpacity(int i) => 1 - (1 - DimNode) * _dim[i];

    private void DrawGraph(DrawingContext context)
    {
        // Edges.
        for (var e = 0; e < _edgeTracks.Count; e++)
        {
            var progress = e < _edgeProgress.Length ? _edgeProgress[e] : 0;
            if (progress <= 0.001)
            {
                continue;
            }
            var spec = _edgeTracks[e].Spec;
            var parent = _poses[spec.Parent];
            var child = _poses[spec.Child];
            var path = PathOf(spec, DotOf(parent), DotOf(child));
            var color = BranchColor(spec.ColorIndex);
            var dim = Math.Min(_dim[spec.Parent], _dim[spec.Child]);
            using (context.PushOpacity(1 - (1 - DimEdge) * dim))
            {
                if (progress >= 0.999)
                {
                    context.DrawGeometry(null, SolidPen(color), path.Geometry);
                }
                else
                {
                    var pen = DrawPen(color);
                    if (path.ShowPart(pen, 0, path.Length * progress))
                    {
                        context.DrawGeometry(null, pen, path.Geometry);
                    }
                }
            }
        }
        // Landing rings.
        var local = _t - _transitionStart;
        for (var i = 0; i < _tracks.Length; i++)
        {
            var ring = _tracks[i].Ring;
            if (double.IsNaN(ring))
            {
                continue;
            }
            var (radius, opacity) = BranchGraphMotion.RingAt(local - ring);
            if (opacity <= 0.001)
            {
                continue;
            }
            var color = BranchColor(_model!.Items[i].ColorIndex);
            using (context.PushOpacity(opacity * NodeOpacity(i)))
            {
                context.DrawEllipse(null, RingPen(color, RingWidth), DotOf(_poses[i]), radius, radius);
            }
        }
        // Dots: the stage-colored halo cuts the lines, then the dot or the merge ring.
        for (var i = 0; i < _poses.Length; i++)
        {
            var pose = _poses[i];
            if (pose.Opacity <= 0.001)
            {
                continue;
            }
            var item = _model!.Items[i];
            var center = DotOf(pose);
            var s = pose.Scale * (i < _hoverScale.Length ? _hoverScale[i] : 1);
            var color = BranchColor(item.ColorIndex);
            using (context.PushOpacity(pose.Opacity * NodeOpacity(i)))
            {
                context.DrawEllipse(StageInk, null, center, HaloRadius * s, HaloRadius * s);
                if (item.IsMerge)
                {
                    context.DrawEllipse(SurfaceInk, RingPen(color, LineWidth), center, MergeRadius * s, MergeRadius * s);
                }
                else
                {
                    context.DrawEllipse(_text.Brush(color), null, center, DotRadius * s, DotRadius * s);
                }
            }
        }
    }

    /// <summary>
    /// The edge's path between its two dots: a straight line in one lane, otherwise a vertical-tangent S-bend whose height
    /// grows with the lane distance (about 25 px for one lane, 36 for two), next to the parent for a branch and next to the
    /// child for a merge. Rebuilt only when an end moved.
    /// </summary>
    private ExplainerPath PathOf(in BranchGraphEdgeSpec spec, Point from, Point to)
    {
        var key = EdgeKey(spec);
        if (_paths.TryGetValue(key, out var path) && _pathKeys.TryGetValue(key, out var was) && was.From == from && was.To == to && was.MergeIn == spec.MergeIn)
        {
            return path;
        }
        path ??= new ExplainerPath();
        _paths[key] = path;
        _pathKeys[key] = (from, to, spec.MergeIn);
        path.Clear().MoveTo(from);
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) < 0.5 || Math.Abs(dy) < 0.5)
        {
            path.LineTo(to);
            return path;
        }
        var direction = Math.Sign(dy);
        var h = Math.Min(Math.Abs(dy), 6 * Math.Sqrt(Math.Abs(dx))) * direction;
        if (spec.MergeIn)
        {
            var start = new Point(from.X, to.Y - h);
            if (Math.Abs(start.Y - from.Y) > 0.5)
            {
                path.LineTo(start);
            }
            path.CubicTo(new Point(from.X, start.Y + h * 0.55), new Point(to.X, start.Y + h * 0.45), to, 16);
        }
        else
        {
            var bend = new Point(to.X, from.Y + h);
            path.CubicTo(new Point(from.X, from.Y + h * 0.55), new Point(to.X, from.Y + h * 0.45), bend, 16);
            if (Math.Abs(to.Y - bend.Y) > 0.5)
            {
                path.LineTo(to);
            }
        }
        return path;
    }

    // ---- the detail card -------------------------------------------------------------------------------------------------

    private double CardWidth => Math.Max(200, Math.Min(DetailWidth, Bounds.Width - DetailLeft - DetailMargin));

    private double CardLeft => Math.Min(DetailLeft, Math.Max(DetailMargin, Bounds.Width - CardWidth - DetailMargin));

    private double CardHeightOf(int item)
    {
        if (_model is null || item < 0)
        {
            return 0;
        }
        var data = _model.Items[item];
        var inner = CardWidth - DetailPadX * 2 - 2;
        var height = DetailPadY * 2 + 2;
        height += _text.GetTrimmed(data.Message, Medium, TextSize, Colors.Black, inner).Height;
        if (data.Body is { } body)
        {
            height += DetailGap + _text.GetWrapped(body, Body, TextSize, Colors.Black, inner).Height;
        }
        height += DetailGap + AvatarSize;
        height += DetailGap + 1 + 8 + _text.Get("0", Mono, SmallSize, Colors.Black).Height;
        return height;
    }

    private bool CardPlacedAbove(int item)
    {
        var pose = _poses[item];
        var below = RowTop(pose.RowY) + RowHeight + DetailOffset;
        var limit = 1 + TopInset + Math.Max(1, _rows) * RowHeight + BottomInset - DetailMargin;
        return below + CardHeightOf(item) > limit;
    }

    private double CardTargetY()
    {
        if (_cardItem < 0 || _cardItem >= _poses.Length)
        {
            return _cardY;
        }
        var pose = _poses[_cardItem];
        var above = CardPlacedAbove(_cardItem);
        _cardAbove = above;
        return above
            ? RowTop(pose.RowY) - DetailOffset - CardHeightOf(_cardItem)
            : RowTop(pose.RowY) + RowHeight + DetailOffset;
    }

    private void DrawCard(DrawingContext context, double width)
    {
        if (_cardItem < 0 || _cardOpacity <= 0.001 || _model is null || RaisedInk is null)
        {
            return;
        }
        var item = _model.Items[_cardItem];
        var cardWidth = CardWidth;
        var height = CardHeightOf(_cardItem);
        var rect = new Rect(CardLeft, _cardY + _cardRise, cardWidth, height);
        var radius = CardRadius.TopLeft;
        var origin = new Point(rect.X + 24, _cardAbove ? rect.Bottom : rect.Y);
        using var opacity = context.PushOpacity(_cardOpacity);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(_cardScale, _cardScale) *
            Matrix.CreateTranslation(origin.X, origin.Y));
        context.DrawRectangle(RaisedInk, null, new RoundedRect(rect, radius), CardShadow);
        context.DrawRectangle(null, PenOf(ref _cardBorderPen, CardBorderInk, 1), rect.Deflate(0.5), radius - 0.5, radius - 0.5);
        var x = rect.X + 1 + DetailPadX;
        var inner = cardWidth - DetailPadX * 2 - 2;
        var y = rect.Y + 1 + DetailPadY;
        var text = ColorOf(TextInk);
        var secondary = ColorOf(SecondaryInk);
        var muted = ColorOf(MutedInk);
        var title = _text.GetTrimmed(item.Message, Medium, TextSize, text, inner);
        title.Draw(context, new Point(x, y));
        y += title.Height;
        if (item.Body is { } bodyText)
        {
            y += DetailGap;
            var body = _text.GetWrapped(bodyText, Body, TextSize, secondary, inner);
            body.Draw(context, new Point(x, y));
            y += body.Height;
        }
        y += DetailGap;
        DrawAvatar(context, item, new Point(x + AvatarSize / 2, y + AvatarSize / 2), AvatarSize / 2);
        var mx = x + AvatarSize + 8;
        var my = y + AvatarSize / 2;
        if (item.Author.Length > 0)
        {
            var author = _text.GetTrimmed(item.Author, Body, SmallSize, text, Math.Max(24, inner - AvatarSize - 8 - 90));
            author.Draw(context, new Point(mx, my - author.Height / 2));
            mx += author.Width + 8;
        }
        if (item.Date.Length > 0)
        {
            var date = _text.Get((item.Author.Length > 0 ? "·  " : string.Empty) + item.Date, Body, SmallSize, muted);
            date.Draw(context, new Point(mx, my - date.Height / 2));
        }
        y += AvatarSize + DetailGap;
        // Foot: a hairline, then hash, +/−, the five diff blocks and the files count.
        if (StageBorderInk is { } line)
        {
            context.FillRectangle(line, new Rect(x, y, inner, 1));
        }
        y += 1 + 8;
        var foot = _text.Get("0", Mono, SmallSize, muted).Height;
        var fy = y + foot / 2;
        DrawIcon(context, s_commitIcon, new Point(x, fy - 7), 14, secondary);
        var fx = x + 14 + 4;
        var hash = _text.Get(_hash[_cardItem], Mono, SmallSize, secondary);
        hash.Draw(context, new Point(fx, fy - hash.Height / 2));
        fx += hash.Width + 12;
        if (item.Additions is not null || item.Deletions is not null)
        {
            var added = item.Additions ?? 0;
            var removed = item.Deletions ?? 0;
            var success = ColorOf(SuccessInk);
            var danger = ColorOf(DangerInk);
            var plus = _text.Get("+" + added, Body, SmallSize, success);
            plus.Draw(context, new Point(fx, fy - plus.Height / 2));
            fx += plus.Width + 8;
            var minus = _text.Get("−" + removed, Body, SmallSize, danger);
            minus.Draw(context, new Point(fx, fy - minus.Height / 2));
            fx += minus.Width + 8;
            var green = added + removed == 0 ? 0 : (int)Math.Round(5.0 * added / (added + removed), MidpointRounding.AwayFromZero);
            var blockRadius = BlockRadius.TopLeft;
            for (var b = 0; b < 5; b++)
            {
                var block = new Rect(fx + b * (DiffBlock + DiffBlockGap), fy - DiffBlock / 2, DiffBlock, DiffBlock);
                context.DrawRectangle(_text.Brush(b < green ? success : danger), null, block, blockRadius, blockRadius);
            }
        }
        if (item.Files is { } files && _owner is not null)
        {
            var count = _text.Get(_owner.Labels.Files(files), Body, SmallSize, muted);
            count.Draw(context, new Point(x + inner - count.Width, fy - count.Height / 2));
        }
    }
}
