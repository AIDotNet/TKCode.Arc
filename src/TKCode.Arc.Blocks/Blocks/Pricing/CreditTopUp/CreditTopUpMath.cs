namespace TKCode.Arc;

/// <summary>
/// The arithmetic of <see cref="CreditTopUp"/> (Arc Credit top-up, design reference): prices, bonuses, the burn rate,
/// days left, health, bar heights, slider steps and how far each odometer wheel turns. Pure functions.
/// </summary>
internal static class CreditTopUpMath
{
    /// <summary>Credits per rate unit: the rate reads "$8 per 1,000" whatever the bonus.</summary>
    public const long RateUnit = 1_000;

    /// <summary>The bonus of the highest tier at or below <paramref name="credits"/>, in percent; 0 below every tier.</summary>
    public static double BonusPercent(long credits, IReadOnlyList<CreditTopUpBonusTier>? tiers)
    {
        var best = 0.0;
        if (tiers is null)
        {
            return best;
        }
        foreach (var tier in tiers)
        {
            if (tier.MinCredits <= credits && tier.Percent > best)
            {
                best = tier.Percent;
            }
        }
        return best;
    }

    /// <summary>price = credits × unit price; bonus = round(credits × percent / 100); total = credits + bonus.</summary>
    public static CreditTopUpQuote Quote(long credits, CreditTopUpPricing pricing)
    {
        credits = Math.Max(0, credits);
        var percent = BonusPercent(credits, pricing.BonusTiers);
        var bonus = (long)Math.Round(credits * percent / 100.0, MidpointRounding.AwayFromZero);
        return new CreditTopUpQuote(credits, bonus, credits + bonus, credits * pricing.UnitPrice, pricing.Currency);
    }

    /// <summary>The smallest amount that earns a bonus, or null without tiers.</summary>
    public static long? FirstBonusAt(IReadOnlyList<CreditTopUpBonusTier>? tiers)
    {
        long? first = null;
        if (tiers is null)
        {
            return first;
        }
        foreach (var tier in tiers)
        {
            if (tier.Percent > 0 && (first is null || tier.MinCredits < first))
            {
                first = tier.MinCredits;
            }
        }
        return first;
    }

    /// <summary>The price of <see cref="RateUnit"/> credits (fixed: bonuses add credits, not discounts).</summary>
    public static decimal RatePerUnit(CreditTopUpPricing pricing) => pricing.UnitPrice * RateUnit;

    /// <summary>
    /// The average daily usage over the full days: every day but the last, which is today and still running. Null when
    /// there is no full day.
    /// </summary>
    public static double? AverageDaily(IReadOnlyList<CreditTopUpUsageDay>? usage)
    {
        if (usage is null || usage.Count < 2)
        {
            return null;
        }
        long sum = 0;
        for (var i = 0; i < usage.Count - 1; i++)
        {
            sum += Math.Max(0, usage[i].Credits);
        }
        return sum / (double)(usage.Count - 1);
    }

    /// <summary>Whole days the balance lasts at <paramref name="average"/> a day (rounded down); null without a burn rate.</summary>
    public static int? DaysLeft(long balance, double? average)
    {
        if (average is not { } rate || rate <= 0)
        {
            return null;
        }
        return (int)Math.Floor(Math.Max(0, balance) / rate);
    }

    /// <summary>Running low when the balance is below the threshold, whether auto top-up is on or not.</summary>
    public static CreditTopUpHealth Health(long balance, long threshold)
        => balance < threshold ? CreditTopUpHealth.RunningLow : CreditTopUpHealth.Healthy;

    /// <summary>A bar's height: value ÷ the largest value × the plot height.</summary>
    public static double BarHeight(long credits, long max, double plotHeight)
        => max <= 0 ? 0 : Math.Clamp(credits / (double)max, 0, 1) * plotHeight;

    /// <summary>An amount kept inside the range and snapped to its step grid (counted from the minimum).</summary>
    public static long Snap(long value, CreditTopUpRange range)
    {
        var min = range.Minimum;
        var max = Math.Max(min, range.Maximum);
        var step = Math.Max(1, range.Step);
        var clamped = Math.Clamp(value, min, max);
        var steps = (long)Math.Round((clamped - min) / (double)step, MidpointRounding.AwayFromZero);
        return Math.Min(max, min + steps * step);
    }

    /// <summary>PageUp / PageDown: a tenth of the range rounded to whole steps (3,000 for 500–30,000 by 500), at least one step.</summary>
    public static long PageStep(CreditTopUpRange range)
    {
        var step = Math.Max(1, range.Step);
        var tenth = (range.Maximum - range.Minimum) / 10.0;
        return Math.Max(step, (long)Math.Round(tenth / step, MidpointRounding.AwayFromZero) * step);
    }

    /// <summary>Index of the pack equal to <paramref name="credits"/>, or −1.</summary>
    public static int PackIndex(IReadOnlyList<long>? packs, long credits)
    {
        if (packs is null)
        {
            return -1;
        }
        for (var i = 0; i < packs.Count; i++)
        {
            if (packs[i] == credits)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>An index moved by <paramref name="delta"/>, wrapping around <paramref name="count"/>.</summary>
    public static int Wrap(int index, int delta, int count) => count <= 0 ? -1 : (((index + delta) % count) + count) % count;

    /// <summary>
    /// How far each existing digit wheel turns when an odometer goes from <paramref name="from"/> to
    /// <paramref name="to"/> (digits least significant first; the result covers the places both numbers have).
    /// Growing with <paramref name="laps"/>: the highest place whose digit changes turns forward the short count
    /// ((t − c) mod 10); every place below it turns that count plus one full lap, unchanged digits included, so the
    /// number reads like a counter. When no shared place changes but new leading places appear, every shared place
    /// laps. Without laps (Arc's checkout figures) every place turns forward the plain count. Shrinking: every place
    /// turns back ((c − t) mod 10), no laps.
    /// </summary>
    public static int[] PlanTurns(IReadOnlyList<int> from, IReadOnlyList<int> to, bool growing, bool laps)
    {
        var shared = Math.Min(from.Count, to.Count);
        var turns = new int[shared];
        if (!growing)
        {
            for (var i = 0; i < shared; i++)
            {
                turns[i] = -(((from[i] - to[i]) % 10 + 10) % 10);
            }
            return turns;
        }
        var top = -1;
        for (var i = shared - 1; i >= 0; i--)
        {
            if (from[i] != to[i])
            {
                top = i;
                break;
            }
        }
        var lapBelow = laps ? (top >= 0 ? top : (to.Count > from.Count ? shared : -1)) : -1;
        for (var i = 0; i < shared; i++)
        {
            var count = ((to[i] - from[i]) % 10 + 10) % 10;
            turns[i] = i < lapBelow ? count + 10 : count;
        }
        return turns;
    }

    /// <summary>The digits of a non-negative whole number, least significant first ("0" has one digit).</summary>
    public static int[] Digits(long value)
    {
        value = Math.Abs(value);
        if (value == 0)
        {
            return [0];
        }
        var list = new List<int>(20);
        while (value > 0)
        {
            list.Add((int)(value % 10));
            value /= 10;
        }
        return list.ToArray();
    }
}
