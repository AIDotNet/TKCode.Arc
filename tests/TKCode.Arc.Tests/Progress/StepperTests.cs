using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class StepperTests
{
    private static AvaloniaList<StepperStep> Steps() =>
    [
        new("Account", "Create your login"), new("Shipping", "Where should we send it"),
        new("Payment", "Card or invoice"), new("Review", "Check and confirm")
    ];

    [Fact]
    public void Connectors_Fill_One_After_Another_From_The_Previous_Step()
    {
        const double gap = 0.08;
        Stepper.DelayAt(0, 0, 3, gap).ShouldBe(0);
        Stepper.DelayAt(1, 0, 3, gap).ShouldBe(0.08, 1e-9);
        Stepper.DelayAt(2, 0, 3, gap).ShouldBe(0.16, 1e-9);
        Stepper.DelayAt(3, 0, 3, gap).ShouldBe(0);
        // Going back drains from the far end.
        Stepper.DelayAt(2, 3, 0, gap).ShouldBe(0);
        Stepper.DelayAt(0, 3, 0, gap).ShouldBe(0.16, 1e-9);
    }

    [AvaloniaFact]
    public void Steps_Get_State_Glyphs_And_Details()
    {
        var steps = Steps();
        var stepper = new Stepper { ItemsSource = steps, CurrentIndex = 2, Width = 640 };
        var window = TestHelpers.Show(stepper);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var items = stepper.Items;
            items.Count.ShouldBe(4);
            items[0].Status.ShouldBe(StepperStepStatus.Complete);
            items[0].Marker!.Kind.ShouldBe(StepperGlyphKind.Check);
            items[2].Status.ShouldBe(StepperStepStatus.Current);
            items[2].Marker!.IsRingShown.ShouldBeTrue();
            items[3].Status.ShouldBe(StepperStepStatus.Upcoming);
            items[3].Marker!.Kind.ShouldBe(StepperGlyphKind.Number);
            items[0].Fill.ShouldBe(1);
            items[2].Fill.ShouldBe(0);
            AutomationProperties.GetName(items[0]).ShouldBe("Account, Completed");
            AutomationProperties.GetName(items[3]).ShouldBe("Review, Not started");
            AutomationProperties.GetItemStatus(stepper).ShouldBe("Step 3 of 4: Payment");

            steps[2].Error = "Card declined";
            Dispatcher.UIThread.RunJobs();
            items[2].Status.ShouldBe(StepperStepStatus.Error);
            items[2].Marker!.Kind.ShouldBe(StepperGlyphKind.Error);
            items[2].Detail.ShouldBe("Card declined");

            stepper.Details = StepperDetails.Current;
            Dispatcher.UIThread.RunJobs();
            items[0].Detail.ShouldBeNull();
            items[2].Detail.ShouldBe("Card declined");

            stepper.CurrentIndex = 4;
            Dispatcher.UIThread.RunJobs();
            stepper.IsComplete.ShouldBeTrue();
            AutomationProperties.GetItemStatus(stepper).ShouldBe("All steps complete");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Horizontal_Steppers_Fold_Into_Markers_With_A_Caption()
    {
        var stepper = new Stepper { ItemsSource = Steps(), CurrentIndex = 1, Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var window = TestHelpers.Show(stepper);
        try
        {
            stepper.Classes.ShouldContain(Stepper.CompactPseudoClass);
            stepper.IsCaptionVisible.ShouldBeTrue();
            stepper.CaptionLabel.ShouldBe("Shipping");
            stepper.CaptionDetail.ShouldBe("Where should we send it");
            stepper.Orientation = Orientation.Vertical;
            Dispatcher.UIThread.RunJobs();
            stepper.IsCaptionVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Between_Reachable_Steps_And_Selects_Completed_Ones()
    {
        var stepper = new Stepper { ItemsSource = Steps(), CurrentIndex = 2, IsNavigable = true, Width = 640 };
        var selected = new List<int>();
        stepper.StepSelected += (_, e) => selected.Add(e.Index);
        var window = TestHelpers.Show(stepper);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var items = stepper.Items;
            items[0].IsTabStop.ShouldBeTrue();
            items[2].IsTabStop.ShouldBeFalse();
            items[3].Focusable.ShouldBeFalse();
            items[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            items[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            items[2].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            items[2].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            items[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            selected.ShouldBe([0]);
            stepper.CurrentIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Steppers_Ignore_Selection()
    {
        var stepper = new Stepper { ItemsSource = Steps(), CurrentIndex = 2 };
        var window = TestHelpers.Show(stepper);
        try
        {
            stepper.Select(0);
            stepper.CurrentIndex.ShouldBe(2);
            stepper.Items[0].Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Steps_Work_Without_A_Source()
    {
        var stepper = new Stepper { Steps = { new StepperStep("One"), new StepperStep("Two") }, CurrentIndex = 1 };
        var window = TestHelpers.Show(stepper);
        try
        {
            stepper.Items.Count.ShouldBe(2);
            stepper.Items[0].Status.ShouldBe(StepperStepStatus.Complete);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connector_Fill_Springs_With_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var stepper = new Stepper { ItemsSource = Steps(), CurrentIndex = 0, Width = 640 };
        var window = TestHelpers.Show(stepper, ArcMotionLevel.Full);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        try
        {
            stepper.CurrentIndex = 3;
            clock.Advance(TimeSpan.FromMilliseconds(90));
            var first = stepper.Items[0].Fill;
            var third = stepper.Items[2].Fill;
            first.ShouldBeGreaterThan(third);
            clock.Advance(TimeSpan.FromSeconds(2));
            stepper.Items[2].Fill.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
