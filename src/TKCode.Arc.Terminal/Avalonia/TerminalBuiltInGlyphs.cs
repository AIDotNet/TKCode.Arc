using Avalonia;
using Avalonia.Media;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// The glyphs <see cref="TerminalView"/> draws itself when <see cref="TerminalView.CustomGlyphs"/> is on (05-rendering.md §8.1–8.3):
/// box drawing (U+2500–257F), block elements (U+2580–259F), braille (U+2800–28FF) and Powerline separators (U+E0B0–E0BF,
/// E0D2, E0D4). Geometry is in whole device pixels (Ghostty's rules): a light line is <see cref="TerminalGlyphRequest.Thickness"/>
/// thick and a heavy one twice that, centered at <c>floor((size − thickness) / 2)</c> so neighbors meet exactly; fractional
/// blocks round their edges the same way on both sides of a split, so two halves never leave a seam.
/// Hosts can wrap it in their own <see cref="ITerminalGlyphProvider"/> to add or replace ranges.
/// </summary>
public sealed class TerminalBuiltInGlyphs : ITerminalGlyphProvider
{
    private const int None = 0;
    private const int Light = 1;
    private const int Heavy = 2;
    private const int Double = 3;

    // U+2500–257F as up, right, down, left weights (0 none, 1 light, 2 heavy, 3 double); "----" is drawn by its own code.
    private static readonly string[] s_lines =
    [
        "0101", "0202", "1010", "2020", "----", "----", "----", "----", "----", "----", "----", "----", "0110", "0210", "0120", "0220",
        "0011", "0012", "0021", "0022", "1100", "1200", "2100", "2200", "1001", "1002", "2001", "2002", "1110", "1210", "2110", "1120",
        "2120", "2210", "1220", "2220", "1011", "1012", "2011", "1021", "2021", "2012", "1022", "2022", "0111", "0112", "0211", "0212",
        "0121", "0122", "0221", "0222", "1101", "1102", "1201", "1202", "2101", "2102", "2201", "2202", "1111", "1112", "1211", "1212",
        "2111", "1121", "2121", "2112", "2211", "1122", "1221", "2212", "1222", "2122", "2221", "2222", "----", "----", "----", "----",
        "0303", "3030", "0310", "0130", "0330", "0013", "0031", "0033", "1300", "3100", "3300", "1003", "3001", "3003", "1310", "3130",
        "3330", "1013", "3031", "3033", "0313", "0131", "0333", "1303", "3101", "3303", "1313", "3131", "3333", "----", "----", "----",
        "----", "----", "----", "----", "0001", "1000", "0100", "0010", "0002", "2000", "0200", "0020", "0201", "1020", "0102", "2010",
    ];

    /// <summary>The shared instance.</summary>
    public static TerminalBuiltInGlyphs Instance { get; } = new();

    private TerminalBuiltInGlyphs()
    {
    }

    /// <inheritdoc />
    public bool Handles(int codepoint) =>
        codepoint is >= 0x2500 and <= 0x259F or >= 0x2800 and <= 0x28FF or >= 0xE0B0 and <= 0xE0BF or 0xE0D2 or 0xE0D4;

    /// <inheritdoc />
    public TerminalGlyph? Create(int codepoint, TerminalGlyphRequest request)
    {
        if (request.Width <= 0 || request.Height <= 0)
        {
            return null;
        }
        return codepoint switch
        {
            >= 0x2500 and <= 0x257F => Box(codepoint, request),
            >= 0x2580 and <= 0x259F => Block(codepoint, request),
            >= 0x2800 and <= 0x28FF => Braille(codepoint, request),
            _ => Powerline(codepoint, request),
        };
    }

    // ---- Box drawing ----

