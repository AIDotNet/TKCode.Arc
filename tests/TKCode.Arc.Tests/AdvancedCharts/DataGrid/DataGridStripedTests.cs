using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>IsStriped on DataGrid: every second row line, by shown order; checked rows stay on top.</summary>
public class DataGridStripedTests
{
    private static ObservableCollection<DataGridRow> Many(int count) =>
        new(Enumerable.Range(0, count).Select(i => new DataGridRow($"r{i}") { { "account", $"Account {i}" }, { "region", "Europe" }, { "arr", (decimal)(count - i) }, { "health", 50m }, { "renewal", null } }));

    private static void ShouldAlternate(DataGrid grid)
    {
        var palette = grid.Palette;
        palette.StripeCellBrush.ShouldNotBe(palette.CellBrush);
        for (var line = 0; line < grid.LineCount; line++)
        {
            var odd = (line & 1) == 1;
            grid.IsStripedLine(line).ShouldBe(odd);
            palette.CellFill(grid.IsStripedLine(line), false).ShouldBe(odd ? palette.StripeCellBrush : palette.CellBrush);
            palette.GutterFill(grid.IsStripedLine(line), false).ShouldBe(odd ? palette.StripeGutterBrush : palette.SurfaceBrush);
        }
    }

    [AvaloniaFact]
    public void Is_Off_By_Default()
    {
        var grid = DataGridBehaviorTests.Create();
        grid.IsStriped.ShouldBeFalse();
        var window = DataGridBehaviorTests.Show(grid);
        try
        {
            for (var line = 0; line < grid.LineCount; line++)
            {
                grid.IsStripedLine(line).ShouldBeFalse();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Odd_Lines_Are_Striped_And_The_Tint_Differs_From_The_Cell_Fill()
    {
        var grid = DataGridBehaviorTests.Create();
        grid.IsStriped = true;
        var window = DataGridBehaviorTests.Show(grid);
        try
        {
            ShouldAlternate(grid);
            grid.IsStriped = false;
            grid.IsStripedLine(1).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Follow_Sort_Insert_And_Remove()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.IsStriped = true;
        var window = DataGridBehaviorTests.Show(grid);
        try
        {
            grid.ApplySort([DataGridSort.Descending("arr")]);
            Dispatcher.UIThread.RunJobs();
            ShouldAlternate(grid);
            rows.Insert(0, new("rx") { { "account", "New" }, { "region", "Europe" }, { "arr", 1m }, { "health", 1m }, { "renewal", null } });
            Dispatcher.UIThread.RunJobs();
            grid.LineCount.ShouldBe(5);
            ShouldAlternate(grid);
            rows.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            grid.LineCount.ShouldBe(4);
            ShouldAlternate(grid);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Are_Stable_While_Scrolling_A_Long_Grid()
    {
        var grid = DataGridBehaviorTests.Create(Many(400));
        grid.IsStriped = true;
        var window = DataGridBehaviorTests.Show(grid);
        try
        {
            foreach (var offset in new[] { 0.0, 777.0, 3001.0, 120.0 })
            {
                ((Avalonia.Controls.Primitives.IScrollable)grid.Viewport).Offset = new Vector(0, offset);
                Dispatcher.UIThread.RunJobs();
                var (first, last) = grid.Viewport.LineWindow();
                for (var line = first; line <= last; line++)
                {
                    grid.IsStripedLine(line).ShouldBe((line & 1) == 1);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checked_Rows_Beat_The_Stripe()
    {
        var palette = DataGridBehaviorTests.Create().Palette;
        palette.CellFill(striped: true, isChecked: true).ShouldBe(palette.CheckedBrush);
        palette.GutterFill(striped: true, isChecked: true).ShouldBe(palette.CheckedBrush);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Stripe_Differs_From_The_Surface_In_Light_And_Dark(bool dark)
    {
        var token = new DataGridToken();
        token.CalculateTokenValues(dark);
        var n = ArcPalette.Neutrals(dark);
        token.StripeCellBackground.ShouldNotBe(n.SurfaceRaised);
        token.StripeGutterBackground.ShouldNotBe(n.Surface);
    }
}
