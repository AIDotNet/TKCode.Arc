using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Overlays;

/// <summary>
/// Focus going back to the opener when a menu, popover or dialog closes: the keyboard ring only when a key closed it,
/// even if the opener had the ring before (a mouse close after a keyboard open).
/// </summary>
public class OverlayFocusTests
{
    private static DropdownMenu Menu()
    {
        var menu = new DropdownMenu { Label = "Actions" };
        menu.Items.Add(new MenuEntry { Header = "Rename" });
        menu.Items.Add(new MenuEntry { Header = "Duplicate" });
        menu.Items.Add(new MenuEntry { Header = "Delete" });
        return menu;
    }

    private static Avalonia.Controls.Button MenuTrigger(DropdownMenu menu) =>
        menu.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == DropdownMenu.TriggerPart);

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { control } });
        window.Width = 600;
        window.Height = 500;
        Pump();
        return window;
    }

    [AvaloniaFact]
    public void DropdownMenu_Pointer_Pick_Returns_Focus_Without_The_Ring()
    {
        var menu = Menu();
        var window = Show(menu);
        try
        {
            var trigger = MenuTrigger(menu);
            Click(window, trigger);
            menu.IsOpen.ShouldBeTrue();
            Click(window, (MenuEntry)menu.Items[1]!);
            menu.IsOpen.ShouldBeFalse();
            trigger.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DropdownMenu_Keyboard_Pick_Returns_Focus_With_The_Ring()
    {
        var menu = Menu();
        var window = Show(menu);
        try
        {
            var trigger = MenuTrigger(menu);
            trigger.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            menu.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            menu.IsOpen.ShouldBeFalse();
            trigger.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    private static (Popover Popover, Button Trigger, Button Done) PopoverWithDone()
    {
        var trigger = new Button { Content = "Share" };
        var done = new Button { Content = "Done" };
        var popover = new Popover { Trigger = trigger, Content = new StackPanel { Children = { done } } };
        done.Click += (_, _) => popover.IsOpen = false;
        return (popover, trigger, done);
    }

    [AvaloniaFact]
    public void Popover_Closed_By_A_Click_Inside_Returns_Focus_Without_The_Ring()
    {
        var (popover, trigger, done) = PopoverWithDone();
        var window = Show(popover);
        try
        {
            Click(window, trigger);
            popover.IsOpen.ShouldBeTrue();
            Click(window, done);
            popover.IsOpen.ShouldBeFalse();
            trigger.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Popover_Closed_With_A_Key_Returns_Focus_With_The_Ring()
    {
        var (popover, trigger, done) = PopoverWithDone();
        var window = Show(popover);
        try
        {
            trigger.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            popover.IsOpen.ShouldBeTrue();
            done.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            popover.IsOpen.ShouldBeFalse();
            trigger.ShouldBeFocusedWithRing();

            Press(window, PhysicalKey.Enter);
            popover.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            popover.IsOpen.ShouldBeFalse();
            trigger.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    private static (Dialog Dialog, Button Opener, Button Cancel) DialogWithCancel()
    {
        var opener = new Button { Content = "Open" };
        var cancel = new Button { Content = "Cancel" };
        Dialog.SetResult(cancel, false);
        var dialog = new Dialog { Title = "Rename project", Content = new TextBlock { Text = "Pick a name." }, Actions = cancel };
        opener.Click += (_, _) => dialog.IsOpen = true;
        return (dialog, opener, cancel);
    }

    [AvaloniaFact]
    public void Dialog_Closed_By_A_Click_Returns_Focus_Without_The_Ring_Even_After_A_Keyboard_Open()
    {
        var (dialog, opener, cancel) = DialogWithCancel();
        var window = TestHelpers.Show(new StackPanel { Children = { opener, dialog } });
        window.Width = 600;
        window.Height = 500;
        try
        {
            opener.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            dialog.IsOpen.ShouldBeTrue();
            Click(window, cancel);
            dialog.IsOpen.ShouldBeFalse();
            opener.ShouldBeFocusedWithoutRing();

            Click(window, opener);
            dialog.IsOpen.ShouldBeTrue();
            // Opened with a click: the dialog's first control takes focus without the ring.
            var inside = TopLevel.GetTopLevel(opener)!.FocusManager!.GetFocusedElement().ShouldBeAssignableTo<StyledElement>()!;
            dialog.Presence!.Contains((IInputElement)inside).ShouldBeTrue();
            HasRing(inside).ShouldBeFalse();
            Click(window, cancel);
            opener.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dialog_Closed_With_A_Key_Returns_Focus_With_The_Ring()
    {
        var (dialog, opener, cancel) = DialogWithCancel();
        var window = TestHelpers.Show(new StackPanel { Children = { opener, dialog } });
        window.Width = 600;
        window.Height = 500;
        try
        {
            Click(window, opener);
            dialog.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            dialog.IsOpen.ShouldBeFalse();
            opener.ShouldBeFocusedWithRing();

            opener.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            dialog.IsOpen.ShouldBeTrue();
            cancel.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            dialog.IsOpen.ShouldBeFalse();
            opener.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
