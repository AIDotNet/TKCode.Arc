using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="WheelPicker"/>: selection sync, display member binding, keyboard navigation, haptics and
/// visible row layout.
/// </summary>
public class WheelPickerTests
{
    private sealed record City(string Name);

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
    public void Syncs_Selection_Updates_Offset_And_Raises_SelectionChanged()
    {
        var picker = new WheelPicker
        {
            Items = { "09:00", "10:00", "11:00", "12:00", "13:00" },
        };
        var changes = new List<(int OldIndex, int NewIndex, object? NewItem)>();
        picker.SelectionChanged += (_, e) => changes.Add((e.OldIndex, e.NewIndex, e.NewItem));

        var window = new Window { Content = picker, Width = 320, Height = 260 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            picker.SelectedIndex.ShouldBe(0);
            picker.SelectedItem.ShouldBe("09:00");
            picker.ScrollOffset.ShouldBe(0, 0.5);

            picker.SelectedIndex = 3;
            Settle(window);
            picker.SelectedItem.ShouldBe("12:00");
            picker.ScrollOffset.ShouldBe(3 * 40, 0.5);

            picker.SelectedItem = "10:00";
            Settle(window);
            picker.SelectedIndex.ShouldBe(1);
            picker.ScrollOffset.ShouldBe(40, 0.5);

            changes.Last().ShouldBe((3, 1, (object?)"10:00"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Navigation_Moves_Selection_And_Triggers_Haptic()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;

        var picker = new WheelPicker
        {
            Items = { "Small", "Medium", "Large" },
            SelectedIndex = 0,
        };
        var window = new Window { Content = picker, Width = 320, Height = 260 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            picker.Focus();
            picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Settle(window);
            picker.SelectedIndex.ShouldBe(1);
            recorder.Calls.ShouldContain(HapticKind.Selection);

            picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
            Settle(window);
            picker.SelectedIndex.ShouldBe(2);

            picker.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
            Settle(window);
            picker.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void DisplayMemberBinding_Renders_Item_Labels()
    {
        var picker = new WheelPicker
        {
            ItemsSource = new[] { new City("Tokyo"), new City("Zurich"), new City("Seattle") },
            DisplayMemberBinding = new Binding(nameof(City.Name)),
        };
        var window = new Window { Content = picker, Width = 320, Height = 260 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            var texts = picker.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("Tokyo");
            texts.ShouldContain("Zurich");
            texts.ShouldContain("Seattle");
        }
        finally
        {
            window.Close();
        }
    }
}
