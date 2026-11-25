// Ported from vscode (src/vs/editor/test/browser/view/minimapLayout.test.ts, commit b57d5c9e), MIT license.
// The deepStrictEqual of an object of summaries is one ShouldBe per entry.

using Shouldly;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewParts;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'MinimapLayout'.</summary>
public sealed class MinimapLayoutTests
{
    private const int LineCount = 1000;
    private const double LineHeight = 20;
    private const double ViewportHeight = 400;
    private const double MinimapHeight = 400;

    private sealed record Options(
        double PixelRatio,
        double MinimapLineHeight,
        int CanvasInnerHeight,
        double LineHeight,
        bool MinimapHeightIsEditorHeight,
        double MinimapHeight,
        double PaddingTop,
        double PaddingBottom,
        bool ScrollBeyondLastLine) : IMinimapLayoutOptions;

    private readonly record struct SliderSummary(int BackwardMoves, double MinSliderTop, double MaxSliderBottom);

    /// <summary>
    /// Scrolls down through the layout, creating the minimap layout like Minimap.render does, and summarizes how the
    /// slider moved over the document (the slider's document line is tracked rather than its top).
    /// </summary>
    private static SliderSummary ScrollDown(LinesLayout linesLayout, double paddingTop, double maxScrollTop)
    {
        const double minimapLineHeight = 2;
        var options = new Options(1, minimapLineHeight, (int)MinimapHeight, LineHeight, false, MinimapHeight, paddingTop, 0, false);
        var scrollHeight = linesLayout.GetLinesTotalHeight();
        MinimapLayout? previousLayout = null;
        double previousSliderLine = 0;
        var backwardMoves = 0;
        var minSliderTop = double.MaxValue;
        double maxSliderBottom = 0;
        for (double scrollTop = 0; scrollTop <= maxScrollTop; scrollTop += 10)
        {
            var viewport = linesLayout.GetLinesViewportData(scrollTop, scrollTop + ViewportHeight);
            var layout = MinimapLayout.Create(
                options,
                viewport.StartLineNumber,
                viewport.EndLineNumber,
                linesLayout.GetVerticalOffsetForLineNumber(viewport.StartLineNumber),
                linesLayout.GetLineHeightForLineNumber(viewport.StartLineNumber),
                ViewportHeight,
                linesLayout.GetWhitespaceViewportData(scrollTop, scrollTop + ViewportHeight).Length > 0,
                LineCount,
                LineCount,
                scrollTop,
                scrollHeight,
                previousLayout);
            var sliderLine = layout.StartLineNumber - layout.TopPaddingLineCount + (layout.SliderTop / minimapLineHeight);
            if (previousLayout is not null && sliderLine < previousSliderLine - 1e-9)
            {
                backwardMoves++;
            }

            minSliderTop = Math.Min(minSliderTop, layout.SliderTop);
            maxSliderBottom = Math.Max(maxSliderBottom, Math.Round(layout.SliderTop + layout.SliderHeight, MidpointRounding.AwayFromZero));
            previousLayout = layout;
            previousSliderLine = sliderLine;
        }

        return new SliderSummary(backwardMoves, minSliderTop, maxSliderBottom);
    }

    private static LinesLayout CreateLinesLayout(double paddingTop, CustomLineHeightData[] customLineHeights, (int AfterLineNumber, double Height)[] viewZones)
    {
        var linesLayout = new LinesLayout(LineCount, LineHeight, paddingTop, 0, customLineHeights);
        linesLayout.ChangeWhitespace(accessor =>
        {
            foreach (var (afterLineNumber, height) in viewZones)
            {
                accessor.InsertWhitespace(afterLineNumber, 0, height, 0);
            }
        });
        return linesLayout;
    }

    private static SliderSummary ScrollAll(LinesLayout layout, double paddingTop) =>
        ScrollDown(layout, paddingTop, layout.GetLinesTotalHeight() - ViewportHeight);

