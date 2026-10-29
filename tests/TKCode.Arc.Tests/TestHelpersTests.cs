using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests;

public class TestHelpersTests
{
    private static Grid StarGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,*"),
        Children = { new Border { Width = 10 }, new Border { Width = 10, [Grid.ColumnProperty] = 1 } }
    };

    /// <summary>
    /// Avalonia's grids share a scratch array per thread for star sizing. A grid measured again with a new width but not
    /// arranged again keeps both the array and its definitions in it, so a dropped control's grid stays reachable from any
    /// other such grid that is alive (in a full run, often one of an earlier test). <see cref="TestHelpers.Collect"/> clears
    /// the array: the "is collected" tests check the control's own references, not this one of Avalonia's.
    /// </summary>
    [AvaloniaFact]
    public void Collect_Is_Not_Held_Up_By_The_Grid_Scratch_Array()
    {
        var holder = StarGrid();
        holder.Measure(new Size(100, 100));
        holder.Arrange(new Rect(0, 0, 100, 100));
        holder.Measure(new Size(120, 100));

        var weak = TestHelpers.ShowAndClose(() => new Border { Child = StarGrid() }, border =>
        {
            var grid = (Grid)border.Child!;
            grid.Measure(new Size(grid.Bounds.Width + 20, grid.Bounds.Height));
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(holder);
    }
}
