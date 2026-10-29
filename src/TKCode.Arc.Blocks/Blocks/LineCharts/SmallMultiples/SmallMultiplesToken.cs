using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and type sizes of <see cref="SmallMultiples"/>'s header and readout (Arc Small multiples, design reference).
/// Colors, fonts and the type ramp come from ArcDesign tokens in the theme; the self-drawn panels keep their metrics in
/// <see cref="SmallMultiplesPanel"/>.
/// </summary>
[ControlDesignToken]
internal sealed class SmallMultiplesToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius (20).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Header padding: 24 at the sides, 20 above, none below (16 at the sides below 480).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness PhoneHeaderPadding { get; set; }

    /// <summary>Readout padding: 24 at the sides, 20 above, 16 below (16 at the sides below 480).</summary>
    public Thickness ReadoutPadding { get; set; }

    public Thickness PhoneReadoutPadding { get; set; }

    /// <summary>The title: 16 on an 18 line; the description 12 on a 16.8 line, 2 under it.</summary>
    public double TitleSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    public double DescriptionGap { get; set; }

    /// <summary>Between the controls in the header, across and between wrapped rows (12).</summary>
    public double ControlGap { get; set; }

    /// <summary>The readout's period: 18 medium (the state beside it is 12, 8 apart; the total 20 further).</summary>
    public double PeriodSize { get; set; }

    /// <summary>Legend keys: 14 wide, 6 before their text, 16 between the two keys.</summary>
    public double KeyWidth { get; set; }

    public double KeyTextGap { get; set; }

    public double KeyGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardRadius = new CornerRadius(20);
        HeaderPadding = new Thickness(24, 20, 24, 0);
        PhoneHeaderPadding = new Thickness(16, 16, 16, 0);
        ReadoutPadding = new Thickness(24, 20, 24, 16);
        PhoneReadoutPadding = new Thickness(16, 16, 16, 12);
        TitleSize = 16;
        TitleLineHeight = 18;
        DescriptionSize = 12;
        DescriptionLineHeight = 16.8;
        DescriptionGap = 2;
        ControlGap = 12;
        PeriodSize = 18;
        KeyWidth = 14;
        KeyTextGap = 6;
        KeyGap = 16;
    }
}
