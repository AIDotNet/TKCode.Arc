namespace TKCode.Arc;

/// <summary>
/// The seeded random numbers of the simulated draft model: a Weyl sequence through the murmur3 finalizer (a small
/// splitmix-style generator). The same seed always gives the same numbers on every platform.
/// </summary>
internal struct DecodeRandom
{
    private uint _state;

    public DecodeRandom(int seed)
    {
        _state = unchecked((uint)seed);
    }

    /// <summary>The next number in [0, 1).</summary>
    public double Next()
    {
        unchecked
        {
            _state += 0x9E3779B9u;
            var z = _state;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            z ^= z >> 16;
            return z / 4294967296.0;
        }
    }
}

/// <summary>One planned round: what the draft proposed, how many guesses the target accepted and what it wrote.</summary>
internal sealed class DecodePlanRound
{
    public DecodePlanRound(int index, int start, string[] drafts, int accepted, string target)
    {
        Index = index;
        Start = start;
        Drafts = drafts;
        Accepted = accepted;
        Target = target;
    }

    /// <summary>The round's position, from 0.</summary>
    public int Index { get; }

    /// <summary>The answer index of the first token this round writes.</summary>
    public int Start { get; }

    /// <summary>The draft chips, in order (after the first miss they show the real tokens, which are discarded anyway).</summary>
    public string[] Drafts { get; }

    /// <summary>The number of drafts k.</summary>
    public int Count => Drafts.Length;

    /// <summary>Guesses accepted from the start, a (0..k).</summary>
    public int Accepted { get; }

    /// <summary>The token the target writes: the correction of guess a, or a bonus token when all k were accepted.</summary>
    public string Target { get; }

    /// <summary>True when every guess was accepted and the target adds a bonus token.</summary>
    public bool IsBonus => Accepted == Drafts.Length;

    /// <summary>The rejected guess (null when every guess was accepted).</summary>
    public string? Missed => Accepted < Drafts.Length ? Drafts[Accepted] : null;

    /// <summary>Tokens this round writes into the answer (a + 1).</summary>
    public int Written => Accepted + 1;
}

/// <summary>A whole run: its rounds and the answer they write, token by token, with the model that wrote each one.</summary>
internal sealed class DecodePlan
{
    public static readonly DecodePlan Empty = new(Array.Empty<DecodePlanRound>());

    public DecodePlan(IReadOnlyList<DecodePlanRound> rounds)
    {
        Rounds = rounds;
        var tokens = new List<string>();
        var byDraft = new List<bool>();
        foreach (var round in rounds)
        {
            for (var j = 0; j < round.Accepted; j++)
            {
                tokens.Add(round.Drafts[j]);
                byDraft.Add(true);
            }
            tokens.Add(round.Target);
            byDraft.Add(false);
            Drafted += round.Count;
            Accepted += round.Accepted;
        }
        Tokens = tokens;
        ByDraft = byDraft;
    }

    /// <summary>The rounds in order.</summary>
    public IReadOnlyList<DecodePlanRound> Rounds { get; }

    /// <summary>The answer, one entry per token (with leading spaces).</summary>
    public IReadOnlyList<string> Tokens { get; }

    /// <summary>For each answer token, true when the draft model wrote it (an accepted guess), false for the target.</summary>
    public IReadOnlyList<bool> ByDraft { get; }

    /// <summary>Target passes (one per round).</summary>
    public int Passes => Rounds.Count;

    /// <summary>Tokens drafted over the run.</summary>
    public int Drafted { get; }

    /// <summary>Drafts accepted over the run.</summary>
    public int Accepted { get; }

    /// <summary>The draft length the ready state shows: the longest round (at least 1).</summary>
    public int Width
    {
        get
        {
            var width = 1;
            foreach (var round in Rounds)
            {
                width = Math.Max(width, round.Count);
            }
            return width;
        }
    }

    /// <summary>The round that writes answer token <paramref name="token"/>.</summary>
    public int RoundOf(int token)
    {
        for (var r = Rounds.Count - 1; r >= 0; r--)
        {
            if (Rounds[r].Start <= token)
            {
                return r;
            }
        }
        return 0;
    }

