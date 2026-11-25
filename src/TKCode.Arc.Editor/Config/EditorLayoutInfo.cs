// Ported from vscode (src/vs/editor/common/config/editorOptions.ts: the minimap options (EditorMinimap's defaults,
// MINIMAP_GUTTER_WIDTH), ComputeOptionsMemory, RenderMinimap, OverviewRulerPosition, EditorLayoutInfo,
// EditorMinimapLayoutInfo, IMinimapLayoutInput and EditorLayoutInfoComputer, commit b57d5c9e), MIT license.
//
// Differences:
//  - the options computeLayout reads through options.get(EditorOption.x) are one record, EditorLayoutOptions, with
//    upstream's names and defaults; the environment (outer size, font, line count, pixel ratio) is
//    EditorLayoutInfoComputerEnv as upstream;
//  - string unions are enums (EditorMinimapSize, EditorMinimapSide, EditorMinimapShowSlider, EditorMinimapAutohide,
//    EditorWordWrap); accessibilitySupport is a bool (whether it is 'on');
//  - the minimap's section header options (showRegionSectionHeaders, showMarkSectionHeaders, markSectionHeaderRegex,
//    sectionHeaderFontSize, sectionHeaderLetterSpacing) are kept for the layout's equality, but nothing draws section
//    headers yet;
//  - the results are records, so two layouts compare by value (upstream's tests use deepStrictEqual).

namespace TKCode.Arc.Editor.Config;

/// <summary>How the minimap draws its lines (upstream's <c>RenderMinimap</c>).</summary>
internal enum RenderMinimap
{
    None = 0,
    Text = 1,
    Blocks = 2,
}

/// <summary>Upstream's <c>minimap.size</c>: <c>'proportional' | 'fill' | 'fit'</c>.</summary>
internal enum EditorMinimapSize
{
    /// <summary>The minimap has the same size as the editor contents (and might scroll).</summary>
    Proportional,

    /// <summary>The minimap stretches or shrinks to fill the height of the editor (no scrolling).</summary>
    Fill,

    /// <summary>The minimap shrinks as necessary to never be larger than the editor (no scrolling).</summary>
    Fit,
}

/// <summary>Upstream's <c>minimap.side</c>.</summary>
internal enum EditorMinimapSide
{
    Right,
    Left,
}

/// <summary>Upstream's <c>minimap.showSlider</c>.</summary>
internal enum EditorMinimapShowSlider
{
    /// <summary>The slider shows while the pointer is over the minimap or the editor scrolls.</summary>
    Mouseover,

    /// <summary>The slider always shows.</summary>
    Always,
}

/// <summary>Upstream's <c>minimap.autohide</c>.</summary>
internal enum EditorMinimapAutohide
{
    None,
    Mouseover,
    Scroll,
}

/// <summary>Upstream's <c>wordWrap</c>.</summary>
internal enum EditorWordWrap
{
    Off,
    On,
    WordWrapColumn,
    Bounded,
}

/// <summary>Upstream's <c>EditorMinimapOptions</c> (the validated <c>IEditorMinimapOptions</c>), with its defaults.</summary>
internal sealed record EditorMinimapOptions
{
    public static EditorMinimapOptions Default { get; } = new();

    /// <summary>Whether the minimap is shown.</summary>
    public bool Enabled { get; init; } = true;

    public EditorMinimapAutohide Autohide { get; init; } = EditorMinimapAutohide.None;

    public EditorMinimapSide Side { get; init; } = EditorMinimapSide.Right;

    public EditorMinimapSize Size { get; init; } = EditorMinimapSize.Proportional;

    public EditorMinimapShowSlider ShowSlider { get; init; } = EditorMinimapShowSlider.Mouseover;

    /// <summary>Render the actual characters on a line, as opposed to color blocks.</summary>
    public bool RenderCharacters { get; init; } = true;

    /// <summary>Limit the width of the minimap to render at most a certain number of columns.</summary>
    public int MaxColumn { get; init; } = 120;

    /// <summary>Scale of content drawn in the minimap: 1, 2 or 3.</summary>
    public int Scale { get; init; } = 1;

    public bool ShowRegionSectionHeaders { get; init; } = true;

    public bool ShowMarkSectionHeaders { get; init; } = true;

    public string MarkSectionHeaderRegex { get; init; } = @"\bMARK:\s*(?<separator>-?)\s*(?<label>.*)$";