    private static TerminalGlyph? Box(int cp, TerminalGlyphRequest r)
    {
        var spec = s_lines[cp - 0x2500];
        if (spec[0] != '-')
        {
            return Lines(spec[0] - '0', spec[1] - '0', spec[2] - '0', spec[3] - '0', r);
        }
        return cp switch
        {
            0x2504 => Dashed(r, horizontal: true, Light, 3),
            0x2505 => Dashed(r, horizontal: true, Heavy, 3),
            0x2506 => Dashed(r, horizontal: false, Light, 3),
            0x2507 => Dashed(r, horizontal: false, Heavy, 3),
            0x2508 => Dashed(r, horizontal: true, Light, 4),
            0x2509 => Dashed(r, horizontal: true, Heavy, 4),
            0x250A => Dashed(r, horizontal: false, Light, 4),
            0x250B => Dashed(r, horizontal: false, Heavy, 4),
            0x254C => Dashed(r, horizontal: true, Light, 2),
            0x254D => Dashed(r, horizontal: true, Heavy, 2),
            0x254E => Dashed(r, horizontal: false, Light, 2),
            0x254F => Dashed(r, horizontal: false, Heavy, 2),
            >= 0x256D and <= 0x2570 => Arc(cp, r),
            _ => Diagonal(cp, r),
        };
    }

    /// <summary>Line positions for a cell: bands of light and heavy strokes and the two rails of a double line.</summary>
    private readonly struct Grid(TerminalGlyphRequest r)
    {
        public readonly int W = r.Width;
        public readonly int H = r.Height;
        public readonly int Light = Math.Max(1, r.Thickness);

        private int Thick(int weight) => weight == Heavy ? Math.Min(2 * Light, Math.Min(W, H)) : Light;

        public int HTop(int weight) => (H - Thick(weight)) / 2;
        public int HBottom(int weight) => HTop(weight) + Thick(weight);
        public int VLeft(int weight) => (W - Thick(weight)) / 2;
        public int VRight(int weight) => VLeft(weight) + Thick(weight);

        // Double rails: [TopRailTop, TopRailBottom), [BottomRailTop, BottomRailBottom), and the same for x.
        public int TopRailTop => Math.Max(0, HTop(TerminalBuiltInGlyphs.Light) - Light);
        public int TopRailBottom => HTop(TerminalBuiltInGlyphs.Light);
        public int BottomRailTop => HBottom(TerminalBuiltInGlyphs.Light);
        public int BottomRailBottom => Math.Min(H, HBottom(TerminalBuiltInGlyphs.Light) + Light);
        public int LeftRailLeft => Math.Max(0, VLeft(TerminalBuiltInGlyphs.Light) - Light);
        public int LeftRailRight => VLeft(TerminalBuiltInGlyphs.Light);
        public int RightRailLeft => VRight(TerminalBuiltInGlyphs.Light);
        public int RightRailRight => Math.Min(W, VRight(TerminalBuiltInGlyphs.Light) + Light);
    }

    private static bool IsSingle(int weight) => weight is Light or Heavy;