    [Fact]
    public void Slider_Moves_Only_Forward_When_Scrolling_Down_Through_View_Zones_Padding_Taller_And_Zero_Height_Lines()
    {
        var noZones = CreateLinesLayout(0, [], []);
        var tallZone = CreateLinesLayout(0, [], [(100, 3000)]);
        var manyZones = CreateLinesLayout(0, [], [.. Enumerable.Range(0, 99).Select(i => ((i + 1) * 10, 72.0))]);
        var paddingAndTallerLines = CreateLinesLayout(30, [new CustomLineHeightData("a", 50, 50, 300), new CustomLineHeightData("b", 400, 400, 120)], [(200, 500), (600, 1000)]);
        var trailingZone = CreateLinesLayout(0, [], [(LineCount, 3000)]);
        // A custom line height of 0.2px rounds to 0px, so line 101 is the first visible line while the zone above it scrolls by
        var zeroHeightLineAfterZone = CreateLinesLayout(0, [new CustomLineHeightData("z", 101, 101, 0.2)], [(100, 3000)]);

        ScrollAll(noZones, 0).ShouldBe(new SliderSummary(0, 0, 401));
        ScrollAll(tallZone, 0).ShouldBe(new SliderSummary(0, 0, 401));
        ScrollAll(manyZones, 0).ShouldBe(new SliderSummary(0, 0, 401));
        ScrollAll(paddingAndTallerLines, 30).ShouldBe(new SliderSummary(0, 0, 401));
        ScrollAll(trailingZone, 0).ShouldBe(new SliderSummary(0, 0, 362));
        ScrollAll(zeroHeightLineAfterZone, 0).ShouldBe(new SliderSummary(0, 0, 401));
    }

    /// <summary>Creates the minimap layout like Minimap.render does in fit / fill mode.</summary>
    private static MinimapLayout CreateFitLayout(LinesLayout linesLayout, double paddingTop, double scrollTop)
    {
        var options = new Options(1, 2, (int)MinimapHeight, LineHeight, true, MinimapHeight, paddingTop, 0, false);
        var viewport = linesLayout.GetLinesViewportData(scrollTop, scrollTop + ViewportHeight);
        var zoneFree = MinimapLayout.GetZoneFreeScrollPosition(MinimapLineOffsets.From(linesLayout), LineCount, LineHeight, paddingTop, viewport.StartLineNumber, ViewportHeight, scrollTop, linesLayout.GetLinesTotalHeight());
        return MinimapLayout.Create(
            options,
            viewport.StartLineNumber,
            viewport.EndLineNumber,
            linesLayout.GetVerticalOffsetForLineNumber(viewport.StartLineNumber),
            linesLayout.GetLineHeightForLineNumber(viewport.StartLineNumber),
            ViewportHeight,
            linesLayout.GetWhitespaceViewportData(scrollTop, scrollTop + ViewportHeight).Length > 0,
            LineCount,
            LineCount,
            zoneFree.ScrollTop,
            zoneFree.ScrollHeight,
            null);
    }

    /// <summary>
    /// Scrolls the top and the middle of every line to the top of the viewport, compares the slider with the slider of
    /// the same document without view zones and taller lines, and maps the slider back to the editor scrollTop.
    /// </summary>
    private static (double MaxDrift, int RoundTripMismatches) Compare(LinesLayout linesLayout, LinesLayout plainLinesLayout, double paddingTop)
    {
        var scrollHeight = linesLayout.GetLinesTotalHeight();
        double maxDrift = 0;
        var roundTripMismatches = 0;
        for (var lineNumber = 1; lineNumber <= LineCount; lineNumber++)
        {
            foreach (var fraction in new[] { 0, 0.5 })
            {
                var scrollTop = linesLayout.GetVerticalOffsetForLineNumber(lineNumber) + (fraction * linesLayout.GetLineHeightForLineNumber(lineNumber));
                if (scrollTop > scrollHeight - ViewportHeight)
                {
                    continue;
                }

                var layout = CreateFitLayout(linesLayout, paddingTop, scrollTop);
                var plainLayout = CreateFitLayout(plainLinesLayout, paddingTop, plainLinesLayout.GetVerticalOffsetForLineNumber(lineNumber) + (fraction * LineHeight));
                maxDrift = Math.Max(maxDrift, Math.Abs(layout.SliderTop - plainLayout.SliderTop));
                if (MinimapLayout.GetScrollTopFromZoneFreeScrollTop(MinimapLineOffsets.From(linesLayout), LineCount, LineHeight, paddingTop, ViewportHeight, layout.ScrollTop, scrollHeight) != scrollTop)
                {
                    roundTripMismatches++;
                }
            }
        }

        return (maxDrift, roundTripMismatches);
    }

