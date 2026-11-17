using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Data;

/// <summary>The booking pill: segments and their pickers, the flow from place to dates to guests, the rules and the texts.</summary>
public class BookingPillTests
{
    private static readonly DateOnly Today = new(2026, 3, 2);

    private static BookingPill Pill() => new()
    {
        MinDate = Today,
        Places =
        {
            new BookingPlace { Name = "Lisbon", Detail = "Old town by the river" },
            new BookingPlace { Name = "Kyoto", Detail = "Temples and quiet lanes" },
            new BookingPlace { Name = "Hobart", Detail = "Harbour and hills" }
        }
    };

    private static Window Show(BookingPill pill)
    {
        var window = TestHelpers.Show(pill);
        window.Width = 760;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Click(Avalonia.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Segments_show_their_labels_and_empty_values()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            var place = pill.SegmentFor(BookingPillPart.Place)!;
            place.Label.ShouldBe("Where");
            place.Value.ShouldBe("Anywhere");
            place.IsEmpty.ShouldBeTrue();
            pill.SegmentFor(BookingPillPart.Dates)!.Value.ShouldBe("Add dates");
            pill.SegmentFor(BookingPillPart.Guests)!.Value.ShouldBe("Add guests");
            AutomationProperties.GetName(place).ShouldBe("Where, Anywhere");
            pill.Popup!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_segment_opens_its_picker_and_the_pill_reshapes_around_it()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            Click(pill.SegmentFor(BookingPillPart.Dates)!);
            pill.OpenPart.ShouldBe(BookingPillPart.Dates);
            pill.Popup!.IsOpen.ShouldBeTrue();
            pill.Classes.Contains(":open").ShouldBeTrue();
            pill.SegmentFor(BookingPillPart.Dates)!.IsOpen.ShouldBeTrue();
            pill.SearchButton!.IsExpanded.ShouldBeTrue();
            pill.Row!.OpenIndex.ShouldBe(1);
            pill.Row.Openness.ShouldBe([0, 1, 0]);
            Dispatcher.UIThread.RunJobs();
            var dates = pill.SegmentFor(BookingPillPart.Dates)!.Bounds.Width;
            var place = pill.SegmentFor(BookingPillPart.Place)!.Bounds.Width;
            dates.ShouldBeGreaterThan(place * 1.2);
            pill.Announcement.ShouldBe("When");

            // A click on the open segment closes it.
            Click(pill.SegmentFor(BookingPillPart.Dates)!);
            pill.OpenPart.ShouldBe(BookingPillPart.None);
            pill.Popup.IsOpen.ShouldBeFalse();
            pill.Row.OpenIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_a_place_moves_on_to_the_dates_and_the_list_filters()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            pill.Open(BookingPillPart.Place);
            Dispatcher.UIThread.RunJobs();
            var options = pill.Pickers.Options;
            options.Count(o => o.IsVisible).ShouldBe(3);
            pill.Pickers.SearchBox!.Text = "hill";
            options.Count(o => o.IsVisible).ShouldBe(1);
            options[0].Title.ShouldBe("Hobart");
            pill.Pickers.SearchBox.Text = "zzz";
            options.Count(o => o.IsVisible).ShouldBe(0);
            pill.Pickers.SearchBox.Text = "";
            Click(options[1]);
            pill.Place.ShouldBe("Kyoto");
            pill.SegmentFor(BookingPillPart.Place)!.Value.ShouldBe("Kyoto");
            pill.OpenPart.ShouldBe(BookingPillPart.Dates);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_picks_make_a_stay_and_move_on_to_the_guests()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            pill.Open(BookingPillPart.Dates);
            pill.CanPick(Today.AddDays(-1)).ShouldBeFalse();
            pill.PickDay(Today.AddDays(-1));
            pill.CheckIn.ShouldBeNull();

            pill.PickDay(new DateOnly(2026, 3, 14));
            pill.CheckIn.ShouldBe(new DateOnly(2026, 3, 14));
            pill.Pickers.StayText.ShouldBe("Pick the day you leave");
            pill.SegmentFor(BookingPillPart.Dates)!.Value.ShouldBe("Mar 14 –");

            // A day before the check-in starts the stay again.
            pill.PickDay(new DateOnly(2026, 3, 10));
            pill.CheckIn.ShouldBe(new DateOnly(2026, 3, 10));
            pill.CheckOut.ShouldBeNull();

            pill.PickDay(new DateOnly(2026, 3, 14));
            pill.CheckOut.ShouldBe(new DateOnly(2026, 3, 14));
            pill.Pickers.StayText.ShouldBe("4 nights");
            pill.SegmentFor(BookingPillPart.Dates)!.Value.ShouldBe("Mar 10 – 14");
            pill.OpenPart.ShouldBe(BookingPillPart.Guests);

            pill.CheckOut = new DateOnly(2026, 4, 2);
            pill.SegmentFor(BookingPillPart.Dates)!.Value.ShouldBe("Mar 10 – Apr 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_month_is_driven_by_the_keyboard()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            pill.Open(BookingPillPart.Dates);
            Dispatcher.UIThread.RunJobs();
            var month = pill.Pickers.Month!;
            month.Focus();
            month.MoveCursor(new DateOnly(2026, 3, 30));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            month.CursorDay.ShouldBe(new DateOnly(2026, 4, 6));
            month.Month.ShouldBe(new DateOnly(2026, 4, 1));
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            month.Month.ShouldBe(new DateOnly(2026, 3, 1));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            pill.CheckIn.ShouldBe(new DateOnly(2026, 3, 6));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Guests_keep_an_adult_with_children_and_stay_within_the_limits()
    {
        var pill = Pill();
        pill.MaxGuests = 3;
        var window = Show(pill);
        try
        {
            pill.Open(BookingPillPart.Guests);
            pill.CanChangeGuests(0, -1).ShouldBeFalse();
            pill.ChangeGuests(1, 1);
            pill.Children.ShouldBe(1);
            pill.Adults.ShouldBe(1);
            // The last adult stays while a child comes along.
            pill.CanChangeGuests(0, -1).ShouldBeFalse();
            pill.ChangeGuests(0, 1);
            pill.CanChangeGuests(1, 1).ShouldBeFalse();
            pill.ChangeGuests(2, 1);
            pill.SegmentFor(BookingPillPart.Guests)!.Value.ShouldBe("3 guests, 1 infant");
            pill.Pickers.Counts[0]!.Value.ShouldBe(2);
            pill.Pickers.PlusButtons[0]!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(pill.Pickers.PlusButtons[2]!).ShouldBe("Increase Infants");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_closes_the_picker_and_reports_everything()
    {
        var pill = Pill();
        pill.Place = "Lisbon";
        pill.CheckIn = new DateOnly(2026, 5, 1);
        pill.CheckOut = new DateOnly(2026, 5, 3);
        pill.Adults = 2;
        BookingPillSearchEventArgs? args = null;
        pill.SearchRequested += (_, e) => args = e;
        var window = Show(pill);
        try
        {
            pill.Open(BookingPillPart.Guests);
            Click(pill.SearchButton!);
            pill.OpenPart.ShouldBe(BookingPillPart.None);
            args.ShouldNotBeNull();
            args.Place.ShouldBe("Lisbon");
            args.CheckIn.ShouldBe(new DateOnly(2026, 5, 1));
            args.CheckOut.ShouldBe(new DateOnly(2026, 5, 3));
            args.Adults.ShouldBe(2);
            AutomationProperties.GetName(pill.SearchButton!).ShouldBe("Search");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_closes_the_picker_and_returns_to_the_segment()
    {
        var pill = Pill();
        var window = Show(pill);
        try
        {
            pill.SegmentFor(BookingPillPart.Place)!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            pill.OpenPart.ShouldBe(BookingPillPart.Place);
            TestHelpers.PumpUntil(() => pill.Pickers.SearchBox!.IsKeyboardFocusWithin).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            pill.OpenPart.ShouldBe(BookingPillPart.None);
            pill.SegmentFor(BookingPillPart.Place)!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_outside_closes_the_picker()
    {
        var pill = Pill();
        var outside = new Border { Height = 100, Background = Avalonia.Media.Brushes.Transparent };
        var window = TestHelpers.Show(new StackPanel { Children = { pill, outside } });
        try
        {
            window.Width = 760;
            window.Height = 600;
            Dispatcher.UIThread.RunJobs();
            pill.Open(BookingPillPart.Place);
            Dispatcher.UIThread.RunJobs();
            var point = outside.TranslatePoint(new Point(700, 90), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            pill.OpenPart.ShouldBe(BookingPillPart.None);
        }
        finally
        {
            window.Close();
        }
    }
}
