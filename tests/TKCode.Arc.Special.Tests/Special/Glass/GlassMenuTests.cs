using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc.Tests.Special.Glass;

/// <summary>
/// The glass menu: the highlight follows the pointer and the keyboard, the keyboard model (wrap, disabled rows, typeahead),
/// invoking commands, the popup opened from a button, and the automation tree.
/// </summary>
public class GlassMenuTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static GlassMenu Menu() => new()
    {
        Items =
        {
            new GlassMenuItem { Content = "New note", Shortcut = "Ctrl+N" },
            new GlassMenuItem { Content = "Duplicate" },
            new GlassMenuItem { Content = "Pin", IsEnabled = false },
            new GlassMenuSeparator(),
            new GlassMenuItem { Content = "Delete", IsDestructive = true },
        }
    };

    private static GlassMenuItem Row(GlassMenu menu, int index) => (GlassMenuItem)menu.ContainerFromIndex(index)!;

    private static Point Center(Visual target, Window window) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;

    private static Rect RowRect(GlassMenu menu, int index)
    {
        var row = Row(menu, index);
        return new Rect(row.TranslatePoint(default, menu.Highlight!)!.Value, row.Bounds.Size);
    }

    [AvaloniaFact]
    public void The_highlight_follows_the_pointer_and_leaves_with_it()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        Pump();
        menu.Highlight!.IsShown.ShouldBeFalse();
        window.MouseMove(Center(Row(menu, 1), window), RawInputModifiers.None);
        Pump();
        menu.HighlightedItem.ShouldBe(Row(menu, 1));
        Row(menu, 1).IsHighlighted.ShouldBeTrue();
        menu.Highlight.CurrentRect.ShouldBe(RowRect(menu, 1));
        menu.Highlight.CurrentOpacity.ShouldBe(1);
        // A disabled row does not take the highlight.
        window.MouseMove(Center(Row(menu, 2), window), RawInputModifiers.None);
        Pump();
        menu.HighlightedItem.ShouldBe(Row(menu, 1));
        window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        Pump();
        menu.HighlightedItem.ShouldBeNull();
        menu.Highlight.CurrentOpacity.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void At_full_motion_the_highlight_glides_and_the_glow_follows_the_pointer()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu();
        var window = TestHelpers.Show(menu, ArcMotionLevel.Full);
        Pump();
        window.MouseMove(Center(Row(menu, 0), window), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        menu.Highlight!.CurrentRect.ShouldBe(RowRect(menu, 0));
        window.MouseMove(Center(Row(menu, 1), window), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        var mid = menu.Highlight.CurrentRect.Y;
        mid.ShouldBeGreaterThan(RowRect(menu, 0).Y);
        mid.ShouldBeLessThan(RowRect(menu, 1).Y);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        menu.Highlight.CurrentRect.Y.ShouldBe(RowRect(menu, 1).Y, 0.5);
        menu.Highlight.CurrentGlow.ShouldBe(1, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrows_wrap_and_skip_disabled_rows_and_letters_jump()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        Pump();
        menu.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Row(menu, 0).IsFocused.ShouldBeTrue();
        menu.HighlightedItem.ShouldBe(Row(menu, 0));
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Row(menu, 4).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Row(menu, 0).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Row(menu, 4).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Row(menu, 0).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Row(menu, 4).IsFocused.ShouldBeTrue();
        window.KeyTextInput("d");
        Row(menu, 1).IsFocused.ShouldBeTrue();
        window.KeyTextInput("d");
        Row(menu, 4).IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Invoking_a_row_runs_its_command_and_reports_the_item()
    {
        var menu = new GlassMenu { ItemsSource = new[] { "Name", "Date", "Size" } };
        GlassMenuItemInvokedEventArgs? invoked = null;
        menu.ItemInvoked += (_, e) => invoked = e;
        var window = TestHelpers.Show(menu);
        Pump();
        var point = Center(Row(menu, 2), window);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
        invoked.ShouldNotBeNull();
        invoked.Item.ShouldBe("Size");
        invoked.Container.ShouldBe(Row(menu, 2));

        invoked = null;
        Row(menu, 0).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        invoked.ShouldNotBeNull();
        invoked.Item.ShouldBe("Name");
        window.Close();
    }

    [AvaloniaFact]
    public void A_button_opens_the_menu_as_a_popup_that_closes_on_a_command_and_on_escape()
    {
        var menu = Menu();
        var button = new ArcButton { Content = "Arrange" };
        GlassMenu.SetMenu(button, menu);
        var scene = new Border { Background = Brushes.Teal };
        var root = new Panel { Children = { scene, button } };
        GlassBackdrop.SetSource(root, scene);
        var window = TestHelpers.Show(root);
        Pump();

        var point = Center(button, window);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
        menu.IsOpen.ShouldBeTrue();
        menu.IsVisible.ShouldBeTrue();
        TopLevel.GetTopLevel(menu).ShouldNotBeNull();
        // The popup inherits the backdrop of the page it opens from.
        GlassBackdrop.GetSource(menu).ShouldBe(scene);
        menu.Pane!.IsFrosted.ShouldBeTrue();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        menu.IsOpen.ShouldBeFalse();
        button.IsFocused.ShouldBeTrue();

        // From the keyboard: Down opens with the first command focused; Enter runs it and closes.
        var invoked = 0;
        menu.ItemInvoked += (_, _) => invoked++;
        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump();
        menu.IsOpen.ShouldBeTrue();
        Row(menu, 0).IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        invoked.ShouldBe(1);
        menu.IsOpen.ShouldBeFalse();
        button.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_menu_in_the_page_cannot_open_as_a_popup()
    {
        var menu = Menu();
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        Should.Throw<InvalidOperationException>(() => menu.Open(menu));
        window.Close();
    }

    [AvaloniaFact]
    public void Automation_is_a_menu_of_menu_items()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        Pump();
        ControlAutomationPeer.CreatePeerForElement(menu).GetAutomationControlType().ShouldBe(AutomationControlType.Menu);
        var row = ControlAutomationPeer.CreatePeerForElement(Row(menu, 0));
        row.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
        row.GetName().ShouldBe("New note");
        row.GetAcceleratorKey().ShouldBe("Ctrl+N");
        ControlAutomationPeer.CreatePeerForElement(Row(menu, 4)).GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
        Row(menu, 4).Classes.ShouldContain(":destructive");
        window.Close();
    }
}
