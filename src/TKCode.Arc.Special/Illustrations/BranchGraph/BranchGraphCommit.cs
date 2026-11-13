using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a commit does to the graph.</summary>
public enum BranchGraphCommitKind
{
    /// <summary>A commit on a branch that already exists (or the very first commit).</summary>
    Commit,

    /// <summary>The first commit of a new branch, which leaves its parent's lane.</summary>
    Branch,

    /// <summary>A commit with two parents: another branch joins this one.</summary>
    Merge
}

/// <summary>
/// One commit of a <see cref="BranchGraph"/>, listed oldest first (the graph shows the newest on top). <see cref="Branch"/>
/// picks its lane. Without <see cref="Parents"/> its parent is the previous commit on the same branch, or, for a branch's
/// first commit, the previous commit of any branch. Give two parents (the branch's own first) to merge. The row shows the
/// message, the author's avatar, <see cref="Hash"/> and <see cref="Date"/>; the detail card adds <see cref="Body"/> and the
/// diff stats.
/// </summary>
public class BranchGraphCommit : AvaloniaObject
{
    /// <summary>The commit's short id ("a1f3c0"); parents refer to commits by it. Without one the commit gets "#n".</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Id));

    /// <summary>The first line of the commit message.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Message));

    /// <summary>The branch the commit is made on; it picks the lane (default "main").</summary>
    public static readonly StyledProperty<string?> BranchProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Branch));

    /// <summary>The parents' ids separated by commas or spaces, the first one on this branch; empty for the usual parent (see the class).</summary>
    public static readonly StyledProperty<string?> ParentsProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Parents));

    /// <summary>Who made the commit (shown on the detail card and read by screen readers).</summary>
    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Author));

    /// <summary>A tag on the commit ("v1.2.0"), shown as a pill in its branch's color after the branch pills.</summary>
    public static readonly StyledProperty<string?> TagProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Tag));

    /// <summary>The short hash shown on the row and the card ("4c1d9e2"); null shows <see cref="Id"/>.</summary>
    public static readonly StyledProperty<string?> HashProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Hash));

    /// <summary>The rest of the commit message, shown on the detail card (up to two lines).</summary>
    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Body));

    /// <summary>The author's picture; null draws a colored disc.</summary>
    public static readonly StyledProperty<IImage?> AuthorAvatarProperty =
        AvaloniaProperty.Register<BranchGraphCommit, IImage?>(nameof(AuthorAvatar));

    /// <summary>The color of the disc drawn without <see cref="AuthorAvatar"/>; null picks one from <see cref="Author"/>.</summary>
    public static readonly StyledProperty<Color?> AuthorColorProperty =
        AvaloniaProperty.Register<BranchGraphCommit, Color?>(nameof(AuthorColor));

    /// <summary>When the commit was made, as display text ("2 hours ago", "Yesterday").</summary>
    public static readonly StyledProperty<string?> DateProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Date));

    /// <summary>Lines added (shown with a + on the card); null hides the stats.</summary>
    public static readonly StyledProperty<int?> AdditionsProperty =
        AvaloniaProperty.Register<BranchGraphCommit, int?>(nameof(Additions));

    /// <summary>Lines removed (shown with a − on the card).</summary>
    public static readonly StyledProperty<int?> DeletionsProperty =
        AvaloniaProperty.Register<BranchGraphCommit, int?>(nameof(Deletions));

    /// <summary>Files changed.</summary>
    public static readonly StyledProperty<int?> FilesProperty =
        AvaloniaProperty.Register<BranchGraphCommit, int?>(nameof(Files));

    /// <summary>Creates an empty commit.</summary>
    public BranchGraphCommit()
    {
    }

    /// <summary>Creates a commit with an id, a branch and a message.</summary>
    public BranchGraphCommit(string id, string branch, string message, string? parents = null, string? author = null, string? tag = null)
    {
        Id = id;
        Branch = branch;
        Message = message;
        Parents = parents;
        Author = author;
        Tag = tag;
    }

    /// <summary>The commit's short id ("a1f3c0"); parents refer to commits by it. Without one the commit gets "#n".</summary>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The first line of the commit message.</summary>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <summary>The branch the commit is made on; it picks the lane (default "main").</summary>
    public string? Branch { get => GetValue(BranchProperty); set => SetValue(BranchProperty, value); }

    /// <summary>The parents' ids separated by commas or spaces, the first one on this branch; empty for the usual parent (see the class).</summary>
    public string? Parents { get => GetValue(ParentsProperty); set => SetValue(ParentsProperty, value); }

    /// <summary>Who made the commit (shown on the detail card and read by screen readers).</summary>
    public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    /// <summary>A tag on the commit ("v1.2.0"), shown as a pill in its branch's color after the branch pills.</summary>
    public string? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc cref="HashProperty"/>
    public string? Hash { get => GetValue(HashProperty); set => SetValue(HashProperty, value); }

    /// <inheritdoc cref="BodyProperty"/>
    public string? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    /// <inheritdoc cref="AuthorAvatarProperty"/>
    public IImage? AuthorAvatar { get => GetValue(AuthorAvatarProperty); set => SetValue(AuthorAvatarProperty, value); }

    /// <inheritdoc cref="AuthorColorProperty"/>
    public Color? AuthorColor { get => GetValue(AuthorColorProperty); set => SetValue(AuthorColorProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public string? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="AdditionsProperty"/>
    public int? Additions { get => GetValue(AdditionsProperty); set => SetValue(AdditionsProperty, value); }

    /// <inheritdoc cref="DeletionsProperty"/>
    public int? Deletions { get => GetValue(DeletionsProperty); set => SetValue(DeletionsProperty, value); }

    /// <inheritdoc cref="FilesProperty"/>
    public int? Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }
}

