using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class RadioGroupTests
{
    private static RadioGroup Plans() => new()
    {
        Header = "Plan",
        ItemsSource = new[]
        {
            new RadioOption("hobby", "Hobby", "For side projects"),
            new RadioOption("pro", "Pro", "For growing teams"),
            new RadioOption("scale", "Scale")
        }
    };

    [AvaloniaFact]
    public void SelectedValue_Selects_The_Option_With_That_Value()
    {
        var group = Plans();
        group.SelectedValue = "pro";
        var window = TestHelpers.Show(group);
        try
        {
            group.SelectedIndex.ShouldBe(1);
            group.SelectedItem.ShouldBeOfType<RadioOption>().Label.ShouldBe("Pro");
            group.ContainerFromIndex(1).ShouldBeOfType<RadioGroupItem>().IsSelected.ShouldBeTrue();
            group.SelectedIndex = 2;
            group.SelectedValue.ShouldBe("scale");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Binds_Two_Way()
    {
        var model = new Model { Plan = "hobby" };
        var group = Plans();
        group.DataContext = model;
        group.Bind(SelectingItemsControl.SelectedValueProperty, new Binding(nameof(Model.Plan)));
        var window = TestHelpers.Show(group);
        try
        {
            group.SelectedIndex.ShouldBe(0);
            group.SelectedIndex = 1;
            model.Plan.ShouldBe("pro");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Options_Work_Like_ItemsSource()
    {
        var group = new RadioGroup();
        group.Items.Add(new RadioOption("a", "A"));
        group.Items.Add(new RadioOption("b", "B"));
        group.SelectedValue = "b";
        var window = TestHelpers.Show(group);
        try
        {
            group.SelectedIndex.ShouldBe(1);
            var row = group.ContainerFromIndex(0).ShouldBeOfType<RadioGroupItem>();
            row.Content.ShouldBe("A");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Set_Before_Items_Is_Applied_When_Items_Arrive()
    {
        var group = new RadioGroup { SelectedValue = "c" };
        group.ItemsSource = new[] { new RadioOption("a", "A"), new RadioOption("c", "C") };
        var window = TestHelpers.Show(group);
        try
        {
            group.SelectedIndex.ShouldBe(1);
            group.SelectedValue.ShouldBe("c");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Select_And_Focus_The_Neighbor_And_Wrap()
    {
        var group = Plans();
        group.SelectedValue = "hobby";
        var window = TestHelpers.Show(group);
        try
        {
            group.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("pro");
            group.ContainerFromIndex(1)!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("scale");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Group_Is_One_Tab_Stop_On_The_Selected_Row()
    {
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var group = Plans();
        group.SelectedValue = "pro";
        var after = new Avalonia.Controls.Button { Content = "After" };
        var window = TestHelpers.Show(new StackPanel { Children = { before, group, after } });
        try
        {
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.ContainerFromIndex(1)!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            after.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Highlight_Sits_On_The_Selected_Row()
    {
        var group = Plans();
        group.SelectedValue = "scale";
        var window = TestHelpers.Show(group);
        try
        {
            var highlight = TestHelpers.Descendant<GlidingIndicator>(group);
            var row = group.ContainerFromIndex(2)!;
            var origin = row.TranslatePoint(default, highlight)!.Value;
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.Y.ShouldBe(origin.Y + 0.5, 0.01);
            highlight.CurrentRect.Height.ShouldBe(row.Bounds.Height - 1, 0.01);
            group.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            highlight.CurrentRect.Y.ShouldBe(0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Options_Are_Skipped()
    {
        var group = new RadioGroup
        {
            ItemsSource = new[] { new RadioOption("a", "A"), new RadioOption("b", "B") { IsDisabled = true }, new RadioOption("c", "C") },
            SelectedValue = "a"
        };
        var window = TestHelpers.Show(group);
        try
        {
            group.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("c");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Highlight_Glides_Under_Full_Motion_And_Snaps_Under_Reduced()
    {
        var clock = new Motion.ManualFrameClock();
        using var install = clock.Install();
        var group = Plans();
        group.SelectedValue = "hobby";
        var window = TestHelpers.Show(group, Motion.ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var highlight = TestHelpers.Descendant<GlidingIndicator>(group);
            var target = group.ContainerFromIndex(2)!.TranslatePoint(default, highlight)!.Value.Y + 0.5;
            group.SelectedIndex = 2;
            clock.Advance(TimeSpan.FromMilliseconds(50));
            highlight.CurrentRect.Y.ShouldBeGreaterThan(0.5);
            highlight.CurrentRect.Y.ShouldBeLessThan(target);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            highlight.CurrentRect.Y.ShouldBe(target, 0.01);
            Motion.ArcMotion.SetLevel(window, Motion.ArcMotionLevel.Reduced);
            group.SelectedIndex = 0;
            highlight.CurrentRect.Y.ShouldBe(0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Model
    {
        public string? Plan { get; set; }
    }
}
