using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="MagicLinkSignIn"/>.</summary>
[ControlDesignToken]
internal sealed class MagicLinkSignInToken : AbstractControlDesignToken
{
    /// <summary>Space inside the card.</summary>
    public Thickness FrameInset { get; set; }

    /// <summary>Space inside the card below <see cref="NarrowWidth"/>.</summary>
    public Thickness FrameInsetNarrow { get; set; }

    /// <summary>The card's default maximum width.</summary>
    public double PreferredWidth { get; set; }

    /// <summary>Below this width the card tightens its padding.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Side of the icon tile over each state's title.</summary>
    public double IconTile { get; set; }

    /// <summary>Size of the glyph in the icon tile.</summary>
    public double IconGlyph { get; set; }

    /// <summary>Space between the icon tile and the title.</summary>
    public Thickness TitleMargin { get; set; }

    /// <summary>Line height of titles.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Line height of body text.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Space between a title and its sentence.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Space above a state's controls.</summary>
    public Thickness BodyMargin { get; set; }

    /// <summary>Gap between stacked buttons.</summary>
    public double ButtonGap { get; set; }

    /// <summary>Diameter of the countdown ring on the resend button.</summary>
    public double RingSize { get; set; }

    /// <summary>Stroke of the countdown ring.</summary>
    public double RingThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameInset = new Thickness(32, 32, 32, 28);
        FrameInsetNarrow = new Thickness(22, 24, 22, 20);
        PreferredWidth = 420;
        NarrowWidth = 360;
        IconTile = 44;
        IconGlyph = 20;
        TitleMargin = new Thickness(0, 18, 0, 0);
        TitleLineHeight = 28;
        BodyLineHeight = 20;
        DescriptionMargin = new Thickness(0, 6, 0, 0);
        BodyMargin = new Thickness(0, 22, 0, 0);
        ButtonGap = 8;
        RingSize = 14;
        RingThickness = 2;
    }
}
