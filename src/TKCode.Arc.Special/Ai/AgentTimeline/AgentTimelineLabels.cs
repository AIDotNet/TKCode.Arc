using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AgentTimeline"/> shows or speaks, with English defaults. Sentences with numbers are format
/// delegates; numbers reach them already rounded (times in seconds to one decimal, speeds as given). Replace the whole
/// object to translate the timeline.
/// </summary>
public sealed class AgentTimelineLabels
{
    /// <summary>The heading when neither <see cref="AgentTimeline.Title"/> nor the run's title is set.</summary>
    public string DefaultTitle { get; set; } = "Agent run";

    /// <summary>The status word while the replay plays (it shimmers).</summary>
    public string Running { get; set; } = "Running";

    /// <summary>The status word while the replay is paused part way.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The status word at the start of the run.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The status word while a live run is followed.</summary>
    public string Live { get; set; } = "Live";

    /// <summary>The thinking kind (legend, span card, accessible names).</summary>
    public string Thinking { get; set; } = "Thinking";

    /// <summary>The tool call kind (legend, accessible names).</summary>
    public string ToolCall { get; set; } = "Tool call";

    /// <summary>The waiting kind (legend, span card, accessible names).</summary>
    public string Waiting { get; set; } = "Waiting";

    /// <summary>The handoff entry of the legend.</summary>
    public string Handoff { get; set; } = "Handoff";

    /// <summary>The failed entry of the legend, and the word in a failed span's accessible name.</summary>
    public string Failed { get; set; } = "Failed";

    /// <summary>The unit after the token counter in the status line.</summary>
    public string TokensUnit { get; set; } = "tokens";

    /// <summary>Read before the summary by screen readers ("Finished: 5 agents, …").</summary>
    public string FinishedPrefix { get; set; } = "Finished:";

    /// <summary>The play button while paused.</summary>
    public string Play { get; set; } = "Play";

    /// <summary>The play button while playing.</summary>
    public string Pause { get; set; } = "Pause";

    /// <summary>The play button once the run has finished.</summary>
    public string Replay { get; set; } = "Replay";

    /// <summary>The restart button.</summary>
    public string Restart { get; set; } = "Restart";

    /// <summary>The speed control's accessible name.</summary>
    public string PlaybackSpeed { get; set; } = "Playback speed";

    /// <summary>The legend's accessible name.</summary>
    public string Legend { get; set; } = "Legend";

    /// <summary>The time scrubber's accessible name.</summary>
    public string RunTime { get; set; } = "Run time";

    /// <summary>The spans' accessible name as a group.</summary>
    public string Spans { get; set; } = "Run timeline";

    /// <summary>A time: 26.4 → "26.4s" (one decimal; "1m 05.3s" from a minute).</summary>
    public Func<double, string> Time { get; set; } = AgentTimelineMath.Seconds;

    /// <summary>A playback speed option: 2 → "2x".</summary>
    public Func<double, string> SpeedOption { get; set; } = speed => AgentTimelineMath.Speed(speed) + "x";

    /// <summary>A cost in US dollars: 0.151 → "$0.15".</summary>
    public Func<double, string> Cost { get; set; } = AgentTimelineMath.Cost;

    /// <summary>A token count in the card: 2050 → "2,050 tokens".</summary>
    public Func<long, string> Tokens { get; set; } = tokens => AgentTimelineMath.Tokens(tokens) + (tokens == 1 ? " token" : " tokens");

    /// <summary>The agent count of the summary: 5 → "5 agents".</summary>
    public Func<int, string> Agents { get; set; } = count => count == 1 ? "1 agent" : $"{count} agents";

    /// <summary>
    /// The speedup of the summary (busy time over wall time; waiting does not count as work): 1.515 → "1.5x parallel
    /// speedup"; under 1.05 → "no parallel speedup".
    /// </summary>
    public Func<double, string> Speedup { get; set; } = SpeedupPhrase;

    /// <summary>Added to the counting status line's accessible name while agents work: 3 → ", 3 agents busy".</summary>
    public Func<int, string> AgentsWorking { get; set; } = count => count == 1 ? ", 1 agent busy" : $", {count} agents busy";

    /// <summary>The scrubber's value text: (12.4, 26.8) → "12.4s of 26.8s".</summary>
    public Func<double, double, string> ScrubberValue { get; set; } =
        (time, length) => AgentTimelineMath.Seconds(time) + " of " + AgentTimelineMath.Seconds(length);

