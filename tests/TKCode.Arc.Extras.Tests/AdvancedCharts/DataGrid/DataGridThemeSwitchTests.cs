using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>A theme change at run time reaches every part the grid paints itself.</summary>
public class DataGridThemeSwitchTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Filter_Row_Gutter_Follows_A_Theme_Change(bool startDark)
    {
        // Issue #135: with no filter active the clear button stays hidden, so nothing redrew the gutter behind it and it
        // kept the surface of the previous theme.
        await TestHelpers.ApplyAsync(startDark ? TestHelpers.Dark : null);
        var grid = DataGridBehaviorTests.Create();
        grid.IsRowCheckEnabled = true;
        var window = DataGridBehaviorTests.Show(grid);
        try
        {
            grid.IsFilterRowOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            var gutter = window.GetVisualDescendants().OfType<DataGridClearFiltersButton>().Single();
            grid.IsFiltering.ShouldBeFalse();
            var before = TestHelpers.ColorOf(gutter.DrawnSurface);
            before.ShouldBe(TestHelpers.ColorOf(grid.Palette.SurfaceBrush));

            await TestHelpers.ApplyAsync(startDark ? null : TestHelpers.Dark);
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();

            var after = TestHelpers.ColorOf(grid.Palette.SurfaceBrush);
            after.ShouldNotBe(before);
            TestHelpers.ColorOf(gutter.DrawnSurface).ShouldBe(after);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }
}
