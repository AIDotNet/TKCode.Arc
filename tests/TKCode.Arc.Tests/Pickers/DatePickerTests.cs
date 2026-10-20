using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Pickers;

public class DatePickerTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 520;
        window.Height = 640;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var picker = new DatePicker { Label = "Due date" };
        var window = Show(picker);
        try
        {
            picker.Placeholder.ShouldBe("Select a date");
            picker.SelectedDate.ShouldBeNull();
            picker.IsDropDownOpen.ShouldBeFalse();
            var value = TestHelpers.Descendant<PickerRollText>(picker);
            value.IsPlaceholder.ShouldBeTrue();
            value.Text.ShouldBe("Select a date");
            picker.Bounds.Height.ShouldBeGreaterThan(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selected_Date_Is_Formatted_In_Parts()
    {
        var picker = new DatePicker { Culture = EnUs, SelectedDate = new DateOnly(2026, 10, 20) };
        var window = Show(picker);
        try
        {
            picker.Text.ShouldBe("Oct 20, 2026");
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("Oct 20, 2026");
            picker.SelectedDate = new DateOnly(2026, 11, 3);
            Dispatcher.UIThread.RunJobs();
            picker.Text.ShouldBe("Nov 3, 2026");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_Date_Commits_On_Enter_And_Invalid_Text_Reverts()
    {
        var changes = new List<DateOnly?>();
        var picker = new DatePicker { Culture = EnUs, SelectedDate = new DateOnly(2026, 10, 20) };
        picker.SelectedDateChanged += (_, e) => changes.Add(e.NewDate);
        var window = Show(picker);
        try
        {
            picker.Focus();
            picker.Text = "10/21/2026";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedDate.ShouldBe(new DateOnly(2026, 10, 21));
            picker.Text.ShouldBe("Oct 21, 2026");

            picker.Text = "not a date";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedDate.ShouldBe(new DateOnly(2026, 10, 21));
            picker.Text.ShouldBe("Oct 21, 2026");

            picker.Text = string.Empty;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedDate.ShouldBeNull();
            changes.ShouldBe([new DateOnly(2026, 10, 21), null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typed_Date_Outside_The_Limits_Is_Rejected()
    {
        var picker = new DatePicker
        {
            Culture = EnUs,
            MinDate = new DateOnly(2026, 10, 1),
            MaxDate = new DateOnly(2026, 10, 31),
            DisabledDates = d => d.DayOfWeek == DayOfWeek.Sunday
        };
        var window = Show(picker);
        try
        {
            picker.Focus();
            picker.Text = "Nov 2, 2026";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            picker.SelectedDate.ShouldBeNull();
            picker.Text = "Oct 18, 2026"; // a Sunday
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            picker.SelectedDate.ShouldBeNull();
            picker.Text = "Oct 19, 2026";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            picker.SelectedDate.ShouldBe(new DateOnly(2026, 10, 19));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_Down_Opens_The_Calendar_And_Escape_Returns_Focus()
    {
        var picker = new DatePicker
        {
            Culture = EnUs,
            SelectedDate = new DateOnly(2026, 10, 20),
            MinDate = new DateOnly(2026, 1, 1),
            MaxDate = new DateOnly(2026, 12, 31)
        };
        var window = Show(picker);
        try
        {
            picker.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            var calendar = picker.PopupCalendar!;
            calendar.SelectedDate.ShouldBe(new DateOnly(2026, 10, 20));
            calendar.MinDate.ShouldBe(new DateOnly(2026, 1, 1));
            calendar.MaxDate.ShouldBe(new DateOnly(2026, 12, 31));
            calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 10, 1));
            var focused = window.FocusManager!.GetFocusedElement() as CalendarDayCell;
            focused.ShouldNotBeNull();
            focused.Date.ShouldBe(new DateOnly(2026, 10, 20));

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedDate.ShouldBe(new DateOnly(2026, 10, 21));
            picker.IsDropDownOpen.ShouldBeFalse(); // motion off: closes at once
            window.FocusManager!.GetFocusedElement().ShouldBe(picker);

            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBe(picker);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_From_The_Calendar_Never_Edit_The_Text()
    {
        var picker = new DatePicker { Culture = EnUs, SelectedDate = new DateOnly(2026, 10, 20) };
        var window = Show(picker);
        try
        {
            picker.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            window.KeyTextInput("x");
            Dispatcher.UIThread.RunJobs();
            picker.Text.ShouldBe("Oct 20, 2026");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Empties_The_Value_And_Closes()
    {
        var picker = new DatePicker { Culture = EnUs, SelectedDate = new DateOnly(2026, 10, 20) };
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var clear = picker.Panel!.ClearButton!;
            clear.IsEnabled.ShouldBeTrue();
            clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            picker.SelectedDate.ShouldBeNull();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formats_Follow_The_Culture()
    {
        var date = new DateOnly(2026, 10, 20);
        PickerDateFormat.Format(date, PickerDateFormat.MediumPattern(EnUs), EnUs).ShouldBe("Oct 20, 2026");
        var zh = CultureInfo.GetCultureInfo("zh-CN");
        PickerDateFormat.Format(date, PickerDateFormat.MediumPattern(zh), zh).ShouldBe("2026年10月20日");
        PickerDateFormat.FormatRange(new DateOnly(2026, 10, 1), date, EnUs).ShouldBe("Oct 1 – 20, 2026");
        PickerDateFormat.FormatRange(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4), EnUs).ShouldBe("Sep 28 – Oct 4, 2026");
        PickerDateFormat.FormatRange(new DateOnly(2025, 12, 28), new DateOnly(2026, 1, 3), EnUs).ShouldBe("Dec 28, 2025 – Jan 3, 2026");
        PickerDateFormat.TryParse("2026年10月21日", PickerDateFormat.MediumPattern(zh), zh, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(new DateOnly(2026, 10, 21));
        var parts = new List<PickerRollSegment>();
        PickerDateFormat.FormatParts(date, PickerDateFormat.MediumPattern(EnUs), EnUs, parts);
        parts.Select(p => p.Text).ShouldBe(["Oct", " ", "20", ", ", "2026"]);
        parts.Select(p => p.Rolls).ShouldBe([true, false, true, false, true]);
    }
}
