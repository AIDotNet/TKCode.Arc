// Ported from Arc's free logo-marquee block (MIT, Copyright (c) 2026 Elia Kuratli): logo-marquee.module.css.
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry and timing of <see cref="LogoMarquee"/>; its colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class LogoMarqueeToken : AbstractControlDesignToken
{
    /// <summary>How long the row takes to travel one list (39 s).</summary>
    public TimeSpan TravelDuration { get; set; }

    /// <summary>The gap between two marks, also after the last one (56).</summary>
    public double ItemGap { get; set; }

    /// <summary>The gap between marks in a narrow block (40).</summary>
    public double ItemGapNarrow { get; set; }

    /// <summary>The gap between a mark and its name (10).</summary>
    public double NameGap { get; set; }

    /// <summary>The mark's edge (22).</summary>
    public double MarkSize { get; set; }

    /// <summary>The mark's edge in a narrow block (20).</summary>
    public double MarkSizeNarrow { get; set; }

    /// <summary>The heading is at most this wide (540).</summary>
    public double TitleMaxWidth { get; set; }

    /// <summary>The description is at most this wide (36ch of base text).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>The intro's padding (40 40 36).</summary>
    public Thickness IntroInset { get; set; }

    /// <summary>The intro's padding in a narrow block (28 20 24).</summary>
    public Thickness IntroInsetNarrow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TravelDuration = TimeSpan.FromSeconds(39);
        ItemGap = 56;
        ItemGapNarrow = 40;
        NameGap = 10;
        MarkSize = 22;
        MarkSizeNarrow = 20;
        TitleMaxWidth = 540;
        DescriptionMaxWidth = 324;
        IntroInset = new Thickness(40, 40, 40, 36);
        IntroInsetNarrow = new Thickness(20, 28, 20, 24);
    }
}
