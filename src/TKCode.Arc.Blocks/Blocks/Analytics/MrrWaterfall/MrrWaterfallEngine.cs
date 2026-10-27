using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>One row of the side panel as the engine lists it.</summary>
/// <param name="Key">Identity across periods (the row slides when the key stays).</param>
/// <param name="Name">The row's name.</param>
/// <param name="Detail">The second line; null when the account has none.</param>
/// <param name="Amount">The row's positive amount.</param>
/// <param name="Share">The row's share of the step (0–1); all rows add up to 1.</param>
/// <param name="AccountCount">How many accounts the row stands for.</param>
/// <param name="IsRest">The remainder row.</param>
internal readonly record struct MrrRowSpec(string Key, string Name, string? Detail, double Amount, double Share, int AccountCount, bool IsRest);

/// <summary>The pixel box of a bar: left edge, width, top and height, all whole pixels (measured: the reference rounds them).</summary>
internal readonly record struct MrrBarBox(double X, double Width, double Top, double Height)
{
    /// <summary>The bar's bottom edge.</summary>
    public double Bottom => Top + Height;
}

/// <summary>
/// The numbers of the MRR bridge, without any UI: the running totals the bars float between, the value axis rule, the bar
/// and band geometry, the label anchors, the headline ratios, the panel's row list and the bar keys. Everything here was
/// measured on the reference (ADR 0024) and is asserted by the tests.
/// </summary>
/// <remarks>
/// Bars: with n movements there are n + 2 bars, the first from the baseline to the starting level, one floating bar per
/// movement from the previous running level to the new one, the last from the baseline to the ending level. Levels are
/// L0 = start, L(i) = L(i-1) ± movement i, so there are n + 1 of them, and the last is the ending level.
/// </remarks>
internal static class MrrWaterfallEngine
{
    /// <summary>The plot's left edge (the tick labels' column).</summary>
    public const double PlotLeft = 48;

    /// <summary>The plot's top padding.</summary>
    public const double PlotTop = 30;

    /// <summary>The space under the plot for the category labels.</summary>
    public const double PlotBottomInset = 34;

    /// <summary>The widest a bar gets.</summary>
    public const double MaxBarWidth = 56;

    /// <summary>A bar's width as a share of its band.</summary>
    public const double BarRatio = 0.56;

    /// <summary>The gap between a bar's end and its value label.</summary>
    public const double LabelGap = 6;

    /// <summary>Under this band width the category labels shorten and labels lose the currency symbol (measured 64, relaxed to 72).</summary>
    public const double CompactBand = 72;

    /// <summary>Under this band width the category labels shrink from 12 to 10.</summary>
    public const double TinyBand = 48;

    /// <summary>The chart's height range: round(0.44 × width) clamped to 232–300.</summary>
    public static double ChartHeight(double width) => Math.Clamp(Math.Round(width * 0.44, MidpointRounding.AwayFromZero), 232, 300);

    /// <summary>The bottom of the plot (the baseline) in a chart of <paramref name="height"/>.</summary>
    public static double PlotBottom(double height) => height - PlotBottomInset;

    /// <summary>The width of one band: the plot's width over the bar count.</summary>
    public static double Band(double width, int bars) => bars <= 0 ? 0 : Math.Max(0, width - PlotLeft) / bars;

    /// <summary>The bar's width in a band: 56 at most, else 56% of the band, whole pixels.</summary>
    public static double BarWidth(double band) => Math.Min(MaxBarWidth, Math.Round(BarRatio * band, MidpointRounding.AwayFromZero));

    /// <summary>The bar's left edge for a band starting at <paramref name="bandLeft"/>: centred, rounded.</summary>
    public static double BarLeft(double bandLeft, double band) => Math.Round(bandLeft + band / 2 - BarWidth(band) / 2, MidpointRounding.AwayFromZero);

    /// <summary>The band a pointer x falls in (clamped); -1 when there are no bars.</summary>
    public static int IndexAt(double x, double width, int bars)
    {
        if (bars <= 0)
        {
            return -1;
        }
        var band = Band(width, bars);
        return band <= 0 ? 0 : Math.Clamp((int)Math.Floor((x - PlotLeft) / band), 0, bars - 1);
    }

    /// <summary>Whether the category labels use their short form.</summary>
    public static bool IsCompactBand(double band) => band < CompactBand;

    /// <summary>Whether the category labels use the smaller type.</summary>
    public static bool IsTinyBand(double band) => band < TinyBand;

    /// <summary>
    /// The running levels: <c>levels[0]</c> is the start, each further level adds the next signed amount. <paramref name="levels"/>
    /// needs one more slot than there are amounts.
    /// </summary>
    public static void Levels(double start, ReadOnlySpan<double> signedAmounts, Span<double> levels)
    {
        levels[0] = start;
        for (var i = 0; i < signedAmounts.Length; i++)
        {
            levels[i + 1] = levels[i] + signedAmounts[i];
        }
    }

    /// <summary>
    /// The value axis: the domain starts <c>0.6 · span</c> below the lowest level and ends <c>0.05 · span</c> above the
    /// highest, the step is the ladder value (1, 2, 2.5, 5 × 10ⁿ) nearest to a sixth of that range, and the ends round
    /// outward to the step. A flat bridge takes a span of a tenth of its level.
    /// </summary>
    public static AnalyticsTickSet Ticks(ReadOnlySpan<double> levels)
    {
        if (levels.Length == 0)
        {
            return AnalyticsTicks.Waterfall(0, 1);
        }
        var min = levels[0];
        var max = levels[0];
        foreach (var level in levels)
        {
            min = Math.Min(min, level);
            max = Math.Max(max, level);
        }
        return AnalyticsTicks.Waterfall(min, max);
    }

