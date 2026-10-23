namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="MixtureOfExperts"/> shows or speaks, in English by default. Replace the instance to translate
/// or reword; sentences with numbers or names are functions so the word order can change. Footer notes may contain
/// <c>{token}</c>, which is replaced by a small chip with the token's text. Numbers of parameters arrive formatted
/// ("30", "2.5").
/// </summary>
public sealed class MixtureOfExpertsLabels
{
    /// <summary>The first option of the top-k switch.</summary>
    public string TopOne { get; init; } = "Top 1";

    /// <summary>The second option of the top-k switch.</summary>
    public string TopTwo { get; init; } = "Top 2";

    /// <summary>The accessible name of the top-k switch.</summary>
    public string TopKGroup { get; init; } = "Experts per token";

    /// <summary>The label before the active-parameters readout.</summary>
    public string ActiveParameters { get; init; } = "Active parameters";

    /// <summary>The text after the readout; the argument is the total in billions.</summary>
    public Func<string, string> OfTotal { get; init; } = total => $"of {total}B";

    /// <summary>The readout as read aloud; the arguments are the active and the total parameters in billions.</summary>
    public Func<string, string, string> ParametersSpoken { get; init; } =
        (active, total) => $"{active}B of {total}B parameters active per token";

    /// <summary>The label over the input chip.</summary>
    public string Input { get; init; } = "Input";

    /// <summary>The label over the router.</summary>
    public string Router { get; init; } = "Router";

    /// <summary>The label over the expert tiles.</summary>
    public string Experts { get; init; } = "Experts";

    /// <summary>The label over the output chip.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The route line while the router scores a token; the argument is the number of experts.</summary>
    public Func<int, string> Scoring { get; init; } = experts => $"Rating all {experts} experts";

    /// <summary>The note while the router scores a token; the arguments are k and the number of experts.</summary>
    public Func<int, int, string> ScoringNote { get; init; } = (k, _) => k == 1
        ? "The router rates every expert and hands the token to the single best fit."
        : $"The router rates every expert and hands the token to the best {k}.";

    /// <summary>The word after an expert that was full and skipped the token.</summary>
    public string Full { get; init; } = "full";

    /// <summary>
    /// The note for a token two experts process; the arguments are the first and second expert and their weights in
    /// percent.
    /// </summary>
    public Func<string, string, int, int, string> NotePair { get; init; } =
        (first, second, a, b) => $"{first} and {second} both work on {{token}}, and the result mixes them {a} to {b}.";

    /// <summary>The note for a token one expert processes at top 1; the arguments are the expert and how many sit idle.</summary>
    public Func<string, int, string> NoteSingle { get; init; } =
        (expert, idle) => $"Only {expert} works on {{token}}; the other {idle} experts sit this one out.";

    /// <summary>The note when one chosen expert was full; the arguments are the full expert and the one that processed the token.</summary>
    public Func<string, string, string> NoteDropped { get; init; } =
        (full, kept) => $"{full} has no room left, so {kept} handles {{token}} alone.";

    /// <summary>The note when every chosen expert was full; the argument names them ("Marks", "Marks and Glue").</summary>
    public Func<string, string> NotePassThrough { get; init; } =
        experts => $"No room with {experts}, so {{token}} passes through this layer unchanged.";

    /// <summary>Joins two expert names in a sentence.</summary>
    public Func<string, string, string> And { get; init; } = (a, b) => $"{a} and {b}";

    /// <summary>The route line once every token is routed; the argument is the number of tokens.</summary>
    public Func<int, string> Summary { get; init; } = count => count == 1 ? "1 token routed" : $"{count} tokens routed";

    /// <summary>
    /// The note once every token is routed; the arguments are k, the number of experts, and the active and total
    /// parameters in billions.
    /// </summary>
    public Func<int, int, string, string, string> SummaryNote { get; init; } =
        (k, experts, active, total) => $"Each token woke {k} of {experts} experts, so {active}B of the {total}B parameters did the work.";

