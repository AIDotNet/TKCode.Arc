using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="GhostText"/>: the text metrics and padding of the field, the label and footer rows,
/// the chip, its key caps and hints, and the disabled opacity (the motion durations live in <see cref="GhostTextMotion"/>). Colors, radii, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class GhostTextToken : AbstractControlDesignToken
{
    /// <summary>The text: 14px on a 22px line (the ghost shares the same layout).</summary>
    public double TextFontSize { get; set; }

    public double TextLineHeight { get; set; }

    /// <summary>The text's padding inside a bordered multi-line field, and inside a plain one.</summary>
    public Thickness EditorPadding { get; set; }

    public Thickness PlainEditorPadding { get; set; }

    /// <summary>The text's padding in a single-line field (it is centered in <see cref="SingleLineHeight"/>).</summary>
    public Thickness SingleLinePadding { get; set; }

    public double SingleLineHeight { get; set; }

    /// <summary>The label row above the text: 12px medium, its padding and the gap to its icon.</summary>
    public double LabelFontSize { get; set; }

    public Thickness LabelPadding { get; set; }

    public Thickness PlainLabelPadding { get; set; }

    /// <summary>The footer row under the text: its height, padding and the gap between its pieces.</summary>
    public double FooterHeight { get; set; }

    public Thickness FooterPadding { get; set; }

    public Thickness PlainFooterPadding { get; set; }

    public double FooterGap { get; set; }

    public double FooterFontSize { get; set; }

    /// <summary>The chip: 24px tall, 8px in from its sides, 12px text and 5px between its parts.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public Thickness SingleLineChipMargin { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipGap { get; set; }

    /// <summary>A key cap: 18px tall, 5px in from its sides, 11px medium text.</summary>
    public double KeyHeight { get; set; }

    public Thickness KeyPadding { get; set; }

    public double KeyFontSize { get; set; }

    /// <summary>The space between the key hints and the chip.</summary>
    public double HintGap { get; set; }

    /// <summary>The whole field's opacity when disabled.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TextFontSize = 14;
        TextLineHeight = 22;
        EditorPadding = new Thickness(14, 12, 14, 4);
        PlainEditorPadding = new Thickness(0, 4, 0, 4);
        SingleLinePadding = new Thickness(14, 0, 8, 0);
        SingleLineHeight = 44;
        LabelFontSize = 12;
        LabelPadding = new Thickness(14, 10, 14, 0);
        PlainLabelPadding = new Thickness(0, 0, 0, 2);
        FooterHeight = 40;
        FooterPadding = new Thickness(14, 0, 8, 0);
        PlainFooterPadding = new Thickness(0);
        FooterGap = 10;
        FooterFontSize = 12;
        ChipHeight = 24;
        ChipPadding = new Thickness(8, 0);
        SingleLineChipMargin = new Thickness(0, 0, 10, 0);
        ChipFontSize = 12;
        ChipGap = 5;
        KeyHeight = 18;
        KeyPadding = new Thickness(5, 0);
        KeyFontSize = 11;
        HintGap = 10;
        DisabledOpacity = 0.55;
    }
}
