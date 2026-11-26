using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="BannerNotification"/>: top safe-area gap, side inset, max width, radius and icon size.</summary>
[ControlDesignToken]
internal sealed class BannerNotificationToken : AbstractControlDesignToken
{
    /// <summary>Gap below the top safe area in px: 8.</summary>
    public double TopGap { get; set; }

    /// <summary>Horizontal margin from the screen edges in px: 12.</summary>
    public double HorizontalInset { get; set; }

    /// <summary>Maximum width of the banner card on wider screens / tablets: 440.</summary>
    public double MaxWidth { get; set; }

    /// <summary>Corner radius of the banner card: 14.</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Size of the leading icon in px: 18.</summary>
    public double IconSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        TopGap = 8;
        HorizontalInset = 12;
        MaxWidth = 440;
        CardRadius = new CornerRadius(14);
        IconSize = 18;
    }
}
