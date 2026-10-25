using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class MobileControlTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Controls_Instantiate_And_Apply_Their_Theme()
    {
        var button = new Button { Content = "Go" };
        var toggle = new Switch();
        var navbar = new Navbar { Title = "Title" };
        var tabbar = new Tabbar { Items = { new TabbarItem { Content = "Home" } } };
        var page = new Page { Header = navbar, Footer = tabbar, Content = new TextBlock { Text = "Body" } };
        var panel = new StackPanel { Children = { button, toggle, page } };
        var window = Show(panel);
        try
        {
            foreach (Avalonia.Controls.Primitives.TemplatedControl control in new Avalonia.Controls.Primitives.TemplatedControl[]
                     { button, toggle, navbar, tabbar, page })
            {
                control.Template.ShouldNotBeNull($"{control.GetType().Name} has no template");
                control.IsLoaded.ShouldBeTrue();
            }
            navbar.Height.ShouldBe(44);
            tabbar.Height.ShouldBe(49);
            tabbar.Items.OfType<TabbarItem>().Single().MinHeight.ShouldBeGreaterThanOrEqualTo(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Button_Has_A_44px_Touch_Target_And_Is_Still_Taller_Than_Content_When_Empty()
    {
        var button = new Button();
        var window = Show(new StackPanel { Children = { button } });
        try
        {
            button.MinWidth.ShouldBeGreaterThanOrEqualTo(Button.MinTouchTarget);
            button.MinHeight.ShouldBeGreaterThanOrEqualTo(Button.MinTouchTarget);
            button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(44);
            button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switch_And_TabbarItem_Have_A_44px_Touch_Target()
    {
        var toggle = new Switch();
        var item = new TabbarItem { Content = "Home" };
        var window = Show(new StackPanel { Children = { toggle, new Tabbar { Items = { item } } } });
        try
        {
            toggle.Bounds.Width.ShouldBeGreaterThanOrEqualTo(44);
            toggle.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
            item.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Button_Pressed_Class_Follows_The_Finger_And_There_Is_No_Hover_Style()
    {
        var button = new Button { Content = "Go" };
        var window = Show(new StackPanel { Children = { button } });
        try
        {
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            button.Classes.Contains(":pressed").ShouldBeTrue();
            window.MouseUp(center, MouseButton.Left);
            button.Classes.Contains(":pressed").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switch_Toggles_On_Click_And_Moves_The_Thumb()
    {
        var toggle = new Switch();
        var window = Show(new StackPanel { Children = { toggle } });
        try
        {
            toggle.IsChecked.ShouldNotBe(true);
            var center = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            toggle.IsChecked.ShouldBe(true);
            toggle.ThumbMargin.Left.ShouldBe(20);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            toggle.IsChecked.ShouldBe(false);
            toggle.ThumbMargin.Left.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Pads_Its_Content_By_The_Safe_Area_And_Platform_Safe_Area_Is_Zero_On_Desktop()
    {
        var page = new Page { Content = new Border() };
        var window = Show(page);
        try
        {
            // Headless has no insets manager: the platform reports no safe area.
            page.SafeArea.ShouldBe(default(Thickness));
            page.UsePlatformSafeArea = false;
            page.SafeArea = new Thickness(0, 47, 0, 34);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var content = page.Content.ShouldBeOfType<Border>();
            content.TranslatePoint(default, window)!.Value.Y.ShouldBe(47);
            page.Content.ShouldBeOfType<Border>().Bounds.Height.ShouldBe(600 - 47 - 34);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabbar_Selects_The_Tapped_Item()
    {
        var first = new TabbarItem { Content = "A" };
        var second = new TabbarItem { Content = "B" };
        var tabbar = new Tabbar { Items = { first, second } };
        var window = Show(tabbar);
        try
        {
            var point = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            tabbar.SelectedItem.ShouldBe(second);
            second.Classes.Contains(":selected").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Button_Animates_Under_Full_Motion_Only()
    {
        var full = new Button { Content = "Go" };
        var off = new Button { Content = "Go" };
        var fullWindow = Show(full, ArcMotionLevel.Full);
        var offWindow = Show(off, ArcMotionLevel.Off);
        try
        {
            full.Classes.Contains(ArcMotion.FullPseudoClass).ShouldBeTrue();
            off.Classes.Contains(ArcMotion.FullPseudoClass).ShouldBeFalse();
            off.Classes.Contains(ArcMotion.ReducedPseudoClass).ShouldBeFalse();
        }
        finally
        {
            fullWindow.Close();
            offWindow.Close();
        }
    }
}
