using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ListGroup"/>: 16px side margins, a 1px outline on the card, and the header and footer insets.</summary>
[ControlDesignToken]
internal sealed class ListGroupToken : AbstractControlDesignToken
{
    /// <summary>Space around the whole group: 16 at the sides, 8 above and below.</summary>
    public Thickness GroupPadding { get; set; }

    /// <summary>Outline around the card: 1.</summary>
    public Thickness CardBorderThickness { get; set; }

    /// <summary>Margin of the header above the card: 16 at the sides, 6 below.</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>Margin of the footer under the card: 16 at the sides, 6 above.</summary>
    public Thickness FooterMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GroupPadding = new Thickness(16, 8);
        CardBorderThickness = new Thickness(1);
        HeaderMargin = new Thickness(16, 0, 16, 6);
        FooterMargin = new Thickness(16, 6, 16, 0);
    }
}