/// <summary>What a <see cref="BranchGraph"/> previews: the history as it is, or how its pull request would land.</summary>
public enum BranchGraphStrategy
{
    /// <summary>The history before the pull request lands.</summary>
    Before,

    /// <summary>A merge commit joins the pull request's branch to the target; both lines of history stay.</summary>
    Merge,

    /// <summary>The pull request's commits are replayed on the target's tip with new hashes.</summary>
    Rebase,

    /// <summary>The pull request's commits are folded into one commit on the target.</summary>
    Squash
}

/// <summary>Arguments of <see cref="BranchGraph.SelectionChanged"/>.</summary>
public sealed class BranchGraphSelectionChangedEventArgs : RoutedEventArgs
{
    internal BranchGraphSelectionChangedEventArgs(RoutedEvent routedEvent, object source, BranchGraphStrategy oldValue,
        BranchGraphStrategy newValue, bool isUserChange)
        : base(routedEvent, source)
    {
        OldValue = oldValue;
        NewValue = newValue;
        IsUserChange = isUserChange;
    }

    /// <summary>The strategy shown before.</summary>
    public BranchGraphStrategy OldValue { get; }

    /// <summary>The strategy shown now.</summary>
    public BranchGraphStrategy NewValue { get; }

    /// <summary>True when the reader picked it (the strategy control or its keys); false for code and autoplay.</summary>
    public bool IsUserChange { get; }
}

/// <summary>Arguments of <see cref="BranchGraph.CommitDrawn"/>.</summary>
public sealed class BranchGraphCommitEventArgs : RoutedEventArgs
{
    internal BranchGraphCommitEventArgs(RoutedEvent routedEvent, object source, BranchGraphCommit commit, int index, BranchGraphCommitKind kind)
        : base(routedEvent, source)
    {
        Commit = commit;
        Index = index;
        Kind = kind;
    }

    /// <summary>The commit that was drawn.</summary>
    public BranchGraphCommit Commit { get; }

    /// <summary>Its position in the history, from 0 (oldest).</summary>
    public int Index { get; }

    /// <summary>Whether it was a plain commit, the start of a branch or a merge.</summary>
    public BranchGraphCommitKind Kind { get; }
}

/// <summary>
/// Every word a <see cref="BranchGraph"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with names or numbers are functions so the word order can change.
/// </summary>
public sealed class BranchGraphLabels
{
    private const string NoStoryText =
        "No longer shown: the graph has no status line, caption or speed control (deprecated in 1.2.0).";

    /// <summary>The status before the story starts. No longer shown (the graph has no status line).</summary>
    [Obsolete(NoStoryText)]
    public string Ready { get; set; } = "Ready";

    /// <summary>The status while the story plays. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public string Drawing { get; set; } = "Drawing";

    /// <summary>The status while paused. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public string Paused { get; set; } = "Paused";

    /// <summary>The status once every commit is drawn. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public string Done { get; set; } = "Up to date";

    /// <summary>The words before the speed control. No longer shown (the graph has no speed control).</summary>
    [Obsolete(NoStoryText)]
    public string Speed { get; set; } = "Speed";

    /// <summary>The caption before the story starts. No longer shown (the footer shows the strategy's summary).</summary>
    [Obsolete(NoStoryText)]
    public Func<int, string> ReadyCaption { get; set; } =
        count => $"A history of {count} commits. Play it to watch branches split off and merge back.";

    /// <summary>The caption of the first commit. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<string, string> FirstCaption { get; set; } = branch => $"Every history starts with a first commit, here on {branch}.";

    /// <summary>The caption of a plain commit. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<string, string, string> CommitCaption { get; set; } =
        (message, branch) => $"“{message}” is committed on {branch}. The branch label moves up to it.";

