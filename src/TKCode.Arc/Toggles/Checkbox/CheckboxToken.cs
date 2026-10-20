using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CheckboxToken : AbstractControlDesignToken
{
    /// <summary>The drawn square (the hit area is <c>ControlSizeMD</c>).</summary>
    public double BoxSize { get; set; }
    public CornerRadius BoxRadius { get; set; }
    public double BoxRadiusValue { get; set; }
    public double MarkStrokeThickness { get; set; }

    /// <summary>The label's first line centers on the square: (44 - 14 x 1.4) / 2 = 12.2 from the top.</summary>
    public Thickness CopyMargin { get; set; }
    public double CopySpacing { get; set; }
    public double LabelLineHeight { get; set; }
    public double DescriptionLineHeight { get; set; }
    public double DisabledOpacity { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BoxSize = 18;
        BoxRadius = new CornerRadius(5);
        BoxRadiusValue = 5;
        MarkStrokeThickness = 1.75;
        CopyMargin = new Thickness(1, 12.2, 0, 0);
        CopySpacing = 2;
        LabelLineHeight = 19.6;
        DescriptionLineHeight = 16.8;
        DisabledOpacity = 0.5;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(22);
    }
}
