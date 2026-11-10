using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Widgets;

/// <summary>The date reel: wheel arithmetic, the day wheel following the month, range, keys, wheel and automation.</summary>
public class DateReelTests
{
    private static DateReel Reel(DateTime date) => new() { SelectedDate = date, Width = 300 };

    [Fact]
    public void Looping_wheels_wrap_and_turn_the_short_way()
    {
        DateReelMath.Wrap(-1, 12).ShouldBe(11);
        DateReelMath.Wrap(25, 12).ShouldBe(1);
        DateReelMath.IndexAt(12.4, 12, true).ShouldBe(0);
        DateReelMath.IndexAt(12.4, 12, false).ShouldBe(11);
        // From December (11) to January (0) is one row on, not eleven back.
        DateReelMath.PositionFor(11, 0, 12, true).ShouldBe(12);
        DateReelMath.PositionFor(23, 10, 12, true).ShouldBe(22);
        DateReelMath.PositionFor(5, 2, 12, false).ShouldBe(2);
    }

    [Fact]
    public void A_flick_coasts_further_the_faster_it_goes_and_rests_on_a_row()
    {
        DateReelMath.Settle(3.2, 0, 31, true, 0.995).ShouldBe(3);
        var slow = DateReelMath.Settle(3, 10, 31, true, 0.995);
        var fast = DateReelMath.Settle(3, 40, 31, true, 0.995);
        slow.ShouldBeGreaterThan(3);
        fast.ShouldBeGreaterThan(slow);
        (fast % 1).ShouldBe(0);
        // A plain wheel stops at its last row.
        DateReelMath.Settle(195, 200, 201, false, 0.995).ShouldBe(200);
        DateReelMath.Settle(2, -200, 201, false, 0.995).ShouldBe(0);
    }

    [Fact]
    public void Rows_bend_away_on_a_drum()
    {
        var step = DateReelMath.Step(5);
        var middle = DateReelMath.Project(0, step, 90);
        middle.Scale.ShouldBe(1);
        middle.Offset.ShouldBe(0);
        var next = DateReelMath.Project(1, step, 90);
        next.Offset.ShouldBe(36, 1.5);
        next.Scale.ShouldBeLessThan(1);
        DateReelMath.Project(5, step, 90).Visible.ShouldBeFalse();
        DateReelMath.DeltaAt(next.Offset, step, 90).ShouldBe(1, 1e-6);
    }

    [AvaloniaFact]
    public void The_wheels_show_the_date_and_the_day_wheel_has_the_month_length()
    {
        var reel = Reel(new DateTime(2024, 2, 10));
        var window = TestHelpers.Show(reel);
        try
        {
            reel.DayColumn.Count.ShouldBe(29);
            reel.DayColumn.SelectedIndex.ShouldBe(9);
            reel.MonthColumn.SelectedIndex.ShouldBe(1);
            reel.YearColumn.SelectedIndex.ShouldBe(2024 - 1900);
            reel.YearColumn.Count.ShouldBe(201);
            reel.Columns.Select(c => c.Part).ShouldBe([DateReelPart.Month, DateReelPart.Day, DateReelPart.Year]);
            reel.Order = DateReelOrder.YearMonthDay;
            reel.Columns.Select(c => c.Part).ShouldBe([DateReelPart.Year, DateReelPart.Month, DateReelPart.Day]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_to_a_shorter_month_keeps_the_day_inside_it()
    {
        var reel = Reel(new DateTime(2026, 1, 31));
        var changes = new List<(DateTime, DateTime)>();
        reel.SelectedDateChanged += (_, e) => changes.Add((e.OldDate, e.NewDate));
        var window = TestHelpers.Show(reel);
        try
        {
            reel.MonthColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(2026, 2, 28));
            reel.DayColumn.Count.ShouldBe(28);
            reel.DayColumn.SelectedIndex.ShouldBe(27);
            changes.ShouldBe([(new DateTime(2026, 1, 31), new DateTime(2026, 2, 28))]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_month_wheel_loops_without_changing_the_year()
    {
        var reel = Reel(new DateTime(2026, 1, 15));
        var window = TestHelpers.Show(reel);
        try
        {
            reel.MonthColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(2026, 12, 15));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(2026, 1, 15));
            // The day wheel loops too.
            reel.DayColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(2026, 1, 31));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_year_wheel_stops_at_its_ends_and_dates_stay_in_range()
    {
        var reel = Reel(new DateTime(2000, 6, 1));
        reel.MinDate = new DateTime(1990, 3, 10);
        reel.MaxDate = new DateTime(2010, 8, 20);
        var window = TestHelpers.Show(reel);
        try
        {
            reel.YearColumn.Count.ShouldBe(21);
            reel.YearColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(2010, 6, 1));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            reel.SelectedDate.Year.ShouldBe(2010);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(1990, 6, 1));
            // March 1990 starts before the range: the day turns back to the first allowed one.
            reel.MonthColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            reel.SelectedDate.ShouldBe(new DateTime(1990, 3, 10));
            reel.IsAllowed(DateReelPart.Month, 0).ShouldBeFalse();
            reel.IsAllowed(DateReelPart.Day, 2).ShouldBeFalse();
            reel.IsAllowed(DateReelPart.Day, 12).ShouldBeTrue();
            reel.SelectedDate = new DateTime(1950, 1, 1);
            reel.SelectedDate.ShouldBe(new DateTime(1990, 3, 10));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Left_and_right_move_between_the_wheels()
    {
        var reel = Reel(new DateTime(2026, 5, 5));
        var window = TestHelpers.Show(reel);
        try
        {
            reel.MonthColumn.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            reel.DayColumn.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            reel.YearColumn.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            reel.YearColumn.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            reel.DayColumn.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_mouse_wheel_turns_one_row_per_notch()
    {
        var reel = Reel(new DateTime(2026, 5, 5));
        var window = TestHelpers.Show(reel);
        try
        {
            window.Height = 400;
            Dispatcher.UIThread.RunJobs();
            var over = reel.DayColumn.TranslatePoint(new Point(reel.DayColumn.Bounds.Width / 2, reel.DayColumn.Bounds.Height / 2), window)!.Value;
            window.MouseWheel(over, new Vector(0, -1));
            reel.SelectedDate.ShouldBe(new DateTime(2026, 5, 6));
            window.MouseWheel(over, new Vector(0, 0.5));
            reel.SelectedDate.ShouldBe(new DateTime(2026, 5, 6));
            window.MouseWheel(over, new Vector(0, 0.5));
            reel.SelectedDate.ShouldBe(new DateTime(2026, 5, 5));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_wheel_is_a_named_spinner()
    {
        var reel = Reel(new DateTime(2026, 3, 14));
        reel.MonthLabel = "Birth month";
        var window = TestHelpers.Show(reel);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(reel.MonthColumn);
            peer.GetName().ShouldBe("Birth month");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Spinner);
            peer.GetHelpText().ShouldBe("March");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.Value.ShouldBe(2);
            range.SetValue(4);
            reel.SelectedDate.ShouldBe(new DateTime(2026, 5, 14));
        }
        finally
        {
            window.Close();
        }
    }
}
