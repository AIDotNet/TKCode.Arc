using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DocumentExtraction"/>, measured from the document extraction of the AI group (design reference
/// only, ADR 0015; spec section 3). Colors, radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DocumentExtractionToken : AbstractControlDesignToken
{
    /// <summary>The widest the block grows (1080).</summary>
    public double MaxWidth { get; set; }

    /// <summary>The height of the two-pane body in the side-by-side layout (600).</summary>
    public double BodyHeight { get; set; }

    /// <summary>The widest the form pane grows (436) in the side-by-side layout.</summary>
    public double FormPaneWidth { get; set; }

    /// <summary>The form pane's share of the width (0.45).</summary>
    public double FormPaneShare { get; set; }

    /// <summary>Below this container width the panes stack (760).</summary>
    public double StackBreakpoint { get; set; }

    /// <summary>Below this container width the footer wraps (700).</summary>
    public double FooterWrapBreakpoint { get; set; }

    /// <summary>Below this container width the layout is narrow (520).</summary>
    public double NarrowBreakpoint { get; set; }

    /// <summary>The viewer's height when stacked (379).</summary>
    public double StackedViewerHeight { get; set; }

    /// <summary>The form's height when stacked (478).</summary>
    public double StackedFormHeight { get; set; }

    /// <summary>The toolbar and the form head (48).</summary>
    public double BarHeight { get; set; }

    /// <summary>The footer (60).</summary>
    public double FooterHeight { get; set; }

    /// <summary>Space inside the footer (16 left, 10 elsewhere).</summary>
    public Thickness FooterPadding { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 1080;
        BodyHeight = 600;
        FormPaneWidth = 436;
        FormPaneShare = 0.45;
        StackBreakpoint = 760;
        FooterWrapBreakpoint = 700;
        NarrowBreakpoint = 520;
        StackedViewerHeight = 379;
        StackedFormHeight = 478;
        BarHeight = 48;
        FooterHeight = 60;
        FooterPadding = new Thickness(16, 10, 10, 10);
    }
}
