using Avalonia;

namespace TKCode.Arc.Tests.Special;

/// <summary>The routing arithmetic, timeline and stage geometry of <see cref="MixtureOfExperts"/>, without UI.</summary>
public class MixtureOfExpertsPlannerTests
{
    private static (string, IReadOnlyList<double>?)[] Sample()
        => MixtureOfExpertsSamples.CreateTokens().Select(t => (t.Text, (IReadOnlyList<double>?)t.Scores)).ToArray();

    private static string[] Names => MixtureOfExpertsSamples.CreateExperts().Select(e => e.Name).ToArray();

    [Fact]
    public void Scores_Are_Normalized_And_Rounded_On_Their_Own()
    {
        var p = MixtureOfExpertsPlanner.Normalize([1, 1, 2], 3);
        p.Sum().ShouldBe(1, 1e-12);
        p[2].ShouldBe(0.5, 1e-12);
        // Missing entries count as zero, negatives too, extra entries are ignored.
        MixtureOfExpertsPlanner.Normalize([2, -1], 4).ShouldBe([1, 0, 0, 0]);
        MixtureOfExpertsPlanner.Normalize([1, 1, 1, 1, 9], 2).ShouldBe([0.5, 0.5]);
        // All zero becomes uniform.
        MixtureOfExpertsPlanner.Normalize([0, 0], 2).ShouldBe([0.5, 0.5]);
        // Independent rounding: 0.735 and 0.265 of a pair, half up.
        MixtureOfExpertsPlanner.Percent(50 / 68.0).ShouldBe(74);
        MixtureOfExpertsPlanner.Percent(18 / 68.0).ShouldBe(26);
        MixtureOfExpertsPlanner.Percent(0.625).ShouldBe(63);
        MixtureOfExpertsPlanner.Percent(0.375).ShouldBe(38);
    }

    [Fact]
    public void Top_K_Breaks_Ties_Toward_The_Lower_Index_And_Weights_Renormalize()
    {
        MixtureOfExpertsPlanner.TopK([0.2, 0.4, 0.4], 2).ShouldBe([1, 2]);
        MixtureOfExpertsPlanner.TopK([0.3, 0.3, 0.4], 1).ShouldBe([2]);
        MixtureOfExpertsPlanner.TopK([0.25, 0.25, 0.25, 0.25], 2).ShouldBe([0, 1]);
        var w = MixtureOfExpertsPlanner.Weights([0.44, 0.22, 0.34], [0, 1]);
        MixtureOfExpertsPlanner.Percent(w[0]).ShouldBe(67);
        MixtureOfExpertsPlanner.Percent(w[1]).ShouldBe(33);
        MixtureOfExpertsPlanner.Weights([0.5, 0.18, 0.32], [0]).ShouldBe([1.0]);
    }

    [Fact]
    public void Capacity_Is_The_Ceiling_Of_Factor_Times_Tokens_Times_K_Over_Experts()
    {
        MixtureOfExpertsPlanner.Capacity(1, 15, 2, 8).ShouldBe(4);
        MixtureOfExpertsPlanner.Capacity(1, 15, 1, 8).ShouldBe(2);
        MixtureOfExpertsPlanner.Capacity(1, 16, 2, 8).ShouldBe(4);
        MixtureOfExpertsPlanner.Capacity(1.25, 16, 2, 8).ShouldBe(5);
        MixtureOfExpertsPlanner.Capacity(0.01, 4, 1, 8).ShouldBe(1);
        MixtureOfExpertsPlanner.Capacity(double.PositiveInfinity, 15, 2, 8).ShouldBe(int.MaxValue);
    }

    [Fact]
    public void Load_Balance_Matches_The_Observed_Values()
    {
        // Load vectors and readouts observed on the reference (section 2b.5 of the notes).
        (int[] Loads, int Percent)[] observed =
        [
            ([0, 0, 0, 1, 0, 0, 0, 0], 0),
            ([0, 0, 0, 1, 0, 1, 0, 0], 57),
            ([0, 1, 0, 1, 0, 2, 0, 2], 76),
            ([0, 2, 0, 1, 0, 2, 0, 3], 71),
            ([0, 3, 1, 1, 0, 2, 0, 3], 80),
            ([0, 4, 2, 1, 1, 2, 1, 3], 82),
            ([0, 4, 2, 2, 1, 2, 2, 3], 86),
            ([0, 4, 2, 2, 1, 2, 3, 3], 87),
            ([0, 4, 3, 2, 2, 2, 3, 3], 90),
            ([0, 4, 3, 2, 3, 3, 3, 3], 93),
            ([3, 4, 4, 3, 4, 4, 3, 3], 98),
        ];
        foreach (var (loads, percent) in observed)
        {
            MixtureOfExpertsPlanner.Percent(MixtureOfExpertsPlanner.LoadBalance(loads)!.Value).ShouldBe(percent);
        }
        MixtureOfExpertsPlanner.LoadBalance([0, 0, 0]).ShouldBeNull();
        MixtureOfExpertsPlanner.LoadBalance([2, 2]).ShouldBe(1);
    }