    public double SectionHeaderFontSize { get; init; } = 9;

    public double SectionHeaderLetterSpacing { get; init; } = 1;
}

/// <summary>A description for the overview ruler position.</summary>
/// <param name="Width">Width of the overview ruler.</param>
/// <param name="Height">Height of the overview ruler.</param>
/// <param name="Top">Top position for the overview ruler.</param>
/// <param name="Right">Right position for the overview ruler.</param>
internal sealed record OverviewRulerPosition(double Width, double Height, double Top, double Right);

/// <summary>The minimap part of the editor's layout.</summary>
internal sealed record EditorMinimapLayoutInfo
{
    public RenderMinimap RenderMinimap { get; init; }

    public double MinimapLeft { get; init; }

    public double MinimapWidth { get; init; }

    public bool MinimapHeightIsEditorHeight { get; init; }

    public bool MinimapIsSampling { get; init; }

    public int MinimapScale { get; init; } = 1;

    public double MinimapLineHeight { get; init; } = 1;

    public int MinimapCanvasInnerWidth { get; init; }

    public int MinimapCanvasInnerHeight { get; init; }

    public double MinimapCanvasOuterWidth { get; init; }

    public double MinimapCanvasOuterHeight { get; init; }
}

/// <summary>The internal layout details of the editor.</summary>
internal sealed record EditorLayoutInfo
{
    /// <summary>Full editor width.</summary>
    public double Width { get; init; }

    /// <summary>Full editor height.</summary>
    public double Height { get; init; }

    /// <summary>Left position for the glyph margin.</summary>
    public double GlyphMarginLeft { get; init; }

    /// <summary>The width of the glyph margin.</summary>
    public double GlyphMarginWidth { get; init; }

    /// <summary>The number of decoration lanes to render in the glyph margin.</summary>
    public int GlyphMarginDecorationLaneCount { get; init; }

    /// <summary>Left position for the line numbers.</summary>
    public double LineNumbersLeft { get; init; }

    /// <summary>The width of the line numbers.</summary>
    public double LineNumbersWidth { get; init; }

    /// <summary>Left position for the line decorations.</summary>
    public double DecorationsLeft { get; init; }

    /// <summary>The width of the line decorations.</summary>
    public double DecorationsWidth { get; init; }

    /// <summary>Left position for the content (actual text).</summary>
    public double ContentLeft { get; init; }

    /// <summary>The width of the content (actual text).</summary>
    public double ContentWidth { get; init; }

    /// <summary>Layout information for the minimap.</summary>
    public EditorMinimapLayoutInfo Minimap { get; init; } = new();

    /// <summary>The number of columns (of typical characters) fitting on a viewport line.</summary>
    public int ViewportColumn { get; init; }

    public bool IsWordWrapMinified { get; init; }

    public bool IsViewportWrapping { get; init; }

    public int WrappingColumn { get; init; } = -1;

    /// <summary>The width of the vertical scrollbar.</summary>
    public double VerticalScrollbarWidth { get; init; }

    /// <summary>The height of the horizontal scrollbar.</summary>
    public double HorizontalScrollbarHeight { get; init; }

    /// <summary>The position of the overview ruler.</summary>
    public OverviewRulerPosition OverviewRuler { get; init; } = new(0, 0, 0, 0);
}

/// <summary>The editor options <see cref="EditorLayoutInfoComputer.ComputeLayout"/> reads, with upstream's names and defaults.</summary>
internal sealed record EditorLayoutOptions
{
    public static EditorLayoutOptions Default { get; } = new();

    public EditorWordWrap WordWrap { get; init; } = EditorWordWrap.Off;

    /// <summary>Upstream's <c>wordWrapOverride1</c>: null is <c>'inherit'</c>.</summary>
    public EditorWordWrap? WordWrapOverride1 { get; init; }

    /// <summary>Upstream's <c>wordWrapOverride2</c>: null is <c>'inherit'</c>.</summary>
    public EditorWordWrap? WordWrapOverride2 { get; init; }

    public int WordWrapColumn { get; init; } = 80;

    public bool WordWrapIndicator { get; init; }

    public bool GlyphMargin { get; init; } = true;

    /// <summary>Whether the line numbers show (upstream's <c>lineNumbers.renderType !== Off</c>).</summary>
    public bool LineNumbers { get; init; } = true;

    public int LineNumbersMinChars { get; init; } = 5;

