using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Pickers;

public class CalendarTests
{
    private static (Calendar Calendar, Avalonia.Controls.Window Window) Show(Calendar calendar)
    {
        var window = TestHelpers.Show(calendar);
        window.Width = 500;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (calendar, window);
    }

    private static CalendarMonthStrip Strip(Calendar calendar) =>
        TestHelpers.Descendant<CalendarMonthStrip>(calendar);

    [AvaloniaFact]
    public void Every_Month_Shows_Six_Weeks()
    {
        var (calendar, window) = Show(new Calendar { DisplayMonth = new DateOnly(2026, 2, 1) });
        try
        {
            var pane = Strip(calendar).PresentPane!;
            pane.DayCells.Count.ShouldBe(42);
            pane.DayCells[0].Date.ShouldBe(new DateOnly(2026, 2, 1)); // Feb 1 2026 is a Sunday
            pane.DayCells[41].Date.ShouldBe(new DateOnly(2026, 3, 14));
            pane.DayCells[30].IsOutside.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FocusTabbableDay_Focuses_The_Selected_Day_Or_The_First_Open_One()
    {
        var calendar = new Calendar { SelectedDate = new DateOnly(2026, 10, 14) };
        var (_, window) = Show(calendar);
        try
        {
            calendar.FocusTabbableDay().ShouldBeTrue();
            var focused = Strip(calendar).PresentPane!.FindCell(new DateOnly(2026, 10, 14))!;
            focused.IsFocused.ShouldBeTrue();

            // Nothing selected and today outside the month: the first day that is not disabled.
            calendar.SelectedDate = null;
            calendar.DisplayMonth = new DateOnly(2030, 3, 1);
            calendar.DisabledDates = d => d.Day < 3;
            Dispatcher.UIThread.RunJobs();
            calendar.FocusTabbableDay().ShouldBeTrue();
            Strip(calendar).PresentPane!.FindCell(new DateOnly(2030, 3, 3))!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Focus_Across_Months_And_Selects()
    {
        var selected = new List<DateOnly>();
        var calendar = new Calendar { SelectedDate = new DateOnly(2026, 10, 31) };
        calendar.DateSelected += (_, e) => selected.Add(e.Date);
        var (_, window) = Show(calendar);
        try
        {
            var cell = Strip(calendar).PresentPane!.FindCell(new DateOnly(2026, 10, 31))!;
            cell.IsTabStop.ShouldBeTrue();
            cell.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            calendar.DisplayMonth.ShouldBe(new DateOnly(2026, 11, 1));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            calendar.SelectedDate.ShouldBe(new DateOnly(2026, 11, 1));
            selected.ShouldBe([new DateOnly(2026, 11, 1)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Days_Are_Skipped_In_The_Direction_Of_Travel()
    {
        var calendar = new Calendar
        {
            SelectedDate = new DateOnly(2026, 10, 16), // Friday
            DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        };
        var (_, window) = Show(calendar);
        try
        {
            Strip(calendar).PresentPane!.FindCell(new DateOnly(2026, 10, 16))!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            calendar.SelectedDate.ShouldBe(new DateOnly(2026, 10, 19));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_PageDown_Moves_A_Year_And_Clamps_The_Day()
    {
        var calendar = new Calendar { SelectedDate = new DateOnly(2028, 2, 29) };
        var (_, window) = Show(calendar);
        try
        {
            Strip(calendar).PresentPane!.FindCell(new DateOnly(2028, 2, 29))!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.Shift);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            calendar.SelectedDate.ShouldBe(new DateOnly(2029, 2, 28));
        }
        finally
        {
            window.Close();
        }
    }
}
