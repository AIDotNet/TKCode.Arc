using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// Updating UpgradePrompt in place: the texts, the meter, the price, the panels, the upgrade and the announcements.
public partial class UpgradePrompt
{
    private ViewportVisibility? _viewport;
    private bool _revealed;
    private bool _syncing;
    private string? _toggleKey;
    private CancellationTokenSource? _upgradeCancel;
    private UpgradePromptSelection? _lastSelection;
    private bool _lastSimulated;

    /// <summary>
    /// Starts the upgrade with <see cref="CurrentSelection"/>, as Upgrade (or Try again) does: the prompt shows the processing
    /// state, awaits <see cref="UpgradeHandler"/> (or the simulation) and then shows the success or the error state. Does
    /// nothing while an upgrade runs or once it succeeded.
    /// </summary>
    public async Task UpgradeAsync()
    {
        if (IsEmpty || State is UpgradePromptState.Processing or UpgradePromptState.Success)
        {
            return;
        }
        var selection = CurrentSelection;
        var handler = UpgradeHandler;
        var cancel = new CancellationTokenSource();
        _upgradeCancel = cancel;
        ErrorMessage = null;
        SetState(UpgradePromptState.Processing);
        var labels = Labels;
        Announce(handler is null ? labels.AnnounceSimulating(selection.Plan.Name) : labels.AnnounceUpgrading(selection.Plan.Name));
        try
        {
            if (handler is not null)
            {
                await handler(selection, cancel.Token);
            }
            else
            {
                await Task.Delay(SimulatedDuration, cancel.Token);
            }
            cancel.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Aborted because the prompt left the tree (or was reset): back to the offer, quietly.
            if (_upgradeCancel == cancel)
            {
                _upgradeCancel = null;
            }
            if (State == UpgradePromptState.Processing)
            {
                SetState(UpgradePromptState.Offer, animate: false);
            }
            cancel.Dispose();
            return;
        }
        catch (Exception error)
        {
            _upgradeCancel = null;
            cancel.Dispose();
            ErrorMessage = string.IsNullOrWhiteSpace(error.Message) ? Labels.GenericError : error.Message;
            SetState(UpgradePromptState.Error);
            Announce(Labels.AnnounceFailed(ErrorMessage));
            RaiseEvent(new UpgradePromptFailedEventArgs(UpgradeFailedEvent, selection, error));
            return;
        }
        _upgradeCancel = null;
        cancel.Dispose();
        _lastSelection = selection;
        _lastSimulated = handler is null;
        SetState(UpgradePromptState.Success);
        Announce(Labels.AnnounceUpgraded(selection.Plan.Name, UpgradePromptFormat.Count(selection.Plan.Limit), Unit));
        RaiseEvent(new UpgradePromptUpgradedEventArgs(UpgradedEvent, selection));
    }

    /// <summary>Maybe later: raises <see cref="Dismissed"/> and shows the reminder. Does nothing while an upgrade runs or without a plan.</summary>
    public void Dismiss()
    {
        if (IsEmpty || State is UpgradePromptState.Processing or UpgradePromptState.Success or UpgradePromptState.Reminder)
        {
            return;
        }
        ErrorMessage = null;
        SetState(UpgradePromptState.Reminder);
        Announce(Labels.AnnounceReminder(ResetsOn));
        RaiseEvent(new RoutedEventArgs(DismissedEvent));
    }

    /// <summary>See Pro again: from the reminder back to the offer.</summary>
    public void ShowOffer()
    {
        if (State != UpgradePromptState.Reminder)
        {
            return;
        }
        SetState(UpgradePromptState.Offer);
    }

    /// <summary>Back to the offer from any state; a running upgrade is cancelled and the meter settles to the current limit.</summary>
    public void Reset()
    {
        AbortUpgrade();
        ErrorMessage = null;
        _lastSelection = null;
        SetState(UpgradePromptState.Offer);
    }

    /// <summary>Continue on the success state: raises <see cref="ContinueRequested"/> and resets unless it was handled.</summary>
    private void Continue()
    {
        if (State != UpgradePromptState.Success)
        {
            return;
        }
        var args = new RoutedEventArgs(ContinueRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
        {
            Reset();
        }
    }

    private void AbortUpgrade()
    {
        var cancel = _upgradeCancel;
        _upgradeCancel = null;
        if (cancel is not null && !cancel.IsCancellationRequested)
        {
            cancel.Cancel();
        }
        if (State == UpgradePromptState.Processing)
        {
            SetState(UpgradePromptState.Offer, animate: false);
        }
    }

    private void SetState(UpgradePromptState state, bool animate = true)
    {
        State = state;
        Refresh(animate);
    }

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        _status.Text = message;
        AutomationProperties.SetName(_status, message);
    }

