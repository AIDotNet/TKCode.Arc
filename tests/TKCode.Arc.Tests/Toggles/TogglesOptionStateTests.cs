using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

/// <summary>
/// The option classes of the toggle groups share one state convention (issue #117): <c>IsEnabled</c> and
/// <c>IsVisible</c>, with the old <c>IsDisabled</c> kept as an obsolete alias until a major version removes it.
/// </summary>
public class TogglesOptionStateTests
{
    private static void AssertAliasInStep(AvaloniaObject option, StyledProperty<bool> enabled, StyledProperty<bool> disabled)
    {
        option.GetValue(enabled).ShouldBeTrue();
        option.GetValue(disabled).ShouldBeFalse();

        option.SetValue(disabled, true);
        option.GetValue(enabled).ShouldBeFalse();
        option.SetValue(disabled, false);
        option.GetValue(enabled).ShouldBeTrue();

        option.SetValue(enabled, false);
        option.GetValue(disabled).ShouldBeTrue();
        option.SetValue(enabled, true);
        option.GetValue(disabled).ShouldBeFalse();
    }

    // The obsolete IsDisabled is what the next tests exercise; the rest of the repository builds without CS0618.
#pragma warning disable CS0618

    [AvaloniaFact]
    public void The_Obsolete_IsDisabled_Stays_The_Inverse_Of_IsEnabled_On_Every_Option_Class()
    {
        AssertAliasInStep(new SegmentedOption("a", "A"), SegmentedOption.IsEnabledProperty, SegmentedOption.IsDisabledProperty);
        AssertAliasInStep(new ToggleGroupOption("a", "A"), ToggleGroupOption.IsEnabledProperty, ToggleGroupOption.IsDisabledProperty);
        AssertAliasInStep(new RadioOption("a", "A"), RadioOption.IsEnabledProperty, RadioOption.IsDisabledProperty);
        AssertAliasInStep(new RadioCardOption("a", "A"), RadioCardOption.IsEnabledProperty, RadioCardOption.IsDisabledProperty);

        // The CLR properties forward too, in both directions.
        var segment = new SegmentedOption("a", "A") { IsDisabled = true };
        segment.IsEnabled.ShouldBeFalse();
        segment.IsEnabled = true;
        segment.IsDisabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void XAML_Written_With_The_Old_IsDisabled_Still_Disables_The_Segment()
    {
        var root = (Border)AvaloniaRuntimeXamlLoader.Load(
            """
            <Border xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
                <arc:SegmentedControl>
                    <arc:SegmentedOption Value="a" Label="A" />
                    <arc:SegmentedOption Value="b" Label="B" IsDisabled="True" />
                </arc:SegmentedControl>
            </Border>
            """);
        var window = TestHelpers.Show(root);
        try
        {
            var control = TestHelpers.Descendant<SegmentedControl>(root);
            ((SegmentedOption)control.Items[1]!).IsEnabled.ShouldBeFalse();
            control.ContainerFromIndex(0)!.IsEnabled.ShouldBeTrue();
            control.ContainerFromIndex(1)!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

#pragma warning restore CS0618

    [AvaloniaFact]
    public void Every_Group_Follows_IsEnabled_And_IsVisible_Of_Its_Options()
    {
        var segments = new[] { new SegmentedOption("a", "A"), new SegmentedOption("b", "B"), new SegmentedOption("c", "C") };
        var toggles = new[] { new ToggleGroupOption("a", "A"), new ToggleGroupOption("b", "B"), new ToggleGroupOption("c", "C") };
        var radios = new[] { new RadioOption("a", "A"), new RadioOption("b", "B"), new RadioOption("c", "C") };
        var cards = new[] { new RadioCardOption("a", "A"), new RadioCardOption("b", "B"), new RadioCardOption("c", "C") };
        var controls = new SelectingControls(
            new SegmentedControl { ItemsSource = segments },
            new ToggleGroup { ItemsSource = toggles },
            new RadioGroup { ItemsSource = radios },
            new RadioCards { ItemsSource = cards });
        var window = TestHelpers.Show(new StackPanel { Children = { controls.Segmented, controls.Toggles, controls.Radios, controls.Cards } });
        try
        {
            foreach (var (control, hide, disable) in new (Avalonia.Controls.Primitives.SelectingItemsControl, Action<bool>, Action<bool>)[]
            {
                (controls.Segmented, v => segments[1].IsVisible = v, e => segments[1].IsEnabled = e),
                (controls.Toggles, v => toggles[1].IsVisible = v, e => toggles[1].IsEnabled = e),
                (controls.Radios, v => radios[1].IsVisible = v, e => radios[1].IsEnabled = e),
                (controls.Cards, v => cards[1].IsVisible = v, e => cards[1].IsEnabled = e)
            })
            {
                var name = control.GetType().Name;
                var container = control.ContainerFromIndex(1).ShouldNotBeNull(name);
                container.IsVisible.ShouldBeTrue(name);
                container.IsEnabled.ShouldBeTrue(name);

                hide(false);
                Dispatcher.UIThread.RunJobs();
                container.IsVisible.ShouldBeFalse(name);
                hide(true);
                Dispatcher.UIThread.RunJobs();
                container.IsVisible.ShouldBeTrue(name);

                disable(false);
                Dispatcher.UIThread.RunJobs();
                container.IsEnabled.ShouldBeFalse(name);
                disable(true);
                Dispatcher.UIThread.RunJobs();
                container.IsEnabled.ShouldBeTrue(name);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Billing_Toggle_Follows_IsEnabled_And_IsVisible_Of_Its_Options()
    {
        var options = new[] { new BillingToggleOption("monthly", "Monthly"), new BillingToggleOption("yearly", "Yearly"), new BillingToggleOption("lifetime", "Lifetime") };
        var toggle = new BillingToggle { ItemsSource = options, SelectedValue = "yearly" };
        var window = TestHelpers.Show(toggle);
        try
        {
            var yearly = toggle.ContainerFromIndex(1).ShouldNotBeNull();
            yearly.IsEnabled.ShouldBeTrue();
            options[1].IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            yearly.IsEnabled.ShouldBeFalse();
            options[1].IsEnabled = true;

            // A hidden period leaves the arrow keys; hiding the selected one keeps the selection.
            options[2].IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            toggle.ContainerFromIndex(2)!.IsVisible.ShouldBeFalse();
            yearly.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            toggle.SelectedValue.ShouldBe("monthly");
            toggle.SelectedValue = "yearly";
            options[1].IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            toggle.SelectedValue.ShouldBe("yearly");
            yearly.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed record SelectingControls(SegmentedControl Segmented, ToggleGroup Toggles, RadioGroup Radios, RadioCards Cards);

    [AvaloniaFact]
    public void The_Arrow_Keys_Skip_A_Hidden_Option()
    {
        var options = new[] { new SegmentedOption("day", "Day"), new SegmentedOption("week", "Week"), new SegmentedOption("month", "Month") };
        var control = new SegmentedControl { ItemsSource = options, SelectedValue = "week" };
        var window = TestHelpers.Show(control);
        try
        {
            options[0].IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            control.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            // Left from Week would reach Day, which is hidden: it wraps to Month.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("month");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("week");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_Selected_Option_Keeps_The_Selection_And_Takes_The_Indicator_With_It()
    {
        var options = new[] { new SegmentedOption("day", "Day"), new SegmentedOption("week", "Week"), new SegmentedOption("month", "Month") };
        var control = new SegmentedControl { ItemsSource = options, SelectedValue = "week" };
        var window = TestHelpers.Show(control);
        try
        {
            var pill = TestHelpers.Descendant<GlidingIndicator>(control);
            pill.HasTarget.ShouldBeTrue();

            options[1].IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("week");
            control.SelectedIndex.ShouldBe(1);
            pill.HasTarget.ShouldBeFalse();

            options[1].IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("week");
            pill.HasTarget.ShouldBeTrue();
            pill.CurrentRect.X.ShouldBe(control.ContainerFromIndex(1)!.Bounds.X + 0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
