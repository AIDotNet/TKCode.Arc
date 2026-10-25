using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CookieConsentToken : AbstractControlDesignToken
{
    /// <summary>The layer keeps 16px from the left, right and bottom edges.</summary>
    public Thickness LayerMargin { get; set; }

    /// <summary>Bar padding (10px 10px 10px 18px) and the gap between message and buttons (space-4).</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Panel padding (18px) and the gap between its parts (space-3).</summary>
    public Thickness PanelPadding { get; set; }

    public double PanelGap { get; set; }

    /// <summary>Gap between buttons (6px).</summary>
    public double ActionGap { get; set; }

    /// <summary>The panel header sits 4px higher and pulls the description 6px closer.</summary>
    public Thickness PanelHeadMargin { get; set; }

    /// <summary>The category list bleeds 6px into the panel padding.</summary>
    public Thickness ListMargin { get; set; }

    /// <summary>Panel actions: 8px above, below a subtle rule.</summary>
    public Thickness PanelActionsPadding { get; set; }

    /// <summary>Close button (32px, pulled 8px into the padding).</summary>
    public double CloseDiameter { get; set; }

    public Thickness CloseMargin { get; set; }

    /// <summary>Reopen button (44px round, Cookie icon 20px).</summary>
    public double ChipDiameter { get; set; }

    /// <summary>
    /// Extra space between message lines so they sit at leading-body (1.4). Line spacing instead of a fixed line height,
    /// because a fixed line height breaks lines that hold the inline policy link.
    /// </summary>
    public double MessageLineSpacing { get; set; }

    public double TitleLineHeight { get; set; }

    public CornerRadius LinkFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LinkFocusRingRadius = new CornerRadius(6);
        LayerMargin = new Thickness(16, 0, 16, 16);
        BarPadding = new Thickness(18, 10, 10, 10);
        PanelPadding = new Thickness(18);
        PanelGap = 12;
        ActionGap = 6;
        PanelHeadMargin = new Thickness(0, -4, 0, -6);
        ListMargin = new Thickness(-6, 0);
        PanelActionsPadding = new Thickness(0, 8, 0, 0);
        CloseDiameter = 32;
        CloseMargin = new Thickness(0, 0, -8, 0);
        ChipDiameter = 44;
        MessageLineSpacing = 14 * (1.4 - 1.21);
        TitleLineHeight = 16 * 1.4;
    }
}
