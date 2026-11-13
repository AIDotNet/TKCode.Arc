using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A board of tasks handed to AI agents: four columns (Queued, Running, Needs you, Done) of cards that show the task, the
/// agent that owns it, its progress or its question, and when it last changed. Chips filter the board by agent, and
/// pressing a card (or Enter on it) raises <see cref="TaskOpened"/> so the app can open that task's run. Give it your
/// tasks through <see cref="Tasks"/>; a task's changes move its card. Without <see cref="Tasks"/> it shows sample tasks
/// whose running ones progress on their own.
/// </summary>
/// <example><code>
/// &lt;arc:AgentTasks Tasks="{Binding Tasks}" TaskOpened="OnTaskOpened" /&gt;
/// </code></example>
[TemplatePart(BoardPart, typeof(Grid))]
[TemplatePart(FiltersPart, typeof(Panel))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(CompactPseudoClass)]
public class AgentTasks : TemplatedControl
{
    public const string BoardPart = "PART_Board";
    public const string FiltersPart = "PART_Filters";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the board is at most 720 wide: the columns stack.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The four columns, in order, by the status they show first.</summary>
    internal static readonly AgentTaskStatus[] Columns = [AgentTaskStatus.Queued, AgentTaskStatus.Running, AgentTaskStatus.NeedsInput, AgentTaskStatus.Done];

    #region Properties

    /// <summary>
    /// The tasks. A collection that raises change notifications is followed; each task's own changes are followed too.
    /// Null (the default) shows sample tasks that progress on their own.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AgentTaskItem>?> TasksProperty =
        AvaloniaProperty.Register<AgentTasks, IReadOnlyList<AgentTaskItem>?>(nameof(Tasks));

