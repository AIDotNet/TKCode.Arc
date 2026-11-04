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
    public double ThumbOnOffset { get; set; }
    public double ThumbOnPressedOffset { get; set; }
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
        ThumbOnOffset = 18;
        ThumbOnPressedOffset = 13;
        LabelMargin = new Thickness(12, 0, 0, 0);
        DisabledOpacity = 0.5;
        FocusRingMargin = new Thickness(-4);
    }
}
