using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of a <see cref="RibbonGroup"/> (self-designed): commands in a 78px band (three 24px rows), 2px between columns,
/// an 11px name in a 20px row under them, 4px padding and a 1px line at the end of the group.
/// </summary>
[ControlDesignToken]
internal sealed class RibbonGroupToken : AbstractControlDesignToken
{
    /// <summary>Height of the band the commands sit in: 78 (three rows of 24 and their gaps).</summary>
    public double ContentHeight { get; set; }

    /// <summary>Space between columns of commands: 2.</summary>
    public double ColumnSpacing { get; set; }

    /// <summary>Padding of the group: 4 at the sides and top.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Height of the row with the group's name: 20.</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Size of the group's name: 11.</summary>
    public double HeaderFontSize { get; set; }

    /// <summary>Space around the group's name in the drop-down of a collapsed group: 4 above it.</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>The line at the end of a group: 1 on the right.</summary>
    public Thickness SeparatorThickness { get; set; }

    /// <summary>Padding of the drop-down of a collapsed group: 6, 2 at the bottom.</summary>
    public Thickness DropDownPadding { get; set; }

    /// <summary>Glyph of the launcher button: 12.</summary>
    public double LauncherGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentHeight = 78;
        ColumnSpacing = 2;
        Padding = new Thickness(4, 4, 4, 0);
        HeaderHeight = 20;
        HeaderFontSize = 11;
        HeaderMargin = new Thickness(0, 4, 0, 0);
        SeparatorThickness = new Thickness(0, 0, 1, 0);
        DropDownPadding = new Thickness(6, 6, 6, 2);
        LauncherGlyphSize = 12;
    }
}
