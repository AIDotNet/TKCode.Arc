namespace TKCode.Arc;

/// <summary>How a shape of the onboarding drawing is filled and outlined.</summary>
internal enum OnboardingInk
{
    /// <summary>The border color filled, the muted text color outlined: the back of the box, the badge's ring.</summary>
    Shade,

    /// <summary>The raised surface filled, the foreground outlined: pages, people, the board.</summary>
    Paper,

    /// <summary>The raised surface filled, the accent outlined: the note that flies in.</summary>
    Note
}

/// <summary>One rounded rectangle of a scene: center, size, corner radius, rotation (degrees, clockwise), opacity, ink.</summary>
internal readonly record struct OnboardingRect(double X, double Y, double Width, double Height, double Radius, double Rotation, double Opacity,
                                               OnboardingInk Ink);

/// <summary>
/// One scene of the onboarding drawing in its 320 × 200 view box: seven rounded rectangles (drawn in order A … G), a
/// dotted trail (a quadratic curve from a start through a control point to an end), three lines and, in the badge, a
/// check mark. Every scene uses the same twelve shapes so one morphs into the next; a shape a scene does not need is
/// hidden (opacity 0) or folded into a point.
/// </summary>
internal sealed class OnboardingArtScene
{
    public required OnboardingRect[] Rects { get; init; }

    /// <summary>Start x, y; control x, y; end x, y.</summary>
    public required double[] Trail { get; init; }

    public required double TrailOpacity { get; init; }

    /// <summary>Three lines, x1, y1, x2, y2 each.</summary>
    public required double[][] Lines { get; init; }

    public required bool[] LineAccent { get; init; }

    public required bool HasCheck { get; init; }

    private double[][]? _centers;
    private double[][]? _shapes;

    /// <summary>Each rectangle's center as a spring target (x, y); built once, never changed.</summary>
    public double[][] Centers => _centers ??= Rects.Select(r => new[] { r.X, r.Y }).ToArray();

    /// <summary>Each rectangle's shape as a spring target (width, height, radius, rotation); built once, never changed.</summary>
    public double[][] Shapes => _shapes ??= Rects.Select(r => new[] { r.Width, r.Height, r.Radius, r.Rotation }).ToArray();
}

/// <summary>
/// The four scenes (our own drawing; the slots play the same roles in every scene so the morph tells one story): the box's
/// handle becomes the left person's body and then the left ribbon, the box's back the left head and the right ribbon,
/// three waiting pages drop into the box and become the people and the ring, the note becomes the right head, the box's
/// front becomes the board and then the badge's disc, and the box's label line and plus sign become the note's lines,
/// the board's lines and the badge's rays.
/// </summary>
internal static class OnboardingArtScenes
{
    public const double ViewWidth = 320;
    public const double ViewHeight = 200;
    public const int RectCount = 7;
    public const int LineCount = 3;

    /// <summary>The check mark: start, corner, end (view-box units).</summary>
    public static readonly double[] Check = [146, 92, 156, 102, 175, 81];

    /// <summary>How far each shape follows the pointer (A … G); the trail, the lines and the check follow fully.</summary>
    public static readonly double[] RectDepth = [.4, .55, .7, .8, .9, 1, 1];

    public static readonly OnboardingArtScene Folder = new()
    {
        Rects =
        [
            new(200, 60, 48, 20, 7, 0, 1, OnboardingInk.Shade),
            new(160, 106, 140, 84, 12, 0, 1, OnboardingInk.Shade),
            new(100, 6, 40, 52, 5, -14, 0, OnboardingInk.Paper),
            new(158, -6, 40, 52, 5, 4, 0, OnboardingInk.Paper),
            new(218, 6, 40, 52, 5, 16, 0, OnboardingInk.Paper),
            new(30, 20, 44, 56, 5, -24, 0, OnboardingInk.Note),
            new(160, 124, 148, 60, 12, 0, 1, OnboardingInk.Paper)
        ],
        Trail = [84, 36, 84, 36, 84, 36],
        TrailOpacity = 0,
        Lines = [[146, 120, 174, 120], [226, 26, 242, 26], [234, 18, 234, 34]],
        LineAccent = [false, true, true],
        HasCheck = false
    };

