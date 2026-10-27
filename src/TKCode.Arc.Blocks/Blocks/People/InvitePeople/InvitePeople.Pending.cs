using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class InvitePeople
{
    /// <summary>An accepted row stays this long before it leaves and the avatar flies to the team (1.2 s).</summary>
    internal static readonly TimeSpan JoinedHold = TimeSpan.FromMilliseconds(1200);

    /// <summary>"Sent again" shows this long on a resend button (2 s).</summary>
    internal static readonly TimeSpan ResentHold = TimeSpan.FromMilliseconds(2000);

    /// <summary>Rows still sending when the send completes turn "sent" this far apart (160 ms).</summary>
    internal static readonly TimeSpan DeliveryStagger = TimeSpan.FromMilliseconds(160);

    private readonly Dictionary<InvitePeoplePendingInvite, InvitePeoplePendingRow> _pendingRowMap = new();
    private readonly Dictionary<InvitePeoplePendingInvite, Rect?> _rowFlights = new();
    private readonly HashSet<InvitePeoplePendingInvite> _observedInvites = new();
    private readonly List<DispatcherTimer> _timers = new();
    private CancellationTokenSource _lifetime = new();
    private bool _quiet;
    private bool _teamArrival;
    private int _attempt;
    private bool _captured;
    private List<InvitePeoplePerson> _startMembers = new();
    private List<(InvitePeoplePendingInvite Invite, InvitePeopleInviteStatus Status, string? SentText, InvitePeopleRole? Role)> _startPending = new();
    private InvitePeoplePlan? _startPlan;

    /// <summary>The row of <paramref name="invite"/>.</summary>
    internal InvitePeoplePendingRow? RowOf(InvitePeoplePendingInvite invite) =>
        _pendingRowMap.TryGetValue(invite, out var row) ? row : null;

    #region Public methods

    /// <summary>Marks an invite as delivered ("Sent just now").</summary>
    public void MarkDelivered(InvitePeoplePendingInvite invite)
    {
        if (invite.Status == InvitePeopleInviteStatus.Sending)
        {
            invite.Status = InvitePeopleInviteStatus.Sent;
        }
    }

    /// <summary>
    /// Marks an invite as accepted: the row shows "Joined" and is announced, and after a moment it leaves while the
    /// avatar flies into the team at the front of <see cref="Members"/> (raising <see cref="InviteJoined"/>).
    /// </summary>
    public void MarkJoined(InvitePeoplePendingInvite invite)
    {
        if (!PendingInvites.Contains(invite) || invite.Status == InvitePeopleInviteStatus.Joined)
        {
            return;
        }
        invite.Status = InvitePeopleInviteStatus.Joined;
        Announce(Labels.JoinedAnnouncement(invite.Person?.DisplayName ?? string.Empty, WorkspaceName ?? string.Empty));
        Schedule(JoinedHold, () => CompleteJoin(invite));
    }

    /// <summary>Sends the chips, as the Send button does (nothing happens while chips need fixing or seats run over).</summary>
    public Task SendAsync() => TrySend();

    /// <summary>
    /// Puts the block back as it was when it first appeared: the field empties, the team, the pending invites and the
    /// plan return to their first values, timers stop and running actions are cancelled. Raises <see cref="ResetPerformed"/>.
    /// </summary>
    public void Reset()
    {
        CancelWork();
        CloseSuggestions();
        CloseRoleMenu(returnFocus: false);
        _flights?.LandAll();
        _quiet = true;
        try
        {
            _entries.Clear();
            _armed = null;
            RebuildChips();
            _chips?.Settle();
            SetInputText(string.Empty);
            RestoreHint();
            if (_captured)
            {
                Members.Clear();
                Members.AddRange(_startMembers);
                PendingInvites.Clear();
                foreach (var (invite, status, sentText, role) in _startPending)
                {
                    invite.Status = status;
                    invite.SentText = sentText;
                    invite.Role = role;
                    PendingInvites.Add(invite);
                }
                SetCurrentValue(CurrentPlanProperty, _startPlan);
            }
            _attempt = 0;
        }
        finally
        {
            _quiet = false;
        }
        _roster = null;
        UpdateSeats();
        UpdateErrors();
        RaiseEntriesChanged();
        Announce(Labels.ResetDone);
        RaiseEvent(new RoutedEventArgs(ResetPerformedEvent));
    }

    /// <summary>Sends an invite again, as its Resend button does (only for invites that were sent).</summary>
    public Task ResendAsync(InvitePeoplePendingInvite invite) => RowOf(invite) is { } row ? ResendRow(row) : Task.CompletedTask;

    /// <summary>Revokes an invite, as its × button does: the row leaves and the seat is free again.</summary>
    public Task RevokeAsync(InvitePeoplePendingInvite invite) => RevokeInvite(invite);

    /// <summary>Moves to the next plan, as the upgrade button does.</summary>
    public Task UpgradeAsync() => TryUpgrade();

    #endregion

    #region State, timers

    private void CaptureStartState()
    {
        if (_captured)
        {
            return;
        }
        _captured = true;
        _startMembers = Members.ToList();
        _startPending = PendingInvites.Select(i => (i, i.Status, i.SentText, i.Role)).ToList();
        _startPlan = CurrentPlan;
    }

    private void CancelWork()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();
        foreach (var timer in _timers)
        {
            timer.Stop();
        }
        _timers.Clear();
        _timerActions.Clear();
        _summaryTimer?.Stop();
        _summaryTimer = null;
    }

    private void Schedule(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer { Interval = delay };
        _timerActions[timer] = action;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            TickNow(timer);
        };
        _timers.Add(timer);
        timer.Start();
    }

    /// <summary>Runs every scheduled step at once, oldest first (tests: the headless clock does not fire timers).</summary>
    internal void FlushTimers()
    {
        while (_timers.Count > 0)
        {
            var timer = _timers[0];
            timer.Stop();
            TickNow(timer);
        }
        if (_summaryTimer is not null)
        {
            RestoreHint();
        }
    }

    private readonly Dictionary<DispatcherTimer, Action> _timerActions = new();

    private void TickNow(DispatcherTimer timer)
    {
        _timers.Remove(timer);
        if (_timerActions.Remove(timer, out var action))
        {
            action();
        }
    }

    #endregion

    #region Team

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _roster = null;
        SyncTeam(animate: _teamArrival && !_quiet);
        UpdateSeats();
    }

    private void SyncTeam(bool animate)
    {
        if (_team is null)
        {
            return;
        }
        _team.SetPeople(Members, animate, Labels.MorePeople);
        _team.IsVisible = Members.Count > 0;
        AutomationProperties.SetName(_team, Labels.TeamName(Members.Count));
    }

    #endregion

    #region Pending rows

    private void OnPendingChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _roster = null;
        foreach (var invite in _observedInvites.ToList())
        {
            if (!PendingInvites.Contains(invite))
            {
                invite.PropertyChanged -= OnInvitePropertyChanged;
                _observedInvites.Remove(invite);
            }
        }
        foreach (var invite in PendingInvites)
        {
            if (_observedInvites.Add(invite))
            {
                invite.PropertyChanged += OnInvitePropertyChanged;
            }
        }
        SyncPendingRows(animate: !_quiet && IsLoaded);
        UpdateSeats();
    }

    private void OnInvitePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is InvitePeoplePendingInvite invite && RowOf(invite) is { } row)
        {
            UpdateRow(row);
        }
    }

    private void SyncPendingRows(bool animate)
    {
        if (_pendingRows is null)
        {
            return;
        }
        animate &= ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        foreach (var (invite, row) in _pendingRowMap.ToList())
        {
            if (!PendingInvites.Contains(invite))
            {
                _pendingRowMap.Remove(invite);
                ExitRow(row, animate);
            }
        }
        for (var i = 0; i < PendingInvites.Count; i++)
        {
            var invite = PendingInvites[i];
            if (_pendingRowMap.ContainsKey(invite))
            {
                continue;
            }
            var row = CreateRow(invite);
            _pendingRowMap[invite] = row;
            var index = _pendingRows.Children.Count;
            for (var j = i + 1; j < PendingInvites.Count; j++)
            {
                if (_pendingRowMap.TryGetValue(PendingInvites[j], out var next) && next.Slot.Parent is not null)
                {
                    index = _pendingRows.Children.IndexOf(next.Slot);
                    break;
                }
            }
            _pendingRows.Children.Insert(index, row.Slot);
            if (animate)
            {
                row.Slot.IsExpanded = false;
                row.Slot.Loaded += ExpandOnLoad;
            }
            if (_rowFlights.Remove(invite, out var from))
            {
                _flights?.Fly(from, row.Avatar, invite.Person);
            }
        }
        UpdateDividers();
        if (_pendingCount is not null)
        {
            _pendingCount.Value = PendingInvites.Count;
        }
        if (_pendingEmptyHost is not null)
        {
            _pendingEmptyHost.IsExpanded = PendingInvites.Count == 0;
        }
    }

    private static void ExpandOnLoad(object? sender, RoutedEventArgs e)
    {
        if (sender is SpringSizeDecorator slot)
        {
            slot.Loaded -= ExpandOnLoad;
            slot.IsExpanded = true;
        }
    }

    private void UpdateDividers()
    {
        var first = true;
        foreach (var slot in _pendingRows?.Children ?? [])
        {
            if (slot is not SpringSizeDecorator { Child: InvitePeoplePendingRow row } || row.IsLeaving)
            {
                continue;
            }
            row.BorderThickness = first ? default : new Thickness(0, 1, 0, 0);
            first = false;
        }
    }

    private async void ExitRow(InvitePeoplePendingRow row, bool animate)
    {
        row.IsLeaving = true;
        if (animate && row.Slot.Parent is not null)
        {
            var fade = new TKCode.Arc.Motion.MotionValue<double>(row, 1, TKCode.Arc.Motion.MotionVectors.Double);
            fade.Changed += v => row.Opacity = v;
            _ = fade.AnimateTo(0, InvitePeopleMotion.RowFade());
            row.Slot.IsExpanded = false;
            await Task.Delay(ArcMotion.IsFull(this) ? 520 : 140);
            fade.Dispose();
        }
        _pendingRows?.Children.Remove(row.Slot);
        UpdateDividers();
    }

    private InvitePeoplePendingRow CreateRow(InvitePeoplePendingInvite invite)
    {
        var labels = Labels;
        var row = new InvitePeoplePendingRow(invite) { Padding = new Thickness(0, 8) };
        ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*,Auto,Auto"), MinHeight = 36 };
        row.Layout = grid;
        row.Avatar = InvitePeopleAvatars.Create(36, invite.Person);
        grid.Children.Add(row.Avatar);
        row.NameText = InvitePeopleText.Create(13, ArcDesignTokenKind.Foreground, FontWeight.Medium, 18.2);
        row.Detail = InvitePeopleText.Create(12, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        row.Copy = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { row.NameText, row.Detail } };
        Grid.SetColumn(row.Copy, 1);
        grid.Children.Add(row.Copy);
        row.Status = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Status.HorizontalAlignment = HorizontalAlignment.Left;
        row.ResendText = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        var resentGhost = ResentContent(labels);
        resentGhost.Opacity = 0;
        var resendGhost = new TextBlock { Text = labels.Resend, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
        row.Resend = new InvitePeopleButton
        {
            Content = new Panel { Children = { resentGhost, resendGhost, row.ResendText } }
        };
        row.Resend.Classes.Add("text");
        row.ResendText.Content = labels.Resend;
        row.Resend.Click += (_, _) => _ = ResendRow(row);
        row.Revoke = new InvitePeopleButton { Width = 28, Height = 28, Content = InvitePeopleText.Glyph(ArcIcons.X, 15) };
        row.Revoke.Classes.Add("icon");
        row.Revoke.Click += (_, _) => _ = RevokeInvite(row.Invite);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { row.Resend, row.Revoke }
        };
        Grid.SetColumn(actions, 3);
        grid.Children.Add(actions);
        row.Child = grid;
        row.Status.Margin = new Thickness(12, 0, 0, 0);
        row.SetNarrow(_isNarrow);
        if (!_isNarrow)
        {
            row.Status.Margin = new Thickness(12, 0, 0, 0);
        }
        row.Slot = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Child = row };
        UpdateRow(row);
        return row;
    }

    private static Control ResentContent(InvitePeopleLabels labels)
    {
        var check = InvitePeopleText.Glyph(ArcIcons.Check, 13, 2);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { check, new TextBlock { Text = labels.Resent, VerticalAlignment = VerticalAlignment.Center } }
        };
    }

    private void UpdateRow(InvitePeoplePendingRow row)
    {
        var labels = Labels;
        var invite = row.Invite;
        var person = invite.Person;
        InvitePeopleAvatars.Update(row.Avatar, person);
        var hasName = !string.IsNullOrWhiteSpace(person?.Name);
        row.NameText.Text = hasName ? person!.Name : person?.Email;
        var role = invite.Role?.Name;
        row.Detail.Text = hasName
            ? string.IsNullOrEmpty(role) ? person?.Email : $"{role} · {person?.Email}"
            : role;
        row.Detail.IsVisible = !string.IsNullOrEmpty(row.Detail.Text);
        row.Status.Content = StatusContent(invite, labels);
        var who = person?.DisplayName ?? string.Empty;
        AutomationProperties.SetName(row.Resend, row.IsResent ? labels.ResentName(who) : labels.ResendName(who));
        AutomationProperties.SetName(row.Revoke, labels.RevokeName(who));
        row.Resend.IsEnabled = invite.Status != InvitePeopleInviteStatus.Joined;
    }

    private Control StatusContent(InvitePeoplePendingInvite invite, InvitePeopleLabels labels)
    {
        var text = InvitePeopleText.Create(12, invite.Status switch
        {
            InvitePeopleInviteStatus.Sending => ArcDesignTokenKind.TextSecondary,
            InvitePeopleInviteStatus.Joined => ArcDesignTokenKind.Success,
            _ => ArcDesignTokenKind.TextMuted
        });
        text.FontFeatures = FontFeatureCollection.Parse("tnum");
        switch (invite.Status)
        {
            case InvitePeopleInviteStatus.Sending:
            {
                text.Text = labels.Sending;
                var spinner = new ArcSpinner { Width = 13, Height = 13, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { spinner, text } };
            }
            case InvitePeopleInviteStatus.Joined:
            {
                text.Text = labels.Joined;
                var check = InvitePeopleText.Glyph(ArcIcons.Check, 13, 2);
                ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { check, text } };
            }
            default:
                text.Text = labels.Sent(invite.SentText);
                return text;
        }
    }

    #endregion

    #region Send

    private async void OnSendClick(object? sender, RoutedEventArgs e) => await TrySend();

    private async Task TrySend()
    {
        var labels = Labels;
        var invalid = _entries.Where(x => !x.IsValid).ToList();
        var valid = _entries.Where(x => x.IsValid).ToList();
        if (invalid.Count > 0)
        {
            foreach (var entry in invalid)
            {
                if (ChipOf(entry) is { } chip)
                {
                    InvitePeopleShake.Run(chip.Face);
                }
            }
            Announce(labels.FixBeforeSending(invalid.Count));
            return;
        }
        if (valid.Count == 0)
        {
            return;
        }
        if (IsOverLimit)
        {
            if (_over is not null)
            {
                InvitePeopleShake.Run(_over);
            }
            Announce(labels.OverBeforeSending(Seats.Over, EffectivePlan?.Name ?? string.Empty));
            return;
        }

        var hadFocus = IsKeyboardFocusWithin;
        var invites = new List<InvitePeoplePendingInvite>();
        foreach (var entry in valid)
        {
            var invite = new InvitePeoplePendingInvite(entry.Person!, entry.Role, null, InvitePeopleInviteStatus.Sending);
            invites.Add(invite);
            if (ChipOf(entry)?.Avatar is { } avatar)
            {
                _rowFlights[invite] = _flights?.BoxOf(avatar);
            }
        }
        _chips?.ArmLayout();
        foreach (var entry in valid)
        {
            RemoveChip(entry);
        }
        _entries.Clear();
        for (var i = 0; i < invites.Count; i++)
        {
            PendingInvites.Insert(i, invites[i]);
        }
        _rowFlights.Clear();
        AfterEntriesChanged();
        if (hadFocus)
        {
            _input?.Focus(NavigationMethod.Tab);
        }
        Announce(labels.SendingCount(invites.Count));

        var request = new InvitePeopleSendRequest(invites, ++_attempt);
        var token = _lifetime.Token;
        try
        {
            if (SendCommand is { } command && command.CanExecute(request))
            {
                command.Execute(request);
            }
            if (SendAction is { } action)
            {
                await action(request, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            foreach (var invite in invites)
            {
                PendingInvites.Remove(invite);
            }
            var back = invites.Select(i => new InvitePeopleParsedEntry(i.Person, i.Person?.DisplayName ?? string.Empty,
                InvitePeopleEntryError.None)).ToList();
            var added = AddEntries(back);
            for (var i = 0; i < added.Count && i < invites.Count; i++)
            {
                added[i].Role = invites[i].Role;
                if (ChipOf(added[i])?.RoleText is { } roleText)
                {
                    roleText.Content = invites[i].Role?.Name;
                }
            }
            Announce(labels.SendFailed);
            RaiseEvent(new InvitePeopleSendFailedEventArgs(SendFailedEvent, request, ex));
            return;
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        var waiting = invites.Where(i => i.Status == InvitePeopleInviteStatus.Sending && PendingInvites.Contains(i)).ToList();
        void Finish()
        {
            Announce(labels.SentCount(invites.Count));
            RaiseEvent(new InvitePeopleInvitesSentEventArgs(InvitesSentEvent, request));
        }
        if (waiting.Count == 0)
        {
            Finish();
            return;
        }
        for (var i = 0; i < waiting.Count; i++)
        {
            var invite = waiting[i];
            var last = i == waiting.Count - 1;
            if (i == 0)
            {
                MarkDelivered(invite);
                if (last)
                {
                    Finish();
                }
                continue;
            }
            Schedule(TimeSpan.FromTicks(DeliveryStagger.Ticks * i), () =>
            {
                MarkDelivered(invite);
                if (last)
                {
                    Finish();
                }
            });
        }
    }

    #endregion

    #region Join, resend, revoke, upgrade

    private void CompleteJoin(InvitePeoplePendingInvite invite)
    {
        if (!PendingInvites.Contains(invite) || invite.Person is not { } person)
        {
            return;
        }
        var from = RowOf(invite) is { } row ? _flights?.BoxOf(row.Avatar) : null;
        PendingInvites.Remove(invite);
        _teamArrival = true;
        try
        {
            Members.Insert(0, person);
        }
        finally
        {
            _teamArrival = false;
        }
        if (_team?.AvatarOf(person) is { } avatar)
        {
            _flights?.Fly(from, avatar, person);
        }
        RaiseEvent(new InvitePeopleInviteEventArgs(InviteJoinedEvent, invite));
    }

    private async Task ResendRow(InvitePeoplePendingRow row)
    {
        var invite = row.Invite;
        if (row.IsResent || row.IsLeaving || invite.Status != InvitePeopleInviteStatus.Sent)
        {
            return;
        }
        var labels = Labels;
        var who = invite.Person?.DisplayName ?? string.Empty;
        var token = _lifetime.Token;
        if (ResendAction is { } action)
        {
            try
            {
                await action(invite, token);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                Announce(labels.ResendFailed(who));
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        row.IsResent = true;
        row.ResendText.Content = ResentContent(labels);
        UpdateRow(row);
        Announce(labels.ResentAnnouncement(who));
        RaiseEvent(new InvitePeopleInviteEventArgs(InviteResentEvent, invite));
        Schedule(ResentHold, () =>
        {
            row.IsResent = false;
            row.ResendText.Content = Labels.Resend;
            UpdateRow(row);
        });
    }

    private async Task RevokeInvite(InvitePeoplePendingInvite invite)
    {
        var index = PendingInvites.IndexOf(invite);
        if (index < 0)
        {
            return;
        }
        var labels = Labels;
        var who = invite.Person?.DisplayName ?? string.Empty;
        PendingInvites.RemoveAt(index);
        _pendingHeading?.Focus(NavigationMethod.Tab);
        Announce(labels.Revoked(who));
        RaiseEvent(new InvitePeopleInviteEventArgs(InviteRevokedEvent, invite));
        if (RevokeAction is not { } action)
        {
            return;
        }
        var token = _lifetime.Token;
        try
        {
            await action(invite, token);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            PendingInvites.Insert(Math.Min(index, PendingInvites.Count), invite);
            Announce(labels.RevokeFailed(who));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnUpgradeClick(object? sender, RoutedEventArgs e) => await TryUpgrade();

    private void OnResetClick(object? sender, RoutedEventArgs e) => Reset();

    private async Task TryUpgrade()
    {
        var next = NextPlan;
        if (next is null)
        {
            return;
        }
        var old = EffectivePlan;
        var token = _lifetime.Token;
        if (UpgradeAction is { } action)
        {
            try
            {
                await action(next, token);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                Announce(Labels.UpgradeFailed);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
        if (token.IsCancellationRequested)
        {
            return;
        }
        var focusWasOnUpgrade = _upgrade?.IsFocused == true;
        SetCurrentValue(CurrentPlanProperty, next);
        if (focusWasOnUpgrade)
        {
            _send?.Focus(NavigationMethod.Tab);
        }
        Announce(Labels.Upgraded(next.Name ?? string.Empty, next.SeatLimit));
        RaiseEvent(new InvitePeoplePlanUpgradedEventArgs(PlanUpgradedEvent, old, next));
    }

    #endregion
}
