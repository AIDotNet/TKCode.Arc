using Avalonia;

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// The arithmetic and pacing of <see cref="PromptCache"/> without UI. The "measured" case is the reference preview's
/// shape (four segments of 3,800 / 2,400 / 36,200 / 6,000 tokens; messages of 180, 240, 410 and 150 tokens; $2.00 input,
/// $2.50 write, $0.20 read; first token 0.42 + processed / 40,000 + cached / 500,000), whose every on-screen number was
/// checked against these formulas. The "default" case is our own built-in story.
/// </summary>
public class PromptCacheMathTests
{
    private static readonly int[] Measured = [3_800, 2_400, 36_200, 6_000];
    private static readonly int[] MeasuredMessages = [180, 240, 410, 150];
    private static readonly PromptCachePricing MeasuredPrices = new() { InputPrice = 2.00, CacheWritePrice = 2.50, CacheReadPrice = 0.20 };

    private static double MeasuredFirstToken(int processed, int cached) => 0.42 + processed / 40_000.0 + cached / 500_000.0;

    private static PromptCacheResult Measure(int message, PromptCacheRequestKind kind, int? brk = null)
        => PromptCacheMath.Compute(Measured, message, kind, brk, MeasuredPrices, MeasuredFirstToken);

    private static readonly int[] Defaults = PromptCacheDefaults.CreateSegments().Select(s => s.Tokens).ToArray();

    private static PromptCacheResult Default(int message, PromptCacheRequestKind kind, int? brk = null)
        => PromptCacheMath.Compute(Defaults, message, kind, brk, PromptCacheDefaults.CreatePricing(), PromptCacheMath.DefaultFirstToken);

    [Fact]
    public void A_Write_Processes_Everything_And_Costs_More_Than_No_Cache()
    {
        var r = Measure(180, PromptCacheRequestKind.Write);
        r.CachedTokens.ShouldBe(0);
        r.WrittenTokens.ShouldBe(48_400);
        r.ProcessedTokens.ShouldBe(48_580);
        PromptCacheMath.FormatTokens(r.ProcessedTokens).ShouldBe("48.6K");
        PromptCacheMath.FormatCost(r.Cost).ShouldBe("$0.1214");
        PromptCacheMath.FormatCost(r.UncachedCost).ShouldBe("$0.0972");
        PromptCacheMath.FormatSeconds(r.FirstToken).ShouldBe("1.63s");
        var (_, _, percent, higher) = PromptCacheMath.CostSummary([r]);
        percent.ShouldBe(25);
        higher.ShouldBeTrue();
    }

    [Fact]
    public void A_Hit_Reads_The_Prefix_And_Processes_Only_The_Message()
    {
        var r = Measure(240, PromptCacheRequestKind.Hit);
        r.CachedTokens.ShouldBe(48_400);
        r.WrittenTokens.ShouldBe(0);
        r.ProcessedTokens.ShouldBe(240);
        PromptCacheMath.FormatCost(r.Cost).ShouldBe("$0.0102");
        PromptCacheMath.FormatSeconds(r.FirstToken).ShouldBe("0.52s");
        PromptCacheMath.FormatCost(Measure(410, PromptCacheRequestKind.Hit).Cost).ShouldBe("$0.0105");
        PromptCacheMath.FormatCost(Measure(150, PromptCacheRequestKind.Hit).Cost).ShouldBe("$0.0100");
        PromptCacheMath.FormatCost(Measure(180, PromptCacheRequestKind.Hit).Cost).ShouldBe("$0.0100");
    }

    [Fact]
    public void A_Partial_Hit_Reads_Up_To_The_Change_And_Writes_The_Rest()
    {
        var r = Measure(150, PromptCacheRequestKind.Partial, 1);
        r.BreakIndex.ShouldBe(1);
        r.CachedTokens.ShouldBe(3_800);
        r.WrittenTokens.ShouldBe(44_600);
        r.ProcessedTokens.ShouldBe(44_750);
        PromptCacheMath.FormatTokens(r.ProcessedTokens).ShouldBe("44.8K");
        PromptCacheMath.FormatTokens(r.CachedTokens).ShouldBe("3.8K");
        PromptCacheMath.FormatTokens(r.WrittenTokens).ShouldBe("44.6K");
        PromptCacheMath.FormatCost(r.Cost).ShouldBe("$0.1126");
        PromptCacheMath.FormatSeconds(r.FirstToken).ShouldBe("1.55s");
        PromptCacheMath.FormatCost(Measure(240, PromptCacheRequestKind.Partial, 1).Cost).ShouldBe("$0.1127");
    }