    /// <summary>
    /// Lines from the cell's edges to its center. Light and heavy arms run past the center by the widest perpendicular arm so
    /// corners close; double arms draw two rails, cut at an inner corner where a perpendicular arm is double too, carried on
    /// as an outer corner where the line turns, and straight through where it continues.
    /// </summary>
    private static TerminalGlyph Lines(int up, int right, int down, int left, TerminalGlyphRequest r)
    {
        var g = new Grid(r);
        var b = new ShapeBuilder();
        var hMax = Math.Max(IsSingle(left) ? left : 0, IsSingle(right) ? right : 0);
        var vMax = Math.Max(IsSingle(up) ? up : 0, IsSingle(down) ? down : 0);
        var hDouble = left == Double || right == Double;
        var vDouble = up == Double || down == Double;

        // Up
        if (IsSingle(up))
        {
            var end = hDouble ? (down != None ? g.BottomRailBottom : left == Double && right == Double ? g.TopRailBottom : g.BottomRailBottom)
                : hMax > 0 ? g.HBottom(hMax) : g.HBottom(up);
            b.Rect(g.VLeft(up), 0, g.VRight(up), end);
        }
        else if (up == Double)
        {
            b.Rect(g.LeftRailLeft, 0, g.LeftRailRight, EndDown(g, left, right, down));
            b.Rect(g.RightRailLeft, 0, g.RightRailRight, EndDown(g, right, left, down));
        }

        // Down
        if (IsSingle(down))
        {
            var start = hDouble ? (up != None ? g.TopRailTop : left == Double && right == Double ? g.BottomRailTop : g.TopRailTop)
                : hMax > 0 ? g.HTop(hMax) : g.HTop(down);
            b.Rect(g.VLeft(down), start, g.VRight(down), g.H);
        }
        else if (down == Double)
        {
            b.Rect(g.LeftRailLeft, StartUp(g, left, right, up), g.LeftRailRight, g.H);
            b.Rect(g.RightRailLeft, StartUp(g, right, left, up), g.RightRailRight, g.H);
        }

        // Left
        if (IsSingle(left))
        {
            var end = vDouble ? (right != None ? g.RightRailRight : up == Double && down == Double ? g.LeftRailRight : g.RightRailRight)
                : vMax > 0 ? g.VRight(vMax) : g.VRight(left);
            b.Rect(0, g.HTop(left), end, g.HBottom(left));
        }
        else if (left == Double)
        {
            b.Rect(0, g.TopRailTop, EndRight(g, up, down, right), g.TopRailBottom);
            b.Rect(0, g.BottomRailTop, EndRight(g, down, up, right), g.BottomRailBottom);
        }

        // Right
        if (IsSingle(right))
        {
            var start = vDouble ? (left != None ? g.LeftRailLeft : up == Double && down == Double ? g.RightRailLeft : g.LeftRailLeft)
                : vMax > 0 ? g.VLeft(vMax) : g.VLeft(right);
            b.Rect(start, g.HTop(right), g.W, g.HBottom(right));
        }
        else if (right == Double)
        {
            b.Rect(StartLeft(g, up, down, left), g.TopRailTop, g.W, g.TopRailBottom);
            b.Rect(StartLeft(g, down, up, left), g.BottomRailTop, g.W, g.BottomRailBottom);
        }
        return b.Fill();
    }

    // Where a rail of a double up arm ends. facing: the perpendicular arm on the rail's side; other: the one on the far side;
    // opposite: the down arm.
    private static int EndDown(Grid g, int facing, int other, int opposite) =>
        facing == Double ? g.TopRailBottom
        : opposite != None ? g.BottomRailBottom
        : IsSingle(facing) ? g.HBottom(facing)
        : other == Double ? g.BottomRailBottom
        : IsSingle(other) ? g.HBottom(other)
        : g.BottomRailBottom;

    private static int StartUp(Grid g, int facing, int other, int opposite) =>
        facing == Double ? g.BottomRailTop
        : opposite != None ? g.TopRailTop
        : IsSingle(facing) ? g.HTop(facing)
        : other == Double ? g.TopRailTop
        : IsSingle(other) ? g.HTop(other)
        : g.TopRailTop;

    private static int EndRight(Grid g, int facing, int other, int opposite) =>
        facing == Double ? g.LeftRailRight
        : opposite != None ? g.RightRailRight
        : IsSingle(facing) ? g.VRight(facing)
        : other == Double ? g.RightRailRight
        : IsSingle(other) ? g.VRight(other)
        : g.RightRailRight;

    private static int StartLeft(Grid g, int facing, int other, int opposite) =>
        facing == Double ? g.RightRailLeft
        : opposite != None ? g.LeftRailLeft
        : IsSingle(facing) ? g.VLeft(facing)
        : other == Double ? g.LeftRailLeft
        : IsSingle(other) ? g.VLeft(other)
        : g.LeftRailLeft;

