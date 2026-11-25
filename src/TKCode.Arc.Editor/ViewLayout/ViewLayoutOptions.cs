// The editor options that ViewLayout (viewLayout.ts, vscode commit b57d5c9e, MIT license) reads through
// options.get(EditorOption.x), as one immutable record with upstream's option names and defaults. Upstream reads them
// from the editor configuration (IEditorConfiguration); here a host supplies them through IViewLayoutConfiguration.
// Config/EditorOptions.cs (the cursor's options) is left as it is; the two meet when the editor configuration is ported.
//
// Only the members ViewLayout reads are here:
//  - layoutInfo: contentWidth, height and verticalScrollbarWidth of upstream's EditorLayoutInfo (computed upstream;
//    the default is an editor of size 0 with the default vertical scrollbar, 14 pixels);
//  - lineHeight: upstream computes it from the font (0 = golden ratio times the font size); the default is that value
//    for the default font size (BareFontInfo);
//  - padding (top, bottom), scrollBeyondLastLine, scrollBeyondLastColumn, smoothScrolling: upstream's defaults;
//  - scrollbar: horizontal, horizontalScrollbarSize and ignoreHorizontalScrollbarInContentHeight, upstream's defaults;
//  - wrappingInfo.isViewportWrapping (computed upstream from wordWrap; false is wordWrap 'off', the default);
//  - fontInfo.typicalHalfwidthCharacterWidth (measured upstream; the default, 10, is what upstream's TestConfiguration
//    reports);
//  - minimap.enabled and minimap.side (upstream's defaults: true, 'right').

using TKCode.Arc.Editor.Config;

namespace TKCode.Arc.Editor.ViewLayout;

/// <summary>Where a <see cref="ViewLayout"/> reads its options (upstream's IEditorConfiguration.options).</summary>
internal interface IViewLayoutConfiguration
{
    ViewLayoutOptions Options { get; }
}

/// <summary>The editor options a <see cref="ViewLayout"/> reads, with upstream's names and defaults.</summary>
internal sealed record ViewLayoutOptions
{
    /// <summary>The sizes of the editor's layout (upstream's computed layoutInfo).</summary>
    public LayoutInfoOptions LayoutInfo { get; init; } = new(0, 0, 14);

    /// <summary>The height of a line in pixels.</summary>
    public double LineHeight { get; init; } = DefaultLineHeight();

    /// <summary>Space above the first line and below the last line.</summary>
    public PaddingOptions Padding { get; init; } = new(0, 0);

    /// <summary>Whether the editor scrolls beyond the last line.</summary>
    public bool ScrollBeyondLastLine { get; init; } = true;

    /// <summary>The number of extra characters beyond which the editor scrolls horizontally.</summary>
    public int ScrollBeyondLastColumn { get; init; } = 4;

    /// <summary>Whether the editor scrolls with an animation.</summary>
    public bool SmoothScrolling { get; init; }

    /// <summary>The scrollbar options ViewLayout reads.</summary>
    public ScrollbarOptions Scrollbar { get; init; } = new(ScrollbarVisibility.Auto, 12, false);

    /// <summary>Upstream's computed wrappingInfo.</summary>
    public WrappingInfoOptions WrappingInfo { get; init; } = new(false);

    /// <summary>The measured font widths ViewLayout reads.</summary>
    public FontInfoOptions FontInfo { get; init; } = new(10);

    /// <summary>The minimap options ViewLayout reads.</summary>
    public MinimapOptions Minimap { get; init; } = new(true, "right");

    private static double DefaultLineHeight() =>
        Math.Round(BareFontInfo.GoldenLineHeightRatio * BareFontInfo.DefaultFontSize, MidpointRounding.AwayFromZero);

    /// <param name="ContentWidth">The width of the content area (where the text is).</param>
    /// <param name="Height">The height of the editor.</param>
    /// <param name="VerticalScrollbarWidth">The width of the vertical scrollbar.</param>
    internal sealed record LayoutInfoOptions(double ContentWidth, double Height, double VerticalScrollbarWidth);

    internal sealed record PaddingOptions(double Top, double Bottom);

    internal sealed record ScrollbarOptions(ScrollbarVisibility Horizontal, double HorizontalScrollbarSize, bool IgnoreHorizontalScrollbarInContentHeight);

    /// <param name="IsViewportWrapping">Whether lines wrap at the viewport width.</param>
    internal sealed record WrappingInfoOptions(bool IsViewportWrapping);

    internal sealed record FontInfoOptions(double TypicalHalfwidthCharacterWidth);

    /// <param name="Enabled">Whether the minimap is shown.</param>
    /// <param name="Side">Upstream's <c>'left' | 'right'</c>.</param>
    internal sealed record MinimapOptions(bool Enabled, string Side);
}
