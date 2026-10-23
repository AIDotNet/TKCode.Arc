namespace TKCode.Arc;

/// <summary>The detail layers of a <see cref="SemanticZoom"/> map, each faded in by on-screen size.</summary>
internal enum SemanticZoomLayer
{
    /// <summary>Team lead avatar and "Name · N projects · N open tasks" under the team name.</summary>
    TeamLine,

    /// <summary>Gray task chips give way to task blocks (the chips fade out as this fades in).</summary>
    Tiles,

    /// <summary>Project title and percentage.</summary>
    ProjectTitle,

    /// <summary>Project health dot and "On track · Due Oct 22".</summary>
    ProjectStatus,

    /// <summary>Project people row text ("Name and N others").</summary>
    ProjectPeople,

    /// <summary>Task status icon and title on the block.</summary>
    TaskTitle,

    /// <summary>Task block corner "2/3 · Sep 9" (before the details cross-fade it out).</summary>
    TaskMeta,

    /// <summary>Task details: status, summary, assignee, place.</summary>
    TaskDetail,

    /// <summary>Checklist heading, bar and items.</summary>
    Checklist
}

/// <summary>
/// The level-of-detail table (Arc Semantic zoom 4.6, design reference). Every window is keyed on the on-screen scale k
/// (px per world unit), not on the readout zoom, so a wide stage (larger fit scale) shows more detail at fit. Values are
/// the settled curves (smoothstep); the stage smooths them over time (τ ≈ 140ms). Font sizes follow k with clamps.
/// </summary>
internal static class SemanticZoomLod
{
    /// <summary>Layer count (array size for per-layer state).</summary>
    public const int LayerCount = 9;

    /// <summary>Readout zoom from which the scope is a team, a project and a task (center under the stage center).</summary>
    public const double TeamZoom = 1.08;

    public const double ProjectZoom = 2.35;

    public const double TaskZoom = 18.3;

    /// <summary>Person circles turn into avatars from this diameter (px; the wide stage's fit, 9.5 px, already shows them).</summary>
    public const double AvatarMinDiameter = 9;

    /// <summary>Dimming of teams outside the scope and of projects / tasks outside the scope's project.</summary>
    public const double TeamDim = 0.42;

    public const double ProjectDim = 0.5;

    /// <summary>Chip opacity per status (Done, In progress, In review, To do, Blocked).</summary>
    public const double ChipDone = 0.13;

    public const double ChipInProgress = 0.85;

    public const double ChipInReview = 0.42;

    public const double ChipTodo = 0.6;

    public const double ChipBlocked = 0.85;

    /// <summary>Background dot layers: base spacing (wu), ×4 per layer, peak opacity, fade windows on the screen spacing.</summary>
    public const double DotSpacing = 96;

    public const double DotPeak = 0.55;

    public const double DotSize = 1.5;

    /// <summary>The k window of a layer (start, end).</summary>
    public static (double Start, double End) Window(SemanticZoomLayer layer) => layer switch
    {
        SemanticZoomLayer.TeamLine => (0.58, 0.70),
        SemanticZoomLayer.Tiles => (0.85, 1.50),
        SemanticZoomLayer.ProjectTitle => (1.01, 1.22),
        SemanticZoomLayer.ProjectStatus => (1.27, 1.53),
        SemanticZoomLayer.ProjectPeople => (1.33, 1.60),
        SemanticZoomLayer.TaskTitle => (1.72, 2.37),
        SemanticZoomLayer.TaskMeta => (2.55, 3.30),
        // Fitted to the settled scan (z 17.9 → 24.5); the block corner fades out on the same curve.
        SemanticZoomLayer.TaskDetail => (7.25, 9.9),
        SemanticZoomLayer.Checklist => (10.8, 11.9),
        _ => (0, 0)
    };

