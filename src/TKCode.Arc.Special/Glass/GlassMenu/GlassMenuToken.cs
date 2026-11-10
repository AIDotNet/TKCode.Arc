using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GlassMenu"/>: its inner space, width and the gap to the anchor it opens from. The material comes
/// from <see cref="GlassPaneToken"/>; corners, shadows and colors from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class GlassMenuToken : AbstractControlDesignToken
{
    /// <summary>Space between the glass's edge and the items.</summary>
    public Thickness MenuPadding { get; set; }

    /// <summary>Narrowest the menu gets.</summary>
    public double MenuMinWidth { get; set; }

    /// <summary>Gap between the anchor and a menu opened from it.</summary>
    public double AnchorGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MenuPadding = new Thickness(6);
        MenuMinWidth = 220;
        AnchorGap = 8;
    }
}

/// <summary>Geometry of one <see cref="GlassMenuItem"/>: its height, inner space, icon and shortcut.</summary>
[ControlDesignToken]
internal sealed class GlassMenuItemToken : AbstractControlDesignToken
{
    /// <summary>Height of an item.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Space inside an item.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>Size of an item's icon.</summary>
    public double IconSize { get; set; }

    /// <summary>Space after the icon, before the label.</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Space before the shortcut hint.</summary>
    public Thickness ShortcutMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 36;
        ItemPadding = new Thickness(10, 0);
        IconSize = 16;
        IconMargin = new Thickness(0, 0, 10, 0);
        ShortcutMargin = new Thickness(16, 0, 0, 0);
    }
}

/// <summary>Geometry of a <see cref="GlassMenuSeparator"/>.</summary>
[ControlDesignToken]
internal sealed class GlassMenuSeparatorToken : AbstractControlDesignToken
{
    /// <summary>Space around the line.</summary>
    public Thickness SeparatorMargin { get; set; }

    /// <summary>Thickness of the line.</summary>
    public double LineThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SeparatorMargin = new Thickness(10, 5);
        LineThickness = 1;
    }
}
