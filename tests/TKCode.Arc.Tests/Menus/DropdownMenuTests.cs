using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Menus;

public class DropdownMenuTests
{
    private static DropdownMenu CreateMenu()
    {
        var menu = new DropdownMenu { Label = "Actions" };
        menu.Items.Add(new MenuEntry { Header = "Rename", Icon = ArcIcons.Pencil, InputGestureText = "R" });
        menu.Items.Add(new MenuEntry { Header = "Duplicate", Icon = ArcIcons.Copy });
        menu.Items.Add(new MenuEntry { Header = "Archive", Icon = ArcIcons.Archive, IsEnabled = false });
        menu.Items.Add(new MenuEntry { Header = "Delete", Icon = ArcIcons.Trash2, IsDestructive = true, HasSeparatorBefore = true });
        return menu;
    }

    private static Window Show(Control control) =>
        TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(40), Children = { control } });

    private static AvaloniaButton Trigger(DropdownMenu menu) =>
        menu.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == DropdownMenu.TriggerPart);

    [AvaloniaFact]
    public void Trigger_Click_Opens_And_Closes_The_Menu()
    {
        var menu = CreateMenu();
        var window = Show(menu);
        try
        {
            var trigger = Trigger(menu);
            ((Button)trigger).IsPressScaleEnabled.ShouldBeFalse();
            trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.IsOpen.ShouldBeTrue();
            menu.Menu.Popup!.IsOpen.ShouldBeTrue();
            menu.Classes.Contains(DropdownMenu.OpenPseudoClass).ShouldBeTrue();
            menu.Menu.HighlightedEntry.ShouldBeNull();

            trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.Menu.Popup!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Loop_And_Skip_Disabled_Rows()
    {
        var menu = CreateMenu();
        var window = Show(menu);
        try
        {
            var trigger = Trigger(menu);
            trigger.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            Header(menu).ShouldBe("Rename");
            menu.Menu.HighlightedEntry!.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Header(menu).ShouldBe("Duplicate");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Header(menu).ShouldBe("Delete");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Header(menu).ShouldBe("Rename");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Header(menu).ShouldBe("Delete");
            menu.Menu.Classes.Contains(MenuSurface.DangerHighlightPseudoClass).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Header(menu).ShouldBe("Rename");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Header(menu).ShouldBe("Delete");
            window.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None);
            Header(menu).ShouldBe("Duplicate");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            trigger.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Activates_The_Row_Raises_ItemClicked_And_Returns_Focus()
    {
        var menu = CreateMenu();
        object? clicked = null;
        var rowClicks = 0;
        ((MenuEntry)menu.Items[1]!).Click += (_, _) => rowClicks++;
        menu.ItemClicked += (_, e) => clicked = e.Item;
        var window = Show(menu);
        try
        {
            var trigger = Trigger(menu);
            trigger.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicked.ShouldBe(menu.Items[1]);
            rowClicks.ShouldBe(1);
            menu.IsOpen.ShouldBeFalse();
            trigger.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Generates_Rows_And_Reports_The_Data_Item()
    {
        var menu = new DropdownMenu { ItemsSource = new[] { "Newest", "Oldest", "Name" } };
        object? clicked = null;
        menu.ItemClicked += (_, e) => clicked = e.Item;
        var window = Show(menu);
        try
        {
            menu.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var rows = menu.Menu.GetRealizedContainers().OfType<MenuEntry>().ToList();
            rows.Count.ShouldBe(3);
            rows[1].Header.ShouldBe("Oldest");
            rows[1].PerformClick();
            clicked.ShouldBe("Oldest");
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Check_And_Radio_Rows_Toggle()
    {
        var surface = new MenuSurface();
        var wrap = new MenuEntry { Header = "Wrap lines", ToggleType = MenuEntryToggleType.CheckBox, StaysOpenOnClick = true };
        var a = new MenuEntry { Header = "Newest", ToggleType = MenuEntryToggleType.Radio, GroupName = "sort", IsChecked = true };
        var b = new MenuEntry { Header = "Oldest", ToggleType = MenuEntryToggleType.Radio, GroupName = "sort" };
        surface.Items.Add(new MenuEntryLabel { Text = "View" });
        surface.Items.Add(wrap);
        surface.Items.Add(new MenuEntrySeparator());
        surface.Items.Add(a);
        surface.Items.Add(b);
        var anchor = new Button { Content = "View" };
        var window = Show(anchor);
        try
        {
            surface.Open(anchor);
            Dispatcher.UIThread.RunJobs();
            wrap.PerformClick();
            wrap.IsChecked.ShouldBeTrue();
            wrap.IconGeometry.ShouldBe(ArcIcons.Check);
            surface.IsOpen.ShouldBeTrue();
            b.PerformClick();
            b.IsChecked.ShouldBeTrue();
            a.IsChecked.ShouldBeFalse();
            b.ShowsRadioDot.ShouldBeTrue();
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Trigger_Answers_In_Color_And_Turns_The_Chevron()
    {
        var menu = CreateMenu();
        var window = Show(menu);
        try
        {
            var trigger = Trigger(menu);
            var chevron = menu.GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Chevron");
            chevron.RenderTransform!.Value.M11.ShouldBe(1, 0.001);
            menu.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            menu.Menu.IsOpen.ShouldBeTrue();
            TestHelpers.ColorOf(trigger.Background).ShouldBe(ArcTokens.GetColor(menu, DesignTokens.ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(trigger.BorderBrush).ShouldBe(ArcTokens.GetColor(menu, DesignTokens.ArcDesignTokenKind.BorderStrong));
            chevron.RenderTransform!.Value.M11.ShouldBe(-1, 0.001);
            trigger.Bounds.Height.ShouldBe(36);
            menu.Menu.MinWidth.ShouldBe(192);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Trigger_Anchors_The_Menu_Without_Press_Scale()
    {
        var custom = new Button { Content = "More", Variant = ButtonVariant.Ghost };
        var menu = CreateMenu();
        menu.Trigger = custom;
        var window = Show(menu);
        try
        {
            custom.IsEffectivelyVisible.ShouldBeTrue();
            custom.IsPressScaleEnabled.ShouldBeFalse();
            Trigger(menu).IsEffectivelyVisible.ShouldBeFalse();
            custom.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.PlacementTarget.ShouldBe(custom);
            menu.Anchor.ShouldBe(custom);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Attached_Menu_Turns_Any_Button_Into_A_Trigger()
    {
        var chevron = new Button { Content = "More", Variant = ButtonVariant.Ghost };
        var surface = new MenuSurface { Alignment = MenuAlignment.End, AnchorGap = 4 };
        surface.Items.Add(new MenuEntry { Header = "Save as draft" });
        surface.Items.Add(new MenuEntry { Header = "Schedule" });
        DropdownMenu.SetMenu(chevron, surface);
        var window = Show(chevron);
        try
        {
            chevron.IsPressScaleEnabled.ShouldBeFalse();
            chevron.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeTrue();
            (surface.HighlightedEntry?.Header as string).ShouldBe("Schedule");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeFalse();
            chevron.IsFocused.ShouldBeTrue();

            chevron.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeTrue();
            surface.HighlightedEntry.ShouldBeNull();
            chevron.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private static string? Header(DropdownMenu menu) => menu.Menu.HighlightedEntry?.Header as string;
}
