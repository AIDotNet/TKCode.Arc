// Ported from vscode (src/vs/editor/test/browser/config/editorLayoutProvider.test.ts, commit b57d5c9e), MIT license.
// The table driven cases are one test each, named as upstream; assert.deepStrictEqual compares the records.

using Shouldly;
using TKCode.Arc.Editor.Config;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'Editor ViewLayout - EditorLayoutProvider'.</summary>
public sealed class EditorLayoutProviderTests
{
    private sealed record Opts
    {
        public double OuterWidth { get; init; } = 1000;
        public double OuterHeight { get; init; } = 800;
        public bool ShowGlyphMargin { get; init; }
        public double LineHeight { get; init; } = 16;
        public bool ShowLineNumbers { get; init; }
        public int LineNumbersMinChars { get; init; }
        public int LineNumbersDigitCount { get; init; } = 1;
        public int? MaxLineNumber { get; init; }
        public double LineDecorationsWidth { get; init; } = 10;
        public double TypicalHalfwidthCharacterWidth { get; init; } = 10;
        public double MaxDigitWidth { get; init; } = 10;
        public double VerticalScrollbarWidth { get; init; }
        public bool VerticalScrollbarHasArrows { get; init; }
        public double ScrollbarArrowSize { get; init; }
        public double HorizontalScrollbarHeight { get; init; }
        public bool Minimap { get; init; }
        public EditorMinimapSide MinimapSide { get; init; } = EditorMinimapSide.Right;
        public bool MinimapRenderCharacters { get; init; } = true;
        public int MinimapMaxColumn { get; init; } = 150;
        public EditorMinimapSize MinimapSize { get; init; } = EditorMinimapSize.Proportional;
        public int MinimapScale { get; init; } = 1;
        public double PixelRatio { get; init; } = 1;
        public EditorWordWrap WordWrap { get; init; } = EditorWordWrap.Off;
        public int WordWrapColumn { get; init; } = 80;
        public bool WordWrapIndicator { get; init; }
    }

    private static EditorLayoutInfo ComputeLayoutInfo(Opts input)
    {
        var options = new EditorLayoutOptions
        {
            GlyphMargin = input.ShowGlyphMargin,
            LineNumbersMinChars = input.LineNumbersMinChars,
            LineDecorationsWidth = input.LineDecorationsWidth,
            Folding = false,
            // upstream's test never writes scrollBeyondLastLine, so it reads as undefined (false)
            ScrollBeyondLastLine = false,
            PaddingTop = 0,
            PaddingBottom = 0,
            Minimap = new EditorMinimapOptions
            {
                Enabled = input.Minimap,
                Autohide = EditorMinimapAutohide.None,
                Size = input.MinimapSize,
                Side = input.MinimapSide,
                RenderCharacters = input.MinimapRenderCharacters,
                MaxColumn = input.MinimapMaxColumn,
                ShowSlider = EditorMinimapShowSlider.Mouseover,
                Scale = input.MinimapScale,
            },
            ArrowSize = input.ScrollbarArrowSize,
            VerticalHasArrows = input.VerticalScrollbarHasArrows,
            HorizontalScrollbarSize = input.HorizontalScrollbarHeight,
            VerticalScrollbarSize = input.VerticalScrollbarWidth,
            LineNumbers = input.ShowLineNumbers,
            WordWrap = input.WordWrap,
            WordWrapColumn = input.WordWrapColumn,
            WordWrapIndicator = input.WordWrapIndicator,
            AccessibilitySupport = false,
        };

        return EditorLayoutInfoComputer.ComputeLayout(options, new EditorLayoutInfoComputerEnv
        {
            Memory = null,
            OuterWidth = input.OuterWidth,
            OuterHeight = input.OuterHeight,
            IsDominatedByLongLines = false,
            LineHeight = input.LineHeight,
            ViewLineCount = input.MaxLineNumber ?? ((int)Math.Pow(10, input.LineNumbersDigitCount) - 1),
            LineNumbersDigitCount = input.LineNumbersDigitCount,
            TypicalHalfwidthCharacterWidth = input.TypicalHalfwidthCharacterWidth,
            MaxDigitWidth = input.MaxDigitWidth,
            PixelRatio = input.PixelRatio,
            GlyphMarginDecorationLaneCount = 1,
        });
    }

    private static void DoTest(Opts input, EditorLayoutInfo expected) => ComputeLayoutInfo(input).ShouldBe(expected);

