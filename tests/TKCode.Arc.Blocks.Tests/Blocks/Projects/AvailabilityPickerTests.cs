using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Projects;

public class AvailabilityPickerTests
{
    // Data ----------------------------------------------------------------------------------------------------------------

    private sealed class Week
    {
        public required ProjectPerson Noor { get; init; }
        public required ProjectPerson Tomas { get; init; }
        public required ProjectPerson Ingrid { get; init; }
        public required AvailabilityDay[] Days { get; init; }
        public ProjectPerson[] People => [Noor, Tomas, Ingrid];
    }

    private static AvailabilitySlot At(int hour, int minute, params ProjectPerson[] busy) => new(new TimeSpan(hour, minute, 0), busy);

    /// <summary>The Gallery's week: Mon 9 – Fri 13 March 2026, four times a day.</summary>
    private static Week CreateWeek()
    {
        var noor = new ProjectPerson("Noor Haddad", "noor");
        var tomas = new ProjectPerson("Tomás Ferreira", "tomas");
        var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid");
        var monday = new DateOnly(2026, 3, 9);
        return new Week
        {
            Noor = noor,
            Tomas = tomas,
            Ingrid = ingrid,
            Days =
            [
                new(monday, At(9, 0), At(11, 0), At(13, 30, tomas), At(15, 30)),
                new(monday.AddDays(1), At(8, 30, ingrid), At(10, 0), At(14, 0), At(16, 0, noor)),
                new(monday.AddDays(2), At(9, 30), At(12, 0, tomas), At(13, 0), At(15, 0)),
                new(monday.AddDays(3), At(10, 30), At(11, 30, noor), At(14, 30), At(16, 30, ingrid)),
                new(monday.AddDays(4), At(8, 30), At(10, 0), At(13, 0, tomas), At(15, 0)),
            ]
        };
    }

    private static AvailabilityPicker CreatePicker(Week week, double width = 700)
    {
        var picker = new AvailabilityPicker
        {
            Title = "Roadmap check-in",
            TimeZoneName = "Lisbon time",
            Width = width,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        picker.Attendees.AddRange(week.People);
        picker.Days.AddRange(week.Days);
        return picker;
    }

    /// <summary>
    /// Shows the picker with English dates (the machine's culture may be any): the culture is put back when the window
    /// closes, and every test closes its window.
    /// </summary>
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var window = TestHelpers.Show(control, level);
        window.Closed += (_, _) => CultureInfo.CurrentCulture = culture;
        window.Width = 900;
        window.Height = 900;
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Advance(ManualFrameClock clock, double milliseconds)
    {
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Visual target)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static AvailabilitySlotButton SlotButton(AvailabilityPicker picker, AvailabilitySlot slot) =>
        picker.SlotButtons.Single(b => ReferenceEquals(b.Slot, slot));

    private static AvailabilityDayButton DayButton(AvailabilityPicker picker, AvailabilityDay day) =>
        picker.DayStrip!.Buttons.Single(b => ReferenceEquals(b.Day, day));

    // Selection ----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void By_Default_The_First_Day_And_Its_First_Free_Time_Are_Selected()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            picker.SelectedDay.ShouldBe(week.Days[0]);
            picker.SelectedSlot.ShouldBe(week.Days[0].Slots[0]);
            picker.DayStrip!.Buttons.Count.ShouldBe(5);
            picker.DayStrip.Buttons.Select(b => b.IsChecked).ShouldBe([true, false, false, false, false]);
            picker.DayStrip.Buttons.Select(b => b.DayName).ShouldBe(["Mon", "Tue", "Wed", "Thu", "Fri"]);
            picker.DayStrip.Buttons.Select(b => b.DayNumber).ShouldBe(["9", "10", "11", "12", "13"]);
            picker.DayStrip.Buttons.Select(b => b.OpenText).ShouldBe(["3 open", "2 open", "3 open", "2 open", "3 open"]);
            picker.SlotButtons.Select(b => b.TimeText).ShouldBe(["09:00", "11:00", "13:30", "15:30"]);
            picker.SlotButtons.Select(b => b.IsChecked).ShouldBe([true, false, false, false]);
            picker.DayTitle!.Text.ShouldBe("Monday, 9 March");
            picker.SelectionLabel!.Text.ShouldBe("Everyone's free");
            picker.SelectionValue!.Text.ShouldBe("Monday, 9 March, 09:00 – 09:30");
            TestHelpers.Descendant<TextBlock>(picker, AvailabilityPicker.SubtitlePart).Text.ShouldBe("30 min · Lisbon time");
            picker.ConfirmButton!.Content.ShouldBe("Book this time");
            picker.ConfirmButton.Variant.ShouldBe(ButtonVariant.Primary);
            picker.IsConfirmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Initial_Time_Wins_And_Brings_Its_Day()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        picker.SelectedSlot = week.Days[2].Slots[2];
        var window = Show(picker);
        try
        {
            picker.SelectedDay.ShouldBe(week.Days[2]);
            picker.SelectedSlot.ShouldBe(week.Days[2].Slots[2]);
            picker.DayStrip!.SelectedIndex.ShouldBe(2);
            SlotButton(picker, week.Days[2].Slots[2]).IsChecked.ShouldBe(true);

            // Later, a time of another day moves the day with it (no auto-selection).
            picker.SelectedSlot = week.Days[4].Slots[3];
            picker.SelectedDay.ShouldBe(week.Days[4]);
            picker.SelectedSlot.ShouldBe(week.Days[4].Slots[3]);
            SlotButton(picker, week.Days[4].Slots[3]).IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Day_Selects_Its_First_Free_Time_Every_Time()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            Click(window, DayButton(picker, week.Days[1]));
            picker.SelectedDay.ShouldBe(week.Days[1]);
            // 08:30 is busy for Ingrid: 10:00 is the first time that suits everyone.
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[1]);

            Click(window, SlotButton(picker, week.Days[1].Slots[2]));
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[2]);

