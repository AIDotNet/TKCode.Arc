namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="SeatManager"/> (the Tidewater workspace on the Team plan) and the simulated backend
/// the block falls back to when no handler is set. Nothing here is a real company, person, card network or processor.
/// </summary>
public static class SeatManagerSamples
{
    /// <summary>How long the simulated charge takes when <see cref="SeatManager.UpdateSeatsAsync"/> is not set (900 ms).</summary>
    public static readonly TimeSpan ChargeDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>How long the simulated resend takes when <see cref="SeatManager.ResendInviteAsync"/> is not set (660 ms).</summary>
    public static readonly TimeSpan ResendDelay = TimeSpan.FromMilliseconds(660);

    /// <summary>The sample workspace: "Tidewater".</summary>
    public const string Workspace = "Tidewater";

    /// <summary>The sample receipt address.</summary>
    public const string ReceiptEmail = "accounts@tidewater.example";

    /// <summary>The message of the sample declined charge.</summary>
    public const string DeclinedMessage = "The card was declined. Nothing was charged.";

    /// <summary>The first day of the sample billing period (Oct 18, 2026).</summary>
    public static DateTime PeriodStart => new(2026, 10, 18);

    /// <summary>The last day of the sample billing period (Nov 18, 2026: 31 days).</summary>
    public static DateTime PeriodEnd => new(2026, 11, 18);

    /// <summary>The sample "today" (Oct 27, 2026: 22 days left).</summary>
    public static DateTime Now => new(2026, 10, 27);

    /// <summary>The seats the sample team pays for (12: every seat taken).</summary>
    public const int Purchased = 12;

    /// <summary>The Team plan: $18 a seat each month, up to 25 seats.</summary>
    public static SeatManagerPlan Plan() => new() { Name = "Team", PricePerSeat = 18m, Interval = SeatManagerInterval.Month, MaxSeats = 25 };

    /// <summary>Ten members.</summary>
    public static IReadOnlyList<SeatManagerMember> Members() =>
    [
        new() { Id = "m1", Name = "Mira Okafor" },
        new() { Id = "m2", Name = "Jonas Feld" },
        new() { Id = "m3", Name = "Lena Varga" },
        new() { Id = "m4", Name = "Tomás Quint" },
        new() { Id = "m5", Name = "Ines Halvorsen" },
        new() { Id = "m6", Name = "Ravi Menon" },
        new() { Id = "m7", Name = "Clara Bosch" },
        new() { Id = "m8", Name = "Eli Navarro" },
        new() { Id = "m9", Name = "Yuki Arden" },
        new() { Id = "m10", Name = "Sam Otieno" }
    ];

    /// <summary>Two pending invites.</summary>
    public static IReadOnlyList<SeatManagerInvite> Invites() =>
    [
        new() { Id = "i1", Email = "theo.lindqvist@tidewater.example", InvitedBy = "Mira Okafor", Note = "3 days ago" },
        new() { Id = "i2", Email = "nadia.berg@tidewater.example", InvitedBy = "Jonas Feld", Note = "expires Oct 30" }
    ];

    /// <summary>The sample card: Nova ending 1881, billed via Ledgerline.</summary>
    public static SeatManagerPaymentMethod PaymentMethod() => new() { Brand = "Nova", Last4 = "1881", ProcessorName = "Ledgerline" };

    /// <summary>Fills <paramref name="block"/> with the whole sample: workspace, plan, seats, members, invites, period, card.</summary>
    public static void Apply(SeatManager block)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.Workspace = Workspace;
        block.Plan = Plan();
        block.PeriodStart = PeriodStart;
        block.PeriodEnd = PeriodEnd;
        block.Now = Now;
        block.PaymentMethod = PaymentMethod();
        block.ReceiptEmail = ReceiptEmail;
        block.Members.Clear();
        foreach (var member in Members())
        {
            block.Members.Add(member);
        }
        block.Invites.Clear();
        foreach (var invite in Invites())
        {
            block.Invites.Add(invite);
        }
        block.Purchased = Purchased;
        block.Target = Purchased;
    }

    /// <summary>
    /// A simulated seat update: waits <paramref name="delay"/> (900 ms by default), then fails with
    /// <see cref="DeclinedMessage"/> whenever <paramref name="shouldFail"/> says so.
    /// </summary>
    public static Func<SeatManagerQuote, CancellationToken, Task> SimulatedUpdate(TimeSpan? delay = null, Func<bool>? shouldFail = null) =>
        async (_, cancellation) =>
        {
            await Task.Delay(delay ?? ChargeDelay, cancellation);
            if (shouldFail?.Invoke() == true)
            {
                throw new InvalidOperationException(DeclinedMessage);
            }
        };

    /// <summary>A simulated resend that waits <paramref name="delay"/> (660 ms by default).</summary>
    public static Func<SeatManagerInvite, CancellationToken, Task> SimulatedResend(TimeSpan? delay = null) =>
        (_, cancellation) => Task.Delay(delay ?? ResendDelay, cancellation);

    /// <summary>A simulated revoke that succeeds at once, or fails whenever <paramref name="shouldFail"/> says so.</summary>
    public static Func<SeatManagerInvite, CancellationToken, Task> SimulatedRevoke(Func<bool>? shouldFail = null) =>
        (invite, _) => shouldFail?.Invoke() == true
            ? Task.FromException(new InvalidOperationException($"The invite to {invite.Email} could not be revoked."))
            : Task.CompletedTask;
}
