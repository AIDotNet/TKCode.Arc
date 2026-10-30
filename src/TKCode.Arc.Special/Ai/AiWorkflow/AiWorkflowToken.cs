using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the <see cref="AiWorkflow"/> card: its widest width, the padding of the header, the step list and the
/// footer (tighter under 480px), the header's type sizes and the add step menu's width. Colors, radii, fonts and shadows
/// come from the ArcDesign tokens in the theme; the step rows' grid (mark 20, gap 10, status slot 72 / 64, rows 32 / 44)
/// lives in <see cref="AiWorkflowMetrics"/>.
/// </summary>
[ControlDesignToken]
internal sealed class AiWorkflowToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (640); it fills its container below that.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Header padding: 16 14 14 12 (14 12 narrow).</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness NarrowHeaderPadding { get; set; }

    /// <summary>Space around the steps: 16 10 (12 8 narrow).</summary>
    public Thickness BodyPadding { get; set; }

    public Thickness NarrowBodyPadding { get; set; }

    /// <summary>Footer padding under its hairline: 10 8 (6 8 narrow).</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness NarrowFooterPadding { get; set; }

    /// <summary>The workflow's name: 14 / 20.</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    /// <summary>The line of context under the name: 12 / 18.</summary>
    public double DescriptionFontSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The run summary in the footer: 12.</summary>
    public double SummaryFontSize { get; set; }

    /// <summary>The add step menu: 300 wide, its list at most 288 high.</summary>
    public double MenuWidth { get; set; }

    public double MenuMaxListHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        HeaderPadding = new Thickness(16, 14, 14, 12);
        NarrowHeaderPadding = new Thickness(14, 12, 12, 12);
        BodyPadding = new Thickness(16, 10);
        NarrowBodyPadding = new Thickness(12, 8);
        FooterPadding = new Thickness(10, 8);
        NarrowFooterPadding = new Thickness(6, 8);
        TitleFontSize = 14;
        TitleLineHeight = 20;
        DescriptionFontSize = 12;
        DescriptionLineHeight = 18;
        SummaryFontSize = 12;
        MenuWidth = 300;
        MenuMaxListHeight = 288;
    }
}

/// <summary>The step rows' grid and the rail's geometry (code-built parts).</summary>
internal static class AiWorkflowMetrics
{
    /// <summary>Below this card width rows grow to 44px and stack their detail under the title.</summary>
    public const double NarrowWidth = 480;

    public const double RowHeight = 32;

    public const double NarrowRowHeight = 44;

    public const double MarkSize = 20;

    public const double MarkGlyphSize = 12;

    /// <summary>Space between the mark and the title (the body below lines up with the title).</summary>
    public const double MarkGap = 10;

    public const double BodyIndent = MarkSize + MarkGap;

    /// <summary>
    /// The card's spring: output previews, errors and added or removed steps open and close on it, the path switch's height
    /// follows on it and the tab and menu highlights slide on it (visual duration 0.22s, no bounce).
    /// </summary>
    public static readonly SpringSpec Spring = SpringSpec.FromVisual(0.22, 0);

    public const double StatusWidth = 72;

    public const double NarrowStatusWidth = 64;

    public const double RemoveSize = 24;

    public const double TitleFontSize = 13;

    public const double TitleLineHeight = 18;

    public const double OutputFontSize = 12;

    public const double OutputLineHeight = 18;

    /// <summary>The traveling dot.</summary>
    public const double DotSize = 5;

    /// <summary>The rail's hairline.</summary>
    public const double RailWidth = 1;
}
