namespace TKCode.Arc;

/// <summary>What a <see cref="MixtureOfExperts"/> is inspecting: nothing, a token or an expert.</summary>
internal enum MixtureOfExpertsInspectKind
{
    None,
    Token,
    Expert
}

/// <summary>The footer's left side: which line and note it shows.</summary>
internal enum MixtureOfExpertsFooterKind
{
    /// <summary>No tokens at all.</summary>
    Empty,

    /// <summary>The router is scoring the token.</summary>
    Scoring,

    /// <summary>The token's route (choices, weights, skips) and its note.</summary>
    Route,

    /// <summary>An inspected expert: its load and what it handles.</summary>
    Expert,

    /// <summary>Every token is routed.</summary>
    Summary
}

/// <summary>Looks of the drawn parts (see <see cref="MixtureOfExpertsScene"/>).</summary>
internal static class MixtureOfExpertsLooks
{
    // Gate lines and merge curves.
    public const int LineIdle = 0;
    public const int LineFirst = 1;
    public const int LineSecond = 2;
    public const int LineDropped = 3;

    // Router rows.
    public const int RowIdle = 0;
    public const int RowLive = 1;
    public const int RowFirst = 2;
    public const int RowSecond = 3;

    // Expert tiles.
    public const int TileNormal = 0;
    public const int TileFirst = 1;
    public const int TileSecond = 2;
    public const int TileDropped = 3;
    public const int TileInspected = 4;

    // Sequence tokens.
    public const int TokenNext = 0;
    public const int TokenCurrent = 1;
    public const int TokenDone = 2;
    public const int TokenInspected = 3;
    public const int TokenFirst = 4;
    public const int TokenSecond = 5;

    // Load bar segments.
    public const int LoadNormal = 0;
    public const int LoadFirst = 1;
    public const int LoadSecond = 2;
    public const int LoadInspected = 3;
}

/// <summary>
/// Everything the card shows at one moment, as targets: which chips sit in the slots, whose scores the router shows,
/// the look of every line, tile, sequence token and load segment, how many tokens are counted, and the footer. Computed
/// from the plan and the story moment (or what is inspected) only when one of them changes; the parts then fade to it.
/// </summary>
internal sealed class MixtureOfExpertsScene
{
    public MixtureOfExpertsScene(int experts, int tokens)
    {
        RowLooks = new int[experts];
        Gates = new int[experts];
        Curves = new int[experts];
        Tiles = new int[experts];
        LoadLooks = new int[experts];
        TokenLooks = new int[tokens];
        TokenDim = new bool[tokens];
    }

    /// <summary>The token in the input slot (−1: empty), and whether it is fresh (not yet sent to the router).</summary>
    public int InputToken { get; set; } = -1;

    public bool InputFresh { get; set; }

    /// <summary>The token whose output sits in the output slot (−1: empty).</summary>
    public int OutputToken { get; set; } = -1;

    /// <summary>True while the router shows its live look (scores of <see cref="RouterToken"/>, top k colored).</summary>
    public bool RouterLive { get; set; }

    /// <summary>The token whose scores the router shows (−1: all zero).</summary>
    public int RouterToken { get; set; } = -1;

    public int[] RowLooks { get; }

    public int[] Gates { get; }

    public int[] Curves { get; }

    /// <summary>True while a packet heads to the router (the input line is darker).</summary>
    public bool FlowInActive { get; set; }

    public int[] Tiles { get; }

    /// <summary>The tokens counted by the experts (the loads are the plan's loads after them).</summary>
    public int Counted { get; set; }

    public int[] LoadLooks { get; }

    public int[] TokenLooks { get; }

    public bool[] TokenDim { get; }

    /// <summary>True when the packets are drawn (not while inspecting, not in the summary).</summary>
    public bool PacketsVisible { get; set; }

    public MixtureOfExpertsFooterKind Footer { get; set; }

    /// <summary>The token the footer talks about (route / scoring), or the inspected expert (expert).</summary>
    public int FooterSubject { get; set; } = -1;