    /// <summary>
    /// <paramref name="count"/> dashes along the cell with whole-pixel gaps; each cell starts with half a gap and ends with
    /// the other half, so the gaps stay even across cells.
    /// </summary>
    private static TerminalGlyph Dashed(TerminalGlyphRequest r, bool horizontal, int weight, int count)
    {
        var g = new Grid(r);
        var b = new ShapeBuilder();
        var length = horizontal ? g.W : g.H;
        var gap = Math.Max(1, length / (2 * count));
        var dashTotal = Math.Max(count, length - count * gap);
        var at = gap / 2;
        for (var i = 0; i < count; i++)
        {
            var dash = dashTotal / count + (i < dashTotal % count ? 1 : 0);
            if (horizontal)
            {
                b.Rect(at, g.HTop(weight), Math.Min(length, at + dash), g.HBottom(weight));
            }
            else
            {
                b.Rect(g.VLeft(weight), at, g.VRight(weight), Math.Min(length, at + dash));
            }
            at += dash + gap;
        }
        return b.Fill();
    }

    /// <summary>Rounded corners: a straight line from each edge into a quarter circle, stroked on the light line's center.</summary>
    private static TerminalGlyph Arc(int cp, TerminalGlyphRequest r)
    {
        var g = new Grid(r);
        var t = g.Light;
        var cx = g.VLeft(Light) + t / 2.0;
        var cy = g.HTop(Light) + t / 2.0;
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            switch (cp)
            {
                case 0x256D: // ╭ right and down
                {
                    var radius = Math.Max(0, Math.Min(g.W - cx, g.H - cy));
                    c.BeginFigure(new Point(g.W, cy), false);
                    c.LineTo(new Point(cx + radius, cy));
                    c.ArcTo(new Point(cx, cy + radius), new Size(radius, radius), 0, false, SweepDirection.CounterClockwise);
                    c.LineTo(new Point(cx, g.H));
                    break;
                }
                case 0x256E: // ╮ left and down
                {
                    var radius = Math.Max(0, Math.Min(cx, g.H - cy));
                    c.BeginFigure(new Point(0, cy), false);
                    c.LineTo(new Point(cx - radius, cy));
                    c.ArcTo(new Point(cx, cy + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                    c.LineTo(new Point(cx, g.H));
                    break;
                }
                case 0x256F: // ╯ left and up
                {
                    var radius = Math.Max(0, Math.Min(cx, cy));
                    c.BeginFigure(new Point(0, cy), false);
                    c.LineTo(new Point(cx - radius, cy));
                    c.ArcTo(new Point(cx, cy - radius), new Size(radius, radius), 0, false, SweepDirection.CounterClockwise);
                    c.LineTo(new Point(cx, 0));
                    break;
                }
                default: // ╰ right and up
                {
                    var radius = Math.Max(0, Math.Min(g.W - cx, cy));
                    c.BeginFigure(new Point(g.W, cy), false);
                    c.LineTo(new Point(cx + radius, cy));
                    c.ArcTo(new Point(cx, cy - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                    c.LineTo(new Point(cx, 0));
                    break;
                }
            }
            c.EndFigure(false);
        }
        return new TerminalGlyph(geometry, 1, t);
    }

    /// <summary>╱ ╲ ╳: corner to corner, a little past the corners so diagonals meet in the next cell (the view clips to the cell).</summary>
    private static TerminalGlyph Diagonal(int cp, TerminalGlyphRequest r)
    {
        double w = r.Width, h = r.Height;
        var overshoot = 0.5 * Math.Max(1, r.Thickness);
        var dx = overshoot * w / h;
        var dy = overshoot;
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            if (cp is 0x2571 or 0x2573)
            {
                c.BeginFigure(new Point(w + dx, -dy), false);
                c.LineTo(new Point(-dx, h + dy));
                c.EndFigure(false);
            }
            if (cp is 0x2572 or 0x2573)
            {
                c.BeginFigure(new Point(-dx, -dy), false);
                c.LineTo(new Point(w + dx, h + dy));
                c.EndFigure(false);
            }
        }
        return new TerminalGlyph(geometry, 1, Math.Max(1, r.Thickness));
    }

    // ---- Block elements ----

    private static TerminalGlyph? Block(int cp, TerminalGlyphRequest r)
    {
        int w = r.Width, h = r.Height;
        var b = new ShapeBuilder();
        // Fractions round the shared edge the same way from both sides: a lower f block starts where an upper (1 − f) block ends.
        int Upper(double f) => (int)Math.Round(f * h);
        int LowerTop(double f) => (int)Math.Round((1 - f) * h);
        int Left(double f) => (int)Math.Round(f * w);
        int RightLeft(double f) => (int)Math.Round((1 - f) * w);
        var mx = (int)Math.Round(w / 2.0);
        var my = (int)Math.Round(h / 2.0);

        switch (cp)
        {
            case 0x2580: b.Rect(0, 0, w, Upper(0.5)); break;
            case >= 0x2581 and <= 0x2588: b.Rect(0, LowerTop((cp - 0x2580) / 8.0), w, h); break;
            case >= 0x2589 and <= 0x258F: b.Rect(0, 0, Left((0x2590 - cp) / 8.0), h); break;
            case 0x2590: b.Rect(RightLeft(0.5), 0, w, h); break;
            case 0x2591: b.Rect(0, 0, w, h); return b.Fill(0.25);
            case 0x2592: b.Rect(0, 0, w, h); return b.Fill(0.5);
            case 0x2593: b.Rect(0, 0, w, h); return b.Fill(0.75);
            case 0x2594: b.Rect(0, 0, w, Upper(1 / 8.0)); break;
            case 0x2595: b.Rect(RightLeft(1 / 8.0), 0, w, h); break;
            default:
            {
                // Quadrants: upper left, upper right, lower left, lower right.
                var q = cp switch
                {
                    0x2596 => 0b0010,
                    0x2597 => 0b0001,
                    0x2598 => 0b1000,
                    0x2599 => 0b1011,
                    0x259A => 0b1001,
                    0x259B => 0b1110,
                    0x259C => 0b1101,
                    0x259D => 0b0100,
                    0x259E => 0b0110,
                    _ => 0b0111,
                };
                if ((q & 0b1000) != 0)
                {
                    b.Rect(0, 0, mx, my);
                }
                if ((q & 0b0100) != 0)
                {
                    b.Rect(mx, 0, w, my);
                }
                if ((q & 0b0010) != 0)
                {
                    b.Rect(0, my, mx, h);
                }
                if ((q & 0b0001) != 0)
                {
                    b.Rect(mx, my, w, h);
                }
                break;
            }
        }
        return b.Fill();
    }

    // ---- Braille ----

    /// <summary>Eight dots, two columns by four rows, of size <c>min(w / 4, h / 8)</c>, centered in their quarter-columns and eighth-rows.</summary>
    private static TerminalGlyph? Braille(int cp, TerminalGlyphRequest r)
    {
        var bits = cp - 0x2800;
        if (bits == 0)
        {
            return null;
        }
        int w = r.Width, h = r.Height;
        var dot = Math.Max(1, Math.Min(w / 4, h / 8));
        var b = new ShapeBuilder();
        // Dots 1–3 and 7 are the left column, 4–6 and 8 the right one (Unicode bit order).
        ReadOnlySpan<(int Bit, int Col, int Row)> dots = [(0, 0, 0), (1, 0, 1), (2, 0, 2), (3, 1, 0), (4, 1, 1), (5, 1, 2), (6, 0, 3), (7, 1, 3)];
        foreach (var (bit, col, row) in dots)
        {
            if ((bits & (1 << bit)) == 0)
            {
                continue;
            }
            var x = (int)Math.Round((2 * col + 1) * w / 4.0 - dot / 2.0);
            var y = (int)Math.Round((2 * row + 1) * h / 8.0 - dot / 2.0);
            b.Rect(x, y, x + dot, y + dot);
        }
        return b.Fill();
    }

    // ---- Powerline ----

    private static TerminalGlyph? Powerline(int cp, TerminalGlyphRequest r)
    {
        double w = r.Width, h = r.Height;
        var t = Math.Max(1, r.Thickness);
        var b = new ShapeBuilder();
        switch (cp)
        {
            case 0xE0B0: b.Polygon(new(0, 0), new(w, h / 2), new(0, h)); break;
            case 0xE0B2: b.Polygon(new(w, 0), new(0, h / 2), new(w, h)); break;
            case 0xE0B1: return Polyline(t, new(0, 0), new(w, h / 2), new(0, h));
            case 0xE0B3: return Polyline(t, new(w, 0), new(0, h / 2), new(w, h));
            case 0xE0B4: return HalfEllipse(rightward: true, filled: true, w, h, t);
            case 0xE0B5: return HalfEllipse(rightward: true, filled: false, w, h, t);
            case 0xE0B6: return HalfEllipse(rightward: false, filled: true, w, h, t);
            case 0xE0B7: return HalfEllipse(rightward: false, filled: false, w, h, t);
            case 0xE0B8: b.Polygon(new(0, 0), new(w, h), new(0, h)); break;
            case 0xE0BA: b.Polygon(new(w, 0), new(w, h), new(0, h)); break;
            case 0xE0BC: b.Polygon(new(0, 0), new(w, 0), new(0, h)); break;
            case 0xE0BE: b.Polygon(new(0, 0), new(w, 0), new(w, h)); break;
            case 0xE0B9 or 0xE0BF: return Polyline(t, new(0, 0), new(w, h));
            case 0xE0BB or 0xE0BD: return Polyline(t, new(w, 0), new(0, h));
            // The inverse hard dividers: the cell minus the triangle of E0B0 / E0B2.
            case 0xE0D2:
                b.Polygon(new(0, 0), new(w, 0), new(w, h), new(0, h), new(w, h / 2));
                break;
            default: // 0xE0D4
                b.Polygon(new(0, 0), new(w, 0), new(0, h / 2), new(w, h), new(0, h));
                break;
        }
        return b.Fill();
    }

    private static TerminalGlyph Polyline(double thickness, params ReadOnlySpan<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(points[0], false);
            for (var i = 1; i < points.Length; i++)
            {
                c.LineTo(points[i]);
            }
            c.EndFigure(false);
        }
        return new TerminalGlyph(geometry, 1, thickness);
    }

    /// <summary>Half an ellipse spanning the cell's height, bulging right (E0B4) or left (E0B6), sampled as a polyline.</summary>
    private static TerminalGlyph HalfEllipse(bool rightward, bool filled, double w, double h, double thickness)
    {
        const int Segments = 32;
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            var x = rightward ? 0 : w;
            var direction = rightward ? 1 : -1;
            c.BeginFigure(new Point(x, 0), filled);
            for (var i = 1; i <= Segments; i++)
            {
                var angle = Math.PI * i / Segments;
                c.LineTo(new Point(x + direction * w * Math.Sin(angle), h / 2 * (1 - Math.Cos(angle))));
            }
            c.EndFigure(filled);
        }
        return filled ? new TerminalGlyph(geometry) : new TerminalGlyph(geometry, 1, thickness);
    }

    /// <summary>Collects filled rectangles and polygons into one geometry; overlapping parts add up (non-zero fill).</summary>
    private sealed class ShapeBuilder
    {
        private readonly StreamGeometry _geometry = new();
        private readonly StreamGeometryContext _context;

        public ShapeBuilder()
        {
            _context = _geometry.Open();
            _context.SetFillRule(FillRule.NonZero);
        }

        public void Rect(double x0, double y0, double x1, double y1)
        {
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }
            _context.BeginFigure(new Point(x0, y0), true);
            _context.LineTo(new Point(x1, y0));
            _context.LineTo(new Point(x1, y1));
            _context.LineTo(new Point(x0, y1));
            _context.EndFigure(true);
        }

        public void Polygon(params ReadOnlySpan<Point> points)
        {
            _context.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
            {
                _context.LineTo(points[i]);
            }
            _context.EndFigure(true);
        }

        public TerminalGlyph Fill(double opacity = 1)
        {
            _context.Dispose();
            return new TerminalGlyph(_geometry, opacity);
        }
    }
}
