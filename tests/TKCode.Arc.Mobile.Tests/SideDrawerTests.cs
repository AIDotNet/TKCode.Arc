using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

public class SideDrawerTests
{
    private static RoutedEventArgs RequestBack(TopLevel topLevel)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        topLevel.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();
        return args;
    }
    [AvaloniaFact]
    public void Opens_Inherits_SafeArea_And_Dismisses_On_System_Back()
    {
        var drawer = new SideDrawer
        {
            DrawerWidth = 280,
            Content = new TextBlock { Text = "Menu" },
        };
        var opened = 0;
        var closed = 0;
        drawer.Opened += (_, _) => opened++;
        drawer.Closed += (_, _) => closed++;

        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(12, 44, 12, 34),
            Content = new TextBlock { Text = "Body" },
            Overlay = drawer,
        };
        var window = new Window { Width = 390, Height = 844, Content = page };
        window.Show();
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            opened.ShouldBe(1);
            drawer.IsHitTestVisible.ShouldBeTrue();
            var safe = drawer.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Safe");
            safe.Padding.ShouldBe(new Thickness(12, 44, 0, 34));

            // Android system back dismisses the open drawer.
            RequestBack(window).Handled.ShouldBeTrue();
            drawer.IsOpen.ShouldBeFalse();
            closed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_Side_And_Non_Dismissible_Back_Absorption_Work()
    {
        var drawer = new SideDrawer
        {
            Side = SideDrawerSide.Right,
            IsDismissible = false,
            DrawerWidth = 400, // Clamped on narrow viewport
        };
        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(0, 24, 16, 20),
            Content = new TextBlock { Text = "Body" },
            Overlay = drawer,
        };
        var window = new Window { Width = 360, Height = 640, Content = page };
        window.Show();
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            drawer.Classes.Contains(":right").ShouldBeTrue();
            var panel = drawer.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Panel");
            panel.Width.ShouldBeLessThanOrEqualTo(360 * 0.85 + 0.01);

            // Non-dismissible drawer absorbs back without closing.
            RequestBack(window).Handled.ShouldBeTrue();
            drawer.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
