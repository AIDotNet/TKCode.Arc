using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="DocsSidebar"/>'s header.</summary>
[ControlDesignToken]
internal sealed class DocsSidebarToken : AbstractControlDesignToken
{
    /// <summary>Side of the product mark beside the title.</summary>
    public double MarkSize { get; set; }

    /// <summary>Space inside the version switcher.</summary>
    public Thickness VersionPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MarkSize = 22;
        VersionPadding = new Thickness(10, 0, 8, 0);
    }
}
