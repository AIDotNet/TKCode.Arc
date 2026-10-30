using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public partial class AiWorkflow
{
    private Grid? _headerGrid;
    private TextBlock? _title;
    private TextBlock? _description;
    private StackPanel? _actions;
    private Border? _simulated;
    private Button? _runButton;
    private AiWorkflowRail? _rail;
    private StackPanel? _empty;
    private StackPanel? _skeleton;
    private Border? _footer;
    private Button? _addButton;
    private TextBlock? _summary;
    private ExplainerAnnouncer? _announcer;

    private readonly Dictionary<string, AiWorkflowStepRow> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _pathLists = new(StringComparer.Ordinal);
    private readonly AiWorkflowPathSwap _pathSwap = new();
    private readonly AiWorkflowPathTabs _tabs = new();
    private readonly ArcGlyph _runGlyph = new() { Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _runLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _addLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<AiWorkflowMenuRow> _triggerRows = [];
    private AiWorkflowAddMenu? _menu;
    private string? _selectedPathId;
    private bool _touch;
    private bool _narrow;
    private bool _animateNextSync;

    // Test access.
    internal AiWorkflowRail? Rail => _rail;
    internal Button? RunButton => _runButton;
    internal Button? AddButton => _addButton;
    internal TextBlock? SummaryText => _summary;
    internal Border? SimulatedBadge => _simulated;
    internal StackPanel? EmptyPanel => _empty;
    internal StackPanel? SkeletonPanel => _skeleton;
    internal ExplainerAnnouncer? Announcer => _announcer;
    internal AiWorkflowAddMenu? Menu => _menu;
    internal AiWorkflowPathTabs PathTabs => _tabs;
    internal AiWorkflowPathSwap PathSwap => _pathSwap;
    internal IReadOnlyList<AiWorkflowMenuRow> TriggerRows => _triggerRows;
    internal bool IsNarrow => _narrow;

    internal AiWorkflowStepRow? RowOf(string id) => _rows.TryGetValue(id, out var row) ? row : null;

    /// <summary>The path shown under the branch (new steps land at its end).</summary>
    internal string? SelectedPathId => AiWorkflowFormat.Branch(EffectiveSteps) is { } branch
        ? branch.Paths.FirstOrDefault(p => p.Id == _selectedPathId)?.Id ?? branch.Paths.FirstOrDefault()?.Id
        : null;

    private bool CanOpenMenu => IsEditable && !IsLoading && EffectiveSteps.Count > 0 &&
                                AiWorkflowFormat.Filter(EffectiveCatalog, null, AiWorkflowFormat.Branch(EffectiveSteps) is null).Count > 0;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_runButton is not null)
        {
            _runButton.Click -= OnRunClick;
        }
        if (_addButton is not null)
        {
            _addButton.Click -= OnAddClick;
        }
        _rail?.Children.Clear();
        _headerGrid = e.NameScope.Find<Grid>(HeaderGridPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _actions = e.NameScope.Find<StackPanel>(ActionsPart);
        _simulated = e.NameScope.Find<Border>(SimulatedPart);
        _runButton = e.NameScope.Find<Button>(RunButtonPart);
        _rail = e.NameScope.Find<AiWorkflowRail>(RailPart);
        _empty = e.NameScope.Find<StackPanel>(EmptyPart);
        _skeleton = e.NameScope.Find<StackPanel>(SkeletonPart);
        _footer = e.NameScope.Find<Border>(FooterPart);
        _addButton = e.NameScope.Find<Button>(AddButtonPart);
        _summary = e.NameScope.Find<TextBlock>(SummaryPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_runButton is not null)
        {
            _runButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _runGlyph, _runLabel } };
            _runButton.Click += OnRunClick;
        }
        if (_addButton is not null)
        {
            _addButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { new ArcGlyph { Data = ArcIcons.Plus, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center }, _addLabel }
            };
            _addButton.Click += OnAddClick;
        }
        if (_rail is not null)
        {
            _rail.Rows = RailRows;
            _rail.IsReached = row => _runner.StatusOf(row.Step.Id) is AiWorkflowStepStatus.Running or AiWorkflowStepStatus.Done or AiWorkflowStepStatus.Failed;
        }
        _tabs.PathPicked -= OnPathPicked;
        _tabs.PathPicked += OnPathPicked;
        BuildSkeleton();
        Sync(animate: false);
        ApplyNarrow();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepsProperty)
        {
            if (!_committing)
            {
                // Steps from the app: the simulation's name, line and catalog no longer apply.
                _fromSimulation = false;
            }
            var animate = _animateNextSync;
            _animateNextSync = false;
            Sync(animate || IsLoaded);
            UpdateAutoPlay();
        }
        else if (change.Property == LabelsProperty || change.Property == IsEditableProperty || change.Property == RunStepProperty ||
                 change.Property == CatalogProperty || change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            RefreshChrome();
            foreach (var row in _rows.Values)
            {
                ApplyRow(row, animate: false);
            }
            if (change.Property == LabelsProperty && AiWorkflowFormat.Branch(EffectiveSteps) is { } branch)
            {
                _tabs.SetPaths(branch.Paths, Labels.Paths);
            }
        }
        else if (change.Property == IsLoadingProperty)
        {
            if (IsLoading)
            {
                _runner.Reset();
                _menu?.Close(returnFocus: false);
            }
            UpdatePseudoClasses();
            RefreshChrome();
            UpdateAutoPlay();
        }
        else if (change.Property == SimulationProperty)
        {
            if (Steps is null)
            {
                // Another flow (or none): the run in progress stops and the new one starts from the top.
                _autoRun = false;
                _runner.Reset();
            }
            else
            {
                _fromSimulation = false;
            }
            Sync(animate: false);
            UpdatePseudoClasses();
            RefreshChrome();
            UpdateAutoPlay();
        }
        else if (change.Property == AutoPlayProperty)
        {
            UpdateAutoPlay();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewport?.Dispose();
        _viewport = new Primitives.ViewportVisibility(this, threshold: 0.25, once: false);
        _viewport.VisibilityChanged += (_, visible) => SetOnScreen(visible);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        _onScreen = false;
        _menu?.Close(returnFocus: false);
        // The app's runner is cancelled when the card leaves the window.
        if (_runner.IsRunning)
        {
            _autoRun = false;
            _runner.Reset();
        }
        UpdateAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < AiWorkflowMetrics.NarrowWidth;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            ApplyNarrow();
        }
    }

    private void ApplyNarrow()
    {
        PseudoClasses.Set(NarrowPseudoClass, _narrow);
        foreach (var row in _rows.Values)
        {
            row.SetNarrow(_narrow);
        }
        foreach (var row in _triggerRows)
        {
            row.ShowsDetail = !_narrow;
        }
        if (_actions is not null)
        {
            // Under 480px the header wraps: the name and its line on top, the actions under them.
            Grid.SetRow(_actions, _narrow ? 1 : 0);
            Grid.SetColumn(_actions, _narrow ? 0 : 1);
            _actions.HorizontalAlignment = _narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            _actions.Margin = _narrow ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 0, 0);
        }
        _rail?.InvalidateVisual();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(RunningPseudoClass, IsRunning);
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        PseudoClasses.Set(EmptyPseudoClass, !IsLoading && EffectiveSteps.Count == 0);
        PseudoClasses.Set(EditablePseudoClass, IsEditable);
    }

    private void OnRunClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsRunning)
        {
            StopRun();
        }
        else
        {
            StartRun();
        }
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_menu?.IsOpen == true)
        {
            _menu.Close(returnFocus: true);
        }
        else
        {
            OpenAddMenu();
        }
    }

    private AiWorkflowAddMenu CreateMenu()
    {
        var menu = new AiWorkflowAddMenu();
        menu.Picked += (_, item) => AddStep(item);
        return menu;
    }

    private void OnPathPicked(object? sender, string pathId) => SelectPathCore(pathId, animate: true);

    private void SelectPathCore(string pathId, bool animate)
    {
        if (AiWorkflowFormat.Branch(EffectiveSteps) is not { } branch || branch.Paths.All(p => p.Id != pathId))
        {
            return;
        }
        _selectedPathId = pathId;
        _tabs.Select(pathId, animate);
        if (_pathLists.TryGetValue(pathId, out var list))
        {
            _pathSwap.Show(list, animate);
        }
        _rail?.InvalidateVisual();
    }

    /// <summary>The rows on the rail in order: the top-level rows, then the shown path's rows (leaving rows included).</summary>
    private IEnumerable<AiWorkflowStepRow> RailRows()
    {
        if (_rail is null)
        {
            yield break;
        }
        foreach (var child in _rail.Children)
        {
            if (child is AiHeightBox { Content: AiWorkflowStepRow row })
            {
                yield return row;
            }
            else if (ReferenceEquals(child, _pathSwap) && _pathSwap.Current is StackPanel list)
            {
                foreach (var inner in list.Children)
                {
                    if (inner is AiHeightBox { Content: AiWorkflowStepRow pathRow })
                    {
                        yield return pathRow;
                    }
                }
            }
        }
    }

    private void ApplyRow(AiWorkflowStepRow row, bool animate) =>
        row.Apply(_runner.Peek(row.Step.Id), Labels, IsEditable, IsRunning, animate && IsLoaded);

    /// <summary>
    /// Brings the rows in line with the steps: rows are kept by step id, so a replaced list updates them in place; a new
    /// step's row opens on its measured height and a removed one closes before it leaves (when <paramref name="animate"/>
    /// and the old and new flows share a step).
    /// </summary>
    private void Sync(bool animate)
    {
        if (_rail is null)
        {
            return;
        }
        // A leaving row whose close was cut short (the card left the window mid-way) goes now.
        foreach (var (id, row) in _rows.ToList())
        {
            if (row.IsLeaving && !row.Shell.IsMoving && row.Shell.DisplayedHeight <= 0.5)
            {
                Detach(row.Shell);
                _rows.Remove(id);
            }
        }
        var steps = EffectiveSteps;
        var all = AiWorkflowFormat.All(steps).ToList();
        var ids = new HashSet<string>(all.Select(s => s.Id), StringComparer.Ordinal);
        var shared = _rows.Keys.Any(ids.Contains);
        animate = animate && IsLoaded && shared;
        if (!shared && _rows.Count > 0)
        {
            // A different flow: the last run's state does not apply to it.
            _runner.Reset();
        }

        foreach (var step in all)
        {
            if (_rows.TryGetValue(step.Id, out var row) && !row.IsLeaving)
            {
                row.Update(step);
                continue;
            }
            if (row is not null)
            {
                Detach(row.Shell);
            }
            row = new AiWorkflowStepRow(step);
            row.RemoveRequested += OnRowRemove;
            row.RetryRequested += OnRowRetry;
            row.SetNarrow(_narrow);
            row.SetTouch(_touch);
            _rows[step.Id] = row;
            if (animate)
            {
                row.Shell.CloseInstantly();
                row.Shell.OpenWhenLoaded();
            }
            else
            {
                row.Shell.OpenInstantly();
            }
        }
        foreach (var (id, row) in _rows.ToList())
        {
            if (ids.Contains(id) || row.IsLeaving)
            {
                continue;
            }
            if (animate && row.Shell.IsLoaded)
            {
                row.IsLeaving = true;
                var leaving = row;
                leaving.Shell.Closed += (_, _) =>
                {
                    Detach(leaving.Shell);
                    if (_rows.TryGetValue(leaving.Step.Id, out var current) && ReferenceEquals(current, leaving))
                    {
                        _rows.Remove(leaving.Step.Id);
                    }
                    _rail?.InvalidateVisual();
                };
                leaving.Shell.IsExpanded = false;
            }
            else
            {
                Detach(row.Shell);
                _rows.Remove(id);
            }
        }

        // The top-level list, then the branch's paths under it.
        var branch = AiWorkflowFormat.Branch(steps);
        var top = steps.Select(s => (Control)_rows[s.Id].Shell).ToList();
        if (branch is not null)
        {
            top.Add(_pathSwap);
        }
        Place(_rail.Children, top);
        foreach (var row in _rows.Values)
        {
            if (row.Step.Kind != AiWorkflowStepKind.Branch || !ReferenceEquals(row.Step, branch))
            {
                row.SetTabs(null);
            }
        }
        if (branch is not null)
        {
            var branchRow = _rows[branch.Id];
            _tabs.SetPaths(branch.Paths, Labels.Paths);
            _tabs.IsVisible = branch.Paths.Count > 1;
            branchRow.SetTabs(_tabs);
            foreach (var path in branch.Paths)
            {
                if (!_pathLists.TryGetValue(path.Id, out var list))
                {
                    list = new StackPanel();
                    _pathLists[path.Id] = list;
                }
                AutomationProperties.SetName(list, Labels.PathName(path.Label));
                Place(list.Children, [.. path.Steps.Select(s => (Control)_rows[s.Id].Shell)]);
            }
            foreach (var stale in _pathLists.Keys.Where(k => branch.Paths.All(p => p.Id != k)).ToList())
            {
                _pathLists.Remove(stale);
            }
            var selected = SelectedPathId;
            _selectedPathId = selected;
            if (selected is not null)
            {
                _tabs.Select(selected, animate: false);
                _pathSwap.Show(_pathLists[selected], animate: false);
            }
        }
        else
        {
            _pathSwap.Show(null, animate: false);
            _pathLists.Clear();
            _selectedPathId = null;
        }
        foreach (var row in _rows.Values)
        {
            ApplyRow(row, animate);
        }
        AutomationProperties.SetName(_rail, Labels.StepsName);
        UpdatePseudoClasses();
        RefreshChrome();
        _rail.InvalidateVisual();
    }

    /// <summary>
    /// Puts <paramref name="desired"/> in <paramref name="children"/> with the fewest moves (rows mid-animation are never
    /// detached), keeping leaving rows where they were until they have closed.
    /// </summary>
    private static void Place(Avalonia.Controls.Controls children, List<Control> desired)
    {
        var result = new List<Control>(desired);
        var old = children.ToList();
        for (var i = 0; i < old.Count; i++)
        {
            if (old[i] is AiHeightBox { Content: AiWorkflowStepRow { IsLeaving: true } } && !result.Contains(old[i]))
            {
                var at = 0;
                for (var j = i - 1; j >= 0; j--)
                {
                    var before = result.IndexOf(old[j]);
                    if (before >= 0)
                    {
                        at = before + 1;
                        break;
                    }
                }
                result.Insert(at, old[i]);
            }
        }
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (!result.Contains(children[i]))
            {
                children.RemoveAt(i);
            }
        }
        for (var i = 0; i < result.Count; i++)
        {
            if (i < children.Count && ReferenceEquals(children[i], result[i]))
            {
                continue;
            }
            var existing = children.IndexOf(result[i]);
            if (existing >= 0)
            {
                children.RemoveAt(existing);
            }
            else if (result[i].Parent is { } other)
            {
                Detach(result[i], other);
            }
            children.Insert(i, result[i]);
        }
    }

    private void Detach(Control control) => Detach(control, control.Parent);

    private static void Detach(Control control, StyledElement? parent)
    {
        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case AiWorkflowRail rail:
                rail.Children.Remove(control);
                break;
        }
    }

    private void OnRowRemove(object? sender, EventArgs e)
    {
        if (sender is AiWorkflowStepRow row)
        {
            var next = NextFocusAfterRemove(row);
            if (RemoveStep(row.Step.Id))
            {
                next?.Focus(Avalonia.Input.NavigationMethod.Tab);
            }
        }
    }

    /// <summary>Where focus goes when a step is removed with the keyboard: the next row's remove button, or Add step.</summary>
    private Control? NextFocusAfterRemove(AiWorkflowStepRow row)
    {
        if (!row.RemoveButton.IsKeyboardFocusWithin)
        {
            return null;
        }
        var rows = RailRows().ToList();
        var index = rows.IndexOf(row);
        for (var i = index + 1; i < rows.Count; i++)
        {
            if (rows[i].RemoveButton.IsVisible && !rows[i].IsLeaving)
            {
                return rows[i].RemoveButton;
            }
        }
        return _addButton;
    }

    private void OnRowRetry(object? sender, EventArgs e)
    {
        _autoRun = false;
        Retry();
    }

    /// <summary>The header, the footer, the empty state and the skeleton for the current state.</summary>
    private void RefreshChrome()
    {
        var labels = Labels;
        var steps = EffectiveSteps;
        var loading = IsLoading;
        var empty = !loading && steps.Count == 0;
        AutomationProperties.SetName(this, loading ? labels.Loading : DisplayedTitle);
        if (_title is not null)
        {
            _title.Text = DisplayedTitle;
            _title.IsVisible = DisplayedTitle.Length > 0;
        }
        if (_description is not null)
        {
            _description.Text = DisplayedDescription;
            _description.IsVisible = DisplayedDescription.Length > 0;
        }
        if (_simulated is not null)
        {
            _simulated.IsVisible = RunStep is null && !loading && !empty;
            if (_simulated.Child is TextBlock note)
            {
                note.Text = labels.Simulated;
            }
        }
        if (_runButton is not null)
        {
            _runGlyph.Data = IsRunning ? ArcIcons.Square : ArcIcons.Play;
            _runLabel.Text = IsRunning ? labels.Stop : labels.TestRun;
            _runButton.IsEnabled = !loading && !empty;
            AutomationProperties.SetName(_runButton, _runLabel.Text);
        }
        if (_addButton is not null)
        {
            _addLabel.Text = labels.AddStep;
            _addButton.IsVisible = CanOpenMenu || (_menu?.IsOpen ?? false);
            AutomationProperties.SetName(_addButton, labels.AddStep);
        }
        if (_summary is not null)
        {
            _summary.Text = _runner switch
            {
                { IsRunning: true } => labels.Testing,
                { LastStatus: AiWorkflowRunStatus.Passed } => labels.Passed(_runner.LastDuration),
                { LastStatus: AiWorkflowRunStatus.Failed, FailedStepId: { } id } =>
                    labels.FailedAt(AiWorkflowFormat.All(steps).FirstOrDefault(s => s.Id == id)?.Title ?? id),
                { WasStopped: true } => labels.Stopped,
                _ => labels.Ready
            };
        }
        if (_footer is not null)
        {
            _footer.IsVisible = !loading && !empty;
        }
        if (_rail is not null)
        {
            _rail.IsVisible = !loading && !empty;
        }
        if (_skeleton is not null)
        {
            _skeleton.IsVisible = loading;
        }
        RefreshEmpty(empty);
    }

    private void RefreshEmpty(bool empty)
    {
        if (_empty is null)
        {
            return;
        }
        _empty.IsVisible = empty;
        if (!empty)
        {
            return;
        }
        var triggers = AiWorkflowFormat.Triggers(EffectiveCatalog);
        var same = triggers.Count == _triggerRows.Count && triggers.Select((t, i) => ReferenceEquals(t, _triggerRows[i].Item)).All(b => b);
        if (same && _empty.Children.Count > 0)
        {
            if (_empty.Children[0] is TextBlock head)
            {
                head.Text = triggers.Count > 0 ? Labels.ChooseTrigger : Labels.NoSteps;
            }
            return;
        }
        foreach (var row in _triggerRows)
        {
            row.Click -= OnTriggerClick;
        }
        _triggerRows.Clear();
        _empty.Children.Clear();
        var caption = AiWorkflowViews.Text(triggers.Count > 0 ? Labels.ChooseTrigger : Labels.NoSteps, 12, ArcDesignTokenKind.TextMuted);
        caption.Margin = new Thickness(0, 0, 0, 6);
        _empty.Children.Add(caption);
        foreach (var trigger in triggers)
        {
            var row = new AiWorkflowMenuRow(trigger, null) { Classes = { "trigger" }, ShowsDetail = !_narrow };
            row.IsEnabled = IsEditable;
            row.Click += OnTriggerClick;
            _triggerRows.Add(row);
            _empty.Children.Add(row);
        }
    }

    private void OnTriggerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is AiWorkflowMenuRow row)
        {
            var focus = row.IsKeyboardFocusWithin;
            AddStep(row.Item);
            if (focus)
            {
                _runButton?.Focus(Avalonia.Input.NavigationMethod.Tab);
            }
        }
    }

    /// <summary>Three skeleton steps (a mark and a bar each) for the loading state.</summary>
    private void BuildSkeleton()
    {
        if (_skeleton is null || _skeleton.Children.Count > 0)
        {
            return;
        }
        foreach (var width in new[] { 0.42, 0.58, 0.34 })
        {
            var mark = new Border { Width = AiWorkflowMetrics.MarkSize, Height = AiWorkflowMetrics.MarkSize }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
            ArcTokens.Bind(mark, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            var bar = new Border { Height = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left }
                .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(bar, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            var grid = new Grid
            {
                Height = AiWorkflowMetrics.RowHeight,
                ColumnDefinitions = new ColumnDefinitions($"{AiWorkflowMetrics.MarkSize},{AiWorkflowMetrics.MarkGap},*")
            };
            grid.Children.Add(mark);
            Grid.SetColumn(bar, 2);
            grid.Children.Add(bar);
            var fraction = width;
            grid.SizeChanged += (_, args) => bar.Width = Math.Max(24, (args.NewSize.Width - AiWorkflowMetrics.BodyIndent) * fraction);
            _skeleton.Children.Add(grid);
        }
    }
}
