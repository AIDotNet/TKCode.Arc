using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The stage of a <see cref="MixtureOfExperts"/>: one drawn layer with the column labels, the input and output lines,
/// the router and its score bars, the gate lines, the merge curves and point, and the input and output chips; the expert
/// tiles sit on it as real, focusable controls, and the packets move on a layer of their own above them (the only layer
/// that redraws every frame while a packet travels). The geometry comes from <see cref="MixtureOfExpertsLayout"/> for the
/// stage's own width; the stage only draws what the card sets.
/// </summary>
internal sealed class MixtureOfExpertsStage : Control
{
    private readonly List<MixtureOfExpertsTile> _tiles = new();
    private readonly MixtureOfExpertsPackets _packets;
    private MixtureOfExperts? _owner;
    private MixtureOfExpertsLayout? _layout;
    private MixtureOfExpertsPlan? _plan;

    // Router.
    private MixtureOfExpertsFade _router = new(0);
    private MixtureOfExpertsFade[] _rows = [];
    private MixtureOfExpertsTween[] _fills = [];
    private int[] _percents = [];
    private MixtureOfExpertsFade[] _gates = [];
    private MixtureOfExpertsFade[] _curves = [];
    private MixtureOfExpertsFade _flowIn = new(0);

    // Chips: the one in the slot and the one leaving it.
    private int _input = -1;
    private int _inputLeaving = -1;
    private MixtureOfExpertsTween _inputIn = new(1);
    private MixtureOfExpertsTween _inputOut = new(0);
    private MixtureOfExpertsFade _inputLook = new(0);
    private int _output = -1;
    private int _outputLeaving = -1;
    private MixtureOfExpertsTween _outputIn = new(1);
    private MixtureOfExpertsTween _outputOut = new(0);
    private double _until;

