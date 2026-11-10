using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn part of a <see cref="DnsResolution"/>: two readouts (round trips, lookup time), the diagram (browser,
/// resolver with its cache, and the three name servers, joined by curves packets travel along) and a log of the last four
/// messages. Wide (from 520px) the servers stand in a column on the right; narrower they sit in a row under the browser
/// and the resolver. Everything is drawn from the owner's scene at the current story time, so pausing freezes it and
/// stepping lands on the same picture; frames allocate nothing (geometry is rebuilt only when the size changes).
/// </summary>
internal sealed class DnsResolutionStage : Control
{
    internal static readonly StyledProperty<IBrush?> QueryInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(QueryInk));

    internal static readonly StyledProperty<IBrush?> ReferralInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(ReferralInk));

    internal static readonly StyledProperty<IBrush?> AnswerInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(AnswerInk));

    internal static readonly StyledProperty<IBrush?> MissInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(MissInk));

    internal static readonly StyledProperty<IBrush?> NodeFillProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(NodeFill));

    internal static readonly StyledProperty<IBrush?> NodeBorderProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(NodeBorder));

    internal static readonly StyledProperty<IBrush?> LineInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(LineInk));

    internal static readonly StyledProperty<IBrush?> TextInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(TextInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<DnsResolutionStage, IBrush?>(nameof(MutedInk));


    internal static readonly StyledProperty<CornerRadius> NodeRadiusProperty =
        AvaloniaProperty.Register<DnsResolutionStage, CornerRadius>(nameof(NodeRadius), new CornerRadius(14));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<DnsResolutionStage>();

    /// <summary>The width from which the servers stand in a column on the right.</summary>
    public const double WideWidth = 520;

    private const double StatsHeight = 44;
    private const double LogRowHeight = 22;
    private const int LogRows = 4;
    private const double LogGap = 16;
    private const double NodeHeight = 52;

    private readonly ExplainerTextCache _text = new(512);
    private readonly Rect[] _nodes = new Rect[5];
    private readonly Point[][] _curves = new Point[4][];
    private readonly Geometry?[] _curveGeometry = new Geometry?[4];
    private readonly Dictionary<(IBrush, double, bool), IPen> _pens = new();
    private readonly Dictionary<(DnsResolutionNode, DnsResolutionNode), string> _routes = new();
    private DnsResolution? _owner;
    private int _tripsShown;
    private string? _tripsText;
    private int _msShown;
    private string? _msText;
    private int _ttlShown;
    private string? _ttlText;
    private Rect _cache;
    private double _diagramTop;
    private double _diagramHeight;
    private double _layoutWidth = -1;
    private bool _wide;

    static DnsResolutionStage()
    {
        AffectsRender<DnsResolutionStage>(QueryInkProperty, ReferralInkProperty, AnswerInkProperty, MissInkProperty, NodeFillProperty,
            NodeBorderProperty, LineInkProperty, TextInkProperty, SecondaryInkProperty, MutedInkProperty, NodeRadiusProperty);
        AffectsMeasure<DnsResolutionStage>(FontFamilyProperty);
    }

    public DnsResolutionStage()
    {
        for (var i = 0; i < _curves.Length; i++)
        {
            _curves[i] = new Point[4];
        }
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) =>
        {
            _text.Clear();
            _pens.Clear();
            InvalidateVisual();
        };
    }

    public IBrush? QueryInk { get => GetValue(QueryInkProperty); set => SetValue(QueryInkProperty, value); }

    public IBrush? ReferralInk { get => GetValue(ReferralInkProperty); set => SetValue(ReferralInkProperty, value); }

    public IBrush? AnswerInk { get => GetValue(AnswerInkProperty); set => SetValue(AnswerInkProperty, value); }

    public IBrush? MissInk { get => GetValue(MissInkProperty); set => SetValue(MissInkProperty, value); }

    public IBrush? NodeFill { get => GetValue(NodeFillProperty); set => SetValue(NodeFillProperty, value); }

    public IBrush? NodeBorder { get => GetValue(NodeBorderProperty); set => SetValue(NodeBorderProperty, value); }

    public IBrush? LineInk { get => GetValue(LineInkProperty); set => SetValue(LineInkProperty, value); }

    public IBrush? TextInk { get => GetValue(TextInkProperty); set => SetValue(TextInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public CornerRadius NodeRadius { get => GetValue(NodeRadiusProperty); set => SetValue(NodeRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>True while the servers stand in a column on the right.</summary>
    public bool IsWide => _wide;

    /// <summary>A part's rectangle in the stage (tests).</summary>
    public Rect NodeRect(DnsResolutionNode node) => _nodes[(int)node];

    /// <summary>The cache box (tests).</summary>
    public Rect CacheRect => _cache;

    /// <summary>The words a screen reader hears for the stage now.</summary>
    public string Summary { get; private set; } = string.Empty;

    public void Attach(DnsResolution owner)
    {
        _owner = owner;
        Changed();
    }

    public void OnLabelsChanged()
    {
        _routes.Clear();
        Changed();
    }

    /// <summary>"From → To" for a log line, built once per pair.</summary>
    private string Route(DnsResolutionNode from, DnsResolutionNode to)
    {
        if (!_routes.TryGetValue((from, to), out var route))
        {
            route = from == to ? NodeName(from) : NodeName(from) + " → " + NodeName(to);
            _routes[(from, to)] = route;
        }
        return route;
    }

    /// <summary>The text for a number, built only when the number changes.</summary>
    private static string Number(ref int last, ref string? text, int value, string suffix)
    {
        if (text is null || value != last)
        {
            last = value;
            text = value.ToString(CultureInfo.InvariantCulture) + suffix;
        }
        return text;
    }

    /// <summary>The scene changed: update the summary and draw again.</summary>
    public void Changed()
    {
        _ttlText = null;
        UpdateSummary();
        InvalidateVisual();
    }

    /// <summary>The story clock moved.</summary>
    public void Tick() => InvalidateVisual();

    private void UpdateSummary()
    {
        if (_owner is null)
        {
            return;
        }
        var scene = _owner.Scene;
        var labels = _owner.Labels;
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{labels.RoundTrips} {scene.RoundTrips}, {labels.LookupTime} {(int)Math.Round(scene.ElapsedTarget)} ms");
        if (scene.Log.Count > 0)
        {
            var last = scene.Log[^1];
            text = $"{NodeName(last.From)} to {NodeName(last.To)}: {last.Text}. {text}";
        }
        Summary = text;
        AutomationProperties.SetName(this, text);
    }

    internal string NodeName(DnsResolutionNode node)
    {
        var labels = _owner?.Labels ?? new DnsResolutionLabels();
        return node switch
        {
            DnsResolutionNode.Client => labels.Client,
            DnsResolutionNode.Resolver => labels.Resolver,
            DnsResolutionNode.Root => labels.Root,
            DnsResolutionNode.Tld => labels.Tld,
            _ => labels.Authoritative
        };
    }

    private string NodeDetail(DnsResolutionNode node)
    {
        if (_owner is null)
        {
            return string.Empty;
        }
        return node switch
        {
            DnsResolutionNode.Client => _owner.Scene.HasClientAddress ? _owner.Address : _owner.Domain,
            DnsResolutionNode.Resolver => _owner.Labels.ResolverNote,
            DnsResolutionNode.Root => ".",
            DnsResolutionNode.Tld => _owner.TldText,
            _ => _owner.NameServerText
        };
    }

    private static Geometry NodeIcon(DnsResolutionNode node) => node switch
    {
        DnsResolutionNode.Client => ArcIcons.Laptop,
        DnsResolutionNode.Resolver => ArcIcons.Search,
        DnsResolutionNode.Root => ArcIcons.Globe,
        DnsResolutionNode.Tld => ArcIcons.Layers,
        _ => ArcIcons.ShieldCheck
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var diagram = width >= WideWidth ? 3 * NodeHeight + 2 * 12 : NodeHeight + 8 + 40 + 28 + 60;
        return new Size(width, StatsHeight + diagram + LogGap + LogRows * LogRowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
        {
            Solve(finalSize.Width);
        }
        return finalSize;
    }

    /// <summary>Places the parts and the curves for a width.</summary>
    private void Solve(double width)
    {
        _layoutWidth = width;
        _wide = width >= WideWidth;
        _diagramTop = StatsHeight;
        var top = _diagramTop;
        if (_wide)
        {
            var column = Math.Min(190, (width - 2 * 40) / 3);
            var middle = (width - column) / 2;
            var right = width - column;
            _nodes[(int)DnsResolutionNode.Root] = new Rect(right, top, column, NodeHeight);
            _nodes[(int)DnsResolutionNode.Tld] = new Rect(right, top + NodeHeight + 12, column, NodeHeight);
            _nodes[(int)DnsResolutionNode.Authoritative] = new Rect(right, top + 2 * (NodeHeight + 12), column, NodeHeight);
            _nodes[(int)DnsResolutionNode.Resolver] = new Rect(middle, top + NodeHeight + 12, column, NodeHeight);
            _nodes[(int)DnsResolutionNode.Client] = new Rect(0, top + NodeHeight + 12, column, NodeHeight);
            _cache = new Rect(middle, top + 2 * (NodeHeight + 12), column, NodeHeight);
            _diagramHeight = 3 * NodeHeight + 24;
        }
        else
        {
            var half = (width - 16) / 2;
            _nodes[(int)DnsResolutionNode.Client] = new Rect(0, top, half, NodeHeight);
            _nodes[(int)DnsResolutionNode.Resolver] = new Rect(half + 16, top, half, NodeHeight);
            _cache = new Rect(half + 16, top + NodeHeight + 8, half, 40);
            var third = (width - 16) / 3;
            var row = top + NodeHeight + 8 + 40 + 28;
            for (var i = 0; i < 3; i++)
            {
                _nodes[2 + i] = new Rect(i * (third + 8), row, third, 60);
            }
            _diagramHeight = NodeHeight + 8 + 40 + 28 + 60;
        }
        // Curves: browser–resolver, then resolver to each server.
        var client = _nodes[0];
        var resolver = _nodes[1];
        if (_wide)
        {
            SetCurve(0, new Point(client.Right, client.Center.Y), new Point(resolver.X, resolver.Center.Y));
            for (var i = 0; i < 3; i++)
            {
                var server = _nodes[2 + i];
                SetCurve(1 + i, new Point(resolver.Right, resolver.Center.Y), new Point(server.X, server.Center.Y));
            }
        }
        else
        {
            SetCurve(0, new Point(client.Right, client.Center.Y), new Point(resolver.X, resolver.Center.Y));
            for (var i = 0; i < 3; i++)
            {
                var server = _nodes[2 + i];
                // From the cache box's bottom (the resolver stands over it) down to each server.
                SetCurve(1 + i, new Point(_cache.Center.X, _cache.Bottom), new Point(server.Center.X, server.Y), vertical: true);
            }
        }
    }

    private void SetCurve(int index, Point from, Point to, bool vertical = false)
    {
        var points = _curves[index];
        points[0] = from;
        points[3] = to;
        if (vertical)
        {
            var dy = (to.Y - from.Y) / 2;
            points[1] = new Point(from.X, from.Y + dy);
            points[2] = new Point(to.X, to.Y - dy);
        }
        else
        {
            var dx = (to.X - from.X) / 2;
            points[1] = new Point(from.X + dx, from.Y);
            points[2] = new Point(to.X - dx, to.Y);
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(points[0], false);
            g.CubicBezierTo(points[1], points[2], points[3]);
            g.EndFigure(false);
        }
        _curveGeometry[index] = geometry;
    }

    private static Point Bezier(Point[] p, double t)
    {
        var u = 1 - t;
        var a = u * u * u;
        var b = 3 * u * u * t;
        var c = 3 * u * t * t;
        var d = t * t * t;
        return new Point(a * p[0].X + b * p[1].X + c * p[2].X + d * p[3].X, a * p[0].Y + b * p[1].Y + c * p[2].Y + d * p[3].Y);
    }

    /// <summary>The curve between two parts (0 browser–resolver, 1–3 resolver–server), or -1.</summary>
    private static int CurveOf(DnsResolutionNode a, DnsResolutionNode b)
    {
        if ((a, b) is (DnsResolutionNode.Client, DnsResolutionNode.Resolver) or (DnsResolutionNode.Resolver, DnsResolutionNode.Client))
        {
            return 0;
        }
        var server = a == DnsResolutionNode.Resolver ? b : a;
        return server >= DnsResolutionNode.Root ? (int)server - 1 : -1;
    }

    private IPen? Pen(IBrush? brush, double thickness, bool dashed = false)
    {
        if (brush is null)
        {
            return null;
        }
        if (!_pens.TryGetValue((brush, thickness, dashed), out var pen))
        {
            pen = dashed
                ? new ImmutablePen(brush.ToImmutable(), thickness, new ImmutableDashStyle([3, 3], 0), PenLineCap.Round)
                : new ImmutablePen(brush.ToImmutable(), thickness, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[(brush, thickness, dashed)] = pen;
        }
        return pen;
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    private IBrush? KindInk(DnsResolutionPacketKind kind) => kind switch
    {
        DnsResolutionPacketKind.Referral => ReferralInk,
        DnsResolutionPacketKind.Answer => AnswerInk,
        _ => QueryInk
    };

    public override void Render(DrawingContext context)
    {
        if (_owner is null || _layoutWidth < 0)
        {
            return;
        }
        var scene = _owner.Scene;
        var now = _owner.StoryTime;
        var animates = _owner.Animates;
        var typeface = new Typeface(FontFamily);
        DrawStats(context, scene, now, typeface);
        // The curves, faint; the one a packet travels on in the packet's color.
        var packet = scene.Packet is { } p && animates && now >= p.Start && now <= p.Start + p.Duration ? p : null;
        var activeCurve = packet is null ? -1 : CurveOf(packet.From, packet.To);
        for (var i = 0; i < 4; i++)
        {
            if (_curveGeometry[i] is not { } geometry)
            {
                continue;
            }
            if (i == activeCurve)
            {
                using (context.PushOpacity(0.55))
                {
                    context.DrawGeometry(null, Pen(KindInk(packet!.Kind), 2), geometry);
                }
            }
            else
            {
                context.DrawGeometry(null, Pen(LineInk, 1.25, dashed: true), geometry);
            }
        }
        for (var i = 0; i < 5; i++)
        {
            DrawNode(context, (DnsResolutionNode)i, scene, now, animates, typeface);
        }
        DrawCache(context, scene, now, animates, typeface);
        if (packet is not null && activeCurve >= 0)
        {
            DrawPacket(context, packet, activeCurve, now);
        }
        DrawLog(context, scene, now, animates, typeface);
    }

    private void DrawStats(DrawingContext context, DnsResolutionScene scene, double now, Typeface typeface)
    {
        var labels = _owner!.Labels;
        var muted = ColorOf(MutedInk);
        var text = ColorOf(TextInk);
        var x = 0.0;
        var label = _text.Get(labels.RoundTrips, typeface, 12, muted);
        label.Draw(context, new Point(x, 0));
        var value = _text.Get(Number(ref _tripsShown, ref _tripsText, scene.RoundTrips, string.Empty), typeface, 18, text);
        value.Draw(context, new Point(x, 16));
        x = Math.Max(label.Width, 60) + 28;
        label = _text.Get(labels.LookupTime, typeface, 12, muted);
        label.Draw(context, new Point(x, 0));
        var ms = (int)Math.Round(scene.ElapsedMs.Value(now));
        value = _text.Get(Number(ref _msShown, ref _msText, ms, " ms"), typeface, 18, text);
        value.Draw(context, new Point(x, 16));
    }

    private void DrawNode(DrawingContext context, DnsResolutionNode node, DnsResolutionScene scene, double now, bool animates, Typeface typeface)
    {
        var rect = _nodes[(int)node];
        var radius = Math.Min(NodeRadius.TopLeft, rect.Height / 2);
        context.DrawRectangle(NodeFill, Pen(NodeBorder, 1), rect, radius, radius);
        // The part the story is at: an accent ring that fades in.
        if (scene.Current == (int)node && !double.IsNaN(scene.LitAt[(int)node]))
        {
            var lit = animates ? Math.Clamp((now - scene.LitAt[(int)node]) / 0.2, 0, 1) : 1;
            if (lit > 0)
            {
                using (context.PushOpacity(lit))
                {
                    context.DrawRectangle(null, Pen(QueryInk, 2), rect, radius, radius);
                }
            }
        }
        var iconBrush = scene.Current == (int)node ? QueryInk : SecondaryInk;
        const double iconSize = 16;
        var iconX = rect.X + 12;
        var iconY = rect.Center.Y - iconSize / 2;
        var narrowServer = !_wide && node >= DnsResolutionNode.Root;
        if (narrowServer)
        {
            iconX = rect.Center.X - iconSize / 2;
            iconY = rect.Y + 8;
        }
        using (context.PushTransform(Matrix.CreateScale(iconSize / 24, iconSize / 24) * Matrix.CreateTranslation(iconX, iconY)))
        {
            context.DrawGeometry(null, Pen(iconBrush, 2), NodeIcon(node));
        }
        var nameColor = ColorOf(TextInk);
        var detailColor = node == DnsResolutionNode.Client && scene.HasClientAddress ? ColorOf(AnswerInk) : ColorOf(MutedInk);
        if (narrowServer)
        {
            var inner = Math.Max(10, rect.Width - 8);
            var name = _text.GetTrimmed(NodeName(node), typeface, 12, nameColor, inner);
            name.Draw(context, new Point(rect.Center.X - name.Width / 2, rect.Y + 28));
            var detail = _text.GetTrimmed(NodeDetail(node), typeface, 11, detailColor, inner);
            detail.Draw(context, new Point(rect.Center.X - detail.Width / 2, rect.Y + 42));
            return;
        }
        var textX = iconX + iconSize + 10;
        var width = Math.Max(10, rect.Right - textX - 10);
        var nameLayout = _text.GetTrimmed(NodeName(node), typeface, 13, nameColor, width);
        nameLayout.Draw(context, new Point(textX, rect.Y + 9));
        var detailLayout = _text.GetTrimmed(NodeDetail(node), typeface, 12, detailColor, width);
        detailLayout.Draw(context, new Point(textX, rect.Y + 27));
    }

    private void DrawCache(DrawingContext context, DnsResolutionScene scene, double now, bool animates, Typeface typeface)
    {
        var rect = _cache;
        var radius = Math.Min(NodeRadius.TopLeft, rect.Height / 2);
        context.DrawRectangle(null, Pen(LineInk, 1, dashed: true), rect, radius, radius);
        if (scene.CacheFlash != DnsResolutionCacheFlash.None)
        {
            var age = animates ? now - scene.CacheFlashAt : 0.3;
            var flash = Math.Clamp(1 - Math.Max(0, age - 0.3) / 0.9, 0, 1);
            if (flash > 0)
            {
                using (context.PushOpacity(flash))
                {
                    context.DrawRectangle(null, Pen(scene.CacheFlash == DnsResolutionCacheFlash.Hit ? AnswerInk : MissInk, 2), rect, radius, radius);
                }
            }
        }
        var labels = _owner!.Labels;
        var x = rect.X + 12;
        var width = Math.Max(10, rect.Width - 24);
        var muted = ColorOf(MutedInk);
        if (_wide)
        {
            _text.GetTrimmed(labels.Cache, typeface, 12, muted, width).Draw(context, new Point(x, rect.Y + 8));
        }
        string line;
        Color color;
        if (scene.HasCacheEntry)
        {
            var age = Math.Max(0, now - scene.CachedAt);
            // A minute of story time is a second on screen: the time to live visibly runs down by the second lookup.
            var left = Math.Max(0, _owner.TimeToLive - (int)Math.Floor(age * 4));
            if (_ttlText is null || left != _ttlShown)
            {
                _ttlShown = left;
                _ttlText = string.Create(CultureInfo.InvariantCulture, $"{_owner.Address} · {left} s");
            }
            line = _ttlText;
            color = ColorOf(TextInk);
        }
        else
        {
            line = labels.CacheEmpty;
            color = muted;
        }
        var y = _wide ? rect.Y + 27 : rect.Center.Y - 8;
        if (!_wide)
        {
            // Narrow: the heading shares the line (built when the line changes, not every frame).
            line = CacheLine(labels.Cache, line);
        }
        _text.GetTrimmed(line, typeface, 12, color, width).Draw(context, new Point(x, y));
    }

    private string? _cacheLineKey;
    private string? _cacheLine;

    private string CacheLine(string heading, string line)
    {
        if (!ReferenceEquals(line, _cacheLineKey) || _cacheLine is null)
        {
            _cacheLineKey = line;
            _cacheLine = heading + ": " + line;
        }
        return _cacheLine;
    }

    private void DrawPacket(DrawingContext context, DnsResolutionPacket packet, int curve, double now)
    {
        var progress = Math.Clamp((now - packet.Start) / packet.Duration, 0, 1);
        var eased = ArcEasings.InOut.Ease(progress);
        // Curves run from the resolver (or the browser) outwards; replies travel them backwards.
        var forward = packet.From == DnsResolutionNode.Client || (packet.From == DnsResolutionNode.Resolver && packet.To != DnsResolutionNode.Client);
        var points = _curves[curve];
        var ink = KindInk(packet.Kind);
        if (ink is null)
        {
            return;
        }
        for (var k = 4; k >= 1; k--)
        {
            var t = Math.Clamp(eased - k * 0.035, 0, 1);
            var at = Bezier(points, forward ? t : 1 - t);
            using (context.PushOpacity(0.5 - k * 0.1))
            {
                context.DrawEllipse(ink, null, at, 4 - k * 0.5, 4 - k * 0.5);
            }
        }
        var head = Bezier(points, forward ? eased : 1 - eased);
        context.DrawEllipse(ink, null, head, 5, 5);
    }

    private void DrawLog(DrawingContext context, DnsResolutionScene scene, double now, bool animates, Typeface typeface)
    {
        var top = _diagramTop + _diagramHeight + LogGap;
        var count = scene.Log.Count;
        var first = Math.Max(0, count - LogRows);
        var width = Bounds.Width;
        for (var i = first; i < count; i++)
        {
            var line = scene.Log[i];
            var row = i - first;
            var age = animates ? now - line.At : 1;
            var enter = Math.Clamp(age / 0.24, 0, 1);
            var y = top + row * LogRowHeight + (1 - enter) * 6;
            var newest = i == count - 1;
            using (context.PushOpacity(enter * (newest ? 1 : 0.72)))
            {
                var dot = KindInk(line.Kind);
                if (dot is not null)
                {
                    context.DrawEllipse(dot, null, new Point(4, y + 9), 3, 3);
                }
                var route = Route(line.From, line.To);
                var routeLayout = _text.GetTrimmed(route, typeface, 12, ColorOf(MutedInk), Math.Max(10, width * 0.42));
                routeLayout.Draw(context, new Point(14, y + 1));
                var x = 14 + Math.Max(routeLayout.Width, Math.Min(width * 0.42, 170)) + 12;
                var message = _text.GetTrimmed(line.Text, typeface, 13, ColorOf(newest ? TextInk : SecondaryInk), Math.Max(10, width - x));
                message.Draw(context, new Point(x, y));
            }
        }
    }
}
