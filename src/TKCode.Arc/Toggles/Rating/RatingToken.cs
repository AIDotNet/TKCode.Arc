using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RatingToken : AbstractControlDesignToken
{
    /// <summary>Cells are wider than their stars so each star is an easy target.</summary>
    public double CellSizeSM { get; set; }
    public double CellSizeMD { get; set; }
    public double CellSizeLG { get; set; }
    public double GlyphSizeSM { get; set; }
    public double GlyphSizeMD { get; set; }
    public double GlyphSizeLG { get; set; }
    public double CellGap { get; set; }
    public double FieldGap { get; set; }
    public double ValueGap { get; set; }
    public double RowGap { get; set; }

    /// <summary>The faint mark of the committed value while a different value is previewed.</summary>
    public double KeptOpacity { get; set; }
    public double DisabledOpacity { get; set; }
    public CornerRadius CellRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CellSizeSM = 28;
        CellSizeMD = 36;
        CellSizeLG = 44;
        GlyphSizeSM = 18;
        GlyphSizeMD = 24;
        GlyphSizeLG = 30;
        CellGap = 2;
        FieldGap = 6;
        ValueGap = 8;
        RowGap = 2;
        KeptOpacity = 0.3;
        DisabledOpacity = 0.5;
        CellRadius = new CornerRadius(12);
    }
}