    public MixtureOfExpertsStage()
    {
        ClipToBounds = false;
        _packets = new MixtureOfExpertsPackets(this);
        VisualChildren.Add(_packets);
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    public MixtureOfExperts? Owner => _owner;

    public MixtureOfExpertsLayout? Layout => _layout;

    public MixtureOfExpertsPlan? Plan => _plan;

    public IReadOnlyList<MixtureOfExpertsTile> Tiles => _tiles;

    public MixtureOfExpertsPackets Packets => _packets;

    /// <summary>The token in the input slot (−1: none).</summary>
    public int InputToken => _input;

    /// <summary>The token whose output sits in the output slot (−1: none).</summary>
    public int OutputToken => _output;

    public bool IsWide => _layout?.IsWide ?? true;

    /// <summary>Sets the experts: one tile per expert, rebuilt only when the count changes.</summary>
    public void SetExperts(MixtureOfExperts owner, IReadOnlyList<(string Name, Geometry? Icon)> experts)
    {
        _owner = owner;
        if (_tiles.Count != experts.Count || (_tiles.Count > 0 && _tiles[0].Owner != owner))
        {
            foreach (var tile in _tiles)
            {
                VisualChildren.Remove(tile);
                LogicalChildren.Remove(tile);
            }
            _tiles.Clear();
            for (var i = 0; i < experts.Count; i++)
            {
                var tile = new MixtureOfExpertsTile(owner, i);
                _tiles.Add(tile);
                // Tiles go under the packet layer.
                VisualChildren.Insert(VisualChildren.Count - 1, tile);
                LogicalChildren.Add(tile);
            }
            if (_tiles.Count > 0)
            {
                KeyboardNavigation.SetTabOnceActiveElement(this, _tiles[0]);
            }
            var n = experts.Count;
            _rows = new MixtureOfExpertsFade[n];
            _fills = new MixtureOfExpertsTween[n];
            _percents = new int[n];
            _gates = new MixtureOfExpertsFade[n];
            _curves = new MixtureOfExpertsFade[n];
            _layout = null;
        }
        for (var i = 0; i < experts.Count; i++)
        {
            _tiles[i].SetExpert(experts[i].Name, experts[i].Icon);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Moves every drawn part to <paramref name="scene"/>, fading on wall time when <paramref name="animate"/>.</summary>
    public void Apply(MixtureOfExpertsPlan plan, MixtureOfExpertsScene scene, double wall, bool animate)
    {
        _plan = plan;
        var n = Math.Min(_rows.Length, plan.ExpertCount);
        var t = animate ? 0.24 : 0;
        Track(_router.Set(scene.RouterLive ? 1 : 0, wall, t), _router.Until);
        Track(_flowIn.Set(scene.FlowInActive ? 1 : 0, wall, t), _flowIn.Until);
        var router = scene.RouterToken >= 0 && scene.RouterToken < plan.Routes.Length ? plan.Routes[scene.RouterToken] : null;
        for (var i = 0; i < n; i++)
        {
            var p = router?.Scores[i] ?? 0;
            _percents[i] = MixtureOfExpertsPlanner.Percent(p);
            Track(_fills[i].Set(p, wall, t), _fills[i].Until);
            Track(_rows[i].Set(scene.RowLooks[i], wall, t), _rows[i].Until);
            Track(_gates[i].Set(scene.Gates[i], wall, t), _gates[i].Until);
            Track(_curves[i].Set(scene.Curves[i], wall, t), _curves[i].Until);
        }
        // The input chip: the old one fades out in 100 ms while the new one slides in from 14 px left over 250 ms.
        if (scene.InputToken != _input)
        {
            _inputLeaving = _input;
            _inputOut.Jump(1);
            Track(_inputOut.Set(0, wall, animate ? 0.1 : 0), _inputOut.Until);
            _input = scene.InputToken;
            _inputIn.Jump(0);
            Track(_inputIn.Set(1, wall, animate ? 0.25 : 0, ArcEasings.Enter), _inputIn.Until);
            _inputLook.Jump(scene.InputFresh ? 0 : 1);
        }
        else
        {
            Track(_inputLook.Set(scene.InputFresh ? 0 : 1, wall, t), _inputLook.Until);
        }
        // The output chip: the old one slides 10 px right and fades out while the new one slides in from 10 px left.
        if (scene.OutputToken != _output)
        {
            _outputLeaving = _output;
            _outputOut.Jump(1);
            Track(_outputOut.Set(0, wall, animate ? 0.16 : 0), _outputOut.Until);
            _output = scene.OutputToken;
            _outputIn.Jump(0);
            Track(_outputIn.Set(1, wall, animate ? 0.23 : 0, ArcEasings.Enter), _outputIn.Until);
        }
        InvalidateVisual();
    }

    /// <summary>Invalidates the drawn layer while one of its fades runs.</summary>
    public void Tick(double wall)
    {
        if (_until > 0 && wall <= _until + 0.05)
        {
            InvalidateVisual();
        }
        foreach (var tile in _tiles)
        {
            tile.Tick(wall);
        }
    }

    private void Track(bool changed, double until)
    {
        if (changed)
        {
            _until = Math.Max(_until, until);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var experts = Math.Max(1, _tiles.Count);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 694;
        var wide = width >= MixtureOfExpertsLayout.WideMinWidth;
        foreach (var tile in _tiles)
        {
            tile.Measure(Size.Infinity);
        }
        _packets.Measure(Size.Infinity);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, MixtureOfExpertsLayout.HeightFor(wide, experts));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var experts = Math.Max(1, _tiles.Count);
        if (_layout is null || Math.Abs(_layout.Width - finalSize.Width) > 0.01 || _layout.Experts != experts)
        {
            _layout = MixtureOfExpertsLayout.Build(finalSize.Width, experts);
            _owner?.OnStageLayoutChanged();
        }
        for (var i = 0; i < _tiles.Count; i++)
        {
            var tile = _tiles[i];
            tile.SetCompact(!_layout.IsWide);
            tile.Radius = _layout.TileRadius;
            tile.Arrange(_layout.Tiles[i]);
        }
        _packets.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var owner = _owner;
        var layout = _layout;
        if (owner is null || layout is null || !owner.Palette.IsReady)
        {
            return;
        }
        var palette = owner.Palette;
        var wall = owner.Wall;
        DrawLabels(context, palette, layout, owner.Labels);
        // Lines first: the router box, tiles and chips sit on their ends.
        DrawLine(context, layout.FlowIn, palette.BorderStrong, palette.FlowActive, _flowIn, wall);
        for (var i = 0; i < _gates.Length && i < layout.Experts; i++)
        {
            DrawRouteLine(context, layout.Gates[i], _gates[i], wall, palette);
            DrawRouteLine(context, layout.Curves[i], _curves[i], wall, palette);
        }
        context.DrawGeometry(null, palette.Pen(palette.BorderStrong, 1), layout.FlowOut.Geometry);
        DrawRouter(context, palette, layout, wall);
        context.DrawEllipse(palette.Brush(palette.Surface), palette.Pen(palette.BorderStrong, 1), layout.Joint, 2.5, 2.5);
        DrawChips(context, palette, layout, wall);
    }

    private static void DrawLabels(DrawingContext context, MixtureOfExpertsPalette palette, MixtureOfExpertsLayout layout, MixtureOfExpertsLabels labels)
    {
        DrawLabel(context, palette, labels.Input, layout.InputLabel);
        DrawLabel(context, palette, labels.Output, layout.OutputLabel);
        if (layout.HasColumnLabels)
        {
            DrawLabel(context, palette, labels.Router, layout.RouterLabel);
            DrawLabel(context, palette, labels.Experts, layout.ExpertsLabel);
        }
    }

    private static void DrawLabel(DrawingContext context, MixtureOfExpertsPalette palette, string text, (Point At, int Align) anchor)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var formatted = palette.Text(text, palette.TextMuted, size: 12);
        var x = anchor.Align switch
        {
            1 => anchor.At.X - formatted.Width,
            0 => anchor.At.X - formatted.Width / 2,
            _ => anchor.At.X
        };
        context.DrawText(formatted, new Point(x, anchor.At.Y - formatted.Height / 2));
    }

    private void DrawLine(DrawingContext context, ExplainerPath path, Color rest, Color active, MixtureOfExpertsFade fade, double wall)
    {
        var mix = fade.Mix(wall);
        var from = fade.From == 1 ? active : rest;
        var to = fade.To == 1 ? active : rest;
        var (fa, ta) = MixtureOfExpertsPart.FadeOpacity(from, to, mix);
        StrokePath(context, path, from, 1, false, fa);
        StrokePath(context, path, to, 1, false, ta);
    }

    private void DrawRouteLine(DrawingContext context, ExplainerPath path, MixtureOfExpertsFade fade, double wall, MixtureOfExpertsPalette palette)
    {
        var mix = fade.Mix(wall);
        var (fromColor, fromWidth, fromDashed) = LineStyle(palette, fade.From);
        var (toColor, toWidth, toDashed) = LineStyle(palette, fade.To);
        if (fade.From == fade.To || mix >= 1)
        {
            StrokePath(context, path, toColor, toWidth, toDashed, 1);
            return;
        }
        StrokePath(context, path, fromColor, fromWidth, fromDashed, 1 - mix);
        StrokePath(context, path, toColor, toWidth, toDashed, mix);
    }

    private static (Color Color, double Width, bool Dashed) LineStyle(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.LineFirst => (p.RankLine[0], 1.5, false),
        MixtureOfExpertsLooks.LineSecond => (p.RankLine[1], 1.5, false),
        MixtureOfExpertsLooks.LineDropped => (p.WarningLine, 1.5, true),
        _ => (p.Border, 1, false)
    };

    private void StrokePath(DrawingContext context, ExplainerPath path, Color color, double width, bool dashed, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0 || path.Length <= 0 || _owner is not { } owner)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(null, owner.Palette.Pen(color, width, dashed), path.Geometry);
        }
    }