    /// <summary>Hermite smoothstep of <paramref name="x"/> between the edges.</summary>
    public static double Smoothstep(double edge0, double edge1, double x)
    {
        if (edge1 <= edge0)
        {
            return x >= edge1 ? 1 : 0;
        }
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>The settled opacity of <paramref name="layer"/> at scale <paramref name="k"/>.</summary>
    public static double Alpha(SemanticZoomLayer layer, double k)
    {
        var (start, end) = Window(layer);
        var alpha = Smoothstep(start, end, k);
        if (layer == SemanticZoomLayer.TaskMeta)
        {
            // The block's corner text cross-fades with the task details.
            alpha *= 1 - Alpha(SemanticZoomLayer.TaskDetail, k);
        }
        return alpha;
    }

    /// <summary>Fills <paramref name="target"/> with every layer's settled opacity.</summary>
    public static void Targets(double k, Span<double> target)
    {
        for (var i = 0; i < LayerCount && i < target.Length; i++)
        {
            target[i] = Alpha((SemanticZoomLayer)i, k);
        }
    }

    // Font sizes (px) -------------------------------------------------------------------------------------------------

    public static double TeamTitleSize(double k) => Math.Clamp(21 * k, 11, 22);

    public static double TeamPercentSize(double k) => Math.Clamp(15 * k, 11, 15);

    public static double TeamLineSize(double k) => Math.Clamp(15 * k, 11, 13);

    public static double ProjectTitleSize(double k) => Math.Clamp(9.75 * k, 12, 20);

    /// <summary>Project percentage and status line.</summary>
    public static double ProjectSmallSize(double k) => Math.Clamp(7.12 * k, 11, 13);

    public static double ProjectPeopleSize(double k) => Math.Clamp(6.75 * k, 11, 13);

    public static double TaskTitleSize(double k) => Math.Clamp(3.45 * k, 11, 14);

    public static double TaskMetaSize(double k) => Math.Clamp(3.04 * k, 11, 12.32);

    /// <summary>The task title in the details grows past 14px from k ≈ 7.2 (17.4px at 30×), at most 28px.</summary>
    public static double DetailTitleSize(double k) => Math.Min(28, 14 + 0.69 * Math.Max(0, k - 7.2));

    /// <summary>Details text (status, summary, place, checklist heading).</summary>
    public const double DetailTextSize = 13;

    /// <summary>Assignee name and checklist items in the details.</summary>
    public const double DetailNameSize = 14;

    // Radii and strokes (px) ------------------------------------------------------------------------------------------

    public static double TeamRadius(double k) => Math.Clamp(13.5 * k, 6, 30);

    public static double CardRadius(double k) => Math.Min(9 * k, 26);

    public static double TileRadius(double k) => Math.Clamp(3 * k, 1.5, 20);

    public static double BarHeight(double k) => Math.Clamp(2.2 * k, 1.5, 6);

    /// <summary>
    /// Opacity of the background dot layer whose dots are <paramref name="spacing"/> px apart: fades in from 14 px (just
    /// visible at 17) to 30 px, fades out from 150 px to 420 px (fitted to the measured layer scan).
    /// </summary>
    public static double DotAlpha(double spacing) =>
        DotPeak * Smoothstep(14, 30, spacing) * (1 - Smoothstep(150, 420, spacing));

    /// <summary>The finest dot layer exponent n (spacing 96·4ⁿ·k) that is still visible at scale <paramref name="k"/>.</summary>
    public static int FirstDotLayer(double k)
    {
        // Spacing ≥ 14px: 96·4ⁿ·k ≥ 14.
        var n = Math.Ceiling(Math.Log(14 / (DotSpacing * Math.Max(1e-9, k)), 4));
        return (int)n;
    }

    /// <summary>Chip opacity of a status at the overview.</summary>
    public static double ChipAlpha(SemanticZoomTaskStatus status) => status switch
    {
        SemanticZoomTaskStatus.Done => ChipDone,
        SemanticZoomTaskStatus.InProgress => ChipInProgress,
        SemanticZoomTaskStatus.InReview => ChipInReview,
        SemanticZoomTaskStatus.Blocked => ChipBlocked,
        _ => ChipTodo
    };

    /// <summary>The scope level of a readout zoom (deepest level whose threshold is reached).</summary>
    public static SemanticZoomLevel LevelForZoom(double zoom) =>
        zoom >= TaskZoom ? SemanticZoomLevel.Task
        : zoom >= ProjectZoom ? SemanticZoomLevel.Project
        : zoom >= TeamZoom ? SemanticZoomLevel.Team
        : SemanticZoomLevel.Organization;
}
