using System.Collections.Specialized;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AiSidePanel
{
    private readonly Dictionary<AiPanelTurn, AiPanelTurnView> _views = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private AiPanelSuggestionRow? _suggestionRow;
    private AiFadePresence? _suggestionFade;
    private AiPanelTurn? _animateNext;

    /// <summary>The views of the turns, oldest first (tests).</summary>
    internal IReadOnlyList<AiPanelTurnView> TurnViews => Turns.Where(_views.ContainsKey).Select(t => _views[t]).ToList();

    /// <summary>The view of <paramref name="turn"/>, when it is on screen.</summary>
    internal AiPanelTurnView? ViewOf(AiPanelTurn turn) => _views.GetValueOrDefault(turn);

    /// <summary>The row of unused suggestions after the newest answer, when it shows (tests).</summary>
    internal AiPanelSuggestionRow? FollowingSuggestions =>
        _suggestionRow is { IsVisible: true } row && _turnsPanel?.Children.Contains(row) == true ? row : null;

    /// <summary>The suggestions not sent yet, in their order.</summary>
    internal IReadOnlyList<string> UnusedSuggestions => Suggestions.Where(s => !_used.Contains(s)).Distinct().ToList();

    #region Turns

    private void OnTurnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null && _run is { } run && e.OldItems.Contains(run.Turn) && !Turns.Contains(run.Turn))
        {
            // The host took the running turn away: the answer stops without a trace.
            CancelRun(announce: false);
        }
        SyncThread();
    }

    /// <summary>Builds the views of every turn again (a new template).</summary>
    private void RebuildThread()
    {
        ClearThreadViews();
        SyncThread();
    }

    private void ClearThreadViews()
    {
        if (_turnsPanel is not null)
        {
            _turnsPanel.Children.Clear();
        }
        _views.Clear();
        _suggestionFade?.Dispose();
        _suggestionFade = null;
        _suggestionRow = null;
    }

    /// <summary>The views follow <see cref="Turns"/>: views of removed turns go, new turns get views, the order is kept.</summary>
    private void SyncThread()
    {
        if (_turnsPanel is null)
        {
            RefreshEmpty();
            return;
        }
        var keep = new HashSet<AiPanelTurn>(Turns, ReferenceEqualityComparer.Instance);
        foreach (var turn in _views.Keys.Where(t => !keep.Contains(t)).ToList())
        {
            _turnsPanel.Children.Remove(_views[turn]);
            _views.Remove(turn);
        }
        foreach (var turn in _requests.Keys.Where(t => !keep.Contains(t)).ToList())
        {
            _requests.Remove(turn);
        }
        for (var i = 0; i < Turns.Count; i++)
        {
            var turn = Turns[i];
            if (!_views.TryGetValue(turn, out var view))
            {
                view = new AiPanelTurnView(turn, Labels, AssistantName, animateIn: ReferenceEquals(turn, _animateNext));
                _views[turn] = view;
            }
            var at = _turnsPanel.Children.IndexOf(view);
            if (at != i)
            {
                if (at >= 0)
                {
                    _turnsPanel.Children.RemoveAt(at);
                }
                _turnsPanel.Children.Insert(i, view);
            }
        }
        RefreshEmpty();
        RefreshSuggestions();
    }

    /// <summary>The empty state shows while there are no turns (100ms out as the thread fades in over 160ms).</summary>
    private void RefreshEmpty()
    {
        var empty = Turns.Count == 0;
        PseudoClasses.Set(EmptyPseudoClass, empty);
        _emptyFade?.SetShown(empty);
        _threadFade?.SetShown(!empty);
    }

    #endregion

    #region Suggestions

    private void OnSuggestionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshSuggestions();

    /// <summary>
    /// The suggestions (spec F11): all of them in the empty thread; after an answer that ended (done or stopped) the unused
    /// ones follow it, in one row; none while an answer is read or written, after a failure, or once all were used.
    /// </summary>
    private void RefreshSuggestions()
    {
        _emptySuggestions?.SetPrompts(Suggestions.Distinct().ToList());
        if (_turnsPanel is null)
        {
            return;
        }
        var last = Turns.Count > 0 ? Turns[^1] : null;
        var unused = UnusedSuggestions;
        var show = last is { State: AiPanelTurnState.Done or AiPanelTurnState.Stopped } && unused.Count > 0;
        if (!show)
        {
            // A new question removes the row at once (no exit).
            if (_suggestionRow is not null)
            {
                _turnsPanel.Children.Remove(_suggestionRow);
            }
            return;
        }
        var row = EnsureSuggestionRow();
        row.SetPrompts(unused);
        var appear = !_turnsPanel.Children.Contains(row);
        if (appear)
        {
            _turnsPanel.Children.Add(row);
        }
        else if (_turnsPanel.Children[^1] != row)
        {
            _turnsPanel.Children.Remove(row);
            _turnsPanel.Children.Add(row);
        }
        if (appear)
        {
            // The row fades in with the answer's end (160ms, no movement).
            _suggestionFade?.Dispose();
            _suggestionFade = new AiFadePresence(row, shown: false);
            _suggestionFade.SetShown(true);
        }
    }

    private AiPanelSuggestionRow EnsureSuggestionRow()
    {
        if (_suggestionRow is null)
        {
            _suggestionRow = new AiPanelSuggestionRow();
            _suggestionRow.Classes.Add("following");
            AutomationProperties.SetName(_suggestionRow, Labels.Suggestions);
        }
        return _suggestionRow;
    }

    private void OnSuggestionInvoked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (e.Source is AiPanelSuggestionPill { Text: { Length: > 0 } prompt })
        {
            _ = AskAsync(prompt);
        }
    }

    #endregion
}
