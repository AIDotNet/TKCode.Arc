namespace TKCode.Arc;

/// <summary>
/// Tokens of a <see cref="ToggleGroupItem"/> of its own: the press its content gives. The option's size, insets, icon
/// sizes and focus ring stay in <c>ToggleGroupToken</c>, where theme overrides already address them.
/// </summary>
[ControlDesignToken]
internal sealed class ToggleGroupItemToken : AbstractControlDesignToken
{
    /// <summary>The scale the content shrinks to while the option is pressed (Arc: 0.92).</summary>
    public double PressScale { get; set; }

    /// <summary>How long the content takes to shrink (standard easing).</summary>
    public TimeSpan PressDownDuration { get; set; }

    /// <summary>How long the content takes to come back (spring easing).</summary>
    public TimeSpan PressUpDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PressScale = 0.92;
        PressDownDuration = TimeSpan.FromMilliseconds(90);
        PressUpDuration = TimeSpan.FromMilliseconds(320);
    }
}