    private void DrawRouter(DrawingContext context, MixtureOfExpertsPalette palette, MixtureOfExpertsLayout layout, double wall)
    {
        var box = layout.Router;
        var radius = layout.RouterRadius;
        var outer = new RoundedRect(box, radius);
        context.DrawRectangle(palette.Brush(palette.SurfaceMuted), null, outer);
        var inner = new RoundedRect(box.Deflate(0.5), radius - 0.5);
        var mix = _router.Mix(wall);
        var from = _router.From == 1 ? palette.RouterLiveBorder : palette.Border;
        var to = _router.To == 1 ? palette.RouterLiveBorder : palette.Border;
        var (fa, ta) = MixtureOfExpertsPart.FadeOpacity(from, to, mix);
        if (fa > 0)
        {
            using (context.PushOpacity(fa))
            {
                context.DrawRectangle(null, palette.Pen(from, 1), inner);
            }
        }
        if (ta > 0)
        {
            using (context.PushOpacity(ta))
            {
                context.DrawRectangle(null, palette.Pen(to, 1), inner);
            }
        }
        var trackBrush = palette.Brush(palette.Track);
        for (var i = 0; i < _rows.Length && i < layout.Experts; i++)
        {
            var track = layout.Tracks[i];
            context.DrawRectangle(trackBrush, null, new RoundedRect(track, 2));
            var p = Math.Clamp(_fills[i].Value(wall), 0, 1);
            var rowMix = _rows[i].Mix(wall);
            var fromFill = FillColor(palette, _rows[i].From);
            var toFill = FillColor(palette, _rows[i].To);
            var bar = layout.IsWide
                ? new Rect(track.X, track.Y, track.Width * p, track.Height)
                : new Rect(track.X, track.Bottom - track.Height * p, track.Width, track.Height * p);
            if (bar.Width > 0.01 && bar.Height > 0.01)
            {
                var (oa, na) = MixtureOfExpertsPart.FadeOpacity(fromFill, toFill, rowMix);
                FillBar(context, palette, bar, fromFill, oa);
                FillBar(context, palette, bar, toFill, na);
            }
            // The percentage switches at once; its color fades with the row.
            var text = PercentText(_percents[i]);
            var (fromText, fromMedium) = TextStyle(palette, _rows[i].From);
            var (toText, toMedium) = TextStyle(palette, _rows[i].To);
            var (xa, xb) = (fromText == toText && fromMedium == toMedium) || rowMix >= 1 ? (0, 1) : (1 - rowMix, rowMix);
            DrawScore(context, palette, layout, i, text, fromText, fromMedium, xa);
            DrawScore(context, palette, layout, i, text, toText, toMedium, xb);
        }
    }