    /// <summary>The footer when there are no tokens.</summary>
    public string NoTokens { get; init; } = "There are no tokens to route.";

    /// <summary>
    /// An inspected expert's load; the arguments are the tokens it took and its capacity (null when nothing is ever
    /// dropped).
    /// </summary>
    public Func<int, int?, string> ExpertLoad { get; init; } = (count, capacity) => capacity is { } c
        ? $"{count} of {c} slots used"
        : count == 1 ? "1 token" : $"{count} tokens";

    /// <summary>The label of the load balance readout.</summary>
    public string LoadBalance { get; init; } = "Load balance";

    /// <summary>The load balance readout before anything is routed.</summary>
    public string NothingRouted { get; init; } = "Nothing routed yet";

    /// <summary>The load balance as read aloud; the arguments are the percentage, the number of experts and of routes.</summary>
    public Func<int, int, int, string> LoadBalanceSpoken { get; init; } =
        (percent, experts, routes) => $"Load balance {percent} percent across {experts} experts, {routes} routes";

    /// <summary>The accessible name of the token sequence.</summary>
    public string TokensGroup { get; init; } = "Tokens. Point at, focus or pin one to see its route.";

    /// <summary>The accessible name of the expert tiles.</summary>
    public string ExpertsGroup { get; init; } = "Experts";

    /// <summary>A sequence token's accessible name; the arguments are the token and its route as spoken.</summary>
    public Func<string, string, string> TokenSpoken { get; init; } = (token, route) => $"{token}: {route}";

    /// <summary>One choice in a spoken route; the arguments are the expert and its weight in percent.</summary>
    public Func<string, int, string> ChoiceSpoken { get; init; } = (expert, percent) => $"{expert} {percent}%";

    /// <summary>A skipped choice in a spoken route; the argument is the expert.</summary>
    public Func<string, string> DroppedSpoken { get; init; } = expert => $"{expert} full, skipped";

    /// <summary>
    /// An expert tile's accessible name; the arguments are the expert's name and the tokens it has taken.
    /// </summary>
    public Func<string, int, string> ExpertSpoken { get; init; } =
        (name, count) => count == 1 ? $"{name} expert, 1 token" : $"{name} expert, {count} tokens";

    /// <summary>Added to an expert tile's name when the expert is full.</summary>
    public string AtCapacity { get; init; } = "at capacity";

    /// <summary>Added to a lit expert's name; the argument is the token.</summary>
    public Func<string, string> FirstChoiceFor { get; init; } = token => $"first choice for {token}";

    /// <summary>Added to a lit expert's name; the argument is the token.</summary>
    public Func<string, string> SecondChoiceFor { get; init; } = token => $"second choice for {token}";

    /// <summary>Added to a full expert's name while it skips a token; the argument is the token.</summary>
    public Func<string, string> SkippedFor { get; init; } = token => $"skipped {token}";

    /// <summary>Spoken when the story pauses.</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>Spoken when the story plays on.</summary>
    public string Resumed { get; init; } = "Routing continues";

    /// <summary>Spoken when the story starts again from the first token.</summary>
    public string Restarted { get; init; } = "Starting over from the first token";

    /// <summary>Spoken when a token or an expert is pinned; the argument is its name.</summary>
    public Func<string, string> Pinned { get; init; } = name => $"{name} pinned. Press Escape to let routing continue.";

    /// <summary>Spoken when a pin is released and the story plays on.</summary>
    public string UnpinnedResumed { get; init; } = "Unpinned. Routing continues.";

    /// <summary>Spoken when a pin is released while the story stays paused.</summary>
    public string Unpinned { get; init; } = "Unpinned";

    /// <summary>
    /// Spoken when the top k changes; the arguments are k and the active and total parameters in billions.
    /// </summary>
    public Func<int, string, string, string> TopKChanged { get; init; } = (k, active, total) =>
        $"{(k == 1 ? "1 expert" : $"{k} experts")} per token: {active}B of {total}B parameters active";
}
