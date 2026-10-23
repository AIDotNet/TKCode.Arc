using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The stage geometry of a <see cref="MixtureOfExperts"/> for a width and an expert count, rebuilt only when either
/// changes. Wide (at least <see cref="WideMinWidth"/>): input, router, a column of expert tiles and output from left to
/// right; the input and output slots take 12% of the width, the router max(124, 22%), the tiles max(148, 26%), and the
/// rest goes 27 : 33 : 24.5 : 15.5 to the input line, the gate lines, the merge curves and the output line. Rows are 32
/// apart (28-high tiles), the router reaches 10 above and below the tiles, and the stage is 22 + 32E + 16 high. Each
/// merge curve is a cubic with its control points at 0.6 and ⅔ of the horizontal distance. Narrow: top to bottom, the
/// router a band of vertical score bars over a row of icon tiles, 286 high.
/// </summary>
internal sealed class MixtureOfExpertsLayout
{
    /// <summary>The narrowest stage that uses the wide layout (a 568 card: 24 padding and a 1 px border each side).</summary>
    public const double WideMinWidth = 518;

    public const double RowPitch = 32;
    public const double WideTileHeight = 28;
    public const double ChipHeight = 28;
    public const double NarrowHeight = 286;
    public const double NarrowTileTop = 164;
    public const double NarrowTileHeight = 52;
    public const double NarrowBandTop = 50;
    public const double NarrowBandHeight = 92;

    private MixtureOfExpertsLayout(bool wide, double width, int experts)
    {
        IsWide = wide;
        Width = width;
        Experts = experts;
        Tiles = new Rect[experts];
        Tracks = new Rect[experts];
        ScoreTexts = new Rect[experts];
        Gates = new ExplainerPath[experts];
        Curves = new ExplainerPath[experts];
        for (var i = 0; i < experts; i++)
        {
            Gates[i] = new ExplainerPath();
            Curves[i] = new ExplainerPath();
        }
    }

    public bool IsWide { get; }

    public double Width { get; }

    public double Height { get; private set; }

    public int Experts { get; }

    /// <summary>The input chip's slot (the chip sits at its right edge when wide, centered when narrow).</summary>
    public Rect InputSlot { get; private set; }

    /// <summary>The output chip's slot (the chip sits at its left edge when wide, centered when narrow).</summary>
    public Rect OutputSlot { get; private set; }

    /// <summary>The router box (wide) or band (narrow).</summary>
    public Rect Router { get; private set; }

    /// <summary>The router's corner radius.</summary>
    public double RouterRadius => 14;

    /// <summary>The expert tiles.</summary>
    public Rect[] Tiles { get; }

    /// <summary>The tiles' corner radius.</summary>
    public double TileRadius => IsWide ? 10 : 9;

    /// <summary>Each expert's score track: a 4-high horizontal bar (wide) or a 4-wide vertical bar (narrow).</summary>
    public Rect[] Tracks { get; }

    /// <summary>Where each expert's percentage goes: right-aligned (wide) or centered above its bar (narrow).</summary>
    public Rect[] ScoreTexts { get; }

    /// <summary>The merge point.</summary>
    public Point Joint { get; private set; }

    public ExplainerPath FlowIn { get; } = new();

    public ExplainerPath FlowOut { get; } = new();

    /// <summary>The router-to-expert lines.</summary>
    public ExplainerPath[] Gates { get; }

    /// <summary>The expert-to-merge curves.</summary>
    public ExplainerPath[] Curves { get; }

    /// <summary>Stage labels: anchor point (vertical center) and alignment (−1 left, 0 center, 1 right).</summary>
    public (Point At, int Align) InputLabel { get; private set; }

    public (Point At, int Align) RouterLabel { get; private set; }

    public (Point At, int Align) ExpertsLabel { get; private set; }

    public (Point At, int Align) OutputLabel { get; private set; }

    /// <summary>The router and experts labels show only in the wide layout.</summary>
    public bool HasColumnLabels => IsWide;

    /// <summary>The stage height for <paramref name="experts"/> in the given layout.</summary>
    public static double HeightFor(bool wide, int experts) => wide ? 22 + RowPitch * experts + 16 : NarrowHeight;

    public static MixtureOfExpertsLayout Build(double width, int experts)
    {
        width = Math.Max(0, double.IsFinite(width) ? width : 0);
        experts = Math.Max(1, experts);
        var layout = new MixtureOfExpertsLayout(width >= WideMinWidth, width, experts);
        if (layout.IsWide)
        {
            layout.BuildWide();
        }
        else
        {
            layout.BuildNarrow();
        }
        return layout;
    }

