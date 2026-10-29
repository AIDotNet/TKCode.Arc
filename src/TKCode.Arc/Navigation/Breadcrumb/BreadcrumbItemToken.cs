using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BreadcrumbItemToken : AbstractControlDesignToken
{
    /// <summary>Arc: padding 4px 2px.</summary>
    public Thickness CrumbPadding { get; set; }

    /// <summary>Arc: border-radius 7px.</summary>
    public CornerRadius CrumbRadius { get; set; }

    /// <summary>Gap between the chevron and the label (Arc: 6px).</summary>
    public double SeparatorGap { get; set; }

    /// <summary>Arc: 16px chevron, stroke 1.75.</summary>
    public double SeparatorSize { get; set; }

    /// <summary>text-sm × leading-body.</summary>
    public double LabelLineHeight { get; set; }

    /// <summary>Hover underline: color-mix(currentColor 45%).</summary>
    public double UnderlineOpacity { get; set; }

    public Thickness FocusRingMargin { get; set; }

    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CrumbPadding = new Thickness(2, 4);
        CrumbRadius = new CornerRadius(7);
        SeparatorGap = 6;
        SeparatorSize = 16;
        LabelLineHeight = 14 * 1.4;
        UnderlineOpacity = 0.45;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(11);
    }
}