    /// <summary>The caption of a branch's first commit. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<string, string, string, string> BranchCaption { get; set; } =
        (branch, from, parent) => $"{branch} branches off {from} at {parent}. Work now goes on in two places at once.";

    /// <summary>The caption of a merge. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<string, string, string> MergeCaption { get; set; } =
        (source, target) => $"{source} is merged into {target}. The merge commit has two parents, one from each side.";

    /// <summary>Added to a caption when the commit has a tag. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<string, string> TagCaption { get; set; } = tag => $" It is tagged {tag}.";

    /// <summary>The caption at the end. No longer shown.</summary>
    [Obsolete(NoStoryText)]
    public Func<int, int, int, string> DoneCaption { get; set; } =
        (commits, branches, merges) => $"{commits} commits on {branches} branches, joined by {merges} merges. Select a commit to see its history.";

    /// <summary>The start of a commit's accessible name; the arguments are the message, the hash, the branch and the author (may be empty).</summary>
    public Func<string, string, string, string, string> Describe { get; set; } =
        (message, id, branch, author) => author.Length > 0 ? $"{message}, {id} on {branch} by {author}" : $"{message}, {id} on {branch}";

    /// <summary>The strategy control's option for the history as it is.</summary>
    public string Before { get; set; } = "Before";

    /// <summary>The strategy control's option for a merge commit.</summary>
    public string Merge { get; set; } = "Merge";

    /// <summary>The strategy control's option for a rebase.</summary>
    public string Rebase { get; set; } = "Rebase";

    /// <summary>The strategy control's option for a squash.</summary>
    public string Squash { get; set; } = "Squash";

    /// <summary>The accessible name of the strategy control.</summary>
    public string StrategyGroup { get; set; } = "Preview how the pull request lands";

    /// <summary>The date of the commits a preview adds.</summary>
    public string JustNow { get; set; } = "Just now";

    /// <summary>The start of the commit list's accessible name (the summary follows).</summary>
    public string CommitList { get; set; } = "Commits, newest on top";

    /// <summary>The graph's accessible name; the arguments are the title, the pull request's branch and the target.</summary>
    public Func<string, string, string, string> GraphName { get; set; } =
        (title, source, target) => $"{title}, history of {source} and {target}";

    /// <summary>The Before summary; the arguments are the pull request's branch, the target, commits ahead and commits behind.</summary>
    public Func<string, string, int, int, string> BeforeSummary { get; set; } =
        (source, target, ahead, behind) => $"{source} is {Commits(ahead)} ahead of {target} and {behind} behind.";

    /// <summary>The summary of a history without a pull request; the arguments are the commits and the branches.</summary>
    public Func<int, int, string> HistorySummary { get; set; } =
        (commits, branches) => $"{Commits(commits)} on {branches} {(branches == 1 ? "branch" : "branches")}.";

    /// <summary>The Merge summary; the arguments are the pull request's commits and the target.</summary>
    public Func<int, string, string> MergeSummary { get; set; } =
        (count, target) => $"One merge commit joins {Commits(count)} to {target}, and both lines of history stay.";

    /// <summary>The Rebase summary; the arguments are the pull request's commits and the target.</summary>
    public Func<int, string, string> RebaseSummary { get; set; } =
        (count, target) => $"{Commits(count)} {(count == 1 ? "moves" : "move")} onto the tip of {target} with new hashes, in one straight line.";

    /// <summary>The Squash summary; the arguments are the pull request's commits and the target.</summary>
    public Func<int, string, string> SquashSummary { get; set; } =
        (count, target) => $"{Commits(count)} {(count == 1 ? "becomes" : "become")} a single commit on {target}.";

    /// <summary>The message of the merge commit; the arguments are the pull request's number (0 for none) and its branch.</summary>
    public Func<int, string, string> MergeMessage { get; set; } =
        (number, source) => number > 0 ? $"Merge pull request #{number} from {source}" : $"Merge branch {source}";

    /// <summary>The message of the squashed commit; the arguments are the pull request's title and number (0 for none).</summary>
    public Func<string, int, string> SquashMessage { get; set; } =
        (title, number) => number > 0 ? $"{title} (#{number})" : title;

    /// <summary>The diff stats in a commit's accessible name; the arguments are lines added, lines removed and files.</summary>
    public Func<int, int, int, string> Stats { get; set; } =
        (added, removed, files) => $"{added} added, {removed} removed, {files} {(files == 1 ? "file" : "files")}";

    /// <summary>The files count on the detail card.</summary>
    public Func<int, string> Files { get; set; } = files => files == 1 ? "1 file" : $"{files} files";

    /// <summary>The branches that point at a commit, in its accessible name; the argument is the branch names joined by "and".</summary>
    public Func<string, string> Heads { get; set; } = names => $"Head of {names}";

    private static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";
}
