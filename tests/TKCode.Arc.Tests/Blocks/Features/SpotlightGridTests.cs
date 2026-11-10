using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Features;

public class SpotlightGridTests
{
    private static Window Show(Control control, double width = 1100)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Show_Six_Cards_In_Three_Columns()
    {
        var grid = new SpotlightGrid();
        var window = Show(grid);
        try
        {
            grid.Surface!.Cards.Count.ShouldBe(6);
            grid.Surface.Columns.ShouldBe(3);
            grid.Surface.Cards[2].Badge.ShouldBe("New");
            grid.Surface.Intensity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Pointer_Lights_The_Grid_And_Leaving_Dims_It()
    {
        var grid = new SpotlightGrid();
        var window = Show(grid);
        try
        {
            var surface = grid.Surface!;
            var point = surface.TranslatePoint(new Point(100, 60), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            surface.Intensity.ShouldBe(1);
            surface.SpotlightTarget.X.ShouldBe(100, 0.5);
            surface.SpotlightTarget.Y.ShouldBe(60, 0.5);

            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            surface.Intensity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Carries_The_Light_And_Arrows_Move_By_Row()
    {
        var grid = new SpotlightGrid();
        var window = Show(grid);
        try
        {
            var cards = grid.Surface!.Cards;
            cards[0].Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            grid.Surface.Intensity.ShouldBe(1);
            grid.Surface.SpotlightTarget.ShouldBe(cards[0].Bounds.Center);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            cards[3].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            cards[4].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Raises_ItemInvoked_And_Disabling_The_Light_Keeps_It_Off()
    {
        var grid = new SpotlightGrid { IsSpotlightEnabled = false };
        SpotlightGridItem? invoked = null;
        grid.ItemInvoked += (_, e) => invoked = e.Item;
        var window = Show(grid);
        try
        {
            var card = grid.Surface!.Cards[1];
            card.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldBe(card.Item);
            grid.Surface.Intensity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Items_Replace_The_Samples_And_No_Header_Hides_It()
    {
        var grid = new SpotlightGrid { Eyebrow = null, Title = null, Description = null, MaxColumns = 2 };
        grid.Items.Add(new SpotlightGridItem { Title = "One" });
        grid.Items.Add(new SpotlightGridItem { Title = "Two" });
        grid.Items.Add(new SpotlightGridItem { Title = "Three" });
        var window = Show(grid);
        try
        {
            grid.Surface!.Cards.Select(c => c.Title).ShouldBe(["One", "Two", "Three"]);
            grid.Surface.Columns.ShouldBe(2);
            grid.Classes.Contains(":no-header").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Drop_With_The_Width()
    {
        SpotlightGridSurface.ColumnsFor(1000, 260, 3, 16).ShouldBe(3);
        SpotlightGridSurface.ColumnsFor(600, 260, 3, 16).ShouldBe(2);
        SpotlightGridSurface.ColumnsFor(300, 260, 3, 16).ShouldBe(1);
    }
}
