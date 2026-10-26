using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class RadioIconButtonTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Part(Control owner, string name) =>
        owner.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);

    private static Point CenterOf(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void Radio_Row_Is_44_High_And_Tapping_The_Label_Chooses_It_And_Clears_The_Others()
    {
        var a = new Radio { Content = "Standard", GroupName = "speed", IsChecked = true };
        var b = new Radio { Content = "Express", GroupName = "speed" };
        var panel = new StackPanel { Children = { a, b } };
        var window = Show(panel);
        try
        {
            a.Bounds.Height.ShouldBeGreaterThanOrEqualTo(44);
            Part(a, "PART_Circle").Bounds.Width.ShouldBe(22, 0.5);
            Part(b, "PART_Dot").Opacity.ShouldBe(0);
            Part(a, "PART_Dot").Opacity.ShouldBe(1);

            // Far from the circle: the whole row is the target. A finger on it dims it, and nothing hovers.
            var point = b.TranslatePoint(new Point(b.Bounds.Width - 4, b.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            b.Opacity.ShouldBeLessThan(1);
            window.MouseUp(point, MouseButton.Left);

            b.IsChecked.ShouldBe(true);
            a.IsChecked.ShouldBe(false);
            Part(b, "PART_Dot").Opacity.ShouldBe(1);
            Part(a, "PART_Dot").Opacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Radio_Arrow_Keys_Move_The_Choice_Within_The_Group_And_Skip_Disabled_Items()
    {
        var a = new Radio { Content = "A", GroupName = "g", IsChecked = true };
        var b = new Radio { Content = "B", GroupName = "g", IsEnabled = false };
        var c = new Radio { Content = "C", GroupName = "g" };
        var other = new Radio { Content = "Other", GroupName = "h", IsChecked = true };
        var window = Show(new StackPanel { Children = { a, b, c, other } });
        try
        {
            a.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            c.IsChecked.ShouldBe(true);
            a.IsChecked.ShouldBe(false);
            other.IsChecked.ShouldBe(true); // another group is untouched

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); // wraps to the first
            a.IsChecked.ShouldBe(true);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            c.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Radio_Dot_Springs_Under_Full_Motion_And_Jumps_When_Motion_Is_Off()
    {
        var full = new Radio { Content = "A", GroupName = "f" };
        var off = new Radio { Content = "A", GroupName = "o" };
        var fullWindow = Show(full, ArcMotionLevel.Full);
        var offWindow = Show(off, ArcMotionLevel.Off);
        try
        {
            full.Classes.Contains(ArcMotion.FullPseudoClass).ShouldBeTrue();
            Part(full, "PART_Dot").Transitions.ShouldNotBeNull();
            Part(full, "PART_Dot").Transitions!.Count.ShouldBeGreaterThan(0);

            off.Classes.Contains(ArcMotion.FullPseudoClass).ShouldBeFalse();
            off.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var dot = Part(off, "PART_Dot");
            dot.Opacity.ShouldBe(1);
            (dot.Transitions?.Count ?? 0).ShouldBe(0);
            dot.RenderTransform!.Value.M11.ShouldBe(1, 0.001);
        }
        finally
        {
            fullWindow.Close();
            offWindow.Close();
        }
    }

    [AvaloniaFact]
    public void IconButton_Is_A_44px_Target_Without_Hover_And_Takes_Its_Icon()
    {
        var button = new IconButton { Icon = ArcIcons.Plus };
        AutomationPropertiesName(button, "Add");
        var window = Show(new StackPanel { Children = { button } });
        try
        {
            button.Bounds.Width.ShouldBe(44, 0.5);
            button.Bounds.Height.ShouldBe(44, 0.5);
            Part(button, "PART_Icon").ShouldNotBeNull();
            ((ISolidColorBrush)button.Background!).Color.A.ShouldBe((byte)0);

            var point = CenterOf(button, window);
            window.MouseMove(point);
            ((ISolidColorBrush)button.Background!).Color.A.ShouldBe((byte)0); // hovering changes nothing
            window.MouseDown(point, MouseButton.Left);
            button.Classes.Contains(":pressed").ShouldBeTrue();
            button.Opacity.ShouldBeLessThan(1);
            window.MouseUp(point, MouseButton.Left);
            button.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AutomationPropertiesName(Control control, string name) =>
        Avalonia.Automation.AutomationProperties.SetName(control, name);

    [AvaloniaFact]
    public void IconButton_Variants_Set_Classes_Fill_And_The_Fab_Is_56_With_A_Shadow()
    {
        var plain = new IconButton { Icon = ArcIcons.Plus };
        var tinted = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Tinted };
        var filled = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Filled };
        var fab = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Fab };
        var window = Show(new StackPanel { Children = { plain, tinted, filled, fab } });
        try
        {
            tinted.Classes.Contains(":tinted").ShouldBeTrue();
            filled.Classes.Contains(":filled").ShouldBeTrue();
            fab.Classes.Contains(":fab").ShouldBeTrue();
            ((ISolidColorBrush)tinted.Background!).Color.A.ShouldBeGreaterThan((byte)0);
            ((ISolidColorBrush)filled.Background!).Color.A.ShouldBe((byte)255);
            filled.Bounds.Width.ShouldBe(44, 0.5);
            fab.Bounds.Width.ShouldBe(56, 0.5);
            fab.Bounds.Height.ShouldBe(56, 0.5);

            ((Border)Part(fab, "PART_Frame")).BoxShadow.Count.ShouldBeGreaterThan(0);
            ((Border)Part(plain, "PART_Frame")).BoxShadow.Count.ShouldBe(0);
            Part(fab, "PART_Icon").Bounds.Width.ShouldBe(24, 0.5);

            fab.Variant = IconButtonVariant.Plain;
            fab.Classes.Contains(":fab").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IconButton_Press_Scales_Under_Full_Motion_And_Does_Not_Move_Under_Reduced_Motion()
    {
        var full = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Fab };
        var off = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Fab };
        var fullWindow = Show(full, ArcMotionLevel.Full);
        var offWindow = Show(off, ArcMotionLevel.Off);
        var reduced = new IconButton { Icon = ArcIcons.Plus, Variant = IconButtonVariant.Fab };
        var reducedWindow = Show(reduced, ArcMotionLevel.Reduced);
        try
        {
            full.Classes.Contains(ArcMotion.FullPseudoClass).ShouldBeTrue();
            full.Transitions.ShouldNotBeNull();
            full.Transitions!.Count.ShouldBe(2); // spring on the transform and a fade

            // Off: the pressed state is applied at once, with no transition.
            var point = CenterOf(off, offWindow);
            offWindow.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            off.Classes.Contains(":pressed").ShouldBeTrue();
            (off.Transitions?.Count ?? 0).ShouldBe(0);
            off.RenderTransform!.Value.M11.ShouldBe(0.92, 0.001);
            offWindow.MouseUp(point, MouseButton.Left);

            // Reduced: the press only dims, the button does not shrink.
            var rpoint = CenterOf(reduced, reducedWindow);
            reducedWindow.MouseDown(rpoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            reduced.Classes.Contains(":pressed").ShouldBeTrue();
            reduced.RenderTransform!.Value.M11.ShouldBe(1, 0.001);
            reducedWindow.MouseUp(rpoint, MouseButton.Left);
        }
        finally
        {
            fullWindow.Close();
            offWindow.Close();
            reducedWindow.Close();
        }
    }

    [AvaloniaFact]
    public void IconButton_Click_Raises_The_Command()
    {
        var count = 0;
        var button = new IconButton { Icon = ArcIcons.Plus };
        button.Click += (_, _) => count++;
        var window = Show(new StackPanel { Children = { button } });
        try
        {
            var point = CenterOf(button, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