    public double LineDecorationsWidth { get; init; } = 10;

    public bool Folding { get; init; } = true;

    /// <summary>Upstream's <c>showFoldingControls !== 'never'</c>.</summary>
    public bool ShowFoldingDecoration { get; init; } = true;

    public bool ScrollBeyondLastLine { get; init; } = true;

    public double PaddingTop { get; init; }

    public double PaddingBottom { get; init; }

    public EditorMinimapOptions Minimap { get; init; } = EditorMinimapOptions.Default;

    /// <summary>Upstream's <c>scrollbar.verticalScrollbarSize</c>.</summary>
    public double VerticalScrollbarSize { get; init; } = 14;

    /// <summary>Upstream's <c>scrollbar.horizontalScrollbarSize</c>.</summary>
    public double HorizontalScrollbarSize { get; init; } = 12;

    public bool VerticalHasArrows { get; init; }

    public double ArrowSize { get; init; } = 11;

    /// <summary>Whether upstream's <c>accessibilitySupport</c> is enabled (a screen reader is attached).</summary>
    public bool AccessibilitySupport { get; init; }
}

/// <summary>What the layout depends on besides the options.</summary>
internal sealed record EditorLayoutInfoComputerEnv
{
    public ComputeOptionsMemory? Memory { get; init; }

    public double OuterWidth { get; init; }

    public double OuterHeight { get; init; }

    public bool IsDominatedByLongLines { get; init; }

    public double LineHeight { get; init; }

    public int ViewLineCount { get; init; }

    public int LineNumbersDigitCount { get; init; }

    public double TypicalHalfwidthCharacterWidth { get; init; }

    public double MaxDigitWidth { get; init; }

    public double PixelRatio { get; init; } = 1;

    public int GlyphMarginDecorationLaneCount { get; init; } = 1;
}

/// <summary>The minimap layout's input (upstream's IMinimapLayoutInput).</summary>
internal sealed record MinimapLayoutInput
{
    public double OuterWidth { get; init; }

    public double OuterHeight { get; init; }

    public double LineHeight { get; init; }

    public double TypicalHalfwidthCharacterWidth { get; init; }

    public double PixelRatio { get; init; }

    public bool ScrollBeyondLastLine { get; init; }

    public double PaddingTop { get; init; }

    public double PaddingBottom { get; init; }

    public EditorMinimapOptions Minimap { get; init; } = EditorMinimapOptions.Default;

    public double VerticalScrollbarWidth { get; init; }

    public int ViewLineCount { get; init; }

    public double RemainingWidth { get; init; }

    public bool IsViewportWrapping { get; init; }
}

/// <summary>What the layout remembers between computations, to break the fit / fill and wrapping loop.</summary>
internal sealed class ComputeOptionsMemory
{
    public MinimapLayoutInput? StableMinimapLayoutInput { get; set; }

    public int StableFitMaxMinimapScale { get; set; }

    public double StableFitRemainingWidth { get; set; }
}

/// <summary>The result of <see cref="EditorLayoutInfoComputer.ComputeContainedMinimapLineCount"/>.</summary>
internal readonly record struct ContainedMinimapLineCount(
    double TypicalViewportLineCount,
    double ExtraLinesBeforeFirstLine,
    double ExtraLinesBeyondLastLine,
    double DesiredRatio,
    double MinimapLineCount);

/// <summary>Computes the editor's layout: gutter parts, content, minimap and overview ruler.</summary>
internal static class EditorLayoutInfoComputer
{
    /// <summary>The space left of the minimap's characters (upstream's <c>MINIMAP_GUTTER_WIDTH</c>).</summary>
    public const int MinimapGutterWidth = 8;

    public static ContainedMinimapLineCount ComputeContainedMinimapLineCount(
        int viewLineCount,
        bool scrollBeyondLastLine,
        double paddingTop,
        double paddingBottom,
        double height,
        double lineHeight,
        double pixelRatio)
    {
        var typicalViewportLineCount = height / lineHeight;
        var extraLinesBeforeFirstLine = Math.Floor(paddingTop / lineHeight);
        var extraLinesBeyondLastLine = Math.Floor(paddingBottom / lineHeight);
        if (scrollBeyondLastLine)
        {
            extraLinesBeyondLastLine = Math.Max(extraLinesBeyondLastLine, typicalViewportLineCount - 1);
        }

        var desiredRatio = (extraLinesBeforeFirstLine + viewLineCount + extraLinesBeyondLastLine) / (pixelRatio * height);
        var minimapLineCount = Math.Floor(viewLineCount / desiredRatio);
        return new ContainedMinimapLineCount(typicalViewportLineCount, extraLinesBeforeFirstLine, extraLinesBeyondLastLine, desiredRatio, minimapLineCount);
    }

