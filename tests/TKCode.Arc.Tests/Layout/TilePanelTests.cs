using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Layout;

/// <summary>TilePanel: cell sizes, first-fit placement without overlap, span limits, row overflow, auto fill and column-major growth.</summary>
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
}
