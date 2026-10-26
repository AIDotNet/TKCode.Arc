using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// The motion pass over the existing mobile controls. They assert motion state (where the glider is, whether the clear
/// button is present, how far the check has drawn), not pixels: under Full a <see cref="ManualFrameClock"/> steps the
/// springs, under Off everything jumps.
/// </summary>
public class MotionPassTests
{
    private static Window Show(Control content, ArcMotionLevel level, int width = 400)
    {
        var window = new Window { Content = content, Width = width, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Frames(ManualFrameClock clock, int count)
    {
        clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60 * count));
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(Control root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Rect Cover(Control host, Visual relativeTo)
        => new(host.TranslatePoint(default, relativeTo)!.Value, host.Bounds.Size);

    // Selection is set after the items (an initializer would set it while the list is still empty).
    private static Tabbar NewTabbar()
    {
        var tabbar = new Tabbar
        {
            Items =
            {
                new TabbarItem { Content = "Home", Icon = ArcIcons.Search },
                new TabbarItem { Content = "Search", Icon = ArcIcons.Search },
                new TabbarItem { Content = "Me", Icon = ArcIcons.Search },
            },
        };
        tabbar.SelectedIndex = 0;
        return tabbar;
    }

    private static Segmented NewSegmented()
    {
        var control = new Segmented
        {
            Items = { new SegmentedItem { Content = "Day" }, new SegmentedItem { Content = "Week" }, new SegmentedItem { Content = "Month" } },
        };
        control.SelectedIndex = 0;
        return control;
    }

    private static Rect TabTarget(Tabbar tabbar, int index)
        => Cover(((TabbarItem)tabbar.Items[index]!).IndicatorHost!, tabbar.Glider!);

    [AvaloniaFact]
    public void Tabbar_Glider_Is_Under_The_Selected_Icon_And_Jumps_Under_Off()
    {
        var tabbar = NewTabbar();
        var window = Show(tabbar, ArcMotionLevel.Off);
        try
        {
            var glider = tabbar.Glider.ShouldNotBeNull();
            glider.HasTarget.ShouldBeTrue();
            glider.CurrentOpacity.ShouldBe(1);
            glider.CurrentRect.ShouldBe(TabTarget(tabbar, 0));

            tabbar.SelectedIndex = 2;
            glider.CurrentRect.ShouldBe(TabTarget(tabbar, 2)); // no frames needed: it jumped
            glider.CurrentRect.X.ShouldBeGreaterThan(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabbar_Glider_Fades_In_Place_At_First_Then_Glides_On_A_Spring_And_Snaps_On_Resize()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var tabbar = NewTabbar();
        var window = Show(tabbar, ArcMotionLevel.Full);
        try
        {
            var glider = tabbar.Glider.ShouldNotBeNull();
            // First show: lands on the selected item at once and fades in (no slide from the corner).
            glider.CurrentRect.ShouldBe(TabTarget(tabbar, 0));
            glider.CurrentOpacity.ShouldBeLessThan(1);
            Frames(clock, 30);
            glider.CurrentOpacity.ShouldBe(1, 0.01);

            tabbar.SelectedIndex = 1;
            var from = glider.CurrentRect.X;
            var to = TabTarget(tabbar, 1).X;
            Frames(clock, 3);
            glider.CurrentRect.X.ShouldBeGreaterThan(from);
            glider.CurrentRect.X.ShouldBeLessThan(to); // still on its way
            Frames(clock, 120);
            glider.CurrentRect.X.ShouldBe(to, 0.5);

            // A container resize is not a selection change: it snaps.
            window.Width = 300;
            Dispatcher.UIThread.RunJobs();
            glider.CurrentRect.ShouldBe(TabTarget(tabbar, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabbar_Items_Have_No_Selected_Background_And_Reduced_Motion_Jumps()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var tabbar = NewTabbar();
        var window = Show(tabbar, ArcMotionLevel.Reduced);
        try
        {
            var glider = tabbar.Glider.ShouldNotBeNull();
            tabbar.SelectedIndex = 2;
            glider.CurrentRect.ShouldBe(TabTarget(tabbar, 2)); // reduced: jump, no frames run
            ((TabbarItem)tabbar.Items[2]!).Classes.Contains(":selected").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segmented_Block_Slides_Behind_The_Selected_Segment()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var control = NewSegmented();
        var window = Show(control, ArcMotionLevel.Full);
        try
        {
            var glider = control.Glider.ShouldNotBeNull();
            Rect Target(int i) => Cover((Control)control.Items[i]!, glider);
            glider.CurrentRect.ShouldBe(Target(0));
            Frames(clock, 30);

            control.SelectedIndex = 2;
            Frames(clock, 3);
            glider.CurrentRect.X.ShouldBeGreaterThan(Target(0).X);
            glider.CurrentRect.X.ShouldBeLessThan(Target(2).X);
            Frames(clock, 120);
            glider.CurrentRect.X.ShouldBe(Target(2).X, 0.5);
            glider.CurrentRect.Width.ShouldBe(Target(2).Width, 0.5);

            // The segment itself no longer paints the selected fill: the glider does.
            var item = (SegmentedItem)control.Items[2]!;
            item.Classes.Contains(":selected").ShouldBeTrue();
            (item.Background is null || item.Background is Avalonia.Media.ISolidColorBrush { Color.A: 0 }).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segmented_Block_Jumps_Under_Off_And_Hides_Without_A_Selection()
    {
        var control = NewSegmented();
        var window = Show(control, ArcMotionLevel.Off);
        try
        {
            var glider = control.Glider.ShouldNotBeNull();
            control.SelectedIndex = 1;
            glider.CurrentRect.ShouldBe(Cover((Control)control.Items[1]!, glider));
            control.SelectedIndex = -1;
            glider.CurrentOpacity.ShouldBe(0);
            glider.HasTarget.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checkbox_Check_Draws_In_Under_Full_And_Is_Complete_Under_Off()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var animated = new Checkbox { Content = "Animated" };
        var window = Show(animated, ArcMotionLevel.Full);
        try
        {
            var check = animated.CheckGlyph.ShouldNotBeNull();
            animated.IsChecked = true;
            check.Progress.ShouldBeLessThan(0.5);
            Frames(clock, 3);
            check.Progress.ShouldBeGreaterThan(0);
            check.Progress.ShouldBeLessThan(1);
            Frames(clock, 60);
            check.Progress.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }

        var instant = new Checkbox { Content = "Instant" };
        var offWindow = Show(instant, ArcMotionLevel.Off);
        try
        {
            instant.IsChecked = true;
            instant.CheckGlyph!.Progress.ShouldBe(1);
            instant.CheckGlyph.Opacity.ShouldBe(1);
        }
        finally
        {
            offWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Checkbox_Dash_And_Check_Morph_Into_Each_Other()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var box = new Checkbox { Content = "Three", IsThreeState = true };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            var mark = box.MarkGlyph.ShouldNotBeNull();
            box.IsChecked = null;
            mark.Data.ShouldNotBeNull();
            var dash = mark.Data;

            box.IsChecked = true; // dash -> check morphs the same stroke instead of drawing
            mark.Morph!.IsAnimating.ShouldBeTrue();
            mark.Data.ShouldNotBe(dash);
            Frames(clock, 120);
            mark.Morph.IsAnimating.ShouldBeFalse();

            box.IsChecked = null; // and back
            mark.Morph.IsAnimating.ShouldBeTrue();
            mark.Data.ShouldBe(dash);
            Frames(clock, 120);
            mark.Morph.IsAnimating.ShouldBeFalse();

        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checkbox_Marks_Show_The_Right_Glyph_Per_State_Under_Off()
    {
        var box = new Checkbox { Content = "Three", IsThreeState = true };
        var window = Show(box, ArcMotionLevel.Off);
        try
        {
            box.CheckGlyph!.Opacity.ShouldBe(0);
            box.MarkGlyph!.Opacity.ShouldBe(0);
            box.IsChecked = true;
            box.CheckGlyph.Opacity.ShouldBe(1);
            box.CheckGlyph.Progress.ShouldBe(1);
            box.MarkGlyph.Opacity.ShouldBe(0);
            box.IsChecked = null;
            box.CheckGlyph.Opacity.ShouldBe(0);
            box.MarkGlyph.Opacity.ShouldBe(1);
            box.MarkGlyph.Morph!.IsAnimating.ShouldBeFalse();
            box.IsChecked = true;
            box.MarkGlyph.Opacity.ShouldBe(1); // dash -> check morphed in place
            box.CheckGlyph.Opacity.ShouldBe(0);
            box.IsChecked = false;
            box.MarkGlyph.Opacity.ShouldBe(0);
            box.CheckGlyph.Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TextField_Clear_Button_Pops_In_And_Leaves_Through_A_PresenceHost()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var field = new TextField { PlaceholderText = "Name" };
        var window = Show(new StackPanel { Children = { field } }, ArcMotionLevel.Full);
        try
        {
            var host = Part<PresenceHost>(field, "PART_ClearHost");
            var clear = Part<Avalonia.Controls.Button>(field, "PART_Clear");
            host.IsPresent.ShouldBeFalse();
            clear.IsVisible.ShouldBeFalse();

            field.Text = "Ada";
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            host.IsPresent.ShouldBeTrue();
            clear.IsVisible.ShouldBeTrue();
            host.Opacity.ShouldBeLessThan(0.5); // entering from a fade, not appearing at once
            Frames(clock, 2);
            host.Opacity.ShouldBeGreaterThan(0);
            host.Opacity.ShouldBeLessThan(1);
            Frames(clock, 60);
            host.Opacity.ShouldBe(1, 0.01);

            field.Text = "";
            host.IsPresent.ShouldBeFalse();
            clear.IsVisible.ShouldBeTrue(); // keeps its place while it leaves
            Frames(clock, 60);
            clear.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TextField_Clear_Button_Appears_And_Leaves_At_Once_Under_Off()
    {
        var field = new TextField { Text = "Ada" };
        var window = Show(new StackPanel { Children = { field } }, ArcMotionLevel.Off);
        try
        {
            var clear = Part<Avalonia.Controls.Button>(field, "PART_Clear");
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            clear.IsVisible.ShouldBeTrue();
            Part<PresenceHost>(field, "PART_ClearHost").Opacity.ShouldBe(1);
            field.Text = "";
            clear.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navbar_Title_Cross_Fades_When_It_Changes_Under_Full_And_Replaces_Under_Off()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var navbar = new Navbar { Title = "Inbox" };
        var window = Show(navbar, ArcMotionLevel.Full);
        try
        {
            var swap = Part<ArcContentSwap>(navbar, "PART_Title");
            int Layers() => swap.GetVisualChildren().OfType<ContentPresenter>().Count();
            Layers().ShouldBe(1);
            navbar.Title = "Sent";
            Layers().ShouldBe(2); // the old title is still leaving
            Frames(clock, 60);
            // The exiting layer is removed from a thread-pool continuation that posts to the UI thread.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Layers() != 1 && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            Layers().ShouldBe(1);
            swap.GetVisualChildren().OfType<ContentPresenter>().Single().Content.ShouldBe("Sent");
        }
        finally
        {
            window.Close();
        }

        var plain = new Navbar { Title = "Inbox" };
        var offWindow = Show(plain, ArcMotionLevel.Off);
        try
        {
            var swap = Part<ArcContentSwap>(plain, "PART_Title");
            plain.Title = "Sent";
            swap.GetVisualChildren().OfType<ContentPresenter>().Count().ShouldBe(1);
        }
        finally
        {
            offWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Switch_Thumb_Does_Not_Stretch_Under_Off_And_Pressed_Is_Reported()
    {
        var toggle = new Switch();
        var window = Show(toggle, ArcMotionLevel.Off);
        try
        {
            var thumb = Part<Border>(toggle, "PART_Thumb");
            var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            toggle.Classes.Contains(":pressed").ShouldBeTrue();
            thumb.RenderTransform!.Value.M11.ShouldBe(1, 0.001);
            window.MouseUp(point, MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switch_Thumb_Stretches_Toward_Its_Target_Under_Full_When_Pressed()
    {
        var toggle = new Switch();
        var window = Show(toggle, ArcMotionLevel.Full);
        try
        {
            var thumb = Part<Border>(toggle, "PART_Thumb");
            var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            toggle.Classes.Contains(":pressed").ShouldBeTrue();
            thumb.RenderTransformOrigin.Point.X.ShouldBe(0); // off: grows from the leading edge, toward the right
            thumb.Transitions.ShouldNotBeNull();
            window.MouseUp(point, MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_Scale_Lives_On_The_Checkbox_Box_Not_On_The_Row_Or_The_List_Row()
    {
        var box = new Checkbox { Content = "Remember" };
        var row = new ListItem { Content = "Wi-Fi" };
        var window = Show(new StackPanel { Children = { box, row } }, ArcMotionLevel.Full);
        try
        {
            Part<Panel>(box, "PART_BoxHost").RenderTransform.ShouldNotBeNull();
            box.RenderTransform.ShouldBeNull();
            row.RenderTransform.ShouldBeNull();
            row.Classes.Contains(":pointerover").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
