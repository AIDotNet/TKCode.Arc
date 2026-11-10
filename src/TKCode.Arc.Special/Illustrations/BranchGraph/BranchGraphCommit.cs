using Avalonia;
using Avalonia.Interactivity;

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
/// One commit of a <see cref="BranchGraph"/>, listed oldest first. <see cref="Branch"/> picks its lane. Without
/// <see cref="Parents"/> its parent is the previous commit on the same branch, or, for a branch's first commit, the
/// previous commit of any branch. Give two parents (the branch's own first) to merge.
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

    /// <summary>Who made the commit (shown on wide cards and read by screen readers).</summary>
    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Author));

    /// <summary>A tag on the commit ("v1.2.0"), shown as a pill.</summary>
    public static readonly StyledProperty<string?> TagProperty =
        AvaloniaProperty.Register<BranchGraphCommit, string?>(nameof(Tag));

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

    /// <summary>Who made the commit (shown on wide cards and read by screen readers).</summary>
    public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    /// <summary>A tag on the commit ("v1.2.0"), shown as a pill.</summary>
    public string? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }
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
/// Every word a <see cref="BranchGraph"/> shows or announces, in English by default (the playback buttons are named by
/// <see cref="Explainer.PlaybackLabels"/>). Replace the instance to translate or reword; texts with names or numbers are
/// functions so the word order can change.
/// </summary>
public sealed class BranchGraphLabels
{
    /// <summary>The status before the story starts.</summary>
    public string Ready { get; set; } = "Ready";

    /// <summary>The status while the story plays or steps.</summary>
    public string Drawing { get; set; } = "Drawing";

    /// <summary>The status while paused.</summary>
    public string Paused { get; set; } = "Paused";

    /// <summary>The status once every commit is drawn.</summary>
    public string Done { get; set; } = "Up to date";

    /// <summary>The words before the speed control, also its accessible name.</summary>
    public string Speed { get; set; } = "Speed";

    /// <summary>The caption before the story starts; the argument is the number of commits.</summary>
    public Func<int, string> ReadyCaption { get; set; } =
        count => $"A history of {count} commits. Play it to watch branches split off and merge back.";

    /// <summary>The caption of the first commit; the argument is its branch.</summary>
    public Func<string, string> FirstCaption { get; set; } = branch => $"Every history starts with a first commit, here on {branch}.";

    /// <summary>The caption of a plain commit; the arguments are the message and the branch.</summary>
    public Func<string, string, string> CommitCaption { get; set; } =
        (message, branch) => $"“{message}” is committed on {branch}. The branch label moves up to it.";

    /// <summary>The caption of a branch's first commit; the arguments are the new branch, the branch it left and the parent's id.</summary>
    public Func<string, string, string, string> BranchCaption { get; set; } =
        (branch, from, parent) => $"{branch} branches off {from} at {parent}. Work now goes on in two places at once.";

    /// <summary>The caption of a merge; the arguments are the branch merged in and the branch it joins.</summary>
    public Func<string, string, string> MergeCaption { get; set; } =
        (source, target) => $"{source} is merged into {target}. The merge commit has two parents, one from each side.";

    /// <summary>Added to a caption when the commit has a tag; the argument is the tag.</summary>
    public Func<string, string> TagCaption { get; set; } = tag => $" It is tagged {tag}.";

    /// <summary>The caption at the end; the arguments are the commits, the branches and the merges.</summary>
    public Func<int, int, int, string> DoneCaption { get; set; } =
        (commits, branches, merges) => $"{commits} commits on {branches} branches, joined by {merges} merges. Select a commit to see its history.";

    /// <summary>A commit's accessible description; the arguments are the message, the id, the branch and the author (may be empty).</summary>
    public Func<string, string, string, string, string> Describe { get; set; } =
        (message, id, branch, author) => author.Length > 0 ? $"{message}, {id} on {branch} by {author}" : $"{message}, {id} on {branch}";
}