    /// <summary>
    /// Computes the minimap scale in device pixels from the configured <c>editor.minimap.scale</c>. The configured scale
    /// is doubled on high DPI screens. Between a pixel ratio of 1 and 2, the scale grows with the pixel ratio. The
    /// tolerance keeps a pixel ratio slightly below a whole step at that step, instead of shrinking the minimap abruptly.
    /// </summary>
    private static int ComputeMinimapScale(int configuredScale, double pixelRatio)
    {
        if (pixelRatio >= 2)
        {
            return (int)Math.Round(configuredScale * 2.0, MidpointRounding.AwayFromZero);
        }

        return Math.Max(configuredScale, (int)Math.Floor((configuredScale * pixelRatio) + 0.125));
    }

    public static EditorMinimapLayoutInfo ComputeMinimapLayout(MinimapLayoutInput input, ComputeOptionsMemory memory)
    {
        var outerWidth = input.OuterWidth;
        var outerHeight = input.OuterHeight;
        var pixelRatio = input.PixelRatio;

        if (!input.Minimap.Enabled)
        {
            return new EditorMinimapLayoutInfo
            {
                RenderMinimap = RenderMinimap.None,
                MinimapLeft = 0,
                MinimapWidth = 0,
                MinimapHeightIsEditorHeight = false,
                MinimapIsSampling = false,
                MinimapScale = 1,
                MinimapLineHeight = 1,
                MinimapCanvasInnerWidth = 0,
                MinimapCanvasInnerHeight = (int)Math.Floor(pixelRatio * outerHeight),
                MinimapCanvasOuterWidth = 0,
                MinimapCanvasOuterHeight = outerHeight,
            };
        }

        // Can use memory if only the `viewLineCount` and `remainingWidth` have changed
        var stable = memory.StableMinimapLayoutInput;
        var couldUseMemory = stable is not null
            // && input.outerWidth === lastMinimapLayoutInput.outerWidth !!! INTENTIONAL OMITTED
            && input.OuterHeight == stable.OuterHeight
            && input.LineHeight == stable.LineHeight
            && input.TypicalHalfwidthCharacterWidth == stable.TypicalHalfwidthCharacterWidth
            && input.PixelRatio == stable.PixelRatio
            && input.ScrollBeyondLastLine == stable.ScrollBeyondLastLine
            && input.PaddingTop == stable.PaddingTop
            && input.PaddingBottom == stable.PaddingBottom
            && input.Minimap.Enabled == stable.Minimap.Enabled
            && input.Minimap.Side == stable.Minimap.Side
            && input.Minimap.Size == stable.Minimap.Size
            && input.Minimap.ShowSlider == stable.Minimap.ShowSlider
            && input.Minimap.RenderCharacters == stable.Minimap.RenderCharacters
            && input.Minimap.MaxColumn == stable.Minimap.MaxColumn
            && input.Minimap.Scale == stable.Minimap.Scale
            && input.VerticalScrollbarWidth == stable.VerticalScrollbarWidth
            // && input.viewLineCount === lastMinimapLayoutInput.viewLineCount !!! INTENTIONAL OMITTED
            // && input.remainingWidth === lastMinimapLayoutInput.remainingWidth !!! INTENTIONAL OMITTED
            && input.IsViewportWrapping == stable.IsViewportWrapping;

        var lineHeight = input.LineHeight;
        var typicalHalfwidthCharacterWidth = input.TypicalHalfwidthCharacterWidth;
        var scrollBeyondLastLine = input.ScrollBeyondLastLine;
        var minimapRenderCharacters = input.Minimap.RenderCharacters;
        var minimapScale = ComputeMinimapScale(input.Minimap.Scale, pixelRatio);
        var minimapMaxColumn = input.Minimap.MaxColumn;
        var minimapSize = input.Minimap.Size;
        var minimapSide = input.Minimap.Side;
        var verticalScrollbarWidth = input.VerticalScrollbarWidth;
        var viewLineCount = input.ViewLineCount;
        var remainingWidth = input.RemainingWidth;
        var isViewportWrapping = input.IsViewportWrapping;

        var baseCharHeight = minimapRenderCharacters ? 2 : 3;
        var minimapCanvasInnerHeight = (int)Math.Floor(pixelRatio * outerHeight);
        var minimapCanvasOuterHeight = minimapCanvasInnerHeight / pixelRatio;
        var minimapHeightIsEditorHeight = false;
        var minimapIsSampling = false;
        double minimapLineHeight = baseCharHeight * minimapScale;
        var minimapCharWidth = minimapScale / pixelRatio;
        double minimapWidthMultiplier = 1;

        if (minimapSize is EditorMinimapSize.Fill or EditorMinimapSize.Fit)
        {
            var (typicalViewportLineCount, extraLinesBeforeFirstLine, extraLinesBeyondLastLine, desiredRatio, minimapLineCount) = ComputeContainedMinimapLineCount(
                viewLineCount,
                scrollBeyondLastLine,
                input.PaddingTop,
                input.PaddingBottom,
                outerHeight,
                lineHeight,
                pixelRatio);

            // ratio is intentionally not part of the layout to avoid the layout changing all the time
            // when doing sampling
            var ratio = viewLineCount / minimapLineCount;

            if (ratio > 1)
            {
                minimapHeightIsEditorHeight = true;
                minimapIsSampling = true;
                minimapScale = 1;
                minimapLineHeight = 1;
                minimapCharWidth = minimapScale / pixelRatio;
            }
            else
            {
                var fitBecomesFill = false;
                var maxMinimapScale = minimapScale + 1;

                if (minimapSize == EditorMinimapSize.Fit)
                {
                    var effectiveMinimapHeight = Math.Ceiling((extraLinesBeforeFirstLine + viewLineCount + extraLinesBeyondLastLine) * minimapLineHeight);
                    if (isViewportWrapping && couldUseMemory && remainingWidth <= memory.StableFitRemainingWidth)
                    {
                        // There is a loop when using `fit` and viewport wrapping:
                        // - view line count impacts minimap layout
                        // - minimap layout impacts viewport width
                        // - viewport width impacts view line count
                        // To break the loop, once we go to a smaller minimap scale, we try to stick with it.
                        fitBecomesFill = true;
                        maxMinimapScale = memory.StableFitMaxMinimapScale;
                    }
                    else
                    {
                        fitBecomesFill = effectiveMinimapHeight > minimapCanvasInnerHeight;
                    }
                }

                if (minimapSize == EditorMinimapSize.Fill || fitBecomesFill)
                {
                    minimapHeightIsEditorHeight = true;
                    var configuredMinimapScale = minimapScale;
                    minimapLineHeight = Math.Min(lineHeight * pixelRatio, Math.Max(1, Math.Floor(1 / desiredRatio)));
                    if (isViewportWrapping && couldUseMemory && remainingWidth <= memory.StableFitRemainingWidth)
                    {
                        // There is a loop when using `fill` and viewport wrapping (see above).
                        // To break the loop, once we go to a smaller minimap scale, we try to stick with it.
                        maxMinimapScale = memory.StableFitMaxMinimapScale;
                    }

                    minimapScale = Math.Min(maxMinimapScale, Math.Max(1, (int)Math.Floor(minimapLineHeight / baseCharHeight)));
                    if (minimapScale > configuredMinimapScale)
                    {
                        minimapWidthMultiplier = Math.Min(2, (double)minimapScale / configuredMinimapScale);
                    }

                    minimapCharWidth = minimapScale / pixelRatio / minimapWidthMultiplier;
                    minimapCanvasInnerHeight = (int)Math.Ceiling(Math.Max(typicalViewportLineCount, extraLinesBeforeFirstLine + viewLineCount + extraLinesBeyondLastLine) * minimapLineHeight);
                    if (isViewportWrapping)
                    {
                        // remember for next time
                        memory.StableMinimapLayoutInput = input;
                        memory.StableFitRemainingWidth = remainingWidth;
                        memory.StableFitMaxMinimapScale = minimapScale;
                    }
                    else
                    {
                        memory.StableMinimapLayoutInput = null;
                        memory.StableFitRemainingWidth = 0;
                    }
                }
            }
        }

        // Given:
        // (leaving 2px for the cursor to have space after the last character)
        // viewportColumn = (contentWidth - verticalScrollbarWidth - 2) / typicalHalfwidthCharacterWidth
        // minimapWidth = viewportColumn * minimapCharWidth
        // contentWidth = remainingWidth - minimapWidth
        // What are good values for contentWidth and minimapWidth ?
        // minimapWidth = ((remainingWidth - verticalScrollbarWidth - 2) * minimapCharWidth) / (typicalHalfwidthCharacterWidth + minimapCharWidth)

        var minimapMaxWidth = Math.Floor(minimapMaxColumn * minimapCharWidth);
        var minimapWidth = Math.Min(minimapMaxWidth, Math.Max(0, Math.Floor((remainingWidth - verticalScrollbarWidth - 2) * minimapCharWidth / (typicalHalfwidthCharacterWidth + minimapCharWidth))) + MinimapGutterWidth);

        var minimapCanvasInnerWidth = (int)Math.Floor(pixelRatio * minimapWidth);
        var minimapCanvasOuterWidth = minimapCanvasInnerWidth / pixelRatio;
        minimapCanvasInnerWidth = (int)Math.Floor(minimapCanvasInnerWidth * minimapWidthMultiplier);

        var renderMinimap = minimapRenderCharacters ? RenderMinimap.Text : RenderMinimap.Blocks;
        var minimapLeft = minimapSide == EditorMinimapSide.Left ? 0 : outerWidth - minimapWidth - verticalScrollbarWidth;

        return new EditorMinimapLayoutInfo
        {
            RenderMinimap = renderMinimap,
            MinimapLeft = minimapLeft,
            MinimapWidth = minimapWidth,
            MinimapHeightIsEditorHeight = minimapHeightIsEditorHeight,
            MinimapIsSampling = minimapIsSampling,
            MinimapScale = minimapScale,
            MinimapLineHeight = minimapLineHeight,
            MinimapCanvasInnerWidth = minimapCanvasInnerWidth,
            MinimapCanvasInnerHeight = minimapCanvasInnerHeight,
            MinimapCanvasOuterWidth = minimapCanvasOuterWidth,
            MinimapCanvasOuterHeight = minimapCanvasOuterHeight,
        };
    }