    /// <summary>
    /// The pixel box of bar <paramref name="index"/> for the running <paramref name="levels"/> on the value <paramref name="scale"/>
    /// (bottom pixel first). The first and last bars are totals standing on the baseline; the others float between the
    /// level before and the level after.
    /// </summary>
    public static MrrBarBox Bar(int index, int bars, ReadOnlySpan<double> levels, AnalyticsLinearScale scale, double plotBottom, double bandLeft, double band)
    {
        double top;
        double bottom;
        if (index == 0)
        {
            top = scale.Map(levels[0]);
            bottom = plotBottom;
        }
        else if (index == bars - 1)
        {
            top = scale.Map(levels[^1]);
            bottom = plotBottom;
        }
        else
        {
            var a = scale.Map(levels[index - 1]);
            var b = scale.Map(levels[index]);
            top = Math.Min(a, b);
            bottom = Math.Max(a, b);
        }
        var y = Math.Round(top, MidpointRounding.AwayFromZero);
        var y2 = Math.Round(bottom, MidpointRounding.AwayFromZero);
        return new MrrBarBox(BarLeft(bandLeft, band), BarWidth(band), y, Math.Max(0, y2 - y));
    }

    /// <summary>The y of the connector that leaves bar <paramref name="index"/> (whole pixel minus half): the running level after the bar.</summary>
    public static double ConnectorY(int index, ReadOnlySpan<double> levels, AnalyticsLinearScale scale)
    {
        var level = levels[Math.Min(index, levels.Length - 1)];
        return Math.Round(scale.Map(level), MidpointRounding.AwayFromZero) - 0.5;
    }

    /// <summary>The y a value label hangs from: 6 above a growth or total bar, 6 under a decrease.</summary>
    public static double LabelAnchor(MrrBarBox box, bool below) => below ? box.Bottom + LabelGap : box.Top - LabelGap;

    /// <summary>Whether the label of bar <paramref name="index"/> sits under its bar (the bar is a decrease).</summary>
    public static bool IsBelow(int index, int bars, ReadOnlySpan<double> levels) =>
        index > 0 && index < bars - 1 && levels[index] < levels[index - 1];

    /// <summary>The break mark's vertices on a total bar, relative to the bar's left edge and 14 above the baseline.</summary>
    public static (double X, double Y)[] BreakMark(double barWidth) => [(0, 5), (barWidth, 0), (barWidth, 3), (0, 8)];

    /// <summary>The selection marker's rectangle: the bar's width, 2 high, 2 under the baseline.</summary>
    public static (double Y, double Height) Marker(double plotBottom) => (plotBottom + 2, 2);

    /// <summary>Net revenue retention: the start plus expansion minus contraction minus churn, over the start (new business and reactivation do not count).</summary>
    public static double? NetRevenueRetention(double start, double expansion, double contraction, double churn) =>
        start > 0 ? (start + expansion - contraction - churn) / start : null;

    /// <summary>Quick ratio: what was added over what was lost; null when nothing was lost.</summary>
    public static double? QuickRatio(double added, double lost) => lost > 0 ? added / lost : null;

    /// <summary>Logo churn: accounts that left over accounts at the start.</summary>
    public static double? LogoChurn(int churned, int startingAccounts) => startingAccounts > 0 ? (double)churned / startingAccounts : null;

    /// <summary>The key a pressed key moves the selected bar to; the ends wrap and Home / End jump (null: not a navigation key).</summary>
    public static int? Navigate(Key key, int current, int bars) => AnalyticsKeyboard.Step(key, current, bars, wrap: true);

    /// <summary>
    /// The panel's rows for a step: the <paramref name="maxNamed"/> largest accounts by amount (ties keep their order), then a
    /// remainder row for what they do not explain (amount: total minus the named ones, accounts: the step's count minus
    /// the named ones). Shares are of <paramref name="total"/>. Pass <paramref name="withRest"/> false for the tier lists of the
    /// start and end bars, whose rows are the whole.
    /// </summary>
    public static List<MrrRowSpec> Rows(IReadOnlyList<MrrAccount> accounts, double total, int accountTotal, int maxNamed, bool withRest,
        string restKey, string restName, Func<int, string> restDetail)
    {
        var rows = new List<MrrRowSpec>();
        var ordered = accounts.Select((a, i) => (Account: a, Index: i))
            .OrderByDescending(t => Math.Abs(t.Account.Amount)).ThenBy(t => t.Index)
            .Take(Math.Max(0, maxNamed))
            .Select(t => t.Account).ToList();
        var sum = 0.0;
        var named = 0;
        var denominator = total > 0 ? total : ordered.Sum(a => Math.Abs(a.Amount));
        foreach (var account in ordered)
        {
            var amount = Math.Abs(account.Amount);
            sum += amount;
            named += Math.Max(1, account.AccountCount);
            rows.Add(new MrrRowSpec(account.EffectiveKey, account.Name, account.Detail, amount,
                denominator > 0 ? Math.Min(1, amount / denominator) : 0, account.AccountCount, false));
        }
        if (withRest)
        {
            var rest = total - sum;
            var restCount = Math.Max(0, accountTotal - named);
            if (rest > 0.005 || restCount > 0 && rest > -0.005 && accounts.Count > ordered.Count)
            {
                rows.Add(new MrrRowSpec(restKey, restName, restDetail(restCount), Math.Max(0, rest),
                    denominator > 0 ? Math.Clamp(rest / denominator, 0, 1) : 0, restCount, true));
            }
        }
        return rows;
    }
}
