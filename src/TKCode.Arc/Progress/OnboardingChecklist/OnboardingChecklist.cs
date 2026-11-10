// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): the settling, focus and keyboard rules.
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="OnboardingChecklist.CompletedChanged"/>.</summary>
public sealed class OnboardingChecklistCompletedChangedEventArgs : RoutedEventArgs
{
    public OnboardingChecklistCompletedChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> completed) : base(routedEvent)
    {
        Completed = completed;
    }

    /// <summary>The keys of the finished steps, in step order.</summary>
    public IReadOnlyList<string> Completed { get; }
}

/// <summary>Arguments of <see cref="OnboardingChecklist.HiddenChanged"/>.</summary>
public sealed class OnboardingChecklistHiddenChangedEventArgs : RoutedEventArgs
{
    public OnboardingChecklistHiddenChangedEventArgs(RoutedEvent routedEvent, bool isHidden) : base(routedEvent)
    {
        IsHidden = isHidden;
    }

    /// <summary>Whether the body is now hidden.</summary>
    public bool IsHidden { get; }
}

/// <summary>
/// Arc onboarding checklist: a getting started list for a new account or workspace. The ring and count follow every
/// check, finished steps strike through and fold into a completed row that opens again, and the next step opens and takes
/// focus. Use it for a short, one time setup of three to eight steps; use a stepper or a step form when the order is strict.
/// </summary>
[TemplatePart(OpenListPart, typeof(StackPanel))]
[TemplatePart(CompletedListPart, typeof(StackPanel))]
[PseudoClasses(BodyHiddenPseudoClass, GroupOpenPseudoClass, NarrowPseudoClass, StartedPseudoClass)]
public class OnboardingChecklist : TemplatedControl
{
    public const string OpenListPart = "PART_OpenList";
    public const string CompletedListPart = "PART_CompletedList";
    public const string BodyHiddenPseudoClass = ":body-hidden";
    public const string GroupOpenPseudoClass = ":group-open";
    public const string NarrowPseudoClass = ":narrow";
    public const string StartedPseudoClass = ":started";

    /// <summary>How long a checked step stays in place, struck through, before it folds into the completed row.</summary>
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(720);

    /// <summary>At this width and below, the hide button keeps its spoken name and shows only the chevron.</summary>
    private const double NarrowWidth = 380;

    private static readonly Avalonia.Media.Geometry s_ringTick = ArcIcons.Parse("M8.1 12.3 10.8 15l5.1-5.7");
    private static readonly TimeSpan s_ringTickDelay = TimeSpan.FromSeconds(0.12);

    /// <summary>The title while steps remain.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<OnboardingChecklist, string?>(nameof(Title));

    /// <summary>The title once every step is done.</summary>
    public static readonly StyledProperty<string?> DoneTitleProperty =
        AvaloniaProperty.Register<OnboardingChecklist, string?>(nameof(DoneTitle), "You’re all set");

