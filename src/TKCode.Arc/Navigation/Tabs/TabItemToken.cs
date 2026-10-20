using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TabItemToken : AbstractControlDesignToken
{
    /// <summary>Arc: min-width 5.5rem.</summary>
    public double TriggerMinWidth { get; set; }

    /// <summary>Arc: padding 0 space-3.</summary>
    public Thickness InlinePadding { get; set; }

    /// <summary>Arc: radius-control − 3px.</summary>
    public CornerRadius TriggerRadius { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerMinWidth = 88;
        InlinePadding = new Thickness(12, 0);
        TriggerRadius = new CornerRadius(15);
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(19);
        DisabledOpacity = 0.5;
    }
}
