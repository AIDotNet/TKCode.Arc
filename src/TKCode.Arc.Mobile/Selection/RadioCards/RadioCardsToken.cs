using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of the mobile <see cref="RadioCards"/>: the gaps of the grid and the selection ring.</summary>
// The desktop TKCode.Arc package declares a RadioCardsToken too, and identities are unique per application.
[ControlDesignToken(Identity = "MobileRadioCards")]
internal sealed class RadioCardsToken : AbstractControlDesignToken
{
    /// <summary>Space between two columns of cards: 10.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Space between two rows of cards: 10.</summary>
    public double RowGap { get; set; }

    /// <summary>Width of the selection ring around the selected card: 2.</summary>
    public double RingThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ColumnGap = 10;
        RowGap = 10;
        RingThickness = 2;
    }
}
