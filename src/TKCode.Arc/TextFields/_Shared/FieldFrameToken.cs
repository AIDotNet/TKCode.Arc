using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FieldFrameToken : AbstractControlDesignToken
{
    /// <summary>Inline padding per size (Arc input: <c>0 var(--space-3)</c> at the default size).</summary>
    public Thickness InlinePaddingSM { get; set; }
    public Thickness InlinePaddingMD { get; set; }
    public Thickness InlinePaddingLG { get; set; }

    /// <summary>Space between prefix / suffix and the editor (<c>gap: var(--space-2)</c>).</summary>
    public double AffixSpacing { get; set; }

    /// <summary>Focus halo: <c>box-shadow: 0 0 0 6px transparent</c> at rest, <c>0 0 0 3px var(--focus-ring)</c> when focused.</summary>
    public Thickness FocusRingRestMargin { get; set; }
    public Thickness FocusRingRestThickness { get; set; }
    public Thickness FocusRingMargin { get; set; }
    public Thickness FocusRingThickness { get; set; }
    public CornerRadius FocusRingRadius { get; set; }

    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePaddingSM = new Thickness(10, 0);
        InlinePaddingMD = new Thickness(12, 0);
        InlinePaddingLG = new Thickness(14, 0);
        AffixSpacing = 8;
        FocusRingRestMargin = new Thickness(-6);
        FocusRingRestThickness = new Thickness(6);
        FocusRingMargin = new Thickness(-3);
        FocusRingThickness = new Thickness(3);
        FocusRingRadius = new CornerRadius(21);
        DisabledOpacity = 0.5;
    }
}
