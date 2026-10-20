using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

public class MultiSelectTests
{
    private static MultiSelect Labels() => new()
    {
        Label = "Labels",
        Width = 320,
        ItemsSource = new[]
        {
            new SelectOption("bug", "Bug"), new SelectOption("feature", "Feature"), new SelectOption("docs", "Documentation"),
            new SelectOption("perf", "Performance"), new SelectOption("design", "Design", isDisabled: true)
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

    private static MultiSelectChipRow Chips(MultiSelect select) => TestHelpers.Descendant<MultiSelectChipRow>(select);

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var select = Labels();
        var window = Show(select);
        try
        {
            select.Placeholder.ShouldBe("Select options");
            select.MaxVisibleChips.ShouldBe(2);
            select.Classes.Contains(":has-selection").ShouldBeFalse();
            var trigger = select.GetVisualDescendants().OfType<Border>().First(b => b.Name == MultiSelect.TriggerPart);
            trigger.Bounds.Height.ShouldBe(44);
            select.GetVisualDescendants().OfType<Primitives.PresenceHost>().First(p => p.Name == MultiSelect.ClearHostPart)
                  .IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Follow_Selection_Order_And_Fold_Into_A_Count()
    {
        var select = Labels();
        var window = Show(select);
        try
        {
            select.Selection.Select(3);
            select.Selection.Select(0);
            select.Selection.Select(2);
            Dispatcher.UIThread.RunJobs();
            Chips(select).ChipLabels.ShouldBe(["Performance", "Bug"]);
            Chips(select).MoreCount.ShouldBe(1);
            select.SelectedValues.ShouldBe(["perf", "bug", "docs"]);
            select.Classes.Contains(":has-selection").ShouldBeTrue();
            select.Selection.Deselect(3);
            Dispatcher.UIThread.RunJobs();
            Chips(select).ChipLabels.ShouldBe(["Bug", "Documentation"]);
            Chips(select).MoreCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_Moves_And_Toggles_Without_Closing()
    {
        var select = Labels();
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            select.IsOpen.ShouldBeTrue();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            options.ActiveOption.ShouldBe(0);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            select.SelectedValues.ShouldBe(["bug"]);
            Press(window, PhysicalKey.ArrowUp);
            options.ActiveOption.ShouldBe(3); // wraps, skipping the disabled last option
            Press(window, PhysicalKey.Enter);
            select.SelectedValues.ShouldBe(["bug", "perf"]);
            Press(window, PhysicalKey.Enter);
            select.SelectedValues.ShouldBe(["bug"]);
            Press(window, PhysicalKey.Escape);
            select.IsOpen.ShouldBeFalse();
            Press(window, PhysicalKey.Space);
            select.IsOpen.ShouldBeTrue();
            options.ActiveOption.ShouldBe(-1); // a closed menu forgets its highlight
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Toggles_Options_And_Clear_Empties()
    {
        var select = Labels();
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            foreach (var position in new[] { 1, 2, 4 })
            {
                var point = options.TranslatePoint(options.OptionRect(position).Center, window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }
            select.SelectedValues.ShouldBe(["feature", "docs"]); // Design is disabled
            select.IsOpen.ShouldBeTrue();
            var clear = select.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == MultiSelect.ClearPart);
            var center = clear.TranslatePoint(new Point(11, 11), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.SelectedValues.ShouldBeEmpty();
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Searchable_Menu_Filters()
    {
        var select = Labels();
        select.IsSearchable = true;
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var search = TestHelpers.Descendant<SelectOptionView>(window).FindAncestorOfType<DockPanel>()!
                                    .GetVisualDescendants().OfType<TextBox>().First();
            search.IsFocused.ShouldBeTrue();
            window.KeyTextInput("per");
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<SelectOptionView>(window).OptionCount.ShouldBe(1);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            select.SelectedValues.ShouldBe(["perf"]);
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
        var select = Labels();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { select } }, Motion.ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            select.Selection.Select(0);
            select.Selection.Select(1);
            select.Selection.Select(2);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            select.Selection.Deselect(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            Chips(select).ChipLabels.ShouldBe(["Bug", "Documentation"]);
        }
        finally
        {
            window.Close();
        }
    }
}
