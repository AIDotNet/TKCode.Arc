using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class AiChat
{
    private readonly Dictionary<AiChatTurn, AiChatTurnView> _views = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _watched = new(ReferenceEqualityComparer.Instance);
    private List<AiChatBranchEntry> _branch = new();
    private AiChatConversation? _shown;
    private int _shownFrom;
    private int _batch;
    private bool _refreshQueued;
    private bool _switchPending;
    private bool _creating;
    private bool _earlierQueued;

    #region Test surface

    /// <summary>The visible branch as shown (tests).</summary>
    internal IReadOnlyList<AiChatBranchEntry> Branch => _branch;

    /// <summary>The turn views in thread order (tests).</summary>
    internal IReadOnlyList<AiChatTurnView> TurnViews =>
        _turns?.Children.OfType<AiChatTurnView>().ToList() ?? (IReadOnlyList<AiChatTurnView>)Array.Empty<AiChatTurnView>();

    /// <summary>The view of <paramref name="turn"/>, when it has one (tests).</summary>
    internal AiChatTurnView? ViewOf(AiChatTurn turn) => _views.TryGetValue(turn, out var view) ? view : null;

    /// <summary>The index in <see cref="Branch"/> of the first turn that has a view (older ones are made when scrolled to).</summary>
    internal int FirstShownIndex => _shownFrom;

    #endregion

    #region Switching conversations

    private void OnCurrentConversationChanged(AiChatConversation? previous, AiChatConversation? next)
    {
        if (_titlePill is not null)
        {
            _titlePill.Current = next;
        }
        UpdateTitle();
        if (_turns is null)
        {
            return;
        }
        if (_creating)
        {
            // The first message of the empty chat made this conversation: it shows at once, the empty state fades out.
            ShowConversationNow();
            return;
        }
        if (_shown is not null && _branch.Count > 0)
        {
            _scroller?.SaveOffset(_shown);
        }
        var leavingEmpty = _branch.Count == 0;
        if (leavingEmpty && AiChatBranch.Visible(next).Count == 0)
        {
            // Empty to empty: nothing on screen changes.
            _shown = next;
            RefreshBranch(animate: false);
            UpdateBusy();
            return;
        }
        if (_paneFade is null)
        {
            ShowConversationNow();
            return;
        }
        // Arc 5.8: the thread fades out (100ms), the other conversation comes in (160ms, rising 4px).
        _switchPending = true;
        if (leavingEmpty && _empty is not null)
        {
            _empty.IsShown = false;
        }
        if (!_paneFade.IsShown)
        {
            // Still fading out from an earlier switch: that fade's end shows the newest conversation.
            return;
        }
        _paneFade.SetShown(false);
    }

    private void OnPaneExited(object? sender, EventArgs e)
    {
        if (!_switchPending)
        {
            return;
        }
        _switchPending = false;
        ShowConversationNow();
        _paneFade?.SetShown(true);
    }

    /// <summary>Puts <see cref="CurrentConversation"/> on screen without motion (its turns at their saved offset; finished replies are not replayed).</summary>
    private void ShowConversationNow()
    {
        if (_turns is null)
        {
            return;
        }
        ClearThread();
        _shown = CurrentConversation;
        RefreshBranch(animate: false);
        if (_shown is not null && _branch.Count > 0)
        {
            _scroller?.RestoreOffset(_shown);
        }
        UpdateBusy();
    }

    #endregion

    #region The visible branch

    /// <summary>Defers branch refreshes while the chat changes several lists of the tree at once.</summary>
    private BatchScope Batch()
    {
        _batch++;
        return new BatchScope(this);
    }

    private readonly struct BatchScope(AiChat owner) : IDisposable
    {
        public void Dispose()
        {
            owner._batch--;
            if (owner._batch == 0 && owner._refreshQueued)
            {
                owner._refreshQueued = false;
                owner.RefreshBranch(animate: true);
            }
        }
    }

    /// <summary>
    /// Brings the thread in line with the visible branch of the shown conversation: views of turns that stay are kept (a
    /// turn whose version changed binds the new one and fades it in), views of turns that left go, new turns get a view
    /// that fades in. A long conversation first gets views for its newest <see cref="InitialTurns"/> turns only.
    /// </summary>
    private void RefreshBranch(bool animate)
    {
        if (_batch > 0)
        {
            _refreshQueued = true;
            return;
        }
        if (_turns is null)
        {
            return;
        }
        var branch = AiChatBranch.Visible(_shown);
        // Keep the oldest turn that has a view; otherwise show the newest turns.
        var from = -1;
        if (_shownFrom < _branch.Count)
        {
            var first = _branch[_shownFrom].Turn;
            from = branch.FindIndex(entry => ReferenceEquals(entry.Turn, first));
        }
        if (from < 0)
        {
            var keep = Math.Max(InitialTurns, _branch.Count - _shownFrom);
            from = Math.Max(0, branch.Count - Math.Max(1, keep));
        }
        _branch = branch;
        _shownFrom = Math.Min(from, Math.Max(0, branch.Count - 1));
        if (branch.Count == 0)
        {
            _shownFrom = 0;
        }

        var wanted = new List<AiChatTurnView>(branch.Count - _shownFrom);
        var entered = new List<AiChatTurnView>();
        var swapped = new List<AiChatTurnView>();
        var live = new HashSet<AiChatTurn>(ReferenceEqualityComparer.Instance);
        for (var i = _shownFrom; i < branch.Count; i++)
        {
            var entry = branch[i];
            live.Add(entry.Turn);
            if (_views.TryGetValue(entry.Turn, out var view))
            {
                if (!ReferenceEquals(view.Version, entry.Version))
                {
                    swapped.Add(view);
                }
                view.Bind(entry.Turn, entry.Version);
            }
            else
            {
                view = CreateView(entry);
                entered.Add(view);
            }
            wanted.Add(view);
        }
        foreach (var (turn, view) in _views.ToList())
        {
            if (!live.Contains(turn))
            {
                RemoveView(turn, view);
            }
        }
        PlaceViews(wanted);
        for (var i = 0; i < wanted.Count; i++)
        {
            wanted[i].IsLast = i == wanted.Count - 1;
            wanted[i].SetPosition(_shownFrom + i + 1, branch.Count);
        }
        if (animate)
        {
            foreach (var view in entered)
            {
                view.PlayEnter();
            }
            foreach (var view in swapped)
            {
                view.PlayVersionSwap();
            }
        }
        WatchBranch();
        PseudoClasses.Set(EmptyPseudoClass, branch.Count == 0);
        if (_empty is not null && !_switchPending)
        {
            _empty.IsShown = branch.Count == 0;
        }
    }

    private AiChatTurnView CreateView(AiChatBranchEntry entry)
    {
        var view = new AiChatTurnView { Owner = this, IsNarrow = _isNarrow, IsTouch = _isTouch };
        view.Bind(entry.Turn, entry.Version);
        _views[entry.Turn] = view;
        return view;
    }

    private void RemoveView(AiChatTurn turn, AiChatTurnView view)
    {
        _views.Remove(turn);
        view.Unbind();
        _turns?.Children.Remove(view);
        if (view.ReplyView?.Reply is { } reply)
        {
            OnReplyViewGone(reply);
        }
    }

    /// <summary>Puts the views in the panel in order, touching only the children from the first difference on.</summary>
    private void PlaceViews(List<AiChatTurnView> wanted)
    {
        if (_turns is null)
        {
            return;
        }
        var children = _turns.Children;
        var same = 0;
        while (same < wanted.Count && same < children.Count && ReferenceEquals(children[same], wanted[same]))
        {
            same++;
        }
        if (same == wanted.Count && same == children.Count)
        {
            return;
        }
        while (children.Count > same)
        {
            children.RemoveAt(children.Count - 1);
        }
        for (var i = same; i < wanted.Count; i++)
        {
            children.Add(wanted[i]);
        }
    }

    private void ClearThread()
    {
        foreach (var (turn, view) in _views.ToList())
        {
            RemoveView(turn, view);
        }
        _turns?.Children.Clear();
        _branch = new List<AiChatBranchEntry>();
        _shownFrom = 0;
        Unwatch();
    }

    #endregion

    #region Watching the tree

    /// <summary>
    /// Follows every list and object the visible branch runs through: the conversation's turns, each turn's versions and
    /// selected version, each shown version's prompt, reply and continuation, and each reply's status (for
    /// <see cref="IsBusy"/>). A change refreshes the branch.
    /// </summary>
    private void WatchBranch()
    {
        var wanted = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (_shown is not null)
        {
            wanted.Add(_shown);
            wanted.Add(_shown.Turns);
        }
        foreach (var entry in _branch)
        {
            wanted.Add(entry.Owner);
            wanted.Add(entry.Turn);
            wanted.Add(entry.Turn.Versions);
            wanted.Add(entry.Version);
            wanted.Add(entry.Version.Continuation);
            wanted.Add(entry.Version.Reply);
        }
        foreach (var item in _watched.ToList())
        {
            if (!wanted.Contains(item))
            {
                Subscribe(item, false);
                _watched.Remove(item);
            }
        }
        foreach (var item in wanted)
        {
            if (_watched.Add(item))
            {
                Subscribe(item, true);
            }
        }
    }

    private void Unwatch()
    {
        foreach (var item in _watched)
        {
            Subscribe(item, false);
        }
        _watched.Clear();
    }

    private void Subscribe(object item, bool on)
    {
        switch (item)
        {
            case INotifyCollectionChanged list:
                if (on)
                {
                    list.CollectionChanged += OnBranchListChanged;
                }
                else
                {
                    list.CollectionChanged -= OnBranchListChanged;
                }
                break;
            case AiChatReply reply:
                if (on)
                {
                    reply.PropertyChanged += OnBranchReplyChanged;
                }
                else
                {
                    reply.PropertyChanged -= OnBranchReplyChanged;
                }
                break;
            case AvaloniaObject node:
                if (on)
                {
                    node.PropertyChanged += OnBranchNodeChanged;
                }
                else
                {
                    node.PropertyChanged -= OnBranchNodeChanged;
                }
                break;
        }
    }

    private void OnBranchListChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRefresh();

    private void OnBranchNodeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChatConversation.TitleProperty)
        {
            UpdateTitle();
            return;
        }
        if (e.Property == AiChatTurn.SelectedVersionIndexProperty || e.Property == AiChatTurnVersion.PromptProperty ||
            e.Property == AiChatTurnVersion.ReplyProperty)
        {
            QueueRefresh();
        }
    }

    private void OnBranchReplyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AiChatReply.StatusProperty || sender is not AiChatReply reply)
        {
            return;
        }
        UpdateBusy();
        if (!_runs.ContainsKey(reply))
        {
            // A reply your code drives: say how it ended.
            switch (e.GetNewValue<AiChatReplyStatus>())
            {
                case AiChatReplyStatus.Done when e.GetOldValue<AiChatReplyStatus>() is AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming:
                    Announce(Labels.ReplyFinished);
                    break;
                case AiChatReplyStatus.Failed:
                    Announce(Labels.ReplyFailedAnnounce(reply.ErrorMessage ?? string.Empty));
                    break;
            }
        }
    }

    private void QueueRefresh()
    {
        if (_batch > 0)
        {
            _refreshQueued = true;
            return;
        }
        RefreshBranch(animate: true);
        UpdateTitle();
        UpdateBusy();
    }

    #endregion

    #region Older turns on demand

    /// <summary>Near the top of a long conversation, the older turns get their views (a batch at a time).</summary>
    private void OnThreadScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_shownFrom <= 0 || _earlierQueued || _scroller?.Scroll is not { } scroll)
        {
            return;
        }
        // Only once the person left the bottom (or a conversation opened scrolled up), never on the way in.
        if (!IsFollowingLatest && scroll.Offset.Y < scroll.Viewport.Height)
        {
            _earlierQueued = true;
            // Out of the layout pass that reported the scroll.
            Dispatcher.UIThread.Post(ShowEarlier, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Makes views for up to <see cref="EarlierTurns"/> older turns above the first one shown and keeps what the person
    /// looks at in place: the offset grows by the height that came in above.
    /// </summary>
    internal void ShowEarlier()
    {
        _earlierQueued = false;
        if (_shownFrom <= 0 || _turns is null)
        {
            return;
        }
        var scroll = _scroller?.Scroll;
        var extent = scroll?.Extent.Height ?? 0;
        var offset = scroll?.Offset.Y ?? 0;
        var from = Math.Max(0, _shownFrom - Math.Max(1, EarlierTurns));
        var created = new List<AiChatTurnView>();
        for (var i = from; i < _shownFrom; i++)
        {
            created.Add(CreateView(_branch[i]));
        }
        _turns.Children.InsertRange(0, created);
        _shownFrom = from;
        var views = TurnViews;
        for (var i = 0; i < views.Count; i++)
        {
            views[i].SetPosition(_shownFrom + i + 1, _branch.Count);
        }
        WatchBranch();
        if (scroll is not null)
        {
            scroll.UpdateLayout();
            var grown = scroll.Extent.Height - extent;
            if (grown > 0)
            {
                scroll.Offset = new Vector(scroll.Offset.X, offset + grown);
            }
        }
    }

    #endregion
}
