using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FieldMessageToken : AbstractControlDesignToken
{
    /// <summary>Space above the copy, inside the clipped row (<c>padding-top: var(--space-2)</c>).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>12px copy on a 1.4 line (<c>var(--text-xs)</c>, <c>var(--leading-body)</c>).</summary>
    public double TextLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowPadding = new Thickness(0, 8, 0, 0);
        TextLineHeight = 12 * 1.4;
    }
}
