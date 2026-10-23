namespace TKCode.Arc;

/// <summary>Where one token is in its trip through the layer.</summary>
internal enum MixtureOfExpertsPhase
{
    /// <summary>The token is current and its chip slides in; the router still shows the previous scores, dimmed.</summary>
    Entering,

    /// <summary>A neutral packet travels from the input chip to the router.</summary>
    ToRouter,

    /// <summary>The router shows the token's scores and its top k.</summary>
    Scored,

    /// <summary>A packet in each choice's color travels to its expert.</summary>
    ToExperts,

    /// <summary>The chosen experts light up and their counters roll.</summary>
    Lit,

    /// <summary>The experts' outputs travel along the curves to the merge point.</summary>
    ToMerge,

    /// <summary>The merged output travels to the output chip.</summary>
    ToOutput,

    /// <summary>The output chip shows the token; the scene rests until the next token.</summary>
    Done,

    /// <summary>Every token is routed: the summary shows.</summary>
    Summary
}

/// <summary>A point in the story: the token being routed, the time since its start and its phase.</summary>
internal readonly record struct MixtureOfExpertsMoment(int Token, double Local, MixtureOfExpertsPhase Phase)
{
    public bool IsSummary => Phase == MixtureOfExpertsPhase.Summary;
}

/// <summary>
/// The story's clock, as pure functions of story time (seconds at speed 1). Each token takes 2.2 s: its chip enters at
/// +0, a neutral packet goes to the router from +0.16 to +0.48, the scores show at +0.48, packets in the choices' colors
/// go to the experts from +0.76 to +1.14, the experts light up and count at +1.14, the outputs curve to the merge point
/// from +1.38 to +1.66, the merged packet reaches the output from +1.66 to +1.84 and the output chip changes at +1.84.
/// Every packet eases in and out (cubic) along the arc length of its path. Under reduced motion a token takes 1.8 s with
/// no travel: scores and the route at +0, experts lit and counted at +0.6, output at +1.1. After the last token the
/// summary shows.
/// </summary>
internal static class MixtureOfExpertsTimeline
{
    public const double Period = 2.2;
    public const double ChipIn = 0.25;
    public const double RouterStart = 0.16;
    public const double RouterEnd = 0.48;
    public const double Scored = 0.48;
    public const double GateStart = 0.76;
    public const double GateEnd = 1.14;
    public const double Lit = 1.14;
    public const double MergeStart = 1.38;
    public const double MergeEnd = 1.66;
    public const double OutputStart = 1.66;
    public const double OutputEnd = 1.84;
    public const double Done = 1.84;

    /// <summary>How long the summary rests before a looping story starts again.</summary>
    public const double SummaryHold = 3.6;

    public const double ReducedPeriod = 1.8;
    public const double ReducedLit = 0.6;
    public const double ReducedDone = 1.1;

    /// <summary>A skipped expert's packet stops this far before the tile.</summary>
    public const double DropGap = 5;

    public static double PeriodOf(bool reduced) => reduced ? ReducedPeriod : Period;

    /// <summary>The story's length: one period per token (the summary starts there).</summary>
    public static double Length(int tokens, bool reduced) => Math.Max(0, tokens) * PeriodOf(reduced);

    /// <summary>The moment at story time <paramref name="time"/>.</summary>
    public static MixtureOfExpertsMoment At(double time, int tokens, bool reduced)
    {
        if (tokens <= 0)
        {
            return new MixtureOfExpertsMoment(-1, 0, MixtureOfExpertsPhase.Summary);
        }
        var period = PeriodOf(reduced);
        time = Math.Max(0, time);
        if (time >= tokens * period - 1e-9)
        {
            return new MixtureOfExpertsMoment(tokens - 1, time - (tokens - 1) * period, MixtureOfExpertsPhase.Summary);
        }
        var index = Math.Min(tokens - 1, (int)Math.Floor(time / period + 1e-9));
        var local = Math.Max(0, time - index * period);
        return new MixtureOfExpertsMoment(index, local, PhaseAt(local, reduced));
    }

    /// <summary>The phase <paramref name="local"/> seconds into a token.</summary>
    public static MixtureOfExpertsPhase PhaseAt(double local, bool reduced)
    {
        if (reduced)
        {
            return local < ReducedLit ? MixtureOfExpertsPhase.Scored
                : local < ReducedDone ? MixtureOfExpertsPhase.Lit
                : MixtureOfExpertsPhase.Done;
        }
        return local < RouterStart ? MixtureOfExpertsPhase.Entering
            : local < RouterEnd ? MixtureOfExpertsPhase.ToRouter
            : local < GateStart ? MixtureOfExpertsPhase.Scored
            : local < GateEnd ? MixtureOfExpertsPhase.ToExperts
            : local < MergeStart ? MixtureOfExpertsPhase.Lit
            : local < MergeEnd ? MixtureOfExpertsPhase.ToMerge
            : local < OutputEnd ? MixtureOfExpertsPhase.ToOutput
            : MixtureOfExpertsPhase.Done;
    }

    /// <summary>The time within a token when it leaves the router for its experts (<see cref="MixtureOfExperts.Routed"/>).</summary>
    public static double SendTime(bool reduced) => reduced ? ReducedLit : GateStart;

    /// <summary>True once the router shows the token's scores (and the footer its route).</summary>
    public static bool IsScored(MixtureOfExpertsMoment m) => m.Phase >= MixtureOfExpertsPhase.Scored;

    /// <summary>True while the chosen experts are lit (from the light-up beat to the next token).</summary>
    public static bool IsLit(MixtureOfExpertsMoment m) => m.Phase is >= MixtureOfExpertsPhase.Lit and < MixtureOfExpertsPhase.Summary;

    /// <summary>True while the gate lines of the choice are lit.</summary>
    public static bool AreGatesLit(MixtureOfExpertsMoment m, bool reduced)
        => reduced ? m.Phase == MixtureOfExpertsPhase.Lit : m.Phase is MixtureOfExpertsPhase.ToExperts or MixtureOfExpertsPhase.Lit;

    /// <summary>True while the merge curves of the choice are lit (full motion only).</summary>
    public static bool AreCurvesLit(MixtureOfExpertsMoment m) => m.Phase is MixtureOfExpertsPhase.ToMerge or MixtureOfExpertsPhase.ToOutput;

    /// <summary>The number of tokens whose experts have counted them (the loads shown).</summary>
    public static int CountedTokens(MixtureOfExpertsMoment m)
    {
        if (m.Token < 0)
        {
            return 0;
        }
        return m.IsSummary || IsLit(m) ? m.Token + 1 : m.Token;
    }

    /// <summary>The number of tokens whose output has reached the output chip.</summary>
    public static int DoneTokens(MixtureOfExpertsMoment m)
    {
        if (m.Token < 0)
        {
            return 0;
        }
        return m.Phase >= MixtureOfExpertsPhase.Done ? m.Token + 1 : m.Token;
    }

    /// <summary>The cubic ease in and out: 4u³ in the first half, 1 − (−2u + 2)³ / 2 in the second.</summary>
    public static double EaseInOutCubic(double u)
    {
        u = Math.Clamp(u, 0, 1);
        return u < 0.5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2;
    }

    /// <summary>
    /// The eased progress (0..1) of a packet that travels from <paramref name="start"/> to <paramref name="end"/>
    /// seconds into a token, or −1 when it is not on its way (it disappears on arrival).
    /// </summary>
    public static double Leg(double local, double start, double end)
    {
        if (local < start || local >= end)
        {
            return -1;
        }
        return EaseInOutCubic((local - start) / (end - start));
    }
}
