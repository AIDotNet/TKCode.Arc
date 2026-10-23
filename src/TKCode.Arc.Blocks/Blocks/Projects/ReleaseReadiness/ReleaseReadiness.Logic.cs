using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(OwnersPart, typeof(ReleaseReadinessOwners))]
[TemplatePart(AvatarsPart, typeof(ProjectsAvatarStack))]
[TemplatePart(RemainingPart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(BarPart, typeof(ProjectsProgressLine))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(FooterPart, typeof(ReleaseReadinessFooter))]
[TemplatePart(NotifyPart, typeof(Switch))]
[TemplatePart(ResetPart, typeof(Button))]
[TemplatePart(ReleasePart, typeof(Button))]
[TemplatePart(StatusHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(StatusPart, typeof(ReleaseReadinessMessage))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(AlertPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, ReadyPseudoClass, ReleasingPseudoClass, ReleasedPseudoClass,
    FailedPseudoClass, StatusPseudoClass)]
public partial class ReleaseReadiness
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string OwnersPart = "PART_Owners";
    public const string AvatarsPart = "PART_Avatars";
    public const string RemainingPart = "PART_Remaining";
    public const string CountPart = "PART_Count";
    public const string BarPart = "PART_Bar";
    public const string RowsPart = "PART_Rows";
    public const string FooterPart = "PART_Footer";
    public const string NotifyPart = "PART_Notify";
    public const string ResetPart = "PART_Reset";
    public const string ReleasePart = "PART_Release";
    public const string StatusHostPart = "PART_StatusHost";
    public const string StatusPart = "PART_Status";
    public const string AnnouncerPart = "PART_Announcer";
    public const string AlertPart = "PART_Alert";

    /// <summary>Set while <see cref="IsFramed"/> is true: the bordered card.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is 388 wide or less: side paddings of 16 instead of 20.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while every required check is done (the bar turns to the success color).</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Set while the release action runs.</summary>
    public const string ReleasingPseudoClass = ":releasing";

    /// <summary>Set after the release started, until a reset.</summary>
    public const string ReleasedPseudoClass = ":released";

    /// <summary>Set after a failed attempt.</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while the status line under the footer is open.</summary>
    public const string StatusPseudoClass = ":status";

    #region Token-driven internals

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(CompactWidth), 388);

    internal static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(GlyphSize), 16);

    internal static readonly StyledProperty<double> GlyphStrokeProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(GlyphStroke), 2);

    internal static readonly StyledProperty<double> GlyphGapProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(GlyphGap), 8);

    /// <summary>The bar's value and maximum. Template-bound so the bar is created with them and shows them without a slide.</summary>
    internal static readonly StyledProperty<double> BarValueProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(BarValue));

    internal static readonly StyledProperty<double> BarMaximumProperty =
        AvaloniaProperty.Register<ReleaseReadiness, double>(nameof(BarMaximum), 1);

    internal double BarValue { get => GetValue(BarValueProperty); set => SetValue(BarValueProperty, value); }

    internal double BarMaximum { get => GetValue(BarMaximumProperty); set => SetValue(BarMaximumProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    internal double GlyphStroke { get => GetValue(GlyphStrokeProperty); set => SetValue(GlyphStrokeProperty, value); }

    internal double GlyphGap { get => GetValue(GlyphGapProperty); set => SetValue(GlyphGapProperty, value); }

    #endregion

    private readonly List<ReleaseCheck> _checks = new();
    private readonly Dictionary<ReleaseCheck, ReleaseReadinessRow> _rows = new();
    private INotifyCollectionChanged? _observedChecks;

    private TextBlock? _title;
    private TextBlock? _subtitle;
    private ReleaseReadinessOwners? _owners;
    private ProjectsAvatarStack? _avatars;
    private TextBlock? _remaining;
    private TextBlock? _count;
    private ProjectsProgressLine? _bar;
    private StackPanel? _rowsPanel;
    private Switch? _notify;
    private Button? _reset;
    private Button? _release;
    private SpringSizeDecorator? _statusHost;
    private ReleaseReadinessMessage? _status;
    private ProjectsAnnouncer? _announcer;
    private ProjectsAnnouncer? _alert;

    private TextBlock? _labelText;
    private bool? _labelIsDone;
    private INotifyCollectionChanged? _observedOwners;

    private CancellationTokenSource? _cancellation;
    private ReleaseRequest? _lastRequest;
    private int _generation;
    private int _attempt;
    private bool _bulk;
    private bool _syncingNotify;

    static ReleaseReadiness()
    {
        FocusableProperty.OverrideDefaultValue<ReleaseReadiness>(false);
    }

    public ReleaseReadiness()
    {
        Checks.CollectionChanged += (_, _) =>
        {
            if (ChecksSource is null)
            {
                RebuildChecks();
            }
        };
        Owners.CollectionChanged += (_, _) => UpdateOwners();
        PseudoClasses.Set(FramedPseudoClass, true);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The checks the block shows now (from <see cref="ChecksSource"/> or <see cref="Checks"/>), in order.</summary>
    public IReadOnlyList<ReleaseCheck> EffectiveChecks => _checks;

    internal IReadOnlyList<ReleaseReadinessRow> Rows => _rowsPanel?.Children.OfType<ReleaseReadinessRow>().ToList() ?? [];

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal ProjectsAnnouncer? Alert => _alert;

    #region Public methods

    /// <summary>
    /// Starts the release as the button does: raises <see cref="ReleaseStarting"/>, runs <see cref="ReleaseCommand"/> and
    /// awaits <see cref="ReleaseAction"/>. Does nothing (and returns false) unless the gate is ready or the last attempt
    /// failed. Returns true when the release started without an error.
    /// </summary>
    public async Task<bool> ReleaseAsync()
    {
        if (State is not (ReleaseReadinessState.Ready or ReleaseReadinessState.Failed) || !IsReady)
        {
            return false;
        }
        var request = new ReleaseRequest(IsNotifyEnabled, _checks.ToArray(), _attempt + 1);
        var starting = new ReleaseStartingEventArgs(ReleaseStartingEvent, this, request);
        RaiseEvent(starting);
        if (starting.Cancel || State is not (ReleaseReadinessState.Ready or ReleaseReadinessState.Failed))
        {
            return false;
        }
        var command = ReleaseCommand;
        if (command is not null && !command.CanExecute(request))
        {
            return false;
        }
        _attempt = request.Attempt;
        _lastRequest = request;
        ErrorText = null;
        var generation = ++_generation;
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        SetState(ReleaseReadinessState.Releasing);
        Announce(ReleasingText);
        try
        {
            command?.Execute(request);
            if (ReleaseAction is { } action)
            {
                await action(request, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // CancelRelease (or the block leaving the window) already put the gate back.
            return false;
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                Fail(request, exception);
            }
            return false;
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }
            cancellation.Dispose();
        }
        if (generation != _generation)
        {
            return false;
        }
        Succeed(request);
        return true;
    }

    /// <summary>
    /// Puts every check back to its first value (<see cref="ReleaseCheck.IsInitiallyDone"/>, or the value it had when it
    /// was first shown), closes the status line and hides the reset button. The notify switch keeps its value. A
    /// running release is cancelled first. When the focus was inside the block it moves to the first open check.
    /// </summary>
    public void Reset()
    {
        var previous = State;
        var focus = CaptureFocus();
        if (previous == ReleaseReadinessState.Releasing)
        {
            CancelRunning();
        }
        _bulk = true;
        try
        {
            foreach (var check in _checks.ToArray())
            {
                check.SetCurrentValue(ReleaseCheck.IsDoneProperty, check.ResetValue);
            }
        }
        finally
        {
            _bulk = false;
        }
        _attempt = 0;
        _lastRequest = null;
        ErrorText = null;
        UpdateCounts(raiseState: false);
        SetState(ComputedState());
        Announce(RemainingTextNow());
        RaiseEvent(new ReleaseResetEventArgs(ResetPerformedEvent, this, previous));
        if (focus.Inside)
        {
            FocusAfterReset(focus.Method);
        }
    }

    /// <summary>
    /// Cancels a running release: the action's token is cancelled and the gate goes back to ready at once (a late
    /// result of the action is ignored). Does nothing unless <see cref="State"/> is Releasing.
    /// </summary>
    public void CancelRelease()
    {
        if (State != ReleaseReadinessState.Releasing)
        {
            return;
        }
        CancelRunning();
        SetState(ComputedState());
        Announce(RemainingTextNow());
    }

    #endregion

    #region Template

    /// <summary>The block is a group named by its title (Arc's section labelled by its heading).</summary>
    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ReleaseReadinessGroupPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_notify is not null)
        {
            _notify.IsCheckedChanged -= OnNotifyChanged;
        }
        if (_release is not null)
        {
            _release.Click -= OnReleaseClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        _rowsPanel?.Children.Clear();

        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _owners = e.NameScope.Find<ReleaseReadinessOwners>(OwnersPart);
        _avatars = e.NameScope.Find<ProjectsAvatarStack>(AvatarsPart);
        _remaining = e.NameScope.Find<TextBlock>(RemainingPart);
        _count = e.NameScope.Find<TextBlock>(CountPart);
        _bar = e.NameScope.Find<ProjectsProgressLine>(BarPart);
        _rowsPanel = e.NameScope.Find<StackPanel>(RowsPart);
        _notify = e.NameScope.Find<Switch>(NotifyPart);
        _reset = e.NameScope.Find<Button>(ResetPart);
        _release = e.NameScope.Find<Button>(ReleasePart);
        _statusHost = e.NameScope.Find<SpringSizeDecorator>(StatusHostPart);
        _status = e.NameScope.Find<ReleaseReadinessMessage>(StatusPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        _alert = e.NameScope.Find<ProjectsAnnouncer>(AlertPart);

        if (_bar is not null)
        {
            // Arc's readiness bar is a 580ms CSS spring transition with no overshoot (critically damped, ω 13.8).
            _bar.Motion = MotionSpec.Spring(ProjectsSprings.CssSpring);
        }
        if (_notify is not null)
        {
            _notify.IsChecked = IsNotifyEnabled;
            _notify.IsCheckedChanged += OnNotifyChanged;
        }
        if (_release is not null)
        {
            _labelIsDone = null;
            _labelText = null;
            _release.Click += OnReleaseClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }

        RebuildChecks();
        UpdateHeader();
        UpdateOwners();
        UpdateNotifyText();
        UpdateParts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ChecksSourceProperty)
        {
            if (_observedChecks is not null)
            {
                _observedChecks.CollectionChanged -= OnSourceChanged;
            }
            _observedChecks = ChecksSource as INotifyCollectionChanged;
            if (_observedChecks is not null)
            {
                _observedChecks.CollectionChanged += OnSourceChanged;
            }
            RebuildChecks();
        }
        else if (property == OwnersSourceProperty)
        {
            if (_observedOwners is not null)
            {
                _observedOwners.CollectionChanged -= OnOwnersSourceChanged;
            }
            _observedOwners = OwnersSource as INotifyCollectionChanged;
            if (_observedOwners is not null)
            {
                _observedOwners.CollectionChanged += OnOwnersSourceChanged;
            }
            UpdateOwners();
        }
        else if (property == OwnersLabelProperty || property == OwnersLabelPrefixProperty || property == AndWordProperty)
        {
            UpdateOwners();
        }
        else if (property == TitleProperty || property == SubtitleProperty)
        {
            UpdateHeader();
            UpdateParts();
        }
        else if (property == IsNotifyEnabledProperty)
        {
            if (_notify is not null && !_syncingNotify)
            {
                _syncingNotify = true;
                _notify.SetCurrentValue(ToggleButton.IsCheckedProperty, IsNotifyEnabled);
                _syncingNotify = false;
            }
        }
        else if (property == NotifyTextProperty || property == NotifyFormatProperty || property == ChannelNameProperty)
        {
            UpdateNotifyText();
            UpdateParts();
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
        else if (property == IsNotifyVisibleProperty || property == IsResetVisibleProperty || property == ReleaseTextProperty
                 || property == ReleasedTextProperty || property == ResetTextProperty || property == RemainingFormatProperty
                 || property == RemainingOneTextProperty || property == ReadyTextProperty || property == ReleasingTextProperty
                 || property == CountFormatProperty || property == SuccessFormatProperty
                 || property == SuccessNotifiedFormatProperty || property == ReleaseNameProperty
                 || property == ChecksLabelProperty || property == ProgressLabelProperty)
        {
            UpdateParts();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A release still running when the block leaves the window is cancelled, like an unmounted request.
        CancelRelease();
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var row in _rows.Values)
        {
            row.IsCompact = compact;
        }
    }

    #endregion

    #region Checks

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildChecks();

    private void OnOwnersSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateOwners();

    private void RebuildChecks()
    {
        var source = (IEnumerable?)ChecksSource ?? Checks;
        var next = new List<ReleaseCheck>();
        foreach (var item in source)
        {
            if (item is ReleaseCheck check && !next.Contains(check))
            {
                next.Add(check);
            }
        }
        foreach (var gone in _checks.Where(c => !next.Contains(c)).ToArray())
        {
            gone.PropertyChanged -= OnCheckPropertyChanged;
            if (_rows.Remove(gone, out var row))
            {
                row.Detach();
            }
        }
        foreach (var added in next.Where(c => !_checks.Contains(c)))
        {
            added.FirstSeenDone ??= added.IsDone;
            added.PropertyChanged += OnCheckPropertyChanged;
        }
        _checks.Clear();
        _checks.AddRange(next);

        if (_rowsPanel is not null)
        {
            var compact = PseudoClasses.Contains(CompactPseudoClass);
            var rows = new List<Control>(_checks.Count);
            for (var i = 0; i < _checks.Count; i++)
            {
                var check = _checks[i];
                if (!_rows.TryGetValue(check, out var row))
                {
                    row = new ReleaseReadinessRow(check);
                    _rows[check] = row;
                }
                row.IsFirst = i == 0;
                row.IsCompact = compact;
                row.IsLocked = IsLocked;
                rows.Add(row);
            }
            var same = _rowsPanel.Children.Count == rows.Count && rows.Select((r, i) => ReferenceEquals(_rowsPanel.Children[i], r)).All(x => x);
            if (!same)
            {
                _rowsPanel.Children.Clear();
                _rowsPanel.Children.AddRange(rows);
            }
        }
        UpdateCounts(raiseState: true);
        UpdateParts();
    }

    private void OnCheckPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ReleaseCheck check)
        {
            return;
        }
        if (e.Property == ReleaseCheck.IsDoneProperty)
        {
            var oldValue = e.GetOldValue<bool>();
            var newValue = e.GetNewValue<bool>();
            UpdateCounts(raiseState: !_bulk);
            RaiseEvent(new ReleaseCheckChangedEventArgs(CheckChangedEvent, this, check, oldValue, newValue, DoneCount, TotalCount));
            if (!_bulk)
            {
                Announce(RemainingTextNow());
            }
            UpdateParts();
        }
        else if (e.Property == ReleaseCheck.IsRequiredProperty)
        {
            UpdateCounts(raiseState: !_bulk);
            UpdateParts();
        }
    }

    private void UpdateCounts(bool raiseState)
    {
        var total = 0;
        var done = 0;
        foreach (var check in _checks)
        {
            if (check.IsRequired)
            {
                total++;
                if (check.IsDone)
                {
                    done++;
                }
            }
        }
        TotalCount = total;
        DoneCount = done;
        var ready = _checks.Count > 0 && done == total;
        // Maximum first: a value above the old maximum would be clamped for a frame.
        SetCurrentValue(BarMaximumProperty, total == 0 ? 1.0 : total);
        SetCurrentValue(BarValueProperty, total == 0 ? (ready ? 1.0 : 0.0) : done);
        if (ready != IsReady)
        {
            IsReady = ready;
            PseudoClasses.Set(ReadyPseudoClass, ready);
            RaiseEvent(new ReleaseReadyChangedEventArgs(ReadyChangedEvent, this, ready));
        }
        if (!raiseState)
        {
            return;
        }
        // A change to the checks leaves a failed attempt behind (its error no longer describes the gate).
        if (State is ReleaseReadinessState.Collecting or ReleaseReadinessState.Ready or ReleaseReadinessState.Failed)
        {
            if (State == ReleaseReadinessState.Failed)
            {
                ErrorText = null;
            }
            SetState(ComputedState());
        }
    }

    private ReleaseReadinessState ComputedState() => IsReady ? ReleaseReadinessState.Ready : ReleaseReadinessState.Collecting;

    private bool IsLocked => State is ReleaseReadinessState.Releasing or ReleaseReadinessState.Released;

    #endregion

    #region Release flow

    private void OnReleaseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = ReleaseAsync();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Reset();
    }

    private void OnNotifyChanged(object? sender, RoutedEventArgs e)
    {
        if (_notify is null || _syncingNotify)
        {
            return;
        }
        _syncingNotify = true;
        SetCurrentValue(IsNotifyEnabledProperty, _notify.IsChecked == true);
        _syncingNotify = false;
    }

    private void CancelRunning()
    {
        _generation++;
        var cancellation = _cancellation;
        _cancellation = null;
        if (cancellation is null)
        {
            return;
        }
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The attempt finished in the same turn; nothing left to cancel.
        }
    }

    private void Succeed(ReleaseRequest request)
    {
        var focus = CaptureFocus();
        SetState(ReleaseReadinessState.Released);
        Announce(StatusText);
        RaiseEvent(new ReleasedEventArgs(ReleasedEvent, this, request));
        if (focus.Inside)
        {
            FocusAfterSuccess(focus.Method);
        }
    }

    private void Fail(ReleaseRequest request, Exception exception)
    {
        var focus = CaptureFocus();
        string text;
        try
        {
            text = ErrorFormatter?.Invoke(exception) ?? exception.Message;
        }
        catch (Exception)
        {
            text = exception.Message;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            text = FailureText;
        }
        ErrorText = text;
        SetState(ReleaseReadinessState.Failed);
        _alert?.Announce(text);
        RaiseEvent(new ReleaseFailedEventArgs(ReleaseFailedEvent, this, request, exception, text));
        // The release button stays enabled for a retry and kept the focus; put it back if something took it.
        if (focus.Inside && _release is { IsEffectivelyEnabled: true, IsFocused: false })
        {
            _release.Focus(focus.Method);
        }
    }

    private void SetState(ReleaseReadinessState state)
    {
        var old = State;
        State = state;
        PseudoClasses.Set(ReleasingPseudoClass, state == ReleaseReadinessState.Releasing);
        PseudoClasses.Set(ReleasedPseudoClass, state == ReleaseReadinessState.Released);
        PseudoClasses.Set(FailedPseudoClass, state == ReleaseReadinessState.Failed);
        UpdateParts();
        if (old != state)
        {
            RaiseEvent(new ReleaseStateChangedEventArgs(StateChangedEvent, this, old, state));
        }
    }

    #endregion

    #region Focus

    private (bool Inside, NavigationMethod Method) CaptureFocus()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is null || !(ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused)))
        {
            return (false, NavigationMethod.Unspecified);
        }
        var keyboard = focused is StyledElement styled && styled.Classes.Contains(":focus-visible");
        return (true, keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    private void FocusAfterSuccess(NavigationMethod method)
    {
        // Arc drops the focus to the page when the button disables itself; the reset button (or the status line) keeps it.
        if (_reset is { IsVisible: true, IsEffectivelyEnabled: true })
        {
            _reset.Focus(method);
        }
        else
        {
            _status?.Focus(method);
        }
    }

    private void FocusAfterReset(NavigationMethod method)
    {
        // Arc drops the focus with the vanished reset button; the first open check takes it.
        var rows = Rows;
        var target = rows.FirstOrDefault(r => !r.Check.IsDone)?.Checkbox as Control
                     ?? (_release is { IsEffectivelyEnabled: true } ? _release : rows.FirstOrDefault()?.Checkbox);
        target?.Focus(method);
    }

    #endregion

    #region Parts

    private void UpdateHeader()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = Subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        }
        AutomationProperties.SetName(this, Title ?? string.Empty);
    }

    private void UpdateOwners()
    {
        if (_avatars is null || _owners is null)
        {
            return;
        }
        var source = OwnersSource ?? Owners;
        var people = source.OfType<ProjectPerson>().ToList();
        _avatars.People = OwnersSource ?? Owners;
        var label = OwnersLabel ?? OwnersLabelPrefix + ProjectsText.JoinNames(people.Select(p => p.Name), AndWord);
        _avatars.Label = label;
        AutomationProperties.SetName(_owners, label);
        _owners.IsVisible = people.Count > 0;
    }

    private void UpdateNotifyText()
    {
        if (_notify is null)
        {
            return;
        }
        var text = NotifyText ?? ProjectsText.Format(NotifyFormat, ChannelName ?? string.Empty).Trim();
        _notify.Content = text;
        AutomationProperties.SetName(_notify, text);
    }

    /// <summary>The progress text for the current counts and state.</summary>
    internal string RemainingTextNow()
    {
        if (State is ReleaseReadinessState.Releasing or ReleaseReadinessState.Released)
        {
            return ReleasingText;
        }
        var left = TotalCount - DoneCount;
        if (left <= 0)
        {
            return IsReady ? ReadyText : ProjectsText.Format(RemainingFormat, 0);
        }
        return left == 1 ? RemainingOneText : ProjectsText.Format(RemainingFormat, left);
    }

    private string SuccessTextNow()
    {
        var name = ReleaseName ?? Title ?? string.Empty;
        var notified = (_lastRequest?.Notify ?? IsNotifyEnabled) && !string.IsNullOrEmpty(ChannelName);
        return notified
            ? ProjectsText.Format(SuccessNotifiedFormat, name, ChannelName)
            : ProjectsText.Format(SuccessFormat, name);
    }

    private void Announce(string? text)
    {
        // Only what the person caused is spoken: nothing before the block is on screen.
        if (IsLoaded)
        {
            _announcer?.Announce(text);
        }
    }

    private void UpdateParts()
    {
        var state = State;
        var locked = IsLocked;
        if (_remaining is not null)
        {
            _remaining.Text = RemainingTextNow();
        }
        if (_count is not null)
        {
            _count.Text = ProjectsText.Format(CountFormat, DoneCount, TotalCount);
        }
        if (_bar is not null)
        {
            AutomationProperties.SetName(_bar, ProgressLabel);
        }
        if (_rowsPanel is not null)
        {
            AutomationProperties.SetName(_rowsPanel, ChecksLabel);
        }
        foreach (var row in _rows.Values)
        {
            row.IsLocked = locked;
        }
        if (_notify is not null)
        {
            _notify.IsVisible = IsNotifyVisible;
            _notify.IsEnabled = !locked;
        }

        // Reset first: it takes the focus before the release button disables itself.
        if (_reset is not null)
        {
            _reset.Content = ResetText;
            AutomationProperties.SetName(_reset, ResetText);
            _reset.IsVisible = state == ReleaseReadinessState.Released && IsResetVisible;
        }
        if (_release is not null)
        {
            var released = state == ReleaseReadinessState.Released;
            if (_labelIsDone != released)
            {
                // A new label for every swap: the old one stays in the button while it blurs out, so it cannot come back.
                var (panel, text) = released
                    ? ReleaseReadinessLabel.Create(true, ArcIcons.CircleCheck, GlyphSize, GlyphStroke, GlyphGap)
                    : ReleaseReadinessLabel.Create(false, ArcIcons.ArrowRight, GlyphSize, GlyphStroke, GlyphGap);
                _labelIsDone = released;
                _labelText = text;
                _labelText.Text = released ? ReleasedText : ReleaseText;
                _release.Content = panel;
            }
            else if (_labelText is not null)
            {
                _labelText.Text = released ? ReleasedText : ReleaseText;
            }
            AutomationProperties.SetName(_release, released ? ReleasedText : ReleaseText);
            _release.IsLoading = state == ReleaseReadinessState.Releasing;
            AutomationProperties.SetItemStatus(_release, state == ReleaseReadinessState.Releasing ? ReleasingText : null);
            _release.IsEnabled = state == ReleaseReadinessState.Releasing
                                 || (IsReady && state is ReleaseReadinessState.Ready or ReleaseReadinessState.Failed);
        }

        string? status = state switch
        {
            ReleaseReadinessState.Released => SuccessTextNow(),
            ReleaseReadinessState.Failed => ErrorText,
            _ => null,
        };
        StatusText = status;
        PseudoClasses.Set(StatusPseudoClass, status is not null);
        if (_status is not null)
        {
            if (status is not null)
            {
                // The closing line keeps its last text while it folds away.
                _status.Text = status;
                _status.IsError = state == ReleaseReadinessState.Failed;
            }
            // A closed (or closing) line is not read: its text no longer describes the gate.
            AutomationProperties.SetAccessibilityView(_status, status is null ? AccessibilityView.Raw : AccessibilityView.Content);
        }
        if (_statusHost is not null)
        {
            _statusHost.IsExpanded = status is not null;
        }
    }

    #endregion
}
