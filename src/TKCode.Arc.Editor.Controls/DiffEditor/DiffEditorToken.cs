namespace TKCode.Arc;

/// <summary>Geometry of <see cref="DiffEditor"/> that follows neither the theme nor the accent.</summary>
[ControlDesignToken]
public sealed class DiffEditorToken : AbstractControlDesignToken
{
    /// <summary>The width of the overview ruler at the right edge, which marks where the changes are.</summary>
    public double OverviewRulerWidth { get; set; }

    /// <summary>The width of the gutter column with the + and − signs of inserted and removed lines.</summary>
    public double IndicatorWidth { get; set; }

    /// <summary>The width of the gutter column with the arrows that revert a change.</summary>
    public double RevertColumnWidth { get; set; }

    /// <summary>The width of the strip between the two editors when blocks of lines moved (it draws their connections).</summary>
    public double MovedBlocksWidth { get; set; }

    /// <summary>The width of the line between the two editors when no block moved.</summary>
    public double SeparatorWidth { get; set; }

    /// <summary>The height of the bar that stands for a hidden run of unchanged lines.</summary>
    public double CollapsedRegionHeight { get; set; }

    /// <summary>The opacity of the background of changed characters, over the line background (0 to 1).</summary>
    public double CharHighlightOpacity { get; set; }

    /// <summary>The smallest height of the diff editor.</summary>
    public double MinHeight { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        OverviewRulerWidth = 12;
        IndicatorWidth = 16;
        RevertColumnWidth = 16;
        MovedBlocksWidth = 20;
        SeparatorWidth = 1;
        CollapsedRegionHeight = 24;
        CharHighlightOpacity = isDarkMode ? 0.4 : 0.3;
        MinHeight = 120;
    }
}