    [Fact]
    public void Top_Two_Plan_Of_The_Sample_Skips_Two_Second_Choices()
    {
        var plan = MixtureOfExpertsPlanner.Plan(Sample(), 8, 2, 1);
        var names = Names;
        plan.Capacity.ShouldBe(4);
        plan.Routes.Length.ShouldBe(15);
        int[] balance = [57, 86, 76, 71, 69, 76, 82, 84, 87, 90, 93, 94, 95, 97, 98];
        for (var i = 0; i < 15; i++)
        {
            MixtureOfExpertsPlanner.Percent(plan.Routes[i].LoadBalance!.Value).ShouldBe(balance[i], $"token {i}");
        }
        var first = plan.Routes[0];
        first.Experts.Select(e => names[e]).ShouldBe(["People", "Syntax"]);
        first.Weights.Select(MixtureOfExpertsPlanner.Percent).ShouldBe([71, 29]);
        var seven = plan.Routes[7];
        seven.Experts.Select(e => names[e]).ShouldBe(["Digits", "Math"]);
        seven.Dropped.Select(e => names[e]).ShouldBe(["Math"]);
        // The skipped choice keeps its weight; the other is not raised to 100%.
        seven.Weights.Select(MixtureOfExpertsPlanner.Percent).ShouldBe([76, 24]);
        plan.Routes[12].Dropped.Select(e => names[e]).ShouldBe(["Syntax"]);
        plan.Routes.Count(r => r.Dropped.Count > 0).ShouldBe(2);
        plan.Routes[^1].Routes.ShouldBe(28);
        plan.Routes[^1].Loads.ShouldBe([4, 2, 4, 3, 4, 3, 4, 4]);
        plan.Routes.Any(r => r.IsPassThrough).ShouldBeFalse();
    }

    [Fact]
    public void Top_One_Plan_Of_The_Sample_Passes_The_Last_Token_Through()
    {
        var plan = MixtureOfExpertsPlanner.Plan(Sample(), 8, 1, 1);
        plan.Capacity.ShouldBe(2);
        int[] balance = [0, 57, 76, 86, 91, 76, 82, 86, 89, 91, 94, 95, 97, 98, 98];
        for (var i = 0; i < 15; i++)
        {
            MixtureOfExpertsPlanner.Percent(plan.Routes[i].LoadBalance!.Value).ShouldBe(balance[i], $"token {i}");
        }
        var last = plan.Routes[^1];
        last.IsPassThrough.ShouldBeTrue();
        Names[last.Experts[0]].ShouldBe("Marks");
        last.Routes.ShouldBe(14);
        plan.Routes[0].Weights.ShouldBe([1.0]);
    }

    [Fact]
    public void An_Infinite_Capacity_Factor_Never_Drops()
    {
        var plan = MixtureOfExpertsPlanner.Plan(Sample(), 8, 2, double.PositiveInfinity);
        plan.IsUnlimited.ShouldBeTrue();
        plan.Routes.All(r => r.Dropped.Count == 0).ShouldBeTrue();
        plan.Routes[^1].Routes.ShouldBe(30);
        // Without a capacity the bar fills relative to the busiest expert.
        plan.CapacityFill(2, [2, 4]).ShouldBe(0.5);
    }

    [Fact]
    public void Every_Choice_Full_Passes_Through_Without_Load()
    {
        // Two experts, capacity 1 at factor 1 with one token per ... : force it with a tiny factor.
        (string, IReadOnlyList<double>?)[] tokens = [("a", [3.0, 1.0]), ("b", [3.0, 1.0]), ("c", [3.0, 1.0])];
        var plan = MixtureOfExpertsPlanner.Plan(tokens, 2, 2, 0.3);
        plan.Capacity.ShouldBe(1);
        plan.Routes[0].Dropped.ShouldBeEmpty();
        plan.Routes[1].IsPassThrough.ShouldBeTrue();
        plan.Routes[1].Routes.ShouldBe(2);
        plan.Routes[1].Loads.ShouldBe([1, 1]);
    }