    /// <summary>The panel of the body and the footer for a state.</summary>
    internal static int PanelOf(UpgradePromptState state) => state switch
    {
        UpgradePromptState.Success => SuccessPanel,
        UpgradePromptState.Reminder => ReminderPanel,
        _ => OfferPanel
    };

    private void ApplyBreakpoint(double width)
    {
        var narrowWidth = this.TryFindResource(UpgradePromptTokens.Key(UpgradePromptTokenKind.NarrowWidth), out var value) && value is double d ? d : 420;
        PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < narrowWidth);
    }

    /// <summary>
    /// The bar fills once, the first time the card is mostly in view (Full motion); otherwise it is drawn at its value at once.
    /// </summary>
    private void WatchViewport()
    {
        if (_bar is null || _revealed || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            Reveal(animate: false);
            return;
        }
        if (_viewport is null)
        {
            _viewport = new ViewportVisibility(this, 0.5, once: true);
            _viewport.VisibilityChanged += (_, visible) =>
            {
                if (visible)
                {
                    Reveal(animate: true);
                }
            };
            if (_viewport.IsVisible)
            {
                Reveal(animate: true);
            }
        }
    }

    private void Reveal(bool animate)
    {
        if (_revealed || _bar is null)
        {
            return;
        }
        _revealed = true;
        var (used, limit) = MeterValues();
        _bar.SetShare(UpgradePromptFormat.Fill(used, limit), animate);
    }

    /// <summary>The meter's used amount and limit: the plan's limit once upgraded, the current one before.</summary>
    internal (double Used, double Limit) MeterValues() =>
        State == UpgradePromptState.Success ? (Used, ShownPlan.Limit) : (Used, Limit);

    private void Refresh(bool animate)
    {
        if (_root is null || _title is null || _description is null || _bar is null || _body is null || _footer is null)
        {
            return;
        }
        animate = animate && IsLoaded;
        var plan = ShownPlan;
        var labels = Labels;
        var unit = Unit;
        var state = State;
        var (used, limit) = MeterValues();
        var atLimit = UpgradePromptFormat.IsAtLimit(used, limit);
        var left = UpgradePromptFormat.Count(UpgradePromptFormat.Left(used, limit));
        var empty = IsEmpty;
        AutomationProperties.SetName(this, empty ? labels.EmptyTitle : labels.Region(plan.Name));
        // Without a plan there is nothing to measure against or offer: only the header and the footer show.
        _meterBlock!.IsVisible = !empty;
        _hairline!.IsVisible = !empty;
        _bodyHost!.IsVisible = !empty;

        RefreshHeader(state, plan, labels, unit, atLimit, limit, left, animate);
        RefreshMeter(state, plan, labels, unit, used, limit, atLimit, left, animate);
        RefreshOffer(state, plan, labels, animate);
        RefreshSuccess(plan, labels);
        _reminderLine!.Show(labels.ReminderLine(unit), animate);
        _reminderHint!.Show(labels.ReminderHint(plan.Name), animate);
        RefreshFooter(state, plan, labels);

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var panel = PanelOf(state);
        var lostBody = _body.Show(panel, animate, focused);
        var lostFooter = _footer.Show(panel, animate, focused);
        if (lostBody || lostFooter)
        {
            var target = panel switch
            {
                SuccessPanel => _continue,
                ReminderPanel => _seeAgain,
                _ => _upgrade
            };
            var keyboard = focused is Control { Classes: var classes } && classes.Contains(":focus-visible");
            target?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void RefreshHeader(UpgradePromptState state, UpgradePromptPlan plan, UpgradePromptLabels labels, string unit, bool atLimit,
        double limit, string left, bool animate)
    {
        string title;
        string description;
        Geometry glyph;
        ArcDesignTokenKind fill;
        ArcDesignTokenKind ink;
        switch (state)
        {
            case UpgradePromptState.Success:
                title = labels.SuccessTitle(plan.Name);
                description = labels.SuccessDescription;
                glyph = ArcIcons.Check;
                fill = ArcDesignTokenKind.SuccessSubtle;
                ink = ArcDesignTokenKind.Success;
                break;
            case UpgradePromptState.Reminder:
                title = labels.ReminderTitle;
                description = labels.ReminderDescription(unit, ResetsOn);
                glyph = ArcIcons.Clock;
                fill = ArcDesignTokenKind.SurfaceMuted;
                ink = ArcDesignTokenKind.TextSecondary;
                break;
            case var _ when IsEmpty:
                title = Title ?? labels.EmptyTitle;
                description = Description ?? string.Empty;
                glyph = ArcIcons.Zap;
                fill = ArcDesignTokenKind.SurfaceMuted;
                ink = ArcDesignTokenKind.TextSecondary;
                break;
            default:
                title = Title ?? (atLimit
                    ? labels.TitleAtLimit(UpgradePromptFormat.Count(limit), unit)
                    : labels.TitleBelowLimit(left, unit));
                description = Description ?? labels.Description(plan.Name, UpgradePromptFormat.Count(plan.Limit), unit, ResetsOn);
                glyph = ArcIcons.Zap;
                fill = atLimit ? ArcDesignTokenKind.WarningSubtle : ArcDesignTokenKind.AccentSubtle;
                ink = atLimit ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.Accent;
                break;
        }
        _title!.Show(title, animate);
        _description!.Show(description, animate);
        _badgeFill!.Set(fill);
        _badgeInk!.Set(ink);
        if (!ReferenceEquals(_badgeGlyph, glyph))
        {
            var first = _badgeGlyph is null;
            _badgeGlyph = glyph;
            _badgeSwap!.Mode = animate && !first && ArcMotion.IsFull(this) ? ContentSwapMode.Pop : ContentSwapMode.None;
            _badgeSwap.Content = new ArcGlyph { Data = glyph }.PromptTok(ArcGlyph.SizeProperty, UpgradePromptTokenKind.BadgeGlyphSize);
        }
    }

    private void RefreshMeter(UpgradePromptState state, UpgradePromptPlan plan, UpgradePromptLabels labels, string unit, double used,
        double limit, bool atLimit, string left, bool animate)
    {
        var usedText = UpgradePromptFormat.Count(used);
        var limitText = UpgradePromptFormat.Count(limit);
        _meterLabel!.Show(labels.MeterLabel(unit), animate);
        _meterCount!.Show(labels.MeterCount(usedText, limitText), animate);
        Geometry glyph;
        string status;
        if (atLimit)
        {
            glyph = ArcIcons.CircleAlert;
            status = labels.LimitReached;
            _statusInk!.Set(ArcDesignTokenKind.Warning);
            _statusTextInk!.Set(ArcDesignTokenKind.Warning);
            _barFill!.Set(ArcDesignTokenKind.Warning);
        }
        else
        {
            var upgraded = state == UpgradePromptState.Success;
            glyph = upgraded ? ArcIcons.CircleCheck : ArcIcons.Gauge;
            status = labels.Left(left, unit);
            _statusInk!.Set(upgraded ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextMuted);
            _statusTextInk!.Set(upgraded ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextSecondary);
            _barFill!.Set(upgraded ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Accent);
        }
        _statusText!.Show(status, animate);
        if (!ReferenceEquals(_statusGlyph, glyph))
        {
            var first = _statusGlyph is null;
            _statusGlyph = glyph;
            _statusIconSwap!.Mode = animate && !first && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off
                ? ContentSwapMode.Label
                : ContentSwapMode.None;
            _statusIconSwap.Content = new ArcGlyph { Data = glyph }.PromptTok(ArcGlyph.SizeProperty, UpgradePromptTokenKind.StatusIconSize);
        }
        _meterNote!.Show(state == UpgradePromptState.Success ? labels.PlanLimit(plan.Name)
            : string.IsNullOrEmpty(ResetsOn) ? string.Empty : labels.ResetsOn(ResetsOn), animate);

        var value = labels.MeterValue(usedText, limitText, unit);
        AutomationProperties.SetName(_meter!, value);
        AutomationProperties.SetHelpText(_meter!, status);
        if (_revealed)
        {
            _bar!.SetShare(UpgradePromptFormat.Fill(used, limit), animate);
        }
    }

    private void RefreshOffer(UpgradePromptState state, UpgradePromptPlan plan, UpgradePromptLabels labels, bool animate)
    {
        var decimals = UpgradePromptFormat.Decimals(plan);
        var perMonth = UpgradePromptFormat.PerMonth(plan, Interval);
        var charge = UpgradePromptFormat.Charge(plan, Interval);
        _planName!.Text = plan.Name;
        _price!.Prefix = UpgradePromptFormat.Symbol(plan.Currency);
        _price.Decimals = decimals;
        _price.IsInstant = !animate;
        _price.Value = (double)perMonth;
        _period!.Text = labels.PerMonth;
        var note = Interval == UpgradePromptInterval.Yearly
            ? labels.BilledYearly(UpgradePromptFormat.Money(charge, plan.Currency, decimals))
            : labels.BilledMonthly;
        _billingNote!.Show(note, animate);
        AutomationProperties.SetName(_priceGroup!, labels.PriceText(UpgradePromptFormat.Money(perMonth, plan.Currency, decimals), note));

        RefreshToggle(plan, labels);
        _toggle!.IsEnabled = state != UpgradePromptState.Processing;

        var error = state == UpgradePromptState.Error && !string.IsNullOrEmpty(ErrorMessage);
        if (error)
        {
            _errorText!.Show(ErrorMessage!, animate: false);
        }
        _errorLine!.Opacity = error ? 1 : 0;
        AutomationProperties.SetAccessibilityView(_errorLine, error ? AccessibilityView.Default : AccessibilityView.Raw);
        _billingNote.Opacity = error ? 0 : 1;

        _unlocksHeading!.Text = labels.Unlocks(plan.Name);
        var key = string.Join("\u001f", plan.Features.Select(f => f.Id + "\u001e" + f.Label + "\u001e" + f.Mark?.GetHashCode() + "\u001e" + f.MarkBrush?.GetHashCode()));
        if (key != _featuresKey)
        {
            _featuresKey = key;
            _features!.Children.Clear();
            foreach (var feature in plan.Features)
            {
                _features.Children.Add(FeatureRow(feature));
            }
        }
    }

    private void RefreshToggle(UpgradePromptPlan plan, UpgradePromptLabels labels)
    {
        var saving = UpgradePromptFormat.SavingPercent(plan);
        string? badge = saving > 0 ? labels.SavePercent(saving) : null;
        var key = string.Join("\u001f", labels.Monthly, labels.Yearly, badge);
        _syncing = true;
        try
        {
            if (key != _toggleKey)
            {
                _toggleKey = key;
                _toggle!.ItemsSource = new[]
                {
                    new BillingToggleOption(ValueOf(UpgradePromptInterval.Monthly), labels.Monthly),
                    new BillingToggleOption(ValueOf(UpgradePromptInterval.Yearly), labels.Yearly, badge)
                };
            }
            AutomationProperties.SetName(_toggle!, labels.BillingPeriod);
            _toggle!.Value = ValueOf(Interval);
        }
        finally
        {
            _syncing = false;
        }
    }

    internal static string ValueOf(UpgradePromptInterval interval) => interval == UpgradePromptInterval.Yearly ? "yearly" : "monthly";

    private void RefreshSuccess(UpgradePromptPlan plan, UpgradePromptLabels labels)
    {
        _limitsHeading!.Text = labels.NewLimits;
        var key = string.Join("\u001f", plan.Limits.Select(l => l.Label + "\u001e" + l.Value + "\u001e" + l.Mark?.GetHashCode() + "\u001e" + l.MarkBrush?.GetHashCode()));
        if (key != _limitsKey)
        {
            _limitsKey = key;
            _limits!.Children.Clear();
            foreach (var limit in plan.Limits)
            {
                _limits.Children.Add(LimitRow(limit));
            }
        }
        var selection = _lastSelection;
        var charged = selection is null
            ? string.Empty
            : _lastSimulated
                ? labels.SimulatedCharge
                : labels.Charged(UpgradePromptFormat.Money(selection.Amount, selection.Currency));
        _chargedNote!.Show(charged, animate: false);
    }

    private void RefreshFooter(UpgradePromptState state, UpgradePromptPlan plan, UpgradePromptLabels labels)
    {
        var empty = IsEmpty;
        _later!.Content = labels.MaybeLater;
        _later.IsEnabled = !empty && state != UpgradePromptState.Processing;
        _upgrade!.Content = state switch
        {
            _ when empty => labels.EmptyUpgrade,
            UpgradePromptState.Processing => labels.Upgrading,
            UpgradePromptState.Error => labels.TryAgain,
            _ => labels.Upgrade(plan.Name)
        };
        _upgrade.IsEnabled = !empty;
        _upgrade.IsLoading = state == UpgradePromptState.Processing;
        _continue!.Content = labels.Continue;
        _seeAgain!.Content = labels.SeeAgain(plan.Name);
    }

    /// <summary>
    /// The toggle settles its selection while it attaches (it may pick its first option); only changes after it loaded
    /// come from the person, so it is put back on <see cref="Interval"/> once it is loaded.
    /// </summary>
    private void OnToggleLoaded(object? sender, RoutedEventArgs e)
    {
        if (_toggle is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _toggle.Value = ValueOf(Interval);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnToggleChanged(object? sender, BillingToggleValueChangedEventArgs e)
    {
        if (_syncing || e.NewValue is null || _toggle is not { IsLoaded: true })
        {
            return;
        }
        var interval = e.NewValue == ValueOf(UpgradePromptInterval.Yearly) ? UpgradePromptInterval.Yearly : UpgradePromptInterval.Monthly;
        if (interval == Interval)
        {
            return;
        }
        SetCurrentValue(IntervalProperty, interval);
        RaiseEvent(new UpgradePromptIntervalChangedEventArgs(IntervalChangedEvent, interval));
    }
}
