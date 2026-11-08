using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TicketTear"/> (Arc Ticket tear, design reference). The paper, edge, text and focus ring colors
/// come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TicketTearToken : AbstractControlDesignToken
{
    /// <summary>The ticket is at least this high, so the perforation has room for a row of holes.</summary>
    public double TicketMinHeight { get; set; }

    /// <summary>Radius of the two notches where the perforation meets the long edges.</summary>
    public double NotchRadius { get; set; }

    /// <summary>Radius of a perforation hole.</summary>
    public double HoleRadius { get; set; }

    /// <summary>Distance the holes aim for between their centres; they are spread evenly to fill the height.</summary>
    public double HolePitch { get; set; }

    /// <summary>Space around the ticket's body.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>Space around the stub's content, clear of the holes.</summary>
    public Thickness StubPadding { get; set; }

    /// <summary>Color of the shadow a lifted flap casts.</summary>
    public Color FlapShadowColor { get; set; }

    /// <summary>Margin of the stub's focus ring.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Corner radius of the stub's focus ring.</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TicketMinHeight = 148;
        NotchRadius = 9;
        HoleRadius = 2.5;
        HolePitch = 13;
        BodyPadding = new Thickness(20, 18);
        StubPadding = new Thickness(14, 12, 12, 12);
        FlapShadowColor = Color.FromArgb(isDarkMode ? (byte)0x4A : (byte)0x1C, 0, 0, 0);
        FocusRingMargin = new Thickness(-3);
        FocusRingRadius = new CornerRadius(16);
    }
}
