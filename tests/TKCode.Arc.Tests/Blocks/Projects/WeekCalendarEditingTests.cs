using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>Editing the week calendar: drawing, moving, resizing, popovers, delete and undo, keys, read only, motion.</summary>
public class WeekCalendarEditingTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateOnly Wednesday = Monday.AddDays(2);
    private static readonly DateOnly Thursday = Monday.AddDays(3);
    private static readonly DateOnly Friday = Monday.AddDays(4);
    private static readonly DateOnly Saturday = Monday.AddDays(5);
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    private static DateTimeOffset Utc(DateOnly day, double hours) =>
        new(day.ToDateTime(TimeOnly.MinValue).AddHours(hours), TimeSpan.Zero);

    private static (Window Window, WeekCalendar Calendar) Show(double width = 1440, ArcMotionLevel level = ArcMotionLevel.Off,
                                                               Action<WeekCalendar>? setup = null)
    {
        var calendar = new WeekCalendar
        {
            TimeZone = TimeZoneInfo.Utc,
            Culture = CultureInfo.GetCultureInfo("en-US"),
            GridHeight = 600,
        };
        calendar.Now = Utc(Thursday, 14 + 20 / 60.0);
        calendar.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        calendar.Events.AddRange(WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc));
        setup?.Invoke(calendar);
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

    private static WeekCalendarEvent Event(WeekCalendar c, string title) => c.Events.First(e => e.Title == title);

    private static WeekCalendarEventPresenter Block(WeekCalendar c, string title) =>
        Pane(c).Presenters.First(p => p.Event?.Title == title);

    /// <summary>The window point of <paramref name="minute"/> on <paramref name="day"/> (x px into its column).</summary>
    private static Point At(Window window, WeekCalendar c, DateOnly day, double minute, double x = 40)
    {
        var cell = Pane(c).CellFor(day).ShouldNotBeNull();
        return cell.TranslatePoint(new Point(x, c.TimeGrid.YOf(minute)), window).ShouldNotBeNull();
    }

    private static Point On(Window window, WeekCalendarEventPresenter block, double y, double x = 30) =>
        block.TranslatePoint(new Point(x, y), window).ShouldNotBeNull();

    private static void Press(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump(1);
    }

    private static void MoveTo(Window window, Point point, int steps = 1, Point? from = null)
    {
        var start = from ?? point;
        for (var i = 1; i <= steps; i++)
        {
            var p = steps == 1 ? point : new Point(start.X + (point.X - start.X) * i / steps, start.Y + (point.Y - start.Y) * i / steps);
            window.MouseMove(p, RawInputModifiers.LeftMouseButton);
            Pump(1);
        }
    }

    private static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static void Drag(Window window, Point from, Point to, int steps = 6)
    {
        Press(window, from);
        MoveTo(window, to, steps, from);
        Release(window, to);
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    // Drawing ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Drawing_On_Empty_Time_Opens_Quick_Create_And_Enter_Saves_The_Event()
    {
        var (window, c) = Show();
        try
        {
            var created = new List<WeekCalendarEvent>();
            c.EventCreated += (_, e) => created.Add(e.Event);
            var from = At(window, c, Saturday, 13 * 60 + 7);
            Press(window, from);
            MoveTo(window, At(window, c, Saturday, 14 * 60 + 40), 5, from);
            // While held: the draft follows, on the pressed day only, and nothing is open yet.
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.Draw);
            c.Classes.Contains(WeekCalendar.DraggingPseudoClass).ShouldBeTrue();
            var draft = Pane(c).Draft.ShouldNotBeNull();
            (draft.Day, draft.Start, draft.End).ShouldBe((Saturday, 13 * 60, 14 * 60 + 45));
            MoveTo(window, At(window, c, Sunday, 14 * 60 + 40));
            Pane(c).Draft!.Day.ShouldBe(Saturday);
            c.Editor.ShouldBeNull();
            Release(window, At(window, c, Sunday, 14 * 60 + 40));

            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            (quick.Day, quick.Start, quick.End).ShouldBe((Saturday, 13 * 60, 14 * 60 + 45));
            quick.WhenText.ShouldBe("Sat, Oct 24 1 – 2:45 PM, 1 hr 45 min");
            quick.Placement.ShouldNotBeNull().Side.ShouldBe(WeekCalendarPopoverSide.Left);
            Focused(window).ShouldBe(quick.TitleBox);
            c.Classes.Contains(WeekCalendar.EditingPseudoClass).ShouldBeTrue();
            c.Classes.Contains(WeekCalendar.DraggingPseudoClass).ShouldBeFalse();

            quick.TitleBox!.Text = "Kiln check";
            Key(window, PhysicalKey.Enter);
            var saved = created.ShouldHaveSingleItem();
            saved.Title.ShouldBe("Kiln check");
            saved.Start.ShouldBe(Utc(Saturday, 13));
            saved.End.ShouldBe(Utc(Saturday, 14.75));
            saved.CalendarId.ShouldBe(WeekCalendarDemoData.Studio);
            c.Events.ShouldContain(saved);
            c.SelectedEvent.ShouldBe(saved);
            c.Editor.ShouldBeNull();
            Pane(c).Draft.ShouldBeNull();
            ((WeekCalendarEventPresenter)Focused(window)!).Event.ShouldBe(saved);
            c.Announcer!.History[^1].ShouldBe("Kiln check added on Saturday, October 24, 1:00 PM to 2:45 PM");
            c.CanUndo.ShouldBeTrue();
            c.Toast.ShouldNotBeNull().Message.ShouldBe(c.Announcer.History[^1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drawing_Upwards_Ends_At_The_Pressed_Slot_And_A_Short_Press_Drafts_An_Hour()
    {
        var (window, c) = Show();
        try
        {
            var from = At(window, c, Saturday, 14 * 60 + 20);
            Press(window, from);
            MoveTo(window, At(window, c, Saturday, 13 * 60 + 10), 4, from);
            var draft = Pane(c).Draft.ShouldNotBeNull();
            (draft.Start, draft.End).ShouldBe((13 * 60 + 15, 14 * 60 + 30));
            Key(window, PhysicalKey.Escape);
            Pane(c).Draft.ShouldBeNull();
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.Canceled);
            c.Announcer!.History[^1].ShouldBe("Change canceled");
            Release(window, At(window, c, Saturday, 13 * 60 + 10));
            c.Editor.ShouldBeNull();

            // 3.5px is still a click: an hour from the 15-minute slot pressed.
            var click = At(window, c, Saturday, 13 * 60 + 33);
            Press(window, click);
            MoveTo(window, click + new Vector(0, 3.5));
            Release(window, click + new Vector(0, 3.5));
            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            (quick.Start, quick.End).ShouldBe((13 * 60 + 30, 14 * 60 + 30));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_And_Cancel_Drop_The_Draft_And_Give_The_Day_Back_The_Focus()
    {
        var (window, c) = Show();
        try
        {
            var click = At(window, c, Saturday, 13 * 60 + 33);
            Press(window, click);
            Release(window, click);
            c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>().TitleBox!.Text = "Typed";
            Key(window, PhysicalKey.Escape);
            c.Editor.ShouldBeNull();
            Pane(c).Draft.ShouldBeNull();
            c.Events.ShouldNotContain(e => e.Title == "Typed");
            Focused(window).ShouldBe(Pane(c).CellFor(Saturday));

            c.BeginCreate(Sunday).ShouldBeTrue();
            var cancel = TestHelpers.Descendant<Button>(c.Editor!, WeekCalendarQuickCreate.CancelPart);
            cancel.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            c.Editor.ShouldBeNull();
            Focused(window).ShouldBe(Pane(c).CellFor(Sunday));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_Outside_A_Popover_Only_Closes_It()
    {
        var (window, c) = Show();
        try
        {
            var click = At(window, c, Saturday, 13 * 60 + 33);
            Press(window, click);
            Release(window, click);
            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            // Pressing empty time elsewhere closes the popover and does nothing else: no new draft, no new popover.
            var elsewhere = At(window, c, Monday, 16 * 60 + 10);
            Press(window, elsewhere);
            Release(window, elsewhere);
            quick.IsClosing.ShouldBeTrue();
            c.Editor.ShouldBeNull();
            Pane(c).Draft.ShouldBeNull();
            Focused(window).ShouldBe(Pane(c).CellFor(Monday));

            // Outside the details on another event: only the focus moves there.
            c.OpenDetails(Event(c, "Sprint kickoff")).ShouldBeTrue();
            var lunch = Block(c, "Lunch with Noor");
            var point = On(window, lunch, 10);
            Press(window, point);
            Release(window, point);
            c.Editor.ShouldBeNull();
            Focused(window).ShouldBe(lunch);
        }
        finally
        {
            window.Close();
        }
    }

    // Moving and resizing ------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Dragging_An_Event_Moves_It_Between_Days_And_Times_Keeping_The_Grab_Offset()
    {
        var (window, c) = Show();
        try
        {
            var updates = new List<WeekCalendarEventUpdatedEventArgs>();
            c.EventUpdated += (_, e) => updates.Add(e);
            var stroll = Event(c, "Flea market stroll");
            var block = Block(c, "Flea market stroll");
            var from = On(window, block, 20);
            var columnWidth = Pane(c).CellFor(Saturday)!.Bounds.Width;
            var to = from + new Vector(-columnWidth, 60);
            Press(window, from);
            MoveTo(window, to, 6, from);

            // Lifted in place: full width, selected, its origin dashed and its time in a pill; nothing changed yet.
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.Move);
            block.IsDragging.ShouldBeTrue();
            c.SelectedEvent.ShouldBe(stroll);
            Pane(c).LiftedAt.ShouldBe((Friday, 645.0, 735.0));
            Pane(c).DragOrigin.ShouldNotBeNull();
            Pane(c).DragLabel.ShouldNotBeNull().Text.ShouldBe("10:45 AM – 12:15 PM");
            block.Preview.ShouldBe((645.0, 735.0));
            stroll.Start.ShouldBe(Utc(Saturday, 9.5));
            updates.ShouldBeEmpty();

            Release(window, to);
            stroll.Start.ShouldBe(Utc(Friday, 10.75));
            stroll.End.ShouldBe(Utc(Friday, 12.25));
            var update = updates.ShouldHaveSingleItem();
            update.Event.ShouldBe(stroll);
            update.Previous.Start.ShouldBe(Utc(Saturday, 9.5));
            update.Previous.End.ShouldBe(Utc(Saturday, 11));
            c.Announcer!.History[^1].ShouldBe("Flea market stroll moved to Fri, Oct 23, 10:45 AM");
            c.Toast.ShouldNotBeNull().Message.ShouldBe("Flea market stroll moved to Fri, Oct 23, 10:45 AM");
            c.SelectedEvent.ShouldBe(stroll);
            // The block that moved keeps its control (it flies from where it was) and stays focused.
            Block(c, "Flea market stroll").ShouldBe(block);
            Focused(window).ShouldBe(block);
            Pane(c).DragOrigin.ShouldBeNull();
            block.IsDragging.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drag_That_Stays_In_Its_Slot_Changes_Nothing_And_A_Click_Opens_Details()
    {
        var (window, c) = Show();
        try
        {
            var updates = 0;
            c.EventUpdated += (_, _) => updates++;
            var block = Block(c, "Flea market stroll");
            var from = On(window, block, 20);
            Press(window, from);
            MoveTo(window, from + new Vector(0, 5), 1);
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.Move);
            Release(window, from + new Vector(0, 5));
            updates.ShouldBe(0);
            c.Toast.ShouldBeNull();
            c.Editor.ShouldBeNull();

            Press(window, from);
            Release(window, from);
            var details = c.Editor.ShouldBeOfType<WeekCalendarDetails>();
            details.Event.ShouldBe(Event(c, "Flea market stroll"));
            c.SelectedEvent.ShouldBe(details.Event);
            Focused(window).ShouldBe(details.TitleBox);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bottom_Edge_Resizes_To_15_Minute_Steps_And_Never_Below_15_Minutes()
    {
        var (window, c) = Show();
        try
        {
            var roadmap = Event(c, "Roadmap pass");
            var block = Block(c, "Roadmap pass");
            var edge = On(window, block, block.Bounds.Height - 3);
            Press(window, edge);
            MoveTo(window, edge + new Vector(0, 70), 5, edge);
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.Resize);
            Pane(c).LiftedAt.ShouldBe((Wednesday, 630.0, 765.0));
            Pane(c).DragLabel!.Text.ShouldBe("10:30 AM – 12:45 PM");
            Release(window, edge + new Vector(0, 70));
            roadmap.Start.ShouldBe(Utc(Wednesday, 10.5));
            roadmap.End.ShouldBe(Utc(Wednesday, 12.75));
            c.Announcer!.History[^1].ShouldBe("Roadmap pass now runs 10:30 AM – 12:45 PM");

            block = Block(c, "Roadmap pass");
            edge = On(window, block, block.Bounds.Height - 2);
            Press(window, edge);
            MoveTo(window, edge + new Vector(0, -300), 5, edge);
            Release(window, edge + new Vector(0, -300));
            roadmap.End.ShouldBe(Utc(Wednesday, 10.75));
            // 8px above the bottom edge is the body: a move, not a resize.
            block = Block(c, "Roadmap pass");
            block.IsOnResizeHandle(block.Bounds.Height - 8).ShouldBeFalse();
            block.IsOnResizeHandle(block.Bounds.Height - 6).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void All_Day_Events_Do_Not_Drag_But_Open_Their_Details()
    {
        var (window, c) = Show();
        try
        {
            var bar = Grid(c).Current!.AllDay.Bars.First(b => b.Event?.Title == "Dad's birthday");
            var birthday = bar.Event!;
            var start = birthday.Start;
            var from = bar.TranslatePoint(new Point(20, 10), window)!.Value;
            Drag(window, from, from + new Vector(150, 80));
            birthday.Start.ShouldBe(start);
            Grid(c).Gesture.ShouldBe(WeekCalendarGesture.None);

            Press(window, from);
            Release(window, from);
            var details = c.Editor.ShouldBeOfType<WeekCalendarDetails>();
            details.Event.ShouldBe(birthday);
            details.SummaryText.ShouldBe("Thursday, October 22 All day");
            details.StartField!.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Near_The_Bottom_The_Grid_Auto_Scrolls_While_The_Pointer_Stands_Still()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full);
        try
        {
            var grid = Grid(c);
            var block = Block(c, "Flea market stroll");
            var from = On(window, block, 20);
            Press(window, from);
            var scroller = grid.Scroller;
            var bottom = grid.TranslatePoint(new Point(from.X, scroller.Bounds.Bottom - 10), window)!.Value;
            MoveTo(window, bottom, 6, from);
            grid.AutoScrollStepAt(grid.TranslatePoint(new Point(0, scroller.Bounds.Bottom - 10), grid)!.Value).ShouldBe(16, 0.01);
            grid.AutoScrollStepAt(new Point(0, scroller.Bounds.Top + 30)).ShouldBe(-8, 0.01);
            grid.AutoScrollStepAt(new Point(0, scroller.Bounds.Top + 200)).ShouldBe(0);
            grid.IsAutoScrolling.ShouldBeTrue();
            var before = scroller.Offset.Y;
            var target = Pane(c).LiftedAt!.Value.Start;
            clock.Advance(TimeSpan.FromMilliseconds(250));
            // 16px per 60 Hz frame: about 240px in a quarter second, and the dragged event follows the pointer's new time.
            (scroller.Offset.Y - before).ShouldBeInRange(200, 260);
            grid.AutoScrolled.ShouldBe(scroller.Offset.Y - before, 0.01);
            Pane(c).LiftedAt!.Value.Start.ShouldBeGreaterThan(target);
            MoveTo(window, from);
            grid.IsAutoScrolling.ShouldBeFalse();
            Release(window, from);
        }
        finally
        {
            window.Close();
        }
    }

    // Motion ------------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Dragged_Event_Hops_Between_Slots_On_The_Hop_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(level: ArcMotionLevel.Full);
        try
        {
            var block = Block(c, "Flea market stroll");
            var from = On(window, block, 20);
            Press(window, from);
            MoveTo(window, from + new Vector(0, 4), 1);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            Pump();
            var motion = Pane(c).Flip.Get(block);
            motion.Offset.Value.Y.ShouldBe(0, 0.01);
            // One slot down (13px): the block's place jumps a slot and its offset springs from −13 to 0 (≈ 330ms).
            MoveTo(window, from + new Vector(0, 17), 1);
            Pump();
            Pane(c).LiftedAt!.Value.Start.ShouldBe(585);
            motion.Offset.Value.Y.ShouldBe(-13, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(49));
            (motion.Offset.Value.Y / -13).ShouldBeInRange(0.35, 0.65);
            clock.Advance(TimeSpan.FromMilliseconds(350));
            motion.Offset.Value.Y.ShouldBe(0, 0.2);
            Release(window, from + new Vector(0, 17));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void After_A_Drop_Moved_Blocks_Settle_On_The_Smooth_Spring_And_Reduced_Motion_Jumps()
    {
        foreach (var level in new[] { ArcMotionLevel.Full, ArcMotionLevel.Reduced })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var (window, c) = Show(level: level);
            try
            {
                // Metrics check (Fri 1 PM) dropped on Thursday at noon shares Team lunch's time: it takes the second column.
                var block = Block(c, "Metrics check");
                var from = On(window, block, 8);
                var to = At(window, c, Thursday, 12 * 60, block.Bounds.Width / 3) + new Vector(0, 8);
                Press(window, from);
                MoveTo(window, to, 6, from);
                clock.Advance(TimeSpan.FromMilliseconds(600));
                Release(window, to);
                Event(c, "Metrics check").Start.ShouldBe(Utc(Thursday, 12));
                var moved = Block(c, "Metrics check");
                moved.Slot.Column.ShouldBe(1);
                var motion = Pane(c).Flip.Get(moved);
                if (level == ArcMotionLevel.Full)
                {
                    var start = motion.Offset.Value.X;
                    start.ShouldBeLessThan(-20);
                    clock.Advance(TimeSpan.FromMilliseconds(100));
                    (motion.Offset.Value.X / start).ShouldBeInRange(0.5, 0.75);
                    clock.Advance(TimeSpan.FromMilliseconds(1200));
                }
                motion.Offset.Value.X.ShouldBe(0, 0.3);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Popovers_Scale_In_At_Full_Motion_Fade_At_Reduced_And_Jump_With_Off()
    {
        foreach (var level in new[] { ArcMotionLevel.Full, ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var (window, c) = Show(level: level);
            try
            {
                c.BeginCreate(Saturday, new TimeOnly(13, 0)).ShouldBeTrue();
                var editor = c.Editor.ShouldNotBeNull();
                editor.RenderTransformOrigin.ShouldBe(new RelativePoint(1, 0, RelativeUnit.Relative));
                if (level == ArcMotionLevel.Off)
                {
                    editor.Opacity.ShouldBe(1);
                    editor.CurrentScale.ShouldBe(1);
                    continue;
                }
                editor.Opacity.ShouldBeLessThan(0.05);
                editor.CurrentScale.ShouldBe(level == ArcMotionLevel.Full ? 0.96 : 1, 0.001);
                clock.Advance(TimeSpan.FromMilliseconds(80));
                editor.Opacity.ShouldBeInRange(0.3, 0.95);
                clock.Advance(TimeSpan.FromMilliseconds(600));
                editor.Opacity.ShouldBe(1, 0.01);
                editor.CurrentScale.ShouldBe(1, 0.002);

                // The undo toast rises 12px (Full) or only fades (Reduced).
                c.DeleteEvent(Event(c, "Flea market stroll")).ShouldBeTrue();
                var toast = c.Toast.ShouldNotBeNull();
                toast.Rise.ShouldBe(level == ArcMotionLevel.Full ? 12 : 0, 0.01);
                toast.Opacity.ShouldBeLessThan(0.05);
                clock.Advance(TimeSpan.FromMilliseconds(600));
                toast.Rise.ShouldBe(0, 0.1);
                toast.Opacity.ShouldBe(1, 0.02);
            }
            finally
            {
                window.Close();
            }
        }
    }

    // Keyboard ---------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void N_And_Enter_On_A_Day_Draft_At_Its_Next_Free_Hour()
    {
        var (window, c) = Show();
        try
        {
            c.FocusDay(Wednesday);
            Pump();
            Key(window, PhysicalKey.N);
            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            (quick.Day, quick.Start, quick.End).ShouldBe((Wednesday, 18 * 60 + 30, 19 * 60 + 30));
            quick.OpenedByKeyboard.ShouldBeTrue();
            Focused(window).ShouldBe(quick.TitleBox);
            // The draft was scrolled into view.
            var offset = Grid(c).Scroller.Offset.Y;
            c.TimeGrid.YOf(18 * 60 + 30).ShouldBeInRange(offset, offset + Grid(c).Scroller.Viewport.Height);
            Key(window, PhysicalKey.Escape);
            Focused(window).ShouldBe(Pane(c).CellFor(Wednesday));

            c.FocusDay(Sunday);
            Pump();
            Key(window, PhysicalKey.Enter);
            quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            (quick.Day, quick.Start).ShouldBe((Sunday, 9 * 60));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Opens_Details_And_Escape_Closes_Them_Then_Clears_The_Selection()
    {
        var (window, c) = Show();
        try
        {
            var block = Block(c, "Sprint kickoff");
            block.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            var details = c.Editor.ShouldBeOfType<WeekCalendarDetails>();
            details.OpenedByKeyboard.ShouldBeTrue();
            Focused(window).ShouldBe(details.TitleBox);
            c.SelectedEvent.ShouldBe(Event(c, "Sprint kickoff"));
            Key(window, PhysicalKey.Escape);
            c.Editor.ShouldBeNull();
            Focused(window).ShouldBe(block);
            block.IsSelected.ShouldBeTrue();
            Key(window, PhysicalKey.Escape);
            c.SelectedEvent.ShouldBeNull();
            block.IsSelected.ShouldBeFalse();
            c.Announcer!.History[^1].ShouldBe("No event selected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_And_Backspace_Remove_The_Focused_Or_Selected_Event_And_Undo_Restores_It()
    {
        var (window, c) = Show();
        try
        {
            var deleted = new List<WeekCalendarEvent>();
            var created = new List<WeekCalendarEvent>();
            c.EventDeleted += (_, e) => deleted.Add(e.Event);
            c.EventCreated += (_, e) => created.Add(e.Event);
            var roadmap = Event(c, "Roadmap pass");
            var index = c.Events.IndexOf(roadmap);
            Block(c, "Roadmap pass").Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Delete);
            c.Events.ShouldNotContain(roadmap);
            deleted.ShouldBe([roadmap]);
            c.Announcer!.History[^1].ShouldBe("Roadmap pass deleted");
            c.Toast.ShouldNotBeNull().Message.ShouldBe("Roadmap pass deleted");
            Focused(window).ShouldBe(Pane(c).CellFor(Wednesday));
            Key(window, PhysicalKey.Z, RawInputModifiers.Control);
            c.Events.IndexOf(roadmap).ShouldBe(index);
            created.ShouldBe([roadmap]);
            c.Announcer.History[^1].ShouldBe("Roadmap pass restored");
            c.CanUndo.ShouldBeFalse();

            // Backspace deletes the selected event while a day has the focus.
            c.SelectedEvent = Event(c, "Sprint kickoff");
            c.FocusDay(Wednesday);
            Pump();
            Key(window, PhysicalKey.Backspace);
            c.Events.ShouldNotContain(e => e.Title == "Sprint kickoff");
            c.SelectedEvent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_Arrows_Move_The_Event_And_Alt_Shift_Changes_Its_Length_Keeping_The_Focus()
    {
        var (window, c) = Show();
        try
        {
            var roadmap = Event(c, "Roadmap pass");
            var block = Block(c, "Roadmap pass");
            block.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            roadmap.Start.ShouldBe(Utc(Wednesday, 10.75));
            roadmap.End.ShouldBe(Utc(Wednesday, 11.75));
            c.SelectedEvent.ShouldBe(roadmap);
            c.Announcer!.History[^1].ShouldBe("Roadmap pass, Wed, Oct 21, 10:45 AM to 11:45 AM");
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            roadmap.Start.ShouldBe(Utc(Thursday, 10.75));
            ((WeekCalendarEventPresenter)Focused(window)!).Event.ShouldBe(roadmap);
            Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt | RawInputModifiers.Shift);
            roadmap.End.ShouldBe(Utc(Thursday, 12));
            for (var i = 0; i < 6; i++)
            {
                Key(window, PhysicalKey.ArrowUp, RawInputModifiers.Alt | RawInputModifiers.Shift);
            }
            roadmap.End.ShouldBe(Utc(Thursday, 11));
            ((WeekCalendarEventPresenter)Focused(window)!).Event.ShouldBe(roadmap);
            // The run of nudges undoes in one step, back to before the first.
            c.Toast.ShouldNotBeNull().Message.ShouldBe(c.Announcer.History[^1]);
            Key(window, PhysicalKey.Z, RawInputModifiers.Control);
            roadmap.Start.ShouldBe(Utc(Wednesday, 10.5));
            roadmap.End.ShouldBe(Utc(Wednesday, 11.5));
            c.Announcer.History[^1].ShouldStartWith("Reverted: Roadmap pass, ");

            // Up and Down without Alt still walk between events.
            var focused = (WeekCalendarEventPresenter)Focused(window)!;
            Key(window, PhysicalKey.ArrowDown);
            Focused(window).ShouldNotBe(focused);
        }
        finally
        {
            window.Close();
        }
    }

    // Undo and events --------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Undo_Is_One_Level_And_Reverts_Creates_Moves_Calendars_And_Deletes()
    {
        var (window, c) = Show();
        try
        {
            var log = new List<string>();
            c.EventCreated += (_, e) => log.Add("created " + e.Event.Title);
            c.EventDeleted += (_, e) => log.Add("deleted " + e.Event.Title);
            WeekCalendarEventUpdatedEventArgs? lastUpdate = null;
            c.EventUpdated += (_, e) =>
            {
                lastUpdate = e;
                log.Add("updated " + e.Event.Title);
            };

            // Create, then undo: the event goes and EventDeleted says so.
            c.BeginCreate(Sunday, new TimeOnly(9, 0));
            ((WeekCalendarQuickCreate)c.Editor!).TitleBox!.Text = "Glaze firing";
            c.SaveQuickCreate((WeekCalendarQuickCreate)c.Editor!);
            c.Undo().ShouldBeTrue();
            c.Events.ShouldNotContain(e => e.Title == "Glaze firing");
            log.ShouldBe(["created Glaze firing", "deleted Glaze firing"]);
            c.Undo().ShouldBeFalse();

            // One level: of two moves only the second is undone, with the values swapped in EventUpdated.
            var a = Event(c, "Metrics check");
            var b = Event(c, "Pack for Lisbon");
            c.MoveEventTo(a, Friday, 15 * 60, WeekCalendarEditSource.Pointer);
            c.MoveEventTo(b, Wednesday, 20 * 60, WeekCalendarEditSource.Pointer);
            c.Undo().ShouldBeTrue();
            b.Start.ShouldBe(Utc(Wednesday, 8));
            lastUpdate!.Event.ShouldBe(b);
            lastUpdate.Previous.Start.ShouldBe(Utc(Wednesday, 20));
            a.Start.ShouldBe(Utc(Friday, 15));
            c.Undo().ShouldBeFalse();

            // A resize, undone, gets its old end back.
            c.ResizeEventTo(a, Friday, 16 * 60, WeekCalendarEditSource.Pointer);
            a.End.ShouldBe(Utc(Friday, 16));
            c.Undo().ShouldBeTrue();
            a.End.ShouldBe(Utc(Friday, 15.5));
            lastUpdate!.Previous.End.ShouldBe(Utc(Friday, 16));

            // A calendar picked in the details is undone too.
            c.OpenDetails(a).ShouldBeTrue();
            var details = (WeekCalendarDetails)c.Editor!;
            details.Calendars!.Chips.First(chip => chip.CalendarId == WeekCalendarDemoData.Home).IsChecked = true;
            a.CalendarId.ShouldBe(WeekCalendarDemoData.Home);
            c.Announcer!.History[^1].ShouldBe("Metrics check moved to Home");
            c.Undo().ShouldBeTrue();
            a.CalendarId.ShouldBe(WeekCalendarDemoData.Studio);
            details.Calendars.SelectedId.ShouldBe(WeekCalendarDemoData.Studio);
            c.CloseEditor();

            // The toast's time running out ends the undo.
            c.DeleteEvent(a).ShouldBeTrue();
            c.CanUndo.ShouldBeTrue();
            c.ExpireUndo();
            c.CanUndo.ShouldBeFalse();
            c.Undo().ShouldBeFalse();
            c.Events.ShouldNotContain(a);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Details_Apply_Times_Dates_And_Calendars_At_Once_And_Titles_On_Close()
    {
        var (window, c) = Show();
        try
        {
            var updates = new List<WeekCalendarEventUpdatedEventArgs>();
            c.EventUpdated += (_, e) => updates.Add(e);
            var kickoff = Event(c, "Sprint kickoff");
            c.OpenDetails(kickoff).ShouldBeTrue();
            var details = (WeekCalendarDetails)c.Editor!;
            details.DurationText.ShouldBe("1 hr");
            details.StartField!.SelectedValue.ShouldBe(9 * 60 + 30);
            details.EndField!.SelectedValue.ShouldBe(10 * 60 + 30);
            details.LocationText.ShouldBe("Room Maple");

            // A new start keeps the length.
            details.StartField.SelectedValue = 10 * 60;
            kickoff.Start.ShouldBe(Utc(Wednesday, 10));
            kickoff.End.ShouldBe(Utc(Wednesday, 11));
            c.Announcer!.History[^1].ShouldBe("Sprint kickoff is now on Wed, Oct 21, 10 – 11 AM");
            details.EndField.SelectedValue = 12 * 60;
            kickoff.End.ShouldBe(Utc(Wednesday, 12));
            details.DurationText.ShouldBe("2 hr");
            details.DateField!.SelectedDate = Friday;
            kickoff.Start.ShouldBe(Utc(Friday, 10));
            kickoff.End.ShouldBe(Utc(Friday, 12));
            updates.Count.ShouldBe(3);
            // The changes of one popover undo together.
            c.UndoEntry!.Previous!.Start.ShouldBe(Utc(Wednesday, 9.5));
            // The popover stays where it opened.
            c.Editor.ShouldBe(details);

            details.TitleBox!.Text = "Sprint kickoff (moved)";
            c.CloseEditor();
            kickoff.Title.ShouldBe("Sprint kickoff (moved)");
            updates.Count.ShouldBe(4);
            updates[^1].Previous.Title.ShouldBe("Sprint kickoff");
            c.Announcer.History[^1].ShouldBe("Title changed to Sprint kickoff (moved)");
            // Renaming is not undoable; the times are.
            c.Undo().ShouldBeTrue();
            kickoff.Start.ShouldBe(Utc(Wednesday, 9.5));
            kickoff.Title.ShouldBe("Sprint kickoff (moved)");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_In_The_Details_Closes_Them_And_Offers_Undo()
    {
        var (window, c) = Show();
        try
        {
            var lunch = Event(c, "Team lunch");
            c.OpenDetails(lunch).ShouldBeTrue();
            var details = (WeekCalendarDetails)c.Editor!;
            details.DeleteButton.ShouldNotBeNull().IsVisible.ShouldBeTrue();
            details.DeleteButton!.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            c.Editor.ShouldBeNull();
            c.Events.ShouldNotContain(lunch);
            c.Toast.ShouldNotBeNull();
            // Undo on the toast; the focus does not drop to the page.
            c.Toast!.UndoButton.ShouldNotBeNull().Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            c.Events.ShouldContain(lunch);
            c.Toast.ShouldBeNull();
            (Focused(window) as Visual).ShouldNotBeNull().FindAncestorOfType<WeekCalendar>(includeSelf: true).ShouldBe(c);
        }
        finally
        {
            window.Close();
        }
    }

    // Data, read only, New event --------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void New_Event_Drafts_At_The_Next_Free_Hour_Unless_The_Request_Is_Handled()
    {
        var (window, c) = Show();
        try
        {
            var newEvent = TestHelpers.Descendant<Button>(c, WeekCalendar.NewEventPart);
            newEvent.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            (quick.Day, quick.Start, quick.End).ShouldBe((Thursday, 17 * 60, 18 * 60));
            c.CloseEditor();
            c.NewEventRequested += (_, e) => e.Handled = true;
            newEvent.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            c.Editor.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Turns_Every_Edit_Off()
    {
        var (window, c) = Show(setup: cal => cal.IsReadOnly = true);
        try
        {
            c.Classes.Contains(WeekCalendar.ReadOnlyPseudoClass).ShouldBeTrue();
            var changes = 0;
            c.EventUpdated += (_, _) => changes++;
            c.EventCreated += (_, _) => changes++;
            c.EventDeleted += (_, _) => changes++;
            var click = At(window, c, Saturday, 13 * 60 + 33);
            Drag(window, click, click + new Vector(0, 80));
            Press(window, click);
            Release(window, click);
            c.Editor.ShouldBeNull();
            var block = Block(c, "Roadmap pass");
            Drag(window, On(window, block, 10), On(window, block, 10) + new Vector(-150, 60));
            block.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Key(window, PhysicalKey.Delete);
            Key(window, PhysicalKey.N);
            c.BeginCreate(Sunday).ShouldBeFalse();
            c.DeleteEvent(Event(c, "Roadmap pass")).ShouldBeFalse();
            changes.ShouldBe(0);
            c.Events.ShouldContain(e => e.Title == "Roadmap pass");
            Event(c, "Roadmap pass").Start.ShouldBe(Utc(Wednesday, 10.5));

            // The details still open, as text: no time fields, no calendar choice, no Delete.
            block.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            var details = c.Editor.ShouldBeOfType<WeekCalendarDetails>();
            details.IsEventReadOnly.ShouldBeTrue();
            details.DeleteButton!.IsVisible.ShouldBeFalse();
            details.StartField!.IsEffectivelyVisible.ShouldBeFalse();
            details.SummaryText.ShouldBe("Wed, Oct 21 10:30 – 11:30 AM, 1 hr");
            details.TitleBox!.IsReadOnly.ShouldBeTrue();
            c.CloseEditor();
            changes.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edits_Write_A_Writable_Source_And_Only_Raise_Events_For_A_Read_Only_One()
    {
        var writable = new ObservableCollection<WeekCalendarEvent>(WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc));
        var (window, c) = Show(setup: cal =>
        {
            cal.Events.Clear();
            cal.EventsSource = writable;
        });
        try
        {
            c.BeginCreate(Sunday, new TimeOnly(9, 0));
            c.SaveQuickCreate((WeekCalendarQuickCreate)c.Editor!);
            writable.ShouldContain(e => e.Title == "Untitled event");
            c.DeleteEvent(writable.First(e => e.Title == "Roadmap pass"));
            writable.ShouldNotContain(e => e.Title == "Roadmap pass");
        }
        finally
        {
            window.Close();
        }

        var fixedList = new ReadOnlyObservableCollection<WeekCalendarEvent>(
            new ObservableCollection<WeekCalendarEvent>(WeekCalendarDemoData.CreateEvents(Monday, TimeZoneInfo.Utc)));
        var (window2, c2) = Show(setup: cal =>
        {
            cal.Events.Clear();
            cal.EventsSource = fixedList;
        });
        try
        {
            var created = 0;
            var deleted = 0;
            c2.EventCreated += (_, _) => created++;
            c2.EventDeleted += (_, _) => deleted++;
            var count = fixedList.Count;
            c2.BeginCreate(Sunday, new TimeOnly(9, 0));
            c2.SaveQuickCreate((WeekCalendarQuickCreate)c2.Editor!);
            c2.DeleteEvent(fixedList.First(e => e.Title == "Roadmap pass"));
            created.ShouldBe(1);
            deleted.ShouldBe(1);
            fixedList.Count.ShouldBe(count);
            // Moves still write the event objects themselves.
            var roadmap = fixedList.First(e => e.Title == "Roadmap pass");
            c2.MoveEventTo(roadmap, Wednesday, 15 * 60, WeekCalendarEditSource.Pointer);
            roadmap.Start.ShouldBe(Utc(Wednesday, 15));
        }
        finally
        {
            window2.Close();
        }
    }

    // Touch and swipes -------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Swipe_Drags_The_Neighbouring_Days_Along_And_Commits_Past_A_Fifth()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(width: 390, level: ArcMotionLevel.Full);
        try
        {
            var grid = Grid(c);
            c.EffectiveView.ShouldBe(WeekCalendarView.ThreeDay);
            var start = c.RangeStart;
            var width = grid.Current!.Head.Bounds.Width;
            width.ShouldBeGreaterThan(0);

            // Not far enough: everything springs back.
            grid.BeginSwipe();
            var (previous, next) = grid.SwipeNeighbours;
            previous.ShouldNotBeNull().Days.Start.ShouldBe(start.AddDays(-3));
            next.ShouldNotBeNull().Days.Start.ShouldBe(start.AddDays(3));
            grid.UpdateSwipe(-40);
            grid.Current!.X.Value.ShouldBe(-40);
            next!.X.Value.ShouldBe(width - 40, 0.01);
            previous!.X.Value.ShouldBe(-width - 40, 0.01);
            grid.EndSwipe(0);
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            c.RangeStart.ShouldBe(start);
            grid.Slots.Count.ShouldBe(1);
            grid.Current!.X.Value.ShouldBe(0);

            // Past 20%: the next days keep going from where the finger left them, with no seam.
            grid.BeginSwipe();
            var incoming = grid.SwipeNeighbours.Next!;
            grid.UpdateSwipe(-0.3 * width);
            grid.EndSwipe(0);
            c.RangeStart.ShouldBe(start.AddDays(3));
            grid.Current.ShouldBe(incoming);
            incoming.X.Value.ShouldBe(0.7 * width, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            incoming.X.Value.ShouldBeInRange(1, 0.7 * width - 1);
            clock.Advance(TimeSpan.FromSeconds(2));
            Pump();
            incoming.X.Value.ShouldBe(0, 0.01);
            grid.Slots.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Swipes_Change_Days_Without_Travel()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, c) = Show(width: 390, level: ArcMotionLevel.Reduced);
        try
        {
            var grid = Grid(c);
            var start = c.RangeStart;
            grid.BeginSwipe();
            grid.UpdateSwipe(-0.4 * grid.Current!.Head.Bounds.Width);
            grid.EndSwipe(0);
            c.RangeStart.ShouldBe(start.AddDays(3));
            grid.Current!.X.Value.ShouldBe(0);
            clock.Advance(TimeSpan.FromSeconds(1));
            Pump();
            grid.Slots.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Calendars_Open_Popovers_Under_The_Event_Inside_The_Sides()
    {
        var (window, c) = Show(width: 390);
        try
        {
            c.BeginCreate(c.RangeStart.AddDays(2), new TimeOnly(12, 0)).ShouldBeTrue();
            var quick = c.Editor.ShouldBeOfType<WeekCalendarQuickCreate>();
            var placement = quick.Placement.ShouldNotBeNull();
            placement.Side.ShouldBeOneOf(WeekCalendarPopoverSide.Below, WeekCalendarPopoverSide.Above);
            placement.TopLeft.X.ShouldBeGreaterThanOrEqualTo(8);
            (placement.TopLeft.X + quick.Bounds.Width).ShouldBeLessThanOrEqualTo(c.Bounds.Width - 8 + 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
