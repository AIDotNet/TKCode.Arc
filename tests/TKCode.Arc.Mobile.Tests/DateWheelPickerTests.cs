using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="DateWheelPicker"/>: date/time/date-time modes, leap-year day clamping, Done commit vs Cancel,
/// and Android system back dismissal.
/// </summary>
public class DateWheelPickerTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Date_Mode_Clamps_February_Days_For_Leap_And_NonLeap_Years_And_Commits_On_Done()
    {
        var picker = new DateWheelPicker
        {
            Content = "Birthday",
            Mode = DateWheelMode.Date,
            MinYear = 2020,
            MaxYear = 2025,
            SelectedDateTime = new DateTime(2024, 1, 31),
        };
        DateTime? raised = null;
        picker.ValueChanged += (_, e) => raised = e.NewValue;

        var page = new Page { Content = new ListGroup { Items = { picker } } };
        var window = new Window { Content = page, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            picker.DisplayText.ShouldBe("2024-01-31");

            picker.IsOpen = true;
            Settle(window);
            var sheet = picker.PickerSheet.ShouldNotBeNull();
            var wheels = sheet.GetVisualDescendants().OfType<WheelPicker>().ToList();
            wheels.Count.ShouldBe(3); // Year, Month, Day

            // Switch month to February in leap year 2024: day count shrinks to 29 and clamps 31 -> 29.
            wheels[1].SelectedIndex = 1;
            Settle(window);
            wheels[2].Items.Count.ShouldBe(29);
            wheels[2].SelectedIndex.ShouldBe(28);

            // Switch year to non-leap 2023: day count shrinks to 28 and clamps 29 -> 28.
            wheels[0].SelectedIndex = 3; // 2023
            Settle(window);
            wheels[2].Items.Count.ShouldBe(28);
            wheels[2].SelectedIndex.ShouldBe(27);

            picker.CommitCurrentWheels();
            Settle(window);
            picker.IsOpen.ShouldBeFalse();
            picker.SelectedDateTime.ShouldBe(new DateTime(2023, 2, 28));
            picker.DisplayText.ShouldBe("2023-02-28");
            raised.ShouldBe(new DateTime(2023, 2, 28));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Time_Mode_Respects_MinuteInterval_And_System_Back_Dismisses_Without_Committing()
    {
        var initial = new DateTime(2026, 5, 1, 9, 15, 0);
        var picker = new DateWheelPicker
        {
            Content = "Alarm",
            Mode = DateWheelMode.Time,
            MinuteInterval = 15,
            SelectedDateTime = initial,
        };
        var page = new Page { Content = new ListGroup { Items = { picker } } };
        var window = new Window { Content = page, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            picker.DisplayText.ShouldBe("09:15");

            picker.IsOpen = true;
            Settle(window);
            var wheels = picker.PickerSheet!.GetVisualDescendants().OfType<WheelPicker>().ToList();
            wheels.Count.ShouldBe(2);
            wheels[1].Items.Count.ShouldBe(4); // 00, 15, 30, 45

            // Change wheel to 10:30, then press Android system back to cancel.
            wheels[0].SelectedIndex = 10;
            wheels[1].SelectedIndex = 2;
            Settle(window);

            var back = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(back);
            Settle(window);
            back.Handled.ShouldBeTrue();
            picker.IsOpen.ShouldBeFalse();
            // Original value is unchanged.
            picker.SelectedDateTime.ShouldBe(initial);
        }
        finally
        {
            window.Close();
        }
    }
}
