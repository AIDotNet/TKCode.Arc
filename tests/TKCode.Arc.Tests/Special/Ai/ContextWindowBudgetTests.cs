using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The arithmetic and formats of ContextWindow against the numbers measured from the reference (notes section 2b.8):
/// cumulative rounding half away from zero, the reserve counted back from cell 200, the threshold on a row boundary,
/// token and percent formats, the warning and threshold checks, and compaction.
/// </summary>
public class ContextWindowBudgetTests
{
    private static ContextWindowLayout Layout(long window, long reserve, params long[] tokens) =>
        ContextWindowBudget.Compute(window, reserve, 0.8, tokens);

    [Theory]
    [InlineData(996, "996")]
    [InlineData(900, "900")]
    [InlineData(509, "509")]
    [InlineData(0, "0")]
    [InlineData(-5, "0")]
    [InlineData(4_100, "4.1K")]
    [InlineData(5_300, "5.3K")]
    [InlineData(8_700, "8.7K")]
    [InlineData(2_560, "2.6K")]
    [InlineData(2_000, "2K")]
    [InlineData(4_000, "4K")]
    [InlineData(10_000, "10K")]
    [InlineData(16_000, "16K")]
    [InlineData(16_740, "17K")]
    [InlineData(24_400, "24K")]
    [InlineData(509_000, "509K")]
    [InlineData(508_900, "509K")]
    [InlineData(1_000_000, "1M")]
    [InlineData(1_200_000, "1.2M")]
    [InlineData(999.5, "1K")]
    [InlineData(9_960, "10K")]
    [InlineData(999_600, "1M")]
    public void Tokens_FormatAsMeasured(double value, string expected)
    {
        ContextWindowFormat.Tokens(value).ShouldBe(expected);
    }

