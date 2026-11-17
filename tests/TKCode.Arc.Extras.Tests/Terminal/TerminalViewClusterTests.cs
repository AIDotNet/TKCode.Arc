using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;
using static TKCode.Arc.Tests.Terminal.TerminalTestKit;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Issue #141: grapheme clusters are shaped and keep the grid; emoji get their own runs sized to their cells.</summary>
public class TerminalViewClusterTests
{
    private static double Advance(GlyphRun run) => run.GlyphInfos.Sum(i => i.GlyphAdvance);

    private static double CellW(TerminalView view) => view.Metrics.CellWidth / view.Metrics.Scaling;

    [AvaloniaFact]
    public void A_Combining_Mark_Is_Shaped_Into_Its_Cell_And_The_Next_Cell_Stays_On_The_Grid()
    {
        // é as e + U+0301, then z.
        var row = Row([Cell('e', grapheme: true), Cell('z')], graphemes: new Dictionary<int, int[]> { [0] = [0x0301] });
        With(Frame(row), view =>
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            run.GlyphInfos.Count.ShouldBe(3);
            // The cluster's advances add up to one cell, so z starts exactly at column 1.
            (run.GlyphInfos[0].GlyphAdvance + run.GlyphInfos[1].GlyphAdvance).ShouldBe(CellW(view), 1e-9);
            run.GlyphInfos[2].GlyphAdvance.ShouldBe(CellW(view), 1e-9);
            run.GlyphInfos[2].GlyphCluster.ShouldBe(2);
            view.ShapeCalls.ShouldBe(1);
        });
    }

    [AvaloniaTheory]
    [InlineData(new[] { 0x1F469, 0x200D, 0x1F4BB })] // 👩‍💻
    [InlineData(new[] { 0x1F1E8, 0x1F1F3 })] // 🇨🇳
    [InlineData(new[] { 0x2764, 0xFE0F })] // ❤️
    [InlineData(new[] { 0x1F44D, 0x1F3FD })] // 👍🏽
    public void An_Emoji_Cluster_Gets_Its_Own_Run_Within_Its_Two_Cells(int[] codepoints)
    {
        var row = Row([Cell('a'), Cell(codepoints[0], CellWidth.Wide, grapheme: true), Cell(0, CellWidth.SpacerTail), Cell('b')],
            graphemes: new Dictionary<int, int[]> { [1] = codepoints[1..] });
        With(Frame(row), view =>
        {
            var runs = view.GlyphRunsOf(row);
            runs.Count.ShouldBe(3);
            var emoji = runs[1].Run;
            emoji.Characters.ToString().ShouldBe(string.Concat(codepoints.Select(char.ConvertFromUtf32)));
            emoji.BaselineOrigin.X.ShouldBeGreaterThanOrEqualTo(ColumnX(view, 1) - 1e-9);
            (emoji.BaselineOrigin.X + Advance(emoji)).ShouldBeLessThanOrEqualTo(ColumnX(view, 3) + 1e-9);
            runs[2].Run.BaselineOrigin.X.ShouldBe(ColumnX(view, 3), 1e-9);
        });
    }

    [AvaloniaFact]
    public void A_Single_Emoji_Without_A_Cluster_Is_Sized_To_Its_Cells_Too()
    {
        var row = Row([Cell(0x1F600, CellWidth.Wide), Cell(0, CellWidth.SpacerTail)]);
        With(Frame(row), view =>
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            Advance(run).ShouldBeLessThanOrEqualTo(2 * CellW(view) + 1e-9);
        });
    }

    [AvaloniaFact]
    public void Vs15_Keeps_Text_Presentation_In_The_Text_Run()
    {
        // ❤︎ (U+2764 U+FE0E) next to a letter: one run, the cluster one cell wide.
        var row = Row([Cell('a'), Cell(0x2764, grapheme: true), Cell('b')], graphemes: new Dictionary<int, int[]> { [1] = [0xFE0E] });
        With(Frame(row), view =>
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            Advance(run).ShouldBe(3 * CellW(view), 1e-9);
        });
    }

    [AvaloniaFact]
    public void Thai_Marks_Stay_With_Their_Base()
    {
        // ก็ : ก + U+0E47
        var row = Row([Cell(0x0E01, grapheme: true), Cell('x')], graphemes: new Dictionary<int, int[]> { [0] = [0x0E47] });
        With(Frame(row), view =>
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            run.Characters.ToString().ShouldBe("ก็x");
            run.GlyphInfos[^1].GlyphCluster.ShouldBe(2);
        });
    }

    [AvaloniaFact]
    public void Ascii_Never_Goes_Through_The_Shaper()
    {
        var row = Text("plain ascii, CJK 中 and box ─");
        With(Frame(row), view => view.ShapeCalls.ShouldBe(0));
    }

    [AvaloniaFact]
    public void Shaped_Clusters_Are_Cached()
    {
        var graphemes = new Dictionary<int, int[]> { [0] = [0x0301], [1] = [0x0301] };
        var row = Row([Cell('e', grapheme: true), Cell('e', grapheme: true)], graphemes: graphemes);
        With(Frame(row), view => view.ShapeCalls.ShouldBe(1));
    }
}