    // noMinimapExpected + noMinimapLayout
    private static EditorLayoutInfo NoMinimap(
        double width, double height, double lineNumbersWidth, double decorationsLeft, double contentLeft, double contentWidth,
        int viewportColumn, double verticalScrollbarWidth, double horizontalScrollbarHeight, OverviewRulerPosition overviewRuler) => new()
        {
            Width = width,
            Height = height,
            GlyphMarginLeft = 0,
            GlyphMarginWidth = 0,
            GlyphMarginDecorationLaneCount = 1,
            LineNumbersLeft = 0,
            LineNumbersWidth = lineNumbersWidth,
            DecorationsLeft = decorationsLeft,
            DecorationsWidth = 10,
            ContentLeft = contentLeft,
            ContentWidth = contentWidth,
            Minimap = new EditorMinimapLayoutInfo
            {
                RenderMinimap = RenderMinimap.None,
                MinimapLeft = 0,
                MinimapWidth = 0,
                MinimapHeightIsEditorHeight = false,
                MinimapIsSampling = false,
                MinimapScale = 1,
                MinimapLineHeight = 1,
                MinimapCanvasInnerWidth = 0,
                MinimapCanvasOuterWidth = 0,
                MinimapCanvasInnerHeight = (int)height,
                MinimapCanvasOuterHeight = height,
            },
            ViewportColumn = viewportColumn,
            IsWordWrapMinified = false,
            IsViewportWrapping = false,
            WrappingColumn = -1,
            VerticalScrollbarWidth = verticalScrollbarWidth,
            HorizontalScrollbarHeight = horizontalScrollbarHeight,
            OverviewRuler = overviewRuler,
        };

    [Fact]
    public void EditorLayoutProvider_1() => DoTest(
        new Opts { OuterWidth = 1000, OuterHeight = 800, ShowLineNumbers = false, LineNumbersMinChars = 0, LineNumbersDigitCount = 1 },
        NoMinimap(1000, 800, 0, 0, 10, 990, 98, 0, 0, new OverviewRulerPosition(0, 800, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_1_1() => DoTest(
        new Opts { OuterWidth = 1000, OuterHeight = 800, VerticalScrollbarWidth = 11, HorizontalScrollbarHeight = 12, ScrollbarArrowSize = 13, VerticalScrollbarHasArrows = true },
        NoMinimap(1000, 800, 0, 0, 10, 990, 97, 11, 12, new OverviewRulerPosition(11, 800 - (2 * 13), 13, 0)));

    [Fact]
    public void EditorLayoutProvider_2() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 800 },
        NoMinimap(900, 800, 0, 0, 10, 890, 88, 0, 0, new OverviewRulerPosition(0, 800, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_3() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900 },
        NoMinimap(900, 900, 0, 0, 10, 890, 88, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_4() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, LineNumbersMinChars = 5 },
        NoMinimap(900, 900, 0, 0, 10, 890, 88, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_5() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, ShowLineNumbers = true, LineNumbersMinChars = 5 },
        NoMinimap(900, 900, 50, 50, 60, 840, 83, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_6() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, ShowLineNumbers = true, LineNumbersMinChars = 5, LineNumbersDigitCount = 5 },
        NoMinimap(900, 900, 50, 50, 60, 840, 83, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_7() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, ShowLineNumbers = true, LineNumbersMinChars = 5, LineNumbersDigitCount = 6 },
        NoMinimap(900, 900, 60, 60, 70, 830, 82, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_8() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, ShowLineNumbers = true, LineNumbersMinChars = 5, LineNumbersDigitCount = 6, TypicalHalfwidthCharacterWidth = 5, MaxDigitWidth = 5 },
        NoMinimap(900, 900, 30, 30, 40, 860, 171, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    [Fact]
    public void EditorLayoutProvider_8_Rounds_Floats() => DoTest(
        new Opts { OuterWidth = 900, OuterHeight = 900, ShowLineNumbers = true, LineNumbersMinChars = 5, LineNumbersDigitCount = 6, TypicalHalfwidthCharacterWidth = 5.05, MaxDigitWidth = 5.05 },
        NoMinimap(900, 900, 30, 30, 40, 860, 169, 0, 0, new OverviewRulerPosition(0, 900, 0, 0)));

    // minimapInput: 1000 x 800, no line numbers, decorations 10, characters of 10, no scroll bars, minimap with characters, max column 150
    private static readonly Opts s_minimapInput = new() { Minimap = true, MinimapRenderCharacters = true, MinimapMaxColumn = 150 };

    // minimapExpected + minimapLayout
    private static EditorLayoutInfo WithMinimap(
        double glyphMarginLeft, double contentLeft, double contentWidth, double minimapLeft, double minimapWidth,
        bool heightIsEditorHeight, bool isSampling, int scale, double minimapLineHeight, int canvasInnerWidth,
        int canvasInnerHeight, double canvasOuterWidth, int viewportColumn) => new()
        {
            Width = 1000,
            Height = 800,
            GlyphMarginLeft = glyphMarginLeft,
            GlyphMarginWidth = 0,
            GlyphMarginDecorationLaneCount = 1,
            LineNumbersLeft = glyphMarginLeft,
            LineNumbersWidth = 0,
            DecorationsLeft = glyphMarginLeft,
            DecorationsWidth = 10,
            ContentLeft = contentLeft,
            ContentWidth = contentWidth,
            Minimap = new EditorMinimapLayoutInfo
            {
                RenderMinimap = RenderMinimap.Text,
                MinimapLeft = minimapLeft,
                MinimapWidth = minimapWidth,
                MinimapHeightIsEditorHeight = heightIsEditorHeight,
                MinimapIsSampling = isSampling,
                MinimapScale = scale,
                MinimapLineHeight = minimapLineHeight,
                MinimapCanvasInnerWidth = canvasInnerWidth,
                MinimapCanvasInnerHeight = canvasInnerHeight,
                MinimapCanvasOuterWidth = canvasOuterWidth,
                MinimapCanvasOuterHeight = 800,
            },
            ViewportColumn = viewportColumn,
            IsWordWrapMinified = false,
            IsViewportWrapping = false,
            WrappingColumn = -1,
            VerticalScrollbarWidth = 0,
            HorizontalScrollbarHeight = 0,
            OverviewRuler = new OverviewRulerPosition(0, 800, 0, 0),
        };

    [Fact]
    public void EditorLayoutProvider_9_Render_Minimap() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 1, MinimapSide = EditorMinimapSide.Right, PixelRatio = 1 },
        WithMinimap(0, 10, 893, 903, 97, false, false, 1, 2, 97, 800, 97, 89));

    [Fact]
    public void EditorLayoutProvider_9_Render_Minimap_With_PixelRatio_2() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 1, MinimapSide = EditorMinimapSide.Right, PixelRatio = 2 },
        WithMinimap(0, 10, 893, 903, 97, false, false, 2, 4, 194, 1600, 97, 89));

