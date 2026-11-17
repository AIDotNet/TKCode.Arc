namespace TKCode.Arc.Tests.Special;

/// <summary>
/// The pure planning and timing of <see cref="SpeculativeDecoding"/>: the seeded simulation of the built-in sample (the
/// same rhythm as the measured reference run), the draft-length table, the replayed run and the story times.
/// </summary>
public class SpeculativeDecodingPlannerTests
{
    private static DecodePlan Sample(int length, int seed = SpeculativeDecodingSamples.Seed)
        => DecodePlanner.Simulate(SpeculativeDecodingSamples.Tokens, length, seed, SpeculativeDecoding.DefaultEase);

    private static string Join(IEnumerable<string> tokens) => string.Concat(tokens);

    [Fact]
    public void The_Default_Run_Has_The_Six_Rounds_Of_The_Story()
    {
        var plan = Sample(4);
        plan.Rounds.Count.ShouldBe(6);
        string[][] drafts =
        [
            ["Tomorrow", " at", " 9", ","],
            [",", " the", " north", " wing"],
            [" wing", " switches", " to", " backup"],
            [" for", " 25", " minutes", ","],
            [" we", " replace", " two", " old"],
            [" replace", " two", " old", " panels"]
        ];
        int[] accepted = [2, 2, 4, 4, 0, 4];
        string[] targets = [" 7", " east", " power", " while", " crews", "."];
        for (var r = 0; r < 6; r++)
        {
            plan.Rounds[r].Drafts.ShouldBe(drafts[r]);
            plan.Rounds[r].Accepted.ShouldBe(accepted[r]);
            plan.Rounds[r].Target.ShouldBe(targets[r]);
        }
        Join(plan.Tokens).ShouldBe("Tomorrow at 7, the east wing switches to backup power for 25 minutes, while crews replace two old panels.");
        plan.Rounds[2].IsBonus.ShouldBeTrue();
        plan.Rounds[4].Missed.ShouldBe(" we");
    }

    [Fact]
    public void The_Default_Run_Totals_Match_The_Reference_Shape()
    {
        var stats = Sample(4).Total(0.1)!;
        stats.Passes.ShouldBe(6);
        stats.Drafted.ShouldBe(24);
        stats.Accepted.ShouldBe(16);
        stats.Tokens.ShouldBe(22);
        Math.Round(stats.Speedup, 1).ShouldBe(2.6);
        Math.Round(stats.Acceptance * 100).ShouldBe(67);
        Math.Round(stats.TokensPerPass, 1).ShouldBe(3.7);
    }

    [Fact]
    public void Stats_Accumulate_Round_By_Round()
    {
        var plan = Sample(4);
        plan.StatsAfter(0, 0.1).ShouldBeNull();
        // Round 1: 3 tokens / (1 pass + 0.1 × 4 drafted) = 2.14×, 2 of 4 accepted, 3 tokens per pass.
        var first = plan.StatsAfter(1, 0.1)!;
        first.Speedup.ShouldBe(3 / 1.4, 1e-9);
        first.Acceptance.ShouldBe(0.5);
        first.TokensPerPass.ShouldBe(3);
        // Round 3: 11 tokens / 4.2 = 2.6×, 8 of 12, 3.7; round 5: 17 / 7 = 2.4×, 12 of 20.
        plan.StatsAfter(3, 0.1)!.Speedup.ShouldBe(11 / 4.2, 1e-9);
        plan.StatsAfter(5, 0.1)!.Speedup.ShouldBe(17 / 7.0, 1e-9);
        plan.StatsAfter(5, 0.1)!.Acceptance.ShouldBe(0.6);
    }

    [Fact]
    public void The_Speedup_Peaks_At_A_Middle_Draft_Length()
    {
        // n: passes, drafted, accepted, speedup (one decimal).
        (int Passes, int Drafted, int Accepted, double Speedup)[] table =
        [
            (10, 19, 12, 1.8),
            (8, 21, 14, 2.2),
            (6, 24, 16, 2.6),
            (6, 29, 16, 2.5),
            (6, 33, 16, 2.4),
            (6, 37, 16, 2.3),
            (6, 39, 16, 2.2)
        ];
        for (var n = 2; n <= 8; n++)
        {
            var stats = Sample(n).Total(0.1)!;
            var row = table[n - 2];
            stats.Passes.ShouldBe(row.Passes, $"n={n}");
            stats.Drafted.ShouldBe(row.Drafted, $"n={n}");
            stats.Accepted.ShouldBe(row.Accepted, $"n={n}");
            Math.Round(stats.Speedup, 1).ShouldBe(row.Speedup, $"n={n}");
        }
    }

