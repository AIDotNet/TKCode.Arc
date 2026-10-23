using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class ModelArena
{
    /// <summary>Arc's series colors by provider, in the order the providers first appear in the pool (spec 3.8, 10.1).</summary>
    private static readonly int[] s_tones = [3, 4, 1, 2];

    private IReadOnlyList<RankedBarItem>? _shownItems;
    private bool _everOpened;
    private bool _popupHooked;

    #region Public methods

    /// <summary>
    /// Votes on the round (only while the vote is open): the standings record it (A or B better: a win and a loss; Tie: a
    /// tie each; Both bad: a loss each), both models are revealed, the vote strip shows the result and Next round, and
    /// <see cref="VoteCast"/> is raised and <see cref="VoteCommand"/> run with the <see cref="ArenaVote"/>.
    /// </summary>
    /// <param name="choice">The choice.</param>
    public void Vote(ArenaChoice choice)
    {
        if (Phase != ArenaPhase.Voting || _pair is not { } pair)
        {
            return;
        }
        var winner = choice switch
        {
            ArenaChoice.A => pair.A,
            ArenaChoice.B => pair.B,
            _ => null
        };
        // Before the standings change: an open popover shows the change at once and clears the mark again.
        HasUnseenResult = true;
        Score(pair.A, choice switch { ArenaChoice.A => 1, ArenaChoice.Tie => 0, _ => -1 });
        Score(pair.B, choice switch { ArenaChoice.B => 1, ArenaChoice.Tie => 0, _ => -1 });
        var vote = new ArenaVote(Round, _roundPrompt, pair.A, pair.B, choice, winner, PaneA.Metrics, PaneB.Metrics);
        _voteDue = null;
        _revealedByVote = true;
        LastVote = vote;
        SetPhase(ArenaPhase.Voted);
        SyncReveal();
        SyncVoteBar();
        var labels = Labels;
        var result = winner is not null
            ? labels.ResultChose(winner.Name)
            : choice == ArenaChoice.Tie ? labels.ResultTie : labels.ResultBothBad;
        Announce(labels.AnnounceVoted(result, pair.A.Name, pair.B.Name));
        RaiseEvent(new ArenaVoteEventArgs(VoteCastEvent, this, vote));
        if (VoteCommand is { } command && command.CanExecute(vote))
        {
            command.Execute(vote);
        }
    }

    /// <summary>Empties <see cref="Standings"/> (the popover's Reset does this) and raises <see cref="LeaderboardReset"/>.</summary>
    public void ResetLeaderboard()
    {
        var had = Standings.Count > 0;
        Standings.Clear();
        _shownItems = [];
        HasUnseenResult = false;
        if (IsLeaderboardOpen)
        {
            FillStandings(replay: false);
        }
        if (!had)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(LeaderboardResetEvent, this));
        Announce(Labels.AnnounceReset);
    }

    #endregion

    #region Scoring

    /// <summary>Adds one result to a model's record: 1 a win, 0 a tie, −1 a loss.</summary>
    private void Score(AiModel model, int outcome)
    {
        Standings.TryGetValue(model.Id, out var record);
        Standings[model.Id] = outcome switch
        {
            > 0 => record with { Wins = record.Wins + 1 },
            0 => record with { Ties = record.Ties + 1 },
            _ => record with { Losses = record.Losses + 1 }
        };
    }

    /// <summary>The votes behind the standings: every vote gives two models one game each.</summary>
    private int VoteCount()
    {
        var games = 0;
        foreach (var entry in Standings)
        {
            games += Math.Max(0, entry.Value.Games);
        }
        return (games + 1) / 2;
    }

    private bool HasGames()
    {
        foreach (var entry in Standings)
        {
            if (entry.Value.Games > 0)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Vote strip

    /// <summary>The strip follows the phase: waiting while a model runs (and for a moment after both are done), then the vote, the result, the stop or the failure.</summary>
    private void SyncVoteBar()
    {
        if (_voteBar is null)
        {
            return;
        }
        SyncVoteTexts();
        _voteBar.Kind = Phase switch
        {
            ArenaPhase.Running => ArenaVoteKind.Pending,
            ArenaPhase.Voting => _voteDue is null ? ArenaVoteKind.Vote : ArenaVoteKind.Pending,
            ArenaPhase.Voted => ArenaVoteKind.Voted,
            ArenaPhase.Stopped => ArenaVoteKind.Stopped,
            ArenaPhase.Failed => ArenaVoteKind.Failed,
            _ => ArenaVoteKind.Hidden
        };
    }

    /// <summary>
    /// The strip's changing words: the count of complete answers, the failure (which model, or both), and after the vote the
    /// result with the winner's mark and name. Before the vote nothing in it names a model.
    /// </summary>
    private void SyncVoteTexts()
    {
        if (_voteBar is null)
        {
            return;
        }
        var labels = Labels;
        _voteBar.PendingCountText = labels.WaitingCount(CompleteCount());
        var a = PaneA.Status == ArenaPaneStatus.Failed;
        var b = PaneB.Status == ArenaPaneStatus.Failed;
        _voteBar.FailedText = Phase == ArenaPhase.Failed && (a || b)
            ? labels.FailedRow(a && b ? labels.BothModels : labels.PaneName(a ? "A" : "B"))
            : null;
        if (Phase == ArenaPhase.Voted && LastVote is { } vote)
        {
            var winner = vote.Winner;
            _voteBar.ResultPrefixText = winner is null ? null : labels.ResultChosePrefix;
            _voteBar.ResultName = winner?.Name;
            _voteBar.ResultMark = winner?.Mark;
            _voteBar.ResultMarkColor = winner?.MarkColor;
            _voteBar.ResultText = winner is not null
                ? labels.ResultChose(winner.Name)
                : vote.Choice == ArenaChoice.Tie ? labels.ResultTie : labels.ResultBothBad;
            return;
        }
        _voteBar.ResultPrefixText = null;
        _voteBar.ResultName = null;
        _voteBar.ResultMark = null;
        _voteBar.ResultMarkColor = null;
        _voteBar.ResultText = null;
    }

    private int CompleteCount() => (IsComplete(PaneA.Status) ? 1 : 0) + (IsComplete(PaneB.Status) ? 1 : 0);

    #endregion

    #region Standings popover

    private void OnLeaderboardClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsLeaderboardOpenProperty, !IsLeaderboardOpen);
    }

    /// <summary>
    /// Opens or closes the popover at the Leaderboard button. Its rows are filled before it shows (so they do not crossfade
    /// in) and dropped once it has closed: the standings exist in the trees only while they are on screen.
    /// </summary>
    private void SyncLeaderboardOpen()
    {
        PseudoClasses.Set(LeaderboardOpenPseudoClass, IsLeaderboardOpen);
        if (_popover is null || _leaderboard is null)
        {
            return;
        }
        if (IsLeaderboardOpen)
        {
            if (_popover.IsOpen)
            {
                return;
            }
            FillStandings(replay: true);
            _popover.ShowAt(_leaderboard);
            HookPopup();
            if (_popover.Surface is { } surface)
            {
                // The standings bring their own 14 16 8 inside the 1px border.
                surface.Padding = default;
            }
        }
        else if (_popover.IsOpen)
        {
            _popover.Close();
        }
    }

    private void HookPopup()
    {
        if (_popupHooked || _popover?.Popup is not { } popup)
        {
            return;
        }
        _popupHooked = true;
        popup.Closed += OnPopupClosed;
    }

    private void OnPopoverPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Popover.IsOpenProperty && _popover is not null && _popover.IsOpen != IsLeaderboardOpen)
        {
            // Escape or a press outside closed it (the popover returns the focus to the button after Escape).
            SetCurrentValue(IsLeaderboardOpenProperty, _popover.IsOpen);
        }
    }

    /// <summary>The focus goes to the popover's body, Arc's dialog element (spec 6.2).</summary>
    private void OnPopoverOpened(object? sender, EventArgs e) => _standings?.FocusDefault();

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (!IsLeaderboardOpen && _standings is not null)
        {
            _standings.Items = null;
        }
    }

    private void OnResetRequested(object? sender, EventArgs e) => ResetLeaderboard();

    private void OnStandingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsLeaderboardOpen)
        {
            FillStandings(replay: false);
        }
    }

    /// <summary>
    /// The popover's rows, count and Reset from <see cref="Standings"/>. When it opens (<paramref name="replay"/>): the very
    /// first time the bars grow from zero (ai.md section 8: Arc's docs say so, its preview jumps), and after a vote the rows
    /// replay from the list shown last time; otherwise they just show.
    /// </summary>
    private void FillStandings(bool replay)
    {
        if (_standings is null)
        {
            return;
        }
        var items = BuildItems();
        _standings.VotesText = Labels.Votes(VoteCount());
        _standings.CanReset = HasGames();
        _standings.Items = items;
        if (replay && items.Count > 0)
        {
            if (!_everOpened)
            {
                _standings.Replay([]);
            }
            else if (HasUnseenResult && _shownItems is { } previous)
            {
                _standings.Replay(previous);
            }
        }
        if (replay)
        {
            _everOpened = true;
        }
        _shownItems = items;
        HasUnseenResult = false;
    }

    /// <summary>
    /// Models with at least one game, by win rate (ties broken by wins, then by name), at most <see cref="LeaderboardSize"/>;
    /// entries for models that are not contestants are left out (there is no name or mark to show).
    /// </summary>
    private List<RankedBarItem> BuildItems()
    {
        var rows = new List<(AiModel Model, ArenaRecord Record)>();
        foreach (var entry in Standings)
        {
            if (entry.Value.Games <= 0 || FindContestant(entry.Key) is not { } model)
            {
                continue;
            }
            rows.Add((model, entry.Value));
        }
        rows.Sort(static (x, y) =>
        {
            var rate = y.Record.WinRate.CompareTo(x.Record.WinRate);
            if (rate != 0)
            {
                return rate;
            }
            var wins = y.Record.Wins.CompareTo(x.Record.Wins);
            return wins != 0 ? wins : string.Compare(x.Model.Name, y.Model.Name, StringComparison.CurrentCulture);
        });
        var count = LeaderboardSize > 0 ? Math.Min(LeaderboardSize, rows.Count) : rows.Count;
        var labels = Labels;
        var items = new List<RankedBarItem>(count);
        for (var i = 0; i < count; i++)
        {
            var (model, record) = rows[i];
            var rate = record.WinRate;
            var percent = (int)Math.Round(Math.Clamp(rate, 0, 1) * 100, MidpointRounding.AwayFromZero);
            items.Add(new RankedBarItem(model.Id, model.Name, model.Mark, model.MarkColor, ToneOf(model), rate,
                labels.StandingRow(i + 1, model.Name, percent)));
        }
        return items;
    }

    private AiModel? FindContestant(string id)
    {
        foreach (var model in Contestants)
        {
            if (string.Equals(model.Id, id, StringComparison.Ordinal))
            {
                return model;
            }
        }
        return null;
    }

    /// <summary>The bar's series tone: one per provider, in the order providers first appear in <see cref="Contestants"/>.</summary>
    private int ToneOf(AiModel model)
    {
        var key = model.ProviderId ?? model.Id;
        var seen = new List<string>();
        foreach (var contestant in Contestants)
        {
            var provider = contestant.ProviderId ?? contestant.Id;
            if (seen.Contains(provider))
            {
                continue;
            }
            if (string.Equals(provider, key, StringComparison.Ordinal))
            {
                return s_tones[seen.Count % s_tones.Length];
            }
            seen.Add(provider);
        }
        return s_tones[seen.Count % s_tones.Length];
    }

    #endregion
}