    public static EditorLayoutInfo ComputeLayout(EditorLayoutOptions options, EditorLayoutInfoComputerEnv env)
    {
        var outerWidth = Math.Truncate(env.OuterWidth);
        var outerHeight = Math.Truncate(env.OuterHeight);
        var lineHeight = Math.Truncate(env.LineHeight);
        var lineNumbersDigitCount = env.LineNumbersDigitCount;
        var typicalHalfwidthCharacterWidth = env.TypicalHalfwidthCharacterWidth;
        var maxDigitWidth = env.MaxDigitWidth;
        var pixelRatio = env.PixelRatio;
        var viewLineCount = env.ViewLineCount;

        var wordWrapOverride1 = options.WordWrapOverride2 ?? options.WordWrapOverride1;
        var wordWrap = wordWrapOverride1 ?? options.WordWrap;

        var wordWrapColumn = options.WordWrapColumn;
        var wordWrapIndicator = options.WordWrapIndicator;
        var isDominatedByLongLines = env.IsDominatedByLongLines;

        var showGlyphMargin = options.GlyphMargin;
        var showLineNumbers = options.LineNumbers;
        var lineNumbersMinChars = options.LineNumbersMinChars;
        var scrollBeyondLastLine = options.ScrollBeyondLastLine;
        var minimap = options.Minimap;

        var verticalScrollbarWidth = options.VerticalScrollbarSize;
        var verticalScrollbarHasArrows = options.VerticalHasArrows;
        var scrollbarArrowSize = options.ArrowSize;
        var horizontalScrollbarHeight = options.HorizontalScrollbarSize;

        var lineDecorationsWidth = options.LineDecorationsWidth;
        if (options.Folding && options.ShowFoldingDecoration)
        {
            lineDecorationsWidth += 16;
        }

        double lineNumbersWidth = 0;
        if (showLineNumbers)
        {
            var digitCount = Math.Max(lineNumbersDigitCount, lineNumbersMinChars);
            lineNumbersWidth = Math.Round(digitCount * maxDigitWidth, MidpointRounding.AwayFromZero);
        }

        double glyphMarginWidth = 0;
        if (showGlyphMargin)
        {
            glyphMarginWidth = lineHeight * env.GlyphMarginDecorationLaneCount;
        }

        double glyphMarginLeft = 0;
        var lineNumbersLeft = glyphMarginLeft + glyphMarginWidth;
        var decorationsLeft = lineNumbersLeft + lineNumbersWidth;
        var contentLeft = decorationsLeft + lineDecorationsWidth;

        var remainingWidth = outerWidth - glyphMarginWidth - lineNumbersWidth - lineDecorationsWidth;

        var isWordWrapMinified = false;
        var isViewportWrapping = false;
        var wrappingColumn = -1;

        if (options.AccessibilitySupport && wordWrapOverride1 is null && isDominatedByLongLines)
        {
            // Force viewport width wrapping if model is dominated by long lines
            isWordWrapMinified = true;
            isViewportWrapping = true;
        }
        else if (wordWrap is EditorWordWrap.On or EditorWordWrap.Bounded)
        {
            isViewportWrapping = true;
        }
        else if (wordWrap == EditorWordWrap.WordWrapColumn)
        {
            wrappingColumn = wordWrapColumn;
        }

        var minimapLayout = ComputeMinimapLayout(new MinimapLayoutInput
        {
            OuterWidth = outerWidth,
            OuterHeight = outerHeight,
            LineHeight = lineHeight,
            TypicalHalfwidthCharacterWidth = typicalHalfwidthCharacterWidth,
            PixelRatio = pixelRatio,
            ScrollBeyondLastLine = scrollBeyondLastLine,
            PaddingTop = options.PaddingTop,
            PaddingBottom = options.PaddingBottom,
            Minimap = minimap,
            VerticalScrollbarWidth = verticalScrollbarWidth,
            ViewLineCount = viewLineCount,
            RemainingWidth = remainingWidth,
            IsViewportWrapping = isViewportWrapping,
        }, env.Memory ?? new ComputeOptionsMemory());

        if (minimapLayout.RenderMinimap != RenderMinimap.None && minimapLayout.MinimapLeft == 0)
        {
            // the minimap is rendered to the left, so move everything to the right
            glyphMarginLeft += minimapLayout.MinimapWidth;
            lineNumbersLeft += minimapLayout.MinimapWidth;
            decorationsLeft += minimapLayout.MinimapWidth;
            contentLeft += minimapLayout.MinimapWidth;
        }

        var contentWidth = remainingWidth - minimapLayout.MinimapWidth;

        // (leaving 2px for the cursor to have space after the last character)
        var viewportColumn = Math.Max(1, (int)Math.Floor((contentWidth - verticalScrollbarWidth - 2) / typicalHalfwidthCharacterWidth));

        var verticalArrowSize = verticalScrollbarHasArrows ? scrollbarArrowSize : 0;

        if (isViewportWrapping)
        {
            // compute the actual wrappingColumn
            // (leaving the rightmost column for the word wrap indicator so that it does not overlap
            // the wrapped text or end up underneath the vertical scrollbar)
            wrappingColumn = Math.Max(1, viewportColumn - (wordWrapIndicator ? 1 : 0));
            if (wordWrap == EditorWordWrap.Bounded)
            {
                wrappingColumn = Math.Min(wrappingColumn, wordWrapColumn);
            }
        }

        return new EditorLayoutInfo
        {
            Width = outerWidth,
            Height = outerHeight,
            GlyphMarginLeft = glyphMarginLeft,
            GlyphMarginWidth = glyphMarginWidth,
            GlyphMarginDecorationLaneCount = env.GlyphMarginDecorationLaneCount,
            LineNumbersLeft = lineNumbersLeft,
            LineNumbersWidth = lineNumbersWidth,
            DecorationsLeft = decorationsLeft,
            DecorationsWidth = lineDecorationsWidth,
            ContentLeft = contentLeft,
            ContentWidth = contentWidth,
            Minimap = minimapLayout,
            ViewportColumn = viewportColumn,
            IsWordWrapMinified = isWordWrapMinified,
            IsViewportWrapping = isViewportWrapping,
            WrappingColumn = wrappingColumn,
            VerticalScrollbarWidth = verticalScrollbarWidth,
            HorizontalScrollbarHeight = horizontalScrollbarHeight,
            OverviewRuler = new OverviewRulerPosition(verticalScrollbarWidth, outerHeight - (2 * verticalArrowSize), verticalArrowSize, 0),
        };
    }
}
