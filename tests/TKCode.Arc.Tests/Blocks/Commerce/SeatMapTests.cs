using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Commerce;

/// <summary><see cref="SeatMap"/>: layout, choosing with the limit, totals, the pointer and the keyboard.</summary>
public class SeatMapTests
{
    private static (SeatMap Map, Window Window) Show(double width = 1000)
    {
        var map = new SeatMap();
        SeatMapSamples.ApplyHall(map);
        var window = TestHelpers.Show(new Border { Width = width, Child = map });
        window.Width = 1100;
        window.Height = 1200;
        Pump();
        return (map, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [Fact]
    public void Layout_Numbers_Rows_Bows_And_Opens_Aisles()
    {
        var section = new SeatMapSection { Id = "S", Name = "S", TierId = "t", Rows = 2, SeatsPerRow = 4, AisleAfter = [2], Curve = 1 };
        var seats = SeatMapLayout.Build([section], [new SeatMapTier { Id = "t", Price = 10 }]);
        seats.Count.ShouldBe(8);
        seats[0].Id.ShouldBe("S-A-1");
        seats[7].Id.ShouldBe("S-B-4");
        (seats[2].Position.X - seats[1].Position.X).ShouldBe(2);
        (seats[1].Position.X - seats[0].Position.X).ShouldBe(1);
        seats[1].Position.Y.ShouldBeLessThan(seats[0].Position.Y);
        seats[0].Price.ShouldBe(10);
        SeatMapLayout.Step(seats, 0, 1, 0).ShouldBe(1);
        SeatMapLayout.Step(seats, 0, 0, 1).ShouldBe(4);
        SeatMapLayout.Step(seats, 0, -1, 0).ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Choosing_Respects_Taken_Seats_The_Limit_And_Sums_Fees()
    {
        var (map, window) = Show();
        try
        {
            map.Seats.Count.ShouldBe(80 + 15 + 15 + 80);
            var free = map.Seats.Where(s => !map.IsTaken(s.Id)).ToList();
            var taken = map.Seats.First(s => map.IsTaken(s.Id));
            map.ToggleSeat(taken.Id).ShouldBeFalse();
            map.ToggleSeat("NOPE").ShouldBeFalse();
            var events = 0;
            map.SelectionChanged += (_, _) => events++;
            map.MaxSelection = 2;
            map.ToggleSeat(free[0].Id).ShouldBeTrue();
            map.ToggleSeat(free[1].Id).ShouldBeTrue();
            map.ToggleSeat(free[2].Id).ShouldBeFalse();
            map.LastAnnouncement.ShouldBe("You can choose up to 2 seats.");
            map.Classes.Contains(":full").ShouldBeTrue();
            map.Total.ShouldBe(free[0].Price + free[1].Price + 7m);
            events.ShouldBe(2);
            map.ToggleSeat(free[0].Id).ShouldBeTrue();
            map.SelectedSeats.ShouldBe([free[1]]);
            map.TakenSeats = [free[1].Id];
            map.SelectedSeats.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Press_Chooses_The_Seat_Under_It()
    {
        var (map, window) = Show();
        try
        {
            var canvas = map.Canvas!;
            var index = map.Seats.ToList().FindIndex(s => !map.IsTaken(s.Id));
            var point = canvas.TranslatePoint(canvas.SeatCenter(index), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            map.SelectedSeats.Single().ShouldBe(map.Seats[index]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_The_Cursor_And_Chooses()
    {
        var (map, window) = Show();
        try
        {
            var canvas = map.Canvas!;
            canvas.Focus(NavigationMethod.Tab);
            Pump();
            canvas.CursorIndex.ShouldBe(0);
            AutomationProperties.GetName(canvas).ShouldStartWith("Orchestra · Row A · Seat 1, $120");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            canvas.CursorIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            map.Seats[canvas.CursorIndex].Row.ShouldBe('B');
            var seat = map.Seats[canvas.CursorIndex];
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            map.SelectedSeats.Contains(seat).ShouldBe(!map.IsTaken(seat.Id));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Continue_Reports_The_Seats_And_Narrow_Block_Moves_The_Summary()
    {
        var (map, window) = Show(width: 500);
        try
        {
            map.IsCompact.ShouldBeTrue();
            var seat = map.Seats.First(s => !map.IsTaken(s.Id));
            map.ToggleSeat(seat.Id);
            SeatMapSelectionEventArgs? request = null;
            map.ContinueRequested += (_, e) => request = e;
            var go = TestHelpers.Descendant<StackPanel>(map, "PART_Summary").Children.OfType<Button>().Last();
            go.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            request!.Seats.ShouldBe([seat]);
            request.Total.ShouldBe(seat.Price + 3.5m);
        }
        finally
        {
            window.Close();
        }
    }
}
