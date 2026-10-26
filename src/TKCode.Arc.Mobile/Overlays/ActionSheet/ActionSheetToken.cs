using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="ActionSheet"/>: the space around its two cards, the gap between them and the header padding.</summary>
[ControlDesignToken]
internal sealed class ActionSheetToken : AbstractControlDesignToken
{
    /// <summary>Space around the cards: 8 at the sides and below.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Space above the Cancel card, which sets it apart from the actions: 8.</summary>
    public Thickness CancelMargin { get; set; }

    /// <summary>Padding of the title and message area: 16 by 12.</summary>
    public Thickness HeaderPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelPadding = new Thickness(8, 0, 8, 8);
        CancelMargin = new Thickness(0, 8, 0, 0);
        HeaderPadding = new Thickness(16, 12);
    }
}
