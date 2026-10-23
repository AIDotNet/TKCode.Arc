using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Selects;

public class SelectTests
{
    private static Select Regions() => new()
    {
        Label = "Region",
        Description = "Where your data is stored.",
        Width = 260,
        Items =
        {
            new SelectItem { Content = "North America", Value = "na" },
            new SelectItem { Content = "Europe", Value = "eu" },
            new SelectItem { Content = "Asia Pacific", Value = "apac", IsEnabled = false },
            new SelectItem { Content = "South America", Value = "sa" }
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

    [AvaloniaFact]
    public void Defaults_Show_The_Placeholder_And_Arc_Metrics()
    {
        var select = Regions();
        var window = Show(select);
        try
        {
            select.Placeholder.ShouldBe("Select an option");
            select.SelectedText.ShouldBeNull();
            select.Classes.Contains(":placeholder").ShouldBeTrue();
            var value = TestHelpers.Descendant<SelectValueRoll>(select);
            value.Text.ShouldBe("Select an option");
            value.IsPlaceholder.ShouldBeTrue();
            var trigger = select.GetVisualDescendants().OfType<Border>().First(b => b.Name == Select.TriggerPart);
            trigger.Bounds.Height.ShouldBe(44);
            trigger.CornerRadius.ShouldBe(new CornerRadius(18));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Items_Resolve_Values_Both_Ways()
    {
        var select = Regions();
        var window = Show(select);
        try
        {
            select.SelectedValue = "eu";
            Dispatcher.UIThread.RunJobs();
            select.SelectedIndex.ShouldBe(1);
            select.SelectedText.ShouldBe("Europe");
            select.SelectedIndex = 3;
            select.SelectedValue.ShouldBe("sa");
            TestHelpers.Descendant<SelectValueRoll>(select).Text.ShouldBe("South America");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Source_Uses_Selectors()
    {
        var select = new Select
        {
            ItemsSource = new[] { (Id: 1, Name: "Newest"), (Id: 2, Name: "Oldest") },
            TextSelector = item => ((ValueTuple<int, string>)item!).Item2,
            ValueSelector = item => ((ValueTuple<int, string>)item!).Item1,
            SelectedValue = 2
        };
        var window = Show(select);
        try
        {
            select.SelectedIndex.ShouldBe(1);
            select.SelectedText.ShouldBe("Oldest");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_Moves_Skips_Disabled_And_Picks()
    {
        var select = Regions();
        var changes = 0;
        select.SelectionChanged += (_, _) => changes++;
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            select.IsOpen.ShouldBeTrue();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            options.ActiveOption.ShouldBe(0);
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(1);
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(3); // Asia Pacific is disabled
            Press(window, PhysicalKey.ArrowDown);
            options.ActiveOption.ShouldBe(3); // no wrap (Radix)
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("sa");
            changes.ShouldBe(1);

            Press(window, PhysicalKey.Space);
            select.IsOpen.ShouldBeTrue();
            options.ActiveOption.ShouldBe(3); // opens on the selected option
            Press(window, PhysicalKey.Home);
            options.ActiveOption.ShouldBe(0);
            Press(window, PhysicalKey.Escape);
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("sa");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typeahead_Picks_While_Closed_And_Highlights_While_Open()
    {
        var select = Regions();
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            window.KeyTextInput("e");
            Dispatcher.UIThread.RunJobs();
            select.SelectedValue.ShouldBe("eu");
            select.IsOpen.ShouldBeFalse();

            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("s");
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<SelectOptionView>(window).ActiveOption.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Opens_And_Picks_An_Option()
    {
        var select = Regions();
        var window = Show(select);
        try
        {
            var trigger = select.GetVisualDescendants().OfType<Border>().First(b => b.Name == Select.TriggerPart);
            var center = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeTrue();

            var options = TestHelpers.Descendant<SelectOptionView>(window);
            var row = options.OptionRect(1);
            var point = options.TranslatePoint(row.Center, window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            options.ActiveOption.ShouldBe(1);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("eu");

            // Pressing the trigger of an open menu closes it (no light-dismiss reopen).
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeTrue();
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Lists_Draw_Only_Visible_Rows()
    {
        var select = new Select { ItemsSource = Enumerable.Range(0, 5000).Select(i => $"Option {i}").ToArray(), Width = 240 };
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            options.OptionCount.ShouldBe(5000);
            options.Bounds.Height.ShouldBe(5000 * 36);
            var scroll = options.FindAncestorOfType<ScrollViewer>()!;
            scroll.Viewport.Height.ShouldBeLessThanOrEqualTo(320);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Hover_Highlight_Takes_The_Option_Corner_Radius_Of_The_Theme()
    {
        var select = Regions();
        var window = Show(select);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            options.Highlight.CornerRadius.ShouldBe(new CornerRadius(14));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Full, false)]
    [InlineData(ArcMotionLevel.Reduced, true)]
    public void The_Menu_Closes_Without_An_Exit_Motion_Only_At_The_Full_Level(ArcMotionLevel level, bool fades)
    {
        var select = Regions();
        ArcMotion.SetLevel(select, level);
        var window = Show(select);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            var popup = TestHelpers.Descendant<ArcPopup>(select);
            (popup.CloseMotion is not null).ShouldBe(fades);
        }
        finally
        {
            window.Close();
        }
    }
}