    /// <summary>
    /// Computes the scene for <paramref name="moment"/> of <paramref name="plan"/>, or for what is inspected: a token
    /// shows its whole route at once with the loads after it; an expert keeps the frozen stage but clears the choice
    /// colors and highlights the tokens it has taken so far.
    /// </summary>
    public static MixtureOfExpertsScene Compute(MixtureOfExpertsPlan plan, MixtureOfExpertsMoment moment, bool reduced,
        MixtureOfExpertsInspectKind inspect = MixtureOfExpertsInspectKind.None, int inspected = -1)
    {
        var experts = plan.ExpertCount;
        var tokens = plan.Routes.Length;
        var scene = new MixtureOfExpertsScene(experts, tokens);
        if (tokens == 0)
        {
            scene.Footer = MixtureOfExpertsFooterKind.Empty;
            return scene;
        }
        if (inspect == MixtureOfExpertsInspectKind.Token && inspected >= 0 && inspected < tokens)
        {
            ComputeInspectedToken(scene, plan, moment, inspected);
            return scene;
        }
        ComputeMoment(scene, plan, moment, reduced);
        if (inspect == MixtureOfExpertsInspectKind.Expert && inspected >= 0 && inspected < experts)
        {
            ApplyInspectedExpert(scene, plan, inspected);
        }
        return scene;
    }

    private static void ComputeMoment(MixtureOfExpertsScene scene, MixtureOfExpertsPlan plan, MixtureOfExpertsMoment m, bool reduced)
    {
        var tokens = plan.Routes.Length;
        var i = m.Token;
        scene.Counted = MixtureOfExpertsTimeline.CountedTokens(m);
        scene.OutputToken = MixtureOfExpertsTimeline.DoneTokens(m) - 1;
        if (m.IsSummary)
        {
            scene.RouterToken = tokens - 1;
            for (var t = 0; t < tokens; t++)
            {
                scene.TokenLooks[t] = MixtureOfExpertsLooks.TokenDone;
            }
            scene.Footer = MixtureOfExpertsFooterKind.Summary;
            return;
        }
        var route = plan.Routes[i];
        scene.InputToken = i;
        scene.InputFresh = !reduced && m.Phase < MixtureOfExpertsPhase.Scored;
        scene.FlowInActive = !reduced && m.Phase < MixtureOfExpertsPhase.Scored;
        scene.RouterLive = MixtureOfExpertsTimeline.IsScored(m);
        scene.RouterToken = scene.RouterLive ? i : i - 1;
        if (scene.RouterLive)
        {
            SetRows(scene, route);
        }
        if (MixtureOfExpertsTimeline.AreGatesLit(m, reduced))
        {
            SetGates(scene, route);
        }
        if (!reduced && MixtureOfExpertsTimeline.AreCurvesLit(m))
        {
            SetCurves(scene, route);
        }
        if (MixtureOfExpertsTimeline.IsLit(m))
        {
            SetTiles(scene, route);
            SetLoads(scene, route);
        }
        for (var t = 0; t < tokens; t++)
        {
            scene.TokenLooks[t] = t < i ? MixtureOfExpertsLooks.TokenDone
                : t > i ? MixtureOfExpertsLooks.TokenNext
                : m.Phase >= MixtureOfExpertsPhase.Done ? MixtureOfExpertsLooks.TokenDone
                : MixtureOfExpertsLooks.TokenCurrent;
        }
        scene.PacketsVisible = !reduced;
        scene.Footer = scene.RouterLive ? MixtureOfExpertsFooterKind.Route : MixtureOfExpertsFooterKind.Scoring;
        scene.FooterSubject = i;
    }