    public static readonly OnboardingArtScene Files = new()
    {
        Rects =
        [
            new(200, 60, 48, 20, 7, 0, 1, OnboardingInk.Shade),
            new(160, 106, 140, 84, 12, 0, 1, OnboardingInk.Shade),
            new(130, 78, 52, 68, 6, -12, 1, OnboardingInk.Paper),
            new(160, 72, 52, 68, 6, 0, 1, OnboardingInk.Paper),
            new(190, 78, 52, 68, 6, 12, 1, OnboardingInk.Paper),
            new(62, 42, 46, 58, 6, -14, 1, OnboardingInk.Note),
            new(160, 124, 148, 60, 12, 0, 1, OnboardingInk.Paper)
        ],
        Trail = [90, 38, 106, 10, 124, 42],
        TrailOpacity = 1,
        Lines = [NoteLine(-12, 26), NoteLine(-3, 22), NoteLine(6, 14)],
        LineAccent = [false, false, false],
        HasCheck = false
    };

    public static readonly OnboardingArtScene Team = new()
    {
        Rects =
        [
            new(104, 168, 42, 32, 16, 0, 1, OnboardingInk.Paper),
            new(104, 136, 22, 22, 11, 0, 1, OnboardingInk.Paper),
            new(160, 168, 42, 32, 16, 0, 1, OnboardingInk.Paper),
            new(160, 136, 22, 22, 11, 0, 1, OnboardingInk.Paper),
            new(216, 168, 42, 32, 16, 0, 1, OnboardingInk.Paper),
            new(216, 136, 22, 22, 11, 0, 1, OnboardingInk.Paper),
            new(160, 64, 128, 76, 10, 0, 1, OnboardingInk.Paper)
        ],
        Trail = [226, 124, 240, 92, 200, 68],
        TrailOpacity = 1,
        Lines = [[116, 50, 170, 50], [116, 64, 196, 64], [116, 78, 156, 78]],
        LineAccent = [false, true, false],
        HasCheck = false
    };

    public static readonly OnboardingArtScene Badge = new()
    {
        Rects =
        [
            new(148, 160, 20, 50, 4, 12, 1, OnboardingInk.Paper),
            new(172, 160, 20, 50, 4, -12, 1, OnboardingInk.Paper),
            new(160, 92, 104, 104, 52, 0, 1, OnboardingInk.Shade),
            new(160, 92, 16, 16, 8, 0, 0, OnboardingInk.Paper),
            new(160, 92, 16, 16, 8, 0, 0, OnboardingInk.Paper),
            new(160, 92, 16, 16, 8, 0, 0, OnboardingInk.Paper),
            new(160, 92, 80, 80, 40, 0, 1, OnboardingInk.Paper)
        ],
        Trail = [160, 92, 160, 92, 160, 92],
        TrailOpacity = 0,
        Lines = [Ray(-125), Ray(-90), Ray(-55)],
        LineAccent = [false, false, false],
        HasCheck = true
    };

    public static OnboardingArtScene Get(OnboardingScene scene) => scene switch
    {
        OnboardingScene.Files => Files,
        OnboardingScene.Team => Team,
        OnboardingScene.Badge => Badge,
        _ => Folder
    };

    /// <summary>A line written on the flying note: from x −14 in the note's own frame, turned and moved with the note.</summary>
    private static double[] NoteLine(double y, double length)
    {
        const double cx = 62, cy = 42, angle = -14 * Math.PI / 180;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        double x0 = -14, x1 = -14 + length;
        return
        [
            Math.Round(cx + x0 * cos - y * sin, 3), Math.Round(cy + x0 * sin + y * cos, 3),
            Math.Round(cx + x1 * cos - y * sin, 3), Math.Round(cy + x1 * sin + y * cos, 3)
        ];
    }

    /// <summary>A ray of the badge: from radius 61 to 72 around the badge's center, at an angle (−90° points up).</summary>
    private static double[] Ray(double degrees)
    {
        const double cx = 160, cy = 92, inner = 61, outer = 72;
        var angle = degrees * Math.PI / 180;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return
        [
            Math.Round(cx + inner * cos, 3), Math.Round(cy + inner * sin, 3),
            Math.Round(cx + outer * cos, 3), Math.Round(cy + outer * sin, 3)
        ];
    }
}
