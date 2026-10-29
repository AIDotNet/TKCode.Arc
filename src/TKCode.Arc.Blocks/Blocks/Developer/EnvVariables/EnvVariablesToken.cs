using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="EnvVariables"/>, measured from Arc Env variables (design reference only, ADR 0027). Colors,
/// type sizes and fonts come from ArcDesign tokens; the few tints Arc mixes (banner, editor, hover fills) are mixed at run
/// time from those tokens. Paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class EnvVariablesToken : AbstractControlDesignToken
{
    /// <summary>Below this width of the control the matrix turns into stacked rows (760).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this width the side insets tighten from 24 to 16 and buttons fill the width (560).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>The header's padding (24, 20, 24, 16).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Side inset of rows, toolbar, banner and panels (24; 16 when narrow).</summary>
    public double SideInset { get; set; }

    public double NarrowSideInset { get; set; }

    /// <summary>The width of an environment column of the matrix (92) and of the edit column (52).</summary>
    public double ScopeColumnWidth { get; set; }

    public double EditColumnWidth { get; set; }

    /// <summary>A matrix row is at least 64 high.</summary>
    public double RowMinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactWidth = 760;
        NarrowWidth = 560;
        HeaderPadding = new Thickness(24, 20, 24, 16);
        SideInset = 24;
        NarrowSideInset = 16;
        ScopeColumnWidth = 92;
        EditColumnWidth = 52;
        RowMinHeight = 64;
    }
}