    /// <summary>The agent whose tasks show; null shows every agent (two-way: the chips write it).</summary>
    public static readonly StyledProperty<string?> AgentFilterProperty =
        AvaloniaProperty.Register<AgentTasks, string?>(nameof(AgentFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the board shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AgentTasksLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentTasks, AgentTasksLabels>(nameof(Labels), new AgentTasksLabels());

    /// <summary>Whether the sample tasks are shown (no <see cref="Tasks"/> given).</summary>
    public static readonly DirectProperty<AgentTasks, bool> IsSimulatedProperty =
        AvaloniaProperty.RegisterDirect<AgentTasks, bool>(nameof(IsSimulated), o => o.IsSimulated);

    /// <summary>True while the board is at most 720 wide.</summary>
    public static readonly DirectProperty<AgentTasks, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AgentTasks, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentTasks, double>(nameof(CompactWidth), 720);

    /// <summary>Raised when the person opens a task's run (a press on its card, or Enter).</summary>
    public static readonly RoutedEvent<AgentTaskEventArgs> TaskOpenedEvent =
        RoutedEvent.Register<AgentTasks, AgentTaskEventArgs>(nameof(TaskOpened), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TasksProperty"/>
    public IReadOnlyList<AgentTaskItem>? Tasks { get => GetValue(TasksProperty); set => SetValue(TasksProperty, value); }

    /// <inheritdoc cref="AgentFilterProperty"/>
    public string? AgentFilter { get => GetValue(AgentFilterProperty); set => SetValue(AgentFilterProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentTasksLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool IsSimulated { get => _isSimulated; private set => SetAndRaise(IsSimulatedProperty, ref _isSimulated, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="TaskOpenedEvent"/>
    public event EventHandler<AgentTaskEventArgs>? TaskOpened
    {
        add => AddHandler(TaskOpenedEvent, value);
        remove => RemoveHandler(TaskOpenedEvent, value);
    }

    #endregion

    private readonly Dictionary<AgentTaskItem, AgentTasksCard> _cards = new();
    private readonly List<AgentTaskItem> _observed = new();
    private readonly AgentTasksColumn[] _columns = new AgentTasksColumn[4];
    private IReadOnlyList<AgentTaskItem>? _sample;
    private INotifyCollectionChanged? _observedCollection;
    private Grid? _board;
    private Panel? _filters;
    private AiAnnouncer? _announcer;
    private TextBlock? _title;
    private TextBlock? _summary;
    private DispatcherTimer? _timer;
    private bool _isSimulated = true;
    private bool _isCompact;
    private bool _attached;

    static AgentTasks()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AgentTasks>(AutomationLandmarkType.Region);
    }

    /// <summary>The current time for relative times and the samples; tests replace it.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>The tasks on the board (yours or the samples).</summary>
    internal IReadOnlyList<AgentTaskItem> ShownTasks => Tasks ?? (_sample ??= AgentTasksSamples.Tasks(Clock()));

    internal AgentTasksColumn? ColumnAt(int index) => _columns[index];

    internal AgentTasksCard? CardOf(AgentTaskItem task) => _cards.GetValueOrDefault(task);

    internal AiAnnouncer? Announcer => _announcer;

    internal Panel? FilterHost => _filters;

    /// <summary>The column a status sits in.</summary>
    internal static int ColumnOf(AgentTaskStatus status) => status switch
    {
        AgentTaskStatus.Queued => 0,
        AgentTaskStatus.Running => 1,
        AgentTaskStatus.NeedsInput or AgentTaskStatus.Failed => 2,
        _ => 3
    };

    /// <summary>Raises <see cref="TaskOpened"/> for <paramref name="task"/>.</summary>
    public void OpenTask(AgentTaskItem task) => RaiseEvent(new AgentTaskEventArgs(TaskOpenedEvent, task));

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _board = e.NameScope.Find<Grid>(BoardPart);
        _filters = e.NameScope.Find<Panel>(FiltersPart);
        _announcer = e.NameScope.Find<AiAnnouncer>(AnnouncerPart);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _summary = e.NameScope.Find<TextBlock>("PART_Summary");
        _board?.Children.Clear();
        for (var i = 0; i < 4; i++)
        {
            var column = new AgentTasksColumn();
            _columns[i] = column;
            _board?.Children.Add(column);
        }
        LayoutColumns();
        Rebuild();
    }

    private void LayoutColumns()
    {
        if (_board is null)
        {
            return;
        }
        _board.ColumnDefinitions.Clear();
        _board.RowDefinitions.Clear();
        for (var i = 0; i < 4; i++)
        {
            if (IsCompact)
            {
                _board.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            else
            {
                _board.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }
            if (_columns[i] is { } column)
            {
                Grid.SetColumn(column, IsCompact ? 0 : i);
                Grid.SetRow(column, IsCompact ? i : 0);
            }
        }
    }

    /// <summary>Lays every card into its column, in the order of the tasks, and refreshes the header and the chips.</summary>
    private void Rebuild()
    {
        var labels = Labels;
        var tasks = ShownTasks;
        var filter = AgentFilter;
        var keep = new HashSet<AgentTaskItem>(tasks);
        foreach (var task in _cards.Keys.ToArray())
        {
            if (!keep.Contains(task))
            {
                _cards.Remove(task);
            }
        }
        var lists = new List<AgentTasksCard>[4];
        for (var i = 0; i < 4; i++)
        {
            lists[i] = new List<AgentTasksCard>();
        }
        var running = 0;
        var waiting = 0;
        foreach (var task in tasks)
        {
            if (task.Status == AgentTaskStatus.Running)
            {
                running++;
            }
            else if (task.Status is AgentTaskStatus.NeedsInput or AgentTaskStatus.Failed)
            {
                waiting++;
            }
            if (filter is not null && !string.Equals(task.Agent, filter, StringComparison.Ordinal))
            {
                continue;
            }
            if (!_cards.TryGetValue(task, out var card))
            {
                card = new AgentTasksCard { Task = task, Owner = this };
                _cards[task] = card;
            }
            card.Refresh();
            lists[ColumnOf(task.Status)].Add(card);
        }
        for (var i = 0; i < 4; i++)
        {
            _columns[i]?.SetCards(labels.Column(Columns[i]), lists[i], labels.EmptyColumn);
        }
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_summary is not null)
        {
            _summary.Text = labels.Summary(running, waiting);
        }
        RebuildFilters(tasks);
    }

    private void RebuildFilters(IReadOnlyList<AgentTaskItem> tasks)
    {
        if (_filters is null)
        {
            return;
        }
        AutomationProperties.SetName(_filters, Labels.Filter);
        var agents = new List<string>();
        foreach (var task in tasks)
        {
            if (task.Agent is { Length: > 0 } agent && !agents.Contains(agent))
            {
                agents.Add(agent);
            }
        }
        agents.Sort(StringComparer.Ordinal);
        var wanted = new List<string?> { null };
        wanted.AddRange(agents);
        var same = _filters.Children.Count == wanted.Count;
        for (var i = 0; same && i < wanted.Count; i++)
        {
            same = _filters.Children[i] is AgentTasksChip chip && chip.Agent == wanted[i];
        }
        if (!same)
        {
            _filters.Children.Clear();
            foreach (var agent in wanted)
            {
                var chip = new AgentTasksChip { Agent = agent };
                chip.Click += (_, _) => SetCurrentValue(AgentFilterProperty, agent);
                _filters.Children.Add(chip);
            }
        }
        foreach (var child in _filters.Children)
        {
            if (child is AgentTasksChip chip)
            {
                chip.Content = chip.Agent ?? Labels.AllAgents;
                chip.IsSelected = chip.Agent == AgentFilter;
                AutomationProperties.SetName(chip, (string)chip.Content);
            }
        }
    }

    #endregion

    #region Keyboard

    /// <summary>
    /// Arrow keys move between cards: Up and Down within a column, Left and Right to the nearest card of the next column
    /// that has one; Home and End go to the first and last card of the column. Enter or Space opens the focused card.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not AgentTasksCard card)
        {
            return;
        }
        var column = -1;
        var row = -1;
        for (var i = 0; i < 4 && column < 0; i++)
        {
            var index = _columns[i]?.IndexOf(card) ?? -1;
            if (index >= 0)
            {
                column = i;
                row = index;
            }
        }
        if (column < 0)
        {
            return;
        }
        AgentTasksCard? target = null;
        switch (e.Key)
        {
            case Key.Up:
                target = _columns[column].CardAt(row - 1) ?? (IsCompact ? NextColumnCard(column, -1, int.MaxValue) : null);
                break;
            case Key.Down:
                target = _columns[column].CardAt(row + 1) ?? (IsCompact ? NextColumnCard(column, 1, 0) : null);
                break;
            case Key.Left:
                target = NextColumnCard(column, -1, row);
                break;
            case Key.Right:
                target = NextColumnCard(column, 1, row);
                break;
            case Key.Home:
                target = _columns[column].CardAt(0);
                break;
            case Key.End:
                target = _columns[column].CardAt(_columns[column].Count - 1);
                break;
        }
        if (target is not null && !ReferenceEquals(target, card))
        {
            e.Handled = true;
            target.Focus(NavigationMethod.Directional);
            target.BringIntoView();
        }
    }

    private AgentTasksCard? NextColumnCard(int column, int direction, int row)
    {
        for (var i = column + direction; i >= 0 && i < 4; i += direction)
        {
            if (_columns[i] is { Count: > 0 } next)
            {
                return next.CardAt(Math.Clamp(row, 0, next.Count - 1));
            }
        }
        return null;
    }

    #endregion

    #region Data

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TasksProperty)
        {
            IsSimulated = change.GetNewValue<IReadOnlyList<AgentTaskItem>?>() is null;
            Observe();
            Rebuild();
        }
        else if (change.Property == AgentFilterProperty || change.Property == LabelsProperty)
        {
            Rebuild();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    private void Observe()
    {
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged -= OnTasksChanged;
            _observedCollection = null;
        }
        foreach (var task in _observed)
        {
            task.PropertyChanged -= OnTaskPropertyChanged;
        }
        _observed.Clear();
        if (!_attached)
        {
            return;
        }
        var tasks = ShownTasks;
        if (tasks is INotifyCollectionChanged notify)
        {
            _observedCollection = notify;
            notify.CollectionChanged += OnTasksChanged;
        }
        foreach (var task in tasks)
        {
            task.PropertyChanged += OnTaskPropertyChanged;
            _observed.Add(task);
        }
    }

    private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Observe();
        Rebuild();
    }

    private void OnTaskPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AgentTaskItem task)
        {
            return;
        }
        if (e.Property == AgentTaskItem.StatusProperty || e.Property == AgentTaskItem.AgentProperty)
        {
            var focused = _cards.TryGetValue(task, out var card) && card.IsFocused;
            Rebuild();
            if (e.Property == AgentTaskItem.StatusProperty)
            {
                _announcer?.Announce(Labels.Moved(task.Title, Labels.Status(task.Status)));
            }
            if (focused && card is not null && card.IsVisible)
            {
                Dispatcher.UIThread.Post(() => card.Focus(NavigationMethod.Directional), DispatcherPriority.Input);
            }
            return;
        }
        if (_cards.TryGetValue(task, out var existing))
        {
            existing.Refresh();
        }
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        if (compact == _isCompact)
        {
            return;
        }
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        LayoutColumns();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    #endregion

    #region Time and the sample work

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe();
        Rebuild();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1.2), DispatcherPriority.Background, (_, _) => Advance());
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _timer?.Stop();
        _timer = null;
        Observe();
    }

    private int _ticks;

    /// <summary>
    /// One beat of the board's clock: the relative times refresh, and the sample tasks that run move on a little (a
    /// finished one goes to Done and the next queued task of the same agent starts). Tests call it directly.
    /// </summary>
    internal void Advance()
    {
        _ticks++;
        if (IsSimulated)
        {
            var now = Clock();
            foreach (var task in ShownTasks.ToArray())
            {
                if (task.Status != AgentTaskStatus.Running)
                {
                    continue;
                }
                var progress = Math.Min(1, task.Progress + 0.015 + (task.Title.Length % 5) * 0.004);
                task.Updated = now;
                if (progress >= 1)
                {
                    task.Progress = 1;
                    task.Note = null;
                    task.Status = AgentTaskStatus.Done;
                    foreach (var next in ShownTasks)
                    {
                        if (next.Status == AgentTaskStatus.Queued && next.Agent == task.Agent)
                        {
                            next.Status = AgentTaskStatus.Running;
                            next.Updated = now;
                            break;
                        }
                    }
                }
                else
                {
                    task.Progress = progress;
                }
            }
        }
        if (_ticks % 25 == 0)
        {
            foreach (var card in _cards.Values)
            {
                card.Refresh();
            }
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentTasksAutomationPeer(this);

    private sealed class AgentTasksAutomationPeer(AgentTasks owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((AgentTasks)Owner).Labels.Title;
    }
}
