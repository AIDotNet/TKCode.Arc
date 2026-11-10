using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SelectionToolbar"/>.</summary>
[ControlDesignToken]
internal sealed class SelectionToolbarToken : AbstractControlDesignToken
{
    /// <summary>Space between the bar and the bottom edge of the content.</summary>
    public Thickness BarMargin { get; set; }

    /// <summary>Space inside the bar.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Space around the count text.</summary>
    public Thickness CountPadding { get; set; }

    /// <summary>How far the bar rises as it enters.</summary>
    public double EnterOffset { get; set; }

    /// <summary>Below this width the actions show only their icons (with tooltips).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Height of the separators between the count, the actions and the clear button.</summary>
    public double SeparatorHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarMargin = new Thickness(16, 0, 16, 18);
        BarPadding = new Thickness(6);
        CountPadding = new Thickness(10, 0, 6, 0);
        EnterOffset = 16;
        CompactWidth = 560;
        SeparatorHeight = 20;
    }
}
