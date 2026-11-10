using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the frost color of <see cref="WalletCard"/>.</summary>
[ControlDesignToken]
internal sealed class WalletCardToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Widest the card face grows (its height follows the 1.586 card ratio).</summary>
    public double FaceMaxWidth { get; set; }

    /// <summary>Space around the face column beside the balance column (the gap is on its right).</summary>
    public Thickness FaceColumnMargin { get; set; }

    /// <summary>Space around the face column when the columns stack (the gap is below it).</summary>
    public Thickness FaceColumnStackedMargin { get; set; }

    /// <summary>Block width below which the two columns stack.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>Space inside the card face.</summary>
    public Thickness FacePadding { get; set; }

    /// <summary>Height of an activity row.</summary>
    public double RowHeight { get; set; }

    /// <summary>Space inside an activity row.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Diameter of a merchant's disc.</summary>
    public double MerchantDiameter { get; set; }

    /// <summary>Height of the monthly limit meter.</summary>
    public double MeterHeight { get; set; }

    /// <summary>The frosted layer over a frozen face (the page background, mostly opaque).</summary>
    public Color FrostColor { get; set; }

    /// <summary>Text printed on the face (light on every finish, in both themes).</summary>
    public Color FaceTextColor { get; set; }

    /// <summary>Small labels printed on the face.</summary>
    public Color FaceMutedTextColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        BlockPadding = new Thickness(24);
        FaceMaxWidth = 360;
        FaceColumnMargin = new Thickness(0, 0, 32, 0);
        FaceColumnStackedMargin = new Thickness(0, 0, 0, 24);
        CompactThreshold = 680;
        FacePadding = new Thickness(22, 20);
        RowHeight = 56;
        RowPadding = new Thickness(10, 8);
        MerchantDiameter = 36;
        MeterHeight = 6;
        FaceTextColor = Color.FromArgb(245, 255, 255, 255);
        FaceMutedTextColor = Color.FromArgb(170, 255, 255, 255);
        FrostColor =Oklch.WithAlpha(n.Background, isDarkMode ? 0.62 : 0.58);
    }
}