    [Fact]
    public void Synthesized_Scores_Are_Stable_And_Shaped_Like_A_Gate()
    {
        var a = MixtureOfExpertsPlanner.Synthesize("hello", 8);
        var b = MixtureOfExpertsPlanner.Synthesize("hello", 8);
        a.ShouldBe(b);
        a.Sum().ShouldBe(1, 1e-9);
        var sorted = a.OrderDescending().ToArray();
        // A clear favourite, a runner-up and small rest.
        sorted[0].ShouldBeGreaterThan(sorted[1]);
        sorted[1].ShouldBeGreaterThan(sorted[2]);
        (sorted[0] + sorted[1]).ShouldBeGreaterThan(0.45);
        MixtureOfExpertsPlanner.Synthesize(" hello", 8).ShouldNotBe(a);
        MixtureOfExpertsPlanner.Fnv1a("a").ShouldBe(0xE40C292Cu);
        foreach (var e in new[] { 2, 3, 12 })
        {
            var s = MixtureOfExpertsPlanner.Synthesize("token", e);
            s.Length.ShouldBe(e);
            s.Sum().ShouldBe(1, 1e-9);
        }
    }

    [Fact]
    public void Active_Parameters_Are_Shared_Plus_K_Per_Expert()
    {
        var p = MixtureOfExpertsParameters.Default;
        p.Active(1).ShouldBe(18);
        p.Active(2).ShouldBe(30);
        MixtureOfExpertsParameters.Parse("671, 5, 16").Active(2).ShouldBe(37);
        MixtureOfExpertsParameters.Parse("100 2.5 4").ShouldBe(new MixtureOfExpertsParameters(100, 2.5, 4));
    }

    [Fact]
    public void Timeline_Follows_The_Measured_Beats()
    {
        MixtureOfExpertsTimeline.At(0, 15, false).ShouldBe(new MixtureOfExpertsMoment(0, 0, MixtureOfExpertsPhase.Entering));
        MixtureOfExpertsTimeline.At(0.3, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.ToRouter);
        MixtureOfExpertsTimeline.At(0.6, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.Scored);
        MixtureOfExpertsTimeline.At(0.95, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.ToExperts);
        MixtureOfExpertsTimeline.At(1.2, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.Lit);
        MixtureOfExpertsTimeline.At(1.52, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.ToMerge);
        MixtureOfExpertsTimeline.At(1.75, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.ToOutput);
        MixtureOfExpertsTimeline.At(1.99, 15, false).Phase.ShouldBe(MixtureOfExpertsPhase.Done);
        var second = MixtureOfExpertsTimeline.At(2.2 + 0.33, 15, false);
        second.Token.ShouldBe(1);
        second.Local.ShouldBe(0.33, 1e-9);
        MixtureOfExpertsTimeline.At(33, 15, false).IsSummary.ShouldBeTrue();
        MixtureOfExpertsTimeline.Length(15, false).ShouldBe(33, 1e-9);
        // A loop is 15 tokens and the 3.6 s summary.
        (MixtureOfExpertsTimeline.Length(15, false) + MixtureOfExpertsTimeline.SummaryHold).ShouldBe(36.6, 1e-9);
        // Loads count from the light-up beat; outputs from the done beat.
        MixtureOfExpertsTimeline.CountedTokens(MixtureOfExpertsTimeline.At(2.2 * 3 + 0.617, 15, false)).ShouldBe(3);
        MixtureOfExpertsTimeline.CountedTokens(MixtureOfExpertsTimeline.At(2.2 * 4 + 1.65, 15, false)).ShouldBe(5);
        MixtureOfExpertsTimeline.DoneTokens(MixtureOfExpertsTimeline.At(2.2 * 4 + 1.8, 15, false)).ShouldBe(4);
        MixtureOfExpertsTimeline.DoneTokens(MixtureOfExpertsTimeline.At(2.2 * 4 + 1.9, 15, false)).ShouldBe(5);
    }

    [Fact]
    public void Reduced_Timeline_Has_Three_Beats_Per_Token_And_No_Travel()
    {
        MixtureOfExpertsTimeline.At(0, 15, true).Phase.ShouldBe(MixtureOfExpertsPhase.Scored);
        MixtureOfExpertsTimeline.At(0.6, 15, true).Phase.ShouldBe(MixtureOfExpertsPhase.Lit);
        MixtureOfExpertsTimeline.At(1.1, 15, true).Phase.ShouldBe(MixtureOfExpertsPhase.Done);
        MixtureOfExpertsTimeline.At(1.8, 15, true).Token.ShouldBe(1);
        MixtureOfExpertsTimeline.Length(15, true).ShouldBe(27, 1e-9);
        MixtureOfExpertsTimeline.AreGatesLit(MixtureOfExpertsTimeline.At(0.7, 15, true), true).ShouldBeTrue();
        MixtureOfExpertsTimeline.AreGatesLit(MixtureOfExpertsTimeline.At(1.2, 15, true), true).ShouldBeFalse();
        MixtureOfExpertsTimeline.SendTime(true).ShouldBe(0.6);
        MixtureOfExpertsTimeline.SendTime(false).ShouldBe(0.76);
    }

