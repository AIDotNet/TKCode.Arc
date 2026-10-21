using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class SegmentedControlTests
{
    private static SegmentedControl Views() => new()
    {
        ItemsSource = new[]
        {
            new SegmentedOption("day", "Day"),
            new SegmentedOption("week", "Week"),
            new SegmentedOption("month", "Month")
        },
        SelectedValue = "week"
    };

    [AvaloniaFact]
    public void Arrows_Wrap_And_Home_End_Jump()
    {
        var control = Views();
        var window = TestHelpers.Show(control);
        try
        {
            control.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("day");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("month");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue.ShouldBe("week");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_Control_Places_Its_Pill_When_Shown_Again()
    {
        var control = Views();
        var host = new Avalonia.Controls.Border { Child = control };
        var window = TestHelpers.Show(host);
        try
        {
            host.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            control.SelectedValue = "month";
            Dispatcher.UIThread.RunJobs();
            host.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            var pill = TestHelpers.Descendant<GlidingIndicator>(control);
            var segment = control.ContainerFromIndex(2)!;
            pill.CurrentRect.X.ShouldBe(segment.Bounds.X + 0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pill_Sits_Under_The_Selected_Segment()
    {
        var control = Views();
        var window = TestHelpers.Show(control);
        try
        {
            var pill = TestHelpers.Descendant<GlidingIndicator>(control);
            var segment = control.ContainerFromIndex(1)!;
            segment.Bounds.Height.ShouldBe(36);
            pill.CurrentRect.X.ShouldBe(segment.Bounds.X + 0.5, 0.01);
            pill.CurrentRect.Width.ShouldBe(segment.Bounds.Width - 1, 0.01);
            control.Bounds.Height.ShouldBe(36 + 6 + 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Raises_ItemIntent_With_The_Value()
    {
        var control = Views();
        var intents = new List<object?>();
        control.ItemIntent += (_, e) => intents.Add(e.Value);
        var window = TestHelpers.Show(control);
        try
        {
            control.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            intents.ShouldBe(["week", "day"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_Strings_Report_Themselves()
    {
        var control = new SegmentedControl { ItemsSource = new[] { "List", "Board" } };
        var window = TestHelpers.Show(control);
        try
        {
            control.SelectedIndex = 1;
            control.SelectedValue.ShouldBe("Board");
            control.ContainerFromIndex(0).ShouldBeOfType<SegmentedItem>().Content.ShouldBe("List");
        }
        finally
        {
            window.Close();
        }
    }
}
