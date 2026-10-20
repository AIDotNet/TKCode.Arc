using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

public class ChipGroupTests
{
    private static readonly string[] Topics = { "Design", "Engineering", "Research", "Marketing", "Sales", "Support", "Finance" };

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Width = 640;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<ChipGroupChip> Chips(ChipGroup group) =>
        group.GetVisualDescendants().OfType<ChipGroupChip>().Where(c => !c.IsLeaving).ToList();

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Chips_Have_Arc_Metrics_And_Grow_When_Selected()
    {
        var group = new ChipGroup { Label = "Topics", ItemsSource = Topics };
        var window = Show(group);
        try
        {
            var chips = Chips(group);
            chips.Count.ShouldBe(7);
            chips[0].Bounds.Height.ShouldBe(36);
            var width = chips[1].Bounds.Width;
            chips[1].Text.ShouldBe("Engineering");
            Click(window, chips[1]);
            group.SelectedValues.ShouldBe(["Engineering"]);
            Dispatcher.UIThread.RunJobs();
            chips[1].IsSelectedChip.ShouldBeTrue();
            chips[1].Bounds.Width.ShouldBe(width + ChipGroupChip.CheckSlot, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Multiple_Keeps_Option_Order_And_Single_Replaces_Or_Clears()
    {
        var group = new ChipGroup { Label = "Topics", ItemsSource = Topics };
        var window = Show(group);
        try
        {
            Click(window, Chips(group)[4]);
            Click(window, Chips(group)[0]);
            group.SelectedValues.ShouldBe(["Design", "Sales"]);
            group.IsMultiple = false;
            Dispatcher.UIThread.RunJobs();
            group.SelectedValues.Count.ShouldBe(1);
            Click(window, Chips(group)[2]);
            group.SelectedValues.ShouldBe(["Research"]);
            Click(window, Chips(group)[2]);
            group.SelectedValues.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Overflow_Folds_Keeps_Selected_Chips_And_Expands()
    {
        var group = new ChipGroup { Label = "Topics", ItemsSource = Topics, MaxVisibleItems = 3 };
        group.Selection.Select(5);
        var window = Show(group);
        try
        {
            var chips = Chips(group);
            chips.Select(c => c.Text).ShouldBe(["Design", "Engineering", "Research", "Support", "+3 more"]);
            var more = chips[^1];
            more.IsMore.ShouldBeTrue();
            Click(window, more);
            group.IsExpanded.ShouldBeTrue();
            Chips(group).Select(c => c.Text).ShouldBe(["Design", "Engineering", "Research", "Marketing", "Sales", "Support", "Finance", "Show less"]);
            // A chip picked while expanded stays in view after folding.
            Click(window, Chips(group)[6]);
            Click(window, Chips(group)[^1]);
            group.IsExpanded.ShouldBeFalse();
            Chips(group).Select(c => c.Text).ShouldBe(["Design", "Engineering", "Research", "Support", "Finance", "+2 more"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Roving_Focus_With_Arrows_Home_End_And_Space()
    {
        var group = new ChipGroup { Label = "Topics", ItemsSource = Topics, MaxVisibleItems = 3 };
        group.Selection.Select(1);
        var window = Show(group);
        try
        {
            var chips = Chips(group);
            chips.Count(c => c.IsTabStop).ShouldBe(1);
            chips[1].IsTabStop.ShouldBeTrue(); // the first selected chip holds the tab stop
            chips[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chips[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chips[^1].IsFocused.ShouldBeTrue(); // wraps to the more chip
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chips[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValues.ShouldBe(["Design", "Engineering"]);
            Chips(group).Count(c => c.IsTabStop).ShouldBe(1);
            Chips(group)[0].IsTabStop.ShouldBeTrue(); // the last focused chip keeps the stop
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Items_And_Automation()
    {
        var group = new ChipGroup
        {
            Label = "Status",
            Items = { new SelectItem { Content = "Open", Value = "open" }, new SelectItem { Content = "Closed", Value = "closed" } }
        };
        var window = Show(group);
        try
        {
            Click(window, Chips(group)[1]);
            group.SelectedValues.ShouldBe(["closed"]);
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(group);
            peer.GetName().ShouldBe("Status");
            var chipPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Chips(group)[1]);
            ((Avalonia.Automation.Provider.IToggleProvider)chipPeer).ToggleState.ShouldBe(Avalonia.Automation.Provider.ToggleState.On);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morph_And_Reflow_Run_Under_Full_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var group = new ChipGroup { Label = "Topics", ItemsSource = Topics, MaxVisibleItems = 4, Width = 420 };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { group } }, Motion.ArcMotionLevel.Full);
        window.Width = 640;
        try
        {
            Dispatcher.UIThread.RunJobs();
            Click(window, Chips(group)[0]);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Click(window, Chips(group)[^1]);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Chips(group).Count.ShouldBe(8); // revealed chips are entering while the more chip glides to the end
            clock.Advance(TimeSpan.FromMilliseconds(900));
            Click(window, Chips(group)[^1]);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            group.IsExpanded.ShouldBeFalse();
            group.SelectedValues.ShouldBe(["Design"]);
            Chips(group).All(c => c.Opacity > 0.99).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
