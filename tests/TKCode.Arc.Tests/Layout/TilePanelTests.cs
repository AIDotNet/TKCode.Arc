using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Layout;

/// <summary>TilePanel: cell sizes, first-fit placement without overlap, span limits, row overflow, auto fill, auto span and column-major growth.</summary>
public class TilePanelTests
{
    private static Border Tile(int rowSpan = 1, int columnSpan = 1)
    {
        var tile = new Border();
        TilePanel.SetRowSpan(tile, rowSpan);
        TilePanel.SetColumnSpan(tile, columnSpan);
        return tile;
    }

    private static TilePanel Panel(double width, double height, params Control[] tiles)
    {
        var panel = new TilePanel
        {
            Width = width,
            Height = height,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.Children.AddRange(tiles);
        return panel;
    }

    private static TilePlacement? At(TilePanel panel, int i) => panel.GetPlacement(panel.Children[i]);

    private static void ShouldNotOverlap(TilePanel panel)
    {
        var rects = panel.Children.Where(c => c.IsVisible).Select(c => c.Bounds).ToArray();
        for (var i = 0; i < rects.Length; i++)
        {
            for (var j = i + 1; j < rects.Length; j++)
            {
                rects[i].Intersects(rects[j]).ShouldBeFalse($"tiles {i} and {j} overlap");
            }
        }
    }

    /// <summary>Shows content in a window tall enough that no panel here is cut to the window.</summary>
    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 1000;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Theme_Gives_The_Token_Defaults()
    {
        var panel = new TilePanel();
        var window = Show(panel);
        try
        {
            panel.Spacing.ShouldBe(8);
            panel.MinCellSize.ShouldBe(96);
            panel.IsAutoFill.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tiles_Take_The_First_Free_Place_In_Reading_Order()
    {
        // Four 100px columns: (430 - 30) / 4; cells are square.
        var panel = Panel(430, double.NaN, Tile(2, 2), Tile(), Tile(), Tile(1, 2), Tile());
        panel.Columns = 4;
        var window = Show(panel);
        try
        {
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 2, 2));
            At(panel, 1).ShouldBe(new TilePlacement(0, 2, 1, 1));
            At(panel, 2).ShouldBe(new TilePlacement(0, 3, 1, 1));
            At(panel, 3).ShouldBe(new TilePlacement(1, 2, 1, 2));
            At(panel, 4).ShouldBe(new TilePlacement(2, 0, 1, 1));
            panel.Children[0].Bounds.ShouldBe(new Rect(0, 0, 210, 210));
            panel.Children[3].Bounds.ShouldBe(new Rect(220, 110, 210, 100));
            panel.Children[4].Bounds.ShouldBe(new Rect(0, 220, 100, 100));
            panel.RowCount.ShouldBe(3);
            panel.Bounds.Height.ShouldBe(320);
            ShouldNotOverlap(panel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Later_Small_Tiles_Fill_Holes_Left_By_Wide_Ones()
    {
        var panel = Panel(320, double.NaN, Tile(1, 2), Tile(1, 2), Tile());
        panel.Columns = 3;
        var window = Show(panel);
        try
        {
            At(panel, 1).ShouldBe(new TilePlacement(1, 0, 1, 2));
            At(panel, 2).ShouldBe(new TilePlacement(0, 2, 1, 1));
            ShouldNotOverlap(panel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spans_Are_Clamped_To_The_Limits_And_The_Grid()
    {
        var panel = Panel(430, double.NaN, Tile(5, 9), Tile(1, 3));
        panel.Columns = 4;
        panel.MaxRowSpan = 2;
        panel.MaxColumnSpan = 3;
        var window = Show(panel);
        try
        {
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 2, 3));
            At(panel, 1).ShouldBe(new TilePlacement(2, 0, 1, 3));
            panel.MaxColumnSpan = 0;
            Dispatcher.UIThread.RunJobs();
            // No limit: still never wider than the grid.
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 2, 4));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Share_The_Height_And_Extra_Tiles_Continue_Below()
    {
        var panel = Panel(320, 210, Enumerable.Range(0, 7).Select(_ => (Control)Tile()).ToArray());
        panel.Columns = 3;
        panel.Rows = 2;
        var window = Show(panel);
        try
        {
            panel.Children[4].Bounds.ShouldBe(new Rect(110, 110, 100, 100));
            // Seventh tile: a third row of the same size, below the panel's height; nothing is dropped.
            panel.Children[6].Bounds.ShouldBe(new Rect(0, 220, 100, 100));
            panel.RowCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automatic_Columns_Follow_The_Width()
    {
        var panel = Panel(430, double.NaN, Tile(), Tile(), Tile());
        panel.MinCellSize = 100;
        var window = Show(panel);
        try
        {
            panel.ColumnCount.ShouldBe(4);
            panel.Width = 300;
            Dispatcher.UIThread.RunJobs();
            // (300 + 10) / 110 = 2.8: two 145px columns, the third tile wraps.
            panel.ColumnCount.ShouldBe(2);
            panel.Children[2].Bounds.ShouldBe(new Rect(0, 155, 145, 145));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fixed_Columns_Keep_The_Placement_And_Scale_With_The_Width()
    {
        var panel = Panel(430, double.NaN, Tile(2, 2), Tile(), Tile());
        panel.Columns = 4;
        var window = Show(panel);
        try
        {
            panel.Width = 830;
            Dispatcher.UIThread.RunJobs();
            At(panel, 2).ShouldBe(new TilePlacement(0, 3, 1, 1));
            panel.Children[0].Bounds.ShouldBe(new Rect(0, 0, 410, 410));
            panel.Children[2].Bounds.ShouldBe(new Rect(630, 0, 200, 200));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoFill_Grows_Tiles_Right_Then_Down_Into_Empty_Cells()
    {
        var panel = Panel(320, 210, Tile(), Tile());
        panel.Columns = 3;
        panel.Rows = 2;
        var window = Show(panel);
        try
        {
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 1, 1));
            panel.IsAutoFill = true;
            Dispatcher.UIThread.RunJobs();
            // The first tile cannot grow right (the second is there), so it grows down; the second takes the rest.
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 2, 1));
            At(panel, 1).ShouldBe(new TilePlacement(0, 1, 2, 2));
            panel.Children[1].Bounds.ShouldBe(new Rect(110, 0, 210, 210));
            ShouldNotOverlap(panel);

            panel.MaxColumnSpan = 1;
            Dispatcher.UIThread.RunJobs();
            // Limits still hold: the last column stays empty.
            At(panel, 1).ShouldBe(new TilePlacement(0, 1, 2, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unbounded_Width_With_Rows_Grows_By_Columns()
    {
        var panel = Panel(double.NaN, 210, Tile(2, 1), Tile(), Tile(), Tile());
        panel.Rows = 2;
        var host = new StackPanel { Orientation = Orientation.Horizontal, Children = { panel } };
        var window = Show(host);
        try
        {
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 2, 1));
            At(panel, 1).ShouldBe(new TilePlacement(0, 1, 1, 1));
            At(panel, 2).ShouldBe(new TilePlacement(1, 1, 1, 1));
            At(panel, 3).ShouldBe(new TilePlacement(0, 2, 1, 1));
            panel.ColumnCount.ShouldBe(3);
            panel.Bounds.Width.ShouldBe(320);
            panel.Children[3].Bounds.ShouldBe(new Rect(220, 0, 100, 100));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsed_Tiles_Take_No_Cell()
    {
        var hidden = Tile(2, 2);
        hidden.IsVisible = false;
        var panel = Panel(320, double.NaN, hidden, Tile());
        panel.Columns = 3;
        var window = Show(panel);
        try
        {
            At(panel, 0).ShouldBeNull();
            At(panel, 1).ShouldBe(new TilePlacement(0, 0, 1, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Works_As_An_ItemsPanel_With_Spans_On_The_Containers()
    {
        var items = new ItemsControl
        {
            Width = 320,
            ItemsSource = new[] { 2, 1, 1 },
            ItemsPanel = new FuncTemplate<Panel?>(() => new TilePanel { Columns = 3, Spacing = 10 }),
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border()),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        items.ContainerPrepared += (_, e) => TilePanel.SetColumnSpan(e.Container, (int)e.Container.DataContext!);
        var window = Show(items);
        try
        {
            var panel = items.GetVisualDescendants().OfType<TilePanel>().Single();
            At(panel, 0).ShouldBe(new TilePlacement(0, 0, 1, 2));
            At(panel, 1).ShouldBe(new TilePlacement(0, 2, 1, 1));
            At(panel, 2).ShouldBe(new TilePlacement(1, 0, 1, 1));
            items.Bounds.Height.ShouldBe(210);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Tiles that set no span, so <see cref="TilePanel.IsAutoSpan"/> sizes them.</summary>
    private static Control[] AutoTiles(int count) => Enumerable.Range(0, count).Select(_ => (Control)new Border()).ToArray();

    private static TilePlacement[] Placements(TilePanel panel) =>
        panel.Children.Where(c => c.IsVisible).Select(c => panel.GetPlacement(c)!.Value).ToArray();

    /// <summary>How many tiles cover each cell of the panel's grid.</summary>
    private static int[,] Coverage(TilePanel panel)
    {
        var cover = new int[panel.RowCount, panel.ColumnCount];
        foreach (var p in Placements(panel))
        {
            for (var r = p.Row; r < p.Row + p.RowSpan; r++)
            {
                for (var c = p.Column; c < p.Column + p.ColumnSpan; c++)
                {
                    cover[r, c]++;
                }
            }
        }
        return cover;
    }

    private static bool Touch(TilePlacement a, TilePlacement b) =>
        a.Row <= b.Row + b.RowSpan && b.Row <= a.Row + a.RowSpan
        && a.Column <= b.Column + b.ColumnSpan && b.Column <= a.Column + a.ColumnSpan;

    [AvaloniaFact]
    public void AutoSpan_Sizes_Children_Without_Spans_And_Keeps_Explicit_Ones()
    {
        var tiles = AutoTiles(24);
        TilePanel.SetColumnSpan(tiles[5], 3);
        TilePanel.SetRowSpan(tiles[9], 1);
        var panel = Panel(430, double.NaN, tiles);
        panel.Columns = 4;
        panel.IsAutoSpan = true;
        var window = Show(panel);
        try
        {
            var placements = Placements(panel);
            At(panel, 5).ShouldBe(At(panel, 5)!.Value with { RowSpan = 1, ColumnSpan = 3 });
            // RowSpan="1" set explicitly counts as a choice: the child stays 1×1.
            At(panel, 9)!.Value.RowSpan.ShouldBe(1);
            At(panel, 9)!.Value.ColumnSpan.ShouldBe(1);
            placements.ShouldAllBe(p => p.RowSpan <= 3 && p.ColumnSpan <= 3);
            var automatic = placements.Where((_, i) => i != 5 && i != 9).ToArray();
            automatic.ShouldAllBe(p => p.RowSpan <= 2 && p.ColumnSpan <= 2);
            automatic.Count(p => p.RowSpan * p.ColumnSpan > 1).ShouldBeGreaterThan(0);
            automatic.Count(p => p.RowSpan * p.ColumnSpan == 1).ShouldBeGreaterThan(automatic.Length / 2);
            ShouldNotOverlap(panel);

            panel.IsAutoSpan = false;
            Dispatcher.UIThread.RunJobs();
            Placements(panel).Where((_, i) => i != 5).ShouldAllBe(p => p.RowSpan == 1 && p.ColumnSpan == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoSpan_Is_Repeatable_Follows_The_Seed_And_Survives_Resizing()
    {
        TilePanel Make(int seed)
        {
            var panel = Panel(430, double.NaN, AutoTiles(16));
            panel.Columns = 4;
            panel.IsAutoSpan = true;
            panel.Seed = seed;
            return panel;
        }

        var first = Make(3);
        var second = Make(3);
        var other = Make(4);
        var window = Show(new StackPanel { Children = { first, second, other } });
        try
        {
            Placements(second).ShouldBe(Placements(first));
            Placements(other).ShouldNotBe(Placements(first));

            var before = Placements(first);
            first.Width = 830;
            Dispatcher.UIThread.RunJobs();
            Placements(first).ShouldBe(before);

            first.Seed = 4;
            Dispatcher.UIThread.RunJobs();
            Placements(first).ShouldBe(Placements(other));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoSpan_Leaves_No_Holes_Before_A_Tile_And_Keeps_Larger_Tiles_Apart()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var panel = Panel(430, double.NaN, AutoTiles(30));
            panel.Columns = 4;
            panel.IsAutoSpan = true;
            panel.Seed = seed;
            var window = Show(panel);
            try
            {
                ShouldNotOverlap(panel);
                // Every cell before the last tile's cell (reading order) is covered: tiles go to the first free cell.
                var cover = Coverage(panel);
                var placements = Placements(panel);
                var last = placements[^1];
                for (var r = 0; r <= last.Row; r++)
                {
                    for (var c = 0; c < (r == last.Row ? last.Column : panel.ColumnCount); c++)
                    {
                        cover[r, c].ShouldBe(1, $"seed {seed}: cell ({r}, {c}) is a hole");
                    }
                }
                var larger = placements.Where(p => p.RowSpan * p.ColumnSpan > 1).ToArray();
                for (var i = 0; i < larger.Length; i++)
                {
                    for (var j = i + 1; j < larger.Length; j++)
                    {
                        Touch(larger[i], larger[j]).ShouldBeFalse($"seed {seed}: {larger[i]} touches {larger[j]}");
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void AutoSpan_Respects_The_Span_Limits_And_The_Shared_Rows()
    {
        var panel = Panel(430, 320, AutoTiles(20));
        panel.Columns = 4;
        panel.Rows = 3;
        panel.IsAutoSpan = true;
        panel.MaxColumnSpan = 1;
        var window = Show(panel);
        try
        {
            var placements = Placements(panel);
            placements.ShouldAllBe(p => p.ColumnSpan == 1 && p.RowSpan <= 2);
            // No automatic tile straddles the last shared row.
            placements.ShouldAllBe(p => p.Row >= 3 || p.Row + p.RowSpan <= 3);
            ShouldNotOverlap(panel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoSpan_With_AutoFill_Covers_A_Fixed_Grid_Without_Holes()
    {
        var failures = new List<string>();
        foreach (var (columns, rows) in new[] { (4, 3), (6, 4), (5, 5), (3, 2) })
        {
            for (var count = 1; count <= columns * rows + 2; count++)
            {
                for (var seed = 0; seed < 6; seed++)
                {
                    var panel = Panel(columns * 110 - 10, rows * 110 - 10, AutoTiles(count));
                    panel.Columns = columns;
                    panel.Rows = rows;
                    panel.IsAutoSpan = true;
                    panel.IsAutoFill = true;
                    panel.Seed = seed;
                    var window = Show(panel);
                    try
                    {
                        var cover = Coverage(panel);
                        for (var r = 0; r < panel.RowCount; r++)
                        {
                            for (var c = 0; c < panel.ColumnCount; c++)
                            {
                                if (cover[r, c] != 1)
                                {
                                    failures.Add($"{columns}x{rows}, {count} tiles, seed {seed}: cell ({r}, {c}) covered {cover[r, c]} times");
                                }
                            }
                        }
                        if (count <= columns * rows)
                        {
                            panel.RowCount.ShouldBe(rows, $"{columns}x{rows}, {count} tiles, seed {seed}");
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        failures.ShouldBeEmpty(string.Join("\n", failures.Take(20)));
    }

    [AvaloniaFact]
    public void AutoFill_With_Explicit_And_Automatic_Tiles_Keeps_The_Explicit_Spans_And_Leaves_No_Holes()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var tiles = AutoTiles(7);
            TilePanel.SetRowSpan(tiles[0], 2);
            TilePanel.SetColumnSpan(tiles[0], 2);
            TilePanel.SetColumnSpan(tiles[4], 3);
            var panel = Panel(430, 320, tiles);
            panel.Columns = 4;
            panel.Rows = 3;
            panel.IsAutoSpan = true;
            panel.IsAutoFill = true;
            panel.Seed = seed;
            var window = Show(panel);
            try
            {
                var cover = Coverage(panel);
                panel.RowCount.ShouldBe(3);
                for (var r = 0; r < 3; r++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        cover[r, c].ShouldBe(1, $"seed {seed}: cell ({r}, {c})");
                    }
                }
                // Explicit spans only ever grow.
                At(panel, 0)!.Value.RowSpan.ShouldBeGreaterThanOrEqualTo(2);
                At(panel, 0)!.Value.ColumnSpan.ShouldBeGreaterThanOrEqualTo(2);
                At(panel, 4)!.Value.ColumnSpan.ShouldBeGreaterThanOrEqualTo(3);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void AutoSpan_Works_In_An_ItemsControl()
    {
        var items = new ItemsControl
        {
            Width = 430,
            ItemsSource = Enumerable.Range(0, 12).ToArray(),
            ItemsPanel = new FuncTemplate<Panel?>(() => new TilePanel { Columns = 4, Rows = 3, Spacing = 10, IsAutoSpan = true, IsAutoFill = true, Seed = 1 }),
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border()),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = 320,
        };
        var window = Show(items);
        try
        {
            var panel = items.GetVisualDescendants().OfType<TilePanel>().Single();
            var placements = Placements(panel);
            placements.Length.ShouldBe(12);
            placements.Sum(p => p.RowSpan * p.ColumnSpan).ShouldBe(12);
            ShouldNotOverlap(panel);
        }
        finally
        {
            window.Close();
        }
    }
}