    private static void FillBar(DrawingContext context, MixtureOfExpertsPalette palette, Rect bar, Color color, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(palette.Brush(color), null, new RoundedRect(bar, 2));
        }
    }

    private static void DrawScore(DrawingContext context, MixtureOfExpertsPalette palette, MixtureOfExpertsLayout layout, int i,
        string text, Color color, bool medium, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        var formatted = palette.Text(text, color, medium, 12);
        var rect = layout.ScoreTexts[i];
        var x = layout.IsWide ? rect.Right - formatted.Width : rect.X + (rect.Width - formatted.Width) / 2;
        using (context.PushOpacity(opacity))
        {
            context.DrawText(formatted, new Point(x, rect.Y + (rect.Height - formatted.Height) / 2));
        }
    }

    private static readonly string[] s_percents = Enumerable.Range(0, 101).Select(i => i + "%").ToArray();

    private static string PercentText(int percent) => s_percents[Math.Clamp(percent, 0, 100)];

    private static Color FillColor(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.RowLive => p.FillLive,
        MixtureOfExpertsLooks.RowFirst => p.Rank[0],
        MixtureOfExpertsLooks.RowSecond => p.Rank[1],
        _ => p.FillIdle
    };

    private static (Color Color, bool Medium) TextStyle(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.RowLive => (p.TextMuted, false),
        MixtureOfExpertsLooks.RowFirst or MixtureOfExpertsLooks.RowSecond => (p.Foreground, true),
        _ => (p.TextIdle, false)
    };

    private void DrawChips(DrawingContext context, MixtureOfExpertsPalette palette, MixtureOfExpertsLayout layout, double wall)
    {
        var plan = _plan;
        if (plan is null)
        {
            return;
        }
        // Input: right-aligned in its slot (wide) or centered (narrow).
        var outFade = _inputOut.Value(wall);
        if (_inputLeaving >= 0 && _inputLeaving < plan.Routes.Length && outFade > 0.001)
        {
            DrawChip(context, palette, layout.InputSlot, layout.IsWide ? 1 : 0, plan.Routes[_inputLeaving], false, false, 1, 0, outFade);
        }
        if (_input >= 0 && _input < plan.Routes.Length)
        {
            var appear = Math.Clamp(_inputIn.Value(wall), 0, 1);
            var lookMix = _inputLook.Mix(wall);
            var used = _inputLook.To == 1 ? lookMix : 1 - lookMix;
            DrawChip(context, palette, layout.InputSlot, layout.IsWide ? 1 : 0, plan.Routes[_input], false, false, used, -14 * (1 - appear), appear);
        }
        // Output: left-aligned (wide) or centered (narrow), with a dot per expert that processed the token.
        var leaving = _outputOut.Value(wall);
        if (_outputLeaving >= 0 && _outputLeaving < plan.Routes.Length && leaving > 0.001)
        {
            DrawChip(context, palette, layout.OutputSlot, layout.IsWide ? -1 : 0, plan.Routes[_outputLeaving], true, plan.Routes[_outputLeaving].IsPassThrough, 0, 10 * (1 - leaving), leaving);
        }
        if (_output >= 0 && _output < plan.Routes.Length)
        {
            var appear = Math.Clamp(_outputIn.Value(wall), 0, 1);
            DrawChip(context, palette, layout.OutputSlot, layout.IsWide ? -1 : 0, plan.Routes[_output], true, plan.Routes[_output].IsPassThrough, 0, -10 * (1 - appear), appear);
        }
    }

    /// <summary>
    /// A 28 px chip: 9 px radius, 10 px padding, 14 px medium text with its spaces kept; output chips start with a 6 px
    /// dot per expert that processed the token (2 px apart, first choice first) and are dashed when the token passed
    /// through. <paramref name="used"/> fades the input chip to its sent look.
    /// </summary>
    private static void DrawChip(DrawingContext context, MixtureOfExpertsPalette palette, Rect slot, int align, MixtureOfExpertsRoute route,
        bool output, bool dashed, double used, double offset, double opacity)
    {
        if (opacity <= 0.001)
        {
            return;
        }
        var dots = 0;
        if (output && !dashed)
        {
            foreach (var e in route.Experts)
            {
                if (!route.IsDropped(e))
                {
                    dots++;
                }
            }
        }
        var dotsWidth = dots > 0 ? dots * 6 + (dots - 1) * 2 + 6 : 0;
        var maxText = Math.Max(8, slot.Width - 20 - dotsWidth);
        var textColor = used > 0.5 ? palette.TextSecondary : palette.Foreground;
        var text = palette.Texts.GetTrimmed(route.Token, palette.Medium, 14, textColor, maxText);
        var width = Math.Ceiling(20 + dotsWidth + Math.Min(maxText, text.WidthIncludingTrailingWhitespace));
        var x = align switch
        {
            1 => slot.Right - width,
            -1 => slot.X,
            _ => slot.X + (slot.Width - width) / 2
        } + offset;
        var rect = new Rect(x, slot.Y, width, slot.Height);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(palette.Brush(palette.Surface), null, new RoundedRect(rect, 9));
            var border = used > 0.5 ? palette.Border : palette.BorderStrong;
            context.DrawRectangle(null, palette.Pen(border, 1, dashed), new RoundedRect(rect.Deflate(0.5), 8.5));
            var cx = rect.X + 10;
            if (dots > 0)
            {
                for (var r = 0; r < route.Experts.Count; r++)
                {
                    var e = route.Experts[r];
                    if (route.IsDropped(e))
                    {
                        continue;
                    }
                    context.DrawEllipse(palette.Brush(palette.Rank[Math.Min(r, 1)]), null, new Point(cx + 3, rect.Center.Y), 3, 3);
                    cx += 8;
                }
                cx += 4;
            }
            context.DrawText(text, new Point(cx, rect.Y + (rect.Height - text.Height) / 2));
        }
    }
}