    private void BuildWide()
    {
        var w = Width;
        var e = Experts;
        var slot = 0.12 * w;
        var routerWidth = Math.Max(124, 0.22 * w);
        var tileWidth = Math.Max(148, 0.26 * w);
        var rest = Math.Max(0, w - 2 * slot - routerWidth - tileWidth);
        var flowIn = 0.27 * rest;
        var gate = 0.33 * rest;
        var curve = 0.245 * rest;
        var routerX = slot + flowIn;
        var routerRight = routerX + routerWidth;
        var tileX = routerRight + gate;
        var tileRight = tileX + tileWidth;
        var jointX = tileRight + curve;
        var outX = w - slot;
        const double top = 22;
        var tilesTop = top + 10;
        var mid = tilesTop + (RowPitch * e - 4) / 2;
        Height = HeightFor(true, e);
        Router = new Rect(routerX, top, routerWidth, RowPitch * e + 16);
        InputSlot = new Rect(0, mid - ChipHeight / 2, slot, ChipHeight);
        OutputSlot = new Rect(outX, mid - ChipHeight / 2, Math.Max(0, w - outX), ChipHeight);
        Joint = new Point(jointX, mid);
        FlowIn.Clear().MoveTo(new Point(slot, mid)).LineTo(new Point(routerX, mid));
        FlowOut.Clear().MoveTo(Joint).LineTo(new Point(outX, mid));
        var trackWidth = Math.Max(8, routerWidth - 60);
        for (var i = 0; i < e; i++)
        {
            var cy = tilesTop + RowPitch * i + WideTileHeight / 2;
            Tiles[i] = new Rect(tileX, cy - WideTileHeight / 2, tileWidth, WideTileHeight);
            Tracks[i] = new Rect(routerX + 12, cy - 2, trackWidth, 4);
            ScoreTexts[i] = new Rect(routerRight - 42, cy - 8, 30, 16);
            Gates[i].Clear().MoveTo(new Point(routerRight, cy)).LineTo(new Point(tileX, cy));
            var dx = jointX - tileRight;
            Curves[i].Clear().MoveTo(new Point(tileRight, cy))
                .CubicTo(new Point(tileRight + 0.6 * dx, cy), new Point(tileRight + 2.0 / 3 * dx, mid), Joint, 40);
        }
        InputLabel = (new Point(slot, 11), 1);
        RouterLabel = (new Point(routerX + routerWidth / 2, 11), 0);
        ExpertsLabel = (new Point(tileX + tileWidth / 2, 11), 0);
        OutputLabel = (new Point(outX, 11), -1);
    }

    private void BuildNarrow()
    {
        var w = Width;
        var e = Experts;
        var cx = w / 2;
        var slotWidth = Math.Min(180, w / 2);
        Height = NarrowHeight;
        InputSlot = new Rect(cx - slotWidth / 2, 0, slotWidth, ChipHeight);
        OutputSlot = new Rect(cx - slotWidth / 2, 258, slotWidth, ChipHeight);
        Router = new Rect(0, NarrowBandTop, w, NarrowBandHeight);
        Joint = new Point(cx, 242);
        FlowIn.Clear().MoveTo(new Point(cx, ChipHeight)).LineTo(new Point(cx, NarrowBandTop));
        FlowOut.Clear().MoveTo(Joint).LineTo(new Point(cx, 258));
        var column = Math.Max(1, (w - 4 * (e - 1)) / e);
        var bandBottom = NarrowBandTop + NarrowBandHeight;
        var tileBottom = NarrowTileTop + NarrowTileHeight;
        const double drop = 26;
        for (var i = 0; i < e; i++)
        {
            var x = i * (column + 4);
            var c = x + column / 2;
            Tiles[i] = new Rect(x, NarrowTileTop, column, NarrowTileHeight);
            Tracks[i] = new Rect(c - 2, 80, 4, 50);
            ScoreTexts[i] = new Rect(x, 58, column, 16);
            Gates[i].Clear().MoveTo(new Point(c, bandBottom)).LineTo(new Point(c, NarrowTileTop));
            Curves[i].Clear().MoveTo(new Point(c, tileBottom))
                .CubicTo(new Point(c, tileBottom + 0.7 * drop), new Point(cx, tileBottom + 0.65 * drop), Joint, 40);
        }
        InputLabel = (new Point(0, 14), -1);
        OutputLabel = (new Point(0, 272), -1);
        RouterLabel = (default, 0);
        ExpertsLabel = (default, 0);
    }
}
