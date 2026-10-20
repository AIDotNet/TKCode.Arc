using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="FilterToolbar"/> (values from Arc's filter-toolbar.module.css).</summary>
[ControlDesignToken]
internal sealed class FilterToolbarToken : AbstractControlDesignToken
{
    public Thickness ToolbarInset { get; set; }
    public double SectionGap { get; set; }
    public double ChipGap { get; set; }
    public double ActionGap { get; set; }
    public Thickness ChipInset { get; set; }
    public double ChipInnerGap { get; set; }
    public double RemoveExtent { get; set; }
    public Thickness ClearInset { get; set; }
    public double SearchWidth { get; set; }
    public Thickness SearchInset { get; set; }

    /// <summary>Below this width the toolbar stacks: chips, then the actions aligned to the end.</summary>
    public double StackBreakpoint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ToolbarInset = new Thickness(16, 12);
        SectionGap = 16;
        ChipGap = 8;
        ActionGap = 12;
        ChipInset = new Thickness(12, 0, 8, 0);
        ChipInnerGap = 8;
        RemoveExtent = 24;
        ClearInset = new Thickness(12, 0);
        SearchWidth = 200;
        SearchInset = new Thickness(12, 0);
        StackBreakpoint = 520;
    }
}
