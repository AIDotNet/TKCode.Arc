using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RadioGroupToken : AbstractControlDesignToken
{
    public Thickness LegendMargin { get; set; }
    public double RowGap { get; set; }
    public Thickness RowInset { get; set; }
    public double MarkGap { get; set; }
    public double RingDiameter { get; set; }
    public CornerRadius RingRadius { get; set; }
    public double DotDiameter { get; set; }
    public CornerRadius DotRadius { get; set; }
    public double PreviewOpacity { get; set; }
    public Thickness DescriptionMargin { get; set; }
    public double LabelLineHeight { get; set; }
    public double DescriptionLineHeight { get; set; }

    /// <summary>The highlight's stroke sits half a pixel inside the row so it covers the row's 1px border.</summary>
    public CornerRadius HighlightRadius { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public CornerRadius FocusRingRadius { get; set; }
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LegendMargin = new Thickness(0, 0, 0, 8);
        RowGap = 8;
        RowInset = new Thickness(12);
        MarkGap = 12;
        // 17px ring; a 7px dot stays on whole pixels inside the 15px interior.
        RingDiameter = 17;
        RingRadius = new CornerRadius(8.5);
        DotDiameter = 7;
        DotRadius = new CornerRadius(3.5);
        PreviewOpacity = 0.22;
        DescriptionMargin = new Thickness(0, 2, 0, 0);
        LabelLineHeight = 19.6;
        DescriptionLineHeight = 16.8;
        HighlightRadius = new CornerRadius(17.5);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(22);
        DisabledOpacity = 0.5;
    }
}
