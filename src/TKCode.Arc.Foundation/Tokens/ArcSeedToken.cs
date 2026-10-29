using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The theme-wide inputs every token calculation can read: the accent seed and the motion level. It replaces the
/// 255 Ant global tokens of AtomUI; Arc only ever read <see cref="ColorPrimary"/> and <see cref="EnableMotion"/>.
/// </summary>
public sealed class ArcSeedToken
{
    /// <summary>The accent seed used when the theme state names none (#1677FF, AtomUI's default primary).</summary>
    public static readonly Color DefaultAccent = Color.FromRgb(0x16, 0x77, 0xFF);

    /// <summary>Seed values of the default state.</summary>
    public static readonly ArcSeedToken Default = new(DefaultAccent, ArcMotionLevel.Full);

    /// <summary>Creates seed values.</summary>
    /// <param name="colorPrimary">The accent seed.</param>
    /// <param name="motionLevel">The motion level.</param>
    public ArcSeedToken(Color colorPrimary, ArcMotionLevel motionLevel)
    {
        ColorPrimary = colorPrimary;
        MotionLevel = motionLevel;
    }

    /// <summary>The accent seed (the brand color the global token derives its accent ramp from).</summary>
    public Color ColorPrimary { get; }

    /// <summary>The motion level of the theme state.</summary>
    public ArcMotionLevel MotionLevel { get; }

    /// <summary>False when the motion level is <see cref="ArcMotionLevel.Off"/>.</summary>
    public bool EnableMotion => MotionLevel != ArcMotionLevel.Off;
}
