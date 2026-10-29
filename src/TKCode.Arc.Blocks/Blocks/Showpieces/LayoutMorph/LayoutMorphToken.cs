using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="LayoutMorph"/> (Arc Layout morph, design reference): the header, the view switch, the stage
/// frame, the panel, the floating tag and the detail's buttons. Colors, radii of the framed surfaces, shadows and type
/// come from the ArcDesign tokens in the theme; the cards' geometry lives in <c>LayoutMorphEngine</c>.
/// </summary>
[ControlDesignToken]
internal sealed class LayoutMorphToken : AbstractControlDesignToken
{
    /// <summary>Space under the header (24) and between its title and lede (10); the header is at most 640 wide.</summary>
    public Thickness HeaderMargin { get; set; }

    public double HeaderGap { get; set; }

    public double HeaderMaxWidth { get; set; }

    /// <summary>The 52px title: line height 57.2, letter spacing −1.56 (−0.03em); 36px in dense containers.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double DenseTitleLineHeight { get; set; }

    public double DenseTitleLetterSpacing { get; set; }

    /// <summary>The lede: 16/22.4 (14/19.6 dense).</summary>
    public double LedeLineHeight { get; set; }

    public double DenseLedeLineHeight { get; set; }

    /// <summary>The toolbar row: at least 44 high, 12 above the stage; the hint sits 20 right of the switch, or 10 under it.</summary>
    public double ToolbarMinHeight { get; set; }

    public Thickness ToolbarMargin { get; set; }

    public Thickness NarrowHintMargin { get; set; }

    /// <summary>The hint: 12/16.8.</summary>
    public double HintLineHeight { get; set; }

    /// <summary>The view switch: 3px inside a 1px border, options 2 apart, 36 high, 13 to the sides, icon 16 with 7 to the label, radius 15.</summary>
    public Thickness BarPadding { get; set; }

    public double OptionGap { get; set; }

    public double OptionHeight { get; set; }

    public Thickness OptionPadding { get; set; }

    public double OptionIconSize { get; set; }

    public double OptionIconGap { get; set; }

    public CornerRadius OptionRadius { get; set; }

    /// <summary>Keyboard ring around an option: 2px, 2px out (radius 15 + 4).</summary>
    public CornerRadius OptionFocusRadius { get; set; }

    public Thickness OptionFocusMargin { get; set; }

    /// <summary>The panel sits 12 under the stage.</summary>
    public Thickness PanelMargin { get; set; }

    /// <summary>Panel name: 14/19.6 over 12/16.8; statistic value 22/26.4 with 2 under the 12/16.8 label.</summary>
    public double PanelTitleLineHeight { get; set; }

    public double PanelLineLineHeight { get; set; }

    public double StatValueLineHeight { get; set; }

    public double StatGap { get; set; }

    /// <summary>The floating tag: padding 7 / 11 / 8, radius 12, two 12/16 lines.</summary>
    public Thickness TagPadding { get; set; }

    public CornerRadius TagRadius { get; set; }

    public double TagLineHeight { get; set; }

    /// <summary>The detail's close button: a 32 circle 14 from the top right, X 16.</summary>
    public double CloseButtonSize { get; set; }

    public Thickness CloseButtonMargin { get; set; }

    public double CloseGlyphSize { get; set; }

    /// <summary>The detail's main button: 160 × 36, radius 18, 20 from the bottom right; full width 12 in when stacked.</summary>
    public double ActionWidth { get; set; }

    public double ActionHeight { get; set; }

    public CornerRadius ActionRadius { get; set; }

    public Thickness ActionMargin { get; set; }

    public Thickness StackedActionMargin { get; set; }

    public double ActionGlyphSize { get; set; }

    public double ActionGlyphGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderMargin = new Thickness(0, 0, 0, 24);
        HeaderGap = 10;
        HeaderMaxWidth = 640;
        TitleLineHeight = 57.2;
        TitleLetterSpacing = -1.56;
        DenseTitleLineHeight = 39.6;
        DenseTitleLetterSpacing = -1.08;
        LedeLineHeight = 22.4;
        DenseLedeLineHeight = 19.6;
        ToolbarMinHeight = 44;
        ToolbarMargin = new Thickness(0, 0, 0, 12);
        NarrowHintMargin = new Thickness(0, 10, 0, 0);
        HintLineHeight = 16.8;
        BarPadding = new Thickness(3);
        OptionGap = 2;
        OptionHeight = 36;
        OptionPadding = new Thickness(13, 0);
        OptionIconSize = 16;
        OptionIconGap = 7;
        OptionRadius = new CornerRadius(15);
        OptionFocusRadius = new CornerRadius(19);
        OptionFocusMargin = new Thickness(-4);
        PanelMargin = new Thickness(0, 12, 0, 0);
        PanelTitleLineHeight = 19.6;
        PanelLineLineHeight = 16.8;
        StatValueLineHeight = 26.4;
        StatGap = 2;
        TagPadding = new Thickness(11, 7, 11, 8);
        TagRadius = new CornerRadius(12);
        TagLineHeight = 16;
        CloseButtonSize = 32;
        CloseButtonMargin = new Thickness(0, 14, 14, 0);
        CloseGlyphSize = 16;
        ActionWidth = 160;
        ActionHeight = 36;
        ActionRadius = new CornerRadius(18);
        ActionMargin = new Thickness(0, 0, 20, 20);
        StackedActionMargin = new Thickness(12, 0, 12, 12);
        ActionGlyphSize = 16;
        ActionGlyphGap = 8;
    }
}
