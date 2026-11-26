using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Mobile platform compatibility tests: system back navigation (<see cref="TopLevel.BackRequestedEvent"/>) priority
/// across overlays and navigation stacks, and soft-keyboard avoidance for <see cref="Sheet"/> and <see cref="Toast"/>.
/// </summary>
public class MobileCompatibilityTests
{
    private static RoutedEventArgs RequestBack(TopLevel topLevel)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        topLevel.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();
        return args;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void System_Back_Pops_NavigationStack_And_Leaves_Root_Unhandled()
    {
        var root = new Page { Content = new TextBlock { Text = "Root" } };
        var detail = new Page { Content = new TextBlock { Text = "Detail" } };
        var stack = new NavigationStack { Root = root };
        var window = new Window { Content = stack, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            stack.Push(detail);
            Settle(window);
            stack.Depth.ShouldBe(2);

            RequestBack(window).Handled.ShouldBeTrue();
            stack.Depth.ShouldBe(1);
            stack.Top.ShouldBeSameAs(root);

            // At the root page, system back is not absorbed so the platform can leave the app.
            RequestBack(window).Handled.ShouldBeFalse();
            stack.Depth.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void System_Back_Dismisses_Sheet_And_Dialog_Before_Popping_NavigationStack()
    {
        var sheet = new Sheet { Content = new TextBlock { Text = "Sheet" } };
        var dialog = new Dialog { Title = "Confirm", IsDismissible = true };
        var overlays = new Grid { Children = { sheet, dialog } };
        var detail = new Page { Content = new TextBlock { Text = "Detail" }, Overlay = overlays };
        var root = new Page { Content = new TextBlock { Text = "Root" } };
        var stack = new NavigationStack { Root = root };
        var window = new Window { Content = stack, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            stack.Push(detail);
            Settle(window);

            sheet.IsOpen = true;
            Settle(window);
            dialog.IsOpen = true;
            Settle(window);

            // Dialog opened last: system back dismisses it first, leaving Sheet and NavigationStack intact.
            RequestBack(window).Handled.ShouldBeTrue();
            dialog.IsOpen.ShouldBeFalse();
            sheet.IsOpen.ShouldBeTrue();
            stack.Depth.ShouldBe(2);

            // Next back dismisses Sheet before touching the page stack.
            RequestBack(window).Handled.ShouldBeTrue();
            sheet.IsOpen.ShouldBeFalse();
            stack.Depth.ShouldBe(2);

            // Next back pops the detail page.
            RequestBack(window).Handled.ShouldBeTrue();
            stack.Depth.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Non_Dismissible_Dialog_Or_Sheet_Blocks_System_Back_From_Popping_Underlying_Page()
    {
        var dialog = new Dialog { Title = "Required", IsDismissible = false };
        var sheet = new Sheet { Content = new TextBlock { Text = "Required" }, IsDismissible = false };
        var detail = new Page
        {
            Content = new TextBlock { Text = "Detail" },
            Overlay = new Grid { Children = { sheet, dialog } },
        };
        var root = new Page { Content = new TextBlock { Text = "Root" } };
        var stack = new NavigationStack { Root = root };
        var window = new Window { Content = stack, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            stack.Push(detail);
            Settle(window);

            dialog.IsOpen = true;
            Settle(window);
            RequestBack(window).Handled.ShouldBeTrue();
            dialog.IsOpen.ShouldBeTrue();
            stack.Depth.ShouldBe(2);

            dialog.IsOpen = false;
            sheet.IsOpen = true;
            Settle(window);
            RequestBack(window).Handled.ShouldBeTrue();
            sheet.IsOpen.ShouldBeTrue();
            stack.Depth.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void System_Back_Closes_Open_Picker_Sheet()
    {
        var picker = new Picker { Content = "Theme", Items = { "Light", "Dark" } };
        var page = new Page { Content = new ListGroup { Items = { picker } } };
        var window = new Window { Content = page, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            picker.IsOpen = true;
            Settle(window);
            picker.IsOpen.ShouldBeTrue();

            RequestBack(window).Handled.ShouldBeTrue();
            picker.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sheet_Lifts_Above_The_Soft_Keyboard_And_Restores_SafeArea_When_Closed()
    {
        var source = new KeyboardInsetSource();
        var sheet = new Sheet { Content = new Border { Height = 160 } };
        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(0, 40, 0, 34),
            Content = new Border(),
            Overlay = sheet,
        };
        var window = new Window { Content = page, Width = 390, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        Page.SetKeyboardInsetSource(window, source);
        window.Show();
        try
        {
            Settle(window);
            sheet.IsOpen = true;
            Settle(window);

            var panel = sheet.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Panel");
            var safeArea = sheet.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_SafeArea");
            panel.Margin.Bottom.ShouldBe(0);
            safeArea.IsVisible.ShouldBeTrue();
            sheet.EffectiveSafeAreaBottom.ShouldBe(34);

            source.Set(260);
            Settle(window);
            panel.Margin.Bottom.ShouldBe(260);
            safeArea.IsVisible.ShouldBeFalse();
            // The panel stays above the keyboard and its bottom sits at or above the keyboard top edge.
            var panelBottom = panel.TranslatePoint(new Point(0, panel.Bounds.Height), window)!.Value.Y;
            panelBottom.ShouldBeLessThanOrEqualTo(600 - 260 + 0.5);

            source.Set(0);
            Settle(window);
            panel.Margin.Bottom.ShouldBe(0);
            safeArea.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toast_Lifts_Above_The_Soft_Keyboard()
    {
        var source = new KeyboardInsetSource();
        var toast = new Toast { BottomOffset = 16 };
        var footer = new Border { Height = 50 };
        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(0, 40, 0, 34),
            Footer = footer,
            Content = new Border(),
            Overlay = toast,
        };
        var window = new Window { Content = page, Width = 390, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        Page.SetKeyboardInsetSource(window, source);
        window.Show();
        try
        {
            Settle(window);
            toast.Show("Saved", TimeSpan.Zero);
            Settle(window);

            var stack = toast.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == "PART_Stack");
            stack.Margin.Bottom.ShouldBe(16 + 34 + 50, 0.5);

            source.Set(280);
            Settle(window);
            stack.Margin.Bottom.ShouldBe(16 + 280, 0.5);

            source.Set(0);
            Settle(window);
            stack.Margin.Bottom.ShouldBe(16 + 34 + 50, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
