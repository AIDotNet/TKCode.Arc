using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class ProjectBoard
{
    private readonly List<ProjectBoardStage> _stages = new();
    private readonly List<ProjectBoardTask> _tasks = new();
    private readonly HashSet<ProjectBoardTask> _watchedTasks = new();
    private readonly HashSet<ProjectBoardStage> _watchedStages = new();
    private readonly Dictionary<ProjectBoardTask, ProjectBoardCard> _cards = new();
    private readonly Dictionary<ProjectBoardTask, ProjectBoardCard> _leavingCards = new();
    private readonly Dictionary<ProjectBoardStage, ProjectBoardColumn> _columns = new();
    private INotifyCollectionChanged? _observedSource;
    private ProjectBoardPanel? _panel;
    private ScrollViewer? _scroll;
    private ProjectsSnapScroll? _snap;
    private ProjectBoardFilterBar? _filterBar;
    private ProjectBoardButton? _add;
    private ProjectsAvatarStack? _team;
    private ProjectsProgressLine? _progress;
    private TextBlock? _subtitle;
    private ProjectBoardHeaderPanel? _header;
    private ProjectsAnnouncer? _announcer;
    private ProjectBoardGrid _grid;
    private int _suppress;
    private int _draftIndex;
    private bool _rebuildPending;
    private bool _pendingAnimate;
    private bool _built;
    private bool _isNarrow;
    private bool _isCompact;
    private bool _progressPlaced;

    // Public methods -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves <paramref name="task"/> to the stage <paramref name="stageKey"/>: to the end of that column (Arc), or before
    /// the task at <paramref name="index"/> of it. Raises <see cref="TaskMoving"/> (which can cancel) and
    /// <see cref="TaskMoved"/>, announces the move and flies the card.
    /// </summary>
    /// <returns>True when the task moved.</returns>
    public bool MoveTask(ProjectBoardTask task, string stageKey, int? index = null)
    {
        FlushItems();
        var to = _stages.FirstOrDefault(s => s.Key == stageKey);
        return to is not null && MoveCore(task, to, index, null);
    }

    /// <summary>Moves <paramref name="task"/> one stage to the right; false in the last stage or when cancelled.</summary>
    public bool MoveNext(ProjectBoardTask task) => MoveBy(task, ProjectBoardMoveDirection.Next, null);

    /// <summary>Moves <paramref name="task"/> one stage to the left; false in the first stage or when cancelled.</summary>
    public bool MovePrevious(ProjectBoardTask task) => MoveBy(task, ProjectBoardMoveDirection.Previous, null);

    /// <summary>
    /// Does what the Add button does: raises <see cref="AddRequested"/> and adds the task its handler supplies, or a draft
    /// from <see cref="DraftTitles"/> owned by <see cref="CurrentUser"/>.
    /// </summary>
    /// <returns>The task added, or null when nothing was added.</returns>
    public ProjectBoardTask? AddTask()
    {
        FlushItems();
        if (AddStage() is not { } stage)
        {
            return null;
        }
        var args = new ProjectBoardAddRequestedEventArgs(AddRequestedEvent, this, stage);
        RaiseEvent(args);
        var task = args.Task;
        if (task is null)
        {
            if (args.Handled)
            {
                return null;
            }
            task = CreateDraft(stage);
        }
        return AddCore(task, task.StageKey is { } key && FindStage(key) is { } own ? own : stage) ? task : null;
    }

    /// <summary>
    /// Adds <paramref name="task"/> at the end of the stage <paramref name="stageKey"/> (null: the task's own stage, or the
    /// add stage), fading its card in. Needs <see cref="Tasks"/> or a writable <see cref="TasksSource"/>.
    /// </summary>
    /// <returns>True when the task was added.</returns>
    public bool AddTask(ProjectBoardTask task, string? stageKey = null)
    {
        FlushItems();
        var stage = stageKey is not null ? FindStage(stageKey) : task.StageKey is { } key ? FindStage(key) ?? AddStage() : AddStage();
        return stage is not null && AddCore(task, stage);
    }

    /// <summary>Moves keyboard focus to <paramref name="task"/>'s card (its first enabled arrow) and scrolls it into view.</summary>
    /// <returns>False when the task has no visible card.</returns>
    public bool FocusTask(ProjectBoardTask task)
    {
        FlushItems();
        if (!_cards.TryGetValue(task, out var card) || card.IsLeaving)
        {
            return false;
        }
        card.ApplyTemplate();
        var target = card.PreviousButton is { IsEffectivelyEnabled: true } previous ? previous : card.NextButton;
        if (target is null || !target.IsEffectivelyEnabled)
        {
            return false;
        }
        BringIntoView(card);
        return target.Focus(NavigationMethod.Tab);
    }

    /// <summary>Scrolls the columns so the stage <paramref name="stageKey"/> starts at the left edge (when they scroll).</summary>
    public void ScrollToStage(string stageKey, bool animate = true)
    {
        FlushItems();
        var index = _stages.FindIndex(s => s.Key == stageKey);
        if (index >= 0 && _snap is not null && _grid.Scrolls)
        {
            _snap.ScrollTo(_grid.SnapPoint(index), animate);
        }
    }

    // Internals for the parts and tests --------------------------------------------------------------------------------

    internal ProjectBoardPanel? Panel => _panel;

    internal ProjectsSnapScroll? SnapScroll => _snap;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal ProjectBoardFilterBar? FilterBar => _filterBar;

    internal ProjectBoardGrid Grid => _grid;

    internal IReadOnlyList<ProjectBoardStage> EffectiveStages
    {
        get
        {
            FlushItems();
            return _stages;
        }
    }

    internal IReadOnlyList<ProjectBoardTask> EffectiveTasks
    {
        get
        {
            FlushItems();
            return _tasks;
        }
    }

    /// <summary>The card of <paramref name="task"/>, if it is shown (or still fading out).</summary>
    internal ProjectBoardCard? CardFor(ProjectBoardTask task)
    {
        FlushItems();
        return _cards.GetValueOrDefault(task);
    }

    /// <summary>The column control of <paramref name="stage"/>.</summary>
    internal ProjectBoardColumn? ColumnFor(ProjectBoardStage stage)
    {
        FlushItems();
        return _columns.GetValueOrDefault(stage);
    }

    /// <summary>The stage <paramref name="task"/> is shown in (unknown keys: the first stage).</summary>
    internal ProjectBoardStage? StageOf(ProjectBoardTask task)
    {
        if (_stages.Count == 0)
        {
            return null;
        }
        return task.StageKey is { } key && FindStage(key) is { } stage ? stage : _stages[0];
    }

    /// <summary>A card's arrow was pressed.</summary>
    internal void OnCardMoveClicked(ProjectBoardCard card, ProjectBoardMoveDirection direction)
    {
        var button = card.ButtonFor(direction);
        var hadFocus = button?.IsFocused == true;
        var keyboard = button?.Classes.Contains(":focus-visible") == true;
        if (!MoveBy(card.Task, direction, card))
        {
            return;
        }
        if (hadFocus)
        {
            RestoreFocus(card, direction, keyboard);
        }
        BringIntoView(card);
    }

    // Moving -------------------------------------------------------------------------------------------------------------

    private bool MoveBy(ProjectBoardTask task, ProjectBoardMoveDirection direction, ProjectBoardCard? card)
    {
        FlushItems();
        if (StageOf(task) is not { } from)
        {
            return false;
        }
        var index = _stages.IndexOf(from) + (direction == ProjectBoardMoveDirection.Next ? 1 : -1);
        if (index < 0 || index >= _stages.Count)
        {
            return false;
        }
        return MoveCore(task, _stages[index], null, card is null ? null : direction);
    }

    private bool MoveCore(ProjectBoardTask task, ProjectBoardStage to, int? index, ProjectBoardMoveDirection? direction)
    {
        if (!_tasks.Contains(task) || StageOf(task) is not { } from)
        {
            return false;
        }
        if (ReferenceEquals(from, to) && index is null)
        {
            return false;
        }
        var moving = new ProjectBoardTaskMovingEventArgs(TaskMovingEvent, this, task, from, to);
        RaiseEvent(moving);
        if (moving.Cancel)
        {
            return false;
        }
        var fromIndex = TasksIn(from).IndexOf(task);
        _suppress++;
        try
        {
            Reorder(task, to, index);
            task.StageKey = to.Key;
        }
        finally
        {
            _suppress--;
        }
        if (_cards.TryGetValue(task, out var card) && !card.IsLeaving)
        {
            _panel?.MarkFlight(card);
        }
        Rebuild(animate: true);
        var toIndex = TasksIn(to).IndexOf(task);
        RaiseEvent(new ProjectBoardTaskMovedEventArgs(TaskMovedEvent, this, task, from, to, fromIndex, toIndex));
        if (direction is { } d && MoveCommand is { } command)
        {
            var request = new ProjectBoardMoveRequest(task, d, from, to);
            if (command.CanExecute(request))
            {
                command.Execute(request);
            }
        }
        Announce(ProjectsText.Format(MovedFormat, task.Title, to.DisplayHeader));
        return true;
    }

    /// <summary>Puts <paramref name="task"/> at the end of <paramref name="to"/> (or before its <paramref name="index"/>th task) in a writable source.</summary>
    private void Reorder(ProjectBoardTask task, ProjectBoardStage to, int? index)
    {
        if (WritableSource() is not { } list)
        {
            return;
        }
        var at = list.IndexOf(task);
        if (at < 0)
        {
            return;
        }
        var others = _tasks.Where(t => !ReferenceEquals(t, task) && ReferenceEquals(StageOf(t), to)).ToList();
        list.RemoveAt(at);
        var insert = list.Count;
        if (index is { } i && i >= 0 && i < others.Count)
        {
            insert = list.IndexOf(others[i]);
        }
        list.Insert(insert, task);
        RefreshTaskList();
    }

    private List<ProjectBoardTask> TasksIn(ProjectBoardStage stage) => _tasks.Where(t => ReferenceEquals(StageOf(t), stage)).ToList();

    private void RestoreFocus(ProjectBoardCard card, ProjectBoardMoveDirection direction, bool keyboard)
    {
        if (card.IsLeaving)
        {
            return;
        }
        var target = card.ButtonFor(direction);
        if (target is null || !target.IsEffectivelyEnabled)
        {
            target = card.ButtonFor(direction == ProjectBoardMoveDirection.Next ? ProjectBoardMoveDirection.Previous : ProjectBoardMoveDirection.Next);
        }
        if (target is { IsEffectivelyEnabled: true } && !target.IsFocused)
        {
            target.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    /// <summary>Scrolls the card's column into view when the columns scroll and it is not fully shown (flights keep going).</summary>
    private void BringIntoView(ProjectBoardCard card)
    {
        if (_snap is null || _scroll is null || !_grid.Scrolls || StageOf(card.Task) is not { } stage)
        {
            return;
        }
        var index = _stages.IndexOf(stage);
        var left = _grid.ColumnX(index);
        var right = left + _grid.ColumnWidth;
        var offset = _snap.IsGliding ? _snap.Target : _scroll.Offset.X;
        var viewport = _scroll.Viewport.Width > 0 ? _scroll.Viewport.Width : _grid.ViewportWidth;
        if (left >= offset - 0.5 && right <= offset + viewport + 0.5)
        {
            return;
        }
        _snap.ScrollTo(_grid.SnapPoint(index), animate: true);
    }

    // Adding -------------------------------------------------------------------------------------------------------------

    private ProjectBoardStage? AddStage()
    {
        if (_stages.Count == 0)
        {
            return null;
        }
        return AddStageKey is { } key && FindStage(key) is { } stage ? stage : _stages[0];
    }

    private ProjectBoardStage? FindStage(string key) => _stages.FirstOrDefault(s => s.Key == key);

    private ProjectBoardTask CreateDraft(ProjectBoardStage stage)
    {
        var titles = DraftTitles;
        var title = titles is { Count: > 0 } ? titles[_draftIndex % titles.Count] : NewTaskTitle;
        _draftIndex++;
        return new ProjectBoardTask(title, stage.Key)
        {
            Area = DraftArea,
            DueText = NoDateText,
            Owner = CurrentUser,
        };
    }

    private bool AddCore(ProjectBoardTask task, ProjectBoardStage stage)
    {
        if (WritableSource() is not { } list || list.Contains(task))
        {
            return false;
        }
        _suppress++;
        try
        {
            task.StageKey = stage.Key;
            list.Add(task);
            RefreshTaskList();
        }
        finally
        {
            _suppress--;
        }
        Rebuild(animate: true);
        RaiseEvent(new ProjectBoardTaskEventArgs(TaskAddedEvent, this, task, stage));
        if (AddCommand is { } command && command.CanExecute(task))
        {
            command.Execute(task);
        }
        Announce(ProjectsText.Format(AddedFormat, task.Title, stage.DisplayHeader));
        return true;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AddTask();
    }

    private void Announce(string text) => _announcer?.Announce(text);

    // Collections ----------------------------------------------------------------------------------------------------------

    private IList? WritableSource()
    {
        IEnumerable source = TasksSource ?? Tasks;
        return source is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;
    }

    private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppress == 0 && TasksSource is null)
        {
            ScheduleRebuild(animate: true);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppress == 0)
        {
            ScheduleRebuild(animate: true);
        }
    }

    private void OnStagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild(animate: false);

    private void OnTaskPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_suppress == 0)
        {
            ScheduleRebuild(animate: true);
        }
    }

    private void OnStagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => ScheduleRebuild(animate: false);

    /// <summary>
    /// Collection and item changes are applied together, once per dispatcher turn (a Clear and a refill keep the cards of
    /// the tasks that come back). Public methods apply pending changes first.
    /// </summary>
    private void ScheduleRebuild(bool animate)
    {
        _pendingAnimate |= animate;
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPending)
            {
                FlushItems();
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>Applies collection changes that are still waiting.</summary>
    internal void FlushItems()
    {
        if (!_rebuildPending)
        {
            return;
        }
        var animate = _pendingAnimate;
        Rebuild(animate);
    }

    private void RefreshTaskList()
    {
        _tasks.Clear();
        IEnumerable source = TasksSource ?? Tasks;
        if (source is null)
        {
            return;
        }
        foreach (var o in source)
        {
            if (o is ProjectBoardTask task && !_tasks.Contains(task))
            {
                _tasks.Add(task);
            }
        }
    }

    private void RefreshStageList()
    {
        _stages.Clear();
        if (Stages is null)
        {
            return;
        }
        foreach (var stage in Stages)
        {
            if (stage is not null && !_stages.Contains(stage))
            {
                _stages.Add(stage);
            }
        }
    }

    private void Watch()
    {
        foreach (var task in _watchedTasks.Where(t => !_tasks.Contains(t)).ToList())
        {
            task.PropertyChanged -= OnTaskPropertyChanged;
            _watchedTasks.Remove(task);
        }
        foreach (var task in _tasks)
        {
            if (_watchedTasks.Add(task))
            {
                task.PropertyChanged += OnTaskPropertyChanged;
            }
        }
        foreach (var stage in _watchedStages.Where(s => !_stages.Contains(s)).ToList())
        {
            stage.PropertyChanged -= OnStagePropertyChanged;
            _watchedStages.Remove(stage);
        }
        foreach (var stage in _stages)
        {
            if (_watchedStages.Add(stage))
            {
                stage.PropertyChanged += OnStagePropertyChanged;
            }
        }
    }

    // Rebuild --------------------------------------------------------------------------------------------------------------

    private bool IsMine(ProjectBoardTask task)
    {
        if (CurrentUser is not { } me || task.Owner is not { } owner)
        {
            return false;
        }
        return ReferenceEquals(me, owner) || (!string.IsNullOrEmpty(me.Identity) && me.Identity == owner.Identity);
    }

    private bool IsShown(ProjectBoardTask task) => Filter == ProjectBoardFilter.All || CurrentUser is null || IsMine(task);

    /// <summary>The stages that count as finished: the marked ones, or the last stage.</summary>
    private bool IsDoneStage(ProjectBoardStage stage)
    {
        var any = false;
        foreach (var s in _stages)
        {
            if (s.IsDone)
            {
                any = true;
                break;
            }
        }
        return any ? stage.IsDone : _stages.Count > 0 && ReferenceEquals(stage, _stages[^1]);
    }

    /// <summary>
    /// Brings the cards, columns and header in line with the data. <paramref name="animate"/> arms the card springs (a
    /// move, a filter, an add); otherwise everything lands at once (first build, structure changes).
    /// </summary>
    private void Rebuild(bool animate)
    {
        _rebuildPending = false;
        _pendingAnimate = false;
        RefreshStageList();
        RefreshTaskList();
        Watch();

        var done = 0;
        foreach (var task in _tasks)
        {
            if (StageOf(task) is { } s && IsDoneStage(s))
            {
                done++;
            }
        }
        DoneCount = done;
        TaskCount = _tasks.Count;
        UpdateHeader();

        if (_panel is null)
        {
            return;
        }
        _panel.AnimatesChanges = AnimatesMoves;
        var spec = animate && _built ? _panel.ChangeSpec : null;
        if (spec is not null)
        {
            _panel.Flip.Arm(spec);
        }

        // Columns follow the stages.
        var columns = new List<ProjectBoardColumn>(_stages.Count);
        foreach (var stage in _stages)
        {
            if (!_columns.TryGetValue(stage, out var column))
            {
                column = new ProjectBoardColumn(stage);
                _columns[stage] = column;
            }
            columns.Add(column);
        }
        foreach (var stage in _columns.Keys.Where(s => !_stages.Contains(s)).ToList())
        {
            _columns.Remove(stage);
        }
        _panel.SetColumns(columns);

        // Cards: one per shown task, kept across rebuilds.
        var perColumn = new List<ProjectBoardCard>[_stages.Count];
        for (var i = 0; i < perColumn.Length; i++)
        {
            perColumn[i] = new List<ProjectBoardCard>();
        }
        var shown = new HashSet<ProjectBoardTask>();
        foreach (var task in _tasks)
        {
            if (StageOf(task) is not { } stage || !IsShown(task))
            {
                continue;
            }
            shown.Add(task);
            if (!_cards.TryGetValue(task, out var card))
            {
                if (_leavingCards.Remove(task, out var leaving))
                {
                    // Shown again before its fade ended: the same card comes back from where it is.
                    card = leaving;
                    _panel.Revive(card);
                }
                else
                {
                    card = new ProjectBoardCard(task) { Board = this };
                    _panel.AddCard(card, enter: spec is not null);
                }
                _cards[task] = card;
            }
            perColumn[_stages.IndexOf(stage)].Add(card);
            UpdateCard(card, stage);
        }
        foreach (var (task, card) in _cards.Where(p => !shown.Contains(p.Key)).ToList())
        {
            _cards.Remove(task);
            if (spec is not null && _tasks.Contains(task))
            {
                _leavingCards[task] = card;
            }
            if (spec is not null)
            {
                _panel.BeginLeave(card);
            }
            else
            {
                _panel.RemoveCard(card);
            }
        }
        _panel.SetCards(perColumn);

        for (var i = 0; i < _stages.Count; i++)
        {
            UpdateColumn(columns[i], _stages[i], perColumn[i].Count);
        }
        _built = true;
        UpdateLayoutState();
    }

    private void UpdateCard(ProjectBoardCard card, ProjectBoardStage stage)
    {
        var task = card.Task;
        var index = _stages.IndexOf(stage);
        card.Stage = stage;
        card.IsNarrow = _isNarrow;
        card.Refresh(task.CommentCount.ToString(CultureInfo.CurrentCulture));
        card.IsDone = IsDoneStage(stage);
        card.CanMovePrevious = index > 0;
        card.CanMoveNext = index < _stages.Count - 1;
        card.PreviousName = ProjectsText.Format(MoveToFormat, task.Title, index > 0 ? _stages[index - 1].DisplayHeader : MovePreviousText);
        card.NextName = ProjectsText.Format(MoveToFormat, task.Title, index < _stages.Count - 1 ? _stages[index + 1].DisplayHeader : MoveNextText);
        AutomationProperties.SetName(card, CardLabel(task));
    }

    /// <summary>"Title, Area, due Fri, assigned to Rhea Calloway, 4 comments" (parts that are empty are left out).</summary>
    internal string CardLabel(ProjectBoardTask task)
    {
        var parts = new List<string>(5) { task.Title };
        if (!string.IsNullOrEmpty(task.Area))
        {
            parts.Add(task.Area!);
        }
        if (!string.IsNullOrEmpty(task.DueText))
        {
            parts.Add(ProjectsText.Format(DueLabelFormat, task.DueText));
        }
        if (task.Owner is { } owner && !string.IsNullOrEmpty(owner.Name))
        {
            parts.Add(ProjectsText.Format(OwnerLabelFormat, owner.Name));
        }
        if (task.CommentCount > 0)
        {
            parts.Add(ProjectsText.Format(task.CommentCount == 1 ? CommentsSingularFormat : CommentsFormat, task.CommentCount));
        }
        return string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private void UpdateColumn(ProjectBoardColumn column, ProjectBoardStage stage, int count)
    {
        column.Header = stage.DisplayHeader;
        column.CountText = count.ToString(CultureInfo.CurrentCulture);
        column.EmptyText = count > 0
            ? null
            : stage.EmptyText ?? (Filter == ProjectBoardFilter.Mine && CurrentUser is not null ? EmptyMineText : EmptyText);
        AutomationProperties.SetName(column,
            ProjectsText.Format(count == 1 ? ColumnLabelSingularFormat : ColumnLabelFormat, stage.DisplayHeader, count));
    }

    private void UpdateHeader()
    {
        if (_subtitle is not null)
        {
            _subtitle.Text = ProjectsText.Format(ProgressFormat, DoneCount, TaskCount);
        }
        if (_progress is not null)
        {
            // The first values after the template is applied land at once (Arc renders the final state on load).
            var spring = _progress.Motion;
            if (!_progressPlaced)
            {
                _progress.Motion = MotionSpec.Instant;
            }
            _progress.Maximum = Math.Max(1, TaskCount);
            _progress.Value = TaskCount == 0 ? 0 : DoneCount;
            if (!_progressPlaced)
            {
                _progress.Motion = spring;
                _progressPlaced = true;
            }
        }
    }

    private void UpdateFilterBar()
    {
        if (_filterBar is null)
        {
            return;
        }
        _filterBar.IsVisible = CurrentUser is not null;
        _filterBar.AllText = AllWorkText;
        _filterBar.MineText = MyWorkText;
        _filterBar.Label = FilterLabel;
        _filterBar.Filter = Filter;
    }

    private void UpdateAdd()
    {
        if (_add is null)
        {
            return;
        }
        _add.IsVisible = IsAddVisible;
        AutomationProperties.SetName(_add, AddText);
        Tooltip.SetTip(_add, _isCompact ? AddText : null);
    }

    private void UpdateTeam()
    {
        if (_team is null)
        {
            return;
        }
        _team.People = Team;
        _team.Label = TeamLabel;
        // Narrow boards leave the team out (Arc), and so does an empty team (its margin would still take room).
        _team.IsVisible = Team is { Count: > 0 } && !_isNarrow;
    }

    private void OnTeamChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateTeam();

    // Template and size ----------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_filterBar is not null)
        {
            _filterBar.Chosen -= OnFilterChosen;
        }
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        _snap?.Dispose();
        _snap = null;
        if (_panel is not null)
        {
            _panel.Children.Clear();
            _panel.Flip.Clear();
        }
        _cards.Clear();
        _columns.Clear();
        _built = false;

        if (_panel is not null)
        {
            _panel.CardRemoved -= OnCardRemoved;
        }
        _leavingCards.Clear();
        _panel = e.NameScope.Find<ProjectBoardPanel>(PanelPart);
        if (_panel is not null)
        {
            _panel.CardRemoved += OnCardRemoved;
        }
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _filterBar = e.NameScope.Find<ProjectBoardFilterBar>(FilterPart);
        _add = e.NameScope.Find<ProjectBoardButton>(AddPart);
        _team = e.NameScope.Find<ProjectsAvatarStack>(TeamPart);
        _progress = e.NameScope.Find<ProjectsProgressLine>(ProgressPart);
        _progressPlaced = false;
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        if (_subtitle is not null)
        {
            _subtitle.FontFeatures = ProjectBoardLayout.TabularNumbers;
        }
        _header = e.NameScope.Find<ProjectBoardHeaderPanel>(HeaderPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        if (_filterBar is not null)
        {
            _filterBar.Chosen += OnFilterChosen;
        }
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_scroll is not null)
        {
            _snap = new ProjectsSnapScroll(_scroll, () => ProjectBoardLayout.SnapPoints(_grid));
        }
        UpdateFilterBar();
        UpdateAdd();
        UpdateTeam();
        Rebuild(animate: false);
        UpdateBreakpoints(Bounds.Width);
    }

    private void OnFilterChosen(ProjectBoardFilter filter) => SetCurrentValue(FilterProperty, filter);

    private void OnCardRemoved(ProjectBoardCard card)
    {
        if (_leavingCards.TryGetValue(card.Task, out var leaving) && ReferenceEquals(leaving, card))
        {
            _leavingCards.Remove(card.Task);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var narrow = width <= NarrowWidth;
        var compact = width <= CompactWidth;
        var changed = narrow != _isNarrow || compact != _isCompact;
        _isNarrow = narrow;
        _isCompact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_header is not null)
        {
            _header.IsCompact = compact;
        }
        if (_filterBar is not null)
        {
            _filterBar.IsNarrow = narrow;
        }
        if (changed)
        {
            foreach (var card in _cards.Values)
            {
                card.IsNarrow = narrow;
            }
            UpdateAdd();
            UpdateTeam();
        }
        _viewportWidth = Math.Max(0, width - (IsFramed ? 2 : 0));
        UpdateLayoutState();
    }

    private double _viewportWidth = double.NaN;

    private void UpdateLayoutState()
    {
        if (!double.IsFinite(_viewportWidth))
        {
            return;
        }
        _grid = ProjectBoardLayout.Columns(_viewportWidth, _stages.Count, _isNarrow, MinColumnWidth);
        _panel?.Configure(_viewportWidth, _isNarrow, MinColumnWidth);
        PseudoClasses.Set(ScrollingPseudoClass, _grid.Scrolls);
        if (_snap is not null)
        {
            _snap.IsEnabled = _grid.Scrolls;
            _snap.Threshold = _grid.ColumnWidth / 3;
        }
    }

    // Property changes -----------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TasksProperty)
        {
            if (change.OldValue is AvaloniaList<ProjectBoardTask> oldTasks)
            {
                oldTasks.CollectionChanged -= OnTasksChanged;
            }
            if (change.NewValue is AvaloniaList<ProjectBoardTask> newTasks)
            {
                newTasks.CollectionChanged += OnTasksChanged;
            }
            ScheduleRebuild(animate: false);
            FlushItems();
        }
        else if (property == TasksSourceProperty)
        {
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged -= OnSourceChanged;
            }
            _observedSource = TasksSource as INotifyCollectionChanged;
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged += OnSourceChanged;
            }
            ScheduleRebuild(animate: false);
            FlushItems();
        }
        else if (property == StagesProperty)
        {
            if (change.OldValue is AvaloniaList<ProjectBoardStage> oldStages)
            {
                oldStages.CollectionChanged -= OnStagesChanged;
            }
            if (change.NewValue is AvaloniaList<ProjectBoardStage> newStages)
            {
                newStages.CollectionChanged += OnStagesChanged;
            }
            ScheduleRebuild(animate: false);
            FlushItems();
        }
        else if (property == FilterProperty)
        {
            PseudoClasses.Set(FilteredPseudoClass, Filter == ProjectBoardFilter.Mine);
            UpdateFilterBar();
            ScheduleRebuild(animate: true);
            FlushItems();
            RaiseEvent(new ProjectBoardFilterChangedEventArgs(FilterChangedEvent, this,
                change.GetOldValue<ProjectBoardFilter>(), change.GetNewValue<ProjectBoardFilter>()));
        }
        else if (property == CurrentUserProperty)
        {
            UpdateFilterBar();
            ScheduleRebuild(animate: true);
        }
        else if (property == TeamProperty || property == TeamLabelProperty)
        {
            if (property == TeamProperty)
            {
                if (change.OldValue is AvaloniaList<ProjectPerson> oldTeam)
                {
                    oldTeam.CollectionChanged -= OnTeamChanged;
                }
                if (change.NewValue is AvaloniaList<ProjectPerson> newTeam)
                {
                    newTeam.CollectionChanged += OnTeamChanged;
                }
            }
            UpdateTeam();
        }
        else if (property == AllWorkTextProperty || property == MyWorkTextProperty || property == FilterLabelProperty)
        {
            UpdateFilterBar();
        }
        else if (property == AddTextProperty || property == IsAddVisibleProperty)
        {
            UpdateAdd();
        }
        else if (property == ProgressFormatProperty)
        {
            UpdateHeader();
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == NarrowWidthProperty || property == CompactWidthProperty || property == MinColumnWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == EmptyTextProperty || property == EmptyMineTextProperty || property == ColumnLabelFormatProperty ||
                 property == ColumnLabelSingularFormatProperty || property == MoveToFormatProperty || property == MovePreviousTextProperty ||
                 property == MoveNextTextProperty || property == DueLabelFormatProperty || property == OwnerLabelFormatProperty ||
                 property == CommentsFormatProperty || property == CommentsSingularFormatProperty || property == AddStageKeyProperty ||
                 property == AnimatesMovesProperty)
        {
            ScheduleRebuild(animate: false);
        }
    }
}
