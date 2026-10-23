using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ModelPicker"/>'s stage, measured from the model picker preview (design reference only, ADR 0015).</summary>
[ControlDesignToken]
internal sealed class ModelPickerToken : AbstractControlDesignToken
{
    /// <summary>The widest the stage (pill and summary) grows: min(440, available).</summary>
    public double StageMaxWidth { get; set; }

    /// <summary>Space between the pill and the summary (grid row gap 24).</summary>
    public double StageGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StageMaxWidth = 440;
        StageGap = 24;
    }
}