    [Fact]
    public void EditorLayoutProvider_9_Render_Minimap_With_PixelRatio_4() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 1, MinimapSide = EditorMinimapSide.Right, PixelRatio = 4 },
        WithMinimap(0, 10, 935, 945, 55, false, false, 2, 4, 220, 3200, 55, 93));

    [Fact]
    public void EditorLayoutProvider_10_Render_Minimap_To_Left() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 1, MinimapSide = EditorMinimapSide.Left, PixelRatio = 4 },
        WithMinimap(55, 65, 935, 0, 55, false, false, 2, 4, 220, 3200, 55, 93));

    [Fact]
    public void EditorLayoutProvider_11_Minimap_Mode_Cover_Without_Sampling() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 3, MaxLineNumber = 120, MinimapSide = EditorMinimapSide.Right, MinimapSize = EditorMinimapSize.Fill, PixelRatio = 2 },
        WithMinimap(0, 10, 893, 903, 97, true, false, 3, 13, 291, 1560, 97, 89));

    [Fact]
    public void EditorLayoutProvider_12_Minimap_Mode_Cover_With_Sampling() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 4, MaxLineNumber = 2500, MinimapSide = EditorMinimapSide.Right, MinimapSize = EditorMinimapSize.Fill, PixelRatio = 2 },
        WithMinimap(0, 10, 935, 945, 55, true, true, 1, 1, 110, 1600, 55, 93));

    [Fact]
    public void EditorLayoutProvider_13_Minimap_Mode_Contain_Without_Sampling() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 3, MaxLineNumber = 120, MinimapSide = EditorMinimapSide.Right, MinimapSize = EditorMinimapSize.Fit, PixelRatio = 2 },
        WithMinimap(0, 10, 893, 903, 97, false, false, 2, 4, 194, 1600, 97, 89));

    [Fact]
    public void EditorLayoutProvider_14_Minimap_Mode_Contain_With_Sampling() => DoTest(
        s_minimapInput with { LineNumbersDigitCount = 4, MaxLineNumber = 2500, MinimapSide = EditorMinimapSide.Right, MinimapSize = EditorMinimapSize.Fit, PixelRatio = 2 },
        WithMinimap(0, 10, 935, 945, 55, true, true, 1, 1, 110, 1600, 55, 93));

    [Fact]
    public void Issue_236999_The_Minimap_Scale_Does_Not_Jump_When_The_Pixel_Ratio_Crosses_2()
    {
        double[] pixelRatios = [1, 1.25, 1.5, 1.75, 1.96, 1.9999998, 2, 2.5];
        var actual = new[] { 1, 2, 3 }.Select(minimapScale => pixelRatios.Select(pixelRatio => ComputeLayoutInfo(s_minimapInput with
        {
            LineNumbersDigitCount = 1,
            MinimapSide = EditorMinimapSide.Right,
            MinimapScale = minimapScale,
            PixelRatio = pixelRatio,
        }).Minimap.MinimapScale).ToArray()).ToArray();

        actual.ShouldBe(new[]
        {
            new[] { 1, 1, 1, 1, 2, 2, 2, 2 },
            new[] { 2, 2, 3, 3, 4, 4, 4, 4 },
            new[] { 3, 3, 4, 5, 6, 6, 6, 6 },
        });
    }

    [Fact]
    public void Issue_31312_When_Wrapping_Leave_2px_For_The_Cursor() => DoTest(
        new Opts
        {
            OuterWidth = 1201,
            OuterHeight = 422,
            ShowGlyphMargin = true,
            LineHeight = 30,
            ShowLineNumbers = true,
            LineNumbersMinChars = 3,
            LineNumbersDigitCount = 1,
            LineDecorationsWidth = 26,
            TypicalHalfwidthCharacterWidth = 12.04296875,
            MaxDigitWidth = 12.04296875,
            VerticalScrollbarWidth = 14,
            HorizontalScrollbarHeight = 10,
            ScrollbarArrowSize = 11,
            VerticalScrollbarHasArrows = false,
            Minimap = true,
            MinimapSide = EditorMinimapSide.Right,
            MinimapRenderCharacters = true,
            MinimapMaxColumn = 120,
            PixelRatio = 2,
        },
        new EditorLayoutInfo
        {
            Width = 1201,
            Height = 422,
            GlyphMarginLeft = 0,
            GlyphMarginWidth = 30,
            GlyphMarginDecorationLaneCount = 1,
            LineNumbersLeft = 30,
            LineNumbersWidth = 36,
            DecorationsLeft = 66,
            DecorationsWidth = 26,
            ContentLeft = 92,
            ContentWidth = 1018,
            Minimap = new EditorMinimapLayoutInfo
            {
                RenderMinimap = RenderMinimap.Text,
                MinimapLeft = 1096,
                MinimapWidth = 91,
                MinimapHeightIsEditorHeight = false,
                MinimapIsSampling = false,
                MinimapScale = 2,
                MinimapLineHeight = 4,
                MinimapCanvasInnerWidth = 182,
                MinimapCanvasInnerHeight = 844,
                MinimapCanvasOuterWidth = 91,
                MinimapCanvasOuterHeight = 422,
            },
            ViewportColumn = 83,
            IsWordWrapMinified = false,
            IsViewportWrapping = false,
            WrappingColumn = -1,
            VerticalScrollbarWidth = 14,
            HorizontalScrollbarHeight = 10,
            OverviewRuler = new OverviewRulerPosition(14, 422, 0, 0),
        });

    [Fact]
    public void Leaves_The_Rightmost_Column_For_The_Word_Wrap_Indicator_When_Wrapping_To_The_Viewport()
    {
        // 990px of content at 10px per character hold 98 columns once the 2px kept free for the cursor are taken off.
        // The indicator occupies the rightmost column, so wrapped text needs to stop one column earlier.
        (int ViewportColumn, int WrappingColumn) WrappingOf(EditorWordWrap wordWrap, bool wordWrapIndicator, int wordWrapColumn = 40)
        {
            var layoutInfo = ComputeLayoutInfo(new Opts
            {
                OuterWidth = 1000,
                OuterHeight = 800,
                Minimap = false,
                WordWrap = wordWrap,
                WordWrapColumn = wordWrapColumn,
                WordWrapIndicator = wordWrapIndicator,
            });
            return (layoutInfo.ViewportColumn, layoutInfo.WrappingColumn);
        }

        WrappingOf(EditorWordWrap.Off, true).ShouldBe((98, -1));
        WrappingOf(EditorWordWrap.On, false).ShouldBe((98, 98));
        WrappingOf(EditorWordWrap.On, true).ShouldBe((98, 97));
        WrappingOf(EditorWordWrap.Bounded, false).ShouldBe((98, 40));
        WrappingOf(EditorWordWrap.Bounded, true).ShouldBe((98, 40));
        WrappingOf(EditorWordWrap.WordWrapColumn, true).ShouldBe((98, 40));
        WrappingOf(EditorWordWrap.WordWrapColumn, true, 120).ShouldBe((98, 120));
    }
}
