using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One expert of a <see cref="MixtureOfExperts"/>: a rounded tile with its icon, name (wide layout only) and a rolling
/// counter of the tokens it took, and a 2 px capacity bar along its bottom. It lights in its choice's color (9% fill, 55%
/// border, colored icon), turns dashed with a warning icon when it is full and skips the token, and shows a strong border
/// while inspected. Fades run 160 ms into a state and 480 ms back to rest; the capacity bar slides over 480 ms.
/// </summary>
internal sealed class MixtureOfExpertsTile : MixtureOfExpertsPart
{
    private readonly RollingDigits _count;
    private MixtureOfExpertsFade _look = new(MixtureOfExpertsLooks.TileNormal);
    private MixtureOfExpertsFade _barLook = new(0);
    private MixtureOfExpertsTween _bar = new(0);
    private string _name = string.Empty;
    private Geometry? _icon;
    private string _letter = string.Empty;

    public MixtureOfExpertsTile(MixtureOfExperts owner, int index) : base(owner, index)
    {
        _count = new RollingDigits
        {
            FontSize = 12,
            UseGrouping = false,
            Falloff = RollingDigitsFalloff.None,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        VisualChildren.Add(_count);
        LogicalChildren.Add(_count);
    }

    public override bool IsExpert => true;

    /// <summary>True in the narrow layout: icon over the counter, no name.</summary>
    public bool IsCompact { get; private set; }

    public double Radius { get; set; } = 10;

    public int Look => _look.To;

    public double Count => _count.Value;

    public void SetExpert(string name, Geometry? icon)
    {
        _name = name;
        _icon = icon;
        _letter = name.Length > 0 ? char.ToUpperInvariant(name.TrimStart().FirstOrDefault()).ToString() : "?";
        InvalidateVisual();
    }

    public void SetCompact(bool compact)
    {
        if (IsCompact != compact)
        {
            IsCompact = compact;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Moves the tile to its new state: look (fades), load (counter rolls), capacity fill and bar color.</summary>
    public void Apply(int look, int load, double fill, bool full, double wall, bool animate)
    {
        var palette = Owner.Palette;
        var lookTime = !animate ? 0 : look == MixtureOfExpertsLooks.TileNormal ? 0.48 : 0.16;
        if (_look.Set(look, wall, lookTime))
        {
            AnimatingUntil = Math.Max(AnimatingUntil, _look.Until);
        }
        var barLook = look is MixtureOfExpertsLooks.TileFirst or MixtureOfExpertsLooks.TileSecond ? look + 1 : full ? 1 : 0;
        if (_barLook.Set(barLook, wall, animate ? 0.24 : 0))
        {
            AnimatingUntil = Math.Max(AnimatingUntil, _barLook.Until);
        }
        if (_bar.Set(fill, wall, animate ? 0.48 : 0))
        {
            AnimatingUntil = Math.Max(AnimatingUntil, _bar.Until);
        }
        if (Math.Abs(_count.Value - load) > 0.001)
        {
            _count.Value = load;
        }
        var lit = look is MixtureOfExpertsLooks.TileFirst or MixtureOfExpertsLooks.TileSecond;
        var countColor = lit ? palette.Foreground : palette.TextMuted;
        if (_count.Foreground is not ISolidColorBrush solid || solid.Color != countColor)
        {
            _count.Foreground = palette.Brush(countColor);
        }
        InvalidateVisual();
    }

    /// <summary>Repaints after a palette change.</summary>
    public void Repaint()
    {
        var lit = _look.To is MixtureOfExpertsLooks.TileFirst or MixtureOfExpertsLooks.TileSecond;
        _count.Foreground = Owner.Palette.Brush(lit ? Owner.Palette.Foreground : Owner.Palette.TextMuted);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _count.Measure(Size.Infinity);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = _count.DesiredSize;
        if (IsCompact)
        {
            // Icon 14, gap 6, counter 16.8 tall, centered as a column.
            var top = (finalSize.Height - 36.8) / 2 + 20;
            _count.Arrange(new Rect((finalSize.Width - size.Width) / 2, top + (16.8 - size.Height) / 2, size.Width, size.Height));
        }
        else
        {
            _count.Arrange(new Rect(finalSize.Width - 10 - size.Width, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var palette = Owner.Palette;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (!palette.IsReady || w < 2 || h < 2)
        {
            return;
        }
        var wall = Owner.Wall;
        var mix = _look.Mix(wall);
        var outer = new RoundedRect(new Rect(0, 0, w, h), Radius);
        // Fill.
        var (fromFill, fromBorder, fromDashed, fromIcon) = Style(palette, _look.From);
        var (toFill, toBorder, toDashed, toIcon) = Style(palette, _look.To);
        var (fa, ta) = FadeOpacity(fromFill, toFill, mix);
        DrawFill(context, outer, fromFill, fa);
        DrawFill(context, outer, toFill, ta);
        // Capacity bar along the bottom, inside the rounded shape.
        var fill = Math.Clamp(_bar.Value(wall), 0, 1);
        if (fill > 0.001)
        {
            using (context.PushClip(outer))
            {
                var bar = new Rect(0, h - 2, w * fill, 2);
                var barMix = _barLook.Mix(wall);
                var from = BarColor(palette, _barLook.From);
                var to = BarColor(palette, _barLook.To);
                var (ba, bb) = FadeOpacity(from, to, barMix);
                DrawFill(context, bar, from, ba);
                DrawFill(context, bar, to, bb);
            }
        }
        // Border: a dashed border fades against a solid one instead of over it.
        var inner = new RoundedRect(new Rect(0.5, 0.5, w - 1, h - 1), Math.Max(0, Radius - 0.5));
        if (fromDashed != toDashed && mix < 1)
        {
            DrawBorder(context, inner, fromBorder, fromDashed, 1 - mix);
            DrawBorder(context, inner, toBorder, toDashed, mix);
        }
        else
        {
            var (oa, na) = FadeOpacity(fromBorder, toBorder, mix);
            DrawBorder(context, inner, fromBorder, fromDashed, oa);
            DrawBorder(context, inner, toBorder, toDashed, na);
        }
        // Icon (or the warning sign while skipping), then the name.
        var iconBox = IsCompact
            ? new Rect((w - 14) / 2, (h - 36.8) / 2, 14, 14)
            : new Rect(10, (h - 14) / 2, 14, 14);
        var fromGlyph = _look.From == MixtureOfExpertsLooks.TileDropped;
        var toGlyph = _look.To == MixtureOfExpertsLooks.TileDropped;
        if (fromGlyph != toGlyph && mix < 1)
        {
            DrawIcon(context, palette, iconBox, fromGlyph, fromIcon, 1 - mix);
            DrawIcon(context, palette, iconBox, toGlyph, toIcon, mix);
        }
        else
        {
            var (ia, ib) = FadeOpacity(fromIcon, toIcon, mix);
            DrawIcon(context, palette, iconBox, toGlyph, fromIcon, ia);
            DrawIcon(context, palette, iconBox, toGlyph, toIcon, ib);
        }
        if (!IsCompact)
        {
            var countWidth = _count.Bounds.Width;
            var nameWidth = Math.Max(0, w - 10 - 14 - 8 - 8 - countWidth - 10);
            var name = palette.Texts.GetTrimmed(_name, palette.Regular, 14, palette.Foreground, nameWidth);
            context.DrawText(name, new Point(32, (h - name.Height) / 2));
        }
        DrawFocusRing(context, new Rect(0, 0, w, h), Radius);
    }

    private static (Color Fill, Color Border, bool Dashed, Color Icon) Style(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.TileFirst => (p.TileFill[0], p.TileBorder[0], false, p.Rank[0]),
        MixtureOfExpertsLooks.TileSecond => (p.TileFill[1], p.TileBorder[1], false, p.Rank[1]),
        MixtureOfExpertsLooks.TileDropped => (p.Surface, p.WarningBorder, true, p.Warning),
        MixtureOfExpertsLooks.TileInspected => (p.SurfaceMuted, p.BorderStrong, false, p.TextMuted),
        _ => (p.Surface, p.Border, false, p.TextMuted)
    };

    private static Color BarColor(MixtureOfExpertsPalette p, int look) => look switch
    {
        1 => p.CapacityFull,
        2 => p.RankLine[0],
        3 => p.RankLine[1],
        _ => p.CapacityIdle
    };

    private void DrawFill(DrawingContext context, RoundedRect rect, Color color, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(Brush(color), null, rect);
        }
    }

    private void DrawFill(DrawingContext context, Rect rect, Color color, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.FillRectangle(Brush(color), rect);
        }
    }

    private void DrawBorder(DrawingContext context, RoundedRect rect, Color color, bool dashed, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(null, Owner.Palette.Pen(color, 1, dashed), rect);
        }
    }

    private void DrawIcon(DrawingContext context, MixtureOfExpertsPalette palette, Rect box, bool warning, Color color, double opacity)
    {
        if (opacity <= 0.001 || color.A == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            var glyph = warning ? ArcIcons.TriangleAlert : _icon;
            if (glyph is null)
            {
                var letter = palette.Texts.Get(_letter, palette.Medium, 12, color);
                context.DrawText(letter, new Point(box.X + (box.Width - letter.Width) / 2, box.Y + (box.Height - letter.Height) / 2));
                return;
            }
            palette.DrawGlyph(context, glyph, box, color);
        }
    }

    private IBrush Brush(Color color) => Owner.Palette.Brush(color);
}
