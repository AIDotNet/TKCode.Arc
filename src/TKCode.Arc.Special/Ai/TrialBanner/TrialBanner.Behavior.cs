using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class TrialBanner
{
    /// <summary>How long the simulated billing call takes when no <see cref="AddPaymentHandler"/> is set.</summary>
    internal static readonly TimeSpan SimulatedBilling = TimeSpan.FromMilliseconds(900);

    /// <summary>How long a simulated note stays in place of the copy.</summary>
    internal static readonly TimeSpan NoteDuration = TimeSpan.FromMilliseconds(2400);

    private TrialBanner? _host;
    private TrialBanner? _mirror;
    private Popover? _popover;
    private TrialBillingPhase _phase;
    private CancellationTokenSource? _billing;
    private string? _note;
    private DispatcherTimer? _noteTimer;
    private bool _listensToToday;

    /// <summary>The banner whose state this one shows: itself, or the pill's banner for the card in its popover.</summary>
    internal TrialBanner Source => _host ?? this;

    internal TrialBillingPhase Phase => Source._phase;

    internal string? Note => Source._note;

    /// <summary>The pill's popover (created on first open).</summary>
    internal Popover? DetailsPopover => _popover;

    /// <summary>The card shown in the pill's popover (created on first open).</summary>
    internal TrialBanner? PopoverCard => _mirror;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            if (Variant != TrialBannerVariant.Pill)
            {
                _popover?.Close();
            }
            UpdateVariantClasses();
            Refresh(instant: false);
        }
        else if (change.Property == IsCollapsedProperty || change.Property == CollapseToProperty)
        {
            if (IsCollapsed)
            {
                _popover?.Close();
            }
            Refresh(instant: false);
        }
        else if (change.Property == PlanProperty || change.Property == DaysLeftProperty || change.Property == TrialDaysProperty
                 || change.Property == EndsAtProperty || change.Property == WarnAtProperty || change.Property == WorkspaceProperty
                 || change.Property == LossSummaryProperty || change.Property == IsUpgradedProperty
                 || change.Property == IsDismissibleProperty || change.Property == IsSnoozableProperty
                 || change.Property == TimeProviderProperty)
        {
            if (change.Property == IsUpgradedProperty && IsUpgraded && _phase != TrialBillingPhase.Idle)
            {
                _phase = TrialBillingPhase.Idle;
            }
            Refresh(instant: false);
        }
        else if (change.Property == LabelsProperty)
        {
            _featuresDirty = true;
            Refresh(instant: false);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_listensToToday)
        {
            _listensToToday = true;
            ArcToday.Changed += OnTodayChanged;
        }
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_listensToToday)
        {
            _listensToToday = false;
            ArcToday.Changed -= OnTodayChanged;
        }
        // Leaving the window cancels a checkout in flight (the popover's card leaves while its banner stays: not that one).
        if (_host is null)
        {
            _billing?.Cancel();
        }
        _noteTimer?.Stop();
    }

    /// <summary>Local midnight: "today" moved, so the days left counted from <see cref="EndsAt"/> may have changed.</summary>
    private void OnTodayChanged(object? sender, EventArgs e) => Refresh(instant: false);

    private void OnFeaturesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TrialFeature feature in e.OldItems)
            {
                feature.PropertyChanged -= OnFeatureChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (TrialFeature feature in e.NewItems)
            {
                feature.PropertyChanged += OnFeatureChanged;
            }
        }
        FeaturesChanged();
    }

    private void OnFeatureChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => FeaturesChanged();

    private void FeaturesChanged()
    {
        _featuresDirty = true;
        Refresh(instant: false);
        if (_mirror is not null)
        {
            _mirror._featuresDirty = true;
            _mirror.Refresh(instant: false);
        }
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (Status == TrialStatus.Upgraded)
        {
            Source.RequestManageBilling();
        }
        else
        {
            _ = Source.AddPaymentAsync();
        }
    }

    private void OnCompareClick(object? sender, RoutedEventArgs e) => Source.RequestComparePlans();

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Source.Dismiss();

    private void OnSnoozeItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is TrialSnoozeOption option)
        {
            Source.Snooze(option);
        }
    }

    /// <summary>The pill opens the card in a popover (pill variant) or the banner again (folded).</summary>
    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        if (Variant == TrialBannerVariant.Pill && !IsCollapsed)
        {
            ToggleDetails();
            return;
        }
        SetCurrentValue(IsCollapsedProperty, false);
        FocusLater(() => ActiveParts?.Primary);
    }

    /// <summary>Opens or closes the card in the pill's popover (Escape and a press outside close it and return focus to the pill).</summary>
    internal void ToggleDetails()
    {
        if (_pill is null)
        {
            return;
        }
        if (_popover?.IsOpen == true)
        {
            _popover.Close();
            return;
        }
        if (_popover is null)
        {
            _mirror = new TrialBanner { _host = this };
            _mirror.UpdateVariantClasses();
            _mirror.Bind(WidthProperty, this.GetResourceObservable(TrialBannerTokens.Key(TrialBannerTokenKind.PopoverCardWidth)));
            // The card is built by the popover's surface from a template: as the popover's own content it would belong to
            // the popover (never shown) instead of the surface. Content first, then the template.
            var mirror = _mirror;
            _popover = new Popover
            {
                Placement = PopoverPlacement.Bottom,
                Alignment = PopoverAlignment.End,
                Offset = 8
            };
            _popover.Content = Labels.Region(Plan);
            _popover.ContentTemplate = new FuncDataTemplate<object>((_, _) => mirror);
            // A popup opens only from the logical tree: the popover joins the banner's.
            LogicalChildren.Add(_popover);
        }
        _popover.ShowAt(_pill);
    }

    /// <summary>Opens checkout: the busy state while the handler runs, the upgraded state when it returns, Try again when it throws.</summary>
    internal async Task AddPaymentAsync()
    {
        if (!HasTrial)
        {
            // No trial to keep: the button is hidden, and nothing runs.
            return;
        }
        if (_host is { } host)
        {
            await host.AddPaymentAsync();
            return;
        }
        if (_phase == TrialBillingPhase.Busy || IsUpgraded)
        {
            return;
        }
        ClearNote();
        var cts = new CancellationTokenSource();
        _billing = cts;
        var handler = AddPaymentHandler;
        var simulated = handler is null;
        SetPhase(TrialBillingPhase.Busy);
        try
        {
            if (handler is null)
            {
                await Task.Delay(SimulatedBilling, TimeProvider, cts.Token);
            }
            else
            {
                await handler(cts.Token);
            }
            cts.Token.ThrowIfCancellationRequested();
            _phase = TrialBillingPhase.Idle;
            SetCurrentValue(IsUpgradedProperty, true);
            Announce(Labels.AnnounceUpgraded(Plan, simulated));
            Refresh(instant: false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            SetPhase(TrialBillingPhase.Idle);
        }
        catch (Exception)
        {
            SetPhase(TrialBillingPhase.Failed);
            Announce(Labels.AnnounceFailed);
        }
        finally
        {
            if (ReferenceEquals(_billing, cts))
            {
                _billing = null;
            }
            cts.Dispose();
        }
    }

    private void SetPhase(TrialBillingPhase phase)
    {
        if (_phase == phase)
        {
            return;
        }
        _phase = phase;
        Refresh(instant: false);
    }

    /// <summary>Raises <see cref="ComparePlansRequested"/>; unhandled, a short simulated note takes the copy's place.</summary>
    internal void RequestComparePlans()
    {
        var args = new RoutedEventArgs(ComparePlansRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
        {
            ShowNote(Labels.PlansNote);
        }
    }

    /// <summary>Raises <see cref="ManageBillingRequested"/>; unhandled, a short simulated note takes the copy's place.</summary>
    internal void RequestManageBilling()
    {
        var args = new RoutedEventArgs(ManageBillingRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
        {
            ShowNote(Labels.BillingNote);
        }
    }

    /// <summary>Raises <see cref="Dismissed"/> and folds the banner.</summary>
    internal void Dismiss()
    {
        RaiseEvent(new RoutedEventArgs(DismissedEvent));
        Announce(Labels.AnnounceDismissed);
        Fold();
    }

    /// <summary>Raises <see cref="Snoozed"/> with when the snooze ends, and folds the banner.</summary>
    internal void Snooze(TrialSnoozeOption option)
    {
        var (days, _, _) = TrialBannerMath.Resolve(DaysLeft, EndsAt, TimeProvider);
        var until = TrialBannerMath.SnoozeUntil(option, days, WarnAt, TimeProvider);
        RaiseEvent(new TrialSnoozedEventArgs(SnoozedEvent, option, until));
        Announce(Labels.AnnounceSnoozed(Labels.Date(TrialBannerMath.LocalDate(until, TimeProvider))));
        Fold();
    }

    /// <summary>
    /// After a dismiss or snooze the banner folds into the pill and focus moves to it (or the banner renders nothing). The
    /// pill variant only closes its popover, focus returning to the pill.
    /// </summary>
    private void Fold()
    {
        if (Variant == TrialBannerVariant.Pill)
        {
            _popover?.Close();
            if (CollapseTo == TrialBannerCollapse.None)
            {
                SetCurrentValue(IsCollapsedProperty, true);
            }
            else
            {
                FocusLater(() => _pill);
            }
            return;
        }
        SetCurrentValue(IsCollapsedProperty, true);
        if (CollapseTo == TrialBannerCollapse.Pill)
        {
            FocusLater(() => _pill);
        }
    }

    private static void FocusLater(Func<Control?> target)
    {
        // With the keyboard ring only when a key collapsed the banner.
        Dispatcher.UIThread.Post(() =>
        {
            if (target() is { } control)
            {
                control.Focus(SpecialFocusModality.For(control));
            }
        }, DispatcherPriority.Loaded);
    }

    private void ShowNote(string text)
    {
        _note = text;
        Announce(text);
        Refresh(instant: false);
        _noteTimer?.Stop();
        _noteTimer ??= new DispatcherTimer { Interval = NoteDuration };
        _noteTimer.Tick -= OnNoteElapsed;
        _noteTimer.Tick += OnNoteElapsed;
        _noteTimer.Start();
    }

    private void OnNoteElapsed(object? sender, EventArgs e) => ClearNote();

    /// <summary>Takes the simulated note away (it also leaves on its own after 2.4s).</summary>
    internal void ClearNote()
    {
        _noteTimer?.Stop();
        if (_note is null)
        {
            return;
        }
        _note = null;
        Refresh(instant: false);
    }

    /// <summary>Says a billing, snooze or dismiss result through the polite live region.</summary>
    private void Announce(string text)
    {
        // The same words twice are still read again.
        _live?.Announce(text);
    }
}
