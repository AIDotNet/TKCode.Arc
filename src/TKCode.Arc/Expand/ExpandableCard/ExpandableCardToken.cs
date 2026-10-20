using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ExpandableCardToken : AbstractControlDesignToken
{
    /// <summary>Arc: the whole header is the trigger, at least 64px tall.</summary>
    public double TriggerMinHeight { get; set; }

    /// <summary>Arc: padding space-4 space-5.</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Gap between the copy and the chevron (Arc: space-5).</summary>
    public Thickness ChevronMargin { get; set; }

    public double ChevronSize { get; set; }

    /// <summary>Gap between the title and the summary (Arc: 4px).</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Arc: padding space-4 space-5 space-5.</summary>
    public Thickness DetailsPadding { get; set; }

    /// <summary>The divider is an inset line, so a closed panel is truly zero height.</summary>
    public Thickness DetailsDivider { get; set; }

    public double DetailsLineHeight { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerMinHeight = 64;
        TriggerPadding = new Thickness(20, 16);
        ChevronMargin = new Thickness(20, 0, 0, 0);
        ChevronSize = 18;
        DescriptionMargin = new Thickness(0, 4, 0, 0);
        DetailsPadding = new Thickness(20, 16, 20, 20);
        DetailsDivider = new Thickness(0, 1, 0, 0);
        DetailsLineHeight = 14 * 1.4;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(38);
    }
}
