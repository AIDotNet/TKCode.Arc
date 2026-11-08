using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class EnumSelectorToken : AbstractControlDesignToken
{
    /// <summary>Space under the header (the legend), as in <see cref="RadioGroup"/>.</summary>
    public Thickness LegendMargin { get; set; }

    /// <summary>Gap between rows of radio options (bordered rows, as in <see cref="RadioGroup"/>).</summary>
    public double RadioRowSpacing { get; set; }

    /// <summary>Gap between rows of checkboxes; their 44px hit areas already space them.</summary>
    public double CheckRowSpacing { get; set; }

    /// <summary>Gap between columns.</summary>
    public double ColumnSpacing { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LegendMargin = new Thickness(0, 0, 0, 8);
        RadioRowSpacing = 8;
        CheckRowSpacing = 0;
        ColumnSpacing = 16;
    }
}
