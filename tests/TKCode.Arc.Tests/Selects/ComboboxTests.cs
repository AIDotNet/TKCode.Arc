using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

public class ComboboxTests
{
    private static Combobox Zones() => new()
    {
        Label = "Time zone",
        Width = 300,
        ItemsSource = new[]
        {
            new SelectOption("pt", "Pacific Time") { Keywords = "los angeles" },
            new SelectOption("et", "Eastern Time") { Keywords = "new york" },
            new SelectOption("lon", "London"),
            new SelectOption("ber", "Berlin", isDisabled: true),
            new SelectOption("tyo", "Tokyo")
        }
    };

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static SelectOptionView Options(Window window) => TestHelpers.Descendant<SelectOptionView>(window);

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var combo = Zones();
        var window = Show(combo);
        try
        {
            combo.Placeholder.ShouldBe("Search or select…");
            combo.EmptyText.ShouldBe("No matches found");
            combo.InputBox.ShouldNotBeNull();
            combo.InputBox!.PlaceholderText.ShouldBe("Search or select…");
            var frame = combo.GetVisualDescendants().OfType<Border>().First(b => b.Name == Combobox.FramePart);
            frame.Bounds.Height.ShouldBe(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Opens_And_Typing_Filters_Label_And_Keywords()
    {
        var combo = Zones();
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            combo.IsOpen.ShouldBeTrue();
            Options(window).OptionCount.ShouldBe(5);
            window.KeyTextInput("york");
            Dispatcher.UIThread.RunJobs();
            combo.SearchText.ShouldBe("york");
            Options(window).OptionCount.ShouldBe(1);
            Options(window).ActiveOption.ShouldBe(-1);
            Press(window, PhysicalKey.ArrowDown);
            Options(window).ActiveOption.ShouldBe(0);
            Press(window, PhysicalKey.Enter);
            combo.IsOpen.ShouldBeFalse();
            combo.SelectedValue.ShouldBe("et");
            combo.SelectedText.ShouldBe("Eastern Time");
            combo.InputBox!.Text.ShouldBe("Eastern Time"); // the label, never the value
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Wrap_And_Skip_Disabled_Options()
    {
        var combo = Zones();
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            var options = Options(window);
            Press(window, PhysicalKey.ArrowUp);
            options.ActiveOption.ShouldBe(4); // from nothing, up starts at the end
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(0); // wraps
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(4); // Berlin (3) is disabled
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_And_Drops_The_Query()
    {
        var combo = Zones();
        combo.SelectedValue = "lon";
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            combo.InputBox!.Text.ShouldBe(string.Empty);
            combo.InputBox.PlaceholderText.ShouldBe("London"); // the chosen label stays as muted copy
            window.KeyTextInput("zz");
            Dispatcher.UIThread.RunJobs();
            Options(window).OptionCount.ShouldBe(0);
            TestHelpers.Descendant<Primitives.PresenceHost>(Options(window).FindAncestorOfType<Primitives.SpringSizeDecorator>()!)
                       .IsPresent.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            combo.IsOpen.ShouldBeFalse();
            combo.SearchText.ShouldBe(string.Empty);
            combo.InputBox.Text.ShouldBe("London");
            combo.SelectedValue.ShouldBe("lon");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Button_Clears_And_Reopens()
    {
        var combo = Zones();
        combo.SelectedValue = "tyo";
        var window = Show(combo);
        try
        {
            var host = combo.GetVisualDescendants().OfType<Primitives.PresenceHost>().First(p => p.Name == Combobox.ClearHostPart);
            host.IsPresent.ShouldBeTrue();
            var clear = combo.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == Combobox.ClearPart);
            var point = clear.TranslatePoint(new Point(11, 11), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            combo.SelectedIndex.ShouldBe(-1);
            combo.SelectedValue.ShouldBeNull();
            combo.IsOpen.ShouldBeTrue();
            host.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Filter_Predicate_Is_Used()
    {
        var combo = Zones();
        combo.FilterPredicate = (item, query) => ((SelectOption)item!).Label.StartsWith(query, StringComparison.OrdinalIgnoreCase);
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("t");
            Dispatcher.UIThread.RunJobs();
            Options(window).OptionCount.ShouldBe(1); // Tokyo only; "Pacific Time" contains but does not start with t
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Picks_An_Option()
    {
        var combo = Zones();
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            var options = Options(window);
            var point = options.TranslatePoint(options.OptionRect(2).Center, window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            combo.SelectedValue.ShouldBe("lon");
            combo.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Animates_Under_Full_Motion_Without_Errors()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var combo = Zones();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { combo } }, Motion.ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            combo.FocusInput();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("o");
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            Dispatcher.UIThread.RunJobs();
            combo.SelectedIndex.ShouldBeGreaterThanOrEqualTo(0);
            combo.InputBox!.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }
}
