using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>The row cache draws what one glyph run per cell drew: same origins and advances, rebuilt only for dirty rows.</summary>
public class TerminalViewRowCacheTests
{
    private static readonly TerminalPalette s_palette =
        new(new TerminalRgb(200, 200, 200), new TerminalRgb(0, 0, 0), null, new TerminalRgb[256]);

    private static readonly ResolvedStyle s_red =
        ResolvedStyle.Default with { Foreground = TerminalColor.Rgb(255, 0, 0) };

    private static ulong Cell(int codepoint, CellWidth width = CellWidth.Narrow, int style = 0, bool grapheme = false) =>
        TCell.Pack((uint)codepoint, width, grapheme, style, SemanticContent.Output, false, false);

    private static RowSnapshot Row(ulong id, ulong[] cells, IReadOnlyDictionary<int, int[]>? graphemes = null) => new()
    {
        RowId = id,
        Version = 1,
        ContentHash = id,
        Cells = cells,
        Styles = [ResolvedStyle.Default, s_red],
        Graphemes = graphemes,
        Flags = RowFlags.None
    };

    private static RowSnapshot Text(ulong id, string text) => Row(id, text.Select(c => Cell(c)).ToArray());

    private static FrameSnapshot Frame(long generation, params RowSnapshot[] rows) => new()
    {
        Generation = generation,
        Columns = rows[0].Cells.Length,
        Rows = rows,
        Cursor = new CursorState(0, 0, false, CursorStyle.Block),
        Palette = s_palette,
        Scrollbar = new ScrollbarState(rows.Length, 0, rows.Length)
    };

    private static void Paint()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (TerminalView View, Window Window) Show(FrameSnapshot frame)
    {
        var view = new TerminalView { Snapshot = frame, GlyphOverride = static codepoint => (ushort)(codepoint & 0xFFFF) };
        var window = TestHelpers.Show(view);
        Paint();
        return (view, window);
    }

    private static double ExpectedX(TerminalView view, int column)
    {
        var m = view.Metrics;
        return (Math.Round(view.Padding * m.Scaling) + column * m.CellWidth) / m.Scaling;
    }

    [AvaloniaFact]
    public void Unchanged_Rows_Are_Not_Rebuilt_And_A_Dirty_Row_Is()
    {
        var a = Text(1, "alpha");
        var b = Text(2, "bravo");
        var (view, window) = Show(Frame(1, a, b));
        try
        {
            view.RowRunBuilds.ShouldBe(2);

            view.InvalidateVisual();
            Paint();
            view.RowRunBuilds.ShouldBe(2);

            // Scrolled: the same row objects at other positions.
            view.Snapshot = Frame(2, b, a);
            Paint();
            view.RowRunBuilds.ShouldBe(2);

            view.Snapshot = Frame(3, b, Text(3, "gamma"));
            Paint();
            view.RowRunBuilds.ShouldBe(3);
            view.CachedRowCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Run_Lays_Its_Glyphs_On_The_Grid()
    {
        var row = Text(1, "abc");
        var (view, window) = Show(Frame(1, row));
        try
        {
            var runs = view.GlyphRunsOf(row);
            runs.Count.ShouldBe(1);
            var m = view.Metrics;
            runs[0].Run.BaselineOrigin.X.ShouldBe(ExpectedX(view, 0), 1e-9);
            runs[0].Run.BaselineOrigin.Y.ShouldBe(m.BaselineY / m.Scaling, 1e-9);
            runs[0].Run.GlyphInfos.Count.ShouldBe(3);
            foreach (var info in runs[0].Run.GlyphInfos)
            {
                info.GlyphAdvance.ShouldBe(m.CellWidth / m.Scaling, 1e-9);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spaces_And_Style_Changes_Start_A_New_Run_At_Its_Own_Column()
    {
        // "ab cd": the space breaks the run; d is red.
        var row = Row(1, [Cell('a'), Cell('b'), Cell(' '), Cell('c'), Cell('d', style: 1)]);
        var (view, window) = Show(Frame(1, row));
        try
        {
            var runs = view.GlyphRunsOf(row);
            runs.Count.ShouldBe(3);
            runs[0].Run.BaselineOrigin.X.ShouldBe(ExpectedX(view, 0), 1e-9);
            runs[0].Run.GlyphInfos.Count.ShouldBe(2);
            runs[1].Run.BaselineOrigin.X.ShouldBe(ExpectedX(view, 3), 1e-9);
            runs[1].Run.GlyphInfos.Count.ShouldBe(1);
            runs[2].Run.BaselineOrigin.X.ShouldBe(ExpectedX(view, 4), 1e-9);
            runs[2].Brush.Color.ShouldBe(Color.FromRgb(255, 0, 0));
            ReferenceEquals(runs[0].Brush, runs[1].Brush).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wide_Character_Advances_Two_Cells_And_Its_Tail_Draws_Nothing()
    {
        var row = Row(1, [Cell('a'), Cell('中', CellWidth.Wide), Cell(0, CellWidth.SpacerTail), Cell('b')]);
        var (view, window) = Show(Frame(1, row));
        try
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            Columns(run).ShouldBe([ExpectedX(view, 0), ExpectedX(view, 1), ExpectedX(view, 3)], 1e-9);
            var m = view.Metrics;
            run.GlyphInfos[1].GlyphAdvance.ShouldBe(2 * m.CellWidth / m.Scaling, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Grapheme_Cluster_And_A_Surrogate_Pair_Keep_One_Glyph_Per_Cell()
    {
        // e + combining acute (a cluster: the base glyph is what the view draws), then an emoji beyond the BMP.
        var graphemes = new Dictionary<int, int[]> { [0] = [0x0301] };
        var row = Row(1, [Cell('e', grapheme: true), Cell(0x1F600, CellWidth.Wide), Cell(0, CellWidth.SpacerTail), Cell('z')], graphemes);
        var (view, window) = Show(Frame(1, row));
        try
        {
            var run = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run;
            Columns(run).ShouldBe([ExpectedX(view, 0), ExpectedX(view, 1), ExpectedX(view, 3)], 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    private static double[] Columns(GlyphRun run)
    {
        var columns = new double[run.GlyphInfos.Count];
        var x = run.BaselineOrigin.X;
        for (var i = 0; i < columns.Length; i++)
        {
            columns[i] = x;
            x += run.GlyphInfos[i].GlyphAdvance;
        }
        return columns;
    }
}
