using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelCard"/>: the card, its header and footer, the summary, chips, spec and price cells, the
/// estimator, the benchmark rows and the compare table, the breakpoints it reads from its own width, and the dim of the
/// rows that are not hovered. Colors, radii and fonts come from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class ModelCardToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows; it is fluid below this.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The card's padding, and the tighter padding under the narrow breakpoint.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness NarrowCardPadding { get; set; }

    /// <summary>The space between the card's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>The mark square in the header, its glyph and the monogram drawn when a model has no mark.</summary>
    public double MarkSize { get; set; }

    public double MarkGlyphSize { get; set; }

    public double MonogramFontSize { get; set; }

    /// <summary>The space between the mark and the name.</summary>
    public double HeaderGap { get; set; }

    /// <summary>The model's name: 16/22 semibold.</summary>
    public double NameFontSize { get; set; }

    public double NameLineHeight { get; set; }

    public FontWeight NameFontWeight { get; set; }

    /// <summary>The provider under the name: 13/18.</summary>
    public double ProviderFontSize { get; set; }

    public double ProviderLineHeight { get; set; }

    /// <summary>The summary: 14/20 over two reserved lines (three under the compact breakpoint).</summary>
    public double SummaryFontSize { get; set; }

    public double SummaryLineHeight { get; set; }

    /// <summary>A modality chip: its height, inside padding, glyph, type and the gap between chips.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGlyphSize { get; set; }

    public double ChipFontSize { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The dash of an unsupported chip's outline.</summary>
    public double ChipDash { get; set; }

    /// <summary>Section headings: 13/18 medium.</summary>
    public double HeadingFontSize { get; set; }

    public double HeadingLineHeight { get; set; }

    /// <summary>Labels of specs and prices, captions and the readout: 12/16.</summary>
    public double LabelFontSize { get; set; }

    public double LabelLineHeight { get; set; }

    /// <summary>Spec and price values: 18/24 semibold, tabular.</summary>
    public double ValueFontSize { get; set; }

    public double ValueLineHeight { get; set; }

    /// <summary>A spec or price cell: its padding and the gap between cells; the row height when cells stack.</summary>
    public Thickness CellPadding { get; set; }

    public double CellGap { get; set; }

    public double StackedRowHeight { get; set; }

    /// <summary>The context gauge under the context window.</summary>
    public double GaugeHeight { get; set; }

    /// <summary>The estimated cost: 22px semibold in a reserved width beside the slider.</summary>
    public double CostFontSize { get; set; }

    public double CostWidth { get; set; }

    /// <summary>A benchmark row: its height, the name column, the score column and the bar's thickness.</summary>
    public double BenchmarkRowHeight { get; set; }

    public double StackedBenchmarkRowHeight { get; set; }

    public double BenchmarkNameWidth { get; set; }

    public double BenchmarkScoreWidth { get; set; }

    public double BarHeight { get; set; }

    public Thickness RowPadding { get; set; }

    /// <summary>The readout under the bars: two reserved lines, three under the narrow breakpoint.</summary>
    public double ReadoutLineHeight { get; set; }

    /// <summary>The opacity of the benchmark rows that are not hovered or focused.</summary>
    public double DimmedOpacity { get; set; }

    /// <summary>A compare table row, its label column, the stacked row's height and the mini bars.</summary>
    public double CompareRowHeight { get; set; }

    public double StackedCompareRowHeight { get; set; }

    public double CompareLabelWidth { get; set; }

    public double MiniBarWidth { get; set; }

    /// <summary>The footer: the model id's type and the gap between its parts.</summary>
    public double IdFontSize { get; set; }

    public double FooterGap { get; set; }

    /// <summary>The loading skeleton's lowest opacity while it pulses.</summary>
    public double SkeletonPulseOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        CardPadding = new Thickness(20);
        NarrowCardPadding = new Thickness(16);
        SectionGap = 18;
        MarkSize = 36;
        MarkGlyphSize = 20;
        MonogramFontSize = 15;
        HeaderGap = 12;
        NameFontSize = 16;
        NameLineHeight = 22;
        NameFontWeight = FontWeight.SemiBold;
        ProviderFontSize = 13;
        ProviderLineHeight = 18;
        SummaryFontSize = 14;
        SummaryLineHeight = 20;
        ChipHeight = 26;
        ChipPadding = new Thickness(9, 0);
        ChipGlyphSize = 13;
        ChipFontSize = 12;
        ChipGap = 6;
        ChipDash = 3;
        HeadingFontSize = 13;
        HeadingLineHeight = 18;
        LabelFontSize = 12;
        LabelLineHeight = 16;
        ValueFontSize = 18;
        ValueLineHeight = 24;
        CellPadding = new Thickness(12, 10);
        CellGap = 8;
        StackedRowHeight = 36;
        GaugeHeight = 4;
        CostFontSize = 22;
        CostWidth = 136;
        BenchmarkRowHeight = 34;
        StackedBenchmarkRowHeight = 48;
        BenchmarkNameWidth = 128;
        BenchmarkScoreWidth = 64;
        BarHeight = 6;
        RowPadding = new Thickness(8, 0);
        ReadoutLineHeight = 18;
        DimmedOpacity = 0.5;
        CompareRowHeight = 34;
        StackedCompareRowHeight = 52;
        CompareLabelWidth = 148;
        MiniBarWidth = 36;
        IdFontSize = 12;
        FooterGap = 8;
        SkeletonPulseOpacity = 0.5;
    }
}
