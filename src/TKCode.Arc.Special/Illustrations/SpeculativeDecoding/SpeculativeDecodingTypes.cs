using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Whether a <see cref="SpeculativeDecoding"/> card can run, is waiting for its data or failed to get it.</summary>
public enum SpeculativeDecodingStatus
{
    /// <summary>The run is ready to play.</summary>
    Ready,

    /// <summary>The data is on its way: the lanes dim, the status line shimmers and the controls are disabled.</summary>
    Loading,

    /// <summary>The data could not be loaded: the status line shows the error and, when someone handles it, a retry button.</summary>
    Error
}

/// <summary>
/// One token of the answer the target model writes, for the simulated run of a <see cref="SpeculativeDecoding"/> card.
/// The text keeps its leading space, as tokenizers do (" deploy"); a token without one (punctuation) stays on the line of
/// the word before it.
/// </summary>
public sealed class DecodeToken
{
    /// <summary>Creates an empty token (for XAML).</summary>
    public DecodeToken()
    {
    }

    /// <summary>Creates a token with its text, how easy it is to guess and what the draft guesses instead.</summary>
    public DecodeToken(string text, double? ease = null, string? miss = null)
    {
        Text = text;
        Ease = ease;
        Miss = miss;
    }

    /// <summary>The token's text, with its leading space.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// How likely the draft model guesses this token, 0 to 1; null uses <see cref="SpeculativeDecoding.DefaultEase"/>
    /// (0.9).
    /// </summary>
    public double? Ease { get; set; }

    /// <summary>What the draft model proposes when it misses this token; null shows an ellipsis.</summary>
    public string? Miss { get; set; }
}

/// <summary>
/// One recorded round of a speculative run, for <see cref="SpeculativeDecoding.Rounds"/>: the tokens the draft model
/// proposed, how many of them the target accepted from the start, and the token the target wrote itself (the correction
/// of the first rejected guess, or a bonus token when every guess was accepted).
/// </summary>
public sealed class DecodeRound
{
    /// <summary>Creates an empty round (for XAML).</summary>
    public DecodeRound()
    {
    }

    /// <summary>Creates a round from its drafts, the number accepted and the target's token.</summary>
    public DecodeRound(IEnumerable<string> drafts, int accepted, string targetToken)
    {
        Drafts.AddRange(drafts);
        Accepted = accepted;
        TargetToken = targetToken;
    }

    /// <summary>The tokens the draft model proposed, in order, each with its leading space.</summary>
    public AvaloniaList<string> Drafts { get; } = new();

    /// <summary>How many drafts the target accepted from the start (clamped to the number of drafts).</summary>
    public int Accepted { get; set; }

    /// <summary>The token the target model wrote itself, with its leading space.</summary>
    public string TargetToken { get; set; } = string.Empty;
}

/// <summary>
/// The totals of a speculative run (or of its rounds so far). The speedup counts the draft model's time:
/// tokens / (passes + draft cost × drafted).
/// </summary>
public sealed record DecodeStats
{
    /// <summary>Target passes, one per round.</summary>
    public int Passes { get; init; }

    /// <summary>Tokens the draft model proposed.</summary>
    public int Drafted { get; init; }

    /// <summary>Drafted tokens the target accepted.</summary>
    public int Accepted { get; init; }

    /// <summary>Tokens written into the answer.</summary>
    public int Tokens { get; init; }

    /// <summary>The share of drafted tokens that were accepted, 0 to 1 (0 when nothing was drafted).</summary>
    public double Acceptance { get; init; }

    /// <summary>Answer tokens per target pass.</summary>
    public double TokensPerPass { get; init; }

    /// <summary>How many times faster than the target alone (one token per pass), counting the draft model's time.</summary>
    public double Speedup { get; init; }
}

/// <summary>Arguments of <see cref="SpeculativeDecoding.Completed"/>: the totals of the run that has just finished.</summary>
public sealed class SpeculativeDecodingCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpeculativeDecodingCompletedEventArgs(RoutedEvent routedEvent, object? source, DecodeStats stats)
        : base(routedEvent, source)
    {
        Stats = stats;
    }

    /// <summary>The totals of the finished run.</summary>
    public DecodeStats Stats { get; }
}
