namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SlotTextToken : AbstractControlDesignToken
{
    /// <summary>Height of the text line, in em.</summary>
    public double LineHeightEm { get; set; }

    /// <summary>Distance between two cells of a reel, in em. Taller than the line, so a landed glyph's neighbours sit outside the window.</summary>
    public double CellPitchEm { get; set; }

    /// <summary>How far a reel's window reaches past the line at the top and bottom, in em.</summary>
    public double WindowInsetYEm { get; set; }

    /// <summary>How far a reel's window reaches past the slot on each side, in em.</summary>
    public double WindowInsetXEm { get; set; }

    /// <summary>The height over which glyphs fade at the window's edges, in em.</summary>
    public double EdgeFadeEm { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineHeightEm = 1.15;
        CellPitchEm = 1.6;
        WindowInsetYEm = 0.22;
        WindowInsetXEm = 0.08;
        EdgeFadeEm = 0.24;
    }
}
