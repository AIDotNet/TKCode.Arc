using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Photos;

/// <summary>The photo grid: the justified and masonry arithmetic, then the control's tiles, selection, keyboard and reflow.</summary>
public class PhotoGridTests
{
    private static Rect[] Layout(double[] ratios, double width, PhotoGridLayout layout = PhotoGridLayout.Justified, int selected = -1,
                                 double rowHeight = 100, double columnWidth = 100, double maxHeight = 300, double spacing = 4)
    {
        var rects = new Rect[ratios.Length];
        var columns = new double[16];
        PhotoGridLayoutEngine.Arrange(ratios, rects, width, spacing, layout, rowHeight, columnWidth, selected, maxHeight, columns);
        return rects;
    }

    // --- arithmetic ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Justified_rows_share_one_height_and_end_exactly_at_the_edge()
    {
        double[] ratios = [1.5, 0.75, 1.5, 1, 1.78, 0.66, 1.5, 1.33, 1];
        var rects = Layout(ratios, 600);
        var rows = rects.Take(rects.Length - 1).GroupBy(r => Math.Round(r.Y, 3)).ToArray();
        rows.Length.ShouldBeGreaterThan(1);
        foreach (var row in rows.Take(rows.Length - 1))
        {
            row.Select(r => Math.Round(r.Height, 6)).Distinct().Count().ShouldBe(1);
            row.Max(r => r.Right).ShouldBe(600, 0.001);
            row.Min(r => r.X).ShouldBe(0, 0.001);
        }
    }

    [Fact]
    public void Every_photo_keeps_its_proportions()
    {
        double[] ratios = [1.5, 0.75, 1.5, 1, 1.78];
        var rects = Layout(ratios, 500);
        for (var i = 0; i < ratios.Length - 1; i++)
        {
            // The last photo of a full row absorbs rounding; it stays within a pixel of its shape.
            (rects[i].Width / rects[i].Height).ShouldBe(ratios[i], 0.02);
        }
    }

    [Fact]
    public void A_short_last_row_keeps_the_target_height()
    {
        double[] ratios = [1.5, 1.5, 1.5, 1.5, 1];
        var rects = Layout(ratios, 400, rowHeight: 100);
        rects[^1].Height.ShouldBe(100, 0.001);
        rects[^1].Right.ShouldBeLessThan(400);
    }

    [Fact]
    public void The_selected_photo_gets_a_centred_row_of_its_own_capped_in_height()
    {
        double[] ratios = [1.5, 1.5, 1, 1.5, 1.5];
        var rects = Layout(ratios, 600, selected: 2, maxHeight: 300);
        var selected = rects[2];
        selected.Height.ShouldBe(300, 0.001);
        selected.Width.ShouldBe(300, 0.001);
        selected.Center.X.ShouldBe(300, 0.001);
        // Nothing else shares its row.
        for (var i = 0; i < rects.Length; i++)
        {
            if (i != 2)
            {
                (rects[i].Bottom <= selected.Top + 0.001 || rects[i].Top >= selected.Bottom - 0.001).ShouldBeTrue();
            }
        }
        rects[3].Top.ShouldBeGreaterThan(selected.Bottom);
    }

    [Fact]
    public void Masonry_puts_each_photo_in_the_shortest_column()
    {
        double[] ratios = [1, 0.5, 1, 1, 1];
        var rects = Layout(ratios, 312, PhotoGridLayout.Masonry, columnWidth: 100, spacing: 4);
        PhotoGridLayoutEngine.ColumnCount(312, 4, 100).ShouldBe(3);
        rects.Select(r => r.Width).Distinct().Count().ShouldBe(1);
        // The tall photo fills column 1, so photos 3 and 4 go to columns 0 and 2 before column 1 again.
        rects[3].X.ShouldBe(rects[0].X, 0.001);
        rects[4].X.ShouldBe(rects[2].X, 0.001);
    }

    [Fact]
    public void A_selected_masonry_photo_spans_every_column_below_the_tallest()
    {
        double[] ratios = [1, 0.5, 1, 1.5];
        var rects = Layout(ratios, 304, PhotoGridLayout.Masonry, selected: 3, columnWidth: 100, spacing: 4, maxHeight: 1000);
        rects[3].Top.ShouldBeGreaterThanOrEqualTo(rects.Take(3).Max(r => r.Bottom));
        rects[3].Width.ShouldBe(304, 0.001);
    }

    // --- control -------------------------------------------------------------------------------------------------------

    private static PhotoGrid Grid(int count = 6) => new()
    {
        Width = 380,
        RowHeight = 80,
        ItemsSource = Enumerable.Range(0, count)
            .Select(i => new PhotoGridPhoto { Source = PhotosTestPictures.Picture(), AspectRatio = i % 2 == 0 ? 1.5 : 0.8, Description = $"Shot {i}" })
            .ToArray()
    };

