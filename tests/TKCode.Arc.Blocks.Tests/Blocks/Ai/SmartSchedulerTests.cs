using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="SmartScheduler"/> and <see cref="SmartSchedulerPlanner"/>: parsing, finding, selecting, booking and invitations.</summary>
public class SmartSchedulerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 16);

    private static (SmartScheduler Scheduler, Window Window) Show(Action<SmartScheduler>? configure = null)
    {
        var scheduler = new SmartScheduler { Delay = static (_, _) => Task.CompletedTask };
        configure?.Invoke(scheduler);
        var window = TestHelpers.Show(scheduler);
        window.Width = 1100;
        Dispatcher.UIThread.RunJobs();
        return (scheduler, window);
    }

    [Fact]
    public void Parse_reads_days_windows_lunch_soon_and_length()
    {
        var c = SmartSchedulerPlanner.Parse("Afternoons, not Friday, keep lunch free, asap, 45 min, before 5pm", Monday, 5);
        c.NotBefore.ShouldBe(13);
        c.NotAfter.ShouldBe(17);
        c.ExcludedDays.ShouldBe([4]);
        c.AvoidLunch.ShouldBeTrue();
        c.PreferSoon.ShouldBeTrue();
        c.DurationMinutes.ShouldBe(45);
        c.Understood.ShouldContain("Not Friday");
        c.Understood.ShouldContain("45 min");

        var after = SmartSchedulerPlanner.Parse("after 2, 1 hour", Monday, 5);
        after.NotBefore.ShouldBe(14);
        after.DurationMinutes.ShouldBe(60);
        SmartSchedulerPlanner.Parse("whenever", Monday, 5).Understood.ShouldBeEmpty();
    }

    [Fact]
    public void Find_keeps_everyone_free_and_every_limit()
    {
        var people = SmartSchedulerSamples.People().Where(p => p.IsInvited).ToArray();
        var constraints = SmartSchedulerPlanner.Parse(SmartSchedulerSamples.Constraints, Monday, 5);
        var slots = SmartSchedulerPlanner.Find(new SmartSchedulerRequest(people, 30, constraints, 5, 9, 18));
        slots.Count.ShouldBe(3);
        foreach (var slot in slots)
        {
            SmartSchedulerPlanner.AllFree(people, slot.Day, slot.Start, slot.End).ShouldBeTrue();
            slot.Start.ShouldBeGreaterThanOrEqualTo(13);
            slot.Day.ShouldNotBe(4);
            (slot.End - slot.Start).ShouldBe(0.5);
            slot.Reasons[0].ShouldBe("Everyone is free");
        }
        slots.Select(s => s.Day).Distinct().Count().ShouldBe(3);
        slots.ShouldBe(slots.OrderByDescending(s => s.Score).ToArray());
    }

    [Fact]
    public void Find_returns_nothing_when_nobody_can_meet()
    {
        SmartSchedulerPerson[] people =
        [
            new() { Id = "a", Name = "A", Busy = Enumerable.Range(0, 5).Select(d => new SmartSchedulerBusy(d, 9, 18)).ToArray() },
        ];
        SmartSchedulerPlanner.Find(new SmartSchedulerRequest(people, 30, new SmartSchedulerConstraints(), 5, 9, 18)).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Finding_proposes_selects_the_best_and_books_it()
    {
        var (scheduler, window) = Show();
        SmartSchedulerBookedEventArgs? booked = null;
        scheduler.SlotBooked += (_, e) => booked = e;
        try
        {
            scheduler.ChipsPanel!.Children.Count.ShouldBe(3);
            await scheduler.FindAsync();
            scheduler.State.ShouldBe(SmartSchedulerState.Proposed);
            scheduler.Slots.Count.ShouldBe(3);
            scheduler.SelectedSlot.ShouldBeSameAs(scheduler.Slots[0]);
            scheduler.OptionRows.Count.ShouldBe(3);
            scheduler.OptionRows[0].IsSelected.ShouldBeTrue();

            scheduler.SelectSlot(2);
            scheduler.OptionRows[2].IsSelected.ShouldBeTrue();
            scheduler.Week!.SelectedIndex.ShouldBe(2);

            await scheduler.BookAsync();
            scheduler.State.ShouldBe(SmartSchedulerState.Booked);
            booked.ShouldNotBeNull().Slot.ShouldBeSameAs(scheduler.Slots[2]);
            booked.People.Count.ShouldBe(3);
            booked.Start.Date.ShouldBe(Monday.AddDays(booked.Slot.Day).ToDateTime(TimeOnly.MinValue));
            scheduler.StatusText!.Text.ShouldStartWith("Booked for");

            scheduler.StartOver();
            scheduler.State.ShouldBe(SmartSchedulerState.Idle);
            scheduler.Slots.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Arrow_keys_on_the_week_move_between_slots_and_inviting_clears_them()
    {
        var (scheduler, window) = Show();
        try
        {
            await scheduler.FindAsync();
            scheduler.Week!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            scheduler.SelectedSlot.ShouldBeSameAs(scheduler.Slots[1]);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            scheduler.SelectedSlot.ShouldBeSameAs(scheduler.Slots[2]);

            var leo = scheduler.People!.Single(p => p.Id == "leo");
            scheduler.SetInvited(leo, true);
            scheduler.InvitedPeople.Count.ShouldBe(4);
            scheduler.Slots.ShouldBeEmpty();
            scheduler.PeoplePanel!.Children.OfType<Checkbox>().Last().IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_length_in_the_words_sets_the_duration_and_no_times_says_so()
    {
        var (scheduler, window) = Show(s =>
        {
            s.ConstraintsText = "mornings, before 10am, 90 min";
        });
        try
        {
            await scheduler.FindAsync();
            scheduler.DurationMinutes.ShouldBe(90);
            scheduler.State.ShouldBe(SmartSchedulerState.NoTimes);
            scheduler.Classes.Contains(":no-times").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
