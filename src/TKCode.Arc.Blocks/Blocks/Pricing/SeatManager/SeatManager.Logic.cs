using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class SeatManager
{
    /// <summary>How long a limit notice stays (Arc: 2.6 s).</summary>
    internal static readonly TimeSpan LimitNoticeDuration = TimeSpan.FromMilliseconds(2600);

    /// <summary>How long the done message stays before the card line returns (Arc: 2.8 s).</summary>
    internal static readonly TimeSpan DoneDuration = TimeSpan.FromMilliseconds(2800);

    private static readonly SeatManagerPlan s_noPlan = new();

    private sealed class PendingRevoke
    {
        public required SeatManagerInvite Invite;
        public required CancellationTokenSource Cancel;
        public bool Expired;
        public bool Quiet;
    }

    private readonly Dictionary<SeatManagerInvite, PendingRevoke> _pending = new();
    private readonly Dictionary<SeatManagerInvite, SeatManagerResendState> _resend = new();
    private CancellationTokenSource? _saving;
    private CancellationTokenSource? _noticeTimer;
    private CancellationTokenSource? _doneTimer;
    private string? _limitNotice;
    private bool _quiet;
    private bool _lastInputKeyboard;

    /// <summary>Waits for the block's timers (notice, done message, undo window, simulated backend, hold repeat, demo loop).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (t, c) => Task.Delay(t, c);

    /// <summary>The members counted for seats.</summary>
    internal int MemberTotal => Math.Max(0, MemberCount ?? _members.Count);

    /// <summary>Invites that still hold a seat (not in an undo window, not leaving).</summary>
    internal int ActiveInvites => _invites.Count(i => i is not null && !_pending.ContainsKey(i));

    /// <summary>Seats in use.</summary>
    internal int Used => SeatManagerMath.Used(MemberTotal, ActiveInvites);

    /// <summary>The limit notice showing now, or null (tests).</summary>
    internal string? LimitNotice => _limitNotice;

    /// <summary>The resend state of <paramref name="invite"/>.</summary>
    internal SeatManagerResendState ResendStateOf(SeatManagerInvite invite) =>
        _resend.TryGetValue(invite, out var state) ? state : SeatManagerResendState.Idle;

    /// <summary>True while <paramref name="invite"/> is revoked and its undo window is open (or it is leaving).</summary>
    public bool IsRevokePending(SeatManagerInvite invite) => _pending.ContainsKey(invite);

    private SeatManagerPlan PlanOrEmpty => Plan ?? s_noPlan;

    private DateTime Today => (Now ?? DateTime.Today).Date;

    private bool IsLocked => Phase is SeatManagerPhase.Confirm or SeatManagerPhase.Saving or SeatManagerPhase.Error;

    private bool IsSimulated => UpdateSeatsAsync is null && ResendInviteAsync is null && RevokeInviteAsync is null;

    #region Changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PurchasedProperty)
        {
            // While nothing is being changed the stepper follows the seats paid for.
            var old = change.GetOldValue<int>();
            if (Phase is SeatManagerPhase.Idle or SeatManagerPhase.Done && Target == old)
            {
                SetCurrentValue(TargetProperty, Purchased);
            }
            Refresh();
        }
        else if (property == TargetProperty || property == PlanProperty || property == MemberCountProperty ||
                 property == PeriodStartProperty || property == PeriodEndProperty || property == NowProperty ||
                 property == WorkspaceProperty || property == PaymentMethodProperty || property == ReceiptEmailProperty)
        {
            Refresh();
        }
        else if (property == LabelsProperty)
        {
            foreach (var row in _rows.Values)
            {
                row.Refresh();
            }
            ResetSlots();
            Refresh();
        }
        else if (property == NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (property == IsAutoPlayEnabledProperty || property == UpdateSeatsAsyncProperty ||
                 property == ResendInviteAsyncProperty || property == RevokeInviteAsyncProperty)
        {
            UpdateAutoPlay();
        }
        else if (property == PhaseProperty)
        {
            var oldPhase = change.GetOldValue<SeatManagerPhase>();
            var newPhase = change.GetNewValue<SeatManagerPhase>();
            UpdatePhasePseudoClasses();
            RaiseEvent(new SeatManagerPhaseChangedEventArgs(PhaseChangedEvent, oldPhase, newPhase));
        }
    }

    private void OnMembersChanged() => Refresh();

    private void OnInvitesChanged()
    {
        // Revokes whose invite left the list some other way are over.
        foreach (var gone in _pending.Keys.Where(i => !_invites.Contains(i)).ToList())
        {
            _pending[gone].Cancel.Cancel();
            _pending.Remove(gone);
        }
        foreach (var gone in _resend.Keys.Where(i => !_invites.Contains(i)).ToList())
        {
            _resend.Remove(gone);
        }
        SyncRows();
        Refresh();
    }

    private void RecomputeLimits()
    {
        Minimum = Used;
        var cap = PlanOrEmpty.MaxSeats;
        Maximum = Math.Max(Minimum, cap is > 0 ? cap.Value : NoCap);
        CoerceValue(TargetProperty);
    }

    /// <summary>Recomputes the limits and the quote and redraws everything that depends on them.</summary>
    private void Refresh()
    {
        RecomputeLimits();
        var plan = PlanOrEmpty;
        var start = PeriodStart == default ? Today : PeriodStart;
        var end = PeriodEnd == default ? start.AddMonths(1) : PeriodEnd;
        var quote = SeatManagerQuote.Build(Purchased, Target, plan, start, end, Today);
        var changed = quote != _quote;
        Quote = quote;
        UpdateView();
        if (changed)
        {
            RaiseEvent(new SeatManagerQuoteChangedEventArgs(QuoteChangedEvent, quote));
        }
    }

    #endregion

    #region Stepper

    /// <summary>Adds one seat on the stepper (explains instead at the plan's cap). Does nothing while a change is confirmed.</summary>
    public void Increment() => StepBy(1);

    /// <summary>Removes one seat on the stepper (explains instead at the seats in use). Does nothing while a change is confirmed.</summary>
    public void Decrement() => StepBy(-1);

    private void StepBy(int step)
    {
        if (IsLocked)
        {
            return;
        }
        LeaveDone();
        var next = SeatManagerMath.Step(Target, step, Minimum, Maximum, out var edge);
        if (edge != 0)
        {
            ShowLimitNotice(edge);
        }
        if (next != Target)
        {
            Target = next;
        }
    }

    private void JumpTo(int value)
    {
        if (IsLocked)
        {
            return;
        }
        LeaveDone();
        var clamped = Math.Clamp(value, Minimum, Maximum);
        if (clamped == Target && value != clamped)
        {
            ShowLimitNotice(value < clamped ? -1 : 1);
        }
        Target = clamped;
    }

    private void OnSpinRequested(SeatManagerSpinRequest request)
    {
        switch (request)
        {
            case SeatManagerSpinRequest.Up:
                StepBy(1);
                break;
            case SeatManagerSpinRequest.Down:
                StepBy(-1);
                break;
            case SeatManagerSpinRequest.PageUp:
                StepBy(5);
                break;
            case SeatManagerSpinRequest.PageDown:
                StepBy(-5);
                break;
            case SeatManagerSpinRequest.Home:
                JumpTo(Minimum);
                break;
            case SeatManagerSpinRequest.End:
                JumpTo(Maximum);
                break;
        }
    }

    private async void ShowLimitNotice(int edge)
    {
        var labels = Labels;
        _limitNotice = edge < 0 ? labels.NoticeAtMin(Minimum) : labels.NoticeAtMax(PlanOrEmpty.Name, Maximum);
        _noticeTimer?.Cancel();
        var timer = new CancellationTokenSource();
        _noticeTimer = timer;
        UpdateView();
        try
        {
            await Delay(LimitNoticeDuration, timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_noticeTimer, timer))
        {
            _noticeTimer = null;
            _limitNotice = null;
            UpdateView();
        }
    }

    private void OnAddSeatClicked()
    {
        var inside = FocusInside();
        Increment();
        // The action leaves with the full-team notice; the plus button is the natural place to keep adding.
        FocusPart(_plus, inside);
    }

    #endregion

    #region Footer phases

    /// <summary>Opens the confirm step for the stepper's change (focus moves to Pay or Confirm). Does nothing without a change.</summary>
    public void Review()
    {
        if (Phase is not (SeatManagerPhase.Idle or SeatManagerPhase.Done) || Quote.Delta == 0)
        {
            return;
        }
        var inside = FocusInside();
        LeaveDone();
        SetPhase(SeatManagerPhase.Confirm);
        var quote = Quote;
        var labels = Labels;
        Announce(quote.Delta > 0
            ? labels.AnnounceReviewAdd(quote.Delta, FormatMoney(quote.DueToday))
            : labels.AnnounceReviewRemove(-quote.Delta, FormatDate(quote.RenewsOn)));
        FocusPart(_pay, inside);
    }

    /// <summary>Leaves the confirm step (or the error) without changing anything; focus returns to Update seats.</summary>
    public void Back()
    {
        if (Phase is not (SeatManagerPhase.Confirm or SeatManagerPhase.Error))
        {
            return;
        }
        var inside = FocusInside();
        ErrorMessage = null;
        SetPhase(SeatManagerPhase.Idle);
        FocusPart(_update, inside);
    }

    /// <summary>
    /// Applies the change in the confirm step (or retries after an error): runs <see cref="UpdateSeatsAsync"/> (or the
    /// simulated charge), then shows the done message, or the error with Try again.
    /// </summary>
    public async Task ConfirmAsync()
    {
        if (Phase is not (SeatManagerPhase.Confirm or SeatManagerPhase.Error))
        {
            return;
        }
        var quote = Quote;
        var quiet = _quiet;
        var handler = UpdateSeatsAsync;
        var simulated = handler is null;
        _saving?.Cancel();
        var saving = new CancellationTokenSource();
        _saving = saving;
        ErrorMessage = null;
        SetPhase(SeatManagerPhase.Saving);
        try
        {
            if (handler is null)
            {
                await Delay(SeatManagerSamples.ChargeDelay, saving.Token);
            }
            else
            {
                await handler(quote, saving.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_saving, saving))
            {
                _saving = null;
                SetPhase(SeatManagerPhase.Confirm);
            }
            return;
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_saving, saving))
            {
                return;
            }
            _saving = null;
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? Labels.ErrorFallback : ex.Message;
            SetPhase(SeatManagerPhase.Error);
            if (!quiet)
            {
                Announce(ErrorMessage);
            }
            return;
        }
        if (!ReferenceEquals(_saving, saving))
        {
            return;
        }
        _saving = null;
        var focusInFooter = IsFocusIn(_footerActions);
        SetPhase(SeatManagerPhase.Done);
        Purchased = quote.Target;
        if (Target != quote.Target)
        {
            Target = quote.Target;
        }
        RaiseEvent(new SeatManagerSeatsChangedEventArgs(SeatsChangedEvent, quote.Target, quote));
        if (!quiet)
        {
            var labels = Labels;
            var text = labels.AnnounceUpdated(quote.Target, quote.Delta > 0 ? FormatMoney(quote.DueToday) : null);
            Announce(simulated ? text + labels.SimulatedCharge : text);
        }
        // Update seats is disabled now that nothing changes: keep focus on the stepper instead of losing it (Arc drops it).
        FocusPart(_spin, focusInFooter);
        StartDoneTimer();
    }

    private async void StartDoneTimer()
    {
        _doneTimer?.Cancel();
        var timer = new CancellationTokenSource();
        _doneTimer = timer;
        try
        {
            await Delay(DoneDuration, timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_doneTimer, timer))
        {
            _doneTimer = null;
            if (Phase == SeatManagerPhase.Done)
            {
                SetPhase(SeatManagerPhase.Idle);
            }
        }
    }

    private void LeaveDone()
    {
        if (Phase != SeatManagerPhase.Done)
        {
            return;
        }
        _doneTimer?.Cancel();
        _doneTimer = null;
        SetPhase(SeatManagerPhase.Idle);
    }

    private void SetPhase(SeatManagerPhase phase)
    {
        if (Phase == phase)
        {
            return;
        }
        Phase = phase;
        UpdateView();
    }

    private void UpdatePhasePseudoClasses()
    {
        PseudoClasses.Set(ConfirmPseudoClass, Phase == SeatManagerPhase.Confirm);
        PseudoClasses.Set(SavingPseudoClass, Phase == SeatManagerPhase.Saving);
        PseudoClasses.Set(DonePseudoClass, Phase == SeatManagerPhase.Done);
        PseudoClasses.Set(ErrorPseudoClass, Phase == SeatManagerPhase.Error);
    }

    private void OnPayClicked()
    {
        if (Phase is SeatManagerPhase.Confirm or SeatManagerPhase.Error)
        {
            _ = ConfirmAsync();
        }
    }

    #endregion

    #region Invites

    /// <summary>Sends <paramref name="invite"/> again (spinner, then "Sent"; runs <see cref="ResendInviteAsync"/> or the simulation).</summary>
    public async void Resend(SeatManagerInvite invite)
    {
        if (!_invites.Contains(invite) || _pending.ContainsKey(invite) || ResendStateOf(invite) != SeatManagerResendState.Idle)
        {
            return;
        }
        var quiet = _quiet;
        var handler = ResendInviteAsync;
        SetResend(invite, SeatManagerResendState.Sending);
        try
        {
            if (handler is null)
            {
                await Delay(SeatManagerSamples.ResendDelay, CancellationToken.None);
            }
            else
            {
                await handler(invite, CancellationToken.None);
            }
        }
        catch (Exception)
        {
            if (_resend.ContainsKey(invite))
            {
                SetResend(invite, SeatManagerResendState.Idle);
                if (!quiet)
                {
                    Announce(Labels.AnnounceResendFailed(invite.Email));
                }
            }
            return;
        }
        if (!_invites.Contains(invite) || ResendStateOf(invite) != SeatManagerResendState.Sending)
        {
            return;
        }
        var row = RowOf(invite);
        var hadFocus = row is not null && IsFocusIn(row.ResendButton);
        SetResend(invite, SeatManagerResendState.Sent);
        RaiseEvent(new SeatManagerInviteEventArgs(InviteResentEvent, invite));
        if (!quiet)
        {
            var labels = Labels;
            var text = labels.AnnounceResent(invite.Email);
            Announce(handler is null ? text + labels.SimulatedSend : text);
        }
        // "Sent" is disabled: hand focus to the row's other action.
        FocusPart(row?.RevokeButton, hadFocus);
    }

    private void SetResend(SeatManagerInvite invite, SeatManagerResendState state)
    {
        _resend[invite] = state;
        if (RowOf(invite) is { } row)
        {
            row.ResendState = state;
        }
    }

    /// <summary>
    /// Revokes <paramref name="invite"/>: its seat is free at once and the row offers Undo for <see cref="UndoDuration"/>;
    /// when the window closes the row collapses and <see cref="RevokeInviteAsync"/> runs (if it fails, the invite comes back).
    /// </summary>
    public async void Revoke(SeatManagerInvite invite)
    {
        if (!_invites.Contains(invite) || _pending.ContainsKey(invite) || ResendStateOf(invite) == SeatManagerResendState.Sending)
        {
            return;
        }
        var row = RowOf(invite);
        var hadFocus = row is not null && IsFocusIn(row.RevokeButton);
        var pending = new PendingRevoke { Invite = invite, Cancel = new CancellationTokenSource(), Quiet = _quiet };
        _pending[invite] = pending;
        if (row is not null)
        {
            row.IsRevoked = true;
        }
        Refresh();
        if (!pending.Quiet)
        {
            Announce(Labels.AnnounceRevoked(invite.Email, (int)Math.Round(UndoDuration.TotalSeconds)));
        }
        FocusPart(row?.UndoButton, hadFocus);
        try
        {
            await Delay(UndoDuration, pending.Cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (_pending.TryGetValue(invite, out var current) && ReferenceEquals(current, pending))
        {
            await ExpireAsync(pending);
        }
    }

    /// <summary>Brings back <paramref name="invite"/> while its undo window is open.</summary>
    public void Undo(SeatManagerInvite invite)
    {
        if (!_pending.TryGetValue(invite, out var pending) || pending.Expired)
        {
            return;
        }
        var row = RowOf(invite);
        var hadFocus = row is not null && IsFocusIn(row.UndoButton);
        pending.Cancel.Cancel();
        _pending.Remove(invite);
        if (row is not null)
        {
            row.IsRevoked = false;
        }
        Restore(invite, _quiet);
        FocusPart(row?.RevokeButton, hadFocus);
    }

    private void Restore(SeatManagerInvite invite, bool quiet)
    {
        RecomputeLimits();
        if (Target < Minimum)
        {
            Target = Minimum;
        }
        Refresh();
        RaiseEvent(new SeatManagerInviteEventArgs(InviteRestoredEvent, invite));
        if (!quiet)
        {
            Announce(Labels.AnnounceRestored(invite.Email));
        }
    }

    private async Task ExpireAsync(PendingRevoke pending)
    {
        pending.Expired = true;
        var invite = pending.Invite;
        var row = RowOf(invite);
        FocusPart(_spin, row is not null && row.IsKeyboardFocusWithin);
        var collapse = row?.CollapseAsync() ?? Task.CompletedTask;
        var handler = RevokeInviteAsync;
        Exception? failure = null;
        try
        {
            if (handler is not null)
            {
                await handler(invite, pending.Cancel.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        if (!_pending.TryGetValue(invite, out var current) || !ReferenceEquals(current, pending))
        {
            return;
        }
        if (failure is not null)
        {
            // The revoke did not happen: the invite goes back where it was.
            _pending.Remove(invite);
            if (row is not null)
            {
                row.IsRevoked = false;
                row.Reopen();
            }
            Restore(invite, pending.Quiet);
            return;
        }
        await collapse;
        if (!_pending.TryGetValue(invite, out current) || !ReferenceEquals(current, pending))
        {
            return;
        }
        _pending.Remove(invite);
        _invites.Remove(invite);
        RaiseEvent(new SeatManagerInviteEventArgs(InviteRevokedEvent, invite));
        if (!pending.Quiet)
        {
            var labels = Labels;
            var text = labels.AnnounceRemoved(invite.Email);
            Announce(handler is null ? text + labels.SimulatedSend : text);
        }
    }

    #endregion

    #region Input, focus, announcements

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastInputKeyboard = false;
        StopAutoPlay();
    }

    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        _lastInputKeyboard = true;
        StopAutoPlay();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Escape leaves the confirm step (Arc ignores it).
        if (!e.Handled && e.Key == Key.Escape && Phase is SeatManagerPhase.Confirm or SeatManagerPhase.Error)
        {
            e.Handled = true;
            Back();
        }
    }

    /// <summary>True when the keyboard focus is somewhere inside the card.</summary>
    private bool FocusInside()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && IsDescendant(focused);
    }

    /// <summary>
    /// Moves the focus to <paramref name="target"/> when it was inside the card before the change (<paramref name="inside"/>):
    /// a demo loop or a host call never steals it. A target that has just been swapped in is focused once it is laid out.
    /// </summary>
    private void FocusPart(Control? target, bool inside)
    {
        if (target is null || !inside)
        {
            return;
        }
        var method = _lastInputKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        if (TopLevel.GetTopLevel(target) is not null && target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
        {
            target.Focus(method);
            return;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (target.IsEffectivelyVisible)
            {
                target.Focus(method);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private bool IsDescendant(Visual visual)
    {
        for (var v = visual; v is not null; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
        {
            if (ReferenceEquals(v, this))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsFocusIn(Visual? part)
    {
        if (part is null)
        {
            return false;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        for (var v = focused; v is not null; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
        {
            if (ReferenceEquals(v, part))
            {
                return true;
            }
        }
        return false;
    }

    private void Announce(string text)
    {
        if (_quiet)
        {
            return;
        }
        _announcer?.Announce(text);
    }

    #endregion
}
