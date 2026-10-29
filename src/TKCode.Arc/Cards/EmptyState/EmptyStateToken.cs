using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class EmptyStateToken : AbstractControlDesignToken
{
    /// <summary>Icon block (space-12 square).</summary>
    public double IconBlockSize { get; set; }

    /// <summary>Space above the title (space-5) and the description (space-2).</summary>
    public Thickness TitleMargin { get; set; }

    public Thickness DescriptionMargin { get; set; }

    /// <summary>Descriptions wrap at 18rem.</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>Space above the action (space-5).</summary>
    public Thickness ActionMargin { get; set; }

    /// <summary>Horizontal padding (space-5).</summary>
    public double InlinePadding { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        IconBlockSize = 48;
        TitleMargin = new Thickness(0, 20, 0, 0);
        DescriptionMargin = new Thickness(0, 8, 0, 0);
        DescriptionMaxWidth = 288;
        ActionMargin = new Thickness(0, 20, 0, 0);
        InlinePadding = 20;
        TitleLineHeight = 16 * 1.4;
        DescriptionLineHeight = 14 * 1.4;
    }
}
