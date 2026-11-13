using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ProductListing"/>.</summary>
[ControlDesignToken]
internal sealed class ProductListingToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Narrowest a product card gets before the grid drops a column.</summary>
    public double MinCardWidth { get; set; }

    /// <summary>Gap between cards.</summary>
    public double CardGap { get; set; }

    /// <summary>Width of the filters column.</summary>
    public double FiltersWidth { get; set; }

    /// <summary>Space around the filters column beside the grid (the gap is on its right).</summary>
    public Thickness FiltersMargin { get; set; }

    /// <summary>Block width below which the filters fold behind a button above the grid.</summary>
    public double CompactThreshold { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        MinCardWidth = 160;
        CardGap = 20;
        FiltersWidth = 220;
        FiltersMargin = new Thickness(0, 0, 28, 0);
        CompactThreshold = 760;
    }
}
