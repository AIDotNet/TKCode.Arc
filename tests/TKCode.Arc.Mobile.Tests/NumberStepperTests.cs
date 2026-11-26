using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="NumberStepper"/>: bounds clamping, wrapping, decimal step precision, button disabling at limits,
/// keyboard navigation and haptic feedback.
/// </summary>
public class NumberStepperTests
{
    private sealed class RecordingHaptics : IHapticsPlatform
    {
        public List<HapticKind> Calls { get; } = [];

        public void Trigger(HapticKind kind) => Calls.Add(kind);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Increment_And_Decrement_Clamp_Update_Button_Enabled_And_Raise_ValueChanged()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;

        var stepper = new NumberStepper { Minimum = 0, Maximum = 2, Step = 1, Value = 1 };
        var changes = new List<(double Old, double New)>();
        stepper.ValueChanged += (_, e) => changes.Add((e.OldValue, e.NewValue));

        var window = new Window { Content = stepper, Width = 300, Height = 120 };
        window.Show();
        try
        {
            Settle(window);
            var dec = stepper.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == "PART_Decrement");
            var inc = stepper.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == "PART_Increment");

            dec.IsEnabled.ShouldBeTrue();
            inc.IsEnabled.ShouldBeTrue();

            stepper.Increment();
            Settle(window);
            stepper.Value.ShouldBe(2);
            stepper.FormattedValue.ShouldBe("2");
            inc.IsEnabled.ShouldBeFalse();
            recorder.Calls.ShouldBe([HapticKind.Selection]);

            // Stepping past the upper bound triggers a light boundary haptic without raising ValueChanged.
            stepper.Increment();
            Settle(window);
            stepper.Value.ShouldBe(2);
            recorder.Calls.ShouldBe([HapticKind.Selection, HapticKind.Light]);

            stepper.Decrement();
            stepper.Decrement();
            Settle(window);
            stepper.Value.ShouldBe(0);
            dec.IsEnabled.ShouldBeFalse();
            changes.ShouldBe([(1d, 2d), (2d, 1d), (1d, 0d)]);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void Wrapping_And_Decimal_Steps_Avoid_Floating_Point_Drift()
    {
        var stepper = new NumberStepper
        {
            Minimum = 0,
            Maximum = 0.3,
            Step = 0.1,
            Value = 0.1,
            IsWrapping = true,
        };
        var window = new Window { Content = stepper, Width = 300, Height = 120 };
        window.Show();
        try
        {
            Settle(window);
            stepper.Increment();
            stepper.Increment();
            stepper.Value.ShouldBe(0.3);
            stepper.Increment();
            stepper.Value.ShouldBe(0);
            stepper.Decrement();
            stepper.Value.ShouldBe(0.3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Arrows_Home_And_End_Adjust_Value()
    {
        var stepper = new NumberStepper { Minimum = 1, Maximum = 10, Step = 2, Value = 3 };
        var window = new Window { Content = stepper, Width = 300, Height = 120 };
        window.Show();
        try
        {
            Settle(window);
            stepper.Focus();
            stepper.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
            stepper.Value.ShouldBe(5);
            stepper.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            stepper.Value.ShouldBe(3);
            stepper.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
            stepper.Value.ShouldBe(10);
            stepper.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
            stepper.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
