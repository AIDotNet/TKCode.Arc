using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Toggles;

/// <summary>States restyle template parts (token values in templates would be local values that styles cannot override).</summary>
public class ThemeStateTests
{
    private static T Part<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(x => x.Name == name);

    [AvaloniaFact]
    public void Checkbox_Square_Follows_State()
    {
        var box = new Checkbox { Content = "Terms", IsInvalid = true };
        var window = TestHelpers.Show(box);
        try
        {
            var square = Part<Border>(box, "PART_Square");
            var invalid = TestHelpers.ColorOf(square.BorderBrush);
            box.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var on = TestHelpers.ColorOf(square.BorderBrush);
            box.IsInvalid = false;
            box.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            var off = TestHelpers.ColorOf(square.BorderBrush);
            invalid.ShouldNotBe(off);
            on.ShouldNotBe(off);
            on.ShouldNotBe(invalid);
            var ring = Part<Border>(box, "PART_FocusRing");
            ring.IsVisible.ShouldBeFalse();
            box.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            ring.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Radio_Row_Recolors_When_Selected()
    {
        var group = new RadioGroup { ItemsSource = new[] { new RadioOption("a", "A"), new RadioOption("b", "B") } };
        var window = TestHelpers.Show(group);
        try
        {
            var row = (Control)group.ContainerFromIndex(0)!;
            var ring = Part<Border>(row, "PART_Ring");
            var before = TestHelpers.ColorOf(ring.BorderBrush);
            group.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(ring.BorderBrush).ShouldNotBe(before);
            Part<Border>(row, "PART_Dot").Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Radio_Card_Moves_Parts_In_List_Layout()
    {
        var cards = new RadioCards { Layout = RadioCardsLayout.List, ItemsSource = new[] { new RadioCardOption("a", "A", "x", "$1") } };
        var window = TestHelpers.Show(cards);
        try
        {
            var card = (Control)cards.ContainerFromIndex(0)!;
            Grid.GetColumn(Part<Border>(card, "PART_Indicator")).ShouldBe(0);
            Part<Border>(card, "PART_Indicator").Margin.Right.ShouldBe(12);
            Part<Avalonia.Controls.Presenters.ContentPresenter>(card, "PART_Meta").Margin.Left.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Billing_Badge_Takes_The_Accent_When_Selected()
    {
        var toggle = new BillingToggle();
        var window = TestHelpers.Show(toggle);
        try
        {
            var yearly = (Control)toggle.ContainerFromIndex(1)!;
            var badge = Part<Border>(yearly, "PART_Badge");
            var quiet = TestHelpers.ColorOf(badge.Background);
            toggle.Value = "yearly";
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(badge.Background).ShouldNotBe(quiet);
            badge.Height.ShouldBe(24);
            toggle.Size = BillingToggleSize.Large;
            Dispatcher.UIThread.RunJobs();
            badge.Height.ShouldBe(32);
        }
        finally
        {
            window.Close();
        }
    }
}