    [Fact]
    public void The_Cost_Summary_Follows_The_Round()
    {
        var write = Measure(180, PromptCacheRequestKind.Write);
        var hit2 = Measure(240, PromptCacheRequestKind.Hit);
        var hit3 = Measure(410, PromptCacheRequestKind.Hit);
        var hit4 = Measure(150, PromptCacheRequestKind.Hit);
        PromptCacheMath.CostSummary([write, hit2]).ShouldSatisfyAllConditions(
            s => s.Percent.ShouldBe(32), s => s.Higher.ShouldBeFalse());
        PromptCacheMath.CostSummary([write, hit2, hit3]).Percent.ShouldBe(51);
        var round = PromptCacheMath.CostSummary([write, hit2, hit3, hit4]);
        round.Percent.ShouldBe(61);
        PromptCacheMath.FormatCost(round.Cost).ShouldBe("$0.1520");
        PromptCacheMath.FormatCost(round.Uncached).ShouldBe("$0.3892");
        // The fourth request a partial hit instead: 35% lower.
        PromptCacheMath.CostSummary([write, hit2, hit3, Measure(150, PromptCacheRequestKind.Partial, 1)]).Percent.ShouldBe(35);
        // The second request a partial hit: the round still costs 20% more than no cache.
        var early = PromptCacheMath.CostSummary([write, Measure(240, PromptCacheRequestKind.Partial, 1)]);
        early.Percent.ShouldBe(20);
        early.Higher.ShouldBeTrue();
        // A new round whose first request hits a warm cache: 90% lower.
        PromptCacheMath.CostSummary([Measure(180, PromptCacheRequestKind.Hit)]).Percent.ShouldBe(90);
    }

    [Fact]
    public void The_Speedup_Counts_Only_Hits()
    {
        var write = Measure(180, PromptCacheRequestKind.Write);
        PromptCacheMath.SpeedSummary([write]).Hits.ShouldBe(0);
        var one = PromptCacheMath.SpeedSummary([write, Measure(240, PromptCacheRequestKind.Hit)]);
        PromptCacheMath.FormatSpeedup(one.Speedup).ShouldBe("3.1x");
        PromptCacheMath.FormatSeconds(one.FirstToken).ShouldBe("0.52s");
        PromptCacheMath.FormatSeconds(one.Uncached).ShouldBe("1.64s");
        var all = PromptCacheMath.SpeedSummary([write, Measure(240, PromptCacheRequestKind.Hit), Measure(410, PromptCacheRequestKind.Hit),
            Measure(150, PromptCacheRequestKind.Partial, 1)]);
        all.Hits.ShouldBe(2);
        PromptCacheMath.FormatSpeedup(all.Speedup).ShouldBe("3.1x");
        PromptCacheMath.FormatSeconds(PromptCacheMath.SpeedSummary([Measure(180, PromptCacheRequestKind.Hit)]).Uncached).ShouldBe("1.63s");
    }

    [Fact]
    public void The_Default_Story_Has_Its_Own_Numbers()
    {
        var write = Default(190, PromptCacheRequestKind.Write);
        PromptCacheMath.FormatTokens(write.ProcessedTokens).ShouldBe("45.7K");
        PromptCacheMath.FormatCost(write.Cost).ShouldBe("$0.1712");
        PromptCacheMath.FormatSeconds(write.FirstToken).ShouldBe("1.42s");
        var hit2 = Default(260, PromptCacheRequestKind.Hit);
        PromptCacheMath.FormatCost(hit2.Cost).ShouldBe("$0.0144");
        PromptCacheMath.FormatSeconds(hit2.FirstToken).ShouldBe("0.48s");
        var hit3 = Default(420, PromptCacheRequestKind.Hit);
        PromptCacheMath.FormatCost(hit3.Cost).ShouldBe("$0.0149");
        PromptCacheMath.FormatSeconds(hit3.FirstToken).ShouldBe("0.49s");
        var hit4 = Default(160, PromptCacheRequestKind.Hit);
        PromptCacheMath.FormatCost(hit4.Cost).ShouldBe("$0.0141");
        var round = PromptCacheMath.CostSummary([write, hit2, hit3, hit4]);
        PromptCacheMath.FormatCost(round.Cost).ShouldBe("$0.2147");
        PromptCacheMath.FormatCost(round.Uncached).ShouldBe("$0.5491");
        round.Percent.ShouldBe(61);
        PromptCacheMath.FormatSpeedup(PromptCacheMath.SpeedSummary([write, hit2, hit3, hit4]).Speedup).ShouldBe("2.9x");
        var partial = Default(260, PromptCacheRequestKind.Partial, 1);
        PromptCacheMath.FormatTokens(partial.ProcessedTokens).ShouldBe("41.7K");
        PromptCacheMath.FormatCost(partial.Cost).ShouldBe("$0.1573");
        PromptCacheMath.FormatSeconds(partial.FirstToken).ShouldBe("1.33s");
    }

