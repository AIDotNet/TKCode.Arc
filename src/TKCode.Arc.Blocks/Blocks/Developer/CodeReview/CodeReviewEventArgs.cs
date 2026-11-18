using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="CodeReview.SelectionChanged"/>: the file shown before and now.</summary>
public sealed class CodeReviewSelectionChangedEventArgs : RoutedEventArgs
{
    public CodeReviewSelectionChangedEventArgs(RoutedEvent routedEvent, CodeReviewFile? oldFile, CodeReviewFile? newFile)
        : base(routedEvent)
    {
        OldFile = oldFile;
        NewFile = newFile;
    }

    /// <summary>The file that was shown.</summary>
    public CodeReviewFile? OldFile { get; }

    /// <summary>The file shown now.</summary>
    public CodeReviewFile? NewFile { get; }
}

/// <summary>
/// Arguments of <see cref="CodeReview.CommentAdded"/> and <see cref="CodeReview.ResolvedChanged"/>: the thread, and for a
/// resolve whether it is resolved now.
/// </summary>
public sealed class CodeReviewThreadEventArgs : RoutedEventArgs
{
    public CodeReviewThreadEventArgs(RoutedEvent routedEvent, CodeReviewThread thread)
        : base(routedEvent)
    {
        Thread = thread;
        IsResolved = thread.IsResolved;
    }

    /// <summary>The thread.</summary>
    public CodeReviewThread Thread { get; }

    /// <summary>Whether the thread is resolved now.</summary>
    public bool IsResolved { get; }
}

/// <summary>Arguments of <see cref="CodeReview.ReplyPosted"/>.</summary>
public sealed class CodeReviewReplyEventArgs : RoutedEventArgs
{
    public CodeReviewReplyEventArgs(RoutedEvent routedEvent, CodeReviewThread thread, CodeReviewComment comment)
        : base(routedEvent)
    {
        Thread = thread;
        Comment = comment;
    }

    /// <summary>The thread replied to.</summary>
    public CodeReviewThread Thread { get; }

    /// <summary>The new reply (also the last of <see cref="CodeReviewThread.Comments"/>).</summary>
    public CodeReviewComment Comment { get; }
}

/// <summary>Arguments of <see cref="CodeReview.SuggestionApplied"/>: the thread and its file's new diff.</summary>
public sealed class CodeReviewSuggestionAppliedEventArgs : RoutedEventArgs
{
    public CodeReviewSuggestionAppliedEventArgs(RoutedEvent routedEvent, CodeReviewThread thread, CodeReviewFile file,
        IReadOnlyList<CodeReviewDiffLine> lines)
        : base(routedEvent)
    {
        Thread = thread;
        File = file;
        Lines = lines;
    }

    /// <summary>The thread whose suggestion was applied (resolved now).</summary>
    public CodeReviewThread Thread { get; }

    /// <summary>The file the suggestion changed.</summary>
    public CodeReviewFile File { get; }

    /// <summary>The file's diff after the change; commit it on your server.</summary>
    public IReadOnlyList<CodeReviewDiffLine> Lines { get; }
}

/// <summary>Arguments of <see cref="CodeReview.ViewedChanged"/>.</summary>
public sealed class CodeReviewViewedChangedEventArgs : RoutedEventArgs
{
    public CodeReviewViewedChangedEventArgs(RoutedEvent routedEvent, CodeReviewFile file, bool isViewed)
        : base(routedEvent)
    {
        File = file;
        IsViewed = isViewed;
    }

    /// <summary>The file.</summary>
    public CodeReviewFile File { get; }

    /// <summary>Whether it is marked viewed now.</summary>
    public bool IsViewed { get; }
}

/// <summary>Arguments of <see cref="CodeReview.ReviewSubmitted"/> and <see cref="CodeReview.ApprovalWithdrawn"/>.</summary>
public sealed class CodeReviewSubmittedEventArgs : RoutedEventArgs
{
    public CodeReviewSubmittedEventArgs(RoutedEvent routedEvent, CodeReviewSubmission submission)
        : base(routedEvent)
    {
        Submission = submission;
    }

    /// <summary>What was sent.</summary>
    public CodeReviewSubmission Submission { get; }
}
