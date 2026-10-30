using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class TeamMembers
{
    /// <summary>The first sent avatar pops 170 ms after the send, each next one 100 ms later (measured).</summary>
    internal static readonly TimeSpan AvatarPopDelay = TimeSpan.FromMilliseconds(170);

    internal static readonly TimeSpan AvatarPopStagger = TimeSpan.FromMilliseconds(100);

    private readonly Dictionary<TeamMembersInvite, Point> _pendingFlights = new();
    private readonly HashSet<CancellationTokenSource> _running = new();
    private TeamMembersEmailError _error;
    private string? _errorArgument;
    private string? _failureText;
    private TeamMembersMemberRow? _confirming;

    #region Invite field

    private Func<string, string?> MemberNameOf => email =>
        _members.FirstOrDefault(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase))?.DisplayName;

    private Func<string, bool> IsInvited => email =>
        _invites.Any(i => string.Equals(i.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds addresses to the invite field as typing or pasting them would: the text is split at commas, semicolons, spaces
    /// and line breaks; invalid pieces stay in the field with an error, duplicates drop silently, members and pending
    /// invites drop with an error. Returns the addresses that became chips.
    /// </summary>
    public IReadOnlyList<string> AddEmails(string text) => Commit(text, animate: true);

    /// <summary>Removes an address from the invite field. Returns false when it is not there.</summary>
    public bool RemovePendingEmail(string email)
    {
        if (_field is null)
        {
            return _chipsBeforeTemplate.RemoveAll(c => string.Equals(c, email, StringComparison.OrdinalIgnoreCase)) > 0;
        }
        if (!_field.RemoveChip(email, CanAnimate))
        {
            return false;
        }
        SetStatus(Labels.StatusChipRemoved(email), isError: false);
        UpdateInviteState(animateMessage: true);
        return true;
    }

    /// <summary>Empties the invite field (chips and text) and clears its error.</summary>
    public void ClearPendingEmails()
    {
        _chipsBeforeTemplate.Clear();
        _field?.ClearChips(animate: false);
        _field?.SetDraftSilently(string.Empty);
        ClearError();
        UpdateInviteState(animateMessage: true);
    }

    /// <summary>Puts the caret in the invite field.</summary>
    public void FocusInviteField() => _field?.FocusInput(NavigationMethod.Tab);

    private IReadOnlyList<string> Commit(string text, bool animate)
    {
        var chips = PendingEmails;
        var result = TeamMembersEmailParser.Commit(TeamMembersEmailParser.Split(text), chips.ToArray(), MemberNameOf, IsInvited,
            EmailValidator);
        foreach (var email in result.Added)
        {
            if (_field is not null)
            {
                _field.AddChip(email, animate && CanAnimate);
            }
            else
            {
                _chipsBeforeTemplate.Add(email);
            }
        }
        _field?.SetDraftSilently(result.Draft);
        _failureText = null;
        _error = result.Error;
        _errorArgument = result.ErrorArgument;
        UpdateInviteState(animateMessage: true);
        return result.Added;
    }

    private void ClearError()
    {
        _error = TeamMembersEmailError.None;
        _errorArgument = null;
        _failureText = null;
    }

    private void OnFieldCommitted(string text) => Commit(text, animate: true);

    private void OnFieldEnterOnEmpty() => _ = SendInvitesAsync();

    private void OnFieldBackspace()
    {
        var chips = PendingEmails;
        if (chips.Count == 0)
        {
            return;
        }
        RemovePendingEmail(chips[^1]);
    }

    private void OnFieldChipRemove(string email)
    {
        RemovePendingEmail(email);
        _field?.FocusInput(NavigationMethod.Pointer);
    }

    private void OnFieldDraftChanged(string draft)
    {
        // Typing again clears the error (back to the hint or the seat warning).
        if (_error != TeamMembersEmailError.None || _failureText is not null)
        {
            ClearError();
        }
        UpdateInviteState(animateMessage: true);
    }

    private void OnInviteRoleChosen(TeamMembersRolePicker picker, string? key) => SetCurrentValue(InviteRoleProperty, key);

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SendInvitesAsync();
    }

    /// <summary>Recomputes the send count, the button, the field's error look and the line under the field.</summary>
    private void UpdateInviteState(bool animateMessage)
    {
        var chips = PendingEmails;
        var draft = _field?.Draft ?? string.Empty;
        var count = TeamMembersEmailParser.SendCount(chips.ToArray(), draft, MemberNameOf, IsInvited, EmailValidator);
        SendCount = count;
        var remaining = Math.Max(0, SeatLimit) - (_members.Count + _invites.Count);
        var labels = Labels;
        if (_send is not null)
        {
            _send.Content = count >= 2 ? labels.SendMany(count) : labels.SendOne;
            _send.IsEnabled = IsSending || TeamMembersEmailParser.CanSend(count, remaining, chips.Count);
            _send.IsLoading = IsSending;
        }
        if (_field is not null)
        {
            _field.IsInvalid = _error != TeamMembersEmailError.None || _failureText is not null;
            _field.IsBusy = IsSending;
        }
        if (_inviteRole is not null)
        {
            _inviteRole.IsEnabled = !IsSending;
        }
        PseudoClasses.Set(SendingPseudoClass, IsSending);

        TeamMembersMessageKind kind;
        string text;
        if (_failureText is not null)
        {
            kind = TeamMembersMessageKind.Error;
            text = _failureText;
        }
        else if (_error != TeamMembersEmailError.None)
        {
            kind = TeamMembersMessageKind.Error;
            var argument = _errorArgument ?? string.Empty;
            text = _error switch
            {
                TeamMembersEmailError.Invalid => labels.Invalid(argument),
                TeamMembersEmailError.AlreadyMember => labels.AlreadyMember(argument),
                _ => labels.AlreadyInvited(argument)
            };
        }
        else
        {
            var limit = Math.Max(0, SeatLimit);
            switch (TeamMembersEmailParser.SeatNotice(remaining, chips.Count))
            {
                case TeamMembersSeatNotice.OverLimit:
                    kind = TeamMembersMessageKind.Warning;
                    text = labels.OverLimit(remaining, chips.Count - remaining);
                    break;
                case TeamMembersSeatNotice.Full:
                    kind = TeamMembersMessageKind.Warning;
                    text = labels.Full(limit);
                    break;
                case TeamMembersSeatNotice.FullWithChips:
                    kind = TeamMembersMessageKind.Warning;
                    text = labels.FullWithChips(limit);
                    break;
                default:
                    kind = TeamMembersMessageKind.Hint;
                    text = labels.Hint;
                    break;
            }
        }
        ShowMessage(kind, text, animateMessage);
    }

    private void ShowMessage(TeamMembersMessageKind kind, string text, bool animate)
    {
        var key = $"{kind}|{text}";
        MessageKind = kind;
        MessageText = text;
        if (key == _messageKey && _message?.Content is not null)
        {
            return;
        }
        var announce = _messageKey is not null && kind != TeamMembersMessageKind.Hint;
        _messageKey = key;
        if (_message is not null)
        {
            var line = new TeamMembersMessageLine(kind, text);
            if (!animate || !CanAnimate || _message.Content is null)
            {
                var mode = _message.Mode;
                _message.Mode = ContentSwapMode.None;
                _message.Content = line;
                _message.Mode = mode;
            }
            else
            {
                _message.Content = line;
            }
        }
        if (announce)
        {
            _messageAnnouncer?.Announce(text);
        }
    }

    #endregion

    #region Send

    /// <summary>
    /// Sends the invites as the button does: a valid address being typed becomes a chip first; nothing happens (false) when
    /// there is nothing to send, the text has an error, the chips do not fit in the free seats or a send is running.
    /// Awaits <see cref="SendInvitesAction"/>; on success the addresses fly into new pending rows (true).
    /// </summary>
    public async Task<bool> SendInvitesAsync()
    {
        if (IsSending || IsReadOnly)
        {
            return false;
        }
        var draft = _field?.Draft ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(draft))
        {
            Commit(draft, animate: true);
            if (_error != TeamMembersEmailError.None)
            {
                return false;
            }
        }
        var emails = PendingEmails.ToArray();
        var remaining = Math.Max(0, SeatLimit) - (_members.Count + _invites.Count);
        if (emails.Length == 0 || emails.Length > remaining)
        {
            UpdateInviteState(animateMessage: true);
            return false;
        }
        var role = InviteRole;
        var labels = Labels;
        var invites = emails.Select(e => new TeamMembersInvite { Email = e, Role = role, Detail = labels.InvitedJustNow }).ToArray();
        var request = new TeamMembersSendRequest(emails, role, invites);
        ClearError();
        IsSending = true;
        UpdateInviteState(animateMessage: true);
        var cancellation = Begin();
        try
        {
            if (SendInvitesAction is { } action)
            {
                await action(request, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            IsSending = false;
            UpdateInviteState(animateMessage: true);
            return false;
        }
        catch (Exception exception)
        {
            IsSending = false;
            var message = ErrorText(exception, labels.SendFailed);
            _failureText = message;
            UpdateInviteState(animateMessage: true);
            SetStatus(message, isError: true);
            RaiseEvent(new TeamMembersActionFailedEventArgs(ActionFailedEvent, this, TeamMembersAction.Send, request, exception, message));
            return false;
        }
        finally
        {
            End(cancellation);
        }
        IsSending = false;
        // The flights start where the chips are drawn now; the chips vanish as their copies take off.
        RecordFlightSources(invites);
        _field?.ClearChips(animate: false);
        _field?.SetDraftSilently(string.Empty);
        var writable = WritableInvites;
        if (writable is not null)
        {
            foreach (var invite in invites)
            {
                writable.Add(invite);
            }
        }
        var roleName = RoleLabel(role);
        SetStatus(invites.Length == 1 ? labels.StatusInvitedOne(emails[0], roleName) : labels.StatusInvitedMany(invites.Length, roleName),
            isError: false);
        UpdateInviteState(animateMessage: true);
        var args = new TeamMembersInvitesSentEventArgs(InvitesSentEvent, this, invites, role);
        RaiseEvent(args);
        Execute(InvitesSentCommand, args);
        // A source that is not writable: the host adds the invites in its handler; flights that found no row are dropped.
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var invite in invites)
            {
                _pendingFlights.Remove(invite);
            }
        }, DispatcherPriority.Background);
        return true;
    }

    private void RecordFlightSources(IReadOnlyList<TeamMembersInvite> invites)
    {
        if (_field is null || _flights is null || !CanAnimate)
        {
            return;
        }
        foreach (var invite in invites)
        {
            if (invite.Email is { } email && _field.ChipOf(email) is { } chip && chip.TranslatePoint(new Point(-1, -1), _flights) is { } point)
            {
                _pendingFlights[invite] = point;
            }
        }
    }

    private void StartFlight(TeamMembersInvite invite, TeamMembersInviteRow row, Point from, int index)
    {
        if (_flights is null)
        {
            return;
        }
        row.SetBadgeHidden(true);
        // The avatar pops after the row starts to grow (the template may not exist yet: wait for it).
        Dispatcher.UIThread.Post(() => row.AvatarControl?.PopIn(AvatarPopDelay + AvatarPopStagger * index), DispatcherPriority.Loaded);
        var flights = _flights;
        _ = FlyAsync(flights, invite, row, from);
    }

    private static async Task FlyAsync(TeamMembersFlightLayer flights, TeamMembersInvite invite, TeamMembersInviteRow row, Point from)
    {
        try
        {
            await flights.FlyAsync(invite.Email ?? string.Empty, from,
                () => row.EmailBadge is { } badge ? badge.TranslatePoint(default, flights) : null);
        }
        finally
        {
            row.SetBadgeHidden(false);
        }
    }

    #endregion

    #region Pending invites

    /// <summary>Sends a pending invite again (as its Resend button does). Returns true on success.</summary>
    public Task<bool> ResendAsync(TeamMembersInvite invite) =>
        RowOf(invite) is { } row ? ResendAsync(row) : Task.FromResult(false);

    internal async Task<bool> ResendAsync(TeamMembersInviteRow row)
    {
        if (IsReadOnly || row.ResendState == TeamMembersResendState.Sending)
        {
            return false;
        }
        var invite = row.Invite;
        var labels = Labels;
        var email = invite.Email ?? string.Empty;
        if (ResendInviteAction is { } action)
        {
            row.SetResendState(TeamMembersResendState.Sending);
            var cancellation = Begin();
            try
            {
                await action(invite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                row.SetResendState(TeamMembersResendState.Idle);
                return false;
            }
            catch (Exception exception)
            {
                row.SetResendState(TeamMembersResendState.Idle);
                Fail(TeamMembersAction.Resend, invite, exception, labels.ResendFailed(email));
                return false;
            }
            finally
            {
                End(cancellation);
            }
        }
        invite.Detail = labels.ResentJustNow;
        row.SetResendState(TeamMembersResendState.Sent);
        SetStatus(labels.StatusResent(email), isError: false);
        var args = new TeamMembersInviteEventArgs(InviteResentEvent, this, invite);
        RaiseEvent(args);
        Execute(InviteResentCommand, args);
        return true;
    }

    /// <summary>Withdraws a pending invite (as its Revoke button does: no confirmation). Returns true on success.</summary>
    public Task<bool> RevokeAsync(TeamMembersInvite invite) =>
        RowOf(invite) is { } row ? RevokeAsync(row) : Task.FromResult(false);

    internal async Task<bool> RevokeAsync(TeamMembersInviteRow row)
    {
        if (IsReadOnly || row.IsRevoking)
        {
            return false;
        }
        var invite = row.Invite;
        var labels = Labels;
        var email = invite.Email ?? string.Empty;
        if (RevokeInviteAction is { } action)
        {
            row.IsRevoking = true;
            var cancellation = Begin();
            try
            {
                await action(invite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                row.IsRevoking = false;
                return false;
            }
            catch (Exception exception)
            {
                row.IsRevoking = false;
                Fail(TeamMembersAction.Revoke, invite, exception, labels.RevokeFailed(email));
                return false;
            }
            finally
            {
                End(cancellation);
            }
        }
        var hadFocus = row.IsKeyboardFocusWithin;
        var next = NeighbourInviteRow(row);
        WritableInvites?.Remove(invite);
        SetStatus(labels.StatusRevoked(email), isError: false);
        if (hadFocus)
        {
            FocusAfterRemoval(next?.RevokeButton);
        }
        var args = new TeamMembersInviteEventArgs(InviteRevokedEvent, this, invite);
        RaiseEvent(args);
        Execute(InviteRevokedCommand, args);
        return true;
    }

    private TeamMembersInviteRow? NeighbourInviteRow(TeamMembersInviteRow row)
    {
        var rows = InviteRows.Where(r => r.Parent is TeamMembersRowSlot { IsCollapsed: false, IsLeaving: false }).ToList();
        var index = rows.IndexOf(row);
        if (index < 0)
        {
            return null;
        }
        return index + 1 < rows.Count ? rows[index + 1] : index > 0 ? rows[index - 1] : null;
    }

    #endregion

    #region Members

    /// <summary>Opens the inline confirmation on a member's row (closing any other). Returns false for the owner or a read-only view.</summary>
    public bool RequestRemove(TeamMembersPerson member) =>
        RowOf(member) is { } row && BeginRemove(row, NavigationMethod.Tab);

    /// <summary>Closes the inline confirmation, if one is open.</summary>
    public void CancelRemove()
    {
        if (_confirming is { } row)
        {
            CancelRemove(row, NavigationMethod.Tab);
        }
    }

    /// <summary>Removes a member without the confirmation (awaiting <see cref="RemoveMemberAction"/>). Returns true on success.</summary>
    public Task<bool> RemoveMemberAsync(TeamMembersPerson member) =>
        RowOf(member) is { } row ? ConfirmRemoveAsync(row) : Task.FromResult(false);

    internal bool BeginRemove(TeamMembersMemberRow row, NavigationMethod method)
    {
        if (IsReadOnly || row.Person.IsOwner)
        {
            return false;
        }
        if (_confirming is { } other && !ReferenceEquals(other, row))
        {
            other.IsConfirming = false;
        }
        _confirming = row;
        row.IsConfirming = true;
        row.CancelButton?.Focus(method);
        return true;
    }

    internal void CancelRemove(TeamMembersMemberRow row, NavigationMethod method)
    {
        if (!row.IsConfirming || row.IsBusy)
        {
            return;
        }
        row.IsConfirming = false;
        if (ReferenceEquals(_confirming, row))
        {
            _confirming = null;
        }
        row.RemoveButton?.Focus(method);
    }

    internal async Task<bool> ConfirmRemoveAsync(TeamMembersMemberRow row)
    {
        if (IsReadOnly || row.IsBusy || row.Person.IsOwner)
        {
            return false;
        }
        var member = row.Person;
        var labels = Labels;
        var name = member.DisplayName;
        if (RemoveMemberAction is { } action)
        {
            row.IsBusy = true;
            var cancellation = Begin();
            try
            {
                await action(member, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                row.IsBusy = false;
                return false;
            }
            catch (Exception exception)
            {
                row.IsBusy = false;
                Fail(TeamMembersAction.Remove, member, exception, labels.RemoveFailed(name));
                return false;
            }
            finally
            {
                End(cancellation);
            }
        }
        var hadFocus = row.IsKeyboardFocusWithin;
        var next = NeighbourMemberRow(row);
        if (ReferenceEquals(_confirming, row))
        {
            _confirming = null;
        }
        WritableMembers?.Remove(member);
        SetStatus(labels.StatusMemberRemoved(name, WorkspaceName), isError: false);
        if (hadFocus)
        {
            FocusAfterRemoval(next?.RemoveButton);
        }
        var args = new TeamMembersMemberEventArgs(MemberRemovedEvent, this, member);
        RaiseEvent(args);
        Execute(MemberRemovedCommand, args);
        return true;
    }

    private TeamMembersMemberRow? NeighbourMemberRow(TeamMembersMemberRow row)
    {
        var rows = MemberRows.Where(r => r.Parent is TeamMembersRowSlot { IsCollapsed: false, IsLeaving: false }).ToList();
        var index = rows.IndexOf(row);
        if (index < 0)
        {
            return null;
        }
        for (var i = index + 1; i < rows.Count; i++)
        {
            if (rows[i].RemoveButton is { IsVisible: true })
            {
                return rows[i];
            }
        }
        for (var i = index - 1; i >= 0; i--)
        {
            if (rows[i].RemoveButton is { IsVisible: true })
            {
                return rows[i];
            }
        }
        return null;
    }

    private void FocusAfterRemoval(Control? next)
    {
        if (next is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
        {
            // Remove clicked with the mouse moves focus on without the keyboard ring.
            next.Focus(BlockFocusModality.For(this));
        }
        else if (_search is { IsEffectivelyVisible: true })
        {
            _search.Focus(NavigationMethod.Tab);
        }
        else
        {
            _field?.FocusInput();
        }
    }

    /// <summary>Changes a member's role as the row's menu does (awaiting <see cref="ChangeRoleAction"/>). Returns true on success.</summary>
    public Task<bool> ChangeRoleAsync(TeamMembersPerson member, string role) => ChangeRoleCoreAsync(member, role);

    internal void OnRoleChosen(TeamMembersMemberRow row, string? key) => _ = ChangeRoleCoreAsync(row.Person, key);

    private async Task<bool> ChangeRoleCoreAsync(TeamMembersPerson member, string? key)
    {
        if (IsReadOnly || member.IsOwner || string.Equals(member.Role, key, StringComparison.Ordinal))
        {
            return false;
        }
        var old = member.Role;
        var labels = Labels;
        var name = member.DisplayName;
        // The new role shows at once (the trigger morphs); a failure puts the old one back.
        member.Role = key;
        if (ChangeRoleAction is { } action)
        {
            var change = new TeamMembersRoleChange(member, old, key);
            var row = RowOf(member);
            if (row is not null)
            {
                row.IsBusy = true;
            }
            var cancellation = Begin();
            try
            {
                await action(change, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                member.Role = old;
                return false;
            }
            catch (Exception exception)
            {
                member.Role = old;
                Fail(TeamMembersAction.ChangeRole, change, exception, labels.RoleChangeFailed(name));
                return false;
            }
            finally
            {
                if (row is not null)
                {
                    row.IsBusy = false;
                }
                End(cancellation);
            }
        }
        SetStatus(labels.StatusRoleChanged(name, RoleLabel(key)), isError: false);
        var args = new TeamMembersRoleChangedEventArgs(MemberRoleChangedEvent, this, member, old, key);
        RaiseEvent(args);
        Execute(MemberRoleChangedCommand, args);
        return true;
    }

    #endregion

    #region Helpers

    private CancellationTokenSource Begin()
    {
        var cancellation = new CancellationTokenSource();
        _running.Add(cancellation);
        return cancellation;
    }

    private void End(CancellationTokenSource cancellation)
    {
        _running.Remove(cancellation);
        cancellation.Dispose();
    }

    /// <summary>Cancels every running handler (the block left the window).</summary>
    private void CancelAll()
    {
        foreach (var cancellation in _running.ToArray())
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private string ErrorText(Exception exception, string fallback)
    {
        if (ErrorFormatter is { } format)
        {
            return format(exception);
        }
        return string.IsNullOrWhiteSpace(exception.Message) || exception.GetType() == typeof(Exception) && exception.Message.StartsWith("Exception of type", StringComparison.Ordinal)
            ? fallback
            : exception.Message;
    }

    private void Fail(TeamMembersAction action, object subject, Exception exception, string fallback)
    {
        var message = ErrorFormatter is { } format ? format(exception) : fallback;
        SetStatus(message, isError: true);
        RaiseEvent(new TeamMembersActionFailedEventArgs(ActionFailedEvent, this, action, subject, exception, message));
    }

    private static void Execute(System.Windows.Input.ICommand? command, object parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    #endregion
}
