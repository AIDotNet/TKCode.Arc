using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="WebhookTester"/> (design reference: the Developer group's webhook tester, ADR 0027) and the
/// accent-independent tints it derives from the neutrals. Colors that follow the accent, the type ramp and the fonts come
/// from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class WebhookTesterToken : AbstractControlDesignToken
{
    /// <summary>The card's widest width (760).</summary>
    public double MaxCardWidth { get; set; }

    /// <summary>From this width of the card the request and the response sit side by side (680); below they stack.</summary>
    public double SplitWidth { get; set; }

    /// <summary>Below this width the delivery time leaves the rows (420).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the header, the body and the deliveries (20, 16) and its compact form (16, 16).</summary>
    public Thickness SectionPadding { get; set; }

    public Thickness CompactSectionPadding { get; set; }

    /// <summary>Between the two columns (20) and between the fields of a column (14).</summary>
    public double ColumnGap { get; set; }

    public double FieldGap { get; set; }

    /// <summary>Height of a field, a button and a tab (36) and the smallest target (28).</summary>
    public double ControlHeight { get; set; }

    public double MinTarget { get; set; }

    /// <summary>The payload editor: at least this tall (200), with a gutter this wide (36).</summary>
    public double EditorMinHeight { get; set; }

    public double GutterWidth { get; set; }

    /// <summary>The editor's line height (20) and text padding (10, 10).</summary>
    public double EditorLineHeight { get; set; }

    public Thickness EditorPadding { get; set; }

    /// <summary>A delivery row's height (40) and its inset (8, 0).</summary>
    public double RowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    /// <summary>The timeline's circle (22) and the line between steps (2).</summary>
    public double StepSize { get; set; }

    public double ConnectorThickness { get; set; }

    /// <summary>The traveling dot's diameter (6).</summary>
    public double DotSize { get; set; }

    /// <summary>The response panel's height range (120 to 260); it scrolls inside.</summary>
    public double PanelMinHeight { get; set; }

    public double PanelMaxHeight { get; set; }

    /// <summary>The recent list shows at most this much before it scrolls (240).</summary>
    public double ListMaxHeight { get; set; }

    /// <summary>The tinted fill of the editor, the response panel and a hovered row: the muted surface mixed into the surface.</summary>
    public Color InsetBackground { get; set; }

    public Color RowHoverBackground { get; set; }

    /// <summary>The selected row: the muted surface a little stronger.</summary>
    public Color RowSelectedBackground { get; set; }

    /// <summary>The border of a key cap in the send hint.</summary>
    public Color KeyBorder { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        MaxCardWidth = 760;
        SplitWidth = 680;
        CompactWidth = 420;
        SectionPadding = new Thickness(20, 16);
        CompactSectionPadding = new Thickness(16, 16);
        ColumnGap = 20;
        FieldGap = 14;
        ControlHeight = 36;
        MinTarget = 28;
        EditorMinHeight = 200;
        GutterWidth = 36;
        EditorLineHeight = 20;
        EditorPadding = new Thickness(10, 10);
        RowHeight = 40;
        RowPadding = new Thickness(8, 0);
        StepSize = 22;
        ConnectorThickness = 2;
        DotSize = 6;
        PanelMinHeight = 120;
        PanelMaxHeight = 260;
        ListMaxHeight = 240;
        InsetBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.7);
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.8);
        RowSelectedBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 1.0);
        KeyBorder = Oklch.WithAlpha(n.BorderStrong, 0.8);
    }
}
