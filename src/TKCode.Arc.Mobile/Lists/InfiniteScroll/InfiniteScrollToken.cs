namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="InfiniteScroll"/>: bottom status footer height, spinner size and gap.</summary>
[ControlDesignToken]
internal sealed class InfiniteScrollToken : AbstractControlDesignToken
{
    /// <summary>Minimum height of the loading/finished/retry footer (at least 44px for retry taps): 52.</summary>
    public double FooterMinHeight { get; set; }

    /// <summary>Diameter of the loading spinner: 18.</summary>
    public double SpinnerSize { get; set; }

    /// <summary>Stroke thickness of the loading spinner: 2.</summary>
    public double SpinnerThickness { get; set; }

    /// <summary>Gap between the spinner and the status text: 8.</summary>
    public double ContentGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        FooterMinHeight = 52;
        SpinnerSize = 18;
        SpinnerThickness = 2;
        ContentGap = 8;
    }
}
