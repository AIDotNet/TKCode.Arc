using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

public class MorphSelectTests
{
    private static MorphSelect Zones(int extra = 0)
    {
        var select = new MorphSelect { Label = "Time zone" };
        select.Items.Add(new SelectItem { Content = "Pacific Time", Value = "pt", Meta = "UTC−8", Group = "Americas" });
        select.Items.Add(new SelectItem { Content = "Eastern Time", Value = "et", Meta = "UTC−5", Group = "Americas" });
        select.Items.Add(new SelectItem { Content = "London", Value = "lon", Group = "Europe" });
        select.Items.Add(new SelectItem { Content = "Kyiv", Value = "kyiv", Group = "Europe", IsEnabled = false });
        select.Items.Add(new SelectItem { Content = "Tokyo", Value = "tyo", Group = "Asia Pacific" });
        for (var i = 0; i < extra; i++)
        {
            select.Items.Add(new SelectItem { Content = $"Zone {i}", Value = $"z{i}", Group = "More" });
        }
        return select;
    }

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Width = 600;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Closed_Trigger_Is_A_Pill_As_Wide_As_Its_Label()
    {
        var select = Zones();
        var window = Show(select);
        try
        {
            select.Placeholder.ShouldBe("Select");
            var trigger = select.GetVisualDescendants().OfType<Border>().First(b => b.Name == MorphSelect.TriggerPart);
            trigger.Bounds.Height.ShouldBe(44);
            trigger.CornerRadius.ShouldBe(new CornerRadius(22));
            var placeholderWidth = trigger.Bounds.Width;
            select.SelectedValue = "pt";
            Dispatcher.UIThread.RunJobs();
            var roll = TestHelpers.Descendant<SelectValueRoll>(select);
            roll.Text.ShouldBe("Pacific Time");
            trigger.Bounds.Width.ShouldBeGreaterThan(placeholderWidth);
            trigger.Bounds.Width.ShouldBe(Math.Ceiling(roll.ContentWidth + 50), 1.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opens_On_The_Selected_Option_And_Grows_The_Surface()
    {
        var select = Zones();
        select.SelectedValue = "lon";
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            var options = select.OptionView!;
            options.EntryAt(options.ActiveOption).ShouldBe(2);
            options.OptionCount.ShouldBe(5);
            options.Extent.ShouldBe(3 * 28 + 5 * 36 + 7 * 2 + 2 * 4); // three headed groups
            var surface = select.Surface!;
            surface.ShapeSize.Width.ShouldBe(272);
            surface.ShapeSize.Height.ShouldBe(44 + 6 + options.Extent + 6);
            select.IsSearchActive.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Listbox_Keyboard_Model()
    {
        var select = Zones();
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.End);
            select.IsOpen.ShouldBeTrue();
            var options = select.OptionView!;
            options.ActiveOption.ShouldBe(4);
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(4); // no wrap
            Press(window, PhysicalKey.ArrowUp);
            options.ActiveOption.ShouldBe(2); // Kyiv is disabled
            Press(window, PhysicalKey.Home);
            options.ActiveOption.ShouldBe(0);
            Press(window, PhysicalKey.PageDown);
            options.ActiveOption.ShouldBe(4);
            Press(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("tyo");
            select.IsFocused.ShouldBeTrue();

            Press(window, PhysicalKey.Space);
            select.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Escape);
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("tyo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typeahead_Opens_On_The_Match_And_Cycles()
    {
        var select = Zones();
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            window.KeyTextInput("t");
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeTrue();
            var options = select.OptionView!;
            options.EntryAt(options.ActiveOption).ShouldBe(4); // Tokyo
            window.KeyTextInput("e");
            Dispatcher.UIThread.RunJobs();
            options.EntryAt(options.ActiveOption).ShouldBe(4); // "te" has no match: stay
            Press(window, PhysicalKey.Enter);
            select.SelectedValue.ShouldBe("tyo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Lists_Search_From_The_Closed_Trigger()
    {
        var select = Zones(extra: 6);
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            window.KeyTextInput("l");
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeTrue();
            select.IsSearchActive.ShouldBeTrue();
            var options = select.OptionView!;
            options.OptionCount.ShouldBe(1); // London (label, meta and keywords are searched)
            var search = select.Surface!.GetVisualDescendants().OfType<TextBox>().First();
            search.Text.ShouldBe("l");
            search.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            select.SelectedValue.ShouldBe("lon");
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Opens_Hovers_And_Picks()
    {
        var select = Zones();
        var window = Show(select);
        try
        {
            var trigger = select.GetVisualDescendants().OfType<Border>().First(b => b.Name == MorphSelect.TriggerPart);
            var center = trigger.TranslatePoint(new Point(20, 22), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeTrue();
            var options = select.OptionView!;
            var point = options.TranslatePoint(options.OptionRect(1).Center, window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            options.ActiveOption.ShouldBe(1);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.SelectedValue.ShouldBe("et");
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morph_And_Flight_Run_Under_Full_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var select = Zones();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { select } }, Motion.ArcMotionLevel.Full);
        window.Width = 600;
        window.Height = 700;
        try
        {
            Dispatcher.UIThread.RunJobs();
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = select.Surface!;
            clock.Advance(TimeSpan.FromMilliseconds(60));
            surface.ShapeSize.Height.ShouldBeLessThan(150); // still growing out of the 44px trigger
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            surface.ShapeSize.Width.ShouldBe(272, 0.5);
            var options = select.OptionView!;
            var point = options.TranslatePoint(options.OptionRect(4).Center, window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeFalse();
            clock.Advance(TimeSpan.FromSeconds(1.2));
            Dispatcher.UIThread.RunJobs();
            surface.ShapeSize.Height.ShouldBe(44, 0.5);
            select.SelectedValue.ShouldBe("tyo");
        }
        finally
        {
            window.Close();
        }
    }
}
