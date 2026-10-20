using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PasswordStrengthToken : AbstractControlDesignToken
{
    /// <summary>Arc: <c>padding: 0 5px 0 var(--space-4)</c>, <c>gap: var(--space-1)</c>.</summary>
    public Thickness FramePadding { get; set; }
    public double FrameSpacing { get; set; }

    /// <summary>34px toggle, corners <c>calc(var(--radius-control) - 5px)</c>, pressed to 0.94.</summary>
    public double RevealButtonSize { get; set; }
    public CornerRadius RevealButtonRadius { get; set; }
    public double RevealPressScale { get; set; }

    /// <summary>Meter row: 12px below the field (or its error), segments 4px tall with 5px gaps.</summary>
    public Thickness MeterRowMargin { get; set; }
    public double SegmentHeight { get; set; }
    public double SegmentGap { get; set; }

    /// <summary>The strength word's fixed slot: 4.5rem, 12px from the meter, 1.4em tall.</summary>
    public double WordWidth { get; set; }
    public Thickness WordMargin { get; set; }
    public double WordLineHeight { get; set; }

    /// <summary>Requirement list: 16px below the meter, 6px between rules.</summary>
    public Thickness RulesMargin { get; set; }
    public double RulesSpacing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FramePadding = new Thickness(16, 0, 5, 0);
        FrameSpacing = 4;
        RevealButtonSize = 34;
        RevealButtonRadius = new CornerRadius(13);
        RevealPressScale = 0.94;
        MeterRowMargin = new Thickness(0, 12, 0, 0);
        SegmentHeight = 4;
        SegmentGap = 5;
        WordWidth = 72;
        WordMargin = new Thickness(12, 0, 0, 0);
        WordLineHeight = 14 * 1.4;
        RulesMargin = new Thickness(0, 16, 0, 0);
        RulesSpacing = 6;
    }
}
