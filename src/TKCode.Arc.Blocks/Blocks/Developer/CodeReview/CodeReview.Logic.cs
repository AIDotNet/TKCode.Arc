using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>The review's state and the reviewer's actions.</summary>
public partial class CodeReview : ICodeReviewHost
{
    private enum ApproveState
    {
        Idle,
        Busy,
        Approved,
        Failed
    }

    private List<CodeReviewFileState> _files = new();
    private List<CodeReviewThread> _threads = new();
    private readonly List<CodeReviewThread> _pending = new();
    private int _selected = -1;
    private ApproveState _approveState;
    private CancellationTokenSource? _submit;
    private CodeReviewSubmission? _lastSubmission;
    private int _newThreadId;

    /// <summary>Scales the simulated submit and the delays (tests set 0).</summary>
    internal double TimingScale { get; set; } = 1;

    private CodeReviewPullRequest EffectivePull => Pull ?? s_samplePull.Value;

    private static readonly Lazy<CodeReviewPullRequest> s_samplePull = new(CodeReviewDefaults.Pull);

    static CodeReview()
    {
        FocusableProperty.OverrideDefaultValue<CodeReview>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FilesProperty || change.Property == ThreadsProperty)
        {
            if (_root is not null)
            {
                Build();
            }
        }
        else if (change.Property == PullProperty || change.Property == LabelsProperty)
        {
            FillHeader();
            UpdateChrome(animate: false);
        }
        else if (change.Property == SelectedFileIdProperty)
        {
            var id = change.GetNewValue<string?>();
            var index = id is null ? DefaultIndex() : _files.FindIndex(f => f.File.Id == id);
            if (index >= 0 && index != _selected)
            {
                SelectIndex(index);
            }
        }
        else if (change.Property == CompactWidthProperty)
        {
            ApplyLayout(force: false);
        }
    }

    private void LoadData()
    {
        _submit?.Cancel();
        _files = (Files ?? CodeReviewDefaults.Files()).Select(f => new CodeReviewFileState(f)).ToList();
        _threads = Threads is { } given ? given.ToList() : CodeReviewDefaults.Threads();
        _pending.Clear();
        _approveState = ApproveState.Idle;
        IsApproved = false;
        PseudoClasses.Set(ApprovedPseudoClass, false);
        if (_approve is not null)
        {
            _approve.Content = Labels.Approve;
            _approve.Variant = ButtonVariant.Primary;
            _approve.IsLoading = false;
        }
        FillHeader();
        _tabs?.SetFiles(_files.Select(f => f.File).ToList());
        var index = SelectedFileId is { } id ? _files.FindIndex(f => f.File.Id == id) : -1;
        _selected = -1;
        SelectIndex(index >= 0 ? index : DefaultIndex());
        UpdateChrome(animate: false);
    }

    private int DefaultIndex()
    {
        for (var i = 0; i < _files.Count; i++)
        {
            var id = _files[i].Id;
            if (_threads.Any(t => t.FileId == id && !t.IsResolved))
            {
                return i;
            }
        }
        return _files.Count > 0 ? 0 : -1;
    }

    /// <summary>Shows file <paramref name="index"/>; the panel slides in the direction of travel.</summary>
    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _files.Count || _switch is null)
        {
            return;
        }
        var old = _selected;
        if (old == index)
        {
            return;
        }
        _selected = index;
        var panel = Panel(_files[index]);
        _switch.Show(panel, old < 0 ? 0 : Math.Sign(index - old));
        _tabs?.Select(index, animate: old >= 0);
        UpdateChrome(animate: true);
        var newFile = _files[index].File;
        SetCurrentValue(SelectedFileIdProperty, newFile.Id);
        RaiseEvent(new CodeReviewSelectionChangedEventArgs(SelectionChangedEvent, old >= 0 ? _files[old].File : null, newFile));
    }

    // ---- Viewed ----

    private void SetViewed(CodeReviewFileState file, bool viewed)
    {
        if (file.IsViewed == viewed)
        {
            return;
        }
        file.IsViewed = viewed;
        UpdateChrome(animate: true);
        _announcer?.Announce(Labels.ViewedAnnouncement(file.File.Path, viewed));
        RaiseEvent(new CodeReviewViewedChangedEventArgs(ViewedChangedEvent, file.File, viewed));
    }

    /// <summary>Marks a file viewed or not, as its checkbox does.</summary>
    public void SetFileViewed(string fileId, bool viewed)
    {
        var file = _files.FirstOrDefault(f => f.Id == fileId);
        if (file is null)
        {
            return;
        }
        if (_panels.TryGetValue(fileId, out var panel))
        {
            panel.ViewedToggle.SetChecked(viewed);
        }
        SetViewed(file, viewed);
    }

    // ---- ICodeReviewHost ----

    CodeReviewLabels ICodeReviewHost.Labels => Labels;

    CodeReviewPerson ICodeReviewHost.CurrentViewer => Viewer ?? CodeReviewDefaults.Viewer;

    CodeReviewPalette ICodeReviewHost.Palette => _palette;

    bool ICodeReviewHost.IsCompactLayout => _compact;

    string ICodeReviewHost.LanguageOf(string fileId) => _files.FirstOrDefault(f => f.Id == fileId)?.File.Language ?? string.Empty;

    IReadOnlyList<CodeReviewDiffLine> ICodeReviewHost.LinesOf(string fileId) =>
        _files.FirstOrDefault(f => f.Id == fileId)?.Lines ?? (IReadOnlyList<CodeReviewDiffLine>)Array.Empty<CodeReviewDiffLine>();

    void ICodeReviewHost.ApplySuggestion(CodeReviewThread thread, CodeReviewThreadView view) => ApplySuggestionCore(thread, view);

    void ICodeReviewHost.SetResolved(CodeReviewThread thread, bool resolved) => SetResolvedCore(thread, resolved);

    void ICodeReviewHost.PostReply(CodeReviewThread thread, string text)
    {
        var comment = new CodeReviewComment { Author = Viewer ?? CodeReviewDefaults.Viewer, Text = text, CreatedAt = DateTimeOffset.Now };
        thread.Comments.Add(comment);
        _announcer?.Announce(Labels.ReplyPostedAnnouncement);
        RaiseEvent(new CodeReviewReplyEventArgs(ReplyPostedEvent, thread, comment));
    }

    CodeReviewThread? ICodeReviewHost.PostComment(string fileId, string lineId, string label, string text, string? suggestion)
    {
        var thread = new CodeReviewThread
        {
            Id = $"new-{++_newThreadId}",
            FileId = fileId,
            AnchorLineId = lineId,
            Label = label,
            IsPending = !IsApproved,
            Comments = [new CodeReviewComment { Author = Viewer ?? CodeReviewDefaults.Viewer, Text = text, CreatedAt = DateTimeOffset.Now }],
            Suggestion = suggestion is null
                ? null
                : new CodeReviewSuggestion { LineIds = [lineId], NewLines = suggestion.Replace("\r\n", "\n").Split('\n').ToList() }
        };
        _threads.Add(thread);
        if (Threads is { IsReadOnly: false } list && !ReferenceEquals(list, _threads))
        {
            list.Add(thread);
        }
        if (thread.IsPending)
        {
            _pending.Add(thread);
        }
        if (_panels.TryGetValue(fileId, out var panel))
        {
            panel.Diff.PromoteComposer(lineId, thread);
            panel.Diff.Rebuild(animate: true);
        }
        UpdateChrome(animate: true);
        _announcer?.Announce(Labels.CommentAddedAnnouncement(label));
        RaiseEvent(new CodeReviewThreadEventArgs(CommentAddedEvent, thread));
        return thread;
    }

    void ICodeReviewHost.CloseComposer(string fileId, string lineId, bool focusLine)
    {
        if (_panels.TryGetValue(fileId, out var panel))
        {
            panel.Diff.CloseComposer(lineId, focusLine);
        }
    }

    // ---- Threads ----

    private void SetResolvedCore(CodeReviewThread thread, bool resolved, bool announce = true)
    {
        if (thread.IsResolved == resolved)
        {
            return;
        }
        thread.IsResolved = resolved;
        if (_panels.TryGetValue(thread.FileId, out var panel))
        {
            panel.Diff.ViewOf(thread)?.Refresh();
        }
        UpdateChrome(animate: true);
        if (announce)
        {
            _announcer?.Announce(resolved ? Labels.ResolvedAnnouncement : Labels.ReopenedAnnouncement);
        }
        RaiseEvent(new CodeReviewThreadEventArgs(ResolvedChangedEvent, thread));
    }

    private void ApplySuggestionCore(CodeReviewThread thread, CodeReviewThreadView? view)
    {
        if (thread.Suggestion is not { } suggestion || thread.IsSuggestionApplied)
        {
            return;
        }
        var file = _files.FirstOrDefault(f => f.Id == thread.FileId);
        if (file is null)
        {
            return;
        }
        var lines = CodeReviewDiff.Apply(file.Lines, suggestion, $"{thread.Id}-applied", out var newIds);
        if (lines is null)
        {
            return;
        }
        file.Lines = lines;
        thread.IsSuggestionApplied = true;
        thread.IsResolved = true;
        if (newIds.Count > 0)
        {
            thread.AnchorLineId = newIds[^1];
        }
        if (_panels.TryGetValue(file.Id, out var panel))
        {
            panel.Diff.Rebuild(animate: true, flash: newIds.ToHashSet(StringComparer.Ordinal));
            panel.UpdateStat(animate: true);
            (view ?? panel.Diff.ViewOf(thread))?.OnApplied();
        }
        UpdateChrome(animate: true);
        _announcer?.Announce(Labels.ResolvedAnnouncement);
        RaiseEvent(new CodeReviewSuggestionAppliedEventArgs(SuggestionAppliedEvent, thread, file.File, lines));
        RaiseEvent(new CodeReviewThreadEventArgs(ResolvedChangedEvent, thread));
    }

    /// <summary>Applies a thread's suggestion, as its Apply button does.</summary>
    public void ApplySuggestion(CodeReviewThread thread) => ApplySuggestionCore(thread, null);

    /// <summary>Resolves or reopens a thread, as its buttons do.</summary>
    public void SetThreadResolved(CodeReviewThread thread, bool resolved) => SetResolvedCore(thread, resolved);

    // ---- Approve ----

    private void OnApproveClicked()
    {
        switch (_approveState)
        {
            case ApproveState.Idle:
            case ApproveState.Failed:
                _ = Approve();
                break;
        }
    }

    /// <summary>Sends the approval with the pending comments, as the Approve button does.</summary>
    public async Task Approve()
    {
        if (_approveState is ApproveState.Busy or ApproveState.Approved || _approve is null)
        {
            return;
        }
        _approveState = ApproveState.Busy;
        _approve.IsLoading = true;
        var comments = _pending.ToList();
        var submission = new CodeReviewSubmission(string.Empty, comments);
        _submit?.Cancel();
        var cts = new CancellationTokenSource();
        _submit = cts;
        try
        {
            if (SubmitReviewAction is { } action)
            {
                await action(submission, cts.Token);
            }
            else if (TimingScale > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(CodeReviewMotion.SimulatedSubmit.TotalMilliseconds * TimingScale), cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_submit, cts))
            {
                return;
            }
            _approveState = ApproveState.Failed;
            _approve.IsLoading = false;
            _approve.Content = Labels.TryAgain;
            _announcer?.Announce(Labels.FailedAnnouncement);
            return;
        }
        if (!ReferenceEquals(_submit, cts))
        {
            return;
        }
        _approveState = ApproveState.Approved;
        _approve.IsLoading = false;
        _approve.Content = CodeReviewButtons.CheckLabel(Labels.Approved);
        CodeReviewThreadView.ApplyApprovedLook(_approve);
        foreach (var thread in comments)
        {
            thread.IsPending = false;
        }
        _pending.Clear();
        foreach (var panel in _panels.Values)
        {
            panel.Diff.RefreshThreads();
        }
        _lastSubmission = submission;
        UpdateStatusText(comments.Count);
        if (_statusBand is not null)
        {
            _statusBand.IsExpanded = true;
        }
        IsApproved = true;
        PseudoClasses.Set(ApprovedPseudoClass, true);
        _announcer?.Announce(Labels.ApprovedAnnouncement(comments.Count));
        RaiseEvent(new CodeReviewSubmittedEventArgs(ReviewSubmittedEvent, submission));
    }

    /// <summary>Withdraws the approval, as Undo does.</summary>
    public void Withdraw()
    {
        if (_approveState != ApproveState.Approved || _approve is null)
        {
            return;
        }
        _approveState = ApproveState.Idle;
        _approve.ClearValue(BackgroundProperty);
        _approve.ClearValue(ForegroundProperty);
        _approve.ClearValue(BorderThicknessProperty);
        _approve.Content = Labels.Approve;
        _approve.Variant = ButtonVariant.Primary;
        if (_statusBand is not null)
        {
            _statusBand.IsExpanded = false;
        }
        IsApproved = false;
        PseudoClasses.Set(ApprovedPseudoClass, false);
        _announcer?.Announce(Labels.WithdrawnAnnouncement);
        if (_lastSubmission is { } submission)
        {
            RaiseEvent(new CodeReviewSubmittedEventArgs(ApprovalWithdrawnEvent, submission));
        }
        if (_approve.IsKeyboardFocusWithin)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => _approve?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
    }
}
