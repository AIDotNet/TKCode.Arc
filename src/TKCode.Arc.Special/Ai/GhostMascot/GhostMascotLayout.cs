using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Where a <see cref="GhostMascot"/> draws its parts, in a 100 unit square: the regular drawing (56px and up) and the
/// compact one, which frames the ghost tighter and enlarges the eyes and marks so they stay legible at 24px.
/// </summary>
internal sealed class GhostMascotLayout
{
    /// <summary>The regular drawing: a centered ghost over a floating shadow.</summary>
    public static readonly GhostMascotLayout Regular = new()
    {
        Frame = 1,
        FramePivot = new Point(50, 50),
        EyeScale = 1,
        Stroke = 1,
        Motion = 1,
        SwirlCenter = new Point(80, 18),
        SwirlRadius = 6.5,
        SparkleCenter = new Point(82, 17),
        SparkleSize = 7,
        LineY = 91,
        LineStart = 70,
        LineEnd = 97,
        PenLength = 13,
        HasGround = true
    };

    /// <summary>The compact drawing (under 56px): tighter framing, bigger eyes, heavier swirl, pen and line, no shadow.</summary>
    public static readonly GhostMascotLayout Compact = new()
    {
        Frame = 1.2,
        FramePivot = new Point(48, 47),
        EyeScale = 1.35,
        Stroke = 1.75,
        Motion = 0.55,
        SwirlCenter = new Point(77, 21),
        SwirlRadius = 6.2,
        SparkleCenter = new Point(79, 19),
        SparkleSize = 7.5,
        LineY = 84,
        LineStart = 62,
        LineEnd = 87,
        PenLength = 10,
        HasGround = false
    };

    /// <summary>The ghost's scale within the square (the compact drawing zooms in around <see cref="FramePivot"/>).</summary>
    public double Frame { get; private init; }

    /// <summary>The point the compact framing zooms around.</summary>
    public Point FramePivot { get; private init; }

    /// <summary>The eyes' size relative to the regular drawing.</summary>
    public double EyeScale { get; private init; }

    /// <summary>The marks' stroke weight relative to the regular drawing.</summary>
    public double Stroke { get; private init; }

    /// <summary>How far the ghost moves (float, lift, lean) relative to the regular drawing.</summary>
    public double Motion { get; private init; }

    /// <summary>The center of the thinking swirl.</summary>
    public Point SwirlCenter { get; private init; }

    /// <summary>The outer radius of the swirl.</summary>
    public double SwirlRadius { get; private init; }

    /// <summary>The center of the done sparkle.</summary>
    public Point SparkleCenter { get; private init; }

    /// <summary>The sparkle's half size.</summary>
    public double SparkleSize { get; private init; }

    /// <summary>The writing line's height.</summary>
    public double LineY { get; private init; }

    /// <summary>Where the writing line starts.</summary>
    public double LineStart { get; private init; }

    /// <summary>Where the writing line ends.</summary>
    public double LineEnd { get; private init; }

    /// <summary>The pen's length.</summary>
    public double PenLength { get; private init; }

    /// <summary>Whether the floating ground shadow can be drawn.</summary>
    public bool HasGround { get; private init; }
}
