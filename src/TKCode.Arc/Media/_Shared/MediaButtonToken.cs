using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MediaButtonToken : AbstractControlDesignToken
{
    /// <summary>Wide buttons: 0 16px (Arc card stack <c>padding: 0 var(--space-4)</c>).</summary>
    public Thickness WideInlinePadding { get; set; }

    /// <summary>Wide buttons on narrow stacks: 0 12px.</summary>
    public Thickness CompactInlinePadding { get; set; }

    /// <summary>Gap between glyph and label (8px).</summary>
    public double ContentGap { get; set; }

    public double RoundGlyphSize { get; set; }

    public double WideGlyphSize { get; set; }

    /// <summary>Focus ring: 2px wide, 2px outside the button.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Wide buttons: control radius 18 + 4.</summary>
    public CornerRadius WideFocusRingRadius { get; set; }

    /// <summary>aria-disabled opacity of the round carousel controls (0.5).</summary>
    public double RoundDisabledOpacity { get; set; }

    /// <summary>aria-disabled opacity of the card stack controls (0.42).</summary>
    public double WideDisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        WideInlinePadding = new Thickness(16, 0);
        CompactInlinePadding = new Thickness(12, 0);
        ContentGap = 8;
        RoundGlyphSize = 18;
        WideGlyphSize = 16;
        FocusRingMargin = new Thickness(-4);
        WideFocusRingRadius = new CornerRadius(22);
        RoundDisabledOpacity = 0.5;
        WideDisabledOpacity = 0.42;
    }
}