    [Fact]
    public void Requests_Are_Classified_By_The_Cache_And_The_Toggle()
    {
        PromptCacheMath.Classify(warm: false, armed: false, 1, 4).ShouldBe((PromptCacheRequestKind.Write, (int?)null));
        PromptCacheMath.Classify(warm: false, armed: true, 1, 4).ShouldBe((PromptCacheRequestKind.Write, (int?)null));
        PromptCacheMath.Classify(warm: true, armed: false, 1, 4).ShouldBe((PromptCacheRequestKind.Hit, (int?)null));
        PromptCacheMath.Classify(warm: true, armed: true, 1, 4).ShouldBe((PromptCacheRequestKind.Partial, (int?)1));
        PromptCacheMath.Classify(warm: true, armed: true, 3, 4).ShouldBe((PromptCacheRequestKind.Partial, (int?)3));
        // Changing the first segment breaks the match before anything: a write.
        PromptCacheMath.Classify(warm: true, armed: true, 0, 4).ShouldBe((PromptCacheRequestKind.Write, (int?)null));
        PromptCacheMath.Classify(warm: true, armed: true, -1, 4).ShouldBe((PromptCacheRequestKind.Hit, (int?)null));
    }

    [Fact]
    public void Formats_Match_The_Card()
    {
        PromptCacheMath.FormatTokens(240).ShouldBe("240");
        PromptCacheMath.FormatTokens(999.4).ShouldBe("999");
        PromptCacheMath.FormatTokens(6_000).ShouldBe("6K");
        PromptCacheMath.FormatTokens(36_200).ShouldBe("36.2K");
        PromptCacheMath.FormatTokens(999_960).ShouldBe("1M");
        PromptCacheMath.FormatTokens(2_450_000).ShouldBe("2.5M");
        PromptCacheMath.FormatPrice(3).ShouldBe("$3.00");
        PromptCacheMath.FormatPrice(0.3).ShouldBe("$0.30");
        PromptCacheMath.FormatPercent(60.6).ShouldBe("61%");
        PromptCacheMath.FormatSpeedup(3.126).ShouldBe("3.1x");
        PromptCacheMath.FormatClock(258).ShouldBe("4:18");
        PromptCacheMath.FormatClock(300).ShouldBe("5:00");
        PromptCacheMath.DescribeTtl(300).ShouldBe("5-minute");
        PromptCacheMath.DescribeTtl(3600).ShouldBe("1-hour");
        PromptCacheMath.DescribeTtl(90).ShouldBe("90-second");
        PromptCacheMath.DurationWords(300).ShouldBe("5 minutes");
        PromptCacheMath.FormatRatio(0.1).ShouldBe("0.1");
        PromptCacheMath.FormatRatio(1.25).ShouldBe("1.25");
    }

    [Fact]
    public void The_Countdown_Is_The_Ceiling_Of_The_Time_Left_Within_The_Time_To_Live()
    {
        PromptCacheMath.RemainingSeconds(0, 300).ShouldBe(300);
        PromptCacheMath.RemainingSeconds(42, 300).ShouldBe(258);
        PromptCacheMath.RemainingSeconds(41.5, 300).ShouldBe(259);
        PromptCacheMath.RemainingSeconds(299.2, 300).ShouldBe(1);
        PromptCacheMath.RemainingSeconds(300, 300).ShouldBe(0);
        PromptCacheMath.RemainingSeconds(420, 300).ShouldBe(0);
        // A clock a hair before the last use (a restart) never shows more than the time to live.
        PromptCacheMath.RemainingSeconds(-0.5, 300).ShouldBe(300);
        PromptCacheMath.IsLow(239.5, 300).ShouldBeFalse();
        PromptCacheMath.IsLow(240, 300).ShouldBeTrue();
        PromptCacheMath.Drained(150, 300).ShouldBe(0.5);
        PromptCacheMath.Drained(-3, 300).ShouldBe(0);
        PromptCacheMath.Drained(301, 300).ShouldBe(1);
    }

    [Fact]
    public void The_Ring_Follows_About_A_Fifth_Of_The_Way_Each_Frame()
    {
        var step = PromptCacheMath.Follow(0, 1, 1.0 / 60);
        step.ShouldBe(0.217, 0.01);
        PromptCacheMath.Follow(0.4, 0.4, 1).ShouldBe(0.4);
        PromptCacheMath.Follow(1, 0, 0).ShouldBe(1);
        PromptCacheMath.Follow(1, 0, 1).ShouldBe(0, 0.001);
    }

