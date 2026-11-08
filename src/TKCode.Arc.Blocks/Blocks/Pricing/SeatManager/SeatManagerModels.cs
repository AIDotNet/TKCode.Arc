using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How often a <see cref="SeatManagerPlan"/> bills.</summary>
public enum SeatManagerInterval
{
    /// <summary>Billed every month; prices read "per seat/mo".</summary>
    Month,

    /// <summary>Billed every year; prices read "per seat/yr".</summary>
    Year
}

/// <summary>Where the footer of a <see cref="SeatManager"/> is in applying a seat change.</summary>
public enum SeatManagerPhase
{
    /// <summary>Nothing is being applied; the stepper is free.</summary>
    Idle,

    /// <summary>The change is shown for review with Back and Pay (or Confirm); the stepper is locked.</summary>
    Confirm,

    /// <summary>The change is being applied (<see cref="SeatManager.UpdateSeatsAsync"/> runs).</summary>
    Saving,

    /// <summary>The change was applied; the footer says so for a moment, then the block is idle again.</summary>
    Done,

    /// <summary>Applying the change failed; the footer shows the error with Try again.</summary>
    Error
}

/// <summary>The plan the seats are bought on: its name, the price of one seat, the billing interval and the seat cap.</summary>
public sealed class SeatManagerPlan
{
    /// <summary>The plan's name, shown as the badge next to the title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The price of one seat for one interval, in major units of <see cref="Currency"/>.</summary>
    public decimal PricePerSeat { get; set; }

    /// <summary>How often the plan bills (monthly by default).</summary>
    public SeatManagerInterval Interval { get; set; } = SeatManagerInterval.Month;

    /// <summary>ISO 4217 code of every amount ("USD" by default).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The most seats the plan allows; null for no cap. The plus button stops here and says why.</summary>
    public int? MaxSeats { get; set; }
}

/// <summary>A member holding a seat. The first four are drawn as avatars, the rest are counted.</summary>
public sealed class SeatManagerMember
{
    /// <summary>A stable key for the member.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The member's name: the avatar's accessible name, and the seed of its generated gradient.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A picture for the avatar; null draws a gradient generated from <see cref="Name"/>.</summary>
    public IImage? Avatar { get; set; }
}

/// <summary>A pending invite. It reserves a seat until it is accepted or revoked.</summary>
public sealed class SeatManagerInvite
{
    /// <summary>A stable key for the invite.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The address the invite went to.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Who sent the invite; shown in the muted second line when set.</summary>
    public string? InvitedBy { get; set; }

    /// <summary>A short muted suffix for the second line, such as "3 days ago" or "expires Oct 30".</summary>
    public string? Note { get; set; }
}

/// <summary>The card the seats are charged to, named in the footer and in the confirm step.</summary>
public sealed class SeatManagerPaymentMethod
{
    /// <summary>The card's network or brand name ("Nova").</summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>The last four digits of the card.</summary>
    public string Last4 { get; set; } = string.Empty;

    /// <summary>The payment processor; screen readers hear "Billed via …" before the card when it is set.</summary>
    public string? ProcessorName { get; set; }

    /// <summary>A 14px mark drawn before the card; null draws the block's neutral processor mark.</summary>
    public IImage? Mark { get; set; }
}

/// <summary>
/// The price of moving from <see cref="Current"/> to <see cref="Target"/> seats. Added seats are charged today for the
/// days left in the period (<see cref="Delta"/> × price × <see cref="DaysLeft"/> ÷ <see cref="PeriodDays"/>, the total
/// rounded to cents); removed seats cost nothing today and leave at <see cref="RenewsOn"/>.
/// </summary>
/// <param name="Current">Seats paid for now.</param>
/// <param name="Target">Seats after the change.</param>
/// <param name="Delta">Target minus current: positive adds seats, negative removes them.</param>
/// <param name="DueToday">The prorated charge for added seats today; 0 when nothing is added.</param>
/// <param name="NextTotal">The recurring total from the renewal date: target × price per seat.</param>
/// <param name="DaysLeft">Whole days from today to the end of the period.</param>
/// <param name="PeriodDays">Whole days in the billing period.</param>
/// <param name="RenewsOn">The day the period ends and the new total starts.</param>
public readonly record struct SeatManagerQuote(
    int Current,
    int Target,
    int Delta,
    decimal DueToday,
    decimal NextTotal,
    int DaysLeft,
    int PeriodDays,
    DateTime RenewsOn)
{
    /// <summary>
    /// The quote the card shows, exported so a server can check the same numbers: moving from <paramref name="current"/>
    /// to <paramref name="target"/> seats on <paramref name="plan"/> in the period from <paramref name="periodStart"/> to
    /// <paramref name="periodEnd"/>, seen on <paramref name="now"/>.
    /// </summary>
    public static SeatManagerQuote Build(int current, int target, SeatManagerPlan plan, DateTime periodStart, DateTime periodEnd, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return SeatManagerMath.BuildQuote(current, target, plan.PricePerSeat, periodStart, periodEnd, now,
            SeatManagerMath.MinorDigits(plan.Currency));
    }
}

/// <summary>Arguments of <see cref="SeatManager.QuoteChanged"/>: the quote after the stepper moved.</summary>
public sealed class SeatManagerQuoteChangedEventArgs : RoutedEventArgs
{
    internal SeatManagerQuoteChangedEventArgs(RoutedEvent routedEvent, SeatManagerQuote quote) : base(routedEvent)
    {
        Quote = quote;
    }

    /// <summary>The new quote.</summary>
    public SeatManagerQuote Quote { get; }
}

/// <summary>Arguments of <see cref="SeatManager.SeatsChanged"/>: a seat change was applied.</summary>
public sealed class SeatManagerSeatsChangedEventArgs : RoutedEventArgs
{
    internal SeatManagerSeatsChangedEventArgs(RoutedEvent routedEvent, int seats, SeatManagerQuote quote) : base(routedEvent)
    {
        Seats = seats;
        Quote = quote;
    }

    /// <summary>The seats now paid for.</summary>
    public int Seats { get; }

    /// <summary>The quote that was applied.</summary>
    public SeatManagerQuote Quote { get; }
}

/// <summary>Arguments of <see cref="SeatManager.PhaseChanged"/>.</summary>
public sealed class SeatManagerPhaseChangedEventArgs : RoutedEventArgs
{
    internal SeatManagerPhaseChangedEventArgs(RoutedEvent routedEvent, SeatManagerPhase oldPhase, SeatManagerPhase newPhase)
        : base(routedEvent)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }

    /// <summary>The phase before the change.</summary>
    public SeatManagerPhase OldPhase { get; }

    /// <summary>The phase now.</summary>
    public SeatManagerPhase NewPhase { get; }
}

/// <summary>Arguments of the invite events of <see cref="SeatManager"/>.</summary>
public sealed class SeatManagerInviteEventArgs : RoutedEventArgs
{
    internal SeatManagerInviteEventArgs(RoutedEvent routedEvent, SeatManagerInvite invite) : base(routedEvent)
    {
        Invite = invite;
    }

    /// <summary>The invite concerned.</summary>
    public SeatManagerInvite Invite { get; }
}
