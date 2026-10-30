using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class EnvVariables
{
    private static readonly EnvEnvironment[] DefaultEnvironmentList =
    [
        new("development", "Development"),
        new("preview", "Preview"),
        new("production", "Production"),
    ];

    private readonly HashSet<EnvVariable> _observedRows = new();
    private readonly HashSet<string> _pendingEnvSet = new(StringComparer.Ordinal);
    private readonly HashSet<EnvVariable> _changedDuringRun = new();
    private readonly HashSet<string> _envsDuringRun = new(StringComparer.Ordinal);
    private INotifyCollectionChanged? _observedSource;
    private List<EnvVariable> _sorted = new();
    private bool _refreshPosted;
    private bool _mutating;
    private int _runCount;
    private CancellationTokenSource? _runCts;
    private DispatcherTimer? _doneTimer;
    private string? _failure;

    // Template parts (built in OnApplyTemplate; null before).
    private Control? _shell;
    private EnvSplitPanel? _header;
    private TextBlock? _titleText;
    private TextBlock? _projectText;
    private Control? _metaDot;
    private TextBlock? _summaryText;
    private Grid? _actions;
    private Button? _importButton;
    private Button? _addButton;
    private EnvDisclosure? _bannerHost;
    private EnvDisclosure? _addHost;
    private EnvSplitPanel? _toolbar;
    private SearchField? _search;
    private SegmentedControl? _viewSwitch;
    private Decorator? _viewSlot;
    private DeveloperAnnouncer? _announcer;
    private EnvViewHost? _views;
    private EnvVariablesPane? _variablesPane;
    private EnvComparePane? _comparePane;
    private EnvRedeployBanner? _banner;
    private EnvAddPanel? _addPanel;
    private Button? _addOpener;
    private bool _compact;
    private bool _narrow;
    private bool _syncingView;
    private EnvVariable? _editing;

    private void InitializeLogic()
    {
        AddHandler(KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Tunnel);
    }

    #region Data access

    /// <summary>The environments in use (the defaults while <see cref="Environments"/> is empty).</summary>
    internal IReadOnlyList<EnvEnvironment> EffectiveEnvironments =>
        Environments.Count > 0 ? Environments : DefaultEnvironmentList;

    internal IReadOnlyList<string> EnvironmentKeys => EffectiveEnvironments.Select(e => e.Key).ToList();

    internal string EnvironmentName(string key)
    {
        foreach (var env in EffectiveEnvironments)
        {
            if (env.Key == key)
            {
                return string.IsNullOrEmpty(env.Name) ? env.Key : env.Name;
            }
        }
        return key;
    }

    internal string JoinEnvironmentNames(IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys, StringComparer.Ordinal);
        var ordered = EffectiveEnvironments.Where(e => set.Contains(e.Key)).Select(e => EnvironmentName(e.Key)).ToList();
        foreach (var key in set)
        {
            if (EffectiveEnvironments.All(e => e.Key != key))
            {
                ordered.Add(key);
            }
        }
        return DeveloperText.JoinNames(ordered, Labels.AndWord);
    }

    /// <summary>Every row, in display order (by key, then by first environment).</summary>
    internal IReadOnlyList<EnvVariable> SortedVariables => _sorted;

    private IEnumerable<EnvVariable> SourceItems => VariablesSource is { } source ? source.OfType<EnvVariable>() : Variables;

    private IList<EnvVariable>? MutableSource => VariablesSource is null ? Variables : VariablesSource as IList<EnvVariable>;

    /// <summary>The rows the search lets through, in display order.</summary>
    internal List<EnvVariable> VisibleVariables => _sorted.Where(v => EnvVariableRules.Matches(v.Key, SearchText)).ToList();

    internal (string Left, string Right) CompareSides
    {
        get
        {
            var keys = EnvironmentKeys;
            if (keys.Count == 0)
            {
                return (string.Empty, string.Empty);
            }
            var left = CompareLeft is { } l && keys.Contains(l) ? l : keys.Count > 1 ? keys[^2] : keys[0];
            var right = CompareRight is { } r && keys.Contains(r) ? r : keys[^1];
            if (left == right && keys.Count > 1)
            {
                right = keys.First(k => k != left);
            }
            return (left, right);
        }
    }

    internal IReadOnlyList<string> DefaultAddSelection()
    {
        var keys = EnvironmentKeys;
        if (DefaultAddEnvironments is { } chosen)
        {
            return keys.Where(chosen.Contains).ToList();
        }
        return keys.Count <= 1 ? keys.ToList() : keys.Take(keys.Count - 1).ToList();
    }

    #endregion

    #region Change tracking

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariablesProperty || change.Property == VariablesSourceProperty)
        {
            ObserveSource();
            RefreshData(animate: false);
        }
        else if (change.Property == SearchTextProperty)
        {
            if (_search is not null && (_search.Text ?? string.Empty) != SearchText)
            {
                _search.Text = SearchText;
            }
            RefreshVisible(animate: true);
        }
        else if (change.Property == ViewModeProperty)
        {
            SyncViewMode(animate: true);
        }
        else if (change.Property == CompareLeftProperty || change.Property == CompareRightProperty
                 || change.Property == ShowMatchingProperty)
        {
            _comparePane?.Update(animate: true);
        }
        else if (change.Property == TitleProperty || change.Property == ProjectNameProperty || change.Property == LabelsProperty)
        {
            UpdateHeader();
            if (change.Property == LabelsProperty)
            {
                RebuildViews();
            }
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
            if (IsReadOnly)
            {
                CloseAddPanel(restoreFocus: false);
                CloseEditor(restoreFocus: false);
            }
            UpdateHeader();
            _variablesPane?.RefreshRows();
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
    }

    private void OnEnvironmentsChanged()
    {
        RebuildViews();
        RefreshData(animate: false);
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = (VariablesSource ?? Variables) as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_mutating)
        {
            return;
        }
        PostRefresh();
    }

    private void OnRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_mutating)
        {
            return;
        }
        if (e.Property == EnvVariable.KeyProperty || e.Property == EnvVariable.IsSensitiveProperty
            || e.Property == EnvVariable.IsPendingDeployProperty || e.Property == EnvVariable.UpdatedAtProperty
            || e.Property == EnvVariable.UpdatedByProperty || e.Property == EnvVariable.FingerprintProperty
            || e.Property == EnvVariable.ValueProperty)
        {
            PostRefresh();
        }
    }

    private void OnRowEnvironmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_mutating)
        {
            PostRefresh();
        }
    }

    private void PostRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPosted = false;
            RefreshData(animate: true);
        });
    }

    /// <summary>Re-reads the rows (sort, observers, pending marks) and updates every part.</summary>
    private void RefreshData(bool animate)
    {
        var current = SourceItems.ToList();
        foreach (var row in _observedRows.ToList())
        {
            if (!current.Contains(row))
            {
                row.PropertyChanged -= OnRowPropertyChanged;
                row.Environments.CollectionChanged -= OnRowEnvironmentsChanged;
                _observedRows.Remove(row);
            }
        }
        foreach (var row in current)
        {
            if (_observedRows.Add(row))
            {
                row.PropertyChanged += OnRowPropertyChanged;
                row.Environments.CollectionChanged += OnRowEnvironmentsChanged;
            }
        }
        _sorted = EnvVariableRules.Sort(current, EnvironmentKeys);
        if (_editing is not null && !current.Contains(_editing))
        {
            _editing = null;
        }
        UpdateHeader();
        RefreshVisible(animate);
    }

    private void RefreshVisible(bool animate)
    {
        _variablesPane?.Update(animate);
        _comparePane?.Update(animate);
    }

    private void Mutate(Action action)
    {
        var was = _mutating;
        _mutating = true;
        try
        {
            action();
        }
        finally
        {
            _mutating = was;
        }
    }

    /// <summary>Counts one change: marks the rows, adds the environments, opens or updates the banner and raises VariablesChanged.</summary>
    private void RecordChange(EnvVariablesChangeReason reason, IReadOnlyList<EnvVariable> affected, IEnumerable<string> environments)
    {
        var envs = environments.ToList();
        Mutate(() =>
        {
            foreach (var row in affected)
            {
                if (SourceItems.Contains(row))
                {
                    row.IsPendingDeploy = true;
                }
            }
        });
        foreach (var env in envs)
        {
            _pendingEnvSet.Add(env);
        }
        if (RedeployState == EnvRedeployState.Running)
        {
            foreach (var row in affected)
            {
                _changedDuringRun.Add(row);
            }
            foreach (var env in envs)
            {
                _envsDuringRun.Add(env);
            }
        }
        PendingChangeCount++;
        PendingEnvironments = OrderedPending(_pendingEnvSet);
        if (RedeployState is EnvRedeployState.None or EnvRedeployState.Done or EnvRedeployState.Failed)
        {
            _doneTimer?.Stop();
            _failure = null;
            RedeployState = EnvRedeployState.Pending;
        }
        UpdateBanner();
        _sorted = EnvVariableRules.Sort(SourceItems, EnvironmentKeys);
        UpdateHeader();
        RaiseEvent(new EnvVariablesChangedEventArgs(VariablesChangedEvent, reason, affected, _sorted.ToList()));
    }

    private IReadOnlyList<string> OrderedPending(IEnumerable<string> set)
    {
        var keys = new HashSet<string>(set, StringComparer.Ordinal);
        var ordered = EnvironmentKeys.Where(keys.Contains).ToList();
        ordered.AddRange(keys.Where(k => !ordered.Contains(k)));
        return ordered;
    }

    internal void Announce(string text) => _announcer?.Announce(text);

    /// <summary>Everything announced so far (tests and the event log demo).</summary>
    internal IReadOnlyList<string> Announcements => _announcer?.History ?? Array.Empty<string>();

    #endregion

    #region Actions

    /// <summary>A switch asked to turn <paramref name="environment"/> on or off for <paramref name="variable"/>.</summary>
    internal bool RequestToggle(EnvVariable variable, string environment)
    {
        if (IsReadOnly)
        {
            return false;
        }
        var on = !variable.Environments.Contains(environment);
        if (on && EnvVariableRules.FindBlocking(SourceItems, variable, environment) is { } blocking)
        {
            var args = new EnvScopeEventArgs(ScopeConflictEvent, variable, environment, true, blocking);
            RaiseEvent(args);
            if (!args.Handled)
            {
                _variablesPane?.RowFor(variable)?.ShowConflict(Labels.ScopeConflict(variable.Key, EnvironmentName(environment)));
            }
            return false;
        }
        Mutate(() =>
        {
            if (on)
            {
                variable.Environments.Add(environment);
            }
            else
            {
                variable.Environments.Remove(environment);
            }
        });
        RaiseEvent(new EnvScopeEventArgs(ScopeToggledEvent, variable, environment, on));
        Announce(Labels.ScopeAnnouncement(EnvironmentName(environment), variable.Key, on));
        RecordChange(EnvVariablesChangeReason.ScopeToggled, [variable], [environment]);
        _variablesPane?.RowFor(variable)?.Refresh();
        _variablesPane?.UpdateHeaderCounts();
        _comparePane?.Update(animate: true);
        return true;
    }

    /// <summary>Saves an edited value; returns false when nothing was saved.</summary>
    internal bool SaveValue(EnvVariable variable, string value, bool sensitive)
    {
        if (IsReadOnly || string.IsNullOrEmpty(value))
        {
            return false;
        }
        var valueChanged = !string.Equals(variable.Value, value, StringComparison.Ordinal) || variable.IsSensitive;
        var becameSensitive = sensitive && !variable.IsSensitive;
        Mutate(() =>
        {
            variable.Value = value;
            if (sensitive)
            {
                variable.IsSensitive = true;
            }
            variable.UpdatedAt = DateTimeOffset.Now;
        });
        var index = SourceItems.ToList().IndexOf(variable);
        RaiseEvent(new EnvVariableEventArgs(VariableSavedEvent, variable, index, valueChanged, becameSensitive));
        Announce(Labels.SavedAnnouncement(variable.Key));
        RecordChange(EnvVariablesChangeReason.Edited, [variable], variable.Environments.ToList());
        var row = _variablesPane?.RowFor(variable);
        CloseEditor(restoreFocus: true);
        row?.Refresh();
        _variablesPane?.Glow(variable);
        _comparePane?.Update(animate: true);
        return true;
    }

    /// <summary>Removes a row (second step of Remove in the editor).</summary>
    internal void RemoveVariable(EnvVariable variable)
    {
        if (IsReadOnly || MutableSource is not { } source)
        {
            return;
        }
        var index = source.IndexOf(variable);
        if (index < 0)
        {
            return;
        }
        // Focus moves before the row leaves: the next row's edit button, else the previous one's, else Add variable.
        var next = _variablesPane?.NeighbourOf(variable);
        var envs = variable.Environments.ToList();
        if (ReferenceEquals(_editing, variable))
        {
            _editing = null;
        }
        if (next is not null && _variablesPane?.RowFor(next) is { } nextRow)
        {
            nextRow.FocusEdit();
        }
        else
        {
            FocusAddButton();
        }
        Mutate(() => source.RemoveAt(index));
        _sorted = EnvVariableRules.Sort(SourceItems, EnvironmentKeys);
        RaiseEvent(new EnvVariableEventArgs(VariableRemovedEvent, variable, index));
        Announce(Labels.RemovedAnnouncement(variable.Key, JoinEnvironmentNames(envs)));
        RecordChange(EnvVariablesChangeReason.Removed, [variable], envs);
        RefreshVisible(animate: true);
    }

    /// <summary>The problems of a new variable, in the order Arc checks them (null fields are fine).</summary>
    internal (string? Key, string? Value, string? Environments) ValidateNew(string key, string value, IReadOnlyCollection<string> environments)
    {
        string? keyError = EnvVariableDotenvParser.ValidateKey(key) switch
        {
            EnvKeyProblem.Empty => Labels.KeyEmptyError,
            EnvKeyProblem.StartsWithDigit => Labels.KeyDigitError,
            EnvKeyProblem.InvalidCharacters => Labels.KeyCharactersError,
            _ => null,
        };
        if (keyError is null && environments.Count > 0)
        {
            var taken = EnvVariableRules.TakenEnvironments(SourceItems, key, environments);
            if (taken.Count > 0)
            {
                keyError = Labels.KeyTakenError(JoinEnvironmentNames(taken));
            }
        }
        var valueError = string.IsNullOrEmpty(value) ? Labels.ValueEmptyError : null;
        var envError = environments.Count == 0 ? Labels.EnvironmentsError : null;
        return (keyError, valueError, envError);
    }

    /// <summary>Adds a validated variable from the add panel.</summary>
    internal EnvVariable? AddVariable(string key, string value, IReadOnlyCollection<string> environments, bool sensitive)
    {
        if (IsReadOnly || MutableSource is not { } source)
        {
            return null;
        }
        var ordered = EnvironmentKeys.Where(environments.Contains).ToArray();
        var variable = new EnvVariable(key, value, ordered) { IsSensitive = sensitive, UpdatedAt = DateTimeOffset.Now };
        Mutate(() => source.Add(variable));
        _sorted = EnvVariableRules.Sort(SourceItems, EnvironmentKeys);
        RaiseEvent(new EnvVariableEventArgs(VariableAddedEvent, variable, source.IndexOf(variable)));
        Announce(Labels.AddedAnnouncement(key, JoinEnvironmentNames(ordered)));
        RecordChange(EnvVariablesChangeReason.Added, [variable], ordered);
        CloseAddPanel(restoreFocus: true);
        RefreshVisible(animate: true);
        _variablesPane?.Glow(variable);
        return variable;
    }

    /// <summary>Imports the lines of a parsed .env text into <paramref name="environments"/>.</summary>
    internal IReadOnlyList<EnvVariable> Import(EnvDotenvResult result, IReadOnlyCollection<string> environments)
    {
        if (IsReadOnly || MutableSource is not { } source || result.ImportCount == 0 || environments.Count == 0)
        {
            return Array.Empty<EnvVariable>();
        }
        var ordered = EnvironmentKeys.Where(environments.Contains).ToArray();
        var added = new List<EnvVariable>();
        var replaced = new List<EnvVariable>();
        Mutate(() =>
        {
            foreach (var line in result.Lines.Where(l => l.IsImported))
            {
                foreach (var row in source.Where(v => string.Equals(v.Key, line.Key, StringComparison.Ordinal)).ToList())
                {
                    var touched = false;
                    foreach (var env in ordered)
                    {
                        touched |= row.Environments.Remove(env);
                    }
                    if (!touched)
                    {
                        continue;
                    }
                    replaced.Add(row);
                    if (row.Environments.Count == 0)
                    {
                        source.Remove(row);
                    }
                }
                var variable = new EnvVariable(line.Key, line.Value ?? string.Empty, ordered) { UpdatedAt = DateTimeOffset.Now };
                source.Add(variable);
                added.Add(variable);
            }
        });
        _sorted = EnvVariableRules.Sort(SourceItems, EnvironmentKeys);
        var skipped = result.Lines.Where(l => l.IsSkipped).Select(l => l.LineNumber).ToList();
        RaiseEvent(new EnvVariablesImportedEventArgs(VariablesImportedEvent, added, replaced, ordered, skipped));
        Announce(Labels.ImportedAnnouncement(added.Count, JoinEnvironmentNames(ordered)));
        RecordChange(EnvVariablesChangeReason.Imported, added.Concat(replaced).ToList(), ordered);
        CloseAddPanel(restoreFocus: true);
        RefreshVisible(animate: true);
        foreach (var variable in added)
        {
            _variablesPane?.Glow(variable);
        }
        return added;
    }

    /// <summary>Copies a key's value to the environment that lacks it (compare view).</summary>
    internal EnvVariable? CopyAcross(string key, string from, string to)
    {
        if (IsReadOnly)
        {
            return null;
        }
        var source = EnvVariableRules.RowFor(SourceItems, key, from);
        if (source is null || EnvVariableRules.RowFor(SourceItems, key, to) is not null)
        {
            return null;
        }
        Mutate(() => source.Environments.Add(to));
        _sorted = EnvVariableRules.Sort(SourceItems, EnvironmentKeys);
        RaiseEvent(new EnvValueCopiedEventArgs(ValueCopiedEvent, source, from, to));
        Announce(Labels.CopiedAnnouncement(key, EnvironmentName(to)));
        RecordChange(EnvVariablesChangeReason.CopiedAcross, [source], [to]);
        _variablesPane?.RowFor(source)?.Refresh();
        _variablesPane?.UpdateHeaderCounts();
        return source;
    }

    internal void OnValueRevealed(EnvVariable variable, bool revealed) =>
        RaiseEvent(new EnvValueRevealedEventArgs(ValueRevealedEvent, variable, revealed));

    #endregion

    #region Editor and add panel

    /// <summary>Opens the inline editor of <paramref name="variable"/> (closing the add panel and any other editor).</summary>
    public void BeginEdit(EnvVariable variable)
    {
        if (IsReadOnly)
        {
            return;
        }
        if (ReferenceEquals(_editing, variable))
        {
            return;
        }
        CloseAddPanel(restoreFocus: false);
        CloseEditor(restoreFocus: false);
        if (ViewMode != EnvVariablesView.Variables)
        {
            SetCurrentValue(ViewModeProperty, EnvVariablesView.Variables);
        }
        var row = _variablesPane?.RowFor(variable);
        if (row is null)
        {
            return;
        }
        _editing = variable;
        row.OpenEditor();
    }

    /// <summary>Closes the open inline editor without saving.</summary>
    public void CancelEdit() => CloseEditor(restoreFocus: true);

    internal void CloseEditor(bool restoreFocus)
    {
        if (_editing is not { } editing)
        {
            return;
        }
        _editing = null;
        _variablesPane?.RowFor(editing)?.CloseEditor(restoreFocus);
    }

    internal EnvVariable? EditingVariable => _editing;

    /// <summary>Opens the add panel on <paramref name="mode"/> (closing any editor); the .env form starts with <see cref="SampleDotenv"/>.</summary>
    public void OpenAddPanel(EnvAddMode mode = EnvAddMode.Single)
    {
        if (IsReadOnly || _addHost is null)
        {
            return;
        }
        CloseEditor(restoreFocus: false);
        _addOpener = mode == EnvAddMode.Dotenv ? _importButton : _addButton;
        _addPanel ??= new EnvAddPanel(this);
        var wasOpen = _addHost.IsOpen;
        if (!wasOpen || _addPanel.Mode != mode)
        {
            _addPanel.Reset(mode, DefaultAddSelection(), mode == EnvAddMode.Dotenv ? SampleDotenv : string.Empty);
        }
        _addPanel.SetNarrow(_narrow);
        _addHost.Open(_addPanel);
        UpdateAddButtons();
        Dispatcher.UIThread.Post(() => _addPanel?.FocusFirst(), DispatcherPriority.Loaded);
    }

    /// <summary>Closes the add panel without adding.</summary>
    public void CloseAddPanel() => CloseAddPanel(restoreFocus: true);

    internal void CloseAddPanel(bool restoreFocus)
    {
        if (_addHost is null || !_addHost.IsOpen)
        {
            return;
        }
        var opener = _addOpener;
        _addHost.Close();
        UpdateAddButtons();
        if (restoreFocus)
        {
            // Cancel clicked with the mouse hands focus back without the keyboard ring.
            (opener ?? _addButton)?.Focus(BlockFocusModality.For(this));
        }
    }

    internal bool IsAddPanelOpen => _addHost?.IsOpen == true;

    internal EnvAddPanel? AddPanel => _addPanel;

    private void UpdateAddButtons()
    {
        var open = _addHost?.IsOpen == true;
        var mode = _addPanel?.Mode;
        if (_addButton is not null)
        {
            _addButton.Classes.Set("expanded", open && mode == EnvAddMode.Single);
        }
        if (_importButton is not null)
        {
            _importButton.Classes.Set("expanded", open && mode == EnvAddMode.Dotenv);
        }
    }

    internal void FocusAddButton()
    {
        if (_addButton is { IsVisible: true } add)
        {
            add.Focus(BlockFocusModality.For(this));
        }
        else
        {
            _search?.Focus(NavigationMethod.Tab);
        }
    }

    internal void FocusSearch() => _search?.Focus(NavigationMethod.Tab);

    /// <summary>Clears the search and puts the focus back in the search box (Arc drops it).</summary>
    internal void ClearSearch()
    {
        SetCurrentValue(SearchTextProperty, string.Empty);
        if (_search is not null)
        {
            _search.Text = string.Empty;
            _search.Focus(NavigationMethod.Tab);
        }
    }

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled)
        {
            return;
        }
        if (_addHost?.IsOpen == true && e.Source is Visual source && _addPanel is not null && _addPanel.IsVisualAncestorOf(source))
        {
            CloseAddPanel(restoreFocus: true);
            e.Handled = true;
        }
    }

    #endregion

    #region Redeploy

    private EnvRedeployRequest BuildRequest() =>
        new(PendingEnvironments.ToList(), PendingChangeCount, _sorted.Where(v => v.IsPendingDeploy).ToList());

    /// <summary>
    /// Deploys the pending changes (as the banner's button does): awaits <see cref="RedeployAction"/> (or a simulated 1.7s).
    /// Returns true when it succeeded, false when nothing was pending, it was cancelled or it failed.
    /// </summary>
    public async Task<bool> RedeployAsync()
    {
        if (RedeployState == EnvRedeployState.Running || PendingChangeCount == 0)
        {
            return false;
        }
        var request = BuildRequest();
        var requested = new EnvRedeployEventArgs(RedeployRequestedEvent, request);
        RaiseEvent(requested);
        if (requested.Cancel)
        {
            return false;
        }
        if (RedeployCommand is { } command)
        {
            if (!command.CanExecute(request))
            {
                return false;
            }
            command.Execute(request);
        }
        _runCts?.Cancel();
        var cts = new CancellationTokenSource();
        _runCts = cts;
        _runCount = PendingChangeCount;
        _runEnvironments = PendingEnvironments.ToList();
        _changedDuringRun.Clear();
        _envsDuringRun.Clear();
        _failure = null;
        RedeployState = EnvRedeployState.Running;
        UpdateBanner();
        try
        {
            var action = RedeployAction;
            if (action is null)
            {
                await Task.Delay(DeveloperSprings.SimulatedRedeploy, cts.Token);
            }
            else
            {
                await action(request, cts.Token);
            }
            cts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (ReferenceEquals(_runCts, cts))
            {
                _runCts = null;
                RedeployState = PendingChangeCount > 0 ? EnvRedeployState.Pending : EnvRedeployState.None;
                UpdateBanner();
            }
            return false;
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_runCts, cts))
            {
                return false;
            }
            _runCts = null;
            _failure = string.IsNullOrWhiteSpace(exception.Message) ? Labels.FailedDetail : exception.Message;
            RedeployState = EnvRedeployState.Failed;
            UpdateBanner();
            Announce(Labels.FailedAnnouncement(_failure));
            RaiseEvent(new EnvRedeployEventArgs(RedeployFailedEvent, request, exception));
            _banner?.FocusAction();
            return false;
        }
        if (!ReferenceEquals(_runCts, cts))
        {
            return false;
        }
        _runCts = null;
        // Changes made while the deployment ran stay pending for the next one.
        Mutate(() =>
        {
            foreach (var row in SourceItems)
            {
                if (!_changedDuringRun.Contains(row))
                {
                    row.IsPendingDeploy = false;
                }
            }
        });
        var deployed = _runEnvironments;
        PendingChangeCount = Math.Max(0, PendingChangeCount - _runCount);
        _pendingEnvSet.Clear();
        if (PendingChangeCount > 0)
        {
            foreach (var env in _envsDuringRun)
            {
                _pendingEnvSet.Add(env);
            }
        }
        PendingEnvironments = OrderedPending(_pendingEnvSet);
        _doneEnvironments = deployed;
        RedeployState = EnvRedeployState.Done;
        UpdateBanner();
        _variablesPane?.RefreshRows();
        var names = JoinEnvironmentNames(deployed);
        Announce(Labels.DoneTitle(names));
        RaiseEvent(new EnvRedeployEventArgs(RedeployedEvent, request));
        StartDoneTimer();
        return true;
    }

    private IReadOnlyList<string> _doneEnvironments = Array.Empty<string>();
    private IReadOnlyList<string> _runEnvironments = Array.Empty<string>();

    /// <summary>Cancels a running deployment (the changes stay pending).</summary>
    public void CancelRedeploy() => _runCts?.Cancel();

    private void StartDoneTimer()
    {
        _doneTimer?.Stop();
        _doneTimer = new DispatcherTimer { Interval = DeveloperSprings.RedeployDoneDismiss };
        _doneTimer.Tick += (_, _) =>
        {
            _doneTimer?.Stop();
            EndDone();
        };
        _doneTimer.Start();
    }

    /// <summary>Ends the "deployed" confirmation: the banner closes, or shows the changes made meanwhile.</summary>
    internal void EndDone()
    {
        if (RedeployState != EnvRedeployState.Done)
        {
            return;
        }
        var bannerHadFocus = _banner is not null && _banner.IsKeyboardFocusWithin;
        RedeployState = PendingChangeCount > 0 ? EnvRedeployState.Pending : EnvRedeployState.None;
        UpdateBanner();
        if (bannerHadFocus && RedeployState == EnvRedeployState.None)
        {
            FocusSearch();
        }
    }

    private void UpdateBanner()
    {
        if (_bannerHost is null)
        {
            return;
        }
        if (RedeployState == EnvRedeployState.None)
        {
            if (_bannerHost.IsOpen)
            {
                if (_banner?.IsKeyboardFocusWithin == true)
                {
                    FocusSearch();
                }
                _bannerHost.Close();
            }
            return;
        }
        _banner ??= new EnvRedeployBanner(this);
        var envs = RedeployState == EnvRedeployState.Done ? _doneEnvironments : PendingEnvironments;
        _banner.Update(RedeployState, PendingChangeCount, JoinEnvironmentNames(envs), envs.Count, _failure);
        _banner.SetNarrow(_narrow);
        if (!_bannerHost.IsOpen)
        {
            _bannerHost.Open(_banner);
        }
    }

    internal EnvRedeployBanner? Banner => _banner;

    #endregion

    #region Template and layout

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_importButton is not null)
        {
            _importButton.Click -= OnImportClick;
        }
        if (_addButton is not null)
        {
            _addButton.Click -= OnAddClick;
        }
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        }
        _shell = e.NameScope.Find<Control>(ShellPart);
        _header = e.NameScope.Find<EnvSplitPanel>("PART_Header");
        _titleText = e.NameScope.Find<TextBlock>("PART_Title");
        _projectText = e.NameScope.Find<TextBlock>("PART_Project");
        _metaDot = e.NameScope.Find<Control>("PART_MetaDot");
        _summaryText = e.NameScope.Find<TextBlock>("PART_Summary");
        _actions = e.NameScope.Find<Grid>("PART_Actions");
        _importButton = e.NameScope.Find<Button>("PART_Import");
        _addButton = e.NameScope.Find<Button>("PART_Add");
        _bannerHost = e.NameScope.Find<EnvDisclosure>("PART_BannerHost");
        _addHost = e.NameScope.Find<EnvDisclosure>("PART_AddHost");
        _toolbar = e.NameScope.Find<EnvSplitPanel>("PART_Toolbar");
        _search = e.NameScope.Find<SearchField>("PART_Search");
        _viewSwitch = e.NameScope.Find<SegmentedControl>("PART_ViewSwitch");
        _viewSlot = e.NameScope.Find<Decorator>("PART_ViewSlot");
        _announcer = e.NameScope.Find<DeveloperAnnouncer>("PART_Announcer");
        if (_importButton is not null)
        {
            _importButton.Click += OnImportClick;
        }
        if (_addButton is not null)
        {
            _addButton.Click += OnAddClick;
        }
        if (_search is not null)
        {
            _search.Text = SearchText;
            _search.PropertyChanged += OnSearchPropertyChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        _banner = null;
        _addPanel = null;
        RebuildViews();
        PseudoClasses.Set(FramedPseudoClass, IsFramed);
        PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
        UpdateHeader();
        UpdateBanner();
    }

    private void RebuildViews()
    {
        if (_viewSlot is null)
        {
            return;
        }
        _editing = null;
        _variablesPane = new EnvVariablesPane(this);
        _comparePane = new EnvComparePane(this);
        _views = new EnvViewHost(_variablesPane, _comparePane);
        _viewSlot.Child = _views;
        _variablesPane.SetLayout(_compact, _narrow);
        _comparePane.SetLayout(_compact, _narrow);
        if (_viewSwitch is not null)
        {
            _syncingView = true;
            _viewSwitch.ItemsSource = new[]
            {
                new SegmentedOption(EnvVariablesView.Variables, Labels.VariablesView),
                new SegmentedOption(EnvVariablesView.Compare, Labels.CompareView),
            };
            _viewSwitch.SelectedIndex = ViewMode == EnvVariablesView.Compare ? 1 : 0;
            Avalonia.Automation.AutomationProperties.SetName(_viewSwitch, Labels.ViewSwitchName);
            _syncingView = false;
        }
        if (_search is not null)
        {
            _search.Placeholder = Labels.SearchPlaceholder;
            Avalonia.Automation.AutomationProperties.SetName(_search, Labels.SearchPlaceholder);
        }
        _views.Show(ViewMode == EnvVariablesView.Compare ? 1 : 0, animate: false);
        RefreshVisible(animate: false);
    }

    private void OnImportClick(object? sender, RoutedEventArgs e) => ToggleAddPanel(EnvAddMode.Dotenv);

    private void OnAddClick(object? sender, RoutedEventArgs e) => ToggleAddPanel(EnvAddMode.Single);

    private void ToggleAddPanel(EnvAddMode mode)
    {
        if (_addHost?.IsOpen == true && _addPanel?.Mode == mode)
        {
            CloseAddPanel(restoreFocus: true);
            return;
        }
        OpenAddPanel(mode);
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _search is not null)
        {
            SetCurrentValue(SearchTextProperty, _search.Text ?? string.Empty);
        }
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingView || _viewSwitch is null)
        {
            return;
        }
        SetCurrentValue(ViewModeProperty, _viewSwitch.SelectedIndex == 1 ? EnvVariablesView.Compare : EnvVariablesView.Variables);
    }

    private void SyncViewMode(bool animate)
    {
        if (_viewSwitch is not null)
        {
            _syncingView = true;
            _viewSwitch.SelectedIndex = ViewMode == EnvVariablesView.Compare ? 1 : 0;
            _syncingView = false;
        }
        if (ViewMode == EnvVariablesView.Compare)
        {
            CloseEditor(restoreFocus: false);
        }
        _comparePane?.Update(animate: false);
        _views?.Show(ViewMode == EnvVariablesView.Compare ? 1 : 0, animate);
    }

    private void UpdateHeader()
    {
        if (_titleText is not null)
        {
            _titleText.Text = Title ?? Labels.Title;
        }
        if (_projectText is not null)
        {
            _projectText.Text = ProjectName;
            _projectText.IsVisible = !string.IsNullOrEmpty(ProjectName);
        }
        if (_metaDot is not null)
        {
            _metaDot.IsVisible = !string.IsNullOrEmpty(ProjectName);
        }
        if (_summaryText is not null)
        {
            _summaryText.Text = Labels.KeysSummary(EnvVariableRules.CountKeys(SourceItems), EffectiveEnvironments.Count);
        }
        if (_importButton is not null)
        {
            _importButton.IsVisible = !IsReadOnly;
            SetButtonContent(_importButton, EnvUi.FileUp, Labels.ImportButton);
        }
        if (_addButton is not null)
        {
            _addButton.IsVisible = !IsReadOnly;
            SetButtonContent(_addButton, ArcIcons.Plus, Labels.AddButton);
        }
        if (_actions is not null)
        {
            _actions.IsVisible = !IsReadOnly;
        }
    }

    private static void SetButtonContent(Button button, Avalonia.Media.Geometry icon, string text)
    {
        if (button.Content is StackPanel { Children: [ArcGlyph, TextBlock label] } && label.Text == text)
        {
            return;
        }
        button.Content = EnvUi.IconLabel(icon, text);
        Avalonia.Automation.AutomationProperties.SetName(button, text);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            ApplyWidth(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    private void ApplyWidth(double width)
    {
        // 1px hysteresis so a width on the threshold does not flip back and forth.
        var compact = _compact ? width < CompactWidth + 1 : width < CompactWidth;
        var narrow = _narrow ? width < NarrowWidth + 1 : width < NarrowWidth;
        if (compact == _compact && narrow == _narrow)
        {
            return;
        }
        _compact = compact;
        _narrow = narrow;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_actions is not null)
        {
            _actions.ColumnDefinitions = new ColumnDefinitions(narrow ? "*,*" : "Auto,Auto");
            _actions.HorizontalAlignment = narrow ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left;
        }
        if (_header is not null)
        {
            _header.IsStacked = narrow;
            // Set here: the template's token margin wins over a pseudo-class style.
            _header.Margin = narrow ? new Thickness(16, 20, 16, 16) : new Thickness(24, 20, 24, 16);
        }
        if (_toolbar is not null)
        {
            _toolbar.IsStacked = narrow;
        }
        _variablesPane?.SetLayout(compact, narrow);
        _comparePane?.SetLayout(compact, narrow);
        _addPanel?.SetNarrow(narrow);
        _banner?.SetNarrow(narrow);
    }

    /// <summary>True below <see cref="CompactWidth"/> (stacked rows).</summary>
    internal bool IsCompact => _compact;

    /// <summary>True below <see cref="NarrowWidth"/>.</summary>
    internal bool IsNarrow => _narrow;

    internal EnvVariablesPane? VariablesPane => _variablesPane;

    internal EnvComparePane? ComparePane => _comparePane;

    internal Button? AddButton => _addButton;

    internal Button? ImportButton => _importButton;

    internal SearchField? Search => _search;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runCts?.Cancel();
        _doneTimer?.Stop();
        _variablesPane?.StopTimers();
    }

    #endregion
}
