namespace TKCode.Arc;

/// <summary>The kind of one cell of the seat meter.</summary>
internal enum SeatManagerCellKind
{
    Member,
    Invited,
    Open,
    Adding,
    Removing
}

/// <summary>Which sentence the notice line shows (the temporary limit notice wins over all of them).</summary>
internal enum SeatManagerNoticeKind
{
    /// <summary>A temporary note after pressing past a limit (2.6 s).</summary>
    Info,

    /// <summary>Seats are being added (accent).</summary>
    Adding,

    /// <summary>Open seats are being removed (muted).</summary>
    Removing,

    /// <summary>Some seats are open (success).</summary>
    Open,

    /// <summary>Every seat is taken (warning, with "Add a seat").</summary>
    Full
}

/// <summary>
/// The pure numbers of <see cref="SeatManager"/> (Arc Seat manager, design reference; notes 2b.2): the quote, the meter's
/// cells, the notice priority and the stepper's clamping. No UI, tested against the observed values.
/// </summary>
internal static class SeatManagerMath
{
    /// <summary>Above this many cells the meter merges each kind into one segment.</summary>
    public const int MergeThreshold = 40;

    /// <summary>Minor-unit digits of an ISO currency (0 for JPY and KRW, 2 otherwise).</summary>
    public static int MinorDigits(string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        return code is "JPY" or "KRW" ? 0 : 2;
    }

    /// <summary>
    /// Arc's proration: <c>dueToday = round(delta × price × daysLeft ÷ periodDays)</c> rounded on the total (not per seat)
    /// when seats are added, 0 when they are removed; <c>nextTotal = target × price</c>; the period renews at its end.
    /// </summary>
    public static SeatManagerQuote BuildQuote(int current, int target, decimal price, DateTime periodStart, DateTime periodEnd,
        DateTime now, int decimals = 2)
    {
        var periodDays = Math.Max(1, (periodEnd.Date - periodStart.Date).Days);
        var daysLeft = Math.Clamp((periodEnd.Date - now.Date).Days, 0, periodDays);
        var delta = target - current;
        var due = delta > 0
            ? Math.Round(delta * price * daysLeft / periodDays, decimals, MidpointRounding.AwayFromZero)
            : 0m;
        return new SeatManagerQuote(current, target, delta, due, target * price, daysLeft, periodDays, periodEnd.Date);
    }

    /// <summary>Seats in use: members plus the invites that still hold a seat.</summary>
    public static int Used(int members, int activeInvites) => Math.Max(0, members) + Math.Max(0, activeInvites);

    /// <summary>Paid seats nobody holds.</summary>
    public static int Open(int purchased, int used) => Math.Max(purchased - used, 0);

    /// <summary>
    /// The meter's cells in order: members, invited, open, then the seats being added; when seats are removed, the last
    /// open cells turn to "removing". There are max(purchased, target, used) cells.
    /// </summary>
    public static void FillCells(List<SeatManagerCellKind> into, int members, int invites, int purchased, int target)
    {
        into.Clear();
        members = Math.Max(0, members);
        invites = Math.Max(0, invites);
        var used = members + invites;
        var count = Math.Max(Math.Max(purchased, target), used);
        for (var i = 0; i < members; i++)
        {
            into.Add(SeatManagerCellKind.Member);
        }
        for (var i = 0; i < invites; i++)
        {
            into.Add(SeatManagerCellKind.Invited);
        }
        var open = Open(purchased, used);
        for (var i = 0; i < open; i++)
        {
            into.Add(SeatManagerCellKind.Open);
        }
        while (into.Count < count)
        {
            into.Add(SeatManagerCellKind.Adding);
        }
        if (target < purchased)
        {
            var removing = purchased - Math.Max(target, used);
            for (var i = into.Count - 1; i >= 0 && removing > 0; i--)
            {
                if (into[i] == SeatManagerCellKind.Open)
                {
                    into[i] = SeatManagerCellKind.Removing;
                    removing--;
                }
            }
        }
    }

    /// <summary>The notice line's sentence: delta &gt; 0, then delta &lt; 0, then open seats, then full.</summary>
    public static SeatManagerNoticeKind PickNotice(int delta, int open) => delta switch
    {
        > 0 => SeatManagerNoticeKind.Adding,
        < 0 => SeatManagerNoticeKind.Removing,
        _ => open > 0 ? SeatManagerNoticeKind.Open : SeatManagerNoticeKind.Full
    };

    /// <summary>
    /// The stepper's next value: <paramref name="value"/> + <paramref name="step"/> kept within
    /// [<paramref name="min"/>, <paramref name="max"/>]. <paramref name="hitEdge"/> is −1 or +1 when the step pressed
    /// against the lower or upper limit (the value could not move all the way), 0 otherwise.
    /// </summary>
    public static int Step(int value, int step, int min, int max, out int hitEdge)
    {
        max = Math.Max(min, max);
        var wanted = (long)value + step;
        hitEdge = 0;
        if (wanted < min)
        {
            hitEdge = value <= min ? -1 : 0;
            return min;
        }
        if (wanted > max)
        {
            hitEdge = value >= max ? 1 : 0;
            return max;
        }
        return (int)wanted;
    }
}
