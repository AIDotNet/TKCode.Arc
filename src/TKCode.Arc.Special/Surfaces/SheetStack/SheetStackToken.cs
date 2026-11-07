using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Layer geometry of <see cref="SheetStack"/> (Arc Sheet stack, design reference), as measured: sheets at most 560 wide
/// and 20 below the layer's top, dialogs 440 wide with 40 above and below; each level below the top peeks 10 (sheets) or
/// 14 (dialogs) above the sheet over it, scales by 0.05 and darkens by half a step. The scrim is black at 32% (light) or
/// 56% (dark), accent independent.
/// </summary>
[ControlDesignToken]
internal sealed class SheetStackToken : AbstractControlDesignToken
{
    /// <summary>The widest a sheet gets (560); a narrower layer gets a full-width sheet.</summary>
    public double SheetMaxWidth { get; set; }

    /// <summary>The dialog width (440).</summary>
    public double DialogWidth { get; set; }

    /// <summary>The space a sheet leaves above itself at most (20).</summary>
    public double SheetTopInset { get; set; }

    /// <summary>The space a dialog leaves above and below itself at least (40 each).</summary>
    public double DialogVerticalInset { get; set; }

    /// <summary>How far each level below shows above the one over it: 10 for sheets, 14 for dialogs.</summary>
    public double SheetPeek { get; set; }

    public double DialogPeek { get; set; }

    /// <summary>Scale lost per level below the top (0.05) and the dim gained per level (0.5, full at two levels).</summary>
    public double DepthScaleStep { get; set; }

    public double DepthDimStep { get; set; }

    /// <summary>How far past its own height a closing sheet travels (24).</summary>
    public double ExitOvershoot { get; set; }

    /// <summary>The scrim: black at 32% (light) or 56% (dark).</summary>
    public Color ScrimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SheetMaxWidth = 560;
        DialogWidth = 440;
        SheetTopInset = 20;
        DialogVerticalInset = 40;
        SheetPeek = 10;
        DialogPeek = 14;
        DepthScaleStep = 0.05;
        DepthDimStep = 0.5;
        ExitOvershoot = 24;
        ScrimColor = Oklch.ToColor(0, 0, 0, isDarkMode ? 0.56 : 0.32);
    }
}
