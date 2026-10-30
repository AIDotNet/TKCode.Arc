using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ConnectorSetup"/>: the card, its header, the 60px rows with their 32px actions and 2px progress
/// hairline, the access details and the footer. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ConnectorSetupToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The widest the card grows (Arc: fluid up to 520px).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The heading: 16/24.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The line under the heading: 14/20, 4px under the heading.</summary>
    public double DescriptionLineHeight { get; set; }

    public Thickness DescriptionMargin { get; set; }

    /// <summary>The space above the search field and above the list.</summary>
    public Thickness SearchMargin { get; set; }

    public Thickness ListMargin { get; set; }

    /// <summary>A row: 60px tall, the gap between its mark, its text and its action.</summary>
    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    /// <summary>The row's name: 14/20; its status line: 12/16, 2px under the name.</summary>
    public double NameLineHeight { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusLineHeight { get; set; }

    public double StatusGlyphSize { get; set; }

    /// <summary>The actions: 32px tall, 12px in from the sides, 13px medium text.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public double ActionFontSize { get; set; }

    /// <summary>The check and the chevron of the Connected button.</summary>
    public double ActionGlyphSize { get; set; }

    /// <summary>The progress hairline's height.</summary>
    public double HairlineHeight { get; set; }

    /// <summary>The access details: aligned with the row's text (the mark's tile plus the row gap), and their type.</summary>
    public Thickness DetailsMargin { get; set; }

    public double ScopeFontSize { get; set; }

    public double ScopeLineHeight { get; set; }

    public double ScopeGap { get; set; }

    public double ScopeGlyphSize { get; set; }

    /// <summary>The disconnect line under the scopes.</summary>
    public Thickness ConfirmMargin { get; set; }

    /// <summary>Pulls the ghost Disconnect button left by its padding, so its label lines up with the scopes.</summary>
    public Thickness DisconnectMargin { get; set; }

    /// <summary>The footer note: 12/16 with a 14px lock, 16px under the list.</summary>
    public Thickness FooterMargin { get; set; }

    public double FooterLineHeight { get; set; }

    public double FooterGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(20, 20, 20, 16);
        CardMaxWidth = 520;
        TitleLineHeight = 24;
        DescriptionLineHeight = 20;
        DescriptionMargin = new Thickness(0, 4, 0, 0);
        SearchMargin = new Thickness(0, 16, 0, 0);
        ListMargin = new Thickness(0, 8, 0, 0);
        RowHeight = 60;
        RowGap = 12;
        NameLineHeight = 20;
        StatusFontSize = 12;
        StatusLineHeight = 16;
        StatusGlyphSize = 13;
        ActionHeight = 32;
        ActionPadding = new Thickness(12, 0);
        ActionFontSize = 13;
        ActionGlyphSize = 14;
        HairlineHeight = 2;
        DetailsMargin = new Thickness(48, 0, 0, 14);
        ScopeFontSize = 13;
        ScopeLineHeight = 20;
        ScopeGap = 2;
        ScopeGlyphSize = 14;
        ConfirmMargin = new Thickness(0, 8, 0, 0);
        DisconnectMargin = new Thickness(-ActionPadding.Left, 0, 0, 0);
        FooterMargin = new Thickness(0, 12, 0, 0);
        FooterLineHeight = 16;
        FooterGlyphSize = 14;
    }
}
