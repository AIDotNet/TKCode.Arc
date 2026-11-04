namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ArcWindowToken : AbstractControlDesignToken
{
    /// <summary>The narrowest window: room for the title and the caption buttons side by side.</summary>
    public double MinWidth { get; set; }

    /// <summary>The shortest window: the title bar and some content under it.</summary>
    public double MinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MinWidth = 360;
        MinHeight = 240;
    }
}
