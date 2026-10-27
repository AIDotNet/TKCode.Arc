using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the neutral band colors of <see cref="BudgetVariance"/> (sizes from Arc Budget variance, design reference).</summary>
[ControlDesignToken]
internal sealed class BudgetVarianceToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the card.</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Space inside the card.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Gap between the header, the table and the legend.</summary>
    public double SectionGap { get; set; }

    /// <summary>Container width from which the block is a table (narrower stacks each row).</summary>
    public double TableThreshold { get; set; }

    /// <summary>Gap between the table's columns.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Width of the actual and budget columns.</summary>
    public double AmountColumnWidth { get; set; }

    /// <summary>Width of the variance column.</summary>
    public double VarianceColumnWidth { get; set; }

    /// <summary>Width of the chevron column.</summary>
    public double ChevronColumnWidth { get; set; }

    /// <summary>Share of the flexible width given to the name column (the chart takes the rest).</summary>
    public double NameColumnShare { get; set; }

    /// <summary>Space inside a department or total row.</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>Space around the column header (over the row's padding) and below it.</summary>
    public Thickness HeaderRowMargin { get; set; }

    /// <summary>Space around the total row's cells.</summary>
    public Thickness TotalRowMargin { get; set; }

    /// <summary>Gap between the total row and the first department.</summary>
    public Thickness TotalRowGap { get; set; }

    /// <summary>Space above and below a budget line.</summary>
    public Thickness LineRowMargin { get; set; }

    /// <summary>Space around an open department's lines (inset to the row's padding).</summary>
    public Thickness LinesMargin { get; set; }

    /// <summary>Space inside an open department's lines, above the first and below the last.</summary>
    public Thickness LinesPadding { get; set; }

    /// <summary>Space between legend entries.</summary>
    public Thickness LegendMargin { get; set; }

    /// <summary>Minimum height of a department row.</summary>
    public double RowMinHeight { get; set; }

    /// <summary>Corner radius of a department row and its open panel.</summary>
    public CornerRadius RowRadius { get; set; }

    /// <summary>Indent of a budget line's name (past the avatar).</summary>
    public double LineIndent { get; set; }

    /// <summary>Diameter of an owner's avatar.</summary>
    public double AvatarDiameter { get; set; }

    /// <summary>Band height of the total, a department and a budget line.</summary>
    public double TotalBandHeight { get; set; }

    public double DepartmentBandHeight { get; set; }

    public double LineBandHeight { get; set; }

    /// <summary>Bar height of the total, a department and a budget line.</summary>
    public double TotalBarHeight { get; set; }

    public double DepartmentBarHeight { get; set; }

    public double LineBarHeight { get; set; }

    /// <summary>Corner radius of a band.</summary>
    public double BandRadius { get; set; }

    /// <summary>How far the budget tick reaches past the band, above and below.</summary>
    public double TickOverhang { get; set; }

    /// <summary>Width of the budget tick.</summary>
    public double TickWidth { get; set; }

    /// <summary>The three qualitative bands: the text color at 6, 11 and 17 percent opacity, so they read on any row fill (accent independent).</summary>
    public Color BandLowColor { get; set; }

    public Color BandMidColor { get; set; }

    public Color BandHighColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardRadius = new CornerRadius(16);
        CardPadding = new Thickness(20, 16, 20, 20);
        SectionGap = 20;
        TableThreshold = 760;
        ColumnGap = 20;
        AmountColumnWidth = 84;
        VarianceColumnWidth = 112;
        ChevronColumnWidth = 16;
        NameColumnShare = 0.314;
        RowPadding = new Thickness(12, 10);
        HeaderRowMargin = new Thickness(12, 0, 12, 6);
        TotalRowMargin = new Thickness(12, 14);
        TotalRowGap = new Thickness(0, 0, 0, 6);
        LineRowMargin = new Thickness(0, 8);
        LinesMargin = new Thickness(12, 0);
        LinesPadding = new Thickness(0, 4, 0, 8);
        LegendMargin = new Thickness(12, 0);
        RowMinHeight = 60;
        RowRadius = new CornerRadius(12);
        LineIndent = 40;
        AvatarDiameter = 28;
        TotalBandHeight = 18;
        DepartmentBandHeight = 14;
        LineBandHeight = 8;
        TotalBarHeight = 8;
        DepartmentBarHeight = 6;
        LineBarHeight = 4;
        BandRadius = 3;
        TickOverhang = 3;
        TickWidth = 2;
        BandLowColor = Oklch.WithAlpha(n.Foreground, 0.06);
        BandMidColor = Oklch.WithAlpha(n.Foreground, 0.11);
        BandHighColor = Oklch.WithAlpha(n.Foreground, 0.17);
    }
}