    [Fact]
    public void The_Idle_Clock_Drains_A_Whole_Time_To_Live_In_About_Four_Seconds()
    {
        PromptCacheTimeline.DrainTime(300, 300).ShouldBe(4.2, 1e-9);
        // Sixty seconds left (the warning) arrives 3.36s into the drain.
        (PromptCacheTimeline.DrainTime(300, 300) - PromptCacheTimeline.DrainTime(60, 300)).ShouldBe(3.36, 1e-9);
        PromptCacheTimeline.DrainTime(150, 600).ShouldBe(1.05, 1e-9);
        PromptCacheTimeline.SmoothStep(0.5).ShouldBe(0.5);
        PromptCacheTimeline.SmoothStep(0).ShouldBe(0);
        PromptCacheTimeline.SmoothStep(1.2).ShouldBe(1);
    }

    [Fact]
    public void Bars_Use_A_Square_Root_Scale_With_Two_Pixel_Gaps()
    {
        var grows = PromptCacheTimeline.Grows(Measured);
        var message = Math.Sqrt(MeasuredMessages.Max());
        Span<Rect> row = stackalloc Rect[4];
        var slot = PromptCacheTimeline.Layout(grows, 0, 4, message, 0, 0, 268, 10, 2, row);
        row[0].Width.ShouldBe(40.2, 0.05);
        row[1].Width.ShouldBe(31.9, 0.05);
        row[2].Width.ShouldBe(124.0, 0.05);
        row[3].Width.ShouldBe(50.5, 0.05);
        slot.Width.ShouldBe(13.5, 0.05);
        (row[1].X - row[0].Right).ShouldBe(2, 1e-9);
        (slot.X - row[3].Right).ShouldBe(2, 1e-9);
        slot.Right.ShouldBe(268, 1e-9);
        Span<Rect> panel = stackalloc Rect[4];
        PromptCacheTimeline.Layout(grows, 0, 4, 0, 0, 0, 158, 10, 2, panel).ShouldBe(default);
        panel[0].Width.ShouldBe(24.8, 0.05);
        panel[1].Width.ShouldBe(19.7, 0.05);
        panel[2].Width.ShouldBe(76.4, 0.05);
        panel[3].Width.ShouldBe(31.1, 0.05);
        panel[3].Right.ShouldBe(158, 1e-9);
    }

    [Fact]
    public void A_Write_Sweeps_At_One_Steady_Speed_Then_Flies_Lands_And_Finishes()
    {
        var grows = PromptCacheTimeline.Grows(Measured);
        var durations = PromptCacheTimeline.SweepDurations(grows, Math.Sqrt(410));
        durations.Select(d => Math.Round(d * 1000)).ShouldBe([170, 135, 525, 214]);
        var plan = PromptCacheTimeline.Plan(PromptCacheRequestKind.Write, null, durations, 10, full: true);
        plan.SweepStarts[0].ShouldBe(10);
        plan.SweepStarts[3].ShouldBe(10 + durations[0] + durations[1] + durations[2], 1e-9);
        plan.MessageStart.ShouldBe(11.044, 0.001);
        plan.FlightStart.ShouldBe(plan.MessageStart);
        (plan.Land - plan.FlightStart).ShouldBe(0.45, 1e-9);
        (plan.Done - plan.Start).ShouldBe(1.56, 0.02);
        plan.CachedTo.ShouldBe(0);
    }

    [Fact]
    public void A_Hit_And_A_Partial_Hit_Follow_The_Measured_Beats()
    {
        var durations = PromptCacheTimeline.SweepDurations(PromptCacheTimeline.Grows(Measured), Math.Sqrt(410));
        var hit = PromptCacheTimeline.Plan(PromptCacheRequestKind.Hit, null, durations, 0, full: true);
        hit.Done.ShouldBe(0.584, 1e-9);
        hit.CachedTo.ShouldBe(4);
        hit.BandStart.ShouldBe(0);
        hit.MessageStart.ShouldBe(0.28, 1e-9);
        hit.RefreshAt.ShouldBe(0.19, 1e-9);
        double.IsNaN(hit.FlightStart).ShouldBeTrue();
        var partial = PromptCacheTimeline.Plan(PromptCacheRequestKind.Partial, 1, durations, 0, full: true);
        partial.CachedTo.ShouldBe(1);
        partial.SweepFrom.ShouldBe(1);
        double.IsNaN(partial.SweepStarts[0]).ShouldBeTrue();
        partial.SweepStarts[1].ShouldBe(0.24, 1e-9);
        partial.MessageStart.ShouldBe(1.114, 0.002);
        partial.MarkStart.ShouldBe(0.14, 1e-9);
        partial.PanelWritingAt.ShouldBe(0.134, 1e-9);
        partial.Done.ShouldBe(1.633, 0.005);
        var reduced = PromptCacheTimeline.Plan(PromptCacheRequestKind.Partial, 2, durations, 5, full: false);
        reduced.Done.ShouldBe(5);
        reduced.Land.ShouldBe(5);
        reduced.CachedTo.ShouldBe(2);
    }
}
