using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="QuickAccessToolBar"/>: small commands 2px apart, 2px padding.</summary>
[ControlDesignToken]
internal sealed class QuickAccessToolBarToken : AbstractControlDesignToken
{
    /// <summary>Space between commands: 2.</summary>
    public double ItemSpacing { get; set; }

    /// <summary>Padding of the toolbar: 2.</summary>
    public Thickness Padding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemSpacing = 2;
        Padding = new Thickness(2);
    }
}
