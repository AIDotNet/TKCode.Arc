using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Calendars;

/// <summary>
/// <see cref="MonthCalendar"/> on an invented month (November 2026, today the 12th): the grid and its lanes, opening, moving,
/// deleting and undoing events, replies, the new event form, calendar toggles, the filter, months and keys, the width
/// breakpoints of the screen shell and the screen reader names.
/// </summary>
public class MonthCalendarTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    internal static MonthCalendar Create()
    {
        var c = new MonthCalendar
        {
            Today = Today,
            Month = Today,
            SelectedDate = Today,
            CurrentUserId = "me",
            WorkspaceName = "Fjord",
        };
        c.Calendars.Add(new MonthCalendarSource("work", "Work", MonthCalendarTone.Teal));
        c.Calendars.Add(new MonthCalendarSource("home", "Home", MonthCalendarTone.Amber));
        c.People.Add(new MonthCalendarPerson("me", "Ada Lind", "ada@fjord.test") { IsTeammate = false });
        c.People.Add(new MonthCalendarPerson("ben", "Ben Kaur", "ben@fjord.test"));
        c.People.Add(new MonthCalendarPerson("cy", "Cy Moss", "cy@fjord.test"));
        MonthCalendarEvent T(string id, int day, int h, int eh, string cal = "work") =>
            new(id, id, cal, new DateOnly(2026, 11, day), new TimeOnly(h, 0), new TimeOnly(eh, 0));
        c.Events.Add(T("standup", 12, 9, 10));
        c.Events.Add(T("review", 12, 11, 12));
        c.Events.Add(T("lunch", 12, 13, 14, "home"));
        c.Events.Add(T("demo", 12, 15, 16));
        c.Events.Add(T("retro", 12, 16, 17));
        c.Events.Add(new MonthCalendarEvent("offsite", "offsite", "work", new DateOnly(2026, 11, 12)) { EndDate = new DateOnly(2026, 11, 13) });
        c.Events.Add(T("dentist", 6, 17, 18, "home"));
        var invite = T("panel", 18, 10, 11);
        invite.Rsvp = MonthCalendarRsvp.Pending;
        invite.OrganizerId = "ben";
        invite.Attendees.Add(new MonthCalendarAttendee("ben", MonthCalendarRsvp.Yes));
        invite.Attendees.Add(new MonthCalendarAttendee("me", MonthCalendarRsvp.Pending));
        c.Events.Add(invite);
        return c;
    }

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 1440, double height = 900, ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Calendar = Create();
            Calendar.EventCreated += (_, e) => Created.Add(e.Event);
            Calendar.EventDeleted += (_, e) => Deleted.Add(e.Event);
            Calendar.EventChanged += (_, e) => Changed.Add(e);
            Calendar.RsvpChanged += (_, e) => Replies.Add(e);
            Calendar.MonthChanged += (_, e) => Months.Add(e.NewValue);
            Window = TestHelpers.Show(Calendar, level);
            Window.Width = width;
            Window.Height = height;
            Pump();
        }

        public MonthCalendar Calendar { get; }
        public Window Window { get; }
        public List<MonthCalendarEvent> Created { get; } = new();
        public List<MonthCalendarEvent> Deleted { get; } = new();
        public List<MonthCalendarEventChangedEventArgs> Changed { get; } = new();
        public List<MonthCalendarRsvpChangedEventArgs> Replies { get; } = new();
        public List<DateOnly> Months { get; } = new();

        public MonthGridView Grid => Calendar.MonthView!.Grid;

        public MonthCalendarEvent Event(string id) => Calendar.Events.First(e => e.Id == id);

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key, RawInputModifiers mods = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, mods);
            Window.KeyReleaseQwerty(key, mods);
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void The_Grid_Shows_The_Month_In_Six_Weeks_With_Lanes_And_More()
    {
        using var rig = new Rig();
        rig.Grid.Weeks.ShouldBe(6);
        rig.Grid.DayButtons.Count.ShouldBe(42);
        rig.Grid.DayButtons[0].Date.ShouldBe(new DateOnly(2026, 10, 26));
        // Thursday the 12th: the bar and five timed events in four lanes → bar, two chips and "3 more".
        var more = rig.Grid.MoreButtons.ShouldHaveSingleItem();
        more.Date.ShouldBe(Today);
        more.Count.ShouldBe(3);
        rig.Grid.Chips.Count(c => c.Event.StartDate == Today || c.IsBar && c.Event.Id == "offsite").ShouldBeGreaterThanOrEqualTo(3);
    }

    [AvaloniaFact]
    public void Day_Buttons_And_Chips_Are_Named_For_Screen_Readers()
    {
        using var rig = new Rig();
        var today = rig.Grid.DayButtons.First(d => d.Date == Today);
        AutomationProperties.GetName(today).ShouldBe("Thursday, November 12, today, 6 events");
        var panel = rig.Grid.Chips.First(c => c.Event.Id == "panel");
        AutomationProperties.GetName(panel).ShouldBe("panel, Wed, Nov 18, 10:00 AM to 11:00 AM, awaiting your reply");
        AutomationProperties.GetName(rig.Grid.MoreButtons[0]).ShouldBe("3 more events on Thursday, November 12");
    }

    [AvaloniaFact]
    public void Opening_An_Event_Selects_Its_Day_And_Shows_The_Details()
    {
        using var rig = new Rig();
        var invoked = new List<MonthCalendarEvent>();
        rig.Calendar.EventInvoked += (_, e) => invoked.Add(e.Event);
        rig.Calendar.OpenEvent(rig.Event("panel"));
        rig.Pump();
        rig.Calendar.SelectedDate.ShouldBe(new DateOnly(2026, 11, 18));
        rig.Calendar.Pane.ShouldBe(MonthPane.Event);
        invoked.ShouldBe([rig.Event("panel")]);
    }

    [AvaloniaFact]
    public void Deleting_Offers_Undo_That_Adds_The_Same_Event_Back()
    {
        using var rig = new Rig();
        var dentist = rig.Event("dentist");
        rig.Calendar.DeleteEvent(dentist);
        rig.Pump();
        rig.Calendar.Events.ShouldNotContain(dentist);
        rig.Deleted.ShouldBe([dentist]);
        rig.Calendar.Shell!.Toast!.Text.ShouldBe("Deleted dentist");
        rig.Calendar.Undo();
        rig.Pump();
        rig.Calendar.Events.ShouldContain(dentist);
        rig.Created.ShouldBe([dentist]);
    }

    [AvaloniaFact]
    public void Moving_Keeps_The_Time_And_The_Length_And_Can_Be_Undone()
    {
        using var rig = new Rig();
        var offsite = rig.Event("offsite");
        rig.Calendar.MoveEvent(offsite, new DateOnly(2026, 11, 23));
        rig.Pump();
        offsite.StartDate.ShouldBe(new DateOnly(2026, 11, 23));
        offsite.LastDate.ShouldBe(new DateOnly(2026, 11, 24));
        var change = rig.Changed.ShouldHaveSingleItem();
        change.Previous.StartDate.ShouldBe(Today);
        rig.Calendar.Shell!.Toast!.Text.ShouldBe("Moved offsite to Mon, Nov 23");
        rig.Calendar.Undo();
        rig.Pump();
        offsite.StartDate.ShouldBe(Today);
        offsite.LastDate.ShouldBe(new DateOnly(2026, 11, 13));
    }

    [AvaloniaFact]
    public void Alt_Arrows_Move_The_Focused_Chip_By_A_Day_Or_A_Week()
    {
        using var rig = new Rig();
        var chip = rig.Grid.Chips.First(c => c.Event.Id == "dentist");
        chip.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        rig.Event("dentist").StartDate.ShouldBe(new DateOnly(2026, 11, 7));
        rig.Grid.FocusChip("dentist").ShouldBeTrue();
        rig.Pump();
        rig.Key(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        rig.Event("dentist").StartDate.ShouldBe(new DateOnly(2026, 11, 14));
    }

    [AvaloniaFact]
    public void Replying_Updates_The_Event_The_Guest_And_Raises_The_Events()
    {
        using var rig = new Rig();
        var panel = rig.Event("panel");
        rig.Calendar.PendingInvitations().ShouldHaveSingleItem();
        rig.Calendar.Reply(panel, MonthCalendarRsvp.Yes);
        rig.Pump();
        panel.Rsvp.ShouldBe(MonthCalendarRsvp.Yes);
        panel.Attendees.First(a => a.PersonId == "me").Status.ShouldBe(MonthCalendarRsvp.Yes);
        var reply = rig.Replies.ShouldHaveSingleItem();
        reply.OldValue.ShouldBe(MonthCalendarRsvp.Pending);
        reply.NewValue.ShouldBe(MonthCalendarRsvp.Yes);
        rig.Changed.ShouldHaveSingleItem();
        rig.Calendar.PendingInvitations().ShouldBeEmpty();
        rig.Calendar.Shell!.Toast!.Text.ShouldBe("Accepted panel");
    }

    [AvaloniaFact]
    public void The_Form_Needs_A_Title_And_Then_Adds_The_Event()
    {
        using var rig = new Rig();
        var refused = new List<string>();
        rig.Calendar.OpenNewEvent(new DateOnly(2026, 11, 20));
        rig.Pump();
        rig.Calendar.Pane.ShouldBe(MonthPane.Form);
        rig.Calendar.SaveDraft();
        rig.Calendar.Draft!.TitleError.ShouldBe("Add a title");
        rig.Created.ShouldBeEmpty();
        rig.Calendar.EventCreating += (_, e) =>
        {
            if (e.Event.Title == "Blocked")
            {
                e.ErrorMessage = "No room";
                refused.Add(e.Event.Title);
            }
        };
        rig.Calendar.Draft!.Title = "Blocked";
        rig.Calendar.SaveDraft();
        rig.Calendar.Draft!.Error.ShouldBe("No room");
        refused.ShouldBe(["Blocked"]);
        rig.Calendar.Draft!.Title = "Planning";
        rig.Calendar.Draft!.Guests.Add("ben");
        rig.Calendar.SaveDraft();
        rig.Pump();
        var created = rig.Created.ShouldHaveSingleItem();
        created.Title.ShouldBe("Planning");
        created.StartDate.ShouldBe(new DateOnly(2026, 11, 20));
        created.StartTime.ShouldBe(new TimeOnly(10, 0));
        created.Attendees.Select(a => a.PersonId).ShouldBe(["me", "ben"]);
        rig.Calendar.Pane.ShouldBe(MonthPane.Event);
    }

    [AvaloniaFact]
    public void A_Span_Opens_An_All_Day_Form_Across_The_Days()
    {
        using var rig = new Rig();
        rig.Calendar.OpenNewEvent(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 25));
        var draft = rig.Calendar.Draft!;
        draft.AllDay.ShouldBeTrue();
        draft.Title = "Sprint";
        rig.Calendar.SaveDraft();
        var created = rig.Created.ShouldHaveSingleItem();
        created.IsAllDay.ShouldBeTrue();
        created.LastDate.ShouldBe(new DateOnly(2026, 11, 25));
    }

    [AvaloniaFact]
    public void Free_Gaps_Can_Be_Blocked_As_Focus_Time()
    {
        using var rig = new Rig();
        rig.Calendar.BlockFocusTime(Today, new TimeOnly(10, 0), new TimeOnly(11, 0));
        rig.Pump();
        var focus = rig.Created.ShouldHaveSingleItem();
        focus.Title.ShouldBe("Focus time");
        focus.CalendarId.ShouldBe("work");
        rig.Calendar.Shell!.Toast!.Text.ShouldBe("Blocked 10:00 AM to 11:00 AM for focus");
        rig.Calendar.Undo();
        rig.Deleted.ShouldBe([focus]);
    }

    [AvaloniaFact]
    public void Hidden_Calendars_Leave_The_Grid_And_Only_Shows_One()
    {
        using var rig = new Rig();
        var home = rig.Calendar.Calendars[1];
        rig.Calendar.SetCalendarShown(home, false);
        rig.Pump();
        rig.Grid.Chips.ShouldNotContain(c => c.Event.CalendarId == "home");
        rig.Calendar.MonthTotals().Total.ShouldBe(6);
        rig.Calendar.ShowOnly(home);
        rig.Calendar.Calendars.Select(c => c.IsShown).ShouldBe([false, true]);
        rig.Calendar.ShowOnly(home);
        rig.Calendar.Calendars.Select(c => c.IsShown).ShouldBe([true, true]);
    }

    [AvaloniaFact]
    public void The_Filter_Dims_Events_That_Do_Not_Match()
    {
        using var rig = new Rig();
        rig.Calendar.FilterText = "dent";
        rig.Pump();
        rig.Grid.Chips.First(c => c.Event.Id == "dentist").DimValue.ShouldBe(1);
        rig.Grid.Chips.First(c => c.Event.Id == "standup").DimValue.ShouldBe(0.28);
        rig.Calendar.MonthTotals().ShouldBe((8, 1));
    }

    [AvaloniaFact]
    public void Months_Change_With_The_Keys_And_Raise_MonthChanged()
    {
        using var rig = new Rig();
        rig.Grid.FocusDay(Today);
        rig.Key(PhysicalKey.PageDown);
        rig.Calendar.Month.ShouldBe(new DateOnly(2026, 12, 1));
        rig.Months.ShouldBe([new DateOnly(2026, 12, 1)]);
        rig.Key(PhysicalKey.T);
        rig.Calendar.Month.ShouldBe(new DateOnly(2026, 11, 1));
        rig.Calendar.SelectedDate.ShouldBe(Today);
    }

    [AvaloniaFact]
    public void Arrows_Move_The_Focus_And_Enter_Opens_The_Day()
    {
        using var rig = new Rig();
        rig.Grid.FocusDay(Today);
        rig.Pump();
        rig.Key(PhysicalKey.ArrowRight);
        rig.Calendar.FocusDate.ShouldBe(new DateOnly(2026, 11, 13));
        rig.Calendar.SelectedDate.ShouldBe(Today);
        rig.Key(PhysicalKey.Enter);
        rig.Calendar.SelectedDate.ShouldBe(new DateOnly(2026, 11, 13));
        // Leaving the grid turns the month.
        for (var i = 0; i < 4; i++)
        {
            rig.Key(PhysicalKey.ArrowDown);
        }
        rig.Calendar.Month.ShouldBe(new DateOnly(2026, 12, 1));
    }

    [AvaloniaTheory]
    [InlineData(1440, true, true, false)]
    [InlineData(1000, false, true, false)]
    [InlineData(800, false, false, false)]
    [InlineData(390, false, false, true)]
    public void The_Shell_Docks_Or_Sheets_By_Width(double width, bool panelDocked, bool sidebarDocked, bool phone)
    {
        using var rig = new Rig(width, 844);
        var shell = rig.Calendar.Shell!;
        shell.IsPanelDocked.ShouldBe(panelDocked);
        shell.IsSidebarDocked.ShouldBe(sidebarDocked);
        shell.IsPhone.ShouldBe(phone);
        shell.Header.IsMenuVisible.ShouldBe(!sidebarDocked);
        if (phone)
        {
            rig.Calendar.IsCompact.ShouldBeTrue();
            rig.Grid.Compact.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void The_Month_Turns_Compact_When_Its_Card_Is_Narrower_Than_560()
    {
        using var rig = new Rig(1100, 860);
        rig.Calendar.Shell!.IsPanelDocked.ShouldBeTrue();
        rig.Calendar.IsCompact.ShouldBeTrue();
        rig.Grid.Compact.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Weekends_And_The_Week_Start_Follow_The_Settings()
    {
        using var rig = new Rig();
        rig.Calendar.FirstDayOfWeek = DayOfWeek.Sunday;
        rig.Pump();
        rig.Grid.Weeks.ShouldBe(5);
        rig.Grid.DayButtons[0].Date.ShouldBe(new DateOnly(2026, 11, 1));
        rig.Calendar.ShowWeekends = false;
        rig.Pump();
        rig.Grid.DayButtons.ShouldAllBe(d => d.Date.DayOfWeek != DayOfWeek.Saturday && d.Date.DayOfWeek != DayOfWeek.Sunday);
    }

    [AvaloniaFact]
    public void The_Panel_Width_Is_Clamped_And_The_Toggle_Hides_It()
    {
        using var rig = new Rig();
        rig.Calendar.DayPanelWidth = 900;
        rig.Calendar.DayPanelWidth.ShouldBe(460);
        rig.Calendar.Shell!.PanelWidth.ShouldBe(460);
        rig.Calendar.IsDayPanelOpen = false;
        rig.Pump();
        rig.Calendar.Shell.IsPanelOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Views_Switch_Between_Month_Upcoming_And_Invitations()
    {
        using var rig = new Rig();
        rig.Calendar.View = MonthCalendarView.Upcoming;
        rig.Pump();
        rig.Calendar.BodyContent.ShouldBeOfType<MonthUpcomingView>();
        rig.Calendar.View = MonthCalendarView.Invitations;
        rig.Pump();
        rig.Calendar.BodyContent.ShouldBeOfType<MonthInvitationsView>();
        rig.Calendar.View = MonthCalendarView.Teammate;
        rig.Calendar.TeammateId = "ben";
        rig.Pump();
        rig.Calendar.ViewEvents().Select(e => e.Id).ShouldBeEmpty();
    }
}
