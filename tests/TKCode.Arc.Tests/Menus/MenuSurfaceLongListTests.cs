using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Menus;

/// <summary>Long menus (GitHub issue 24): a height limit, scrolling, and an entrance that only plays the visible rows.</summary>
public class MenuSurfaceLongListTests
{
    private static async Task PumpUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static DropdownMenu CreateMenu(int count, MenuSide side = MenuSide.Bottom) => new()
    {
        Label = "Model",
        Side = side,
        ItemsSource = Enumerable.Range(1, count).Select(i => $"Model {i}").ToList()
    };

    /// <summary>A window of the given height with the menu docked to its top or bottom edge.</summary>
    private static Window Show(DropdownMenu menu, double height, bool atBottom, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        DockPanel.SetDock(menu, atBottom ? Dock.Bottom : Dock.Top);
        var dock = new DockPanel { Margin = new Thickness(40, 20), LastChildFill = false, Children = { menu } };
        var window = TestHelpers.Show(dock, level);
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Open(DropdownMenu menu)
    {
        var trigger = menu.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == DropdownMenu.TriggerPart);
        trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static ScrollViewer Scroll(MenuSurface surface) =>
        surface.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == MenuSurface.ScrollViewerPart);

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(default, window)!.Value, visual.Bounds.Size);

    [AvaloniaFact]
    public void Upward_Menu_At_The_Window_Bottom_Stays_Inside_The_Window_And_Scrolls()
    {
        var menu = CreateMenu(30, MenuSide.Top);
        var window = Show(menu, 500, atBottom: true);
        try
        {
            Open(menu);
            var surface = menu.Menu;
            surface.IsOpen.ShouldBeTrue();
            var bounds = InWindow(surface, window);
            bounds.Top.ShouldBeGreaterThanOrEqualTo(0);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(window.ClientSize.Height);
            bounds.Height.ShouldBeLessThanOrEqualTo(384);

            var scroll = Scroll(surface);
            scroll.Extent.Height.ShouldBeGreaterThan(scroll.Viewport.Height);
            scroll.Offset = new Vector(0, 200);
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.Y.ShouldBe(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Short_Window_Limits_The_Menu_Below_The_Theme_Height()
    {
        var menu = CreateMenu(30);
        var window = Show(menu, 260, atBottom: false);
        try
        {
            Open(menu);
            var bounds = InWindow(menu.Menu, window);
            bounds.Height.ShouldBeLessThan(260);
            bounds.Bottom.ShouldBeLessThanOrEqualTo(window.ClientSize.Height);
            var scroll = Scroll(menu.Menu);
            scroll.Extent.Height.ShouldBeGreaterThan(scroll.Viewport.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MaxDropDownHeight_Overrides_The_Theme_Height()
    {
        var menu = CreateMenu(30);
        menu.MaxDropDownHeight = 200;
        var window = Show(menu, 900, atBottom: false);
        try
        {
            Open(menu);
            menu.Menu.Bounds.Height.ShouldBe(200);

            menu.MaxDropDownHeight = double.NaN;
            Dispatcher.UIThread.RunJobs();
            menu.Menu.Bounds.Height.ShouldBe(384);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Short_Menus_Keep_Their_Natural_Height()
    {
        var menu = CreateMenu(4);
        var window = Show(menu, 600, atBottom: false);
        try
        {
            Open(menu);
            var scroll = Scroll(menu.Menu);
            scroll.Extent.Height.ShouldBe(scroll.Viewport.Height);
            menu.Menu.Bounds.Height.ShouldBeLessThan(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Scrolls_The_Highlighted_Row_Into_View_And_Reopening_Starts_At_The_Top()
    {
        var menu = CreateMenu(30);
        var window = Show(menu, 600, atBottom: false);
        try
        {
            var trigger = menu.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == DropdownMenu.TriggerPart);
            trigger.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var last = menu.Menu.HighlightedEntry.ShouldNotBeNull();
            last.Header.ShouldBe("Model 30");
            var scroll = Scroll(menu.Menu);
            scroll.Offset.Y.ShouldBeGreaterThan(0);
            var row = InWindow(last, window);
            var viewport = InWindow(scroll, window);
            row.Bottom.ShouldBeLessThanOrEqualTo(viewport.Bottom + 0.5);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Open(menu);
            scroll.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Only_The_Rows_In_The_Viewport_Play_The_Entrance()
    {
        var menu = CreateMenu(200);
        var window = Show(menu, 700, atBottom: false, ArcMotionLevel.Full);
        try
        {
            Open(menu);
            var rows = menu.Menu.GetRealizedContainers().OfType<MenuEntry>().ToList();
            rows.Count.ShouldBe(200);
            rows[1].Opacity.ShouldBeLessThan(1);
            // 384px holds about ten 36px rows; everything below is shown at once and owns no motion state.
            rows.Skip(20).All(r => r.Opacity >= 1 && r.RenderTransform is null).ShouldBeTrue();
            await PumpUntil(() => rows.All(r => r.Opacity >= 1));
            rows.All(r => r.Opacity >= 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Scrolling_Moves_The_Highlight_To_The_Row_Under_The_Pointer()
    {
        var menu = CreateMenu(30);
        var window = Show(menu, 600, atBottom: false);
        try
        {
            Open(menu);
            var surface = menu.Menu;
            var first = surface.ContainerFromIndex(1).ShouldBeOfType<MenuEntry>();
            var point = InWindow(first, window).Center;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            surface.HighlightedEntry.ShouldBe(first);

            Scroll(surface).Offset = new Vector(0, 36 * 5);
            Dispatcher.UIThread.RunJobs();
            surface.HighlightedEntry.ShouldNotBeNull().Header.ShouldBe("Model 7");
        }
        finally
        {
            window.Close();
        }
    }
}
