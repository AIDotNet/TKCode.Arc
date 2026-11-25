using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
            ItemsSource = new[] { new RadioOption("a", "A"), new RadioOption("b", "B") { IsEnabled = false }, new RadioOption("c", "C") },
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

    [AvaloniaFact]
    public void Data_Bound_Rows_Take_Their_Description_Name_And_State_From_A_Style()
    {
        // The docs' pattern: any item type, an ItemTemplate for the label, a style on the public container for the rest.
        var group = AvaloniaRuntimeXamlLoader.Parse<RadioGroup>("""
            <arc:RadioGroup xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                            Header="Plan" SelectedValueBinding="{Binding Id}">
                <arc:RadioGroup.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </arc:RadioGroup.ItemTemplate>
                <arc:RadioGroup.Styles>
                    <Style Selector="arc|RadioGroupItem">
                        <Setter Property="Description" Value="{Binding Summary}" />
                        <Setter Property="IsEnabled" Value="{Binding IsAvailable}" />
                        <Setter Property="AutomationProperties.Name" Value="{Binding Name}" />
                    </Style>
                </arc:RadioGroup.Styles>
            </arc:RadioGroup>
            """);
        group.ItemsSource = new[]
        {
            new PlanRow("hobby", "Hobby", "For side projects.", true),
            new PlanRow("pro", "Pro", "For growing teams.", true),
            new PlanRow("team", "Team", "Talk to sales.", false)
        };
        group.SelectedValue = "pro";
        var window = TestHelpers.Show(group);
        try
        {
            group.SelectedIndex.ShouldBe(1);
            var row = group.ContainerFromIndex(0).ShouldBeOfType<RadioGroupItem>();
            row.Description.ShouldBe("For side projects.");
            TestHelpers.Descendant<TextBlock>(row, "PART_Description").Text.ShouldBe("For side projects.");
            AutomationProperties.GetName(row).ShouldBe("Hobby");
            var peer = ControlAutomationPeer.CreatePeerForElement(row);
            peer.GetName().ShouldBe("Hobby");
            peer.GetHelpText().ShouldBe("For side projects.");
            group.ContainerFromIndex(2)!.IsEnabled.ShouldBeFalse();

            // The disabled row is skipped like a disabled RadioOption.
            group.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("hobby");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Row_Has_Its_Own_Token()
    {
        var group = Plans();
        var window = TestHelpers.Show(group);
        try
        {
            var row = group.ContainerFromIndex(0).ShouldBeOfType<RadioGroupItem>();
            row.TryFindResource(RadioGroupItemTokens.Key(RadioGroupItemTokenKind.MinRowHeight), out var minHeight).ShouldBeTrue();
            minHeight.ShouldBe(0d);
            row.MinHeight.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Orientation_Horizontal_Lays_Out_Options_Side_By_Side_And_Tracks_Highlight()
    {
        var group = new RadioGroup
        {
            Orientation = Orientation.Horizontal,
            ItemsSource = new[] { new RadioOption("1", "1"), new RadioOption("2", "2"), new RadioOption("3", "3") },
            SelectedValue = "1"
        };
        var window = TestHelpers.Show(group);
        try
        {
            group.Classes.ShouldContain(RadioGroup.HorizontalPseudoClass);
            var panel = group.ItemsPanelRoot.ShouldBeAssignableTo<StackPanel>()!;
            panel.Orientation.ShouldBe(Orientation.Horizontal);

            var first = group.ContainerFromIndex(0).ShouldBeOfType<RadioGroupItem>();
            var second = group.ContainerFromIndex(1).ShouldBeOfType<RadioGroupItem>();
            var third = group.ContainerFromIndex(2).ShouldBeOfType<RadioGroupItem>();
            second.Bounds.X.ShouldBeGreaterThan(first.Bounds.X);
            third.Bounds.X.ShouldBeGreaterThan(second.Bounds.X);
            second.Bounds.Y.ShouldBe(first.Bounds.Y, 0.01);

            var highlight = TestHelpers.Descendant<GlidingIndicator>(group);
            highlight.IsVisible.ShouldBeTrue();
            highlight.HasTarget.ShouldBeTrue();

            group.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            var thirdOrigin = third.TranslatePoint(default, highlight)!.Value;
            highlight.CurrentRect.X.ShouldBe(thirdOrigin.X + 0.5, 0.01);
            highlight.CurrentRect.Width.ShouldBe(third.Bounds.Width - 1, 0.01);

            // Runtime orientation switch back to Vertical updates the items panel and indicator.
            group.Orientation = Orientation.Vertical;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            group.Classes.ShouldNotContain(RadioGroup.HorizontalPseudoClass);
            group.ItemsPanelRoot.ShouldBeAssignableTo<StackPanel>()!.Orientation.ShouldBe(Orientation.Vertical);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Variant_Borderless_Removes_Row_Chrome_Hides_Highlight_And_Keeps_Keyboard_And_Focus()
    {
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var group = new RadioGroup
        {
            Orientation = Orientation.Horizontal,
            Variant = RadioGroupVariant.Borderless,
            ItemsSource = new[]
            {
                new RadioOption("1", "1"),
                new RadioOption("2", "2") { IsEnabled = false },
                new RadioOption("3", "3")
            },
            SelectedValue = "1"
        };
        var after = new Avalonia.Controls.Button { Content = "After" };
        var window = TestHelpers.Show(new StackPanel { Children = { before, group, after } });
        try
        {
            group.Classes.ShouldContain(RadioGroup.BorderlessPseudoClass);
            var highlight = TestHelpers.Descendant<GlidingIndicator>(group);
            highlight.IsVisible.ShouldBeFalse();

            var first = group.ContainerFromIndex(0).ShouldBeOfType<RadioGroupItem>();
            first.Classes.ShouldContain(RadioGroupItem.BorderlessPseudoClass);
            var rowBorder = TestHelpers.Descendant<Border>(first, "PART_Row");
            rowBorder.BorderThickness.ShouldBe(new Thickness(0));
            rowBorder.CornerRadius.ShouldBe(new CornerRadius(0));

            // Single tab stop and keyboard focus ring still work in borderless mode.
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            first.IsFocused.ShouldBeTrue();
            TestHelpers.IsFocusRingVisible(first).ShouldBeTrue();

            // Right arrow skips disabled item 2 and lands on item 3.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("3");
            group.ContainerFromIndex(2)!.IsFocused.ShouldBeTrue();

            // Next Tab leaves the group as a single tab stop.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            after.IsFocused.ShouldBeTrue();

            // Switching back to Default restores border chrome and highlight.
            group.Variant = RadioGroupVariant.Default;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            group.Classes.ShouldNotContain(RadioGroup.BorderlessPseudoClass);
            first.Classes.ShouldNotContain(RadioGroupItem.BorderlessPseudoClass);
            highlight.IsVisible.ShouldBeTrue();
            rowBorder.BorderThickness.Left.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Arrows_Flip_In_RightToLeft_FlowDirection()
    {
        var group = new RadioGroup
        {
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.RightToLeft,
            ItemsSource = new[] { new RadioOption("1", "1"), new RadioOption("2", "2"), new RadioOption("3", "3") },
            SelectedValue = "1"
        };
        var window = TestHelpers.Show(group);
        try
        {
            group.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("2");
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

    public sealed record PlanRow(string Id, string Name, string Summary, bool IsAvailable);
}
