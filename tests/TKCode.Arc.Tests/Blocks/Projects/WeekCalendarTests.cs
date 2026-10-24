using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>The week calendar control: ranges, views, widths, all-day row, details, names, keyboard, motion.</summary>
public class WeekCalendarTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateOnly Thursday = Monday.AddDays(3);

    private static DateTimeOffset Utc(DateOnly day, double hours) =>
        new(day.ToDateTime(TimeOnly.MinValue).AddHours(hours), TimeSpan.Zero);

    private static WeekCalendar Create(bool busy = false, Action<WeekCalendar>? setup = null)
    {
        var calendar = new WeekCalendar
        {
            TimeZone = TimeZoneInfo.Utc,
            Culture = CultureInfo.GetCultureInfo("en-US"),
            GridHeight = 600,
        };
        calendar.Now = Utc(Thursday, 14 + 20 / 60.0);
        foreach (var c in WeekCalendarDemoData.CreateCalendars())
        {
            calendar.Calendars.Add(c);
        }
        calendar.Events.AddRange(busy
            ? WeekCalendarDemoData.CreateBusyAllDayWeek(Monday, TimeZoneInfo.Utc)
            : WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc));
        setup?.Invoke(calendar);
        return calendar;
    }

    private static (Window Window, WeekCalendar Calendar) Show(double width = 1440, ArcMotionLevel level = ArcMotionLevel.Off,
                                                               bool busy = false, Action<WeekCalendar>? setup = null)
    {
        var calendar = Create(busy, setup);
        var window = TestHelpers.Show(calendar, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (window, calendar);
    }

    private static void Pump(int rounds = 4)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static WeekCalendarGridView Grid(WeekCalendar c) => c.GridView.ShouldNotBeNull();

    private static WeekCalendarGridPane Pane(WeekCalendar c) => Grid(c).Current.ShouldNotBeNull().Grid;

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    // Range and navigation --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Opens_On_The_Week_Of_Now_With_Its_Title()
    {
        var (window, c) = Show();
        try
        {
            c.Date.ShouldBe(Thursday);
            c.EffectiveView.ShouldBe(WeekCalendarView.Week);
            c.RangeStart.ShouldBe(Monday);
            c.RangeEnd.ShouldBe(Monday.AddDays(6));
            c.RangeTitle.ShouldBe("Oct 19 to 25, 2026");
            TestHelpers.Descendant<TextBlock>(c, WeekCalendar.TitlePart).Text.ShouldBe("Oct 19 to 25, 2026");
            TestHelpers.Descendant<Button>(c, WeekCalendar.TodayPart).IsEnabled.ShouldBeFalse();
            Pane(c).Cells.Count.ShouldBe(7);
            Grid(c).CornerText.ShouldBe("GMT");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_Previous_And_Today_Move_The_Range_And_Report_It()
    {
        var (window, c) = Show();
        try
        {
            var changes = new List<WeekCalendarRangeChangedEventArgs>();
            c.RangeChanged += (_, e) => changes.Add(e);
            c.Next();
            c.RangeStart.ShouldBe(Monday.AddDays(7));
            c.Date.ShouldBe(Thursday.AddDays(7));
            c.RangeTitle.ShouldBe("Oct 26 to Nov 1, 2026");
            TestHelpers.Descendant<Button>(c, WeekCalendar.TodayPart).IsEnabled.ShouldBeTrue();
            changes[^1].Direction.ShouldBe(1);
            changes[^1].Reason.ShouldBe(WeekCalendarRangeChangeReason.Step);
            changes[^1].OldStart.ShouldBe(Monday);
            changes[^1].NewEnd.ShouldBe(Monday.AddDays(13));
            c.Previous();
            c.Previous();
            c.RangeStart.ShouldBe(Monday.AddDays(-7));
            changes[^1].Direction.ShouldBe(-1);
            c.GoToToday();
            c.RangeStart.ShouldBe(Monday);
            c.Date.ShouldBe(Thursday);
            changes[^1].Reason.ShouldBe(WeekCalendarRangeChangeReason.Today);
            c.Announcer!.History[^1].ShouldBe("Oct 19 to 25, 2026");
            c.GoTo(new DateOnly(2026, 12, 31), animate: false);
            c.RangeTitle.ShouldBe("Dec 28, 2026 to Jan 3, 2027");
            c.Date = Monday.AddDays(1);
            c.RangeStart.ShouldBe(Monday);
            changes[^1].Reason.ShouldBe(WeekCalendarRangeChangeReason.Programmatic);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Views_Change_The_Days_And_Are_Announced()
    {
        var (window, c) = Show();
        try
        {
            var views = new List<WeekCalendarViewChangedEventArgs>();
            c.ViewChanged += (_, e) => views.Add(e);
            c.View = WeekCalendarView.WorkWeek;
            c.RangeStart.ShouldBe(Monday);
            c.RangeEnd.ShouldBe(Monday.AddDays(4));
            c.RangeTitle.ShouldBe("Oct 19 to 23, 2026");
            Pane(c).Cells.Count.ShouldBe(5);
            c.Announcer!.History[^1].ShouldBe("Work week, Oct 19 to 23, 2026");
            views[^1].OldView.ShouldBe(WeekCalendarView.Week);
            views[^1].NewView.ShouldBe(WeekCalendarView.WorkWeek);
            c.View = WeekCalendarView.Day;
            c.RangeStart.ShouldBe(Thursday);
            c.RangeTitle.ShouldBe("Thursday, October 22");
            c.Announcer.History[^1].ShouldBe("Day view, Thursday, October 22");
            AutomationProperties.GetName(TestHelpers.Descendant<Button>(c, WeekCalendar.PreviousPart)).ShouldBe("Previous day");
            c.Next();
            c.Date.ShouldBe(Thursday.AddDays(1));
            c.View = WeekCalendarView.Week;
            c.RangeStart.ShouldBe(Monday);
            AutomationProperties.GetName(TestHelpers.Descendant<Button>(c, WeekCalendar.NextPart)).ShouldBe("Next week");
            var options = (IReadOnlyList<SegmentedOption>)TestHelpers.Descendant<SegmentedControl>(c, WeekCalendar.ViewsPart).ItemsSource!;
            options.Select(o => o.Label).ShouldBe(["Week", "Work week", "Day"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Calendars_Show_Three_Days_Day_And_Agenda()
    {
        var (window, c) = Show(width: 390);
        try
        {
            c.View.ShouldBe(WeekCalendarView.Week);
            c.EffectiveView.ShouldBe(WeekCalendarView.ThreeDay);
            c.RangeStart.ShouldBe(Thursday);
            c.RangeEnd.ShouldBe(Thursday.AddDays(2));
            c.Classes.Contains(WeekCalendar.CompactPseudoClass).ShouldBeTrue();
            c.Classes.Contains(WeekCalendar.StackedPseudoClass).ShouldBeTrue();
            var segmented = TestHelpers.Descendant<SegmentedControl>(c, WeekCalendar.ViewsPart);
            ((IReadOnlyList<SegmentedOption>)segmented.ItemsSource!).Select(o => o.Label).ShouldBe(["3 days", "Day", "Agenda"]);
            segmented.SelectedValue.ShouldBe(WeekCalendarView.ThreeDay);
            AutomationProperties.GetName(TestHelpers.Descendant<Button>(c, WeekCalendar.NextPart)).ShouldBe("Next three days");
            c.Next();
            c.RangeStart.ShouldBe(Thursday.AddDays(3));
            c.View = WeekCalendarView.Agenda;
            c.EffectiveView.ShouldBe(WeekCalendarView.Agenda);
            // The agenda shows the week of the cursor day (Sunday the 25th).
            c.RangeStart.ShouldBe(Monday);
            Pump();
            c.AgendaView!.IsVisible.ShouldBeTrue();
            c.GridView!.IsVisible.ShouldBeFalse();
            // Wide again: the agenda shows the week.
            window.Width = 1200;
            Pump();
            c.EffectiveView.ShouldBe(WeekCalendarView.Week);
            c.GridView.IsVisible.ShouldBeTrue();
            ((IReadOnlyList<SegmentedOption>)segmented.ItemsSource!).Select(o => o.Label).ShouldBe(["Week", "Work week", "Day"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_Shows_From_1100_And_Gives_Way_To_The_Calendars_Popover()
    {
        var (window, c) = Show(width: 1200);
        try
        {
            c.IsSidebarShown.ShouldBeTrue();
            c.Classes.Contains(WeekCalendar.WidePseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<Control>(c, WeekCalendar.SidebarPart).IsVisible.ShouldBeTrue();
            c.CalendarsPopover!.IsVisible.ShouldBeFalse();
            window.Width = 1000;
            Pump();
            c.IsSidebarShown.ShouldBeFalse();
            c.Classes.Contains(WeekCalendar.RegularPseudoClass).ShouldBeTrue();
            c.CalendarsPopover.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(c, "PART_CalendarsLabel").IsVisible.ShouldBeTrue();
            window.Width = 740;
            Pump();
            c.Classes.Contains(WeekCalendar.SwatchOnlyPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(c, "PART_CalendarsLabel").IsVisible.ShouldBeFalse();
            c.IsSidebarVisible = true;
            Pump();
            c.IsSidebarShown.ShouldBeTrue();
            c.CalendarsPopover.IsVisible.ShouldBeFalse();
            // The popover lists the calendars and toggling one there hides its events.
            c.IsSidebarVisible = false;
            Pump();
            TestHelpers.Descendant<Button>(c, WeekCalendar.CalendarsButtonPart).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump(8);
            c.CalendarsPopover.IsOpen.ShouldBeTrue();
            var list = window.GetVisualDescendants().OfType<WeekCalendarSourceList>().First(l => l.Name == WeekCalendar.PopoverListPart);
            list.Toggles.Select(t => t.Label).ShouldBe(["Studio", "Crew", "Home", "Trips", "Health", "Learning"]);
            list.Toggles[4].IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hiding_A_Calendar_Removes_Its_Events_At_Once()
    {
        var (window, c) = Show();
        try
        {
            var toggled = new List<WeekCalendarVisibilityChangedEventArgs>();
            c.CalendarVisibilityChanged += (_, e) => toggled.Add(e);
            var before = Pane(c).Presenters.Count;
            var crew = Pane(c).Presenters.Count(p => p.Event!.CalendarId == WeekCalendarDemoData.Crew);
            crew.ShouldBeGreaterThan(0);
            var sidebar = TestHelpers.Descendant<WeekCalendarSourceList>(c, WeekCalendar.SidebarListPart);
            sidebar.Toggles[1].IsChecked = false;
            Pump();
            Pane(c).Presenters.Count.ShouldBe(before - crew);
            toggled.Single().Calendar.Id.ShouldBe(WeekCalendarDemoData.Crew);
            toggled.Single().IsVisible.ShouldBeFalse();
            c.Calendars[1].IsVisible.ShouldBeFalse();
            // Monday's pricing review had a neighbour; now it is alone and full width.
            Pane(c).Presenters.First(p => p.Event!.Title == "Pricing review").Slot.Width.ShouldBe(1);
            c.Calendars[1].IsVisible = true;
            Pump();
            Pane(c).Presenters.Count.ShouldBe(before);
            sidebar.Toggles[1].IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    // Grid content ---------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Overlapping_Events_Sit_Side_By_Side_In_The_Grid()
    {
        var (window, c) = Show();
        try
        {
            var pane = Pane(c);
            var pricing = pane.Presenters.First(p => p.Event!.Title == "Pricing review");
            var check = pane.Presenters.First(p => p.Event!.Title == "Quick check-in");
            var hiring = pane.Presenters.First(p => p.Event!.Title == "Hiring sync");
            pricing.Slot.ShouldBe(new WeekCalendarColumnSlot(0, 2, 1));
            check.Slot.ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
            hiring.Slot.ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
            var column = pane.Cells[0].Bounds;
            pricing.Bounds.X.ShouldBe(column.X, 0.5);
            pricing.Bounds.Width.ShouldBe(column.Width / 2, 0.5);
            pricing.Bounds.Y.ShouldBe(14 * 52 + 1, 0.01);
            pricing.Bounds.Height.ShouldBe(50, 0.01);
            // Header, all-day and grid columns line up to the pixel.
            var head = Grid(c).Current!.Head.DayButtons;
            for (var i = 0; i < 7; i++)
            {
                head[i].Bounds.X.ShouldBe(pane.Cells[i].Bounds.X);
                head[i].Bounds.Width.ShouldBe(pane.Cells[i].Bounds.Width);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Event_Blocks_Drop_Details_By_Their_Own_Size()
    {
        WeekCalendarEventPresenter.DetailFor(new Size(150, 18), true, true).ShouldBe((WeekCalendarEventLevel.Small, true, false, false, 1));
        WeekCalendarEventPresenter.DetailFor(new Size(120, 24), true, true).Time.ShouldBeFalse();
        WeekCalendarEventPresenter.DetailFor(new Size(150, 50), true, true).ShouldBe((WeekCalendarEventLevel.Medium, true, false, false, 1));
        WeekCalendarEventPresenter.DetailFor(new Size(76, 50), true, true).ShouldBe((WeekCalendarEventLevel.Medium, false, false, false, 2));
        WeekCalendarEventPresenter.DetailFor(new Size(150, 76), true, true).ShouldBe((WeekCalendarEventLevel.Large, true, false, false, 3));
        WeekCalendarEventPresenter.DetailFor(new Size(150, 89), true, true).Location.ShouldBeTrue();
        WeekCalendarEventPresenter.DetailFor(new Size(150, 89), false, true).Location.ShouldBeFalse();
        WeekCalendarEventPresenter.DetailFor(new Size(150, 128), true, true).Avatars.ShouldBeTrue();
        WeekCalendarEventPresenter.DetailFor(new Size(110, 128), true, true).Avatars.ShouldBeFalse();
        WeekCalendarEventPresenter.DetailFor(new Size(120, 128), true, true).Avatars.ShouldBeTrue();
        WeekCalendarEventPresenter.DetailFor(new Size(70, 128), true, true).ShouldBe((WeekCalendarEventLevel.Large, false, false, false, 3));

        var (window, c) = Show();
        try
        {
            var pane = Pane(c);
            var sync = pane.Presenters.First(p => p.Event!.Title == "Morning sync");
            sync.Level.ShouldBe(WeekCalendarEventLevel.Small);
            sync.Classes.Contains(WeekCalendarEventPresenter.SmallPseudoClass).ShouldBeTrue();
            var strategy = pane.Presenters.First(p => p.Event!.Title == "Offsite: strategy");
            strategy.Level.ShouldBe(WeekCalendarEventLevel.Large);
            strategy.ShowsLocation.ShouldBeTrue();
            strategy.ShowsAvatars.ShouldBeTrue();
            var narrow = pane.Presenters.First(p => p.Event!.Title == "Portfolio chat");
            narrow.Classes.Contains(WeekCalendarEventPresenter.NarrowPseudoClass).ShouldBeTrue();
            // Past events (before 2:20 PM on Thursday) are dimmed; later ones are not.
            pane.Presenters.First(p => p.Event!.Title == "Write launch brief").IsPast.ShouldBeTrue();
            pane.Presenters.First(p => p.Event!.Title == "Write launch brief").Classes.Contains(WeekCalendarEventPresenter.PastPseudoClass).ShouldBeTrue();
            pane.Presenters.First(p => p.Event!.Title == "Offsite: workshops").IsPast.ShouldBeFalse();
            c.Now = Utc(Thursday, 17);
            pane.Presenters.First(p => p.Event!.Title == "Offsite: workshops").IsPast.ShouldBeTrue();
            // The phone's three columns drop the avatars but keep the place.
            window.Width = 390;
            Pump();
            var phone = Pane(c);
            var workshops = phone.Presenters.First(p => p.Event!.Title == "Offsite: workshops");
            workshops.ShowsAvatars.ShouldBeFalse();
            workshops.ShowsLocation.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Current_Time_Shows_On_Todays_Range_Only()
    {
        var (window, c) = Show();
        try
        {
            var gutter = Grid(c).Body.Gutter;
            gutter.NowY.ShouldNotBeNull();
            gutter.NowY!.Value.ShouldBe((14 + 20 / 60.0) * 52, 0.01);
            // 2:20 PM is 17px from the 2 PM line: its label stays; at 2:05 it hides.
            gutter.IsLabelHidden(14).ShouldBeFalse();
            c.Now = Utc(Thursday, 14 + 5 / 60.0);
            gutter.IsLabelHidden(14).ShouldBeTrue();
            var layers = Pane(c).GetVisualChildren().OfType<WeekCalendarNowLayer>().ToList();
            layers.Count.ShouldBe(2);
            layers.ShouldAllBe(l => l.IsShown);
            c.Next();
            gutter.NowY.ShouldBeNull();
            Pane(c).GetVisualChildren().OfType<WeekCalendarNowLayer>().ShouldAllBe(l => !l.IsShown);
            c.ShowNowIndicator = false;
            c.GoToToday();
            gutter.NowY.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Grid_Opens_At_The_First_Events_Hour_And_Today_Brings_Now_Into_View()
    {
        var (window, c) = Show();
        try
        {
            Pump(6);
            var scroll = Grid(c).Scroller;
            scroll.Offset.Y.ShouldBe(8 * 52 - 8, 0.5);
            c.Next();
            scroll.Offset.Y.ShouldBe(8 * 52 - 8, 0.5);
            c.GoToToday();
            var viewport = scroll.Viewport.Height;
            viewport.ShouldBeGreaterThan(0);
            scroll.Offset.Y.ShouldBe((14 + 20 / 60.0) * 52 - viewport / 3, 1);
            c.ScrollToTime(new TimeOnly(6, 0));
            scroll.Offset.Y.ShouldBe(6 * 52, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Grid_Shows_Only_The_Hours_Asked_For()
    {
        var (window, c) = Show(setup: cal =>
        {
            cal.StartHour = 7;
            cal.EndHour = 20;
            cal.View = WeekCalendarView.WorkWeek;
        });
        try
        {
            Grid(c).Body.Bounds.Height.ShouldBe(13 * 52);
            var pane = Pane(c);
            pane.Cells.Count.ShouldBe(5);
            // The 8 AM event sits one hour down; the 7–9 PM dinner is cut at 8 PM.
            pane.Presenters.First(p => p.Event!.Title == "Pack for Lisbon").Bounds.Y.ShouldBe(53, 0.01);
            var dinner = pane.Presenters.First(p => p.Event!.Title == "Dinner with friends");
            dinner.EndMinute.ShouldBe(20 * 60);
            pane.Presenters.ShouldNotContain(p => p.Event!.Title == "Flight home" && p.EndMinute > 20 * 60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Time_Zones_Move_The_Same_Events()
    {
        var kolkata = TimeZoneInfo.CreateCustomTimeZone("test/kolkata", new TimeSpan(5, 30, 0), "Kolkata", "Kolkata");
        var (window, c) = Show(setup: cal => cal.TimeZone = kolkata);
        try
        {
            Grid(c).CornerText.ShouldBe("GMT+5:30");
            var sync = Pane(c).Presenters.First(p => p.Event!.Title == "Morning sync");
            // 9:00 UTC is 2:30 PM in Kolkata.
            sync.StartMinute.ShouldBe(14 * 60 + 30);
            sync.AccessibleName.ShouldContain("2:30 PM to 2:45 PM");
        }
        finally
        {
            window.Close();
        }
    }

    // All-day row ------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Full_All_Day_Row_Collapses_And_Expands()
    {
        var (window, c) = Show(busy: true);
        try
        {
            var grid = Grid(c);
            var allDay = grid.Current!.AllDay;
            allDay.Lanes.LaneCount.ShouldBe(5);
            grid.AllDayHeight.ShouldBe(80);
            grid.ExpandButton.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(grid.ExpandButton).ShouldBe("Show all all-day events");
            allDay.Bars.Count.ShouldBe(3);
            var chip = allDay.Chips.Single();
            chip.Label.ShouldBe("+3 more");
            c.IsAllDayExpanded = true;
            Pump();
            grid.AllDayHeight.ShouldBe(8 + 5 * 24);
            allDay.Bars.Count.ShouldBe(6);
            allDay.Chips.ShouldBeEmpty();
            AutomationProperties.GetName(grid.ExpandButton).ShouldBe("Show fewer all-day events");
            c.IsAllDayExpanded = false;
            Pump();
            grid.AllDayHeight.ShouldBe(80);
            // A week with fewer lanes never collapses.
            c.Next();
            grid.AllDayHeight.ShouldBe(32);
            grid.ExpandButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expanding_The_All_Day_Row_Animates_Its_Height_Only_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full, busy: true);
        try
        {
            var grid = Grid(c);
            c.IsAllDayExpanded = true;
            clock.Advance(TimeSpan.FromMilliseconds(100));
            grid.AllDayHeight.ShouldBeInRange(81, 151);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            grid.AllDayHeight.ShouldBe(128, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    // Names ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Every_Part_Has_A_Composed_Name()
    {
        var (window, c) = Show();
        try
        {
            var pane = Pane(c);
            AutomationProperties.GetName(pane).ShouldBe("Oct 19 to 25, 2026");
            (AutomationProperties.GetHelpText(pane) ?? string.Empty).ShouldContain("arrow keys");
            pane.Cells[3].AccessibleName.ShouldBe("Thursday, October 22, today, 9 events");
            pane.Cells[0].AccessibleName.ShouldBe("Monday, October 19, 8 events");
            var sync = pane.Presenters.First(p => p.Event!.Title == "Morning sync" && p.Day == Monday);
            sync.AccessibleName.ShouldBe("Morning sync, Monday, October 19, 9:00 AM to 9:15 AM, Crew, Video room");
            pane.Presenters.First(p => p.Event!.Title == "Write launch brief").AccessibleName
                .ShouldBe("Write launch brief, Monday, October 19, 9:30 AM to 11:30 AM, Studio");
            var hotel = Grid(c).Current!.AllDay.Bars.First(b => b.Event!.Title == "Hotel: Casa Azul");
            hotel.AccessibleName.ShouldBe("Hotel: Casa Azul, all day, Wednesday, October 21 to Friday, October 23, Trips");
            Grid(c).Current!.AllDay.Bars.First(b => b.Event!.Title == "Dad's birthday").AccessibleName
                .ShouldBe("Dad's birthday, all day, Thursday, October 22, Home");
            AutomationProperties.GetName(Grid(c).Current!.Head.DayButtons[0]).ShouldBe("Show Monday, October 19");
            var month = c.MiniMonth!;
            month.Title.ShouldBe("October 2026");
            month.CellFor(Thursday)!.AccessibleName.ShouldBe("Thursday, October 22");
            AutomationProperties.GetItemStatus(month.CellFor(Thursday)!).ShouldBe("Today");
            AutomationProperties.GetName(month.PreviousButton).ShouldBe("Previous month");
            AutomationProperties.GetName(TestHelpers.Descendant<Control>(c, WeekCalendar.SidebarPart)).ShouldBe("Calendar navigation");
            AutomationProperties.GetName(TestHelpers.Descendant<Button>(c, WeekCalendar.NewEventPart)).ShouldBe("New event");
            // An event's own name wins.
            c.Events[0].AutomationName = "Daily stand-up";
            Pump();
            pane.Presenters.First(p => ReferenceEquals(p.Event, c.Events[0])).AccessibleName.ShouldBe("Daily stand-up");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Agenda_Groups_The_Week_By_Day()
    {
        var (window, c) = Show(width: 390, setup: cal => cal.View = WeekCalendarView.Agenda);
        try
        {
            var pane = c.AgendaView!.Current.ShouldNotBeNull();
            var list = pane.List;
            list.Headers.Count.ShouldBe(7);
            list.Headers[3].IsToday.ShouldBeTrue();
            var wednesday = list.Rows.Where(r => r.Day == Monday.AddDays(2)).ToList();
            wednesday[0].IsAllDay.ShouldBeTrue();
            wednesday[0].Event!.Title.ShouldBe("Hotel: Casa Azul");
            wednesday[1].Event!.Title.ShouldBe("Pack for Lisbon");
            // The hotel repeats on each of its days.
            list.Rows.Count(r => r.Event!.Title == "Hotel: Casa Azul").ShouldBe(3);
            wednesday[1].AccessibleName.ShouldBe("Pack for Lisbon, 8:00 AM to 8:30 AM, Home");
            wednesday[0].AccessibleName.ShouldBe("Hotel: Casa Azul, all day, Trips");
            // An empty week says so for every day.
            c.Next();
            c.Next();
            c.Next();
            c.Next();
            c.Next();
            Pump();
            c.AgendaView.Current!.List.Empties.Count.ShouldBe(7);
            c.AgendaView.Current.List.Empties[0].Label.ShouldBe("Nothing planned");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Agenda_Headings_Stay_At_The_Top_While_Their_Day_Scrolls()
    {
        var (window, c) = Show(width: 390, setup: cal => cal.View = WeekCalendarView.Agenda);
        try
        {
            var pane = c.AgendaView!.Current!;
            var list = pane.List;
            pane.Scroller.Offset = new Vector(0, 100);
            Pump();
            var first = list.Headers[0];
            first.PinOffset.ShouldBe(96, 0.5);
            list.Headers[1].PinOffset.ShouldBe(0);
            // Near its section's end the next heading pushes it up.
            var mondayRows = list.Rows.Count(r => r.Day == Monday);
            var sectionBottom = 4 + WeekCalendarAgendaList.HeaderHeight + mondayRows * WeekCalendarAgendaList.RowHeight +
                                (mondayRows - 1) * WeekCalendarAgendaList.RowGap + 8;
            pane.Scroller.Offset = new Vector(0, sectionBottom - 10);
            Pump();
            first.PinOffset.ShouldBe(sectionBottom - 4 - WeekCalendarAgendaList.HeaderHeight, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // The month in the sidebar -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Month_Marks_The_Range_And_Picking_A_Day_Moves_There()
    {
        var (window, c) = Show(level: ArcMotionLevel.Off);
        try
        {
            var month = c.MiniMonth!;
            month.Rows.ShouldBe(5);
            month.Days.Count(d => d.IsInRange).ShouldBe(7);
            month.CellFor(Monday)!.IsRangeStart.ShouldBeTrue();
            month.CellFor(Monday.AddDays(6))!.IsRangeEnd.ShouldBeTrue();
            month.CellFor(Thursday)!.IsToday.ShouldBeTrue();
            month.CellFor(new DateOnly(2026, 9, 28))!.IsOutside.ShouldBeTrue();
            // The month buttons page only the month.
            month.NextButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            month.Title.ShouldBe("November 2026");
            c.RangeStart.ShouldBe(Monday);
            month.CellFor(new DateOnly(2026, 11, 11))!.Focus(NavigationMethod.Directional);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            c.Date.ShouldBe(new DateOnly(2026, 11, 11));
            c.RangeStart.ShouldBe(new DateOnly(2026, 11, 9));
            month.Title.ShouldBe("November 2026");
            Focused(window).ShouldBe(month.CellFor(new DateOnly(2026, 11, 11)));
            // Arrows move the focus between days, across months.
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Focused(window).ShouldBe(month.CellFor(new DateOnly(2026, 10, 28)));
            month.Title.ShouldBe("October 2026");
            c.View = WeekCalendarView.WorkWeek;
            month.Days.Count(d => d.IsInRange).ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    // Commands and events --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void New_Event_And_Event_Activation_Are_Reported()
    {
        var (window, c) = Show();
        try
        {
            var requested = new List<DateOnly>();
            c.NewEventRequested += (_, e) => requested.Add(e.Date);
            TestHelpers.Descendant<Button>(c, WeekCalendar.NewEventPart).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            requested.ShouldBe([Thursday]);
            var invoked = new List<WeekCalendarEvent>();
            c.EventInvoked += (_, e) => invoked.Add(e.Event);
            var brief = Pane(c).Presenters.First(p => p.Event!.Title == "Write launch brief");
            brief.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            invoked.Single().Title.ShouldBe("Write launch brief");
        }
        finally
        {
            window.Close();
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Days_And_The_Focus_Survives_A_Range_Change()
    {
        var (window, c) = Show();
        try
        {
            c.FocusDay(Monday.AddDays(5));
            Pump();
            Focused(window).ShouldBe(Pane(c).CellFor(Monday.AddDays(5)));
            KeyboardNavigation.GetIsTabStop(Pane(c).CellFor(Monday.AddDays(5))!).ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(Pane(c).CellFor(Monday)!).ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            c.Date.ShouldBe(Monday.AddDays(6));
            Focused(window).ShouldBe(Pane(c).CellFor(Monday.AddDays(6)));
            Grid(c).Current!.Head.DayButtons[6].IsCursor.ShouldBeTrue();
            // Past Sunday the next week comes in and the focus lands on its Monday (Arc drops it).
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            c.RangeStart.ShouldBe(Monday.AddDays(7));
            c.Date.ShouldBe(Monday.AddDays(7));
            Focused(window).ShouldBe(Pane(c).CellFor(Monday.AddDays(7)));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            c.RangeStart.ShouldBe(Monday);
            Focused(window).ShouldBe(Pane(c).CellFor(Monday.AddDays(6)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void T_D_And_W_Keep_The_Focus_On_The_Cursor_Day()
    {
        var (window, c) = Show();
        try
        {
            c.Next();
            c.FocusDay(Monday.AddDays(8));
            Pump();
            window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.None);
            Pump();
            c.RangeStart.ShouldBe(Monday);
            c.Date.ShouldBe(Thursday);
            Focused(window).ShouldBe(Pane(c).CellFor(Thursday));
            window.KeyPressQwerty(PhysicalKey.D, RawInputModifiers.None);
            Pump();
            c.View.ShouldBe(WeekCalendarView.Day);
            c.RangeStart.ShouldBe(Thursday);
            Focused(window).ShouldBe(Pane(c).CellFor(Thursday));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            c.Date.ShouldBe(Thursday.AddDays(1));
            Focused(window).ShouldBe(Pane(c).CellFor(Thursday.AddDays(1)));
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
            Pump();
            c.View.ShouldBe(WeekCalendarView.Week);
            c.RangeStart.ShouldBe(Monday);
            Focused(window).ShouldBe(Pane(c).CellFor(Thursday.AddDays(1)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Up_And_Down_Walk_The_Events_And_Left_And_Right_Go_To_The_Days()
    {
        var (window, c) = Show();
        try
        {
            var pane = Pane(c);
            var events = pane.Presenters;
            events[0].Day.ShouldBe(Monday);
            events[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Focused(window).ShouldBe(events[1]);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Focused(window).ShouldBe(events[0]);
            // Down from Monday's last event reaches Tuesday's first.
            var lastMonday = events.Last(p => p.Day == Monday);
            lastMonday.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            ((WeekCalendarEventPresenter)Focused(window)!).Day.ShouldBe(Monday.AddDays(1));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            Focused(window).ShouldBe(pane.CellFor(Monday.AddDays(2)));
            c.Date.ShouldBe(Monday.AddDays(2));
            // Tab order: the cursor cell is the grid's only stop, then the events by day and time.
            KeyboardNavigation.GetTabIndex(events[0]).ShouldBeLessThan(KeyboardNavigation.GetTabIndex(events[1]));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Focused_Today_Button_Hands_The_Focus_To_Todays_Cell()
    {
        var (window, c) = Show();
        try
        {
            c.Next();
            var today = TestHelpers.Descendant<Button>(c, WeekCalendar.TodayPart);
            today.IsEnabled.ShouldBeTrue();
            today.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            today.IsEnabled.ShouldBeFalse();
            Focused(window).ShouldBe(Pane(c).CellFor(Thursday));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Demo_Renders_In_Both_Widths()
    {
        var demos = GalleryDemos.All.Where(d => d.Component == nameof(WeekCalendar)).ToList();
        demos.Select(d => d.Name).ShouldBe(["BusyAllDay", "Editing", "Phone", "Preview", "TimeZones", "WorkHours"], ignoreOrder: true);
        foreach (var demo in demos)
        {
            var control = demo.Create();
            var window = TestHelpers.Show(control);
            try
            {
                foreach (var width in new[] { 1200.0, 500.0 })
                {
                    window.Width = width;
                    window.Height = 900;
                    Pump();
                    var calendars = window.GetVisualDescendants().OfType<WeekCalendar>().ToList();
                    calendars.ShouldNotBeEmpty(demo.Id);
                    calendars.ShouldAllBe(cal => cal.EffectiveEvents.Count > 0, demo.Id);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    // Motion ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Ranges_Slide_On_One_Spring_And_Finished_Panes_Are_Reused()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full);
        try
        {
            var grid = Grid(c);
            var width = grid.Current!.Head.Bounds.Width;
            width.ShouldBeGreaterThan(0);
            var first = grid.Current;
            c.Next();
            grid.Slots.Count.ShouldBe(2);
            var incoming = grid.Current!;
            incoming.ShouldNotBe(first);
            incoming.X.Value.ShouldBe(width, 0.01);
            c.Classes.Contains(WeekCalendar.SlidingPseudoClass).ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(340));
            // About 10% left after 340ms (Arc's fit), the leaving pane mirroring it.
            (incoming.X.Value / width).ShouldBeInRange(0.06, 0.16);
            (first.X.Value / width).ShouldBeInRange(-0.94, -0.84);
            incoming.Head.RenderTransform!.Value.M31.ShouldBe(incoming.X.Value, 0.01);
            incoming.Grid.RenderTransform!.Value.M31.ShouldBe(incoming.X.Value, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            Pump();
            grid.Slots.Count.ShouldBe(1);
            grid.PooledCount.ShouldBe(1);
            incoming.X.Value.ShouldBe(0);
            c.Classes.Contains(WeekCalendar.SlidingPseudoClass).ShouldBeFalse();
            // The next slide reuses the parked panes: no new ones are made.
            var panes = grid.Body.Viewport.Children.Count;
            c.Next();
            grid.Current.ShouldBe(first);
            grid.Body.Viewport.Children.Count.ShouldBe(panes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Interrupted_Slides_Keep_Their_Speed_And_Three_Panes_At_Most_Coexist()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full);
        try
        {
            var grid = Grid(c);
            c.Next();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var arriving = grid.Current!;
            var position = arriving.X.Value;
            var speed = arriving.X.Velocity;
            speed.ShouldBeLessThan(0);
            c.Next();
            grid.Slots.Count.ShouldBe(3);
            // The pane that was arriving now leaves from where it is, with the speed it had.
            arriving.X.Value.ShouldBe(position, 0.01);
            arriving.X.Velocity.ShouldBe(speed, 1);
            clock.Advance(TimeSpan.FromMilliseconds(16));
            arriving.X.Value.ShouldBeLessThan(position);
            c.Next();
            grid.Slots.Count.ShouldBe(3);
            c.RangeStart.ShouldBe(Monday.AddDays(21));
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            grid.Slots.Count.ShouldBe(1);
            grid.Current!.X.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_Ranges_And_Off_Jumps()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            var grid = Grid(c);
            var old = grid.Current!;
            c.Next();
            var incoming = grid.Current!;
            grid.Slots.Count.ShouldBe(2);
            incoming.X.Value.ShouldBe(0);
            incoming.Fade.Value.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            incoming.Fade.Value.ShouldBeInRange(0.2, 0.99);
            old.Fade.Value.ShouldBeInRange(0.01, 0.8);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Pump();
            grid.Slots.Count.ShouldBe(1);
            incoming.Fade.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }

        var (offWindow, off) = Show(level: ArcMotionLevel.Off);
        try
        {
            off.Next();
            Grid(off).Slots.Count.ShouldBe(1);
            Grid(off).Current!.X.Value.ShouldBe(0);
            Grid(off).Current!.Fade.Value.ShouldBe(1);
        }
        finally
        {
            offWindow.Close();
        }
    }

    [AvaloniaFact]
    public void View_Changes_Crossfade_In_Place()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full);
        try
        {
            var grid = Grid(c);
            var week = grid.Current!;
            c.View = WeekCalendarView.Day;
            var day = grid.Current!;
            day.ShouldNotBe(week);
            day.X.Value.ShouldBe(0);
            week.X.Value.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            day.Fade.Value.ShouldBeInRange(0.3, 0.999);
            week.Fade.Value.ShouldBeInRange(0, 0.6);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            Pump();
            grid.Slots.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    // Outside data and the visual tree --------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Calendar_Is_Collected_While_Its_Data_Lives_On()
    {
        var events = new System.Collections.ObjectModel.ObservableCollection<WeekCalendarEvent>(
            WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc));
        var calendars = new System.Collections.ObjectModel.ObservableCollection<WeekCalendarSource>(WeekCalendarDemoData.CreateCalendars());
        var list = new Avalonia.Collections.AvaloniaList<WeekCalendarEvent>(WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc));
        var withSources = TestHelpers.ShowAndClose(() => Create(setup: c =>
        {
            c.EventsSource = events;
            c.CalendarsSource = calendars;
        }));
        var withList = TestHelpers.ShowAndClose(() => Create(setup: c => c.Events = list));
        TestHelpers.Collect(withSources, withList);
        (withSources.IsAlive, withList.IsAlive).ShouldBe((false, false));
        events[0].Title = "Still an event";
        events.Add(new WeekCalendarEvent("late", "Later", Utc(Monday, 16), Utc(Monday, 17), WeekCalendarDemoData.Studio));
        calendars[1].IsVisible = false;
        calendars.RemoveAt(0);
        list.RemoveAt(0);
        list[0].Title = "Still an event";
    }

    [AvaloniaFact]
    public void A_Calendar_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var calendar = Create();
        var host = new Border { Child = calendar };
        var window = TestHelpers.Show(host);
        window.Width = 1440;
        window.Height = 900;
        Pump();
        try
        {
            var before = Pane(calendar).Presenters.Count;
            var crew = Pane(calendar).Presenters.Count(p => p.Event!.CalendarId == WeekCalendarDemoData.Crew);
            crew.ShouldBeGreaterThan(0);
            host.Child = null;
            Pump();
            calendar.Calendars[1].IsVisible = false;
            var added = new WeekCalendarEvent("late", "Added while away", Utc(Monday, 16), Utc(Monday, 17), WeekCalendarDemoData.Studio);
            calendar.Events.Add(added);
            host.Child = calendar;
            Pump();
            Pane(calendar).Presenters.Count.ShouldBe(before - crew + 1);
            Pane(calendar).Presenters.ShouldContain(p => p.Event == added);

            // Back in the window, it listens again.
            calendar.Calendars[1].IsVisible = true;
            Pump();
            calendar.Events.Remove(added);
            Pump();
            Pane(calendar).Presenters.Count.ShouldBe(before);
            Pane(calendar).Presenters.ShouldNotContain(p => p.Event == added);
        }
        finally
        {
            window.Close();
        }
    }
}
