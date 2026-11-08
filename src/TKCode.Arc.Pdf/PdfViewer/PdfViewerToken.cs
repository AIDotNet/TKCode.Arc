using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PdfViewer"/> (design/components/pdf/pdf.md section 7). The fixed colors go over the white paper of
/// a document, not over the theme, so they do not follow the accent or the appearance.
/// </summary>
[ControlDesignToken]
internal sealed class PdfViewerToken : AbstractControlDesignToken
{
    /// <summary>Width of the sidebar.</summary>
    public double SidebarWidth { get; set; }

    /// <summary>Width of a thumbnail's page.</summary>
    public double ThumbnailWidth { get; set; }

    /// <summary>Gap between thumbnails.</summary>
    public double ThumbnailSpacing { get; set; }

    /// <summary>Width of the password form.</summary>
    public double PasswordFormWidth { get; set; }

    /// <summary>Width of the search bar.</summary>
    public double SearchBarWidth { get; set; }

    /// <summary>All search results, over the paper.</summary>
    public Color SearchHighlight { get; set; }

    /// <summary>The current search result, over the paper.</summary>
    public Color SearchHighlightCurrent { get; set; }

    /// <summary>A link under the pointer, over the paper.</summary>
    public Color LinkHoverFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SidebarWidth = 224;
        ThumbnailWidth = 120;
        ThumbnailSpacing = 12;
        PasswordFormWidth = 320;
        SearchBarWidth = 360;
        SearchHighlight = Color.FromArgb(0x66, 0xFF, 0xD6, 0x0A);
        SearchHighlightCurrent = Color.FromArgb(0x99, 0xFF, 0x95, 0x00);
        LinkHoverFill = Color.FromArgb(0x14, 0x00, 0x00, 0x00);
    }
}
