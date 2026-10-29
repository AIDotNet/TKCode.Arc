using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="HorizonChart"/>, measured from the Analytics group's horizon chart block (design reference only,
/// ADR 0024). Colours, radii, the type ramp and the fonts come from ArcDesign tokens in the theme; the band colours are
/// the Series1 / Series3 tokens composed in code. "Medium" values apply while the block is at most 900 wide, "compact"
/// ones while it is at most 560 wide (the block's own width, not the window's).
/// </summary>
[ControlDesignToken]
internal sealed class HorizonChartToken : AbstractControlDesignToken
{
    /// <summary>Block width at or below which the rows use their medium columns (900).</summary>
    public double MediumWidth { get; set; }

    /// <summary>Block width at or below which everything tightens (560).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Header padding (20, 24, 24, 0; compact 16, 16, 16, 0).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Header gaps: 24 between the titles and the controls, 16 between wrapped lines.</summary>
    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    /// <summary>Controls: 16 between the Sort and Bands switches, 8 between wrapped lines, 8 between a label and its switch.</summary>
    public double ControlsColumnGap { get; set; }

    public double ControlsRowGap { get; set; }

    public double ControlLabelGap { get; set; }

    /// <summary>Title 16 on a 17.6 line with −0.48 letter spacing, 2 above the description (12 on 16.8).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The titles shrink (and wrap) down to this width before the controls drop under them (280).</summary>
    public double TitleMinWidth { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double DescriptionGap { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>Readout padding (24, 20, 24, 16; compact 16, 16, 16, 14), gaps 32 across and 12 between wrapped lines.</summary>
    public Thickness ReadoutPadding { get; set; }

    public Thickness CompactReadoutPadding { get; set; }

    public double ReadoutColumnGap { get; set; }

    public double ReadoutRowGap { get; set; }

    /// <summary>The date: 18 on 25.2 (compact 16 on 22.4), Medium.</summary>
    public double DateFontSize { get; set; }

    public double DateLineHeight { get; set; }

    public double CompactDateFontSize { get; set; }

    public double CompactDateLineHeight { get; set; }

    /// <summary>The legend's width (300; a compact block gives it the whole line).</summary>
    public double LegendWidth { get; set; }

    /// <summary>Colour scale: cells 10 high, 2 apart, radius 2; the zero mark 14 high; 4 above the labels, 8 above the caption.</summary>
    public double RampHeight { get; set; }

    public double RampCellRadius { get; set; }

    public double ZeroMarkHeight { get; set; }

    public double ZeroMarkMargin { get; set; }

    public double LegendRowGap { get; set; }

    public double LegendLabelGap { get; set; }

    /// <summary>Rows 36 high (compact 34); the band strip is the row minus 4 above and below, radius 4.</summary>
    public double RowHeight { get; set; }

    public double CompactRowHeight { get; set; }

    public double BandInset { get; set; }

    public double BandRadius { get; set; }

    /// <summary>Plot side padding (24; compact 16).</summary>
    public double SidePadding { get; set; }

    public double CompactSidePadding { get; set; }

    /// <summary>Name column 132 (medium 116, compact 84), value column 60 (52, 44), column gap 20 (16, 10).</summary>
    public double NameWidth { get; set; }

    public double MediumNameWidth { get; set; }

    public double CompactNameWidth { get; set; }

    public double ValueWidth { get; set; }

    public double MediumValueWidth { get; set; }

    public double CompactValueWidth { get; set; }

    public double ColumnGap { get; set; }

    public double MediumColumnGap { get; set; }

    public double CompactColumnGap { get; set; }

    /// <summary>The crosshair reaches 6 above the first row and below the last.</summary>
    public double CrosshairOverhang { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MediumWidth = 900;
        CompactWidth = 560;
        HeaderPadding = new Thickness(24, 20, 24, 0);
        CompactHeaderPadding = new Thickness(16, 16, 16, 0);
        HeaderColumnGap = 24;
        HeaderRowGap = 16;
        ControlsColumnGap = 16;
        ControlsRowGap = 8;
        ControlLabelGap = 8;
        TitleLineHeight = 17.6;
        TitleMinWidth = 280;
        TitleLetterSpacing = -0.48;
        DescriptionGap = 2;
        SmallLineHeight = 16.8;
        ReadoutPadding = new Thickness(24, 20, 24, 16);
        CompactReadoutPadding = new Thickness(16, 16, 16, 14);
        ReadoutColumnGap = 32;
        ReadoutRowGap = 12;
        DateFontSize = 18;
        DateLineHeight = 25.2;
        CompactDateFontSize = 16;
        CompactDateLineHeight = 22.4;
        LegendWidth = 300;
        RampHeight = 10;
        RampCellRadius = 2;
        ZeroMarkHeight = 14;
        ZeroMarkMargin = 3;
        LegendRowGap = 8;
        LegendLabelGap = 4;
        RowHeight = 36;
        CompactRowHeight = 34;
        BandInset = 4;
        BandRadius = 4;
        SidePadding = 24;
        CompactSidePadding = 16;
        NameWidth = 132;
        MediumNameWidth = 116;
        CompactNameWidth = 84;
        ValueWidth = 60;
        MediumValueWidth = 52;
        CompactValueWidth = 44;
        ColumnGap = 20;
        MediumColumnGap = 16;
        CompactColumnGap = 10;
        CrosshairOverhang = 6;
    }
}
