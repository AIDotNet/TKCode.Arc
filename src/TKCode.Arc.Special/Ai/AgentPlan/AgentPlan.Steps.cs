using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AgentPlan
{
    private readonly Dictionary<AgentPlanStep, AgentPlanRow> _rows = new(ReferenceEqualityComparer.Instance);
    private AvaloniaList<AgentPlanStep>? _observed;
    private readonly HashSet<AgentPlanStep> _watchedSteps = new(ReferenceEqualityComparer.Instance);
    private bool _attached;
    private AgentPlanStatus _simulatedStatus = AgentPlanStatus.Draft;
    private AgentPlanSimulation? _defaultSimulation;
    private AgentPlanSimulation? _attachedSimulation;
    private bool _sampleFilled;
    private bool _inBatch;
    private bool _hadFocus;
    private AgentPlanStep? _streamStep;
    private AgentPlanStatus _previousStatus = AgentPlanStatus.Draft;
    private int _previousFailed = -1;

    internal AgentPlanSimulation? ActiveSimulation => Status is null ? _attachedSimulation : null;

    // Property changes -------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepsProperty)
        {
            Observe(change.GetNewValue<AvaloniaList<AgentPlanStep>>());
            if (_built)
            {
                RebuildRows();
                RefreshAll(animate: false);
            }
        }
        else if (change.Property == StatusProperty)
        {
            OnStatusSourceChanged();
        }
        else if (change.Property == SimulationProperty)
        {
            if (_attachedSimulation is not null && TopLevel.GetTopLevel(this) is not null)
            {
                AttachSimulation();
            }
        }
        else if (change.Property == LabelsProperty || change.Property == GoalProperty || change.Property == ModelProperty ||
                 change.Property == SummaryProperty || change.Property == ForegroundInkProperty ||
                 change.Property == SecondaryInkProperty || change.Property == MutedInkProperty)
        {
            RefreshAll(animate: false);
        }
        else if (change.Property == IsPlanningProperty || change.Property == CanKeepPlanningProperty)
        {
            if (_built && _header is not null)
            {
                RefreshFooter(animate: false);
            }
        }
        else if (change.Property == BoundsProperty)
        {
            ApplyNarrow();
        }
        else if (change.Property == Avalonia.Controls.Primitives.TemplatedControl.TemplateProperty)
        {
            _built = false;
        }
    }

    private void OnStatusSourceChanged()
    {
        if (Status is not null)
        {
            _attachedSimulation?.Detach();
            _attachedSimulation = null;
        }
        else if (TopLevel.GetTopLevel(this) is not null)
        {
            AttachSimulation();
        }
        OnCurrentStatusChanged();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        WatchSteps();
        if (Status is null)
        {
            if (_attachedSimulation is null)
            {
                AttachSimulation();
            }
            else
            {
                _attachedSimulation.Wake();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe(null);
        _attachedSimulation?.Suspend();
    }

    private void AttachSimulation()
    {
        _attachedSimulation?.Detach();
        var simulation = Simulation ?? (_defaultSimulation ??= new AgentPlanSimulation());
        _attachedSimulation = simulation;
        var filled = simulation.Attach(this);
        if (filled)
        {
            _sampleFilled = true;
        }
        if (_built)
        {
            RefreshAll(animate: false);
        }
    }

    // Status ---------------------------------------------------------------------------------------------------------------

    void IAgentPlanSimulationHost.SetSimulatedStatus(AgentPlanStatus status)
    {
        if (_simulatedStatus == status)
        {
            return;
        }
        var old = CurrentStatus;
        _simulatedStatus = status;
        RaisePropertyChanged(CurrentStatusProperty, old, CurrentStatus);
        OnCurrentStatusChanged();
    }

    void IAgentPlanSimulationHost.SetPlanning(bool busy, bool canKeepPlanning)
    {
        SetCurrentValue(IsPlanningProperty, busy);
        SetCurrentValue(CanKeepPlanningProperty, canKeepPlanning);
    }

    void IAgentPlanSimulationHost.InsertSuggestion(AgentPlanStep step, int index) => StreamSuggestion(step, index);

    private void OnCurrentStatusChanged()
    {
        var status = CurrentStatus;
        if (!_built)
        {
            _previousStatus = status;
            return;
        }
        var old = _previousStatus;
        _previousStatus = status;
        if (old != status)
        {
            _hadFocus |= IsKeyboardFocusWithin;
            AnnounceStatus(old, status);
        }
        RefreshAll(animate: IsLoaded);
        if (old != status || FailedIndexChanged())
        {
            MoveFocusIfNeeded();
        }
    }

    private bool FailedIndexChanged()
    {
        var failed = AgentPlanFormat.FailedIndex(Steps);
        var changed = failed != _previousFailed;
        _previousFailed = failed;
        return changed;
    }

    private void AnnounceStatus(AgentPlanStatus old, AgentPlanStatus status)
    {
        var labels = Labels;
        switch (status)
        {
            case AgentPlanStatus.Running when old == AgentPlanStatus.Draft:
                var text = labels.AnnounceApproved(Steps.Count);
                Announce(Status is null ? text + " " + labels.AnnounceSimulated : text);
                break;
            case AgentPlanStatus.Running when old == AgentPlanStatus.Paused:
                Announce(labels.AnnounceResumed);
                break;
            case AgentPlanStatus.Paused:
                Announce(labels.AnnouncePaused);
                break;
            case AgentPlanStatus.Done:
                Announce(labels.AnnounceFinished(labels.Duration(AgentPlanFormat.Total(Steps)), EffectiveSummary));
                break;
            case AgentPlanStatus.Draft when old != AgentPlanStatus.Draft:
                Announce(labels.AnnounceBackToDraft);
                break;
        }
    }

    // Focus ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// After a state change that hides the focused control (Approve hides Approve plan, a failure hides Pause, done hides
    /// Pause, Start over hides Start over): focus goes where the user continues, so it never drops to the page.
    /// </summary>
    private void MoveFocusIfNeeded()
    {
        if (!_hadFocus)
        {
            return;
        }
        _hadFocus = false;
        // The old layer is still on screen for a moment: move the focus once layout has run.
        Dispatcher.UIThread.Post(() => FocusForState(), DispatcherPriority.Loaded);
    }

    private bool FocusForState()
    {
        var status = CurrentStatus;
        var failed = AgentPlanFormat.FailedIndex(Steps);
        if (status == AgentPlanStatus.Draft)
        {
            return _approveButton.IsEffectivelyEnabled && _approveButton.Focus(NavigationMethod.Tab);
        }
        if (failed >= 0 && Steps[failed] is { } step && _rows.TryGetValue(step, out var row))
        {
            return row.Retry.Focus(NavigationMethod.Tab);
        }
        if (status == AgentPlanStatus.Done)
        {
            return _startOverButton.Focus(NavigationMethod.Tab);
        }
        return _pauseButton.Focus(NavigationMethod.Tab);
    }

    // Announcements -------------------------------------------------------------------------------------------------------

    internal void Announce(string? text) => _announcer?.Announce(text);

    // Rows ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The steps may be the host's (a view model's list and objects that outlive the plan): the list and the steps are
    /// observed only while the plan is attached. Unwatches every step; rows watch theirs again as they are made.
    /// </summary>
    private void Observe(AvaloniaList<AgentPlanStep>? list)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnStepsChanged;
        }
        foreach (var step in _watchedSteps)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
            step.Activities.CollectionChanged -= OnActivitiesChanged;
        }
        _watchedSteps.Clear();
        _observed = _attached ? list : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnStepsChanged;
        }
    }

    /// <summary>Listens to the steps again on attach and brings the rows up to date with what changed while it did not.</summary>
    private void WatchSteps()
    {
        Observe(Steps);
        if (!_built || _list is null)
        {
            // The rows are made from the steps when the template is applied.
            return;
        }
        // Catch up as the changes would have shown live: rows of removed steps go and new steps get a row (both at once; a
        // streamed suggestion still streams), the rows follow the list's order and every row shows its step as it is now.
        var steps = Steps;
        var current = new HashSet<AgentPlanStep>(steps, ReferenceEqualityComparer.Instance);
        foreach (var gone in _rows.Keys.Where(step => !current.Contains(step)).ToList())
        {
            RemoveRow(gone, animate: false);
        }
        for (var i = 0; i < steps.Count; i++)
        {
            if (_rows.TryGetValue(steps[i], out var row))
            {
                Watch(steps[i]);
                _list.Move(row, i);
            }
            else
            {
                InsertRow(steps[i], i, animate: false);
            }
        }
        RefreshAll(animate: false);
        _ = FailedIndexChanged();
    }

    private void Watch(AgentPlanStep step)
    {
        if (_attached && _watchedSteps.Add(step))
        {
            step.PropertyChanged += OnStepPropertyChanged;
            step.Activities.CollectionChanged += OnActivitiesChanged;
        }
    }

    private void Unwatch(AgentPlanStep step)
    {
        if (_watchedSteps.Remove(step))
        {
            step.PropertyChanged -= OnStepPropertyChanged;
            step.Activities.CollectionChanged -= OnActivitiesChanged;
        }
    }

    private AgentPlanRow CreateRow(AgentPlanStep step)
    {
        var row = new AgentPlanRow(this, step);
        BindGlyph(row.Glyph);
        _rows[step] = row;
        Watch(step);
        return row;
    }

    private static void BindGlyph(AgentPlanGlyph glyph)
    {
        ArcTokens.Bind(glyph, AgentPlanGlyph.RingInkProperty, DesignTokens.ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(glyph, AgentPlanGlyph.TrackInkProperty, DesignTokens.ArcDesignTokenKind.Border);
        ArcTokens.Bind(glyph, AgentPlanGlyph.ArcInkProperty, DesignTokens.ArcDesignTokenKind.Accent);
        ArcTokens.Bind(glyph, AgentPlanGlyph.MutedInkProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(glyph, AgentPlanGlyph.SecondaryInkProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(glyph, AgentPlanGlyph.SuccessInkProperty, DesignTokens.ArcDesignTokenKind.Success);
        ArcTokens.Bind(glyph, AgentPlanGlyph.DangerInkProperty, DesignTokens.ArcDesignTokenKind.Danger);
        ArcTokens.Bind(glyph, AgentPlanGlyph.CutoutInkProperty, DesignTokens.ArcDesignTokenKind.Surface);
    }

    private void RebuildRows()
    {
        if (_list is null)
        {
            return;
        }
        foreach (var step in _rows.Keys.ToList())
        {
            Unwatch(step);
        }
        _rows.Clear();
        _list.Clear();
        for (var i = 0; i < Steps.Count; i++)
        {
            var row = CreateRow(Steps[i]);
            _list.Insert(row, i);
            row.Enter(animate: false);
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_list is null)
        {
            return;
        }
        var animate = IsLoaded && !_inBatch;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (var i = 0; i < e.NewItems!.Count; i++)
                {
                    InsertRow((AgentPlanStep)e.NewItems[i]!, e.NewStartingIndex + i, animate);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (AgentPlanStep step in e.OldItems!)
                {
                    RemoveRow(step, animate);
                }
                break;
            case NotifyCollectionChangedAction.Move:
                var moved = (AgentPlanStep)e.NewItems![0]!;
                if (_rows.TryGetValue(moved, out var movedRow) && _list.LiveIndexOf(movedRow) != e.NewStartingIndex)
                {
                    _list.Move(movedRow, e.NewStartingIndex);
                }
                break;
            default:
                RebuildRows();
                break;
        }
        RefreshAll(animate);
        _ = FailedIndexChanged();
    }

    private void InsertRow(AgentPlanStep step, int index, bool animate)
    {
        var row = CreateRow(step);
        _list!.Insert(row, index);
        var streaming = ReferenceEquals(step, _streamStep);
        row.Enter(animate);
        row.Refresh(animate: false);
        if (streaming)
        {
            _streamStep = null;
            if (row.IsLoaded || !IsLoaded)
            {
                row.StreamTitle();
            }
            else
            {
                void Start(object? s, Avalonia.Interactivity.RoutedEventArgs a)
                {
                    row.Loaded -= Start;
                    row.StreamTitle();
                }
                row.Loaded += Start;
            }
        }
        else if (step.NoteKind == AgentPlanNoteKind.AddedByAgent && CurrentStatus != AgentPlanStatus.Draft)
        {
            row.Wash();
            Announce(Labels.AnnounceInserted(step.Title));
        }
    }

    private void RemoveRow(AgentPlanStep step, bool animate)
    {
        if (!_rows.Remove(step, out var row))
        {
            return;
        }
        Unwatch(step);
        row.Leave(animate, () => _list?.Drop(row));
    }

    internal int PositionOf(AgentPlanRow row)
    {
        var index = _list?.LiveIndexOf(row) ?? -1;
        return index < 0 ? Math.Max(1, Steps.IndexOf(row.Step) + 1) : index + 1;
    }

    private void OnActivitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var (step, row) in _rows)
        {
            if (ReferenceEquals(step.Activities, sender))
            {
                row.Refresh(IsLoaded);
            }
        }
    }

    private void OnStepPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AgentPlanStep step || !_rows.TryGetValue(step, out var row))
        {
            return;
        }
        if (e.Property == AgentPlanStep.StateProperty)
        {
            AnnounceStep(step, e.GetOldValue<AgentPlanStepState>(), e.GetNewValue<AgentPlanStepState>());
            _hadFocus |= IsKeyboardFocusWithin;
        }
        row.Refresh(IsLoaded);
        RefreshAll(IsLoaded);
        if (e.Property == AgentPlanStep.StateProperty && FailedIndexChanged())
        {
            MoveFocusIfNeeded();
        }
    }

    private void AnnounceStep(AgentPlanStep step, AgentPlanStepState old, AgentPlanStepState state)
    {
        if (CurrentStatus == AgentPlanStatus.Draft)
        {
            return;
        }
        var labels = Labels;
        switch (state)
        {
            case AgentPlanStepState.Done:
                Announce(labels.AnnounceDone(step.Title));
                break;
            case AgentPlanStepState.Failed:
                Announce(labels.AnnounceFailed(Math.Max(1, Steps.IndexOf(step) + 1), step.Error ?? string.Empty));
                break;
            case AgentPlanStepState.Skipped:
                Announce(labels.AnnounceSkipped(step.Title));
                break;
            case AgentPlanStepState.Active when old == AgentPlanStepState.Failed:
                Announce(labels.AnnounceRetrying(step.Title));
                break;
        }
    }

    // Draft edits ---------------------------------------------------------------------------------------------------------

    internal void RenameStep(AgentPlanRow row, string title, bool announce)
    {
        row.Step.Title = title;
        row.UpdateNames();
        RaiseChanged(AgentPlanChangeKind.Rename, row.Step);
        if (announce)
        {
            Announce(Labels.AnnounceRenamed(title));
        }
    }

    internal void MoveStep(AgentPlanRow row, int delta, bool fromKeyboard)
    {
        var from = Steps.IndexOf(row.Step);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Steps.Count)
        {
            return;
        }
        Steps.Move(from, to);
        UpdateRowNames();
        RaiseChanged(AgentPlanChangeKind.Move, row.Step);
        Announce(Labels.AnnounceMoved(to + 1, Steps.Count));
    }

    private void OnDragSwapped(AgentPlanRow row, int newIndex)
    {
        var from = Steps.IndexOf(row.Step);
        if (from >= 0 && from != newIndex)
        {
            Steps.Move(from, newIndex);
        }
        UpdateRowNames();
    }

    private void OnDragEnded(AgentPlanRow row, int startIndex)
    {
        if (Steps.IndexOf(row.Step) != startIndex)
        {
            RaiseChanged(AgentPlanChangeKind.Move, row.Step);
        }
    }

    private void UpdateRowNames()
    {
        foreach (var row in _rows.Values)
        {
            row.UpdateNames();
        }
    }

    internal void RemoveStep(AgentPlanRow row, AgentPlanRemoveSource source)
    {
        var index = Steps.IndexOf(row.Step);
        if (index < 0 || CurrentStatus != AgentPlanStatus.Draft)
        {
            return;
        }
        var step = row.Step;
        // A title emptied in the field counts as empty even before it was saved.
        var title = source == AgentPlanRemoveSource.Button ? step.Title : row.TitleBox.Text ?? string.Empty;
        var wasEmpty = string.IsNullOrWhiteSpace(title);
        // Where the focus goes (decided before the row leaves).
        Control? target = null;
        var neighborAfter = index + 1 < Steps.Count ? Steps[index + 1] : null;
        var neighborBefore = index > 0 ? Steps[index - 1] : null;
        var caretAtStart = false;
        switch (source)
        {
            case AgentPlanRemoveSource.Button:
                target = neighborAfter is not null && _rows.TryGetValue(neighborAfter, out var next) ? next.Remove : _addButton;
                break;
            case AgentPlanRemoveSource.Backspace:
                if (neighborBefore is not null && _rows.TryGetValue(neighborBefore, out var previous))
                {
                    target = previous.TitleBox;
                }
                else if (neighborAfter is not null && _rows.TryGetValue(neighborAfter, out var first))
                {
                    target = first.TitleBox;
                    caretAtStart = true;
                }
                else
                {
                    target = _addButton;
                }
                break;
        }
        Steps.Remove(step);
        UpdateRowNames();
        RaiseChanged(AgentPlanChangeKind.Remove, step);
        Announce(wasEmpty ? Labels.AnnounceRemovedEmpty : Labels.AnnounceRemoved(title));
        if (target is not null)
        {
            target.Focus(source == AgentPlanRemoveSource.Button ? NavigationMethod.Tab : NavigationMethod.Unspecified);
            if (target is TextBox box)
            {
                box.CaretIndex = caretAtStart ? 0 : (box.Text?.Length ?? 0);
                box.ClearSelection();
            }
        }
    }

    /// <summary>Add step: an empty step at the end, its title field focused.</summary>
    public void AddStep()
    {
        if (CurrentStatus != AgentPlanStatus.Draft)
        {
            return;
        }
        var step = new AgentPlanStep();
        Steps.Add(step);
        UpdateRowNames();
        RaiseChanged(AgentPlanChangeKind.Add, step);
        Announce(Labels.AnnounceAdded);
        if (_rows.TryGetValue(step, out var row))
        {
            row.TitleBox.Focus(NavigationMethod.Tab);
        }
    }

    internal void OnSuggestionStreamed(AgentPlanRow row)
    {
        UpdateRowNames();
        RaiseChanged(AgentPlanChangeKind.SuggestionStreamed, row.Step);
        Announce(Labels.AnnounceSuggested(row.Step.Title));
    }

    private void RaiseChanged(AgentPlanChangeKind kind, AgentPlanStep? step) =>
        RaiseEvent(new AgentPlanChangedEventArgs(ChangedEvent, Steps.ToList(), kind, step));
}
