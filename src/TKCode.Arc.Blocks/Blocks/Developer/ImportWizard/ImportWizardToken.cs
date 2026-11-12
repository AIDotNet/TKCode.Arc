using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ImportWizard"/> (Arc Import wizard, design reference, ADR 0045) and the accent-independent
/// colors it derives from the neutrals. Colors that follow the accent, radii, the type ramp and the fonts come from
/// ArcDesign tokens. Paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class ImportWizardToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (760).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Below this width of the card the compact layout is used (520).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding of the step bar (24, 24, 24, 0) and its compact form (16, 16, 16, 0).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Padding of a step (24) and its compact form (16).</summary>
    public Thickness StepPadding { get; set; }

    public Thickness CompactStepPadding { get; set; }

    /// <summary>Space between a step's heading, body and footer (20).</summary>
    public double StepSpacing { get; set; }

    /// <summary>Height of the drop zone (180).</summary>
    public double DropZoneHeight { get; set; }

    /// <summary>Width of the confidence cell (92) and its compact form (84).</summary>
    public double ConfidenceWidth { get; set; }

    public double CompactConfidenceWidth { get; set; }

    /// <summary>Width of the progress bar of the import (360).</summary>
    public double ProgressWidth { get; set; }

    /// <summary>Width of the summary of the import (420).</summary>
    public double SummaryWidth { get; set; }

    /// <summary>The widest a review cell grows (220).</summary>
    public double CellMaxWidth { get; set; }

    /// <summary>Text of the selected filter and the clean-up button: L .91 in the dark, .24 in the light.</summary>
    public Color StrongText { get; set; }

    /// <summary>The "likely" confidence level: a neutral at L .80 in the dark, .33 in the light.</summary>
    public Color LikelyColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 760;
        CompactWidth = 520;
        HeaderPadding = new Thickness(24, 24, 24, 0);
        CompactHeaderPadding = new Thickness(16, 16, 16, 0);
        StepPadding = new Thickness(24);
        CompactStepPadding = new Thickness(16);
        StepSpacing = 20;
        DropZoneHeight = 180;
        ConfidenceWidth = 92;
        CompactConfidenceWidth = 84;
        ProgressWidth = 360;
        SummaryWidth = 420;
        CellMaxWidth = 220;
        StrongText = Oklch.ToColor(isDarkMode ? 0.91 : 0.24, 0, 0);
        LikelyColor = Oklch.ToColor(isDarkMode ? 0.80 : 0.33, 0, 0);
    }
}
