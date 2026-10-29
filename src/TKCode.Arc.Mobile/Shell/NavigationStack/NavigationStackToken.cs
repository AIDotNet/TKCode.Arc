using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>Colors of <see cref="NavigationStack"/>: the dim laid over the page underneath while another page slides over it.</summary>
[ControlDesignToken]
internal sealed class NavigationStackToken : AbstractControlDesignToken
{
    /// <summary>Color of the dim over the page underneath at full opacity, when the top page has not moved yet.</summary>
    public Color DimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DimColor = isDarkMode ? Oklch.ToColor(0, 0, 0, 0.38) : Oklch.ToColor(0.16, 0, 0, 0.14);
    }
}