    private static void ComputeInspectedToken(MixtureOfExpertsScene scene, MixtureOfExpertsPlan plan, MixtureOfExpertsMoment m, int j)
    {
        var tokens = plan.Routes.Length;
        var route = plan.Routes[j];
        scene.InputToken = j;
        scene.InputFresh = true;
        scene.OutputToken = j;
        scene.RouterLive = true;
        scene.RouterToken = j;
        scene.Counted = j + 1;
        SetRows(scene, route);
        SetGates(scene, route);
        SetCurves(scene, route);
        SetTiles(scene, route);
        SetLoads(scene, route);
        for (var t = 0; t < tokens; t++)
        {
            scene.TokenLooks[t] = m.IsSummary || t < m.Token ? MixtureOfExpertsLooks.TokenDone
                : t > m.Token ? MixtureOfExpertsLooks.TokenNext
                : m.Phase >= MixtureOfExpertsPhase.Done ? MixtureOfExpertsLooks.TokenDone
                : MixtureOfExpertsLooks.TokenCurrent;
        }
        // The current token keeps its ring; the inspected one gets the strong border.
        if (scene.TokenLooks[j] != MixtureOfExpertsLooks.TokenCurrent || j == m.Token)
        {
            scene.TokenLooks[j] = MixtureOfExpertsLooks.TokenInspected;
        }
        scene.Footer = MixtureOfExpertsFooterKind.Route;
        scene.FooterSubject = j;
    }

    private static void ApplyInspectedExpert(MixtureOfExpertsScene scene, MixtureOfExpertsPlan plan, int e)
    {
        Array.Clear(scene.Gates);
        Array.Clear(scene.Curves);
        Array.Clear(scene.Tiles);
        Array.Clear(scene.LoadLooks);
        scene.Tiles[e] = MixtureOfExpertsLooks.TileInspected;
        scene.LoadLooks[e] = MixtureOfExpertsLooks.LoadInspected;
        scene.PacketsVisible = false;
        for (var t = 0; t < scene.TokenLooks.Length; t++)
        {
            var route = plan.Routes[t];
            var rank = route.RankOf(e);
            if (t < scene.Counted && rank >= 0 && !route.IsDropped(e))
            {
                scene.TokenLooks[t] = rank == 0 ? MixtureOfExpertsLooks.TokenFirst : MixtureOfExpertsLooks.TokenSecond;
                scene.TokenDim[t] = false;
            }
            else
            {
                scene.TokenDim[t] = true;
            }
        }
        scene.Footer = MixtureOfExpertsFooterKind.Expert;
        scene.FooterSubject = e;
    }

    private static void SetRows(MixtureOfExpertsScene scene, MixtureOfExpertsRoute route)
    {
        for (var e = 0; e < scene.RowLooks.Length; e++)
        {
            var rank = route.RankOf(e);
            scene.RowLooks[e] = rank switch
            {
                0 => MixtureOfExpertsLooks.RowFirst,
                1 => MixtureOfExpertsLooks.RowSecond,
                _ => MixtureOfExpertsLooks.RowLive
            };
        }
    }

    private static void SetGates(MixtureOfExpertsScene scene, MixtureOfExpertsRoute route)
    {
        for (var r = 0; r < route.Experts.Count; r++)
        {
            var e = route.Experts[r];
            scene.Gates[e] = route.IsDropped(e) ? MixtureOfExpertsLooks.LineDropped : r == 0 ? MixtureOfExpertsLooks.LineFirst : MixtureOfExpertsLooks.LineSecond;
        }
    }

    private static void SetCurves(MixtureOfExpertsScene scene, MixtureOfExpertsRoute route)
    {
        for (var r = 0; r < route.Experts.Count; r++)
        {
            var e = route.Experts[r];
            if (!route.IsDropped(e))
            {
                scene.Curves[e] = r == 0 ? MixtureOfExpertsLooks.LineFirst : MixtureOfExpertsLooks.LineSecond;
            }
        }
    }

    private static void SetTiles(MixtureOfExpertsScene scene, MixtureOfExpertsRoute route)
    {
        for (var r = 0; r < route.Experts.Count; r++)
        {
            var e = route.Experts[r];
            scene.Tiles[e] = route.IsDropped(e) ? MixtureOfExpertsLooks.TileDropped : r == 0 ? MixtureOfExpertsLooks.TileFirst : MixtureOfExpertsLooks.TileSecond;
        }
    }

    private static void SetLoads(MixtureOfExpertsScene scene, MixtureOfExpertsRoute route)
    {
        for (var r = 0; r < route.Experts.Count; r++)
        {
            var e = route.Experts[r];
            if (!route.IsDropped(e))
            {
                scene.LoadLooks[e] = r == 0 ? MixtureOfExpertsLooks.LoadFirst : MixtureOfExpertsLooks.LoadSecond;
            }
        }
    }
}
