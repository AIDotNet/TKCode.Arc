using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and stub colors of <see cref="TicketTear"/> (Arc Ticket tear, design reference). The body's paper, its text
/// and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TicketTearToken : AbstractControlDesignToken
{
    /// <summary>The ticket is at least this high, so the perforation has room for a row of holes.</summary>
    public double TicketMinHeight { get; set; }

    /// <summary>Corner radius of the ticket's outer corners.</summary>
    public CornerRadius PaperRadius { get; set; }

    /// <summary>Radius of the two notches at the ends of the perforation.</summary>
    public double NotchRadius { get; set; }

    /// <summary>Radius of a perforation hole.</summary>
    public double HoleRadius { get; set; }

    /// <summary>Distance the holes aim for between their centres; they are spread evenly to fill the height.</summary>
    public double HolePitch { get; set; }

    /// <summary>How far inside the stub's left edge the perforation runs.</summary>
    public double PerforationInset { get; set; }

    /// <summary>Space around the ticket's body.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>Space around the stub's content, clear of the holes.</summary>
    public Thickness StubPadding { get; set; }

    /// <summary>Paper of the stub: it stands out from the body, light on a dark theme and dark on a light one.</summary>
    public Color StubPaperColor { get; set; }

    /// <summary>Text on the stub.</summary>
    public Color StubTextColor { get; set; }

    /// <summary>Color of the shadow a lifted flap casts.</summary>
    public Color FlapShadowColor { get; set; }

    /// <summary>Margin of the stub's focus ring.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Corner radius of the stub's focus ring.</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TicketMinHeight = 148;
        PaperRadius = new CornerRadius(0);
        NotchRadius = 8;
        HoleRadius = 2;
        HolePitch = 10;
        PerforationInset = 4.5;
        BodyPadding = new Thickness(25, 24);
        StubPadding = new Thickness(14, 12, 10, 12);
        StubPaperColor = Oklch.MixOklab(neutrals.Foreground, neutrals.Background, 0.24);
        StubTextColor = neutrals.Background;
        FlapShadowColor = Color.FromArgb(isDarkMode ? (byte)0x30 : (byte)0x1C, 0, 0, 0);
        FocusRingMargin = new Thickness(-3);
        FocusRingRadius = new CornerRadius(4);
    }
}
