using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SwitchToken : AbstractControlDesignToken
{
    public double TrackWidth { get; set; }
    public double TrackHeight { get; set; }
    public Thickness TrackInset { get; set; }
    public double ThumbSize { get; set; }
    public double ThumbStretchedWidth { get; set; }
    public Thickness ThumbOnMargin { get; set; }
    public Thickness ThumbOnPressedMargin { get; set; }
    public Thickness LabelMargin { get; set; }
    public double DisabledOpacity { get; set; }
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        // Track inner width (42 - 2 x 3 padding) minus the 18px thumb = 18px travel; pressed thumb widens by 5px.
        TrackWidth = 42;
        TrackHeight = 24;
        TrackInset = new Thickness(3);
        ThumbSize = 18;
        ThumbStretchedWidth = 23;
        ThumbOnMargin = new Thickness(18, 0, 0, 0);
        ThumbOnPressedMargin = new Thickness(13, 0, 0, 0);
        LabelMargin = new Thickness(12, 0, 0, 0);
        DisabledOpacity = 0.5;
        FocusRingMargin = new Thickness(-4);
    }
}