    [Fact]
    public void The_Default_Seed_Is_The_First_That_Tells_The_Story()
    {
        // The seed search behind SpeculativeDecodingSamples.Seed: the six rounds above at n = 4, the speedup rising to n = 4
        // and falling after it.
        int? found = null;
        for (var seed = 0; seed <= SpeculativeDecodingSamples.Seed && found is null; seed++)
        {
            var plan = Sample(4, seed);
            if (plan.Rounds.Count != 6 || string.Join("|", plan.Rounds.Select(r => r.Accepted)) != "2|2|4|4|0|4")
            {
                continue;
            }
            var speedups = Enumerable.Range(2, 7).Select(n => Sample(n, seed).Total(0.1)!.Speedup).ToArray();
            var rising = speedups[0] < speedups[1] && speedups[1] < speedups[2];
            var falling = speedups[2] > speedups[3] && speedups[3] > speedups[4] && speedups[4] > speedups[5] && speedups[5] > speedups[6];
            if (rising && falling && Join(plan.Rounds[0].Drafts) == "Tomorrow at 9,")
            {
                found = seed;
            }
        }
        found.ShouldBe(SpeculativeDecodingSamples.Seed);
    }

    [Fact]
    public void Every_Round_Leaves_At_Least_The_Last_Token_To_The_Target()
    {
        for (var n = 2; n <= 8; n++)
        {
            var plan = Sample(n);
            var count = SpeculativeDecodingSamples.Tokens.Count;
            foreach (var round in plan.Rounds)
            {
                round.Count.ShouldBe(Math.Min(n, count - 1 - round.Start));
            }
            plan.ByDraft[^1].ShouldBeFalse();
            plan.Tokens.Count.ShouldBe(count);
        }
        var single = DecodePlanner.Simulate([new DecodeToken("Hi")], 4, 1, 0.9);
        single.Rounds.Count.ShouldBe(1);
        single.Rounds[0].Count.ShouldBe(0);
        single.Rounds[0].IsBonus.ShouldBeTrue();
        single.Rounds[0].Target.ShouldBe("Hi");
        DecodePlanner.Simulate([], 4, 1, 0.9).Rounds.ShouldBeEmpty();
    }

    [Fact]
    public void Guesses_After_The_First_Miss_Show_The_Real_Tokens()
    {
        // Ease 0 misses at once; the chips after it are the real tokens (discarded anyway). A token without Miss shows "…".
        DecodeToken[] tokens = [new("a", 1), new(" b", 0), new(" c", 1), new(" d", 1), new("."), new(" e")];
        var plan = DecodePlanner.Simulate(tokens, 4, 3, 0.9);
        plan.Rounds[0].Drafts.ShouldBe(["a", " …", " c", " d"]);
        plan.Rounds[0].Accepted.ShouldBe(1);
        plan.Rounds[0].Target.ShouldBe(" b");
        plan.Rounds[1].Start.ShouldBe(2);
    }

    [Fact]
    public void The_Same_Inputs_Give_The_Same_Run()
    {
        var a = Sample(5, 99);
        var b = Sample(5, 99);
        a.Rounds.Select(r => r.Accepted).ShouldBe(b.Rounds.Select(r => r.Accepted));
        var random = new DecodeRandom(7);
        var first = random.Next();
        var again = new DecodeRandom(7);
        again.Next().ShouldBe(first);
        first.ShouldBeInRange(0, 1);
    }

