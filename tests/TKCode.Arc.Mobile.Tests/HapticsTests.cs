using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Cross-platform haptic feedback tests: <see cref="Haptics"/> platform bridge, global disable switch, and triggers
/// wired into mobile controls (<see cref="Switch"/>, <see cref="Sheet"/>, <see cref="ActionSheet"/>).
/// </summary>
public class HapticsTests
{
    private sealed class RecordingHaptics : IHapticsPlatform
    {
        public List<HapticKind> Calls { get; } = [];

        public void Trigger(HapticKind kind) => Calls.Add(kind);
    }

    [Fact]
    public void Trigger_Is_NoOp_Without_Platform_Or_When_Disabled()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        try
        {
            Haptics.Platform = null;
            Haptics.IsEnabled = true;
            Should.NotThrow(() => Haptics.Trigger(HapticKind.Medium));

            var recorder = new RecordingHaptics();
            Haptics.Platform = recorder;
            Haptics.IsEnabled = false;
            Haptics.Trigger(HapticKind.Light);
            recorder.Calls.ShouldBeEmpty();

            Haptics.IsEnabled = true;
            Haptics.Trigger(HapticKind.Selection);
            Haptics.Trigger(HapticKind.Warning);
            recorder.Calls.ShouldBe([HapticKind.Selection, HapticKind.Warning]);
        }
        finally
        {
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void Sheet_Detent_Change_Triggers_Light_Haptic()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;
        var sheet = new Sheet { Detents = SheetDetents.Parse("0.4, 0.9"), Content = new Border() };
        var window = new Window { Content = sheet, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            recorder.Calls.Clear();

            sheet.Expand();
            Dispatcher.UIThread.RunJobs();
            recorder.Calls.ShouldContain(HapticKind.Light);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void ActionSheet_Destructive_Row_Triggers_Warning_Haptic()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;
        var delete = new ActionSheetItem { Content = "Delete", IsDestructive = true };
        var sheet = new ActionSheet { Actions = { delete } };
        var window = new Window { Content = sheet, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            delete.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            recorder.Calls.ShouldContain(HapticKind.Warning);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }

    [AvaloniaFact]
    public void Switch_Click_Triggers_Selection_Haptic()
    {
        var previousPlatform = Haptics.Platform;
        var previousEnabled = Haptics.IsEnabled;
        var recorder = new RecordingHaptics();
        Haptics.Platform = recorder;
        Haptics.IsEnabled = true;
        var toggle = new Switch();
        var window = new Window { Content = toggle, Width = 200, Height = 100 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            recorder.Calls.ShouldBe([HapticKind.Selection]);
        }
        finally
        {
            window.Close();
            Haptics.Platform = previousPlatform;
            Haptics.IsEnabled = previousEnabled;
        }
    }
}
