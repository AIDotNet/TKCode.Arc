using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Menus;

public class ContextMenuTests
{
    private static ContextMenu CreateMenu()
    {
        var menu = new ContextMenu
        {
            Label = "Actions for report.pdf",
            Content = new Border { Width = 200, Height = 100, Child = new TextBlock { Text = "report.pdf" } }
        };
        menu.Items.Add(new MenuEntry { Header = "Copy link", Icon = ArcIcons.Copy });
        menu.Items.Add(new MenuEntry { Header = "Pinned", IsChecked = true });
        menu.Items.Add(new MenuEntry { Header = "Rename" });
        menu.Items.Add(new MenuEntry { Header = "Delete file", Icon = ArcIcons.Trash2, IsDestructive = true });
        return menu;
    }

    [AvaloniaFact]
    public void Context_Variant_Rows_Show_Check_Or_Ellipsis_Without_Icons()
    {
        var menu = CreateMenu();
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.Menu.Variant.ShouldBe(MenuSurfaceVariant.Context);
            menu.Menu.ClosesOnViewportChange.ShouldBeTrue();
            var pinned = (MenuEntry)menu.Items[1]!;
            var rename = (MenuEntry)menu.Items[2]!;
            pinned.Variant.ShouldBe(MenuSurfaceVariant.Context);
            pinned.IconGeometry.ShouldBe(ArcIcons.Check);
            rename.IconGeometry.ShouldBe(ArcIcons.Ellipsis);
            rename.Bounds.Height.ShouldBe(34);
            menu.Menu.MinWidth.ShouldBe(208);
            menu.Menu.MaxHeight.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_F10_Opens_On_The_First_Row_And_Escape_Returns_Focus()
    {
        var menu = CreateMenu();
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Focus();
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            (menu.Menu.HighlightedEntry?.Header as string).ShouldBe("Copy link");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            (menu.Menu.HighlightedEntry?.Header as string).ShouldBe("Delete file");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opens_At_A_Point_And_Reopens_At_A_New_Point()
    {
        var menu = CreateMenu();
        var opened = 0;
        menu.Opened += (_, _) => opened++;
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open(new Point(40, 30));
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.Popup!.AnchorPoint.ShouldBe(new Point(40, 30));
            menu.Open(new Point(120, 60));
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.Popup!.AnchorPoint.ShouldBe(new Point(120, 60));
            opened.ShouldBe(2);
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Attached_Menu_Opens_From_ContextRequested()
    {
        var surface = new MenuSurface();
        surface.Items.Add(new MenuEntry { Header = "Open" });
        var target = new Border { Width = 160, Height = 80, Background = Avalonia.Media.Brushes.Transparent };
        ContextMenu.SetMenu(target, surface);
        var window = TestHelpers.Show(target);
        try
        {
            surface.Variant.ShouldBe(MenuSurfaceVariant.Context);
            target.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeTrue();
            surface.PlacementTarget.ShouldBe(target);
            ContextMenu.SetMenu(target, null);
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
