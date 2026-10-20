using AtomUI.Theme.DesignTokens;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Metrics shared by every <see cref="FieldTextBox"/> theme (Input, Textarea, PasswordField, SearchField, and text-based
/// special inputs): <c>{arc:FieldTextBoxTokenResource TextLineHeight}</c>.
/// </summary>
[ControlDesignToken]
internal sealed class FieldTextBoxToken : AbstractControlDesignToken
{
    /// <summary>Text line box: 14px on a 1.4 line (<c>var(--leading-body)</c>).</summary>
    public double TextLineHeight { get; set; }

    /// <summary>Text line box for Large fields: 16px on a 1.4 line.</summary>
    public double TextLineHeightLG { get; set; }

    /// <summary>Selected text wash: <c>color-mix(in oklab, var(--foreground) 16%, transparent)</c>.</summary>
    public Color SelectionFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TextLineHeight = 14 * 1.4;
        TextLineHeightLG = 16 * 1.4;
        SelectionFill = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.16);
    }
}
