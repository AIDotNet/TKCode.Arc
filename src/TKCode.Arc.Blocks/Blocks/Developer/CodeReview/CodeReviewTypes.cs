namespace TKCode.Arc;

/// <summary>Whether a <see cref="CodeReviewFile"/> is new in the pull request or changes an existing file.</summary>
public enum CodeReviewFileStatus
{
    /// <summary>The file existed and is changed.</summary>
    Modified,

    /// <summary>The file is new; its header shows a "New" pill.</summary>
    Added
}

/// <summary>The kind of a <see cref="CodeReviewDiffLine"/>.</summary>
public enum CodeReviewLineKind
{
    /// <summary>
    /// A hunk header: where the next lines start in the old and new file (<see cref="CodeReviewDiffLine.OldStart"/>,
    /// <see cref="CodeReviewDiffLine.NewStart"/>) and an optional section name in <see cref="CodeReviewDiffLine.Text"/>.
    /// </summary>
    Hunk,

    /// <summary>An unchanged line, on both sides.</summary>
    Context,

    /// <summary>A line only on the new side.</summary>
    Added,

    /// <summary>A line only on the old side.</summary>
    Deleted
}

/// <summary>Someone who takes part in a review: the author, a reviewer or the person reviewing now.</summary>
public sealed class CodeReviewPerson
{
    /// <summary>Creates a person.</summary>
    public CodeReviewPerson(string name)
    {
        Name = name ?? string.Empty;
    }

    /// <summary>The display name; the avatar shows its initials.</summary>
    public string Name { get; }
}

/// <summary>The pull request a <see cref="CodeReview"/> reviews: what the header shows.</summary>
public sealed class CodeReviewPullRequest
{
    /// <summary>The title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The number, shown as "#1234" after the title.</summary>
    public int Number { get; init; }

    /// <summary>Who opened it.</summary>
    public CodeReviewPerson Author { get; init; } = new(string.Empty);

    /// <summary>The branch the changes go into ("main").</summary>
    public string BaseBranch { get; init; } = "main";

    /// <summary>The branch the changes come from.</summary>
    public string HeadBranch { get; init; } = string.Empty;

    /// <summary>How many commits the pull request has.</summary>
    public int CommitCount { get; init; }
}

/// <summary>
/// One line of a file's unified diff. Lines carry no numbers: they follow from the hunk headers, so rewriting a line
/// renumbers the rest. Use the factory methods.
/// </summary>
public sealed class CodeReviewDiffLine
{
    private CodeReviewDiffLine(string id, CodeReviewLineKind kind, string text, int oldStart, int newStart)
    {
        Id = id ?? string.Empty;
        Kind = kind;
        Text = text ?? string.Empty;
        OldStart = oldStart;
        NewStart = newStart;
    }

    /// <summary>A stable id; threads and suggestions point at lines by id, not by number.</summary>
    public string Id { get; }

    /// <summary>What the line is.</summary>
    public CodeReviewLineKind Kind { get; }

    /// <summary>The code of the line without its sign; for a hunk header, the optional section name.</summary>
    public string Text { get; }

    /// <summary>For a hunk header, the first old line number of the hunk; 0 otherwise.</summary>
    public int OldStart { get; }

    /// <summary>For a hunk header, the first new line number of the hunk; 0 otherwise.</summary>
    public int NewStart { get; }

    /// <summary>A hunk header whose lines start at <paramref name="oldStart"/> and <paramref name="newStart"/>.</summary>
    public static CodeReviewDiffLine Hunk(string id, int oldStart, int newStart, string? section = null)
        => new(id, CodeReviewLineKind.Hunk, section ?? string.Empty, Math.Max(0, oldStart), Math.Max(0, newStart));

    /// <summary>An unchanged line.</summary>
    public static CodeReviewDiffLine Context(string id, string text) => new(id, CodeReviewLineKind.Context, text, 0, 0);

    /// <summary>An added line.</summary>
    public static CodeReviewDiffLine Added(string id, string text) => new(id, CodeReviewLineKind.Added, text, 0, 0);

    /// <summary>A removed line.</summary>
    public static CodeReviewDiffLine Deleted(string id, string text) => new(id, CodeReviewLineKind.Deleted, text, 0, 0);
}

/// <summary>A changed file: one tab of a <see cref="CodeReview"/>.</summary>
public sealed class CodeReviewFile
{
    /// <summary>A stable id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The path; the tab shows its last part.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The language the diff is colored as ("ts", "md", "csharp", ...; any name the code highlighter knows).</summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>New or modified.</summary>
    public CodeReviewFileStatus Status { get; init; }

    /// <summary>The diff: hunk headers, each followed by its context, added and deleted lines.</summary>
    public IList<CodeReviewDiffLine> Lines { get; init; } = new List<CodeReviewDiffLine>();
}

/// <summary>One comment in a <see cref="CodeReviewThread"/>.</summary>
public sealed class CodeReviewComment
{
    /// <summary>Who wrote it.</summary>
    public CodeReviewPerson Author { get; init; } = new(string.Empty);

    /// <summary>What it says.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>When it was written; shown as a relative time ("38 min ago").</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>A suggested change: new text for one or more new-side lines of the file.</summary>
public sealed class CodeReviewSuggestion
{
    /// <summary>
    /// The ids of the lines it replaces, in order. Added lines are swapped for the new ones; unchanged lines become
    /// deletions followed by the new lines.
    /// </summary>
    public IList<string> LineIds { get; init; } = new List<string>();

    /// <summary>The new text, one entry per line.</summary>
    public IList<string> NewLines { get; init; } = new List<string>();
}

/// <summary>
/// A conversation anchored under a line of a file. <see cref="CodeReview"/> keeps it up to date: replies are appended to
/// <see cref="Comments"/>, and <see cref="IsResolved"/>, <see cref="IsSuggestionApplied"/> and <see cref="IsPending"/>
/// follow what the reviewer does.
/// </summary>
public sealed class CodeReviewThread
{
    /// <summary>A stable id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The <see cref="CodeReviewFile.Id"/> of the file it belongs to.</summary>
    public string FileId { get; init; } = string.Empty;

    /// <summary>The id of the line it is shown under.</summary>
    public string AnchorLineId { get; set; } = string.Empty;

    /// <summary>What the thread is about, shown on its first line ("Line 9", "Lines 4–7").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The comments, oldest first.</summary>
    public IList<CodeReviewComment> Comments { get; init; } = new List<CodeReviewComment>();

    /// <summary>A change the first comment suggests, or null.</summary>
    public CodeReviewSuggestion? Suggestion { get; init; }

    /// <summary>Whether the conversation is resolved; resolved threads show folded to one line.</summary>
    public bool IsResolved { get; set; }

    /// <summary>Whether <see cref="Suggestion"/> has been applied to the diff.</summary>
    public bool IsSuggestionApplied { get; set; }

    /// <summary>A comment the reviewer added in this review, sent with the approval (shows "Pending" until then).</summary>
    public bool IsPending { get; set; }
}

/// <summary>What <see cref="CodeReview.SubmitReviewAction"/> sends: the approval and the comments added during the review.</summary>
public sealed class CodeReviewSubmission
{
    internal CodeReviewSubmission(string body, IReadOnlyList<CodeReviewThread> comments)
    {
        Body = body;
        Comments = comments;
    }

    /// <summary>The review's summary text (empty: the card has no summary field).</summary>
    public string Body { get; }

    /// <summary>The threads the reviewer started during this review.</summary>
    public IReadOnlyList<CodeReviewThread> Comments { get; }
}
