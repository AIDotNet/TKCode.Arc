using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the footer tint of <see cref="SettingsGroup"/>.</summary>
[ControlDesignToken]
internal sealed class SettingsGroupToken : AbstractControlDesignToken
{
    /// <summary>Space around the header in a card.</summary>
    public Thickness HeaderInset { get; set; }

    /// <summary>Space around the rows in a card (rows add their own vertical padding).</summary>
    public Thickness RowsInset { get; set; }

    /// <summary>Space around the footer content in a card.</summary>
    public Thickness FooterInset { get; set; }

    /// <summary>Space under the header of a plain group.</summary>
    public Thickness PlainHeaderInset { get; set; }

    /// <summary>Space above the footer content of a plain group.</summary>
    public Thickness PlainFooterInset { get; set; }

    /// <summary>Gap between the header and its description.</summary>
    public double HeaderGap { get; set; }

    /// <summary>Gap between the header text and the header actions.</summary>
    public Thickness HeaderActionsMargin { get; set; }

    /// <summary>The footer strip: the foreground at 2.5%, over the card surface.</summary>
    public Color FooterBackground { get; set; }

    /// <summary>Line height of the description (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        HeaderInset = new Thickness(24, 20, 24, 4);
        RowsInset = new Thickness(24, 4);
        FooterInset = new Thickness(24, 14);
        PlainHeaderInset = new Thickness(0, 0, 0, 4);
        PlainFooterInset = new Thickness(0, 14, 0, 0);
        HeaderGap = 4;
        HeaderActionsMargin = new Thickness(16, 0, 0, 0);
        FooterBackground = Oklch.WithAlpha(n.Foreground, isDarkMode ? 0.04 : 0.025);
        BodyLineHeight = 14 * 1.4;
    }
}