/// <summary>
/// The packets: a dot (3.5 px radius, a 1.5 px surface halo) with a soft two-part trail (the last min(34, 70% of the
/// path) px at 16% and its last 45% at 38%) moving along a path by arc length with a cubic ease in and out. Neutral
/// packets carry the token to the router and the merged output to the output chip; packets in the choices' colors go to
/// the experts (stopping 5 px short of a full one) and along the merge curves. Redrawn every frame while one travels.
/// </summary>
internal sealed class MixtureOfExpertsPackets : Control
{
    private readonly MixtureOfExpertsStage _stage;
    private readonly Pen[] _long = new Pen[3];
    private readonly Pen[] _short = new Pen[3];
    private int _penVersion = -1;
    private MixtureOfExpertsTween _opacity = new(1);
    private double _until;

    public MixtureOfExpertsPackets(MixtureOfExpertsStage stage)
    {
        _stage = stage;
        IsHitTestVisible = false;
    }

    /// <summary>The token travelling (−1: none) and the time since its start.</summary>
    public int Token { get; private set; } = -1;

    public double Local { get; private set; }

    public bool IsShown => _opacity.Target > 0.5;

    public void SetShown(bool shown, double wall, bool animate)
    {
        if (_opacity.Set(shown ? 1 : 0, wall, animate ? 0.16 : 0))
        {
            _until = Math.Max(_until, _opacity.Until);
            InvalidateVisual();
        }
    }

    /// <summary>Moves the packets to <paramref name="local"/> seconds into <paramref name="token"/>; redraws only while one travels.</summary>
    public void SetTime(int token, double local, double wall)
    {
        var moving = IsTravelling(token, local) || IsTravelling(Token, Local);
        Token = token;
        Local = local;
        if (moving || (_until > 0 && wall <= _until + 0.05))
        {
            InvalidateVisual();
        }
    }

