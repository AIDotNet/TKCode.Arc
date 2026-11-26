using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="TouchSlider"/>: clamping, step snapping, pointer drag with floating bubble, keyboard
/// adjustment and haptic cues.
/// </summary>
public class TouchSliderTests
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
    public void Drag_Shows_Bubble_Snaps_To_Step_And_Triggers_Haptics()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;

        var slider = new TouchSlider { Minimum = 0, Maximum = 100, Step = 25, Value = 0 };
        var changes = new List<double>();
        slider.ValueChanged += (_, e) => changes.Add(e.NewValue);

        var window = new Window { Content = slider, Width = 328, Height = 120 };
        window.Show();
        try
        {
            Settle(window);
            var bubble = slider.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Bubble");
            bubble.IsVisible.ShouldBeFalse();

            var y = slider.TranslatePoint(new Point(0, slider.Bounds.Height / 2), window)!.Value.Y;
            // Press near the middle (50%) to start dragging and show the bubble.
            window.MouseDown(new Point(164, y), MouseButton.Left);
            Settle(window);
            slider.IsDragging.ShouldBeTrue();
            bubble.IsVisible.ShouldBeTrue();
            slider.Value.ShouldBe(50);

            // Drag to the far right (100%) to hit the upper bound.
            window.MouseMove(new Point(320, y));
            Settle(window);
            slider.Value.ShouldBe(100);
            recorder.Calls.ShouldContain(HapticKind.Light);

            window.MouseUp(new Point(320, y), MouseButton.Left);
            Settle(window);
            slider.IsDragging.ShouldBeFalse();
            bubble.IsVisible.ShouldBeFalse();
            changes.ShouldContain(50d);
            changes.ShouldContain(100d);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void Keyboard_Arrows_Home_And_End_Update_Value()
    {
        var slider = new TouchSlider { Minimum = 10, Maximum = 50, Step = 10, Value = 20 };
        var window = new Window { Content = slider, Width = 300, Height = 100 };
        window.Show();
        try
        {
            Settle(window);
            slider.Focus();
            slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            slider.Value.ShouldBe(30);
            slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
            slider.Value.ShouldBe(20);
            slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
            slider.Value.ShouldBe(50);
            slider.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
            slider.Value.ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }
}
