using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// A stepped stand-in for the block's timers: <see cref="Delay"/> returns tasks that complete only when
/// <see cref="Advance"/> passes their due time, one at a time in order, running the UI continuations in between.
/// </summary>
internal sealed class SeatManagerManualDelays
{
    private readonly List<(TimeSpan Due, TaskCompletionSource Done)> _pending = new();

    public TimeSpan Now { get; private set; }

    public int PendingCount => _pending.Count(p => !p.Done.Task.IsCompleted);

    public Task Delay(TimeSpan duration, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellation);
        }
        var done = new TaskCompletionSource();
        if (cancellation.CanBeCanceled)
        {
            cancellation.Register(() => done.TrySetCanceled());
        }
        _pending.Add((Now + duration, done));
        return done.Task;
    }

    public void Advance(double milliseconds)
    {
        var end = Now + TimeSpan.FromMilliseconds(milliseconds);
        Dispatcher.UIThread.RunJobs();
        while (true)
        {
            _pending.RemoveAll(p => p.Done.Task.IsCompleted);
            var next = _pending.Where(p => p.Due <= end).OrderBy(p => p.Due).FirstOrDefault();
            if (next.Done is null)
            {
                break;
            }
            Now = next.Due;
            _pending.Remove(next);
            next.Done.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
        Now = end;
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// SeatManager: the sample state, the stepper and its limits, the keyboard model, the confirm → saving → done / error
/// phases, revoke with undo and failure, resend, press-and-hold, focus moves, the demonstration loop, the rolling digits
/// and the meter's keyed cells.
/// </summary>
public class SeatManagerTests
{
    private static SeatManager Sample(SeatManagerManualDelays? delays = null)
    {
        var block = new SeatManager { IsAutoPlayEnabled = false };
        SeatManagerSamples.Apply(block);
        if (delays is not null)
        {
            block.Delay = delays.Delay;
        }
        return block;
    }

    private static Window ShowCard(SeatManager block, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = 600;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string FooterText(SeatManager block) => block.FooterTextContent switch
    {
        TextBlock t => t.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : t.Text ?? string.Empty,
        Grid g => g.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty,
        TextShimmer s => s.Text ?? string.Empty,
        _ => string.Empty
    };

    [AvaloniaFact]
    public void The_Sample_Starts_Full_With_Nothing_To_Update()
    {
        var block = Sample();
        var window = ShowCard(block);
        block.Minimum.ShouldBe(12);
        block.Maximum.ShouldBe(25);
        block.Target.ShouldBe(12);
        block.Quote.DueToday.ShouldBe(0m);
        block.Quote.NextTotal.ShouldBe(216m);
        block.NoticeKey.ShouldBe("Full");
        block.Classes.Contains(":full").ShouldBeTrue();
        block.UpdateButton.IsEnabled.ShouldBeFalse();
        FooterText(block).ShouldBe("Nova ending 1881");
        block.MeterPartControl!.Kinds.Count.ShouldBe(12);
        block.DueRoll!.Text.ShouldBe("$0.00");
        block.NextRoll!.Text.ShouldBe("$216.00/mo");
        window.Close();
    }

    [AvaloniaFact]
    public void Adding_Seats_Rolls_The_Quote_And_Arms_Update()
    {
        var block = Sample();
        var window = ShowCard(block);
        SeatManagerQuote? last = null;
        block.QuoteChanged += (_, e) => last = e.Quote;
        block.Increment();
        block.Increment();
        block.Increment();
        block.Target.ShouldBe(15);
        last!.Value.DueToday.ShouldBe(38.32m);
        block.DueRoll!.Text.ShouldBe("$38.32");
        block.NextRoll!.Text.ShouldBe("$270.00/mo");
        block.NoticeKey.ShouldBe("Adding");
        block.MeterPartControl!.Kinds.Count(k => k == SeatManagerCellKind.Adding).ShouldBe(3);
        block.UpdateButton.IsEnabled.ShouldBeTrue();
        FooterText(block).ShouldBe("+3 seats · $38.32 due now");
        block.Classes.Contains(":adding").ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Pressing_Past_A_Limit_Explains_For_A_Moment()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var window = ShowCard(block);
        block.Decrement();
        block.Target.ShouldBe(12);
        block.NoticeKey.ShouldBe("Info");
        block.LimitNotice.ShouldBe("12 seats are in use. Revoke an invite to go lower.");
        block.MinusPartControl!.IsSoftDisabled.ShouldBeTrue();
        delays.Advance(2599);
        block.NoticeKey.ShouldBe("Info");
        delays.Advance(2);
        block.NoticeKey.ShouldBe("Full");

        block.Target = 25;
        block.PlusPartControl!.IsSoftDisabled.ShouldBeTrue();
        block.Increment();
        block.LimitNotice.ShouldBe("Team stops at 25 seats. Talk to sales for more.");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Seat_Count_Follows_The_Spin_Button_Keys()
    {
        var block = Sample();
        var window = ShowCard(block);
        var spin = block.SpinPartControl!;
        spin.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        block.Target.ShouldBe(13);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        block.Target.ShouldBe(14);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        block.Target.ShouldBe(13);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        block.Target.ShouldBe(18);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        block.Target.ShouldBe(25);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        block.Target.ShouldBe(20);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        block.Target.ShouldBe(12);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        block.Target.ShouldBe(12);
        block.NoticeKey.ShouldBe("Info");
        window.Close();
    }

    [AvaloniaFact]
    public void Review_Back_And_Escape_Move_Focus_Sensibly()
    {
        var block = Sample();
        var window = ShowCard(block);
        block.Increment();
        block.UpdateButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        block.Phase.ShouldBe(SeatManagerPhase.Confirm);
        block.PayButton.Content.ShouldBe("Pay $12.77");
        block.PayButton.IsFocused.ShouldBeTrue();
        FooterText(block).ShouldBe("Pay $12.77 now with Nova ending 1881?");
        block.SpinPartControl!.IsTabStop.ShouldBeFalse();
        block.PlusPartControl!.IsLocked.ShouldBeTrue();
        block.Increment();
        block.Target.ShouldBe(13);
        block.Announcer!.Announcement.ShouldBe("Review: 1 seat added, $12.77 due now.");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        block.UpdateButton.IsFocused.ShouldBeTrue();

        block.Review();
        block.Back();
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        window.Close();
    }

    [AvaloniaFact]
    public void Paying_Saves_Then_Shows_Done_Then_The_Card_Again()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var window = ShowCard(block);
        var phases = new List<SeatManagerPhase>();
        block.PhaseChanged += (_, e) => phases.Add(e.NewPhase);
        int? seats = null;
        block.SeatsChanged += (_, e) => seats = e.Seats;
        block.Increment();
        block.Increment();
        block.Increment();
        block.Review();
        block.PayButton.Focus(NavigationMethod.Tab);
        _ = block.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();
        block.Phase.ShouldBe(SeatManagerPhase.Saving);
        block.PayButton.IsLoading.ShouldBeTrue();
        block.BackButton.IsEnabled.ShouldBeFalse();
        FooterText(block).ShouldBe("Paying with Nova ending 1881");
        delays.Advance(899);
        block.Phase.ShouldBe(SeatManagerPhase.Saving);
        delays.Advance(2);
        block.Phase.ShouldBe(SeatManagerPhase.Done);
        block.Purchased.ShouldBe(15);
        seats.ShouldBe(15);
        block.Quote.Delta.ShouldBe(0);
        block.NoticeKey.ShouldBe("Open");
        FooterText(block).ShouldBe("You're on 15 seats. Receipt sent to accounts@tidewater.example.");
        block.Announcer!.Announcement.ShouldBe("Now on 15 seats. $38.32 charged. Simulated, nothing was charged.");
        // Pay went away and Update seats is disabled: focus lands on the seat count, not nowhere.
        block.SpinPartControl!.IsFocused.ShouldBeTrue();
        block.MeterPartControl!.Kinds.Count(k => k == SeatManagerCellKind.Open).ShouldBe(3);
        delays.Advance(2800);
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        FooterText(block).ShouldBe("Nova ending 1881");
        phases.ShouldBe([SeatManagerPhase.Confirm, SeatManagerPhase.Saving, SeatManagerPhase.Done, SeatManagerPhase.Idle]);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Failed_Update_Shows_The_Message_And_Try_Again()
    {
        var block = Sample();
        var fail = true;
        SeatManagerQuote? applied = null;
        block.UpdateSeatsAsync = (quote, _) =>
        {
            if (fail)
            {
                throw new InvalidOperationException(SeatManagerSamples.DeclinedMessage);
            }
            applied = quote;
            return Task.CompletedTask;
        };
        var window = ShowCard(block);
        block.Increment();
        block.Review();
        _ = block.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();
        block.Phase.ShouldBe(SeatManagerPhase.Error);
        block.ErrorMessage.ShouldBe(SeatManagerSamples.DeclinedMessage);
        block.PayButton.Content.ShouldBe("Try again");
        FooterText(block).ShouldBe(SeatManagerSamples.DeclinedMessage);
        block.Purchased.ShouldBe(12);
        fail = false;
        _ = block.ConfirmAsync();
        Dispatcher.UIThread.RunJobs();
        block.Phase.ShouldBe(SeatManagerPhase.Done);
        applied!.Value.Target.ShouldBe(13);
        block.Purchased.ShouldBe(13);
        block.Announcer!.Announcement.ShouldBe("Now on 13 seats. $12.77 charged.");
        window.Close();
    }

    [AvaloniaFact]
    public void Removing_Seats_Confirms_Without_A_Charge()
    {
        var block = Sample();
        block.Purchased = 15;
        block.Target = 12;
        var window = ShowCard(block);
        block.Quote.Delta.ShouldBe(-3);
        block.NoticeKey.ShouldBe("Removing");
        block.MeterPartControl!.Kinds.Count(k => k == SeatManagerCellKind.Removing).ShouldBe(3);
        FooterText(block).ShouldBe("−3 seats from Nov 18 · nothing due now");
        block.Review();
        block.PayButton.Content.ShouldBe("Confirm");
        FooterText(block).ShouldBe("Drop 3 seats on Nov 18?");
        block.Announcer!.Announcement.ShouldBe("Review: 3 seats removed on Nov 18.");
        window.Close();
    }

    [AvaloniaFact]
    public void Revoke_Frees_The_Seat_At_Once_And_Undo_Brings_It_Back()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var called = 0;
        block.RevokeInviteAsync = (_, _) =>
        {
            called++;
            return Task.CompletedTask;
        };
        var window = ShowCard(block);
        var nadia = block.Invites[1];
        block.Revoke(nadia);
        block.IsRevokePending(nadia).ShouldBeTrue();
        block.Minimum.ShouldBe(11);
        block.Rows[1].IsRevoked.ShouldBeTrue();
        block.MeterPartControl!.Kinds[11].ShouldBe(SeatManagerCellKind.Open);
        block.NoticeKey.ShouldBe("Open");
        block.Announcer!.Announcement.ShouldBe("Invite to nadia.berg@tidewater.example revoked, a seat is free. Undo for 3 seconds.");
        delays.Advance(1000);
        block.Undo(nadia);
        block.IsRevokePending(nadia).ShouldBeFalse();
        block.Rows[1].IsRevoked.ShouldBeFalse();
        block.Minimum.ShouldBe(12);
        delays.Advance(5000);
        called.ShouldBe(0);
        block.Invites.Count.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Revoke_Runs_The_Handler_When_The_Window_Closes()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        SeatManagerInvite? revoked = null;
        block.RevokeInviteAsync = (invite, _) =>
        {
            revoked = invite;
            return Task.CompletedTask;
        };
        SeatManagerInvite? removed = null;
        block.InviteRevoked += (_, e) => removed = e.Invite;
        var window = ShowCard(block);
        var nadia = block.Invites[1];
        block.Revoke(nadia);
        delays.Advance(2999);
        revoked.ShouldBeNull();
        delays.Advance(2);
        revoked.ShouldBe(nadia);
        removed.ShouldBe(nadia);
        block.Invites.Count.ShouldBe(1);
        block.Rows.Count.ShouldBe(1);
        block.Minimum.ShouldBe(11);
        block.Announcer!.Announcement.ShouldBe("Invite to nadia.berg@tidewater.example removed.");
        window.Close();
    }

    [AvaloniaFact]
    public void A_Failed_Revoke_Puts_The_Invite_Back_Where_It_Was()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        block.RevokeInviteAsync = SeatManagerSamples.SimulatedRevoke(() => true);
        var restored = 0;
        block.InviteRestored += (_, _) => restored++;
        var window = ShowCard(block);
        var theo = block.Invites[0];
        block.Revoke(theo);
        block.Minimum.ShouldBe(11);
        delays.Advance(3001);
        block.Invites.Count.ShouldBe(2);
        block.Invites[0].ShouldBe(theo);
        block.Rows[0].Invite.ShouldBe(theo);
        block.Rows[0].IsRevoked.ShouldBeFalse();
        block.IsRevokePending(theo).ShouldBeFalse();
        block.Minimum.ShouldBe(12);
        restored.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Undo_Keeps_The_Stepper_Above_The_Seats_In_Use()
    {
        var block = Sample();
        var window = ShowCard(block);
        var nadia = block.Invites[1];
        block.Revoke(nadia);
        block.Decrement();
        block.Target.ShouldBe(11);
        block.Undo(nadia);
        block.Target.ShouldBe(12);
        window.Close();
    }

    [AvaloniaFact]
    public void Resend_Shows_Sent_And_Stays_Sent()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var window = ShowCard(block);
        var theo = block.Invites[0];
        var row = block.Rows[0];
        block.Resend(theo);
        row.ResendState.ShouldBe(SeatManagerResendState.Sending);
        row.ResendButton.IsLoading.ShouldBeTrue();
        row.RevokeButton.IsEnabled.ShouldBeFalse();
        delays.Advance(659);
        row.ResendState.ShouldBe(SeatManagerResendState.Sending);
        delays.Advance(2);
        row.ResendState.ShouldBe(SeatManagerResendState.Sent);
        row.ResendButton.IsEnabled.ShouldBeFalse();
        row.RevokeButton.IsEnabled.ShouldBeTrue();
        Avalonia.Automation.AutomationProperties.GetName(row.ResendButton).ShouldBe("Invite sent to theo.lindqvist@tidewater.example");
        block.Announcer!.Announcement.ShouldBe("Invite sent again to theo.lindqvist@tidewater.example. Simulated, nothing was sent.");
        delays.Advance(60000);
        row.ResendState.ShouldBe(SeatManagerResendState.Sent);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Failed_Resend_Returns_The_Button()
    {
        var block = Sample();
        block.ResendInviteAsync = (_, _) => Task.FromException(new InvalidOperationException("offline"));
        var window = ShowCard(block);
        block.Resend(block.Invites[0]);
        Dispatcher.UIThread.RunJobs();
        block.Rows[0].ResendState.ShouldBe(SeatManagerResendState.Idle);
        block.Announcer!.Announcement.ShouldBe("Could not resend the invite to theo.lindqvist@tidewater.example.");
        window.Close();
    }

    [AvaloniaFact]
    public void Holding_Plus_Repeats_After_380_Then_Every_70_Milliseconds()
    {
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var window = ShowCard(block);
        var plus = block.PlusPartControl!;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var center = plus.TranslatePoint(new Point(plus.Bounds.Width / 2, plus.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        block.Target.ShouldBe(13);
        delays.Advance(379);
        block.Target.ShouldBe(13);
        delays.Advance(1);
        block.Target.ShouldBe(14);
        delays.Advance(70);
        block.Target.ShouldBe(15);
        delays.Advance(140);
        block.Target.ShouldBe(17);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        delays.Advance(500);
        block.Target.ShouldBe(17);
        plus.IsHolding.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Add_A_Seat_Adds_One_And_Hands_Focus_To_Plus()
    {
        var block = Sample();
        var window = ShowCard(block);
        var action = (SeatManagerTextButton)((Grid)block.NoticeContent!).Children[2];
        action.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        block.Target.ShouldBe(13);
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        block.PlusPartControl!.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void No_Invites_Shows_The_Empty_Row()
    {
        var block = Sample();
        block.Invites.Clear();
        var window = ShowCard(block);
        TestHelpers.Descendant<Control>(block, SeatManager.InvitesEmptyPart).IsVisible.ShouldBeTrue();
        block.Minimum.ShouldBe(10);
        block.NoticeKey.ShouldBe("Open");
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Cards_Tighten_Below_460()
    {
        var block = Sample();
        var window = ShowCard(block);
        block.Width = 400;
        Dispatcher.UIThread.RunJobs();
        block.Classes.Contains(":narrow").ShouldBeTrue();
        block.Width = 520;
        Dispatcher.UIThread.RunJobs();
        block.Classes.Contains(":narrow").ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void The_Demo_Loop_Follows_The_Measured_Timeline_Quietly()
    {
        var delays = new SeatManagerManualDelays();
        var block = new SeatManager { Delay = delays.Delay };
        SeatManagerSamples.Apply(block);
        block.OnScreenOverride = true;
        var window = ShowCard(block);
        delays.Advance(899);
        block.IsAutoPlaying.ShouldBeFalse();
        delays.Advance(1);
        block.IsAutoPlaying.ShouldBeTrue();
        block.Target.ShouldBe(13);
        delays.Advance(282);
        block.Target.ShouldBe(14);
        delays.Advance(300);
        block.Target.ShouldBe(15);
        delays.Advance(1101);
        block.Phase.ShouldBe(SeatManagerPhase.Confirm);
        delays.Advance(1301);
        block.Phase.ShouldBe(SeatManagerPhase.Saving);
        delays.Advance(902);
        block.Phase.ShouldBe(SeatManagerPhase.Done);
        block.Purchased.ShouldBe(15);
        delays.Advance(5281 - 3886);
        block.Rows[0].ResendState.ShouldBe(SeatManagerResendState.Sending);
        delays.Advance(660);
        block.Rows[0].ResendState.ShouldBe(SeatManagerResendState.Sent);
        delays.Advance(6798 - 5941);
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        block.IsRevokePending(block.Invites[1]).ShouldBeTrue();
        delays.Advance(3010);
        block.Invites.Count.ShouldBe(1);
        delays.Advance(11399 - 9808);
        block.Purchased.ShouldBe(12);
        block.Target.ShouldBe(12);
        block.Invites.Count.ShouldBe(2);
        block.Rows.Count.ShouldBe(2);
        block.Rows[0].ResendState.ShouldBe(SeatManagerResendState.Idle);
        block.Announcer!.History.ShouldBeEmpty();
        delays.Advance(12700 - 11399);
        block.Target.ShouldBe(13);
        window.Close();
    }

    [AvaloniaFact]
    public void Any_Press_Inside_The_Card_Hands_Control_To_The_Person()
    {
        var delays = new SeatManagerManualDelays();
        var block = new SeatManager { Delay = delays.Delay };
        SeatManagerSamples.Apply(block);
        block.OnScreenOverride = true;
        var window = ShowCard(block);
        delays.Advance(900 + 600);
        block.Target.ShouldBe(15);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var at = block.TranslatePoint(new Point(40, 40), window)!.Value;
        window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
        block.IsAutoPlaying.ShouldBeFalse();
        delays.Advance(20000);
        block.Target.ShouldBe(15);
        block.Phase.ShouldBe(SeatManagerPhase.Idle);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Demo_Loop_Waits_While_Off_Screen_And_Needs_No_Handlers()
    {
        var delays = new SeatManagerManualDelays();
        var block = new SeatManager { Delay = delays.Delay };
        SeatManagerSamples.Apply(block);
        block.OnScreenOverride = false;
        var window = ShowCard(block);
        delays.Advance(5000);
        block.Target.ShouldBe(12);
        block.OnScreenOverride = true;
        delays.Advance(900);
        block.Target.ShouldBe(13);
        block.UpdateSeatsAsync = (_, _) => Task.CompletedTask;
        block.IsAutoPlaying.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Digits_Roll_By_Their_Own_Change_And_New_Places_Fade_In()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var roll = new SeatManagerRoll { Text = "$0.00", FontSize = 14 };
        var window = TestHelpers.Show(new Border { Child = roll }, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        roll.Text = "$11.35";
        // "$" moved one place left and "1" took its place: both fade in; the other digits roll.
        roll.Opacities[0].ShouldBe(0, 0.01);
        roll.Opacities[1].ShouldBe(0, 0.01);
        roll.Opacities[2].ShouldBe(1);
        var mid = new List<double>();
        clock.Advance(TimeSpan.FromMilliseconds(80));
        mid.AddRange(roll.Positions);
        mid[1].ShouldBeInRange(0.3, 0.999);
        mid[2].ShouldBeInRange(0.5, 2.999);
        clock.Advance(TimeSpan.FromMilliseconds(260));
        roll.Positions.ShouldBe([1, 1, 3, 5]);
        roll.Opacities.ShouldAllBe(o => Math.Abs(o - 1) < 0.001);

        // 5 → 1 rolls down through 4, 3, 2 (never round the short way, never by the whole value).
        roll.Text = "$11.31";
        var seen = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            seen.Add(roll.Positions[^1]);
        }
        seen.ShouldAllBe(p => p >= 0.999 && p <= 5.001);
        seen[^1].ShouldBe(1, 0.001);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Digits_Jump()
    {
        var roll = new SeatManagerRoll { Text = "12" };
        var window = TestHelpers.Show(new Border { Child = roll }, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        roll.Text = "15";
        roll.Positions.ShouldBe([1, 5]);
        roll.Opacities.ShouldAllBe(o => o == 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Meter_Cells_Grow_Shrink_And_Recolour_By_Key()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = new SeatManagerMeter { Width = 500 };
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        var kinds = new List<SeatManagerCellKind>();
        SeatManagerMath.FillCells(kinds, 10, 2, 12, 12);
        meter.Show(kinds);
        meter.Cells.Count.ShouldBe(12);

        SeatManagerMath.FillCells(kinds, 10, 2, 12, 13);
        meter.Show(kinds);
        meter.Cells[12].CurrentWeight.ShouldBe(0, 0.001);
        clock.Advance(TimeSpan.FromMilliseconds(80));
        meter.Cells[12].CurrentWeight.ShouldBeInRange(0.2, 0.95);
        clock.Advance(TimeSpan.FromMilliseconds(600));
        meter.Cells[12].CurrentWeight.ShouldBe(1, 0.01);

        // Revoking turns the last invited cell open in place: same key, same weight.
        SeatManagerMath.FillCells(kinds, 10, 1, 12, 13);
        meter.Show(kinds);
        meter.Cells[11].Kind.ShouldBe(SeatManagerCellKind.Open);
        meter.Cells[11].CurrentWeight.ShouldBe(1, 0.01);

        // Going back to 12 seats: the added cell stays until its weight reaches zero.
        SeatManagerMath.FillCells(kinds, 10, 1, 12, 12);
        meter.Show(kinds);
        meter.Cells.Count.ShouldBe(13);
        meter.Cells[12].Leaving.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(800));
        Dispatcher.UIThread.RunJobs();
        meter.Cells.Count.ShouldBe(12);
        window.Close();
    }

    [AvaloniaFact]
    public void Large_Teams_Merge_Into_One_Segment_Per_Kind()
    {
        var meter = new SeatManagerMeter { Width = 500 };
        var window = TestHelpers.Show(meter);
        var kinds = new List<SeatManagerCellKind>();
        SeatManagerMath.FillCells(kinds, 40, 3, 45, 48);
        meter.Show(kinds);
        meter.Cells.Select(c => c.Kind).ShouldBe([SeatManagerCellKind.Member, SeatManagerCellKind.Invited, SeatManagerCellKind.Open, SeatManagerCellKind.Adding]);
        meter.Cells[0].CurrentWeight.ShouldBe(40);
        window.Close();
    }

    [AvaloniaFact]
    public void Full_Motion_Runs_The_Whole_Flow_Without_Errors()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var delays = new SeatManagerManualDelays();
        var block = Sample(delays);
        var window = ShowCard(block, ArcMotionLevel.Full);
        void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }
        block.Increment();
        Frames(4);
        block.Increment();
        Frames(30);
        block.Review();
        Frames(10);
        _ = block.ConfirmAsync();
        delays.Advance(900);
        Frames(30);
        block.Purchased.ShouldBe(14);
        block.Revoke(block.Invites[1]);
        Frames(10);
        delays.Advance(3000);
        Frames(4);
        block.Rows.Last().HeightFactor.ShouldBeLessThan(1);
        Frames(60);
        Dispatcher.UIThread.RunJobs();
        block.Invites.Count.ShouldBe(1);
        window.Close();
    }
}
