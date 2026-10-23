using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentLoop"/> shows or announces, in English by default (the playback buttons are named by
/// <see cref="Explainer.PlaybackLabels"/>). Replace the instance to translate or reword; texts with numbers or names are
/// functions so that the word order can change.
/// </summary>
public sealed class AgentLoopLabels
{
    /// <summary>The top station's name.</summary>
    public string Gather { get; set; } = "Gather context";

    /// <summary>The lower right station's name.</summary>
    public string Act { get; set; } = "Take action";

    /// <summary>The lower left station's name.</summary>
    public string Verify { get; set; } = "Verify";

    /// <summary>The status before the first beat.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The status while the run plays or steps.</summary>
    public string Running { get; set; } = "Running";

    /// <summary>The status while paused.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The status once the last check passed.</summary>
    public string Done { get; set; } = "Done";

    /// <summary>The status once the last check failed.</summary>
    public string Stopped { get; set; } = "Stopped";

    /// <summary>
    /// The loop counter at the center; the argument is the one-based loop. The number inside it rolls when the loop
    /// changes while the words around it stay.
    /// </summary>
    public Func<int, string> LoopCounter { get; set; } = loop => $"Loop {loop}";

    /// <summary>The center line once done; the argument is the number of loops.</summary>
    public Func<int, string> DoneIn { get; set; } = loops => loops == 1 ? "Done in 1 loop" : $"Done in {loops} loops";

    /// <summary>The shorter center line once done, on narrow cards; the argument is the number of loops.</summary>
    public Func<int, string> DoneInShort { get; set; } = loops => loops == 1 ? "1 loop" : $"{loops} loops";

    /// <summary>The center line once stopped; the argument is the number of loops.</summary>
    public Func<int, string> StoppedAfter { get; set; } = loops => loops == 1 ? "Stopped after 1 loop" : $"Stopped after {loops} loops";

    /// <summary>The shorter center line once stopped, on narrow cards; the argument is the number of loops.</summary>
    public Func<int, string> StoppedAfterShort { get; set; } = loops => loops == 1 ? "1 loop" : $"{loops} loops";

    /// <summary>The empty check lane.</summary>
    public string ChecksPlaceholder { get; set; } = "Checks land here";

    /// <summary>The empty tool call lane.</summary>
    public string CallsPlaceholder { get; set; } = "Tool calls land here";

    /// <summary>The single empty lane on narrow cards.</summary>
    public string LanePlaceholder { get; set; } = "Tool calls and checks land here";

    /// <summary>The heading of the context stack.</summary>
    public string Context { get; set; } = "Context";

    /// <summary>The context total beside the heading; the argument is the formatted count (such as "7.2K").</summary>
    public Func<string, string> Tokens { get; set; } = count => $"{count} tokens";

    /// <summary>The word read after a block's token count.</summary>
    public string TokensUnit { get; set; } = "tokens";

    /// <summary>The empty context stack.</summary>
    public string ContextPlaceholder { get; set; } = "Fills as the agent gathers context";

    /// <summary>The accessible name of the context list.</summary>
    public string ContextList { get; set; } = "Context blocks";

    /// <summary>A running call's second line; the argument is the tool.</summary>
    public Func<string, string> Calling { get; set; } = tool => $"Calling {tool}";

    /// <summary>What a screen reader hears before a call's result; the argument is the tool.</summary>
    public Func<string, string> Returned { get; set; } = tool => $"{tool} returned:";

    /// <summary>What a screen reader hears before a failed call's reason; the argument is the tool.</summary>
    public Func<string, string> Failed { get; set; } = tool => $"{tool} failed:";

    /// <summary>The title of a passed check.</summary>
    public string CheckPassed { get; set; } = "Check passed";

    /// <summary>The title of a failed check.</summary>
    public string Retry { get; set; } = "Retry";

    /// <summary>The words "Speed" before the speed control (also its accessible name).</summary>
    public string Speed { get; set; } = "Speed";

    /// <summary>Announced when a block joins while the reader steps or pauses; the arguments are the one-based loop and the block.</summary>
    public Func<int, string, string> GatheredAnnouncement { get; set; } = (loop, label) => $"Loop {loop}: added {label} to the context";

    /// <summary>Announced when the reader pauses during a call; the arguments are the tool and the action.</summary>
    public Func<string, string, string> CallingAnnouncement { get; set; } = (tool, action) => $"Calling {tool}: {action}";

    /// <summary>Announced for a result; the arguments are the tool and the result.</summary>
    public Func<string, string, string> ReturnedAnnouncement { get; set; } = (tool, result) => $"{tool} returned: {result}";

    /// <summary>Announced for a failed call; the arguments are the tool and the reason.</summary>
    public Func<string, string, string> FailedAnnouncement { get; set; } = (tool, result) => $"{tool} failed: {result}";

    /// <summary>Announced for a passed check; the argument is its note.</summary>
    public Func<string, string> PassedAnnouncement { get; set; } = note => $"Check passed: {note}";

    /// <summary>Announced for a failed check; the argument is its note.</summary>
    public Func<string, string> RetryAnnouncement { get; set; } = note => $"Check failed: {note}. Going round again";

    /// <summary>The summary when none is set and the last check passed; the arguments are the loops and the tool calls.</summary>
    public Func<int, int, string> DoneSummary { get; set; } = (loops, calls) =>
        $"Finished in {Count(loops, "loop")} with {Count(calls, "tool call")}.";

    /// <summary>The summary when none is set and the last check failed; the arguments are the loops and the tool calls.</summary>
    public Func<int, int, string> StoppedSummary { get; set; } = (loops, calls) =>
        $"Stopped after {Count(loops, "loop")} and {Count(calls, "tool call")}: the last check did not pass.";

    /// <summary>Token counts as shown: below 1,000 as a whole number, from 1,000 with one decimal and a K ("1.4K").</summary>
    public Func<double, string> FormatTokens { get; set; } = FormatCompact;

    /// <summary>The default token format: "993", "1.0K", "15.8K", "1.2M".</summary>
    public static string FormatCompact(double tokens)
    {
        var value = Math.Max(0, tokens);
        if (value < 999.5)
        {
            return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
        }
        if (value < 999_950)
        {
            return (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "K";
        }
        return (value / 1_000_000).ToString("0.0", CultureInfo.InvariantCulture) + "M";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