    /// <summary>The totals after the first <paramref name="rounds"/> rounds (null before the first).</summary>
    public DecodeStats? StatsAfter(int rounds, double draftCost)
    {
        rounds = Math.Min(rounds, Rounds.Count);
        if (rounds <= 0)
        {
            return null;
        }
        int drafted = 0, accepted = 0, tokens = 0;
        for (var r = 0; r < rounds; r++)
        {
            drafted += Rounds[r].Count;
            accepted += Rounds[r].Accepted;
            tokens += Rounds[r].Written;
        }
        return DecodePlanner.Stats(rounds, drafted, accepted, tokens, draftCost);
    }

    /// <summary>The totals of the whole run (null for an empty run).</summary>
    public DecodeStats? Total(double draftCost) => StatsAfter(Rounds.Count, draftCost);
}

/// <summary>
/// Plans a speculative run, independent of time. A simulated run walks the answer: each round the draft proposes
/// k = min(draft length, tokens left − 1) tokens, so the last token is always the target's; one seeded draw per guess
/// decides whether the draft got it (draw &lt; ease), and drawing stops at the first miss (the guesses after it are shown
/// with the real tokens and discarded). The target writes the token at the first miss, or a bonus token after k hits.
/// A replayed run takes the recorded rounds as they are.
/// </summary>
internal static class DecodePlanner
{
    /// <summary>What a missed guess shows when the token has no <see cref="DecodeToken.Miss"/>.</summary>
    public const string MissPlaceholder = "…";

    /// <summary>Plans a simulated run of <paramref name="tokens"/> with drafts of <paramref name="draftLength"/>.</summary>
    public static DecodePlan Simulate(IReadOnlyList<DecodeToken> tokens, int draftLength, int seed, double defaultEase)
    {
        var count = tokens.Count;
        if (count == 0)
        {
            return DecodePlan.Empty;
        }
        draftLength = Math.Max(1, draftLength);
        var random = new DecodeRandom(seed);
        var rounds = new List<DecodePlanRound>();
        var i = 0;
        while (i < count)
        {
            var k = Math.Min(draftLength, count - 1 - i);
            var drafts = new string[k];
            var accepted = 0;
            var missed = false;
            for (var j = 0; j < k; j++)
            {
                var token = tokens[i + j];
                var text = token.Text ?? string.Empty;
                if (missed)
                {
                    drafts[j] = text;
                    continue;
                }
                var ease = token.Ease is { } e && double.IsFinite(e) ? Math.Clamp(e, 0, 1) : defaultEase;
                if (random.Next() < ease)
                {
                    drafts[j] = text;
                    accepted++;
                }
                else
                {
                    drafts[j] = token.Miss ?? (text.StartsWith(' ') ? " " + MissPlaceholder : MissPlaceholder);
                    missed = true;
                }
            }
            var target = tokens[i + accepted].Text ?? string.Empty;
            rounds.Add(new DecodePlanRound(rounds.Count, i, drafts, accepted, target));
            i += accepted + 1;
        }
        return new DecodePlan(rounds);
    }

    /// <summary>A replayed run from recorded rounds (accepted counts are clamped to the drafts).</summary>
    public static DecodePlan Replay(IReadOnlyList<DecodeRound> recorded)
    {
        var rounds = new List<DecodePlanRound>(recorded.Count);
        var start = 0;
        foreach (var round in recorded)
        {
            var drafts = new string[round.Drafts.Count];
            for (var j = 0; j < drafts.Length; j++)
            {
                drafts[j] = round.Drafts[j] ?? string.Empty;
            }
            var accepted = Math.Clamp(round.Accepted, 0, drafts.Length);
            rounds.Add(new DecodePlanRound(rounds.Count, start, drafts, accepted, round.TargetToken ?? string.Empty));
            start += accepted + 1;
        }
        return new DecodePlan(rounds);
    }

    /// <summary>The totals from raw counts: acceptance = accepted / drafted, speedup = tokens / (passes + cost × drafted).</summary>
    public static DecodeStats Stats(int passes, int drafted, int accepted, int tokens, double draftCost)
    {
        var cost = passes + Math.Max(0, draftCost) * drafted;
        return new DecodeStats
        {
            Passes = passes,
            Drafted = drafted,
            Accepted = accepted,
            Tokens = tokens,
            Acceptance = drafted > 0 ? (double)accepted / drafted : 0,
            TokensPerPass = passes > 0 ? (double)tokens / passes : 0,
            Speedup = cost > 0 ? tokens / cost : 0
        };
    }
}