    [Fact]
    public void TokensKey_EqualExactlyWhenTheTextIsEqual()
    {
        var values = new double[] { 0, 1, 999, 999.4, 999.5, 1000, 4049, 4050, 4100, 9949, 9950, 10_000, 10_499, 10_500, 509_000, 999_499, 999_500, 1_000_000, 1_049_999, 1_050_000 };
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                (ContextWindowFormat.TokensKey(a) == ContextWindowFormat.TokensKey(b))
                    .ShouldBe(ContextWindowFormat.Tokens(a) == ContextWindowFormat.Tokens(b), $"{a} vs {b}");
            }
        }
    }

    [Theory]
    [InlineData(4_100, 1_000_000, "0.4%")]
    [InlineData(4_100, 400_000, "1.0%")]
    [InlineData(32_000, 400_000, "8.0%")]
    [InlineData(64_000, 1_000_000, "6.4%")]
    [InlineData(32_000, 1_000_000, "3.2%")]
    [InlineData(100_000, 1_000_000, "10%")]
    [InlineData(291_000, 1_000_000, "29%")]
    [InlineData(373_000, 400_000, "93%")]
    [InlineData(24_400, 400_000, "6.1%")]
    [InlineData(9_960, 100_000, "10%")]
    [InlineData(0, 1_000_000, "0.0%")]
    public void LegendPercent_OneDecimalBelowTenWholeAbove(double tokens, double window, string expected)
    {
        ContextWindowFormat.LegendPercent(tokens, window).ShouldBe(expected);
    }

    [Theory]
    [InlineData(427_100, 1_000_000, 43)]
    [InlineData(449_700, 400_000, 112)]
    [InlineData(686_000, 1_000_000, 69)]
    [InlineData(176_520, 512_000, 34)]
    [InlineData(0, 1_000_000, 0)]
    public void HeaderPercent_IsWholeAndCanPassHundred(double used, double window, long expected)
    {
        ContextWindowFormat.HeaderPercent(used, window).ShouldBe(expected);
    }

    [Fact]
    public void Layout_1M_InitialReference()
    {
        // 4.1K system, 32K tools, 100K files, no summary, 291K of messages on a 1M window with a 64K reserve.
        var layout = Layout(1_000_000, 64_000, 4_100, 32_000, 100_000, 0, 291_000);
        layout.CategoryCells.ShouldBe([1, 6, 20, 0, 58]);
        layout.FreeCells.ShouldBe(102);
        layout.ReserveCells.ShouldBe(13);
        layout.PerCell.ShouldBe(5_000);
        layout.Used.ShouldBe(427_100);
        layout.Free.ShouldBe(508_900);
        ContextWindowFormat.Tokens(layout.Free).ShouldBe("509K");
        layout.MessageStart.ShouldBe(27);
        layout.MessageCells.ShouldBe(58);
    }

    [Fact]
    public void Layout_512K_UsesCumulativeRoundingAndRoundsHalfAway()
    {
        // Independent rounding would give tools 13 cells; largest remainder would give a 12-cell reserve.
        var layout = Layout(512_000, 32_000, 4_100, 32_000, 100_000, 0, 291_000);
        layout.CategoryCells.ShouldBe([2, 12, 39, 0, 114]);
        layout.FreeCells.ShouldBe(20);
        layout.ReserveCells.ShouldBe(13);
        ContextWindowFormat.Tokens(layout.PerCell).ShouldBe("2.6K");
    }

    [Fact]
    public void Layout_400K_AfterCompaction()
    {
        var layout = Layout(400_000, 32_000, 4_100, 32_000, 100_000, 16_000, 24_400);
        layout.CategoryCells.ShouldBe([2, 16, 50, 8, 12]);
        layout.FreeCells.ShouldBe(96);
        layout.ReserveCells.ShouldBe(16);
        ContextWindowFormat.Tokens(layout.PerCell).ShouldBe("2K");
    }

    [Fact]
    public void Layout_Overflow_StopsBeforeTheReserve()
    {
        // 450K on a 400K window: the files fill up to the reserve, the summary and messages get no cells, free is 0.
        var layout = Layout(400_000, 32_000, 4_100, 32_000, 373_000, 16_000, 24_400);
        layout.ReserveStart.ShouldBe(184);
        layout.UsedCells.ShouldBe(184);
        layout.CategoryCells.ShouldBe([2, 16, 166, 0, 0]);
        layout.FreeCells.ShouldBe(0);
        layout.Free.ShouldBe(0);
        ContextWindowFormat.Tokens(layout.Used).ShouldBe("450K");
        ContextWindowFormat.HeaderPercent(layout.Used, layout.Window).ShouldBe(112);
        for (var i = 184; i < 200; i++)
        {
            layout.Kinds[i].ShouldBe(ContextWindowCellKind.Reserve);
        }
    }

    [Fact]
    public void Layout_FreeAfterTheThresholdLineIsLighter()
    {
        var layout = Layout(1_000_000, 64_000, 4_100, 32_000, 100_000, 0, 291_000);
        layout.ThresholdRow.ShouldBe(8);
        layout.ThresholdCell.ShouldBe(160);
        layout.Kinds[84].ShouldBe(ContextWindowCellKind.Messages);
        layout.Kinds[85].ShouldBe(ContextWindowCellKind.Free);
        layout.Kinds[159].ShouldBe(ContextWindowCellKind.Free);
        layout.Kinds[160].ShouldBe(ContextWindowCellKind.FreeAfter);
        layout.Kinds[186].ShouldBe(ContextWindowCellKind.FreeAfter);
        layout.Kinds[187].ShouldBe(ContextWindowCellKind.Reserve);
    }

    [Theory]
    [InlineData(0.8, 8)]
    [InlineData(0.75, 8)]
    [InlineData(0.74, 7)]
    [InlineData(0.5, 5)]
    [InlineData(0.62, 6)]
    [InlineData(1.0, 10)]
    [InlineData(0.0, 0)]
    public void ThresholdRow_IsTheThresholdCellRoundedToARow(double threshold, int row)
    {
        ContextWindowBudget.ThresholdRow(threshold).ShouldBe(row);
    }

    [Theory]
    [InlineData(677_000, 1_000_000, false)]
    [InlineData(684_000, 1_000_000, true)]
    [InlineData(680_000, 1_000_000, true)]
    // Shown as "272K" in the reference without a warning, so the exact count was under 85% × 80% × 400K = 272,000.
    [InlineData(271_600, 400_000, false)]
    [InlineData(272_000, 400_000, true)]
    [InlineData(281_000, 400_000, true)]
    [InlineData(347_000, 512_000, false)]
    public void Warning_StartsAt85PercentOfTheThreshold(long used, long window, bool warns)
    {
        ContextWindowBudget.IsWarning(used, window, 0.8).ShouldBe(warns);
    }

    [Fact]
    public void Over_IsAtOrPastTheThreshold()
    {
        ContextWindowBudget.IsOver(800_000, 1_000_000, 0.8).ShouldBeTrue();
        ContextWindowBudget.IsOver(799_999, 1_000_000, 0.8).ShouldBeFalse();
        ContextWindowBudget.IsOver(409_600, 512_000, 0.8).ShouldBeTrue();
    }

    [Theory]
    [InlineData(601_000, "24K")]
    [InlineData(267_000, "16K")]
    [InlineData(278_000, "17K")]
    [InlineData(145_000, "8.7K")]
    [InlineData(89_000, "5.3K")]
    [InlineData(56_000, "4K")]
    [InlineData(44_000, "4K")]
    [InlineData(12_000, "4K")]
    public void DefaultSummary_IsSixPercentBetween4KAnd24K(long folded, string summary)
    {
        ContextWindowFormat.Tokens(ContextWindowBudget.DefaultSummary(folded)).ShouldBe(summary);
    }

    [Fact]
    public void FirstChange_IsTheFirstNewCellOfAnAppend()
    {
        var before = Layout(1_000_000, 64_000, 4_100, 32_000, 100_000, 0, 291_000);
        var after = Layout(1_000_000, 64_000, 4_100, 32_000, 100_000, 0, 322_000);
        ContextWindowBudget.FirstChange(before.Kinds, after.Kinds).ShouldBe(85);
        ContextWindowBudget.FirstChange(after.Kinds, after.Kinds).ShouldBe(-1);
        ContextWindowBudget.FirstChange(null, after.Kinds).ShouldBe(0);
    }

    [Fact]
    public void Samples_Total436K()
    {
        var items = ContextWindowSamples.CreateItems();
        var sums = ContextWindowBudget.Sum(items);
        sums.ShouldBe([3_800, 30_200, 98_000, 0, 304_000]);
        items.Count(i => i.Category == ContextWindowCategory.Messages).ShouldBe(24);
        var models = ContextWindowSamples.CreateModels();
        models.Select(m => m.Window).ShouldBe([1_000_000L, 512_000L, 400_000L]);
        models.Select(m => m.Reserve).ShouldBe([64_000L, 32_000L, 32_000L]);
        ContextWindowSamples.CreateTurns().Count.ShouldBe(5);
        ContextWindowSamples.CreateFiles().Count.ShouldBe(3);
    }

    [Fact]
    public void Compact_FoldsAllButTheNewestTwoIntoASummaryBeforeThem()
    {
        var items = ContextWindowSamples.CreateItems();
        var result = ContextWindowBudget.Compact(items, 2, null, "Summary")!;
        result.Folded.Count.ShouldBe(22);
        result.FoldedTokens.ShouldBe(279_000);
        result.SummaryTokens.ShouldBe(16_740);
        result.Before.ShouldBe(436_000);
        result.After.ShouldBe(173_740);
        ContextWindowFormat.Tokens(result.SummaryTokens).ShouldBe("17K");
        result.Items.Count.ShouldBe(items.Count - 22 + 1);
        var summaryIndex = result.Items.ToList().FindIndex(i => i.Category == ContextWindowCategory.Summary);
        result.Items[summaryIndex + 1].Label.ShouldBe("Draft the rollout checklist");
        result.Items[summaryIndex - 1].Category.ShouldBe(ContextWindowCategory.Files);
        // The input is not changed.
        items.Count.ShouldBe(32);
    }

    [Fact]
    public void Compact_AddsUpTheSummaryAndNeverFoldsIt()
    {
        var first = ContextWindowBudget.Compact(ContextWindowSamples.CreateItems(), 2, null, "Summary")!;
        var items = first.Items.ToList();
        items.Add(new ContextWindowItem(ContextWindowCategory.Messages, "One more", 30_600));
        var second = ContextWindowBudget.Compact(items, 2, null, "Summary")!;
        second.Folded.Count.ShouldBe(1);
        second.SummaryTokens.ShouldBe(4_000);
        second.Items.Count(i => i.Category == ContextWindowCategory.Summary).ShouldBe(1);
        second.Summary!.Tokens.ShouldBe(16_740 + 4_000);
        // Nothing to fold with only the newest two left.
        ContextWindowBudget.Compact(second.Items, 2, null, "Summary").ShouldBeNull();
    }

    [Fact]
    public void Compact_UsesTheSummarizer()
    {
        IReadOnlyList<ContextWindowItem>? seen = null;
        var result = ContextWindowBudget.Compact(ContextWindowSamples.CreateItems(), 2, folded =>
        {
            seen = folded;
            return 1_234;
        }, "Summary")!;
        seen!.Count.ShouldBe(22);
        result.SummaryTokens.ShouldBe(1_234);
    }

    [Fact]
    public void SampleRhythm_WarnsAtTheNinthSendAndCompactsAtTheThirteenth()
    {
        var items = ContextWindowSamples.CreateItems().ToList();
        var turns = ContextWindowSamples.CreateTurns();
        long Used() => items.Sum(i => i.Tokens);
        for (var n = 1; n <= 13; n++)
        {
            var turn = turns[(n - 1) % turns.Count];
            items.Add(new ContextWindowItem(ContextWindowCategory.Messages, turn.Label, turn.Tokens));
            var used = Used();
            if (n == 8)
            {
                used.ShouldBe(665_800);
                ContextWindowBudget.IsWarning(used, 1_000_000, 0.8).ShouldBeFalse();
            }
            if (n == 9)
            {
                used.ShouldBe(690_600);
                ContextWindowBudget.IsWarning(used, 1_000_000, 0.8).ShouldBeTrue();
            }
            if (n == 12)
            {
                used.ShouldBe(767_200);
                ContextWindowBudget.IsOver(used, 1_000_000, 0.8).ShouldBeFalse();
            }
        }
        Used().ShouldBe(802_400);
        ContextWindowBudget.IsOver(Used(), 1_000_000, 0.8).ShouldBeTrue();
        var result = ContextWindowBudget.Compact(items, 2, null, "Summary")!;
        result.Folded.Count.ShouldBe(35);
        result.FoldedTokens.ShouldBe(607_800);
        result.SummaryTokens.ShouldBe(24_000);
        result.After.ShouldBe(218_600);
    }
}
