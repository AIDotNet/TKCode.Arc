namespace TKCode.Arc;

/// <summary>Default geometry and motion of <see cref="CyclicPanel"/>.</summary>
[ControlDesignToken]
internal sealed class CyclicPanelToken : AbstractControlDesignToken
{
    /// <summary>Gap between neighbouring items (none: the slots tile the viewport).</summary>
    public double Spacing { get; set; }

    /// <summary>Length of an animated scroll (<c>--duration-standard</c>, 240ms).</summary>
    public TimeSpan ScrollDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Spacing = 0;
        ScrollDuration = TimeSpan.FromMilliseconds(240);
    }
}
