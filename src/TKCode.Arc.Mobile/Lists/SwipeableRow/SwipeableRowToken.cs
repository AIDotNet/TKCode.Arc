namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="SwipeableRow"/>: 76px action buttons, 20px icons, 4px icon-to-label gap and disabled opacity.</summary>
[ControlDesignToken]
internal sealed class SwipeableRowToken : AbstractControlDesignToken
{
    /// <summary>Width of each revealed action button: 76.</summary>
    public double ActionWidth { get; set; }

    /// <summary>Width and height of an action's icon glyph: 20.</summary>
    public double IconExtent { get; set; }

    /// <summary>Vertical space between an action's icon and its label: 4.</summary>
    public double IconGap { get; set; }

    /// <summary>Opacity of a disabled action button: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ActionWidth = 76;
        IconExtent = 20;
        IconGap = 4;
        DisabledOpacity = 0.5;
    }
}
