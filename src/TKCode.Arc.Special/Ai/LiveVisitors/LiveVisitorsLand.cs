using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The land of a <see cref="LiveVisitors"/> map: a coarse equirectangular dot grid, one dot per 4° cell from 80°N to 56°S
/// (90 columns by 34 rows), and the projection from latitude and longitude onto it.
/// <para>
/// Source: the same repository's Natural Earth 1:110m land mask (public domain), the one Blocks' RevenueGlobe ships as
/// <c>RevenueGlobe/Resources/land-mask.bin</c> (written by <c>tools/geo/generate_land_mask.py</c>). Each 4° cell is a dot
/// when at least 30% of its 0.5° mask cells are land. Special cannot reference Blocks, so the rows are copied here.
/// </para>
/// </summary>
internal static class LiveVisitorsLand
{
    /// <summary>Degrees per cell.</summary>
    public const double Step = 4;

    /// <summary>The latitude of the grid's top edge.</summary>
    public const double North = 80;

    /// <summary>The latitude of the grid's bottom edge.</summary>
    public const double South = -56;

    /// <summary>The dot of a land cell, as a share of the cell (0.28 of the pitch: round dots with clear gaps).</summary>
    public const double DotRadius = 0.28;

    /// <summary>Rows from north to south, columns from 180°W eastward; '#' is land.</summary>
    internal static readonly string[] Rows =
    [
        ".....................#####.#############........##...................##...................",
        "..............#####.#####......#########..................##.....##########.#...#.........",
        "....######.########.########...########..........#####....#.##############################",
        "##.######################.###...####...##.......##########################################",
        "....##################...###....###...........###########################################.",
        ".....##...############...#####.................##..##############################...##....",
        "............###################............##.###################################...#.....",
        ".............##################.............#####################################.........",
        "..............################..............####################################..........",
        "..............#############................###.#####.#.##.####################..#.........",
        "..............############.................##.....#.#######################.#..##.........",
        "...............###########.................#####......#####################..###..........",
        "................#########.................##################################..............",
        ".................####...#................##################################...............",
        "..................###....#...............###################..############................",
        "...................####..................##################....###..####..................",
        "......................##.................#################.....##....###...#..............",
        "........................#.####...........#################......#......#..................",
        ".........................#######..........###############.............#...#...............",
        ".........................########..............##########............##.###...............",
        ".........................##########............########...............#.####..##..........",
        ".........................###########............#######................#.......###........",
        ".........................###########............#######...................................",
        "..........................##########............#######..#..................###.#.........",
        "...........................########.............#######.##.................#######........",
        "...........................########.............######..#.................#########.......",
        "...........................######................#####...................##########.......",
        "...........................######................####.....................##########......",
        "...........................#####.................###......................##...####.......",
        "..........................#####.................................................##......#.",
        "..........................###....................................................#......#.",
        "..........................###..........................................................#..",
        "..........................##..............................................................",
        "...........................#.............................................................."
    ];

    private static StreamGeometry? s_geometry;
    private static int s_count = -1;

    /// <summary>Columns of the grid.</summary>
    public static int Columns => Rows[0].Length;

    /// <summary>Rows of the grid.</summary>
    public static int RowCount => Rows.Length;

    /// <summary>The grid's height over its width.</summary>
    public static double Aspect => (double)RowCount / Columns;

    /// <summary>How many land dots there are.</summary>
    public static int DotCount
    {
        get
        {
            if (s_count < 0)
            {
                var count = 0;
                foreach (var row in Rows)
                {
                    foreach (var c in row)
                    {
                        if (c == '#')
                        {
                            count++;
                        }
                    }
                }
                s_count = count;
            }
            return s_count;
        }
    }

    /// <summary>
    /// Every land dot as one geometry in grid units (a cell is 1 × 1), built once and shared: the map draws it under a scale
    /// transform, so resizing never rebuilds it.
    /// </summary>
    public static StreamGeometry Geometry
    {
        get
        {
            if (s_geometry is not null)
            {
                return s_geometry;
            }
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var size = new Size(DotRadius, DotRadius);
                for (var y = 0; y < Rows.Length; y++)
                {
                    var row = Rows[y];
                    for (var x = 0; x < row.Length; x++)
                    {
                        if (row[x] != '#')
                        {
                            continue;
                        }
                        var cx = x + 0.5;
                        var cy = y + 0.5;
                        ctx.BeginFigure(new Point(cx - DotRadius, cy), true);
                        ctx.ArcTo(new Point(cx + DotRadius, cy), size, 0, false, SweepDirection.Clockwise);
                        ctx.ArcTo(new Point(cx - DotRadius, cy), size, 0, false, SweepDirection.Clockwise);
                        ctx.EndFigure(true);
                    }
                }
            }
            s_geometry = geometry;
            return geometry;
        }
    }

    /// <summary>Whether the cell under a latitude and longitude is land.</summary>
    public static bool IsLand(double latitude, double longitude)
    {
        var p = Project(latitude, longitude);
        var x = Math.Clamp((int)p.X, 0, Columns - 1);
        var y = Math.Clamp((int)p.Y, 0, RowCount - 1);
        return Rows[y][x] == '#';
    }

    /// <summary>A latitude and longitude in grid units (latitudes outside the grid are held at its edge).</summary>
    public static Point Project(double latitude, double longitude)
    {
        var lat = Math.Clamp(double.IsFinite(latitude) ? latitude : 0, South, North);
        var lon = double.IsFinite(longitude) ? longitude : 0;
        lon = ((lon + 180) % 360 + 360) % 360;
        return new Point(lon / Step, (North - lat) / Step);
    }
}
