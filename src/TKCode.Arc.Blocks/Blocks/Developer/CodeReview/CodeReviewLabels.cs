namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="CodeReview"/> shows or announces, in English by default. Replace the instance to translate or
/// reword. Texts with numbers or names are functions, so the word order can change and words can agree with a count.
/// </summary>
public sealed class CodeReviewLabels
{
    // ---- Header ----

    /// <summary>The card's accessible name, from the pull request number.</summary>
    public Func<int, string> ReviewName { get; init; } = number => $"Review of pull request {number}";

    /// <summary>The words between the author and the base branch, from the commit count ("wants to merge 4 commits into").</summary>
    public Func<int, string> MergeLine { get; init; } =
        commits => $"wants to merge {commits} {(commits == 1 ? "commit" : "commits")} into";

    /// <summary>The word between the base and the head branch.</summary>
    public string From { get; init; } = "from";

    /// <summary>The approve button.</summary>
    public string Approve { get; init; } = "Approve";

    /// <summary>The approve button after the approval was sent.</summary>
    public string Approved { get; init; } = "Approved";

    /// <summary>The approve button after sending failed.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The status line under the header after approving.</summary>
    public string ApprovedStatus { get; init; } = "You approved these changes";

    /// <summary>The muted end of the status line, from the number of comments sent with it (empty for none).</summary>
    public Func<int, string> WithComments { get; init; } =
        count => count == 0 ? string.Empty : count == 1 ? "with 1 comment" : $"with {count} comments";

    /// <summary>The button that withdraws the approval.</summary>
    public string Undo { get; init; } = "Undo";

    // ---- Files ----

    /// <summary>The accessible name of the file tabs.</summary>
    public string FilesName { get; init; } = "Changed files";

    /// <summary>A file tab's accessible name: path, whether it is viewed and how many conversations are open.</summary>
    public Func<string, bool, int, string> TabName { get; init; } = (path, viewed, open) =>
    {
        var name = viewed ? $"{path}, viewed" : path;
        return open == 0 ? name : $"{name}, {open} open {(open == 1 ? "conversation" : "conversations")}";
    };

    /// <summary>The text after the viewed count ("/5 viewed"), from the number of files.</summary>
    public Func<int, string> ViewedOf { get; init; } = total => $"/{total} viewed";

    /// <summary>The progress's accessible name: viewed files, files, additions and deletions.</summary>
    public Func<int, int, int, int, string> ProgressName { get; init; } =
        (viewed, total, added, deleted) => $"{viewed} of {total} files viewed, {added} additions, {deleted} deletions";

    /// <summary>The pill of a new file.</summary>
    public string New { get; init; } = "New";

    /// <summary>The viewed checkbox.</summary>
    public string Viewed { get; init; } = "Viewed";

    /// <summary>The viewed checkbox's accessible name, from the path.</summary>
    public Func<string, string> ViewedName { get; init; } = path => $"Viewed {path}";

    /// <summary>The diffstat's accessible name: additions and deletions.</summary>
    public Func<int, int, string> DiffstatName { get; init; } = (added, deleted) =>
        $"{added} {(added == 1 ? "addition" : "additions")}, {deleted} {(deleted == 1 ? "deletion" : "deletions")}";

    // ---- Lines ----

    /// <summary>A line number's accessible name, from the kind and the number ("Comment on added line 9").</summary>
    public Func<CodeReviewLineKind, int, string> LineName { get; init; } = (kind, number) => kind switch
    {
        CodeReviewLineKind.Added => $"Comment on added line {number}",
        CodeReviewLineKind.Deleted => $"Comment on deleted line {number}",
        _ => $"Comment on line {number}"
    };

    /// <summary>The label of a new comment's thread, from the line number ("Line 9").</summary>
    public Func<int, string> LineLabel { get; init; } = number => $"Line {number}";

    // ---- Threads ----

