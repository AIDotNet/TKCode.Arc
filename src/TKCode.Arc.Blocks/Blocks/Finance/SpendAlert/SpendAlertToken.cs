using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SpendAlert"/>.</summary>
[ControlDesignToken]
internal sealed class SpendAlertToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Gap between the block's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Height of the meter's bar.</summary>
    public double BarHeight { get; set; }

    /// <summary>Room above the bar for the alert labels.</summary>
    public double LabelBand { get; set; }

    /// <summary>Space inside the alert banner.</summary>
    public Thickness BannerPadding { get; set; }

    /// <summary>How far the banner rises as it appears.</summary>
    public double BannerRise { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        SectionGap = 20;
        BarHeight = 12;
        LabelBand = 18;
        BannerPadding = new Thickness(14, 12);
        BannerRise = 6;
    }
}