            Click(window, DayButton(picker, week.Days[2]));
            Click(window, DayButton(picker, week.Days[1]));
            // Choices are not remembered per day.
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[1]);

            // Pressing the selected day or the selected time changes nothing.
            Click(window, DayButton(picker, week.Days[1]));
            Click(window, SlotButton(picker, week.Days[1].Slots[1]));
            picker.SelectedDay.ShouldBe(week.Days[1]);
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[1]);
            DayButton(picker, week.Days[1]).IsChecked.ShouldBe(true);
            SlotButton(picker, week.Days[1].Slots[1]).IsChecked.ShouldBe(true);

            // A busy time can be chosen.
            Click(window, SlotButton(picker, week.Days[1].Slots[3]));
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[3]);
            picker.SelectionLabel!.Text.ShouldBe("Noor will need to move a meeting");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_Selection_Falls_Back_To_The_First_Bookable_Time()
    {
        var week = CreateWeek();
        var allBusy = new AvailabilityDay(new DateOnly(2026, 3, 16), At(9, 0, week.Noor), At(10, 0, week.Tomas));
        var taken = At(8, 0);
        taken.IsEnabled = false;
        var withTaken = new AvailabilityDay(new DateOnly(2026, 3, 17), taken, At(9, 0, week.Ingrid), At(12, 0));
        var empty = new AvailabilityDay(new DateOnly(2026, 3, 18));
        var picker = CreatePicker(week);
        picker.Days.AddRange([allBusy, withTaken, empty]);
        var window = Show(picker);
        try
        {
            picker.SelectDay(allBusy).ShouldBeTrue();
            picker.SelectedSlot.ShouldBe(allBusy.Slots[0]);
            picker.SelectionLabel!.Text.ShouldBe("Noor will need to move a meeting");

            picker.SelectDay(withTaken);
            picker.SelectedSlot.ShouldBe(withTaken.Slots[2]);

            picker.SelectDay(empty);
            picker.SelectedSlot.ShouldBeNull();
            picker.SelectionLabel.Text.ShouldBe("Pick a time");
            picker.SelectionValue!.Text.ShouldBe(picker.DayLabel(empty));
            picker.ConfirmButton!.IsEnabled.ShouldBeFalse();
            ((AvailabilitySlotGrid)picker.SlotSwap!.Current!).Children.Single().ShouldBeOfType<AvailabilityEmptyNote>()
                .Text.ShouldBe("No times on this day");

            picker.AutoSelectSlot = AvailabilitySlotAutoSelect.First;
            picker.SelectDay(withTaken);
            picker.SelectedSlot.ShouldBe(withTaken.Slots[1]);

            picker.AutoSelectSlot = AvailabilitySlotAutoSelect.None;
            picker.SelectDay(week.Days[3]);
            picker.SelectedSlot.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Times_That_Cannot_Be_Booked_Are_Dimmed_And_Ignore_Presses()
    {
        var week = CreateWeek();
        var taken = week.Days[0].Slots[3];
        taken.IsEnabled = false;
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            week.Days[0].OpenCount.ShouldBe(2);
            var button = SlotButton(picker, taken);
            button.IsEnabled.ShouldBeFalse();
            button.Opacity.ShouldBe(0.5);
            picker.SelectSlot(taken).ShouldBeFalse();
            picker.SelectedSlot.ShouldBe(week.Days[0].Slots[0]);
        }
        finally
        {
            window.Close();
        }
    }

    // Events --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Events_Say_What_Changed_In_Which_Direction_And_Who_Did_It()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        var log = new List<string>();
        AvailabilityDayChangedEventArgs? lastDay = null;
        AvailabilitySlotChangedEventArgs? lastSlot = null;
        picker.SelectedDayChanged += (_, e) =>
        {
            lastDay = e;
            log.Add("day");
        };
        picker.SelectedSlotChanged += (_, e) =>
        {
            lastSlot = e;
            log.Add("slot");
        };
        try
        {
            Click(window, DayButton(picker, week.Days[3]));
            log.ShouldBe(["day", "slot"]);
            lastDay!.OldDay.ShouldBe(week.Days[0]);
            lastDay.NewDay.ShouldBe(week.Days[3]);
            lastDay.Direction.ShouldBe(1);
            lastDay.AutoSelectedSlot.ShouldBe(week.Days[3].Slots[0]);
            lastDay.IsUserInitiated.ShouldBeTrue();
            lastSlot!.OldSlot.ShouldBe(week.Days[0].Slots[0]);
            lastSlot.NewSlot.ShouldBe(week.Days[3].Slots[0]);
            lastSlot.Day.ShouldBe(week.Days[3]);
            lastSlot.IsUserInitiated.ShouldBeTrue();

            log.Clear();
            picker.SelectDay(week.Days[1]);
            log.ShouldBe(["day", "slot"]);
            lastDay.Direction.ShouldBe(-1);
            lastDay.IsUserInitiated.ShouldBeFalse();
            lastSlot.IsUserInitiated.ShouldBeFalse();

            log.Clear();
            Click(window, SlotButton(picker, week.Days[1].Slots[2]));
            log.ShouldBe(["slot"]);
            lastSlot.OldSlot.ShouldBe(week.Days[1].Slots[1]);
            lastSlot.IsUserInitiated.ShouldBeTrue();

            // A day that is not in Days is refused.
            picker.SelectDay(new AvailabilityDay(new DateOnly(2030, 1, 1))).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Texts ---------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Busy_Lines_Use_Short_Names_And_Every_Text_Is_A_Property()
    {
        var week = CreateWeek();
        var day = week.Days[0];
        day.Slots[1].BusyAttendees.AddRange([week.Noor, week.Ingrid]);
        day.Slots[3].BusyAttendees.AddRange(week.People);
        day.Slots[0].Note = "Right after stand-up";
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            picker.SlotButtons.Select(b => b.NoteText).ShouldBe(
                ["Right after stand-up", "Noor and Ingrid are busy", "Tomás is busy", "Noor, Tomás and Ingrid are busy"]);
            picker.SlotButtons.Select(b => b.IsBusy).ShouldBe([false, true, true, true]);
            SlotButton(picker, day.Slots[2]).BusyMarks.ShouldBe([false, true, false]);
            day.OpenCount.ShouldBe(1);

            picker.SelectSlot(day.Slots[1]);
            picker.SelectionLabel!.Text.ShouldBe("Noor and Ingrid will need to move a meeting");

            picker.BusyManyFormat = "{0} sont occupés";
            picker.AndWord = "et";
            picker.ConflictFormat = "{0} devront déplacer une réunion";
            picker.RangeFormat = "{0} à {1}";
            picker.DayLabelFormat = "{0:dd/MM}";
            SlotButton(picker, day.Slots[1]).NoteText.ShouldBe("Noor et Ingrid sont occupés");
            picker.SelectionLabel.Text.ShouldBe("Noor et Ingrid devront déplacer une réunion");
            picker.SelectionValue!.Text.ShouldBe("09/03, 11:00 à 11:30");

            // A malformed format does not throw: it is shown as written.
            picker.OpenCountFormat = "{0 open";
            picker.DayStrip!.Buttons[0].OpenText.ShouldBe("{0 open");

            picker.Subtitle = "Half an hour, video call";
            TestHelpers.Descendant<TextBlock>(picker, AvailabilityPicker.SubtitlePart).Text.ShouldBe("Half an hour, video call");
            picker.Subtitle = null;
            picker.TimeZoneName = null;
            picker.Duration = TimeSpan.FromMinutes(45);
            TestHelpers.Descendant<TextBlock>(picker, AvailabilityPicker.SubtitlePart).Text.ShouldBe("45 min");
            // Times without an end of their own follow the duration.
            picker.SelectionValue.Text.ShouldBe("09/03, 11:00 à 11:45");
        }
        finally
        {
            window.Close();
        }
    }

    // Booking -------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Booking_Confirms_Announces_And_Keeps_The_Focus()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        picker.SelectedSlot = week.Days[0].Slots[2];
        var window = Show(picker);
        var confirmed = new List<AvailabilitySelectionEventArgs>();
        picker.Confirmed += (_, e) => confirmed.Add(e);
        try
        {
            var button = picker.ConfirmButton!;
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();

            picker.IsConfirmed.ShouldBeTrue();
            confirmed.Count.ShouldBe(1);
            var args = confirmed[0];
            args.Day.ShouldBe(week.Days[0]);
            args.Slot.ShouldBe(week.Days[0].Slots[2]);
            args.Start.ShouldBe(new DateTime(2026, 3, 9, 13, 30, 0));
            args.End.ShouldBe(new DateTime(2026, 3, 9, 14, 0, 0));
            args.Conflicts.ShouldBe([week.Tomas]);
            args.DisplayDate.ShouldBe("Monday, 9 March");
            args.DisplayTime.ShouldBe("13:30 – 14:00");
            args.IsUserInitiated.ShouldBeTrue();

            picker.Announcer!.Announcement.ShouldBe("Booked Monday, 9 March, 13:30 to 14:00.");
            button.Variant.ShouldBe(ButtonVariant.Secondary);
            button.IsPressScaleEnabled.ShouldBeFalse();
            button.Content.ShouldBeOfType<AvailabilityBookedLabel>().Text.ShouldBe("Booked");
            AutomationProperties.GetName(button).ShouldBe("Booked");
            picker.SelectionLabel!.Text.ShouldBe("Invites on the way");
            picker.Classes.Contains(AvailabilityPicker.ConfirmedPseudoClass).ShouldBeTrue();

            // Still focusable and focused; pressing again does nothing.
            button.IsFocused.ShouldBeTrue();
            button.IsEnabled.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            confirmed.Count.ShouldBe(1);
            picker.Announcer.History.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Choice_Undoes_The_Booking()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        var resets = new List<AvailabilitySelectionEventArgs>();
        picker.ConfirmationReset += (_, e) => resets.Add(e);
        try
        {
            picker.Confirm().ShouldBeTrue();
            picker.Announcer!.Announcement.ShouldNotBeNull();

            // Pressing the booked time again is not a new choice.
            Click(window, SlotButton(picker, week.Days[0].Slots[0]));
            picker.IsConfirmed.ShouldBeTrue();

            Click(window, SlotButton(picker, week.Days[0].Slots[3]));
            picker.IsConfirmed.ShouldBeFalse();
            resets.Count.ShouldBe(1);
            resets[0].Slot.ShouldBe(week.Days[0].Slots[0]);
            resets[0].IsUserInitiated.ShouldBeTrue();
            picker.Announcer.Announcement.ShouldBeNull();
            AutomationProperties.GetName(picker.Announcer).ShouldBe(string.Empty);
            picker.ConfirmButton!.Variant.ShouldBe(ButtonVariant.Primary);
            picker.ConfirmButton.Content.ShouldBe("Book this time");
            picker.SelectionLabel!.Text.ShouldBe("Everyone's free");

            picker.Confirm().ShouldBeTrue();
            Click(window, DayButton(picker, week.Days[4]));
            picker.IsConfirmed.ShouldBeFalse();
            resets.Count.ShouldBe(2);
            resets[1].Slot.ShouldBe(week.Days[0].Slots[3]);

            picker.Confirm().ShouldBeTrue();
            picker.ResetConfirmation();
            picker.IsConfirmed.ShouldBeFalse();
            resets.Count.ShouldBe(3);
            resets[2].IsUserInitiated.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void A_Request_Can_Be_Cancelled_And_The_Command_Runs_After_Booking()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var command = new RecordingCommand();
        picker.ConfirmCommand = command;
        var window = Show(picker);
        try
        {
            var cancel = true;
            AvailabilityConfirmRequestedEventArgs? request = null;
            picker.ConfirmRequested += (_, e) =>
            {
                request = e;
                e.Cancel = cancel;
            };
            Click(window, picker.ConfirmButton!);
            request.ShouldNotBeNull();
            request!.IsUserInitiated.ShouldBeTrue();
            picker.IsConfirmed.ShouldBeFalse();
            command.Parameters.ShouldBeEmpty();
            picker.Announcer!.History.ShouldBeEmpty();

            cancel = false;
            Click(window, picker.ConfirmButton!);
            picker.IsConfirmed.ShouldBeTrue();
            command.Parameters.Single().ShouldBeOfType<AvailabilitySelectionEventArgs>().Slot.ShouldBe(week.Days[0].Slots[0]);
            picker.Confirm().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void State_Set_From_Code_Is_Shown_Without_An_Announcement()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        picker.IsConfirmed = true;
        var window = Show(picker);
        var confirmed = 0;
        picker.Confirmed += (_, _) => confirmed++;
        try
        {
            // Booked before it was shown: shown as booked, nothing said.
            picker.IsConfirmed.ShouldBeTrue();
            picker.ConfirmButton!.Variant.ShouldBe(ButtonVariant.Secondary);
            picker.Announcer!.History.ShouldBeEmpty();

            picker.IsConfirmed = false;
            picker.ConfirmButton.Variant.ShouldBe(ButtonVariant.Primary);
            picker.IsConfirmed = true;
            confirmed.ShouldBe(1);
            picker.Announcer.History.ShouldBeEmpty();

            // Nothing to book: the picker refuses.
            picker.AutoSelectSlot = AvailabilitySlotAutoSelect.None;
            picker.SelectDay(week.Days[2]);
            picker.IsConfirmed.ShouldBeFalse();
            picker.IsConfirmed = true;
            picker.IsConfirmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Data changes --------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Data_Changes_Update_The_Picker_In_Place()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            var list = picker.SlotSwap!.Current;
            week.Days[0].Slots[0].BusyAttendees.Add(week.Ingrid);
            Pump();
            picker.SlotSwap.Current.ShouldBeSameAs(list);
            SlotButton(picker, week.Days[0].Slots[0]).NoteText.ShouldBe("Ingrid is busy");
            picker.DayStrip!.Buttons[0].OpenText.ShouldBe("2 open");
            picker.SelectionLabel!.Text.ShouldBe("Ingrid will need to move a meeting");

            week.Noor.ShortName = "N.";
            Pump();
            SlotButton(picker, week.Days[0].Slots[2]).NoteText.ShouldBe("Tomás is busy");
            week.Tomas.Name = "Tomás F.";
            week.Tomas.ShortName = "Tom";
            Pump();
            SlotButton(picker, week.Days[0].Slots[2]).NoteText.ShouldBe("Tom is busy");

            // A new time on the shown day: the list is rebuilt in place, the selection stays.
            week.Days[0].Slots.Add(At(8, 0));
            Pump();
            picker.SlotButtons.Select(b => b.TimeText).ShouldBe(["08:00", "09:00", "11:00", "13:30", "15:30"]);
            picker.SelectedSlot.ShouldBe(week.Days[0].Slots[0]);
            picker.SlotSwap.Leaving.ShouldBeEmpty();

            // The selected time goes away: the day picks again.
            week.Days[0].Slots.RemoveAt(0);
            Pump();
            picker.SelectedSlot.ShouldBe(week.Days[0].Slots.Single(s => s.Start == new TimeSpan(8, 0, 0)));

            // The selected day goes away: the first day is selected.
            picker.Days.RemoveAt(0);
            Pump();
            picker.SelectedDay.ShouldBe(week.Days[1]);
            picker.DayStrip.Buttons.Count.ShouldBe(4);
            picker.DayStrip.SelectedIndex.ShouldBe(0);

            // A new attendee gets a mark on every time.
            picker.Attendees.Add(new ProjectPerson("Wen Li"));
            Pump();
            picker.SlotButtons[0].BusyMarks.Count.ShouldBe(4);
            picker.AvatarStack!.VisiblePeople.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Days_Source_Takes_Precedence_And_Is_Observed()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var source = new System.Collections.ObjectModel.ObservableCollection<AvailabilityDay>(week.Days.Skip(3));
        picker.DaysSource = source;
        var window = Show(picker);
        try
        {
            picker.EffectiveDays.ShouldBe(source);
            picker.SelectedDay.ShouldBe(week.Days[3]);
            source.Add(week.Days[0]);
            Pump();
            picker.DayStrip!.Buttons.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Picker_Is_Collected_While_Its_Data_Lives_On()
    {
        var week = CreateWeek();
        var days = new System.Collections.ObjectModel.ObservableCollection<AvailabilityDay>(week.Days);
        var people = new System.Collections.ObjectModel.ObservableCollection<ProjectPerson>(week.People);
        var withSources = TestHelpers.ShowAndClose(() => new AvailabilityPicker { DaysSource = days, AttendeesSource = people });
        // The picker's own lists hold the app's days and people: it follows those too.
        var withItems = TestHelpers.ShowAndClose(() => CreatePicker(week));
        TestHelpers.Collect(withSources, withItems);
        (withSources.IsAlive, withItems.IsAlive).ShouldBe((false, false));
        week.Days[0].Slots.Add(At(17, 0));
        week.Days[1].Slots[0].BusyAttendees.Clear();
        week.Noor.Name = "Noor H.";
        days.RemoveAt(0);
        people.RemoveAt(0);
    }

    [AvaloniaFact]
    public void A_Picker_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var host = new Border { Child = picker };
        var window = Show(host);
        try
        {
            picker.DayStrip!.Buttons.Count.ShouldBe(5);
            host.Child = null;
            Pump();
            picker.Days.RemoveAt(4);
            week.Noor.Name = "Noor H.";
            week.Days[0].Slots.Add(At(17, 0));
            host.Child = picker;
            Pump();
            picker.DayStrip!.Buttons.Count.ShouldBe(4);
            (AutomationProperties.GetName(picker.AvatarStack!) ?? string.Empty).ShouldContain("Noor H.");
            picker.SlotButtons.Count.ShouldBe(5);

            // Back in the window, it listens again.
            picker.Days.Add(week.Days[4]);
            week.Noor.Name = "Noor Haddad";
            week.Days[0].Slots.RemoveAt(4);
            Pump();
            picker.DayStrip!.Buttons.Count.ShouldBe(5);
            (AutomationProperties.GetName(picker.AvatarStack!) ?? string.Empty).ShouldContain("Noor Haddad");
            picker.SlotButtons.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    // Layout --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Pickers_Own_Width()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week, width: 700);
        var window = Show(picker);
        try
        {
            picker.Classes.Contains(AvailabilityPicker.CompactPseudoClass).ShouldBeFalse();
            var grid = (AvailabilitySlotGrid)picker.SlotSwap!.Current!;
            grid.Columns.ShouldBe(2);
            var buttons = picker.SlotButtons;
            buttons[1].Bounds.Y.ShouldBe(buttons[0].Bounds.Y);
            buttons[2].Bounds.Y.ShouldBe(48 + 8);
            buttons[0].Bounds.Height.ShouldBe(48);
            var day = picker.DayStrip!.Buttons[0];
            // Each text line rounds up to a whole pixel (Arc lays out 83.8 / 75.4 / 56.6 on sub-pixels).
            day.Bounds.Height.ShouldBe(10 + 17 + 2 + 25 + 2 + 17 + 10 + 2, 0.01);
            // 700 − 2 (frame) − 48 (sides) = 650 over five columns 6 apart.
            picker.DayStrip.Buttons[1].Bounds.X.ShouldBe((650 - 4 * 6) / 5.0 + 6, 1);
            var confirm = picker.ConfirmButton!;
            confirm.Bounds.Height.ShouldBe(44);

            picker.Width = 500;
            Pump();
            picker.Classes.Contains(AvailabilityPicker.CompactPseudoClass).ShouldBeTrue();
            picker.Classes.Contains(AvailabilityPicker.NarrowPseudoClass).ShouldBeFalse();
            grid.Columns.ShouldBe(1);
            buttons[1].Bounds.Y.ShouldBe(48 + 8);
            day.Classes.Contains(AvailabilityDayButton.CompactPseudoClass).ShouldBeTrue();
            day.Bounds.Height.ShouldBe(8 + 17 + 2 + 20 + 2 + 17 + 8 + 2, 0.01);
            // 500 − 2 − 32 = 466 over five columns 4 apart.
            picker.DayStrip.Buttons[1].Bounds.X.ShouldBe((466 - 4 * 4) / 5.0 + 4, 1);
            // The footer stacks: the button spans the width under the summary.
            confirm.Bounds.Width.ShouldBe(500 - 2 - 32, 1);
            TestHelpers.Descendant<StackPanel>(picker, "PART_Legend").IsEffectivelyVisible.ShouldBeTrue();

            picker.Width = 340;
            Pump();
            picker.Classes.Contains(AvailabilityPicker.NarrowPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<StackPanel>(picker, "PART_Legend").IsEffectivelyVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(day, "PART_Open").IsVisible.ShouldBeFalse();
            day.Bounds.Height.ShouldBe(8 + 17 + 2 + 20 + 8 + 2, 0.01);

            picker.Width = 360;
            Pump();
            picker.Classes.Contains(AvailabilityPicker.NarrowPseudoClass).ShouldBeTrue();
            picker.Width = 520;
            Pump();
            picker.Classes.Contains(AvailabilityPicker.CompactPseudoClass).ShouldBeFalse();
            grid.Columns.ShouldBe(2);

            picker.IsLegendVisible = false;
            TestHelpers.Descendant<StackPanel>(picker, "PART_Legend").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Framed_Draws_A_Surface_And_Flat_Does_Not()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            var frame = TestHelpers.Descendant<Border>(picker, "PART_Frame");
            var footer = TestHelpers.Descendant<Border>(picker, "PART_Footer");
            var head = TestHelpers.Descendant<Border>(picker, "PART_SlotsHead");
            picker.Classes.Contains(AvailabilityPicker.FramedPseudoClass).ShouldBeTrue();
            frame.BorderThickness.ShouldBe(new Thickness(1));
            frame.CornerRadius.ShouldBe(new CornerRadius(34));
            TestHelpers.ColorOf(frame.Background).A.ShouldBe((byte)255);
            TestHelpers.ColorOf(footer.Background).A.ShouldBe((byte)255);
            TestHelpers.ColorOf(footer.Background).ShouldNotBe(TestHelpers.ColorOf(frame.Background));
            TestHelpers.ColorOf(head.BorderBrush).A.ShouldBeGreaterThan((byte)0);

            picker.IsFramed = false;
            Pump();
            frame.BorderThickness.ShouldBe(new Thickness(0));
            TestHelpers.ColorOf(frame.Background).A.ShouldBe((byte)0);
            TestHelpers.ColorOf(footer.Background).A.ShouldBe((byte)0);
            TestHelpers.ColorOf(head.BorderBrush).A.ShouldBe((byte)0);
            // The geometry stays the same.
            TestHelpers.Descendant<Grid>(picker, "PART_Header").Margin.ShouldBe(new Thickness(24, 24, 24, 20));
        }
        finally
        {
            window.Close();
        }
    }

    // Accessibility -------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Every_Part_Has_A_Name_And_Every_Button_Is_A_Tab_Stop()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        picker.SelectedSlot = week.Days[0].Slots[1];
        var window = Show(picker);
        try
        {
            AutomationProperties.GetName(picker).ShouldBe("Roadmap check-in");
            AutomationProperties.GetName(picker.AvatarStack!).ShouldBe("Invited: Noor Haddad, Tomás Ferreira and Ingrid Solberg");
            AutomationProperties.GetName(picker.DayStrip!).ShouldBe("Days from 9 March");
            AutomationProperties.GetName(picker.DayStrip!.Buttons[0]).ShouldBe("Monday, 9 March, open for everyone: 3");
            AutomationProperties.GetName(picker.SlotSwap!.Current!).ShouldBe("Times for Monday, 9 March");
            AutomationProperties.GetName(picker.SlotButtons[1]).ShouldBe("11:00 to 11:30, Everyone's free");
            AutomationProperties.GetName(picker.SlotButtons[2]).ShouldBe("13:30 to 14:00, Tomás is busy");
            AutomationProperties.GetName(picker.ConfirmButton!).ShouldBe("Book this time");
            AutomationProperties.GetHeadingLevel(TestHelpers.Descendant<TextBlock>(picker, AvailabilityPicker.TitlePart)).ShouldBe(2);
            AutomationProperties.GetLiveSetting(picker.Announcer!).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetAccessibilityView(TestHelpers.Descendant<AvailabilityMarks>(picker)).ShouldBe(AccessibilityView.Raw);

            // Toggles report their state (aria-pressed).
            picker.DayStrip.Buttons[0].IsChecked.ShouldBe(true);
            picker.SlotButtons[1].IsChecked.ShouldBe(true);

            // Tab visits every day, every time and the button, in order.
            var stops = picker.DayStrip.Buttons.Cast<InputElement>().Concat(picker.SlotButtons).Append(picker.ConfirmButton!).ToList();
            stops[0].Focus(NavigationMethod.Tab);
            for (var i = 1; i < stops.Count; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Pump();
                stops[i].IsFocused.ShouldBeTrue($"stop {i}");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Space_Choose_And_The_Arrows_Do_Nothing()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            var tuesday = DayButton(picker, week.Days[1]);
            tuesday.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            picker.SelectedDay.ShouldBe(week.Days[1]);
            // Choosing a day keeps the focus where it is.
            tuesday.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            tuesday.IsFocused.ShouldBeTrue();
            picker.SelectedDay.ShouldBe(week.Days[1]);

            var slot = SlotButton(picker, week.Days[1].Slots[2]);
            slot.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[2]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            picker.SelectedSlot.ShouldBe(week.Days[1].Slots[2]);
        }
        finally
        {
            window.Close();
        }
    }

    // Motion --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Day_Highlight_Glides_On_A_Gentle_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var strip = picker.DayStrip!;
            var monday = strip.Buttons[0].Bounds;
            var thursday = strip.Buttons[3].Bounds;
            // The first placement does not glide or fade.
            strip.Highlight.CurrentRect.ShouldBe(monday);
            strip.Highlight.CurrentOpacity.ShouldBe(1);

            picker.SelectDay(week.Days[3]);
            Pump();
            strip.Highlight.CurrentRect.X.ShouldBe(monday.X, 0.5);
            Advance(clock, 100);
            strip.Highlight.CurrentRect.X.ShouldBeInRange(monday.X + 10, thursday.X - 10);
            Advance(clock, 1500);
            strip.Highlight.CurrentRect.X.ShouldBe(thursday.X, 0.5);
            strip.Highlight.CurrentRect.Width.ShouldBe(thursday.Width, 0.5);

            // Without animation it jumps.
            picker.SelectDay(week.Days[1], animate: false);
            Pump();
            strip.Highlight.CurrentRect.X.ShouldBe(strip.Buttons[1].Bounds.X, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Times_Slide_In_From_The_Side_Of_Travel_While_The_Old_Ones_Leave()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var swap = picker.SlotSwap!;
            var first = swap.Current;
            swap.Leaving.ShouldBeEmpty();

            picker.SelectDay(week.Days[2]);
            Pump();
            swap.Leaving.Single().ShouldBeSameAs(first);
            swap.CurrentMotion.X.ShouldBe(16, 0.5);
            swap.CurrentMotion.Opacity.ShouldBe(0, 0.05);
            first!.IsHitTestVisible.ShouldBeFalse();
            first.GetVisualDescendants().OfType<AvailabilitySlotButton>().All(b => !b.Focusable).ShouldBeTrue();

            Advance(clock, 80);
            swap.CurrentMotion.X.ShouldBeInRange(1, 15);
            swap.CurrentMotion.Opacity.ShouldBeGreaterThan(0.5);
            swap.LeavingMotion(0).X.ShouldBeInRange(-16, -1);
            swap.LeavingMotion(0).Opacity.ShouldBeLessThan(0.5);

            Advance(clock, 120);
            Pump();
            // The leaving list is gone after 160ms; the slide finishes later (Smooth spring).
            swap.Leaving.ShouldBeEmpty();
            swap.CurrentMotion.Opacity.ShouldBe(1, 0.001);
            swap.CurrentMotion.X.ShouldBeGreaterThan(0.1);
            Advance(clock, 1200);
            swap.CurrentMotion.X.ShouldBe(0, 0.3);

            // Back to an earlier day: from the left.
            picker.SelectDay(week.Days[0]);
            Pump();
            swap.CurrentMotion.X.ShouldBe(-16, 0.5);
            swap.LeavingMotion(0).X.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quick_Changes_Stack_At_Most_Three_Lists()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var swap = picker.SlotSwap!;
            picker.SelectDay(week.Days[1]);
            Advance(clock, 40);
            var entering = swap.CurrentMotion;
            picker.SelectDay(week.Days[2]);
            // The interrupted list leaves from where it was drawn.
            swap.Leaving.Count.ShouldBe(2);
            swap.LeavingMotion(1).X.ShouldBe(entering.X, 0.5);
            Advance(clock, 40);
            picker.SelectDay(week.Days[3]);
            Advance(clock, 16);
            picker.SelectDay(week.Days[4]);
            swap.Leaving.Count.ShouldBeLessThanOrEqualTo(AvailabilitySlotSwap.MaxLayers - 1);
            Advance(clock, 400);
            Pump();
            swap.Leaving.ShouldBeEmpty();
            picker.SlotButtons.First().Slot.ShouldBe(week.Days[4].Slots[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Highlight_And_Only_Fades_The_Times()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Reduced);
        try
        {
            picker.SelectDay(week.Days[3]);
            Pump();
            picker.DayStrip!.Highlight.CurrentRect.X.ShouldBe(picker.DayStrip.Buttons[3].Bounds.X, 0.5);
            var swap = picker.SlotSwap!;
            swap.Leaving.ShouldBeEmpty();
            swap.CurrentMotion.X.ShouldBe(0);
            swap.CurrentMotion.Opacity.ShouldBe(0, 0.05);
            Advance(clock, 60);
            swap.CurrentMotion.Opacity.ShouldBeInRange(0.2, 0.99);
            Advance(clock, 100);
            swap.CurrentMotion.Opacity.ShouldBe(1, 0.001);

            // The check appears at once.
            picker.SelectSlot(week.Days[3].Slots[2]);
            SlotButton(picker, week.Days[3].Slots[2]).CheckProgress.ShouldBe(1);
            // Texts crossfade without moving.
            picker.SelectionValue!.LeavingCount.ShouldBe(1);
            picker.SelectionValue.CurrentBlock.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(0);
            picker.SelectionValue.CurrentBlock.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Motion_Swaps_Everything_At_Once()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker);
        try
        {
            picker.SelectDay(week.Days[4]);
            Pump();
            picker.SlotSwap!.Leaving.ShouldBeEmpty();
            picker.SlotSwap.CurrentMotion.ShouldBe((0.0, 1.0));
            picker.DayStrip!.Highlight.CurrentRect.X.ShouldBe(picker.DayStrip.Buttons[4].Bounds.X, 0.5);
            picker.SelectionValue!.LeavingCount.ShouldBe(0);
            SlotButton(picker, week.Days[4].Slots[0]).CheckProgress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Check_Slides_In_And_Pushes_The_Marks_Aside()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var before = SlotButton(picker, week.Days[0].Slots[0]);
            var after = SlotButton(picker, week.Days[0].Slots[1]);
            var layout = TestHelpers.Descendant<AvailabilitySlotLayout>(before);
            layout.MarksOffset.ShouldBe(-(14 + 12), 0.01);

            picker.SelectSlot(week.Days[0].Slots[1]);
            after.CheckProgress.ShouldBe(0, 0.001);
            Advance(clock, 100);
            after.CheckProgress.ShouldBeInRange(0.2, 0.9);
            after.CheckOpacity.ShouldBeGreaterThan(after.CheckProgress);
            before.CheckProgress.ShouldBeInRange(0.1, 0.8);
            before.CheckOpacity.ShouldBeLessThan(0.5);
            Advance(clock, 1200);
            after.CheckProgress.ShouldBe(1, 0.01);
            TestHelpers.Descendant<AvailabilitySlotLayout>(after).MarksOffset.ShouldBe(-(14 + 12), 0.3);
            before.CheckProgress.ShouldBe(0, 0.01);
            layout.MarksOffset.ShouldBe(0, 0.3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Texts_Swap_Only_When_They_Change()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            // Another free time on the same day: only the value changes.
            picker.SelectSlot(week.Days[0].Slots[1]);
            picker.DayTitle!.LeavingCount.ShouldBe(0);
            picker.SelectionLabel!.LeavingCount.ShouldBe(0);
            picker.SelectionValue!.LeavingCount.ShouldBe(1);
            var block = picker.SelectionValue.CurrentBlock;
            block.Opacity.ShouldBe(0, 0.01);
            block.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(4, 0.01);
            block.Effect.ShouldNotBeNull();
            Advance(clock, 200);
            Pump();
            picker.SelectionValue.LeavingCount.ShouldBe(0);
            block.Opacity.ShouldBe(1, 0.001);
            block.Effect.ShouldBeNull();

            // A busy time changes the label too; a new day changes the heading.
            picker.SelectSlot(week.Days[0].Slots[2]);
            picker.SelectionLabel.LeavingCount.ShouldBe(1);
            picker.SelectDay(week.Days[1]);
            picker.DayTitle.LeavingCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Stage_Height_Springs_When_A_Day_Has_Fewer_Rows()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var week = CreateWeek();
        var shortDay = new AvailabilityDay(new DateOnly(2026, 3, 14), At(10, 0), At(11, 0));
        var picker = CreatePicker(week);
        picker.Days.Add(shortDay);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var stage = TestHelpers.Descendant<SpringSizeDecorator>(picker, "PART_StageSize");
            stage.Bounds.Height.ShouldBe(48 + 8 + 48, 0.5);
            picker.SelectDay(shortDay);
            Pump();
            stage.IsMoving.ShouldBeTrue();
            Advance(clock, 1500);
            Pump();
            stage.Bounds.Height.ShouldBe(48, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Highlight_Follows_The_Selected_Cards_Press()
    {
        var week = CreateWeek();
        var picker = CreatePicker(week);
        var window = Show(picker, ArcMotionLevel.Full);
        try
        {
            var card = picker.DayStrip!.Buttons[0];
            card.RenderTransform = new ScaleTransform(0.97, 0.97);
            Pump();
            picker.DayStrip.Highlight.RenderTransform.ShouldBeSameAs(card.RenderTransform);
            picker.DayStrip.Highlight.RenderTransformOrigin.Point.ShouldBe(card.Bounds.Center);
        }
        finally
        {
            window.Close();
        }
    }
}
