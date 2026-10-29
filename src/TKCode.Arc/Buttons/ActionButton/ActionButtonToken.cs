using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ActionButtonToken : AbstractControlDesignToken
{
    /// <summary>Inline padding (Arc <c>0 var(--space-5)</c>).</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Gap between the label and the icon slot.</summary>
    public double LabelIconGap { get; set; }

    /// <summary>The 17px icon slot that holds the arrow, spinner and check.</summary>
    public double IconSlotExtent { get; set; }

    /// <summary>Disabled (not pending) opacity.</summary>
    public double DisabledOpacity { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePadding = new Thickness(20, 0);
        LabelIconGap = 8;
        IconSlotExtent = 17;
        DisabledOpacity = 0.7;
        FocusRingRadius = new CornerRadius(22);
    }
}
