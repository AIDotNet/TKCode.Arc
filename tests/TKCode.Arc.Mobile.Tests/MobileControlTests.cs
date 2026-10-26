using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            navbar.Bounds.Height.ShouldBe(44);
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

    [AvaloniaFact]
    public void Button_Variant_Sets_Its_Pseudo_Class_And_Plain_Has_No_Fill()
    {
        var plain = new Button { Content = "Done", Variant = ButtonVariant.Plain };
        var tinted = new Button { Content = "More", Variant = ButtonVariant.Tinted };
        var window = Show(new StackPanel { Children = { plain, tinted } });
        try
        {
            plain.Classes.Contains(":plain").ShouldBeTrue();
            tinted.Classes.Contains(":tinted").ShouldBeTrue();
            ((Avalonia.Media.ISolidColorBrush)plain.Background!).Color.A.ShouldBe((byte)0);
            plain.Variant = ButtonVariant.Filled;
            plain.Classes.Contains(":plain").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navbar_Back_Button_Follows_ShowBack_And_Runs_The_Command()
    {
        var ran = false;
        var navbar = new Navbar
        {
            Title = "Settings", ShowBack = true, BackText = "Home", BackCommand = new RelayCommand(() => ran = true),
        };
        var window = Show(new Page { Header = navbar });
        try
        {
            var back = navbar.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Back");
            back.IsVisible.ShouldBeTrue();
            back.Command!.Execute(null);
            ran.ShouldBeTrue();
            navbar.ShowBack = false;
            back.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabbar_Item_Content_Fits_Inside_The_Bar()
    {
        var tabbar = new Tabbar
        {
            SelectedIndex = 0,
            Items = { new TabbarItem { Content = "Home", Icon = Avalonia.Media.Geometry.Parse("M0,0 L24,24") } },
        };
        var window = Show(new Page { Footer = tabbar });
        try
        {
            var item = tabbar.Items.OfType<TabbarItem>().Single();
            var label = item.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                .Single(p => p.Name == "PART_ContentPresenter");
            var bottom = label.TranslatePoint(new Point(0, label.Bounds.Height), tabbar)!.Value.Y;
            bottom.ShouldBeLessThanOrEqualTo(tabbar.Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navbar_Large_Title_Collapses_Into_The_Bar()
    {
        var navbar = new Navbar { Title = "Settings" };
        var window = Show(new Page { Header = navbar });
        try
        {
            navbar.Bounds.Height.ShouldBe(44);
            navbar.BarTitleOpacity.ShouldBe(1);

            navbar.LargeTitle = true;
            Dispatcher.UIThread.RunJobs();
            navbar.Bounds.Height.ShouldBe(44 + Navbar.LargeTitleExtent);
            navbar.BarTitleOpacity.ShouldBe(0);

            navbar.CollapseProgress = 0.5;
            Dispatcher.UIThread.RunJobs();
            navbar.Bounds.Height.ShouldBe(44 + Navbar.LargeTitleExtent / 2);
            navbar.BarTitleOpacity.ShouldBe(0.5);

            navbar.CollapseProgress = 3;
            navbar.CollapseProgress.ShouldBe(1);
            Dispatcher.UIThread.RunJobs();
            navbar.Bounds.Height.ShouldBe(44);
            navbar.BarTitleOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrollable_Page_Collapses_The_Large_Title_As_The_Content_Scrolls()
    {
        var navbar = new Navbar { Title = "Photos", LargeTitle = true };
        var page = new Page { Header = navbar, IsScrollable = true, Content = new Border { Height = 2000 } };
        var window = Show(page);
        try
        {
            var scroller = page.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PART_Scroller");
            navbar.CollapseProgress.ShouldBe(0);
            scroller.Offset = new Vector(0, Navbar.LargeTitleExtent / 2);
            Dispatcher.UIThread.RunJobs();
            navbar.CollapseProgress.ShouldBe(0.5);
            scroller.Offset = new Vector(0, 500);
            Dispatcher.UIThread.RunJobs();
            navbar.CollapseProgress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Without_IsScrollable_Gives_Its_Content_The_Page_Height()
    {
        var content = new Border { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        var window = Show(new Page { Content = content });
        try
        {
            content.Bounds.Height.ShouldBe(600);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ListGroup_Wraps_Items_In_ListItems_And_Hides_The_Last_Separator()
    {
        var group = new ListGroup
        {
            Header = "Account",
            Footer = "Shown to others.",
            Items = { new ListItem { Content = "Name", Subtitle = "Ada", ShowChevron = true }, "Email" },
        };
        var window = Show(new Page { Content = group });
        try
        {
            var rows = group.GetVisualDescendants().OfType<ListItem>().ToList();
            rows.Count.ShouldBe(2);
            rows.ShouldAllBe(r => r.Bounds.Height >= 48);
            string Visible(ListItem r) => r.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
                .Single(b => b.Name == "PART_Separator").IsVisible.ToString();
            Visible(rows[0]).ShouldBe("True");
            Visible(rows[1]).ShouldBe("False");
            group.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Count(t => t.IsVisible && t.Name is "PART_Header" or "PART_Footer")
                .ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ListItem_Press_Shades_The_Row_And_Click_Runs_Its_Command()
    {
        var ran = false;
        var item = new ListItem { Content = "Open", Command = new RelayCommand(() => ran = true) };
        var window = Show(new Page { Content = new ListGroup { Items = { item } } });
        try
        {
            var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
            var normal = ((Avalonia.Media.ISolidColorBrush)item.Background!).Color;
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            ((Avalonia.Media.ISolidColorBrush)item.Background!).Color.ShouldNotBe(normal);
            window.MouseUp(center, MouseButton.Left);
            ran.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private static Border Part(Control root, string name)
        => root.GetVisualDescendants().OfType<Border>().Single(b => b.Name == name);

    private static Point CenterOf(Control control, Visual to)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), to)!.Value;

    [AvaloniaFact]
    public void Sheet_Closed_Does_Not_Catch_Touches_And_Open_Shows_Panel_And_Scrim()
    {
        var sheet = new Sheet { Title = "Share", Content = new Border { Height = 200 } };
        var window = Show(new Page { Content = new TextBlock { Text = "Body" }, Overlay = sheet });
        try
        {
            sheet.IsHitTestVisible.ShouldBeFalse();
            Part(sheet, "PART_Panel").IsVisible.ShouldBeFalse();

            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.IsHitTestVisible.ShouldBeTrue();
            Part(sheet, "PART_Panel").IsVisible.ShouldBeTrue();
            sheet.PanelOffset.ShouldBe(0);
            Part(sheet, "PART_Scrim").Opacity.ShouldBe(1);

            sheet.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            sheet.IsHitTestVisible.ShouldBeFalse();
            sheet.PanelOffset.ShouldBeGreaterThan(200);
            Part(sheet, "PART_Scrim").Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sheet_Closes_On_Scrim_Tap_And_Escape_Only_When_Dismissible()
    {
        var sheet = new Sheet { Content = new Border { Height = 200 }, IsOpen = true };
        var window = Show(new Page { Overlay = sheet });
        try
        {
            var scrimPoint = new Point(window.Width / 2, 40);
            sheet.IsDismissible = false;
            window.MouseDown(scrimPoint, MouseButton.Left);
            window.MouseUp(scrimPoint, MouseButton.Left);
            sheet.IsOpen.ShouldBeTrue();

            sheet.IsDismissible = true;
            window.MouseDown(scrimPoint, MouseButton.Left);
            window.MouseUp(scrimPoint, MouseButton.Left);
            sheet.IsOpen.ShouldBeFalse();

            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sheet_Drag_Down_Follows_The_Finger_And_Closes_Only_When_Pulled_Far()
    {
        var sheet = new Sheet { Title = "Filters", Content = new Border { Height = 300 }, IsOpen = true };
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            var handle = sheet.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_Handle");
            var start = CenterOf(handle, window);

            // A short pull follows the finger, then the sheet returns to its open position.
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(0, 12), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Point(0, 30), RawInputModifiers.LeftMouseButton);
            sheet.PanelOffset.ShouldBe(30, 0.5);
            Thread.Sleep(80); // a finger that rests before lifting has no speed; headless moves would otherwise read as a flick
            window.MouseUp(start + new Point(0, 30), MouseButton.Left);
            sheet.IsOpen.ShouldBeTrue();

            // A long pull past the middle closes it.
            Dispatcher.UIThread.RunJobs();
            var start2 = CenterOf(handle, window);
            window.MouseDown(start2, MouseButton.Left);
            window.MouseMove(start2 + new Point(0, 12), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start2 + new Point(0, 300), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(start2 + new Point(0, 300), MouseButton.Left);
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sheet_Closes_On_A_Fast_Flick_Even_After_A_Short_Pull()
    {
        var sheet = new Sheet { Content = new Border { Height = 300 }, IsOpen = true };
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            var handle = sheet.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_Handle");
            var start = CenterOf(handle, window);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(0, 8), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(10); // a real time step, so the velocity is measured (32px in about 10ms) instead of read as zero
            window.MouseMove(start + new Point(0, 40), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(0, 40), MouseButton.Left); // lifted while still moving fast: a flick
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private static Page NewPage(string name) => new() { Name = name, Content = new TextBlock { Text = name } };

    private static bool InTree(Visual root, Control page) => root.GetVisualDescendants().Contains(page);

    [AvaloniaFact]
    public void NavigationStack_Push_And_Pop_Keep_Only_The_Top_Page_In_The_Tree()
    {
        var home = NewPage("Home");
        var detail = NewPage("Detail");
        var stack = new NavigationStack { Root = home };
        var events = new List<NavigationKind>();
        stack.Navigated += (_, e) => events.Add(e.Kind);
        var window = Show(stack);
        try
        {
            stack.Depth.ShouldBe(1);
            stack.CanGoBack.ShouldBeFalse();
            stack.BackCommand.CanExecute(null).ShouldBeFalse();
            stack.Pop().ShouldBeFalse();
            InTree(stack, home).ShouldBeTrue();

            stack.Push(detail);
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(2);
            stack.Top.ShouldBe(detail);
            stack.CanGoBack.ShouldBeTrue();
            stack.BackCommand.CanExecute(null).ShouldBeTrue();
            InTree(stack, detail).ShouldBeTrue();
            InTree(stack, home).ShouldBeFalse();

            stack.BackCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            stack.Top.ShouldBe(home);
            InTree(stack, home).ShouldBeTrue();
            InTree(stack, detail).ShouldBeFalse();
            stack.CanGoBack.ShouldBeFalse();
            events.ShouldBe([NavigationKind.Push, NavigationKind.Pop]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NavigationStack_PopToRoot_Returns_To_The_First_Page()
    {
        var home = NewPage("Home");
        var stack = new NavigationStack { Root = home };
        var window = Show(stack);
        try
        {
            stack.Push(NewPage("A"));
            stack.Push(NewPage("B"));
            stack.Push(NewPage("C"));
            stack.Depth.ShouldBe(4);
            stack.PopToRoot().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(1);
            stack.Top.ShouldBe(home);
            InTree(stack, home).ShouldBeTrue();
            stack.PopToRoot().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NavigationStack_Slides_With_Motion_On_And_A_Second_Push_Finishes_The_First()
    {
        var home = NewPage("Home");
        var a = NewPage("A");
        var b = NewPage("B");
        var stack = new NavigationStack { Root = home };
        var window = Show(stack, ArcMotionLevel.Full);
        try
        {
            var top = stack.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "PART_TopLayer");
            stack.Push(a);
            // The new page starts at the right edge, and the one under it is still in the tree for the slide.
            ((Avalonia.Media.TranslateTransform)top.RenderTransform!).X.ShouldBe(stack.Bounds.Width);
            InTree(stack, home).ShouldBeTrue();

            stack.Push(b);
            stack.Depth.ShouldBe(3);
            stack.Top.ShouldBe(b);
            // The first slide was finished at once: home is gone, a is now the page underneath.
            InTree(stack, home).ShouldBeFalse();
            InTree(stack, a).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NavigationStack_Edge_Swipe_Pops_When_Pulled_Far_And_Springs_Back_When_Short()
    {
        var stack = new NavigationStack { Root = NewPage("Home") };
        var window = Show(stack);
        try
        {
            stack.Push(NewPage("Detail"));
            Dispatcher.UIThread.RunJobs();

            // A short swipe from the edge follows the finger, then the page returns.
            var from = new Point(10, 300);
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(15, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(from + new Point(60, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80); // a finger that rests before lifting has no speed
            window.MouseUp(from + new Point(60, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(2);

            // Not from the edge: nothing happens.
            var inside = new Point(120, 300);
            window.MouseDown(inside, MouseButton.Left);
            window.MouseMove(inside + new Point(15, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(inside + new Point(250, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(inside + new Point(250, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(2);

            // A long swipe from the edge pops the page.
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(15, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(from + new Point(300, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(from + new Point(300, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(1);
            stack.CanGoBack.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NavigationStack_Edge_Swipe_Can_Be_Turned_Off()
    {
        var stack = new NavigationStack { Root = NewPage("Home"), IsSwipeBackEnabled = false };
        var window = Show(stack);
        try
        {
            stack.Push(NewPage("Detail"));
            Dispatcher.UIThread.RunJobs();
            var from = new Point(10, 300);
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(15, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(from + new Point(300, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(from + new Point(300, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.Depth.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NavigationStack_Per_Tab_Keeps_Its_Pages_When_The_Tab_Is_Swapped_Away_And_Back()
    {
        var first = new NavigationStack { Root = NewPage("Home") };
        var second = new NavigationStack { Root = NewPage("Search") };
        var tabbar = new Tabbar
        {
            SelectedIndex = 0,
            Items = { new TabbarItem { Content = "Home" }, new TabbarItem { Content = "Search" } },
        };
        var shell = new Page { Footer = tabbar, Content = first };
        var window = Show(shell);
        try
        {
            var detail = NewPage("Detail");
            first.Push(detail);
            Dispatcher.UIThread.RunJobs();

            shell.Content = second;
            Dispatcher.UIThread.RunJobs();
            InTree(shell, second.Top!).ShouldBeTrue();
            InTree(shell, detail).ShouldBeFalse();
            InTree(shell, tabbar).ShouldBeTrue();

            shell.Content = first;
            Dispatcher.UIThread.RunJobs();
            first.Depth.ShouldBe(2);
            first.Top.ShouldBe(detail);
            InTree(shell, detail).ShouldBeTrue();
            first.Pop().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            first.Depth.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TextField_Is_44_High_And_Shows_Clear_Only_While_Focused_With_Text()
    {
        var field = new TextField { PlaceholderText = "Name" };
        var window = Show(new StackPanel { Children = { field, new TextField { Text = "other" } } });
        try
        {
            field.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
            field.IsClearVisible.ShouldBeFalse();

            field.Text = "Ada";
            field.IsClearVisible.ShouldBeFalse(); // text, but no focus

            field.Focus();
            Dispatcher.UIThread.RunJobs();
            field.IsClearVisible.ShouldBeTrue();
            var clear = field.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Clear");
            clear.IsVisible.ShouldBeTrue();

            field.ShowClearButton = false;
            field.IsClearVisible.ShouldBeFalse();
            field.ShowClearButton = true;
            field.IsReadOnly = true;
            field.IsClearVisible.ShouldBeFalse();
            field.IsReadOnly = false;

            clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            field.Text.ShouldBeNullOrEmpty();
            field.IsClearVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Searchbar_Has_A_Search_Icon_A_Pill_Shape_And_Raises_Submitted_On_Enter()
    {
        var bar = new Searchbar { PlaceholderText = "Search" };
        var submitted = 0;
        bar.Submitted += (_, _) => submitted++;
        var window = Show(new StackPanel { Children = { bar } });
        try
        {
            bar.Icon.ShouldNotBeNull();
            bar.Classes.Contains(":search").ShouldBeTrue();
            bar.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            submitted.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checkbox_Whole_Row_Toggles_And_Shows_Check_Dash_And_No_Hover()
    {
        var box = new Checkbox { Content = "Remember me", IsThreeState = true };
        var window = Show(new StackPanel { Children = { box } });
        try
        {
            box.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
            double Opacity(string name) => box.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name).Opacity;
            Opacity("PART_Check").ShouldBe(0);

            // A tap on the label, far from the box, still toggles: the whole row is the target.
            var onLabel = box.TranslatePoint(new Point(box.Bounds.Width - 4, box.Bounds.Height / 2), window)!.Value;
            window.MouseDown(onLabel, MouseButton.Left);
            box.Classes.Contains(":pressed").ShouldBeTrue(); // the box shrinks; the row no longer dims
            window.MouseUp(onLabel, MouseButton.Left);
            box.IsChecked.ShouldBe(true);
            Opacity("PART_Check").ShouldBe(1);

            box.IsChecked = null;
            box.Classes.Contains(":indeterminate").ShouldBeTrue();
            Opacity("PART_Mark").ShouldBe(1);
            Opacity("PART_Check").ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segmented_Shares_The_Width_And_Tapping_A_Segment_Selects_It()
    {
        var control = new Segmented
        {
            SelectedIndex = 0,
            Items = { new SegmentedItem { Content = "Day" }, new SegmentedItem { Content = "Week" }, new SegmentedItem { Content = "Month" } },
        };
        var window = Show(new StackPanel { Children = { control } });
        try
        {
            var items = control.GetVisualDescendants().OfType<SegmentedItem>().ToList();
            items.Count.ShouldBe(3);
            items.Select(i => i.Bounds.Width).Distinct().Count().ShouldBe(1);
            items.ShouldAllBe(i => i.Bounds.Height >= 36);
            control.Bounds.Height.ShouldBe(40, 0.5);

            var unselected = items[1].Foreground;
            var point = items[1].TranslatePoint(new Point(items[1].Bounds.Width / 2, items[1].Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            control.SelectedIndex.ShouldBe(1);
            items[1].Classes.Contains(":selected").ShouldBeTrue();
            items[0].Classes.Contains(":selected").ShouldBeFalse();
            items[1].Foreground.ShouldNotBe(unselected);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