    /// <summary>The pill of a comment that is sent with the approval.</summary>
    public string Pending { get; init; } = "Pending";

    /// <summary>The heading of a suggested change.</summary>
    public string SuggestedChange { get; init; } = "Suggested change";

    /// <summary>The button that applies a suggestion.</summary>
    public string ApplySuggestion { get; init; } = "Apply suggestion";

    /// <summary>The same button once applied.</summary>
    public string Applied { get; init; } = "Applied";

    /// <summary>The reply button and the button that posts a reply.</summary>
    public string Reply { get; init; } = "Reply";

    /// <summary>The resolve button.</summary>
    public string Resolve { get; init; } = "Resolve";

    /// <summary>The button that reopens a resolved thread.</summary>
    public string Reopen { get; init; } = "Reopen";

    /// <summary>The link that folds an opened resolved thread again.</summary>
    public string Hide { get; init; } = "Hide";

    /// <summary>The first word of a folded thread.</summary>
    public string Resolved { get; init; } = "Resolved";

    /// <summary>The first words of a folded thread whose suggestion was applied.</summary>
    public string SuggestionApplied { get; init; } = "Suggestion applied";

    /// <summary>The placeholder of the comment and reply boxes.</summary>
    public string LeaveComment { get; init; } = "Leave a comment";

    /// <summary>The toggle that adds a suggested change to a new comment.</summary>
    public string SuggestChange { get; init; } = "Suggest a change";

    /// <summary>The button that closes a comment or reply box.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The button that posts a new comment.</summary>
    public string Comment { get; init; } = "Comment";

    /// <summary>The comment box's accessible name, from the thread label ("Comment on line 2").</summary>
    public Func<string, string> CommentFieldName { get; init; } = label => $"Comment on {LowerFirst(label)}";

    /// <summary>The reply box's accessible name, from the thread label ("Reply to Line 9").</summary>
    public Func<string, string> ReplyFieldName { get; init; } = label => $"Reply to {label}";

    // ---- Times ----

    /// <summary>A comment's time when it is less than a minute old.</summary>
    public string JustNow { get; init; } = "Just now";

    /// <summary>A comment's age in minutes, hours or days.</summary>
    public Func<TimeSpan, string> Age { get; init; } = age =>
        age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min ago"
        : age.TotalHours < 24 ? $"{(int)age.TotalHours} h ago"
        : $"{(int)age.TotalDays} d ago";

    // ---- Announcements ----

    /// <summary>Announced after a comment was added, from its thread label.</summary>
    public Func<string, string> CommentAddedAnnouncement { get; init; } =
        label => $"Comment added on {LowerFirst(label)}. It is pending until you approve.";

    /// <summary>Announced after a reply was posted.</summary>
    public string ReplyPostedAnnouncement { get; init; } = "Reply posted";

    /// <summary>Announced after a thread was resolved (also by applying its suggestion).</summary>
    public string ResolvedAnnouncement { get; init; } = "Conversation resolved";

    /// <summary>Announced after a thread was reopened.</summary>
    public string ReopenedAnnouncement { get; init; } = "Conversation reopened";

    /// <summary>Announced after a file was marked viewed or not viewed, from the path.</summary>
    public Func<string, bool, string> ViewedAnnouncement { get; init; } =
        (path, viewed) => viewed ? $"{path} marked viewed" : $"{path} marked not viewed";

    /// <summary>Announced after approving, from the number of comments sent.</summary>
    public Func<int, string> ApprovedAnnouncement { get; init; } = count => count == 0
        ? "You approved these changes"
        : $"You approved these changes with {count} {(count == 1 ? "comment" : "comments")}";

    /// <summary>Announced after the approval was withdrawn.</summary>
    public string WithdrawnAnnouncement { get; init; } = "Approval withdrawn";

    /// <summary>Announced when sending the approval failed.</summary>
    public string FailedAnnouncement { get; init; } = "The approval could not be sent. Try again.";

    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;
}
