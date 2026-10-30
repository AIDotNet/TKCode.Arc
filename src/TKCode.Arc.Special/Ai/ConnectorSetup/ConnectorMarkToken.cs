using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ConnectorMark"/>: the padding of the tile around the glyph, its radius and stroke.</summary>
[ControlDesignToken]
internal sealed class ConnectorMarkToken : AbstractControlDesignToken
{
    /// <summary>The tile's padding around the glyph (with the 1px edge, a 20px glyph sits in a 36px tile).</summary>
    public Thickness TilePadding { get; set; }

    /// <summary>The tile's corner radius.</summary>
    public CornerRadius TileCornerRadius { get; set; }

    /// <summary>The glyph's stroke width.</summary>
    public double GlyphStrokeWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TilePadding = new Thickness(7);
        TileCornerRadius = new CornerRadius(10);
        GlyphStrokeWidth = 1.75;
    }
}