    [AvaloniaFact]
    public void Each_photo_is_a_named_tile_at_its_layout_box()
    {
        var grid = Grid();
        var window = TestHelpers.Show(grid);
        try
        {
            grid.Tiles.Count.ShouldBe(6);
            AutomationProperties.GetName(grid.Tiles[1]).ShouldBe("Photo 2 of 6: Shot 1");
            var tile = grid.Tiles[0];
            tile.Bounds.Width.ShouldBeGreaterThan(0);
            (tile.Bounds.Width / tile.Bounds.Height).ShouldBe(1.5, 0.02);
            grid.Bounds.Height.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_selects_a_photo_and_Escape_lets_it_go()
    {
        var grid = Grid();
        var window = TestHelpers.Show(grid);
        try
        {
            grid.Tiles[2].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            grid.SelectedIndex.ShouldBe(2);
            grid.Tiles[2].IsSelected.ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            // It has a row of its own: no other photo overlaps its band.
            var band = grid.Tiles[2].Bounds;
            grid.Tiles.Where(t => t.Index != 2).ShouldAllBe(t => t.Bounds.Bottom <= band.Top + 0.5 || t.Bounds.Top >= band.Bottom - 0.5);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            grid.SelectedIndex.ShouldBe(-1);
            grid.Tiles[2].IsSelected.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_move_through_the_photos_and_between_rows()
    {
        var grid = Grid(8);
        var window = TestHelpers.Show(grid);
        try
        {
            grid.Tiles[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            grid.Tiles[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            var focused = grid.Tiles.Single(t => t.IsFocused);
            focused.Bounds.Top.ShouldBeGreaterThan(grid.Tiles[1].Bounds.Bottom - 0.5);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            grid.Tiles[^1].IsFocused.ShouldBeTrue();
            // One tab stop: the photo last focused.
            grid.Tiles.Count(t => t.IsTabStop).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_bound_collection_adds_and_removes_tiles_and_keeps_the_ones_that_stay()
    {
        var photos = new ObservableCollection<PhotoGridPhoto>(Enumerable.Range(0, 4)
            .Select(_ => new PhotoGridPhoto { Source = PhotosTestPictures.Picture(), AspectRatio = 1.5 }));
        var grid = new PhotoGrid { Width = 380, ItemsSource = photos };
        var window = TestHelpers.Show(grid);
        try
        {
            var kept = grid.Tiles[1];
            photos.Insert(0, new PhotoGridPhoto { Source = PhotosTestPictures.Picture(), AspectRatio = 1 });
            Dispatcher.UIThread.RunJobs();
            grid.Tiles.Count.ShouldBe(5);
            grid.Tiles[2].ShouldBeSameAs(kept);
            photos.RemoveAt(0);
            photos.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            grid.Tiles.Count.ShouldBe(3);
            grid.Tiles[0].ShouldBeSameAs(kept);
            // With motion off the leaving photos go at once.
            grid.LeavingCount.ShouldBe(0);
            grid.GetVisualChildrenCount().ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_photo_without_proportions_takes_the_loaded_pictures_shape()
    {
        var grid = new PhotoGrid { Width = 380, RowHeight = 90 };
        grid.Items.Add(new PhotoGridPhoto { Source = PhotosTestPictures.Picture(20, 40) });
        var window = TestHelpers.Show(grid);
        try
        {
            TestHelpers.PumpUntil(() => Math.Abs(grid.Tiles[0].Bounds.Width / grid.Tiles[0].Bounds.Height - 0.5) < 0.02, 2000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_full_motion_a_change_glides_from_where_the_photos_were()
    {
        var grid = Grid();
        var window = TestHelpers.Show(grid, Motion.ArcMotionLevel.Full);
        try
        {
            var before = grid.Tiles[3].Bounds;
            grid.SelectedIndex = 0;
            // Lay out now, before any frame of the reflow has run.
            grid.UpdateLayout();
            var tile = grid.Tiles[3];
            tile.Bounds.ShouldNotBe(before);
            // At the start of the reflow the photo is still drawn where it was.
            grid.IsReflowing.ShouldBeTrue();
            tile.Shown.X.ShouldBe(before.X, 1);
            tile.Shown.Y.ShouldBe(before.Y, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_reflow_transform_draws_a_box_over_another()
    {
        var target = new Rect(100, 50, 200, 100);
        var shown = new Rect(10, 20, 100, 50);
        var m = PhotoGrid.Place(target, shown);
        // The control's local (0,0) and (200,100) land on the shown box's corners, relative to the layout slot.
        var origin = new Point(0, 0).Transform(m) + target.Position;
        var corner = new Point(200, 100).Transform(m) + target.Position;
        origin.ShouldBe(shown.TopLeft);
        corner.ShouldBe(shown.BottomRight);
    }
}

file static class PhotoGridVisuals
{
    public static int GetVisualChildrenCount(this PhotoGrid grid) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualChildren(grid).Count();
}
