using Avalonia;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A pull request review in one card: the title, author and branches with an Approve button; file tabs with their open
/// conversations and an "n/5 viewed" progress; for the chosen file its path, diffstat and a Viewed checkbox over a syntax
/// tinted unified diff. Hover a line, or focus its number, and an add-comment mark appears on the gutter's edge; press it
/// to write a comment, optionally with a suggested change. Threads hang in the flow under their line with Apply, Reply
/// and Resolve; resolved threads fold to one line, an applied suggestion rewrites the diff in place, and Approve sends
/// the review with the comments added meanwhile, then turns into Approved.
/// Load <see cref="Pull"/>, <see cref="Files"/> and <see cref="Threads"/> from your git provider and send the review with
/// <see cref="SubmitReviewAction"/>; everything else happens in the card. With no data it shows a sample review.
/// <code>
/// &lt;arc:CodeReview Pull="{Binding Pull}" Files="{Binding Files}" Threads="{Binding Threads}" /&gt;
/// </code>
/// </summary>
[TemplatePart(RootPart, typeof(Avalonia.Controls.Panel))]
[PseudoClasses(CompactPseudoClass, ApprovedPseudoClass)]
public partial class CodeReview : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set below <see cref="CompactWidth"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the review is approved.</summary>
    public const string ApprovedPseudoClass = ":approved";

    #region Properties

    /// <summary>The pull request: title, number, author, branches and commit count. Null shows the sample pull request.</summary>
    public static readonly StyledProperty<CodeReviewPullRequest?> PullProperty =
        AvaloniaProperty.Register<CodeReview, CodeReviewPullRequest?>(nameof(Pull));

    /// <summary>
    /// The changed files, one tab each in this order. Null shows the sample's five files. Changing it starts the review
    /// over (viewed marks and open boxes are cleared).
    /// </summary>
    public static readonly StyledProperty<IList<CodeReviewFile>?> FilesProperty =
        AvaloniaProperty.Register<CodeReview, IList<CodeReviewFile>?>(nameof(Files));

    /// <summary>
    /// The existing conversations, each under a line of a file. The card updates them as the reviewer replies, resolves
    /// and applies suggestions, and adds the threads it starts. Null shows the sample's two threads.
    /// </summary>
    public static readonly StyledProperty<IList<CodeReviewThread>?> ThreadsProperty =
        AvaloniaProperty.Register<CodeReview, IList<CodeReviewThread>?>(nameof(Threads));

    /// <summary>The person reviewing: new comments and replies are written as them. Null uses the sample's reviewer.</summary>
    public static readonly StyledProperty<CodeReviewPerson?> ViewerProperty =
        AvaloniaProperty.Register<CodeReview, CodeReviewPerson?>(nameof(Viewer));

    /// <summary>
    /// The id of the file shown. Null (the default) shows the first file with an open conversation, or the first file.
    /// Two-way: choosing a tab writes it.
    /// </summary>
    public static readonly StyledProperty<string?> SelectedFileIdProperty =
        AvaloniaProperty.Register<CodeReview, string?>(nameof(SelectedFileId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Sends the approval with the comments added during the review. While the task runs the button shows progress; an
    /// exception shows "Try again" (and announces it). Null simulates a short submit.
    /// </summary>
    public static readonly StyledProperty<Func<CodeReviewSubmission, CancellationToken, Task>?> SubmitReviewActionProperty =
        AvaloniaProperty.Register<CodeReview, Func<CodeReviewSubmission, CancellationToken, Task>?>(nameof(SubmitReviewAction));

    /// <summary>Below this card width the header stacks, the progress moves above the tabs and one line-number column remains (560).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CodeReview, double>(nameof(CompactWidth), 560);

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<CodeReviewLabels> LabelsProperty =
        AvaloniaProperty.Register<CodeReview, CodeReviewLabels>(nameof(Labels), new CodeReviewLabels());

    /// <summary>Whether the review has been approved (Undo withdraws it).</summary>
    public static readonly DirectProperty<CodeReview, bool> IsApprovedProperty =
        AvaloniaProperty.RegisterDirect<CodeReview, bool>(nameof(IsApproved), o => o.IsApproved);

    /// <summary>How many files are marked viewed.</summary>
    public static readonly DirectProperty<CodeReview, int> ViewedCountProperty =
        AvaloniaProperty.RegisterDirect<CodeReview, int>(nameof(ViewedCount), o => o.ViewedCount);

    #endregion

    #region Events

    /// <summary>Raised when another file is shown.</summary>
    public static readonly RoutedEvent<CodeReviewSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the reviewer adds a comment on a line; the new thread is pending until the review is approved.</summary>
    public static readonly RoutedEvent<CodeReviewThreadEventArgs> CommentAddedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewThreadEventArgs>(nameof(CommentAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when a reply is posted in a thread.</summary>
    public static readonly RoutedEvent<CodeReviewReplyEventArgs> ReplyPostedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewReplyEventArgs>(nameof(ReplyPosted), RoutingStrategies.Bubble);

    /// <summary>Raised when a conversation is resolved or reopened (applying a suggestion resolves its thread).</summary>
    public static readonly RoutedEvent<CodeReviewThreadEventArgs> ResolvedChangedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewThreadEventArgs>(nameof(ResolvedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a suggestion is applied, with the file's new diff; commit the change on your server.</summary>
    public static readonly RoutedEvent<CodeReviewSuggestionAppliedEventArgs> SuggestionAppliedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewSuggestionAppliedEventArgs>(nameof(SuggestionApplied), RoutingStrategies.Bubble);

    /// <summary>Raised when a file is marked viewed or not viewed.</summary>
    public static readonly RoutedEvent<CodeReviewViewedChangedEventArgs> ViewedChangedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewViewedChangedEventArgs>(nameof(ViewedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after the approval was sent.</summary>
    public static readonly RoutedEvent<CodeReviewSubmittedEventArgs> ReviewSubmittedEvent =
        RoutedEvent.Register<CodeReview, CodeReviewSubmittedEventArgs>(nameof(ReviewSubmitted), RoutingStrategies.Bubble);

    /// <summary>Raised when Undo withdraws the approval.</summary>
    public static readonly RoutedEvent<CodeReviewSubmittedEventArgs> ApprovalWithdrawnEvent =
        RoutedEvent.Register<CodeReview, CodeReviewSubmittedEventArgs>(nameof(ApprovalWithdrawn), RoutingStrategies.Bubble);

    /// <summary>Raised when another file is shown.</summary>
    public event EventHandler<CodeReviewSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Raised when the reviewer adds a comment on a line; the new thread is pending until the review is approved.</summary>
    public event EventHandler<CodeReviewThreadEventArgs>? CommentAdded
    {
        add => AddHandler(CommentAddedEvent, value);
        remove => RemoveHandler(CommentAddedEvent, value);
    }

    /// <summary>Raised when a reply is posted in a thread.</summary>
    public event EventHandler<CodeReviewReplyEventArgs>? ReplyPosted
    {
        add => AddHandler(ReplyPostedEvent, value);
        remove => RemoveHandler(ReplyPostedEvent, value);
    }

    /// <summary>Raised when a conversation is resolved or reopened (applying a suggestion resolves its thread).</summary>
    public event EventHandler<CodeReviewThreadEventArgs>? ResolvedChanged
    {
        add => AddHandler(ResolvedChangedEvent, value);
        remove => RemoveHandler(ResolvedChangedEvent, value);
    }

    /// <summary>Raised after a suggestion is applied, with the file's new diff; commit the change on your server.</summary>
    public event EventHandler<CodeReviewSuggestionAppliedEventArgs>? SuggestionApplied
    {
        add => AddHandler(SuggestionAppliedEvent, value);
        remove => RemoveHandler(SuggestionAppliedEvent, value);
    }

    /// <summary>Raised when a file is marked viewed or not viewed.</summary>
    public event EventHandler<CodeReviewViewedChangedEventArgs>? ViewedChanged
    {
        add => AddHandler(ViewedChangedEvent, value);
        remove => RemoveHandler(ViewedChangedEvent, value);
    }

    /// <summary>Raised after the approval was sent.</summary>
    public event EventHandler<CodeReviewSubmittedEventArgs>? ReviewSubmitted
    {
        add => AddHandler(ReviewSubmittedEvent, value);
        remove => RemoveHandler(ReviewSubmittedEvent, value);
    }

    /// <summary>Raised when Undo withdraws the approval.</summary>
    public event EventHandler<CodeReviewSubmittedEventArgs>? ApprovalWithdrawn
    {
        add => AddHandler(ApprovalWithdrawnEvent, value);
        remove => RemoveHandler(ApprovalWithdrawnEvent, value);
    }

    #endregion

    private bool _isApproved;
    private int _viewedCount;

    /// <inheritdoc cref="PullProperty"/>
    public CodeReviewPullRequest? Pull { get => GetValue(PullProperty); set => SetValue(PullProperty, value); }

    /// <inheritdoc cref="FilesProperty"/>
    public IList<CodeReviewFile>? Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    /// <inheritdoc cref="ThreadsProperty"/>
    public IList<CodeReviewThread>? Threads { get => GetValue(ThreadsProperty); set => SetValue(ThreadsProperty, value); }

    /// <inheritdoc cref="ViewerProperty"/>
    public CodeReviewPerson? Viewer { get => GetValue(ViewerProperty); set => SetValue(ViewerProperty, value); }

    /// <inheritdoc cref="SelectedFileIdProperty"/>
    public string? SelectedFileId { get => GetValue(SelectedFileIdProperty); set => SetValue(SelectedFileIdProperty, value); }

    /// <inheritdoc cref="SubmitReviewActionProperty"/>
    public Func<CodeReviewSubmission, CancellationToken, Task>? SubmitReviewAction
    {
        get => GetValue(SubmitReviewActionProperty);
        set => SetValue(SubmitReviewActionProperty, value);
    }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CodeReviewLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsApprovedProperty"/>
    public bool IsApproved { get => _isApproved; private set => SetAndRaise(IsApprovedProperty, ref _isApproved, value); }

    /// <inheritdoc cref="ViewedCountProperty"/>
    public int ViewedCount { get => _viewedCount; private set => SetAndRaise(ViewedCountProperty, ref _viewedCount, value); }
}
