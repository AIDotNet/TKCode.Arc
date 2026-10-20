using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ReorderableList"/> (values from Arc's reorderable-list.module.css).</summary>
[ControlDesignToken]
internal sealed class ReorderableListToken : AbstractControlDesignToken
{
    /// <summary>Gap between rows.</summary>
    public double RowGap { get; set; }

    public double RowMinHeight { get; set; }

    /// <summary>Row padding: 7px around the handle, 16px after the position.</summary>
    public Thickness RowInset { get; set; }

    /// <summary>A 40px handle in a 56px row sits 7px from the edges, so its corner follows the row's.</summary>
    public double HandleExtent { get; set; }

    public CornerRadius HandleRadius { get; set; }

    public double HandleGlyphExtent { get; set; }

    public double CopyGap { get; set; }

    /// <summary>Space between the handle, the copy and the position (gap 12).</summary>
    public Thickness CopyInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowGap = 8;
        RowMinHeight = 56;
        RowInset = new Thickness(7, 7, 16, 7);
        HandleExtent = 40;
        HandleRadius = new CornerRadius(11);
        HandleGlyphExtent = 16;
        CopyGap = 2;
        CopyInset = new Thickness(12, 0);
    }
}
