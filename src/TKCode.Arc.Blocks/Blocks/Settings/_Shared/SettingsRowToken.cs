using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SettingsRow"/>.</summary>
[ControlDesignToken]
internal sealed class SettingsRowToken : AbstractControlDesignToken
{
    /// <summary>Space around the row's content (vertical only: the group supplies the inline inset).</summary>
    public Thickness RowInset { get; set; }

    /// <summary>Gap after the icon tile.</summary>
    public Thickness IconTileMargin { get; set; }

    /// <summary>Gap before the control when it sits beside the text.</summary>
    public Thickness ControlMargin { get; set; }

    /// <summary>Gap above the control once it sits under the text.</summary>
    public Thickness StackedControlMargin { get; set; }

    /// <summary>Gap between the header and the description.</summary>
    public double TextGap { get; set; }

    /// <summary>The icon tile's side.</summary>
    public double IconTile { get; set; }

    /// <summary>The icon glyph's side inside the tile.</summary>
    public double IconGlyph { get; set; }

    public CornerRadius IconTileRadius { get; set; }

    /// <summary>Row width below which the control moves under the text.</summary>
    public double StackThreshold { get; set; }

    /// <summary>Line height of the header and description (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowInset = new Thickness(0, 16);
        IconTileMargin = new Thickness(0, 0, 16, 0);
        ControlMargin = new Thickness(16, 0, 0, 0);
        StackedControlMargin = new Thickness(0, 12, 0, 0);
        TextGap = 2;
        IconTile = 34;
        IconGlyph = 17;
        IconTileRadius = new CornerRadius(10);
        StackThreshold = 440;
        BodyLineHeight = 14 * 1.4;
    }
}
