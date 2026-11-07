using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and theme-dependent shadow colors of <see cref="WalletStack"/> (Arc Wallet stack, design reference). The
/// card materials are drawn from literal OKLCH values per material and theme (see <c>WalletStackCardArt</c>); text and
/// surface colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class WalletStackToken : AbstractControlDesignToken
{
    /// <summary>The wallet is at most this wide (18.5rem); narrower containers scale every card detail with it.</summary>
    public double WalletMaxWidth { get; set; }

    /// <summary>Height of the header row (title and count, or the All cards button).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Title size (Geist 16, weight 500, letter spacing −0.03em).</summary>
    public double TitleSize { get; set; }

    /// <summary>The balance amount's size (Geist 22, letter spacing −0.03em).</summary>
    public double BalanceSize { get; set; }

    /// <summary>Padding of the All cards pill: 10 before the chevron, 12 after the text.</summary>
    public Thickness AllCardsPadding { get; set; }

    /// <summary>The soft drop shadow under each card (light oklch(.22 .02 260 / .26), dark black / .7).</summary>
    public Color CardShadowColor { get; set; }

    /// <summary>The 1–3px contact shadow every card keeps (light black / .16, dark black / .45).</summary>
    public Color CardContactColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        WalletMaxWidth = 296;
        HeaderHeight = 32;
        TitleSize = 16;
        BalanceSize = 22;
        AllCardsPadding = new Thickness(10, 0, 12, 0);
        CardShadowColor = isDarkMode ? Oklch.ToColor(0, 0, 0, 0.7) : Oklch.ToColor(0.22, 0.02, 260, 0.26);
        CardContactColor = Oklch.ToColor(0, 0, 0, isDarkMode ? 0.45 : 0.16);
    }
}
