using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Page"/>: it has no fixed size, only the space it keeps around the content slot.</summary>
[ControlDesignToken]
internal sealed class PageToken : AbstractControlDesignToken
{
    /// <summary>Padding around the content slot: 0.</summary>
    public Thickness ContentInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentInset = new Thickness(0);
    }
}
