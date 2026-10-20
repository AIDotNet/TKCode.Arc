using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Pickers;

public class TimePickerTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 480;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var picker = new TimePicker { Label = "Start", Culture = EnUs };
        var window = Show(picker);
        try
        {
            picker.SelectedTime.ShouldBe(new TimeOnly(9, 0));
            picker.MinuteStep.ShouldBe(15);
            picker.OptionCount.ShouldBe(96);
            picker.IsTwelveHour.ShouldBeTrue();
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("9:00 AM");
            picker.FormatTime(new TimeOnly(0, 30)).ShouldBe("12:30 AM");
            picker.FormatTime(new TimeOnly(13, 5)).ShouldBe("1:05 PM");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clock_Follows_The_Culture()
    {
        var picker = new TimePicker { Culture = CultureInfo.GetCultureInfo("de-DE"), SelectedTime = new TimeOnly(14, 30) };
        var window = Show(picker);
        try
        {
            picker.IsTwelveHour.ShouldBeFalse();
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("14:30");
            picker.ClockFormat = TimePickerClockFormat.TwelveHour;
            picker.IsTwelveHour.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_Moves_Wraps_And_Chooses()
    {
        var changes = new List<TimeOnly?>();
        var picker = new TimePicker { Culture = EnUs, MinuteStep = 30, SelectedTime = new TimeOnly(0, 0) };
        picker.SelectedTimeChanged += (_, e) => changes.Add(e.NewTime);
        var window = Show(picker);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            picker.List!.Count.ShouldBe(48);
            picker.List.ActiveIndex.ShouldBe(0);
            picker.List.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            picker.List.ActiveIndex.ShouldBe(47);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            picker.List.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 30));
            picker.IsDropDownOpen.ShouldBeFalse();
            changes.ShouldBe([new TimeOnly(0, 30)]);

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            picker.IsDropDownOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 30));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Centres_The_Chosen_Time()
    {
        var picker = new TimePicker { Culture = EnUs, SelectedTime = new TimeOnly(12, 0) };
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var scroll = picker.Panel!.Scroll!;
            var list = picker.List!;
            list.ActiveIndex.ShouldBe(48);
            var row = list.RowRect(48);
            var middle = scroll.Offset.Y + scroll.Viewport.Height / 2;
            Math.Abs(row.Center.Y - middle).ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }
}
