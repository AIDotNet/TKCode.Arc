using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="CodeEditor"/> that follows neither the theme nor the accent.</summary>
[ControlDesignToken]
public sealed class CodeEditorToken : AbstractControlDesignToken
{
    /// <summary>The line height as a multiple of the font size (Monaco's golden ratio on Linux and Windows, 1.35).</summary>
    public double LineHeightRatio { get; set; }

    /// <summary>The width of the text cursor in insert mode, in pixels (Monaco's default cursorWidth, 2).</summary>
    public double CursorWidth { get; set; }

    /// <summary>The space between the line numbers and the text, in pixels.</summary>
    public double GutterPadding { get; set; }

    /// <summary>The space left of the line numbers, in pixels.</summary>
    public double GutterLeadingPadding { get; set; }

    /// <summary>The smallest height of the editor.</summary>
    public double MinHeight { get; set; }

    /// <summary>The width of an indent guide, in pixels.</summary>
    public double IndentGuideWidth { get; set; }

    /// <summary>The widest a hover, parameter hint or problem widget gets, in pixels (Monaco's 500).</summary>
    public double HoverMaxWidth { get; set; }

    /// <summary>The tallest a hover or parameter hint widget gets before it scrolls, in pixels.</summary>
    public double HoverMaxHeight { get; set; }

    /// <summary>The padding of a row of a hover, parameter hint or problem widget.</summary>
    public Thickness HoverRowPadding { get; set; }

    /// <summary>The space inside the color picker card of color decorators.</summary>
    public Thickness ColorPickerPadding { get; set; }

    /// <summary>The width of the color picker card of color decorators.</summary>
    public double ColorPickerWidth { get; set; }

    /// <summary>The height of the line under the sticky scroll lines, in pixels.</summary>
    public double StickyScrollBorderThickness { get; set; }

    /// <summary>The width of the overview ruler at the right edge, beside the minimap (Monaco's vertical scroll bar size, 14).</summary>
    public double OverviewRulerWidth { get; set; }

    /// <summary>The most columns the minimap draws (Monaco's <c>minimap.maxColumn</c>, 120); at one pixel a column, its widest.</summary>
    public double MinimapMaxColumn { get; set; }

    /// <summary>The opacity of the minimap slider at rest.</summary>
    public double MinimapSliderOpacity { get; set; }

    /// <summary>The opacity of the minimap slider under the pointer.</summary>
    public double MinimapSliderHoverOpacity { get; set; }

    /// <summary>The opacity of the minimap slider while it is dragged.</summary>
    public double MinimapSliderActiveOpacity { get; set; }

    /// <summary>The width of the folding column in the gutter, where the chevrons are, in pixels.</summary>
    public double FoldingWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineHeightRatio = 1.35;
        CursorWidth = 2;
        GutterPadding = 16;
        GutterLeadingPadding = 12;
        IndentGuideWidth = 1;
        MinHeight = 80;
        HoverMaxWidth = 500;
        HoverMaxHeight = 320;
        HoverRowPadding = new Thickness(8, 4);
        ColorPickerPadding = new Thickness(8);
        ColorPickerWidth = 260;
        StickyScrollBorderThickness = 1;
        OverviewRulerWidth = 14;
        MinimapMaxColumn = 120;
        MinimapSliderOpacity = 0.12;
        MinimapSliderHoverOpacity = 0.18;
        MinimapSliderActiveOpacity = 0.26;
        FoldingWidth = 18;
    }
}