    [Fact]
    public void Packets_Ease_In_And_Out_Cubically_And_Vanish_On_Arrival()
    {
        MixtureOfExpertsTimeline.EaseInOutCubic(0.25).ShouldBe(0.0625, 1e-12);
        MixtureOfExpertsTimeline.EaseInOutCubic(0.5).ShouldBe(0.5, 1e-12);
        MixtureOfExpertsTimeline.EaseInOutCubic(0.75).ShouldBe(0.9375, 1e-12);
        MixtureOfExpertsTimeline.Leg(0.1, 0.16, 0.48).ShouldBe(-1);
        MixtureOfExpertsTimeline.Leg(0.32, 0.16, 0.48).ShouldBe(0.5, 1e-9);
        MixtureOfExpertsTimeline.Leg(0.48, 0.16, 0.48).ShouldBe(-1);
    }

    [Fact]
    public void Wide_Layout_Matches_The_Measured_Stage()
    {
        var layout = MixtureOfExpertsLayout.Build(694, 8);
        layout.IsWide.ShouldBeTrue();
        layout.Height.ShouldBe(294);
        layout.InputSlot.Right.ShouldBe(83, 1);
        layout.Router.X.ShouldBe(136, 1);
        layout.Router.Right.ShouldBe(289, 1);
        layout.Router.Y.ShouldBe(22);
        layout.Router.Bottom.ShouldBe(294);
        layout.Tiles[0].ShouldBe(new Rect(layout.Tiles[0].X, 32, layout.Tiles[0].Width, 28));
        layout.Tiles[0].X.ShouldBe(353, 1);
        layout.Tiles[0].Right.ShouldBe(533, 1);
        layout.Tiles[7].Y.ShouldBe(32 + 7 * 32);
        layout.Joint.X.ShouldBe(581, 1);
        layout.Joint.Y.ShouldBe(158);
        layout.OutputSlot.X.ShouldBe(611, 1);
        layout.Tracks[0].X.ShouldBe(148, 1);
        layout.Tracks[0].Width.ShouldBe(93, 1);
        layout.Tracks[1].Center.Y.ShouldBe(78);
        layout.FlowIn.Length.ShouldBe(53, 1);
        layout.FlowOut.Length.ShouldBe(30, 1);
        layout.Gates[0].Length.ShouldBe(64, 1);
        // Merge curve lengths measured from the middle rows outward.
        layout.Curves[3].Length.ShouldBe(51.5, 0.6);
        layout.Curves[2].Length.ShouldBe(71.6, 0.6);
        layout.Curves[1].Length.ShouldBe(98.1, 0.6);
        layout.Curves[0].Length.ShouldBe(127.0, 0.6);
        // The router and tile widths stop at their minimums on a small wide card.
        var small = MixtureOfExpertsLayout.Build(520, 8);
        small.IsWide.ShouldBeTrue();
        small.Router.Width.ShouldBe(124);
        small.Tiles[0].Width.ShouldBe(148);
        MixtureOfExpertsLayout.Build(694, 12).Height.ShouldBe(422);
    }

    [Fact]
    public void Narrow_Layout_Stacks_The_Stage_Top_To_Bottom()
    {
        var layout = MixtureOfExpertsLayout.Build(342, 8);
        layout.IsWide.ShouldBeFalse();
        layout.Height.ShouldBe(286);
        layout.InputSlot.ShouldBe(new Rect(171 - 85.5, 0, 171, 28));
        MixtureOfExpertsLayout.Build(500, 8).InputSlot.Width.ShouldBe(180);
        layout.Router.ShouldBe(new Rect(0, 50, 342, 92));
        var column = (342 - 4 * 7) / 8.0;
        layout.Tiles[1].ShouldBe(new Rect(column + 4, 164, column, 52));
        layout.Tracks[0].Height.ShouldBe(50);
        layout.Gates[0].Length.ShouldBe(22, 0.01);
        layout.Joint.ShouldBe(new Point(171, 242));
        layout.OutputSlot.Y.ShouldBe(258);
        MixtureOfExpertsLayout.Build(517, 8).IsWide.ShouldBeFalse();
        MixtureOfExpertsLayout.Build(518, 8).IsWide.ShouldBeTrue();
    }
}
