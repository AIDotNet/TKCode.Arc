using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ReasoningTree"/> shows or announces, in English by default: column headers, the header
/// status, the beam width label, the tag and accessible names of the thoughts and the polite announcements. Replace the
/// instance to translate or reword; sentences with numbers are functions so that word order can change. The playback
/// buttons are named by <see cref="Explainer.PlaybackLabels"/>.
/// </summary>
public sealed class ReasoningTreeLabels
{
    /// <summary>The header of the question column (wide layout).</summary>
    public string QuestionColumn { get; init; } = "Question";

    /// <summary>The header of a depth column; the argument is the depth (1 to 3).</summary>
    public Func<int, string> DepthColumn { get; init; } = static depth => $"Depth {depth}";

    /// <summary>The count beside a depth header once that depth is pruned; the arguments are kept and candidates.</summary>
    public Func<int, int, string> KeptCount { get; init; } = static (kept, total) => $"{kept} of {total} kept";

    /// <summary>The count beside the last depth's header once the answer is chosen; the argument is the candidates.</summary>
    public Func<int, string> BestOf { get; init; } = static total => $"Best of {total}";

    /// <summary>The header status before the search starts (and while it waits at its first beat).</summary>
    public string Ready { get; init; } = "Ready";

    /// <summary>The header status while the question is read, before the first thoughts appear.</summary>
    public string Reading { get; init; } = "Studying the question";

    /// <summary>The header status while thoughts are proposed; the argument is how many.</summary>
    public Func<int, string> Proposing { get; init; } = static count => count == 1 ? "Drafting one thought" : $"Drafting {count} thoughts";

    /// <summary>The header status when paused or stepping after thoughts were proposed; the argument is how many.</summary>
    public Func<int, string> Proposed { get; init; } = static count => count == 1 ? "Drafted one thought" : $"Drafted {count} thoughts";

    /// <summary>The header status while thoughts are scored; the argument is how many.</summary>
    public Func<int, string> Scoring { get; init; } = static count => count == 1 ? "Scoring one thought" : $"Scoring {count} thoughts";

    /// <summary>The header status when paused or stepping after thoughts were scored; the argument is how many.</summary>
    public Func<int, string> Scored { get; init; } = static count => count == 1 ? "Scored one thought" : $"Scored {count} thoughts";

    /// <summary>The header status while the best thoughts are kept; the argument is how many survive.</summary>
    public Func<int, string> Keeping { get; init; } = static count => count == 1 ? "Keeping the top one" : $"Keeping the top {count}";

    /// <summary>The header status when paused or stepping after the best thoughts were kept; the argument is how many.</summary>
    public Func<int, string> KeptBest { get; init; } = static count => count == 1 ? "Kept the top one" : $"Kept the top {count}";

    /// <summary>The header status once the answer is found.</summary>
    public string AnswerFound { get; init; } = "Answer found";

    /// <summary>The header status of a tree without thoughts.</summary>
    public string NothingToSearch { get; init; } = "No thoughts to search";

    /// <summary>The accessible name of the group of playback buttons.</summary>
    public string PlaybackGroup { get; init; } = "Playback";

    /// <summary>The label before the beam width control on wide cards (also the control's accessible name).</summary>
    public string BeamWidth { get; init; } = "Beam width";

    /// <summary>The label before the beam width control on compact cards (under 520px).</summary>
    public string BeamWidthShort { get; init; } = "Beam";

    /// <summary>The fate of a thought that is shown but not scored yet.</summary>
    public string NotScored { get; init; } = "Not scored yet";

    /// <summary>The fate of a scored thought whose depth is not pruned yet.</summary>
    public string FateScored { get; init; } = "Scored";

    /// <summary>The fate of a thought that survived its depth.</summary>
    public string FateKept { get; init; } = "Kept";

    /// <summary>The fate of a thought that was pruned.</summary>
    public string FatePruned { get; init; } = "Pruned";

    /// <summary>The fate of a kept thought on the path from the question to the answer.</summary>
    public string FateOnPath { get; init; } = "On the winning path";

    /// <summary>The fate of the answer.</summary>
    public string FateAnswer { get; init; } = "Answer";

    /// <summary>The second line of the question's tag.</summary>
    public string QuestionMeta { get; init; } = "Question";

    /// <summary>The second line of a scored thought's tag; the arguments are the formatted score and the fate.</summary>
    public Func<string, string, string> ScoreMeta { get; init; } = static (score, fate) => $"Score {score} · {fate}";

    /// <summary>Formats a score (default two decimals, "0.82").</summary>
    public Func<double, string> FormatScore { get; init; } =
        static score => (Math.Round(score * 100, MidpointRounding.AwayFromZero) / 100).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The accessible name of a thought; the arguments are its full text and its tag's second line.</summary>
    public Func<string, string, string> ThoughtName { get; init; } = static (text, meta) => $"{text} {meta}.";

    /// <summary>The accessible name of the question node; the argument is the full question.</summary>
    public Func<string, string> QuestionName { get; init; } = static question => $"Question: {question}";

    /// <summary>The accessible name of the tree; the argument is the full question.</summary>
    public Func<string, string> TreeName { get; init; } = static question => $"Search tree for {question}";

    /// <summary>The help text of a thought: its depth, its place among the shown siblings and their number.</summary>
    public Func<int, int, int, string> ThoughtPosition { get; init; } = static (depth, position, count) => $"Depth {depth}, {position} of {count}";

    /// <summary>Announced when the reader pauses or restarts before the first thoughts.</summary>
    public string ReadyAnnouncement { get; init; } = "Ready. Press play, or step through the search one beat at a time.";

    /// <summary>Announced after thoughts are proposed; the arguments are the depth and how many.</summary>
    public Func<int, int, string> ProposedAnnouncement { get; init; } =
        static (depth, count) => count == 1 ? $"Depth {depth}: one thought proposed." : $"Depth {depth}: {count} thoughts proposed.";

    /// <summary>Announced after thoughts are scored; the arguments are the depth, how many, the best label and its score.</summary>
    public Func<int, int, string, string, string> ScoredAnnouncement { get; init; } =
        static (depth, count, best, score) => $"Depth {depth}: {count} scored. Highest is {best} at {score}.";

    /// <summary>Announced after a depth is pruned; the arguments are the depth, the kept and the candidates.</summary>
    public Func<int, int, int, string> KeptAnnouncement { get; init; } =
        static (depth, kept, total) => $"Depth {depth}: kept {kept} of {total}, pruned {total - kept}.";

    /// <summary>Announced when the search ends; the arguments are the answer's score and full text.</summary>
    public Func<string, string, string> AnswerAnnouncement { get; init; } = static (score, text) => $"Answer found with a score of {score}: {text}";

    /// <summary>Announced when the beam width changes; the argument is the new width.</summary>
    public Func<int, string> BeamWidthAnnouncement { get; init; } = static width => $"Beam width {width}. The search starts over.";
}
