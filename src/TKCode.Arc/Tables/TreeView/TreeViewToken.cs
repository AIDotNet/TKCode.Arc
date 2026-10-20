using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>TreeView geometry and the accent-independent file-type tints.</summary>
[ControlDesignToken]
internal sealed class TreeViewToken : AbstractControlDesignToken
{
    /// <summary>A row is 36px plus the 2px gap above it; both collapse together.</summary>
    public double RowSlotHeight { get; set; }
    public double RowGap { get; set; }
    public double RowRadius { get; set; }
    public double IndentWidth { get; set; }
    public double RowInlinePadding { get; set; }
    public double ContentGap { get; set; }
    public Thickness TreePadding { get; set; }
    public CornerRadius TreeCornerRadius { get; set; }

    public Color TypeScriptTint { get; set; }
    public Color StylesheetTint { get; set; }
    public Color MarkdownTint { get; set; }
    public Color JsonTint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowSlotHeight = 38;
        RowGap = 2;
        RowRadius = 10;
        IndentWidth = 18;
        RowInlinePadding = 10;
        ContentGap = 8;
        TreePadding = new Thickness(8, 6, 8, 8);
        TreeCornerRadius = new CornerRadius(16);

        // color-mix(in oklab, <hue> N%, var(--foreground))
        var foreground = ArcPalette.Neutrals(isDarkMode).Foreground;
        TypeScriptTint = Oklch.MixOklab(Color.Parse("#3178C6"), foreground, 0.22);
        StylesheetTint = Oklch.MixOklab(Color.Parse("#8B6ACE"), foreground, 0.27);
        MarkdownTint = Oklch.MixOklab(Color.Parse("#38896B"), foreground, 0.26);
        JsonTint = Oklch.MixOklab(Color.Parse("#C48637"), foreground, 0.22);
    }
}
