using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SpeculativeDecoding"/> shows or announces, apart from the title, the lane names
/// (<see cref="SpeculativeDecoding.DraftLabel"/> and the others) and the playback buttons
/// (<see cref="Explainer.PlaybackLabels"/>). English by default; replace the instance to translate or reword. Texts with
/// numbers are functions so that word order can change.
/// </summary>
public sealed class SpeculativeDecodingLabels
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The status line before the run starts; the argument is the draft length.</summary>
    public Func<int, string> Ready { get; init; } = n => $"Guess {n} tokens cheaply, then check them in one target pass";

    /// <summary>The status line while the draft model proposes; the argument is the number of drafts this round.</summary>
    public Func<int, string> Drafting { get; init; } = k => k == 1 ? "Draft model guesses the next token" : $"Draft model guesses the next {k} tokens";

    /// <summary>The status line while the target checks the drafts; the argument is the number of drafts.</summary>
    public Func<int, string> Verifying { get; init; } = k => k == 1 ? "Target model checks the guess in one pass" : $"Target model checks all {k} guesses in one pass";

    /// <summary>The status line of a round without drafts (one token left, which the target writes).</summary>
    public string LastToken { get; init; } = "One token left, the target writes it";

    /// <summary>The status line after a round kept some guesses; the arguments are the kept and the drafted count.</summary>
    public Func<int, int, string> Kept { get; init; } = (a, k) => $"{a} of {k} kept, the target corrects the {Ordinal(a + 1)}";

    /// <summary>The status line after a round whose first guess was rejected.</summary>
    public string FirstMissed { get; init; } = "First guess missed, the target writes its own token";

    /// <summary>The status line after a round kept every guess; the argument is the number of drafts.</summary>
    public Func<int, string> AllKept { get; init; } = k => k == 1 ? "The guess is kept, and the target adds one more" : $"All {k} kept, and the target adds one more";

    /// <summary>The status line at the end; the argument is the number of target passes.</summary>
    public Func<int, string> Done { get; init; } = passes => $"Finished in {Passes(passes)}";

    /// <summary>The status line when the speculative line is done and the target-only line still writes; the argument is the passes.</summary>
    public Func<int, string> SpeculationDone { get; init; } = passes => $"Drafting finished in {Passes(passes)}, the target alone is still writing";

    /// <summary>The status line when both lines are done; the arguments are the speculative passes and the target-only passes.</summary>
    public Func<int, int, string> CompareDone { get; init; } = (passes, alone) => $"{passes} passes with drafting, {alone} without";

    /// <summary>The status line while <see cref="SpeculativeDecoding.Status"/> is Loading.</summary>
    public string Loading { get; init; } = "Loading the run…";

    /// <summary>The status line in the error state when <see cref="SpeculativeDecoding.ErrorText"/> is empty.</summary>
    public string Error { get; init; } = "The run could not be loaded";

    /// <summary>The retry button in the error state.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The name of the answer line.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The name of the answer line while the target-only line is shown.</summary>
    public string Speculative { get; init; } = "Speculative";

    /// <summary>The name of the target-only line.</summary>
    public string TargetOnly { get; init; } = "Target only";

    /// <summary>The pass count under a line's name; the argument is the number of passes.</summary>
    public Func<int, string> PassCount { get; init; } = n => n == 1 ? "1 pass" : $"{n} passes";

    /// <summary>The first summary line in the lanes at the end; the arguments are the drafted and the accepted count.</summary>
    public Func<int, int, string> DraftSummary { get; init; } = (drafted, accepted) => $"{accepted} of {drafted} drafted tokens kept";

    /// <summary>The second summary line in the lanes at the end; the arguments are the passes and the answer's tokens.</summary>
    public Func<int, int, string> PassSummary { get; init; } = (passes, tokens) => $"{tokens} tokens in {Passes(passes)}";

    /// <summary>The label of the speedup statistic.</summary>
    public string Speedup { get; init; } = "Speedup";

    /// <summary>The label of the acceptance statistic.</summary>
    public string Acceptance { get; init; } = "Drafts accepted";

    /// <summary>The label of the tokens-per-pass statistic.</summary>
    public string TokensPerPass { get; init; } = "Tokens per pass";

    /// <summary>The spoken speedup; the argument is the speedup rounded to one decimal.</summary>
    public Func<double, string> SpeedupSpoken { get; init; } = x => $"{x.ToString("0.0", En)} times";

    /// <summary>The spoken acceptance; the argument is the percentage.</summary>
    public Func<int, string> AcceptanceSpoken { get; init; } = p => $"{p} percent";

    /// <summary>The spoken tokens per pass; the argument is the value rounded to one decimal.</summary>
    public Func<double, string> TokensPerPassSpoken { get; init; } = x => x.ToString("0.0", En);

    /// <summary>The spoken value of a statistic before the first round has settled.</summary>
    public string NotYet { get; init; } = "not yet";

    /// <summary>The label of the draft length slider.</summary>
    public string DraftLength { get; init; } = "Draft length";

    /// <summary>The slider's readout; the argument is the draft length.</summary>
    public Func<int, string> DraftLengthValue { get; init; } = n => n == 1 ? "1 token" : $"{n} tokens";

    /// <summary>The compare switch.</summary>
    public string Compare { get; init; } = "Compare with the target alone";

    /// <summary>The accessible name of the answer line; the argument is the text written so far (empty before the first token).</summary>
    public Func<string, string> OutputName { get; init; } = text => text.Length == 0 ? "Output: nothing yet" : $"Output: {text}";

    /// <summary>The accessible name of the target-only line; the argument is the text written so far.</summary>
    public Func<string, string> TargetOnlyName { get; init; } = text => text.Length == 0 ? "Target only: nothing yet" : $"Target only: {text}";

    /// <summary>
    /// The hidden summary of a round that corrected a guess; the arguments are the round number, the drafts (comma
    /// separated), the accepted count, the target's token and the rejected guess.
    /// </summary>
    public Func<int, string, int, string, string, string> RoundCorrected { get; init; } =
        (round, drafts, accepted, wrote, instead) => $"Round {round}. Draft proposed {drafts}. Target kept {accepted} and wrote {wrote} instead of {instead}.";

    /// <summary>The hidden summary of a round that kept every guess; the arguments are the round number, the drafts, their count and the bonus token.</summary>
    public Func<int, string, int, string, string> RoundAllKept { get; init; } =
        (round, drafts, kept, added) => $"Round {round}. Draft proposed {drafts}. Target kept all {kept} and added {added}.";

    /// <summary>The hidden summary of a round without drafts; the arguments are the round number and the target's token.</summary>
    public Func<int, string, string> RoundTargetOnly { get; init; } = (round, wrote) => $"Round {round}. Target wrote {wrote}.";

    /// <summary>Announced when playback resumes.</summary>
    public string Playing { get; init; } = "Playing";

    /// <summary>Announced when playback pauses.</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>Announced when the run restarts.</summary>
    public string Restarted { get; init; } = "Restarted";

    /// <summary>Announced when a finished run plays again.</summary>
    public string PlayingFromStart { get; init; } = "Playing from the start";

    /// <summary>
    /// Announced when the draft length changes; the arguments are the length, the expected acceptance in percent and the
    /// expected speedup (rounded to one decimal).
    /// </summary>
    public Func<int, int, double, string> DraftLengthChanged { get; init; } =
        (n, percent, speedup) => $"Draft length {n}. {percent} percent of guesses kept, {speedup.ToString("0.0", En)} times faster.";

    /// <summary>Announced when the target-only line is turned on.</summary>
    public string CompareOn { get; init; } = "Racing the target alone, one token per pass.";

    /// <summary>Announced when the target-only line is turned off.</summary>
    public string CompareOff { get; init; } = "Comparison off.";

    /// <summary>Announced at the end; the arguments are the tokens, the passes and the speedup (rounded to one decimal).</summary>
    public Func<int, int, double, string> Finished { get; init; } =
        (tokens, passes, speedup) => $"Finished. {tokens} tokens in {Passes(passes)}, {speedup.ToString("0.0", En)} times the speed of the target alone.";

    /// <summary>Announced when the target-only line is done; the argument is its number of passes.</summary>
    public Func<int, string> TargetOnlyFinished { get; init; } = n => $"The target alone needed {n} passes.";

    /// <summary>English ordinals for the status line ("first" to "eighth", then "9th").</summary>
    public static string Ordinal(int n) => n switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "eighth",
        _ => n.ToString(En) + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" })
    };

    private static string Passes(int n) => n == 1 ? "1 target pass" : $"{n} target passes";
}
