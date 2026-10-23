using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MixtureOfExperts"/> (Arc Mixture of experts, design reference): the card's padding and row gap
/// (24 / 20, and 16 / 16 under 480 px), the space between the sequence's divider and its tokens, and the width of the
/// load balance column. Colors, radii and type come from the ArcDesign tokens in the theme; the stage geometry lives in
/// <c>MixtureOfExpertsLayout</c>.
/// </summary>
[ControlDesignToken]
internal sealed class MixtureOfExpertsToken : AbstractControlDesignToken
{
    /// <summary>The card's padding.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The card's padding under 480 px.</summary>
    public Thickness CompactCardPadding { get; set; }

    /// <summary>The gap between the header, stage, sequence and footer.</summary>
    public double RowGap { get; set; }

    /// <summary>The row gap under 480 px.</summary>
    public double CompactRowGap { get; set; }

    /// <summary>The space between the sequence's divider and its tokens.</summary>
    public Thickness SequencePadding { get; set; }

    /// <summary>The width of the load balance column beside the footer text (cards of 640 px and wider).</summary>
    public double LoadColumnWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(24);
        CompactCardPadding = new Thickness(16);
        RowGap = 20;
        CompactRowGap = 16;
        SequencePadding = new Thickness(0, 16, 0, 0);
        LoadColumnWidth = 220;
    }
}