    /// <summary>True when a packet is on its way at <paramref name="local"/>.</summary>
    public static bool IsTravelling(int token, double local)
        => token >= 0 && ((local >= MixtureOfExpertsTimeline.RouterStart && local < MixtureOfExpertsTimeline.RouterEnd)
            || (local >= MixtureOfExpertsTimeline.GateStart && local < MixtureOfExpertsTimeline.GateEnd)
            || (local >= MixtureOfExpertsTimeline.MergeStart && local < MixtureOfExpertsTimeline.OutputEnd));

    public override void Render(DrawingContext context)
    {
        var owner = _stage.Owner;
        var layout = _stage.Layout;
        var plan = _stage.Plan;
        if (owner is null || layout is null || plan is null || !owner.Palette.IsReady || Token < 0 || Token >= plan.Routes.Length)
        {
            return;
        }
        var opacity = Math.Clamp(_opacity.Value(owner.Wall), 0, 1);
        if (opacity <= 0.001 || !owner.IsTravelShown)
        {
            return;
        }
        var palette = owner.Palette;
        EnsurePens(palette);
        var route = plan.Routes[Token];
        var local = Local;
        using (context.PushOpacity(opacity))
        {
            var u = MixtureOfExpertsTimeline.Leg(local, MixtureOfExpertsTimeline.RouterStart, MixtureOfExpertsTimeline.RouterEnd);
            if (u >= 0)
            {
                DrawPacket(context, palette, layout.FlowIn, u * layout.FlowIn.Length, 2, palette.Foreground);
            }
            u = MixtureOfExpertsTimeline.Leg(local, MixtureOfExpertsTimeline.GateStart, MixtureOfExpertsTimeline.GateEnd);
            if (u >= 0)
            {
                for (var r = 0; r < route.Experts.Count; r++)
                {
                    var e = route.Experts[r];
                    if (e >= layout.Experts)
                    {
                        continue;
                    }
                    var path = layout.Gates[e];
                    var end = route.IsDropped(e) ? Math.Max(0, path.Length - MixtureOfExpertsTimeline.DropGap) : path.Length;
                    DrawPacket(context, palette, path, u * end, Math.Min(r, 1), palette.Rank[Math.Min(r, 1)]);
                }
            }
            u = MixtureOfExpertsTimeline.Leg(local, MixtureOfExpertsTimeline.MergeStart, MixtureOfExpertsTimeline.MergeEnd);
            if (u >= 0)
            {
                for (var r = 0; r < route.Experts.Count; r++)
                {
                    var e = route.Experts[r];
                    if (e >= layout.Experts || route.IsDropped(e))
                    {
                        continue;
                    }
                    var path = layout.Curves[e];
                    DrawPacket(context, palette, path, u * path.Length, Math.Min(r, 1), palette.Rank[Math.Min(r, 1)]);
                }
            }
            u = MixtureOfExpertsTimeline.Leg(local, MixtureOfExpertsTimeline.OutputStart, MixtureOfExpertsTimeline.OutputEnd);
            if (u >= 0 && !route.IsPassThrough)
            {
                DrawPacket(context, palette, layout.FlowOut, u * layout.FlowOut.Length, 2, palette.Foreground);
            }
        }
    }

    private void DrawPacket(DrawingContext context, MixtureOfExpertsPalette palette, ExplainerPath path, double s, int slot, Color color)
    {
        var length = path.Length;
        if (length <= 0)
        {
            return;
        }
        var trail = Math.Min(34, 0.7 * length);
        if (path.ShowPart(_long[slot], s - trail, s))
        {
            context.DrawGeometry(null, _long[slot], path.Geometry);
        }
        if (path.ShowPart(_short[slot], s - 0.45 * trail, s))
        {
            context.DrawGeometry(null, _short[slot], path.Geometry);
        }
        context.DrawEllipse(palette.Brush(color), palette.Pen(palette.Surface, 1.5), path.PointAt(s), 3.5, 3.5);
    }

    private void EnsurePens(MixtureOfExpertsPalette palette)
    {
        if (_penVersion == palette.Version)
        {
            return;
        }
        _penVersion = palette.Version;
        Color[] colors = [palette.Rank[0], palette.Rank[1], palette.Foreground];
        for (var i = 0; i < 3; i++)
        {
            _long[i] = MixtureOfExpertsPens.Trail(Oklch.WithAlpha(colors[i], colors[i].A / 255.0 * 0.16));
            _short[i] = MixtureOfExpertsPens.Trail(Oklch.WithAlpha(colors[i], colors[i].A / 255.0 * 0.38));
        }
    }
}