    [Fact]
    public void Fit_Fill_Slider_Follows_The_Lines_Not_The_View_Zones_Padding_And_Taller_Lines()
    {
        var tallZone = CreateLinesLayout(0, [], [(100, 3000)]);
        var paddingTallerLinesAndZones = CreateLinesLayout(30, [new CustomLineHeightData("a", 50, 50, 300), new CustomLineHeightData("b", 400, 400, 120)], [(200, 500), (600, 1000)]);

        Compare(tallZone, CreateLinesLayout(0, [], []), 0).ShouldBe((0, 0));
        Compare(paddingTallerLinesAndZones, CreateLinesLayout(30, [], []), 30).ShouldBe((0, 0));
        // The zone between lines 100 and 101 spans scrollTop 2000 to 5000
        new[] { 2000.0, 3000, 4000, 4990, 5000 }.Select(scrollTop => CreateFitLayout(tallZone, 0, scrollTop).SliderTop).Distinct().ShouldBe([40]);
        CreateFitLayout(tallZone, 0, tallZone.GetLinesTotalHeight() - ViewportHeight).SliderTop.ShouldBe(392);
    }

    [Fact]
    public void Fit_Fill_Slider_Position_Does_Not_Map_Back_To_Editor_Positions_Inside_A_View_Zone_Or_Past_The_Last_Lines()
    {
        static (double ZoneFreeScrollTop, double ScrollTop) RoundTrip(LinesLayout linesLayout, double scrollTop)
        {
            var scrollHeight = linesLayout.GetLinesTotalHeight();
            var viewport = linesLayout.GetLinesViewportData(scrollTop, scrollTop + ViewportHeight);
            var offsets = MinimapLineOffsets.From(linesLayout);
            var zoneFreeScrollTop = MinimapLayout.GetZoneFreeScrollPosition(offsets, LineCount, LineHeight, 0, viewport.StartLineNumber, ViewportHeight, scrollTop, scrollHeight).ScrollTop;
            return (zoneFreeScrollTop, MinimapLayout.GetScrollTopFromZoneFreeScrollTop(offsets, LineCount, LineHeight, 0, ViewportHeight, zoneFreeScrollTop, scrollHeight));
        }

        // The zone between lines 100 and 101 spans scrollTop 2000 to 5000
        var tallZone = CreateLinesLayout(0, [], [(100, 3000)]);
        // The zone between lines 990 and 991 spans scrollTop 19800 to 22800, the editor scrolls to 22600 and the lines alone to 19600
        var zoneNearEnd = CreateLinesLayout(0, [], [(990, 3000)]);

        RoundTrip(tallZone, 3000).ShouldBe((2000, 5000));
        new[] { 19600.0, 21000, 22600 }.Select(scrollTop => RoundTrip(zoneNearEnd, scrollTop)).ShouldBe([(19600, 22600), (19600, 22600), (19600, 22600)]);
        // Dragging the slider down past the lines still reaches the end of the editor
        MinimapLayout.GetScrollTopFromZoneFreeScrollTop(MinimapLineOffsets.From(zoneNearEnd), LineCount, LineHeight, 0, ViewportHeight, 19700, zoneNearEnd.GetLinesTotalHeight()).ShouldBe(22600);
    }
}