    /// <summary>Bound steps; takes precedence over the inline steps.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<OnboardingChecklist, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The keys of the finished steps. Setting it folds those steps away at once, without motion or events.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> CompletedProperty =
        AvaloniaProperty.Register<OnboardingChecklist, IReadOnlyList<string>?>(nameof(Completed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Hides the body, leaving the header as a bar.</summary>
    public static readonly StyledProperty<bool> IsHiddenProperty =
        AvaloniaProperty.Register<OnboardingChecklist, bool>(nameof(IsHidden), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The fixed words, visible and spoken; English by default.</summary>
    public static readonly StyledProperty<OnboardingChecklistStrings> StringsProperty =
        AvaloniaProperty.Register<OnboardingChecklist, OnboardingChecklistStrings>(nameof(Strings), OnboardingChecklistStrings.Default);

    /// <summary>Whether every step is done.</summary>
    public static readonly DirectProperty<OnboardingChecklist, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<OnboardingChecklist, bool>(nameof(IsComplete), o => o.IsComplete);

    /// <summary>Raised when the finished steps change through a person or an action; setting Completed does not raise it.</summary>
    public static readonly RoutedEvent<OnboardingChecklistCompletedChangedEventArgs> CompletedChangedEvent =
        RoutedEvent.Register<OnboardingChecklist, OnboardingChecklistCompletedChangedEventArgs>(nameof(CompletedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a person hides or shows the body; setting IsHidden does not raise it.</summary>
    public static readonly RoutedEvent<OnboardingChecklistHiddenChangedEventArgs> HiddenChangedEvent =
        RoutedEvent.Register<OnboardingChecklist, OnboardingChecklistHiddenChangedEventArgs>(nameof(HiddenChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CompletedChangedEvent"/>
    public event EventHandler<OnboardingChecklistCompletedChangedEventArgs>? CompletedChanged
    {
        add => AddHandler(CompletedChangedEvent, value);
        remove => RemoveHandler(CompletedChangedEvent, value);
    }

    /// <inheritdoc cref="HiddenChangedEvent"/>
    public event EventHandler<OnboardingChecklistHiddenChangedEventArgs>? HiddenChanged
    {
        add => AddHandler(HiddenChangedEvent, value);
        remove => RemoveHandler(HiddenChangedEvent, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DoneTitleProperty"/>
    public string? DoneTitle { get => GetValue(DoneTitleProperty); set => SetValue(DoneTitleProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="CompletedProperty"/>
    public IReadOnlyList<string>? Completed { get => GetValue(CompletedProperty); set => SetValue(CompletedProperty, value); }

    /// <inheritdoc cref="IsHiddenProperty"/>
    public bool IsHidden { get => GetValue(IsHiddenProperty); set => SetValue(IsHiddenProperty, value); }

    /// <inheritdoc cref="StringsProperty"/>
    public OnboardingChecklistStrings Strings { get => GetValue(StringsProperty); set => SetValue(StringsProperty, value); }

    /// <inheritdoc cref="IsCompleteProperty"/>
    public bool IsComplete { get => _isComplete; private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value); }

    /// <summary>Inline steps (<c>&lt;arc:OnboardingChecklistStep Title="…"/&gt;</c>); ignored while ItemsSource is set.</summary>
    [Content]
    public AvaloniaList<OnboardingChecklistStep> Steps { get; } = new();

    private sealed class Entry
    {
        public required OnboardingChecklistFold Fold { get; init; }
        public required Control Row { get; init; }
        public int Order;
    }

    private readonly OnboardingChecklistModel _model = new();
    private readonly Dictionary<string, Entry> _open = new();
    private readonly Dictionary<string, Entry> _closedRows = new();
    private readonly Dictionary<string, IDisposable> _timers = new();
    private List<string> _reported = new();
    private bool _initialized;
    private bool _writingCompleted;
    private bool _focusNext;
    private string? _focusStep;
    private bool _isComplete;
    private INotifyCollectionChanged? _observed;

    private OnboardingChecklistSwapText? _title;
    private OnboardingChecklistNumber? _countNumber;
    private TextBlock? _countText;
    private Control? _countRow;
    private Control? _ring;
    private ArcRing? _ringFill;
    private OnboardingChecklistSwap? _ringTick;
    private OnboardingChecklistHideButton? _hide;
    private OnboardingChecklistSwapText? _hideLabel;
    private OnboardingChecklistFold? _body;
    private StackPanel? _openList;
    private OnboardingChecklistFold? _group;
    private OnboardingChecklistGroupToggle? _groupToggle;
    private OnboardingChecklistNumber? _groupNumber;
    private TextBlock? _groupText;
    private OnboardingChecklistFold? _groupPanel;
    private StackPanel? _completedList;

    static OnboardingChecklist()
    {
        FocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<OnboardingChecklist>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<OnboardingChecklist>(AutomationControlType.Group);
        AutomationProperties.LiveSettingProperty.OverrideDefaultValue<OnboardingChecklist>(AutomationLiveSetting.Polite);
    }

    public OnboardingChecklist()
    {
        Steps.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                OnStepsChanged();
            }
        };
    }

    /// <summary>Checks a step off, as its check would.</summary>
    public void CompleteStep(string id)
    {
        EnsureInitialized();
        CheckStep(id);
    }

    /// <summary>Marks a finished step as not done, as its check would.</summary>
    public void ReopenStep(string id)
    {
        EnsureInitialized();
        UncheckStep(id, fromGroup: false);
    }

    internal OnboardingChecklistModel Model => _model;

    private IReadOnlyList<OnboardingChecklistStep> CurrentSteps()
    {
        var list = new List<OnboardingChecklistStep>();
        var seen = new HashSet<string>();
        IEnumerable source = ItemsSource ?? Steps;
        foreach (var item in source)
        {
            // Keys are what the state is made of, so a repeated one is ignored.
            if (item is OnboardingChecklistStep step && seen.Add(step.Key))
            {
                list.Add(step);
            }
        }
        return list;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        _model.Strings = Strings;
        _model.Init(CurrentSteps(), Completed);
        _reported = _model.Done;
    }

    private void OnStepsChanged()
    {
        if (!_initialized)
        {
            return;
        }
        _model.SetSteps(CurrentSteps());
        Refresh(IsLoaded);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveItems();
            OnStepsChanged();
        }
        else if (change.Property == StringsProperty)
        {
            _model.Strings = Strings;
            RefreshIfReady();
        }
        else if (change.Property == CompletedProperty)
        {
            if (!_writingCompleted && _initialized)
            {
                foreach (var timer in _timers.Values)
                {
                    timer.Dispose();
                }
                _timers.Clear();
                _model.SetCompleted(Completed);
                _reported = _model.Done;
                RefreshIfReady();
            }
        }
        else if (change.Property == TitleProperty || change.Property == DoneTitleProperty || change.Property == IsHiddenProperty)
        {
            RefreshIfReady();
        }
    }

    private void RefreshIfReady()
    {
        if (_initialized)
        {
            Refresh(IsLoaded);
        }
    }

    private void ObserveItems()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsChanged;
        }
        _observed = this.IsAttachedToVisualTree() ? ItemsSource as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnStepsChanged();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveItems();
        if (_initialized)
        {
            _model.SetSteps(CurrentSteps());
            Refresh(false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnItemsChanged;
            _observed = null;
        }
        // A step that was still settling has settled by the time the checklist comes back.
        foreach (var (id, timer) in _timers)
        {
            timer.Dispose();
            _model.Settle(id);
        }
        _timers.Clear();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(NarrowPseudoClass, e.NewSize.Width <= NarrowWidth);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_hide is not null)
        {
            _hide.Click -= OnHideClick;
        }
        if (_groupToggle is not null)
        {
            _groupToggle.Click -= OnGroupClick;
        }
        _openList?.RemoveHandler(KeyDownEvent, OnListKeyDown);
        if (_groupPanel is not null)
        {
            _groupPanel.Closed -= OnGroupPanelClosed;
        }

        var scope = e.NameScope;
        _title = scope.Find<OnboardingChecklistSwapText>("PART_Title");
        _countNumber = scope.Find<OnboardingChecklistNumber>("PART_CountNumber");
        _countText = scope.Find<TextBlock>("PART_CountText");
        _countRow = scope.Find<Control>("PART_CountRow");
        _ring = scope.Find<Control>("PART_Ring");
        _ringFill = scope.Find<ArcRing>("PART_RingFill");
        _ringTick = scope.Find<OnboardingChecklistSwap>("PART_RingTick");
        _hide = scope.Find<OnboardingChecklistHideButton>("PART_Hide");
        _hideLabel = scope.Find<OnboardingChecklistSwapText>("PART_HideLabel");
        _body = scope.Find<OnboardingChecklistFold>("PART_Body");
        _openList = scope.Find<StackPanel>(OpenListPart);
        _group = scope.Find<OnboardingChecklistFold>("PART_Group");
        _groupToggle = scope.Find<OnboardingChecklistGroupToggle>("PART_GroupToggle");
        _groupNumber = scope.Find<OnboardingChecklistNumber>("PART_GroupNumber");
        _groupText = scope.Find<TextBlock>("PART_GroupText");
        _groupPanel = scope.Find<OnboardingChecklistFold>("PART_GroupPanel");
        _completedList = scope.Find<StackPanel>(CompletedListPart);

        if (_hide is not null)
        {
            _hide.Click += OnHideClick;
        }
        if (_groupToggle is not null)
        {
            _groupToggle.Click += OnGroupClick;
        }
        _openList?.AddHandler(KeyDownEvent, OnListKeyDown);
        if (_groupPanel is not null)
        {
            _groupPanel.Closed += OnGroupPanelClosed;
            _groupPanel.SetOpen(false, false);
        }
        _group?.SetOpen(false, false);

        _open.Clear();
        _closedRows.Clear();
        EnsureInitialized();
        Refresh(false);
    }

    private void OnHideClick(object? sender, RoutedEventArgs e)
    {
        var hidden = !IsHidden;
        SetCurrentValue(IsHiddenProperty, hidden);
        RaiseEvent(new OnboardingChecklistHiddenChangedEventArgs(HiddenChangedEvent, hidden));
    }

    private void OnGroupClick(object? sender, RoutedEventArgs e)
    {
        _model.ToggleGroup();
        Refresh(IsLoaded);
    }

    private void OnGroupPanelClosed(object? sender, EventArgs e)
    {
        // Finished rows exist only while the group is open.
        _completedList?.Children.Clear();
        _closedRows.Clear();
    }

    internal void ToggleStep(string id, bool fromGroup)
    {
        if (_model.IsDone(id))
        {
            UncheckStep(id, fromGroup);
        }
        else
        {
            CheckStep(id);
        }
    }

    internal void ExpandStep(string id)
    {
        _model.Expand(id);
        Refresh(IsLoaded);
    }

    internal async void RunStep(OnboardingChecklistStep step)
    {
        var id = step.Key;
        if (_model.IsPending(id) || _model.IsDone(id) || (step.Action is null && step.Command is null))
        {
            return;
        }
        _model.Start(id);
        Refresh(IsLoaded);
        try
        {
            if (step.Action is { } action)
            {
                await action();
            }
            else if (step.Command is { } command && command.CanExecute(step.CommandParameter))
            {
                command.Execute(step.CommandParameter);
            }
            CheckStep(id);
        }
        catch (Exception exception)
        {
            _model.Fail(id, string.IsNullOrEmpty(exception.Message) ? Strings.ActionFailed : exception.Message);
            Refresh(IsLoaded);
        }
    }

    private void CheckStep(string id)
    {
        if (!_model.Check(id))
        {
            return;
        }
        if (!_timers.ContainsKey(id))
        {
            _timers[id] = DispatcherTimer.RunOnce(() => SettleStep(id), s_settle);
        }
        Refresh(IsLoaded);
    }

    private void SettleStep(string id)
    {
        _timers.Remove(id);
        if (_open.TryGetValue(id, out var entry) && entry.Row.IsKeyboardFocusWithin)
        {
            _focusNext = true;
        }
        _model.Settle(id);
        Refresh(IsLoaded);
    }

    private void UncheckStep(string id, bool fromGroup)
    {
        if (_timers.Remove(id, out var timer))
        {
            timer.Dispose();
        }
        if (fromGroup)
        {
            _focusStep = id;
        }
        _model.Uncheck(id);
        Refresh(IsLoaded);
    }

    /// <summary>Up and Down walk the step titles, Home and End jump to the first and last.</summary>
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not OnboardingChecklistTitleButton current || _openList is null ||
            e.Key is not (Key.Down or Key.Up or Key.Home or Key.End))
        {
            return;
        }
        var titles = new List<OnboardingChecklistTitleButton>();
        foreach (var child in _openList.Children)
        {
            if (child is OnboardingChecklistFold { IsOpen: true, Child: OnboardingChecklistStepRow { TitleButton: { } title } })
            {
                titles.Add(title);
            }
        }
        var index = titles.IndexOf(current);
        if (index < 0)
        {
            return;
        }
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => titles.Count - 1,
            Key.Down => (index + 1) % titles.Count,
            _ => (index - 1 + titles.Count) % titles.Count
        };
        e.Handled = true;
        titles[next].Focus(NavigationMethod.Directional);
    }

    private void Refresh(bool animate)
    {
        var model = _model;
        var strings = Strings;
        var total = model.Total;
        var done = model.Done;
        var doneCount = done.Count;
        var allDone = total > 0 && doneCount == total;
        IsComplete = allDone;

        var title = Title ?? string.Empty;
        var doneTitle = DoneTitle ?? string.Empty;
        var heading = allDone ? doneTitle : title;
        _title?.Set(heading, title, doneTitle, animate);
        AutomationProperties.SetName(this, heading);
        AutomationProperties.SetItemStatus(this, model.Message);

        _countNumber?.Set(doneCount, animate);
        var countSuffix = string.Format(CultureInfo.CurrentCulture, strings.CountFormat, total);
        if (_countText is not null)
        {
            _countText.Text = " " + countSuffix;
        }
        if (_countRow is not null)
        {
            AutomationProperties.SetName(_countRow, $"{doneCount} {countSuffix}");
        }

        RefreshRing(strings, doneCount, total, allDone, animate);
        RefreshHide(strings, animate);
        SyncOpenRows(animate);
        RefreshGroup(strings, animate);
        ReportCompleted(done);
        ApplyFocusRequests();
    }

    private void RefreshRing(OnboardingChecklistStrings strings, int doneCount, int total, bool allDone, bool animate)
    {
        var ratio = total > 0 ? (double)doneCount / total : 0;
        PseudoClasses.Set(StartedPseudoClass, ratio > 0);
        if (_ringFill is not null)
        {
            _ringFill.Value = ratio;
        }
        if (_ring is not null)
        {
            AutomationProperties.SetName(_ring, strings.Progress);
            AutomationProperties.SetItemStatus(_ring, string.Format(CultureInfo.CurrentCulture, strings.ProgressFormat, doneCount, total));
        }
        if (_ringTick is null || allDone == _ringTick.Current is not null)
        {
            return;
        }
        if (!allDone)
        {
            _ringTick.Show(null, 1, animate);
            return;
        }
        // A tick draws in the ring's center once every step is done; 40 box coordinates scaled to the glyph's 24.
        var tick = new StrokeDrawGlyph { Data = s_ringTick, Size = 40, StrokeThickness = 1.35 };
        _ringTick.Show(tick, 1, animate);
        if (animate && ArcMotion.IsFull(this))
        {
            tick.Progress = 0;
            _ = tick.Play(ArcMotionPresets.Standard, ArcEasings.Enter, s_ringTickDelay);
        }
    }

    private void RefreshHide(OnboardingChecklistStrings strings, bool animate)
    {
        var hidden = IsHidden;
        PseudoClasses.Set(BodyHiddenPseudoClass, hidden);
        _hideLabel?.Set(hidden ? strings.Show : strings.Hide, strings.Hide, strings.Show, animate);
        if (_hide is not null)
        {
            _hide.SetIsOpen(!hidden);
            AutomationProperties.SetName(_hide, hidden ? strings.ShowChecklist : strings.HideChecklist);
        }
        if (_body is not null)
        {
            if (hidden && _body.IsKeyboardFocusWithin)
            {
                // A hidden body is out of reach: focus moves to the button that brings it back.
                _hide?.Focus(FocusModality.For(this));
            }
            _body.SetOpen(!hidden, animate);
        }
    }

    private void SyncOpenRows(bool animate)
    {
        if (_openList is null)
        {
            return;
        }
        var target = new List<OnboardingChecklistStep>();
        foreach (var step in _model.Steps)
        {
            if (!_model.IsSettled(step.Key))
            {
                target.Add(step);
            }
        }
        SyncList(_openList, _open, target, animate, CreateOpenRow);
        foreach (var step in target)
        {
            var key = step.Key;
            var row = (OnboardingChecklistStepRow)_open[key].Row;
            row.SetStep(step);
            row.Update(_model.IsDone(key), _model.Expanded == key, _model.IsPending(key), _model.ErrorOf(key), animate);
        }
        AutomationProperties.SetName(_openList, Strings.OpenSteps);
        AutomationProperties.SetAccessibilityView(_openList, target.Count > 0 ? AccessibilityView.Default : AccessibilityView.Raw);
    }

    private Control CreateOpenRow(OnboardingChecklistStep step) => new OnboardingChecklistStepRow(step, this);

    private Control CreateCompletedRow(OnboardingChecklistStep step) => new OnboardingChecklistCompletedRow(step, this);

    private void RefreshGroup(OnboardingChecklistStrings strings, bool animate)
    {
        var completed = new List<OnboardingChecklistStep>();
        foreach (var step in _model.Steps)
        {
            if (_model.IsSettled(step.Key))
            {
                completed.Add(step);
            }
        }
        var groupWasOpen = _group?.IsOpen == true;
        _group?.SetOpen(completed.Count > 0, animate);
        var groupOpen = _model.GroupOpen;
        PseudoClasses.Set(GroupOpenPseudoClass, groupOpen);
        if (completed.Count > 0)
        {
            // The group's number keeps its last value while the group folds away.
            _groupNumber?.Set(completed.Count, animate && groupWasOpen);
        }
        if (_groupText is not null)
        {
            _groupText.Text = " " + strings.Completed;
        }
        if (_groupToggle is not null)
        {
            _groupToggle.SetIsOpen(groupOpen);
            AutomationProperties.SetName(_groupToggle, $"{completed.Count} {strings.Completed}");
        }
        if (_completedList is not null)
        {
            AutomationProperties.SetName(_completedList, strings.CompletedSteps);
        }
        // Nested presences start without motion when the group itself is arriving.
        var panelWasOpen = _groupPanel?.IsOpen == true;
        if (groupOpen && _completedList is not null)
        {
            // Rows inside an opening panel arrive with it; only a row joining an open panel folds in by itself.
            SyncList(_completedList, _closedRows, completed, animate && panelWasOpen && groupWasOpen, CreateCompletedRow);
            foreach (var step in completed)
            {
                if (_closedRows[step.Key].Row is OnboardingChecklistCompletedRow row)
                {
                    row.SetStep(step);
                }
            }
        }
        _groupPanel?.SetOpen(groupOpen, animate && groupWasOpen);
    }

    /// <summary>Keyed rows that enter and leave by folding; a row still leaving comes back if its step does (AnimatePresence).</summary>
    private static void SyncList(StackPanel list, Dictionary<string, Entry> rows, List<OnboardingChecklistStep> target, bool animate, Func<OnboardingChecklistStep, Control> create)
    {
        var keep = new HashSet<string>();
        foreach (var step in target)
        {
            keep.Add(step.Key);
        }
        foreach (var (key, entry) in rows.ToArray())
        {
            if (!keep.Contains(key))
            {
                SetLeaving(entry.Row, true);
                entry.Fold.SetOpen(false, animate);
            }
        }
        for (var order = 0; order < target.Count; order++)
        {
            var key = target[order].Key;
            if (rows.TryGetValue(key, out var entry))
            {
                entry.Order = order;
                SetLeaving(entry.Row, false);
                entry.Fold.SetOpen(true, animate);
                continue;
            }
            var row = create(target[order]);
            var fold = new OnboardingChecklistFold { Kind = OnboardingChecklistFoldKind.Row, Child = row };
            fold.SetOpen(false, false);
            entry = new Entry { Fold = fold, Row = row, Order = order };
            rows[key] = entry;
            fold.Closed += (_, _) =>
            {
                if (rows.TryGetValue(key, out var current) && ReferenceEquals(current.Fold, fold) && !fold.IsOpen)
                {
                    rows.Remove(key);
                    list.Children.Remove(fold);
                }
            };
            list.Children.Insert(InsertionIndex(list, rows, order), fold);
            fold.SetOpen(true, animate);
        }
        Reorder(list, rows);
    }

    private static void SetLeaving(Control row, bool leaving)
    {
        if (row is OnboardingChecklistStepRow stepRow)
        {
            stepRow.IsLeaving = leaving;
        }
    }

    private static int InsertionIndex(StackPanel list, Dictionary<string, Entry> rows, int order)
    {
        for (var i = 0; i < list.Children.Count; i++)
        {
            foreach (var entry in rows.Values)
            {
                if (ReferenceEquals(entry.Fold, list.Children[i]) && entry.Order > order)
                {
                    return i;
                }
            }
        }
        return list.Children.Count;
    }

    /// <summary>Puts rows that are staying back in step order when the list itself was reordered.</summary>
    private static void Reorder(StackPanel list, Dictionary<string, Entry> rows)
    {
        var sorted = rows.Values.Where(e => e.Fold.IsOpen).OrderBy(e => e.Order).ToList();
        var position = 0;
        for (var i = 0; i < list.Children.Count && position < sorted.Count; i++)
        {
            if (rows.Values.Any(e => ReferenceEquals(e.Fold, list.Children[i]) && e.Fold.IsOpen))
            {
                if (!ReferenceEquals(list.Children[i], sorted[position].Fold))
                {
                    list.Children.Move(list.Children.IndexOf(sorted[position].Fold), i);
                }
                position++;
            }
        }
    }

    private void ReportCompleted(List<string> done)
    {
        if (done.SequenceEqual(_reported))
        {
            return;
        }
        _reported = done;
        _writingCompleted = true;
        try
        {
            SetCurrentValue(CompletedProperty, done);
        }
        finally
        {
            _writingCompleted = false;
        }
        RaiseEvent(new OnboardingChecklistCompletedChangedEventArgs(CompletedChangedEvent, done));
    }

    /// <summary>Focus moves after the update that mounts its target: the next step's action, or the step reopened from the completed row.</summary>
    private void ApplyFocusRequests()
    {
        if (_focusStep is { } id)
        {
            _focusStep = null;
            if (_open.TryGetValue(id, out var entry) && entry.Row is OnboardingChecklistStepRow row)
            {
                row.Check?.Focus(FocusModality.For(this));
            }
        }
        if (!_focusNext)
        {
            return;
        }
        _focusNext = false;
        Control? target = null;
        if (_model.Expanded is { } expanded && _open.TryGetValue(expanded, out var next) && next.Row is OnboardingChecklistStepRow nextRow && !nextRow.IsLeaving)
        {
            nextRow.ApplyTemplate();
            target = (Control?)nextRow.PanelPart?.ActionButton ?? nextRow.TitleButton;
        }
        target ??= _group?.IsOpen == true ? _groupToggle : null;
        target?.Focus(FocusModality.For(this));
    }
}