    [Fact]
    public void A_Recorded_Run_Replays_As_Given()
    {
        var plan = DecodePlanner.Replay(SpeculativeDecodingSamples.CodeCompletion);
        plan.Passes.ShouldBe(6);
        plan.Drafted.ShouldBe(23);
        plan.Accepted.ShouldBe(18);
        plan.Tokens.Count.ShouldBe(24);
        Join(plan.Tokens).ShouldBe("const total = items.reduce((sum, item) => sum + item.price * item.qty, 0);");
        var stats = plan.Total(0.1)!;
        Math.Round(stats.Speedup, 1).ShouldBe(2.9);
        Math.Round(stats.Acceptance * 100).ShouldBe(78);
        stats.TokensPerPass.ShouldBe(4);
        var clamped = DecodePlanner.Replay([new DecodeRound(["a", " b"], 5, ".")]);
        clamped.Rounds[0].Accepted.ShouldBe(2);
        clamped.Rounds[0].IsBonus.ShouldBeTrue();
    }

    [Fact]
    public void The_Timeline_Follows_The_Measured_Beats()
    {
        var timeline = new DecodeTimeline(Sample(4), 0.1, 0.64);
        // A round is k × draftCost × pass of drafting plus one pass: 0.256 + 0.64.
        timeline.Start(1).ShouldBe(0.896, 1e-9);
        timeline.VerifyStart(0).ShouldBe(0.256, 1e-9);
        timeline.SweepEnd(0).ShouldBe(0.512, 1e-9);
        timeline.Settle(0).ShouldBe(0.256 + 0.72 * 0.64, 1e-9);
        timeline.ChipTime(0, 3).ShouldBe(0.192, 1e-9);
        timeline.VerdictTime(0, 0).ShouldBe(0.256 + 0.125 * 0.256, 1e-9);
        timeline.VerdictTime(0, 3).ShouldBe(0.256 + 0.875 * 0.256, 1e-9);
        timeline.MainEnd.ShouldBe(6 * 0.896, 1e-9);
        timeline.LandTime(1).ShouldBe(timeline.Settle(0) + 0.02, 1e-9);
        timeline.Sweep(0, 0.384).ShouldBe(2, 1e-9);
        timeline.RoundAt(0.9).ShouldBe(1);
        timeline.RoundAt(10).ShouldBe(6);
        timeline.SettledAt(timeline.Settle(2)).ShouldBe(3);
        // The target alone writes token m at (m − 1) passes + 0.72 pass: 22 tokens end at 13.9 s.
        timeline.CompareEnd.ShouldBe(21 * 0.64 + 0.72 * 0.64, 1e-9);
        timeline.CompareWritten(0.46).ShouldBe(0);
        timeline.CompareWritten(0.4608).ShouldBe(1);
        timeline.CompareWritten(5.376).ShouldBe(8);
        timeline.CompareWritten(100).ShouldBe(22);
    }

    [Fact]
    public void A_Longer_Draft_Takes_Longer_To_Propose()
    {
        var timeline = new DecodeTimeline(Sample(8), 0.1, 0.64);
        timeline.Start(1).ShouldBe(8 * 0.064 + 0.64, 1e-9);
        // The sweep still crosses every cell in 0.4 pass: 32 ms a cell for 8 drafts.
        timeline.VerdictTime(0, 1).ShouldBe(timeline.VerifyStart(0) + 1.5 / 8 * 0.256, 1e-9);
    }

    [Fact]
    public void A_Round_Without_Drafts_Shows_The_Target_Token_At_Once()
    {
        var plan = DecodePlanner.Replay([new DecodeRound(["Ship", " it", " now"], 2, " today"), new DecodeRound([], 0, ".")]);
        Join(plan.Tokens).ShouldBe("Ship it today.");
        plan.ByDraft.ShouldBe([true, true, false, false]);
        var timeline = new DecodeTimeline(plan, 0.1, 0.64);
        timeline.VerifyStart(1).ShouldBe(timeline.Start(1), 1e-9);
        timeline.SweepEnd(1).ShouldBe(timeline.VerifyStart(1), 1e-9);
        timeline.Sweep(1, timeline.Start(1) + 0.1).ShouldBe(0);
        // 4 tokens / (2 passes + 0.1 × 3 drafted).
        plan.Total(0.1)!.Speedup.ShouldBe(4 / 2.3, 1e-9);
    }
}