    /// <summary>Announced when the replay starts or resumes or the speed changes while it plays: 2 → "Playing at 2x".</summary>
    public Func<double, string> PlayingAt { get; set; } = speed => $"Playing at {AgentTimelineMath.Speed(speed)}x";

    /// <summary>Announced when the replay pauses or the scrubber moves: 11.1 → "Paused at 11.1s".</summary>
    public Func<double, string> PausedAt { get; set; } = time => "Paused at " + AgentTimelineMath.Seconds(time);

    /// <summary>Announced when the replay reaches the end or a live run ends.</summary>
    public Func<AgentTimelineSummary, string> RunComplete { get; set; } = summary =>
        $"Run complete: {(summary.AgentCount == 1 ? "1 agent" : $"{summary.AgentCount} agents")} in {AgentTimelineMath.Seconds(summary.Duration)}, " +
        $"{SpeedupPhrase(summary.Speedup)}, {AgentTimelineMath.Cost(summary.Cost)}";

    /// <summary>The card note of a span that received work: (agent, label) → "From Coordinator: Write up the incident".</summary>
    public Func<string, string?, string> HandoffFrom { get; set; } =
        (agent, label) => string.IsNullOrEmpty(label) ? $"From {agent}" : $"From {agent}: {label}";

    /// <summary>The card note of a span that handed work on: (agent, label) → "To Coordinator: Note drafted".</summary>
    public Func<string, string?, string> HandoffTo { get; set; } =
        (agent, label) => string.IsNullOrEmpty(label) ? $"To {agent}" : $"To {agent}: {label}";

    /// <summary>The card note of a span with several handoffs: 5 → "5 handoffs in this span".</summary>
    public Func<int, string> HandoffsDuring { get; set; } = count => $"{count} handoffs in this span";

    /// <summary>The card note of a failed span: its detail → "Failed: Scan ran past the time limit".</summary>
    public Func<string?, string> FailedNote { get; set; } =
        detail => string.IsNullOrEmpty(detail) ? "Failed" : "Failed: " + detail;

    /// <summary>The card note of a retry without a detail.</summary>
    public string RetryNote { get; set; } = "Retried after a failure";

    /// <summary>
    /// A span's accessible name; null uses <see cref="DescribeSpan"/>: "Log reader, Tool call, Mail logs, failed: Bounce
    /// scan took too long, 3.1s to 5.2s, 430 tokens".
    /// </summary>
    public Func<AgentTimelineAgent, AgentTimelineSpan, string>? SpanName { get; set; }

    /// <summary>The default speedup phrase: "1.5x parallel speedup", or "no parallel speedup" below 1.05.</summary>
    public static string SpeedupPhrase(double factor)
        => factor >= 1.05 ? AgentTimelineMath.Speedup(factor) + "x parallel speedup" : "no parallel speedup";

    /// <summary>The kind word of a span.</summary>
    public string KindOf(AgentTimelineSpanKind kind) => kind switch
    {
        AgentTimelineSpanKind.Tool => ToolCall,
        AgentTimelineSpanKind.Waiting => Waiting,
        _ => Thinking
    };

    /// <summary>
    /// The default accessible name of a span: agent, kind, tool, "failed" or "retry", what it did, its time range and its
    /// tokens. An open span reads "from 3.1s, still running".
    /// </summary>
    public string DescribeSpan(AgentTimelineAgent agent, AgentTimelineSpan span)
    {
        if (SpanName is { } custom)
        {
            return custom(agent, span);
        }
        var text = new StringBuilder();
        text.Append(agent.Name).Append(", ").Append(KindOf(span.Kind));
        if (span.Kind == AgentTimelineSpanKind.Tool && !string.IsNullOrEmpty(span.Tool))
        {
            text.Append(", ").Append(span.Tool);
        }
        if (span.Status == AgentTimelineSpanStatus.Failed)
        {
            text.Append(", ").Append(Failed.ToLowerInvariant());
        }
        else if (span.IsRetry)
        {
            text.Append(", retry");
        }
        text.Append(": ").Append(span.Label);
        if (span.End is { } end)
        {
            text.Append(", ").Append(Time(span.Start)).Append(" to ").Append(Time(end));
        }
        else
        {
            text.Append(", from ").Append(Time(span.Start)).Append(", still running");
        }
        if (span.Kind != AgentTimelineSpanKind.Waiting && span.Tokens is { } tokens)
        {
            text.Append(", ").Append(Tokens(tokens));
        }
        return text.ToString();
    }
}
