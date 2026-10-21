using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="BillingOverview"/>: card rhythm, the plan header, the card mark and the invoice table.</summary>
[ControlDesignToken]
internal sealed class BillingOverviewToken : AbstractControlDesignToken
{
    /// <summary>Gap between the plan, payment and invoice cards.</summary>
    public double GroupGap { get; set; }

    /// <summary>Block width below which the price moves under the plan name and the Invoice column hides.</summary>
    public double NarrowThreshold { get; set; }

    /// <summary>Row width below which the card row's button moves under its text (as <see cref="SettingsRow"/> does).</summary>
    public double RowStackThreshold { get; set; }

    /// <summary>Space around the plan header inside its card (the card adds 24px at the sides).</summary>
    public Thickness PlanInset { get; set; }

    /// <summary>Gap between the plan name and its status badge.</summary>
    public double PlanTitleGap { get; set; }

    /// <summary>Gap between the label, the plan name and the line under it.</summary>
    public double PlanLineGap { get; set; }

    /// <summary>Gap between the text and the trailing part of a split row (price, buttons).</summary>
    public double SplitGap { get; set; }

    /// <summary>Gap above the trailing part once it moves under the text.</summary>
    public double StackedGap { get; set; }

    /// <summary>Gap between footer buttons.</summary>
    public double ActionGap { get; set; }

    /// <summary>Vertical space of the card row (matches <see cref="SettingsRow"/>).</summary>
    public Thickness CardRowInset { get; set; }

    /// <summary>Minimum width of the text brand mark.</summary>
    public double CardMarkWidth { get; set; }

    /// <summary>Height of the text brand mark.</summary>
    public double CardMarkHeight { get; set; }

    /// <summary>Corner radius of the text brand mark.</summary>
    public CornerRadius CardMarkRadius { get; set; }

    /// <summary>Inline padding of the text brand mark.</summary>
    public Thickness CardMarkInset { get; set; }

    /// <summary>Gap between the card title and the expiry line.</summary>
    public double CardTextGap { get; set; }

    /// <summary>Space after the alert glyph before the expiry text.</summary>
    public Thickness ExpiryGlyphMargin { get; set; }

    /// <summary>Size of the alert glyph before an expiry warning.</summary>
    public double ExpiryGlyphSize { get; set; }

    /// <summary>Height of an invoice row.</summary>
    public double InvoiceRowHeight { get; set; }

    /// <summary>Height of the invoice column header.</summary>
    public double InvoiceHeaderHeight { get; set; }

    /// <summary>Width of the Date column while the Invoice column is shown.</summary>
    public double DateColumnWidth { get; set; }

    /// <summary>Width of the right-aligned Amount column.</summary>
    public double AmountColumnWidth { get; set; }

    /// <summary>Width of the Amount column in a narrow block.</summary>
    public double AmountColumnWidthNarrow { get; set; }

    /// <summary>Width of the Status column (fits a small "Refunded" badge).</summary>
    public double StatusColumnWidth { get; set; }

    /// <summary>Gap before the Status column.</summary>
    public Thickness StatusColumnMargin { get; set; }

    /// <summary>Gap before the Status column in a narrow block.</summary>
    public Thickness StatusColumnMarginNarrow { get; set; }

    /// <summary>Width of the download column (a 36px icon button).</summary>
    public double ActionColumnWidth { get; set; }

    /// <summary>Gap before the download column.</summary>
    public Thickness ActionColumnMargin { get; set; }

    /// <summary>Gap between the Date and Invoice columns.</summary>
    public Thickness NumberColumnMargin { get; set; }

    /// <summary>Opacity of refunded and void badges and amounts.</summary>
    public double MutedOpacity { get; set; }

    /// <summary>Space around the invoice table inside its card.</summary>
    public Thickness TableInset { get; set; }

    /// <summary>Gap between the lines of the empty invoice message.</summary>
    public double EmptyGap { get; set; }

    /// <summary>Space under the icon tile of the empty invoice message.</summary>
    public Thickness EmptyTileMargin { get; set; }

    /// <summary>Glyph size in the icon tile of the empty invoice message.</summary>
    public double EmptyGlyphSize { get; set; }

    /// <summary>Space above and below the empty invoice message.</summary>
    public Thickness EmptyInset { get; set; }

    /// <summary>Side of the icon tile of the empty invoice message.</summary>
    public double EmptyTile { get; set; }

    /// <summary>Corner radius of the icon tile of the empty invoice message.</summary>
    public CornerRadius EmptyTileRadius { get; set; }

    /// <summary>Glyph size in icon tiles and icon buttons.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Gap between the "Show all invoices" text and its chevron.</summary>
    public double ShowAllGap { get; set; }

    /// <summary>Pulls a ghost button left so its label lines up with the text above it (its own 12px inline padding).</summary>
    public Thickness GhostAlignMargin { get; set; }

    /// <summary>Line height of body text (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GroupGap = 16;
        NarrowThreshold = 560;
        RowStackThreshold = 440;
        PlanInset = new Thickness(0, 20, 0, 20);
        PlanTitleGap = 10;
        PlanLineGap = 4;
        SplitGap = 16;
        StackedGap = 12;
        ActionGap = 8;
        CardRowInset = new Thickness(0, 16);
        CardMarkWidth = 36;
        CardMarkHeight = 24;
        CardMarkRadius = new CornerRadius(6);
        CardMarkInset = new Thickness(5, 0);
        CardTextGap = 2;
        ExpiryGlyphMargin = new Thickness(0, 0, 6, 0);
        ExpiryGlyphSize = 14;
        InvoiceRowHeight = 48;
        InvoiceHeaderHeight = 32;
        DateColumnWidth = 136;
        AmountColumnWidth = 96;
        AmountColumnWidthNarrow = 76;
        StatusColumnWidth = 84;
        StatusColumnMargin = new Thickness(28, 0, 0, 0);
        StatusColumnMarginNarrow = new Thickness(14, 0, 0, 0);
        ActionColumnWidth = 36;
        ActionColumnMargin = new Thickness(4, 0, 0, 0);
        NumberColumnMargin = new Thickness(12, 0, 0, 0);
        MutedOpacity = 0.7;
        TableInset = new Thickness(0, 4, 0, 8);
        EmptyGap = 4;
        EmptyTileMargin = new Thickness(0, 0, 0, 8);
        EmptyGlyphSize = 18;
        EmptyInset = new Thickness(0, 20, 0, 24);
        EmptyTile = 40;
        EmptyTileRadius = new CornerRadius(12);
        GlyphSize = 16;
        ShowAllGap = 6;
        GhostAlignMargin = new Thickness(-12, 0, 0, 0);
        BodyLineHeight = 14 * 1.4;
    }
}
