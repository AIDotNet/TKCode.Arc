using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The four levels of a <see cref="SemanticZoom"/> map, from the whole organization down to one task.</summary>
public enum SemanticZoomLevel
{
    /// <summary>The whole organization (fit view).</summary>
    Organization,

    /// <summary>One team panel.</summary>
    Team,

    /// <summary>One project card.</summary>
    Project,

    /// <summary>One task and its checklist.</summary>
    Task
}

/// <summary>How a project is doing (the colored dot on its card and in its hover card).</summary>
public enum SemanticZoomHealth
{
    /// <summary>On track (success color).</summary>
    OnTrack,

    /// <summary>At risk (warning color).</summary>
    AtRisk,

    /// <summary>Off track (danger color).</summary>
    OffTrack
}

/// <summary>Where a task stands. The status picks the chip shade at the overview and the icon when zoomed in.</summary>
public enum SemanticZoomTaskStatus
{
    /// <summary>Not started.</summary>
    Todo,

    /// <summary>Being worked on.</summary>
    InProgress,

    /// <summary>Waiting for a review.</summary>
    InReview,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Cannot move until something else happens (drawn in the danger color).</summary>
    Blocked
}

/// <summary>Where <see cref="SemanticZoom"/> puts its details panel.</summary>
public enum SemanticZoomPanelPlacement
{
    /// <summary>Beside the map when the control is wider than 900 px inside its border, otherwise below it.</summary>
    Auto,

    /// <summary>Always beside the map (264 px wide).</summary>
    Side,

    /// <summary>Always below the map.</summary>
    Bottom,

    /// <summary>No details panel.</summary>
    None
}

/// <summary>How <see cref="SemanticZoomPanel"/> arranges its numbers.</summary>
public enum SemanticZoomPanelLayout
{
    /// <summary>Two columns: the scope and progress on the left, the three counts on the right, people across the bottom.</summary>
    Grid,

    /// <summary>One column: scope, progress, counts, people (beside the map, or in narrow containers).</summary>
    Stack
}

/// <summary>A direction for the keyboard cursor (<see cref="SemanticZoom.MoveCursor"/>).</summary>
public enum SemanticZoomDirection
{
    /// <summary>To the left.</summary>
    Left,

    /// <summary>Upwards.</summary>
    Up,

    /// <summary>To the right.</summary>
    Right,

    /// <summary>Downwards.</summary>
    Down
}

/// <summary>
/// A person on a <see cref="SemanticZoom"/> map: a team or project lead, or a task's assignee or reviewer. Without an
/// <see cref="Avatar"/> the map draws a muted circle with the person's initials.
/// </summary>
public sealed class SemanticZoomPerson : AvaloniaObject
{
    /// <summary>The person's full name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<SemanticZoomPerson, string>(nameof(Name), string.Empty);

    /// <summary>Job title, shown under the name in a task's details.</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<SemanticZoomPerson, string?>(nameof(Role));

    /// <summary>A picture drawn in a circle; null draws the initials instead.</summary>
    public static readonly StyledProperty<IImage?> AvatarProperty =
        AvaloniaProperty.Register<SemanticZoomPerson, IImage?>(nameof(Avatar));

    /// <summary>Initials for the fallback circle; null takes the first letters of the first two words of <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<SemanticZoomPerson, string?>(nameof(Initials));

    public SemanticZoomPerson()
    {
    }

    public SemanticZoomPerson(string name, string? role = null)
    {
        Name = name;
        Role = role;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public IImage? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="InitialsProperty"/>
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    /// <summary>The initials drawn when there is no avatar.</summary>
    public string DisplayInitials => !string.IsNullOrWhiteSpace(Initials) ? Initials! : ComputeInitials(Name);

    /// <summary>The first word of the name ("Mara Ostrov" → "Mara").</summary>
    public string FirstName
    {
        get
        {
            var name = Name.Trim();
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }

    internal static string ComputeInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        Span<char> buffer = stackalloc char[2];
        var count = 0;
        foreach (var part in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            buffer[count++] = char.ToUpperInvariant(part[0]);
            if (count == 2)
            {
                break;
            }
        }
        return new string(buffer[..count]);
    }

    public override string ToString() => Name;
}

/// <summary>
/// One line of a task's checklist. Ticking every item marks the task done; unticking one of a done task puts it back in
/// review (<see cref="SemanticZoomTask"/>).
/// </summary>
public sealed class SemanticZoomChecklistItem : AvaloniaObject
{
    /// <summary>What has to be done.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SemanticZoomChecklistItem, string>(nameof(Text), string.Empty);

    /// <summary>The item is ticked (two-way).</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<SemanticZoomChecklistItem, bool>(nameof(IsDone), defaultBindingMode: BindingMode.TwoWay);

    public SemanticZoomChecklistItem()
    {
    }

    public SemanticZoomChecklistItem(string text, bool isDone = false)
    {
        Text = text;
        IsDone = isDone;
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>The task this item belongs to (set when it is added to <see cref="SemanticZoomTask.Checklist"/>).</summary>
    public SemanticZoomTask? Task { get; internal set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDoneProperty)
        {
            Task?.OnChecklistChanged(applyRule: true);
        }
        else if (change.Property == TextProperty)
        {
            Task?.NotifyValuesChanged();
        }
    }

    public override string ToString() => Text;
}

/// <summary>
/// Base of the four kinds of node on a <see cref="SemanticZoom"/> map. Every node knows its <see cref="Parent"/> and
/// <see cref="Children"/> and carries the rollups the panel and hover cards show (<see cref="OpenTasks"/>,
/// <see cref="BlockedTasks"/>, <see cref="NextDue"/>, <see cref="Progress"/>, <see cref="People"/>,
/// <see cref="NeedAttention"/>), recomputed whenever a task or checklist below it changes.
/// </summary>
public abstract class SemanticZoomNode : AvaloniaObject
{
    /// <summary>An optional identifier for your own bookkeeping.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<SemanticZoomNode, string?>(nameof(Key));

    /// <summary>The name shown on the map, in breadcrumbs and in the panel.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SemanticZoomNode, string>(nameof(Title), string.Empty);

    /// <summary>A one-line description (teams: shown in the hover card; projects: used in generated task summaries).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SemanticZoomNode, string?>(nameof(Description));

    /// <summary>The node above this one (null for the organization or a node not added anywhere yet).</summary>
    public static readonly DirectProperty<SemanticZoomNode, SemanticZoomNode?> ParentProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, SemanticZoomNode?>(nameof(Parent), o => o.Parent);

    /// <summary>Tasks below this node (a task counts itself).</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> TaskCountProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(TaskCount), o => o.TaskCount);

    /// <summary>Tasks below this node whose status is not done.</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> OpenTasksProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(OpenTasks), o => o.OpenTasks);

    /// <summary>Tasks below this node that are done.</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> DoneTasksProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(DoneTasks), o => o.DoneTasks);

    /// <summary>Tasks below this node that are blocked.</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> BlockedTasksProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(BlockedTasks), o => o.BlockedTasks);

    /// <summary>The earliest due date among the open tasks below this node (null when none is due).</summary>
    public static readonly DirectProperty<SemanticZoomNode, DateOnly?> NextDueProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, DateOnly?>(nameof(NextDue), o => o.NextDue);

    /// <summary>
    /// Completion from 0 to 1: a task's share of ticked checklist items (1 when done); a project, team or the organization
    /// averages every task below it.
    /// </summary>
    public static readonly DirectProperty<SemanticZoomNode, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, double>(nameof(Progress), o => o.Progress);

    /// <summary>Everyone involved, without repeats: leads first, then the assignees of the tasks below.</summary>
    public static readonly DirectProperty<SemanticZoomNode, IReadOnlyList<SemanticZoomPerson>> PeopleProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, IReadOnlyList<SemanticZoomPerson>>(nameof(People), o => o.People);

    /// <summary>Projects below this node that are not on track (teams and the organization).</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> NeedAttentionProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(NeedAttention), o => o.NeedAttention);

    /// <summary>Projects below this node (a project counts itself).</summary>
    public static readonly DirectProperty<SemanticZoomNode, int> ProjectCountProperty =
        AvaloniaProperty.RegisterDirect<SemanticZoomNode, int>(nameof(ProjectCount), o => o.ProjectCount);

    private static readonly IReadOnlyList<SemanticZoomPerson> s_nobody = Array.Empty<SemanticZoomPerson>();

    private SemanticZoomNode? _parent;
    private int _taskCount;
    private int _openTasks;
    private int _doneTasks;
    private int _blockedTasks;
    private DateOnly? _nextDue;
    private double _progress;
    private IReadOnlyList<SemanticZoomPerson> _people = s_nobody;
    private int _needAttention;
    private int _projectCount;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Which level this node is.</summary>
    public abstract SemanticZoomLevel Kind { get; }

    /// <summary>The nodes one level down, in order.</summary>
    public abstract IReadOnlyList<SemanticZoomNode> Children { get; }

    /// <inheritdoc cref="ParentProperty"/>
    public SemanticZoomNode? Parent
    {
        get => _parent;
        internal set => SetAndRaise(ParentProperty, ref _parent, value);
    }

    /// <inheritdoc cref="TaskCountProperty"/>
    public int TaskCount { get => _taskCount; private set => SetAndRaise(TaskCountProperty, ref _taskCount, value); }

    /// <inheritdoc cref="OpenTasksProperty"/>
    public int OpenTasks { get => _openTasks; private set => SetAndRaise(OpenTasksProperty, ref _openTasks, value); }

    /// <inheritdoc cref="DoneTasksProperty"/>
    public int DoneTasks { get => _doneTasks; private set => SetAndRaise(DoneTasksProperty, ref _doneTasks, value); }

    /// <inheritdoc cref="BlockedTasksProperty"/>
    public int BlockedTasks { get => _blockedTasks; private set => SetAndRaise(BlockedTasksProperty, ref _blockedTasks, value); }

    /// <inheritdoc cref="NextDueProperty"/>
    public DateOnly? NextDue { get => _nextDue; private set => SetAndRaise(NextDueProperty, ref _nextDue, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => _progress; private set => SetAndRaise(ProgressProperty, ref _progress, value); }

    /// <inheritdoc cref="PeopleProperty"/>
    public IReadOnlyList<SemanticZoomPerson> People
    {
        get => _people;
        private set => SetAndRaise(PeopleProperty, ref _people, value);
    }

    /// <inheritdoc cref="NeedAttentionProperty"/>
    public int NeedAttention { get => _needAttention; private set => SetAndRaise(NeedAttentionProperty, ref _needAttention, value); }

    /// <inheritdoc cref="ProjectCountProperty"/>
    public int ProjectCount { get => _projectCount; private set => SetAndRaise(ProjectCountProperty, ref _projectCount, value); }

    /// <summary>The organization at the top of this node's chain (null while the node is not attached to one).</summary>
    public SemanticZoomOrganization? Organization
    {
        get
        {
            var node = this;
            while (node.Parent is not null)
            {
                node = node.Parent;
            }
            return node as SemanticZoomOrganization;
        }
    }

    /// <summary>The chain from the organization down to this node.</summary>
    public IReadOnlyList<SemanticZoomNode> GetPath()
    {
        var path = new List<SemanticZoomNode>(4);
        for (var node = this; node is not null; node = node.Parent)
        {
            path.Add(node);
        }
        path.Reverse();
        return path;
    }

    /// <summary>True when <paramref name="node"/> is this node or one of its ancestors.</summary>
    public bool IsSelfOrDescendantOf(SemanticZoomNode? node)
    {
        for (var n = this; n is not null; n = n.Parent)
        {
            if (ReferenceEquals(n, node))
            {
                return true;
            }
        }
        return false;
    }

    // Rollups ---------------------------------------------------------------------------------------------------------

    internal double ProgressSum { get; private set; }

    /// <summary>Recomputes this node's rollups from its children (or, for a task, from itself).</summary>
    internal void Recompute()
    {
        if (this is SemanticZoomTask task)
        {
            var done = task.Status == SemanticZoomTaskStatus.Done;
            var progress = task.TaskProgress;
            TaskCount = 1;
            DoneTasks = done ? 1 : 0;
            OpenTasks = done ? 0 : 1;
            BlockedTasks = task.Status == SemanticZoomTaskStatus.Blocked ? 1 : 0;
            NextDue = done ? null : task.Due;
            ProgressSum = progress;
            Progress = progress;
            ProjectCount = 0;
            NeedAttention = 0;
            SetPeople(task.Assignee is { } assignee ? [assignee] : s_nobody);
            return;
        }
        int count = 0, doneCount = 0, blocked = 0, attention = 0, projects = 0;
        double sum = 0;
        DateOnly? next = null;
        var people = new List<SemanticZoomPerson>();
        var lead = this switch
        {
            SemanticZoomTeam team => team.Lead,
            SemanticZoomProject project => project.Lead,
            _ => null
        };
        if (lead is not null)
        {
            people.Add(lead);
        }
        foreach (var child in Children)
        {
            count += child.TaskCount;
            doneCount += child.DoneTasks;
            blocked += child.BlockedTasks;
            sum += child.ProgressSum;
            attention += child.NeedAttention;
            projects += child.ProjectCount;
            if (child.NextDue is { } due && (next is null || due < next))
            {
                next = due;
            }
            foreach (var person in child.People)
            {
                if (!people.Contains(person))
                {
                    people.Add(person);
                }
            }
        }
        if (this is SemanticZoomProject self)
        {
            projects = 1;
            attention = self.Health == SemanticZoomHealth.OnTrack ? 0 : 1;
        }
        TaskCount = count;
        DoneTasks = doneCount;
        OpenTasks = count - doneCount;
        BlockedTasks = blocked;
        NextDue = next;
        ProgressSum = sum;
        Progress = count > 0 ? sum / count : 0;
        NeedAttention = attention;
        ProjectCount = projects;
        SetPeople(people);
    }

    private void SetPeople(IReadOnlyList<SemanticZoomPerson> people)
    {
        var current = _people;
        if (current.Count == people.Count)
        {
            var same = true;
            for (var i = 0; i < people.Count; i++)
            {
                if (!ReferenceEquals(current[i], people[i]))
                {
                    same = false;
                    break;
                }
            }
            if (same)
            {
                return;
            }
        }
        People = people.Count == 0 ? s_nobody : people;
    }

    /// <summary>Recomputes this node and every ancestor, then tells the organization that values changed.</summary>
    internal void RecomputeUpwards()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            node.Recompute();
        }
        NotifyValuesChanged();
    }

    /// <summary>A shown value (title, status, rollup) changed: the organization tells its map.</summary>
    internal void NotifyValuesChanged() => Organization?.RaiseValuesChanged(this);

    /// <summary>Nodes were added or removed below this node: rollups are recomputed and the map lays itself out again.</summary>
    internal void NotifyStructureChanged()
    {
        Recompute();
        for (var node = Parent; node is not null; node = node.Parent)
        {
            node.Recompute();
        }
        Organization?.RaiseStructureChanged();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            NotifyValuesChanged();
        }
    }

    /// <summary>Keeps <see cref="Parent"/> and the rollups in step with a child collection.</summary>
    internal static void Track<T>(SemanticZoomNode owner, AvaloniaList<T> list) where T : SemanticZoomNode
    {
        list.ResetBehavior = ResetBehavior.Remove;
        list.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (T item in e.OldItems)
                {
                    if (ReferenceEquals(item.Parent, owner))
                    {
                        item.Parent = null;
                    }
                }
            }
            if (e.NewItems is not null)
            {
                foreach (T item in e.NewItems)
                {
                    item.Parent = owner;
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in list)
                {
                    item.Parent = owner;
                }
            }
            owner.NotifyStructureChanged();
        };
    }

    public override string ToString() => Title;
}

/// <summary>The root of a <see cref="SemanticZoom"/> map: the organization and its teams.</summary>
public sealed class SemanticZoomOrganization : SemanticZoomNode
{
    public SemanticZoomOrganization()
    {
        Track(this, Teams);
    }

    /// <summary>The teams, in the order they are laid out (left to right, top to bottom).</summary>
    [Content]
    public AvaloniaList<SemanticZoomTeam> Teams { get; } = new();

    /// <inheritdoc />
    public override SemanticZoomLevel Kind => SemanticZoomLevel.Organization;

    /// <inheritdoc />
    public override IReadOnlyList<SemanticZoomNode> Children => Teams;

    /// <summary>Every project, team by team.</summary>
    public IEnumerable<SemanticZoomProject> AllProjects => Teams.SelectMany(t => t.Projects);

    /// <summary>Every task, project by project.</summary>
    public IEnumerable<SemanticZoomTask> AllTasks => Teams.SelectMany(t => t.Projects).SelectMany(p => p.Tasks);

    /// <summary>Raised after teams, projects, tasks or checklist items were added or removed anywhere in the map.</summary>
    internal event Action? StructureChanged;

    /// <summary>Raised after a shown value (a title, a status, a tick or a rollup) changed somewhere in the map.</summary>
    internal event Action<SemanticZoomNode>? ValuesChanged;

    /// <summary>Counts every structure and value change, so a map that was away (detached) can tell whether to rebuild.</summary>
    internal int Revision { get; private set; }

    internal void RaiseStructureChanged()
    {
        Revision++;
        StructureChanged?.Invoke();
    }

    internal void RaiseValuesChanged(SemanticZoomNode node)
    {
        Revision++;
        ValuesChanged?.Invoke(node);
    }
}

/// <summary>A team: a panel of project cards with a lead.</summary>
public sealed class SemanticZoomTeam : SemanticZoomNode
{
    /// <summary>The team lead, shown in the team's header and hover card.</summary>
    public static readonly StyledProperty<SemanticZoomPerson?> LeadProperty =
        AvaloniaProperty.Register<SemanticZoomTeam, SemanticZoomPerson?>(nameof(Lead));

    public SemanticZoomTeam()
    {
        Track(this, Projects);
    }

    /// <inheritdoc cref="LeadProperty"/>
    public SemanticZoomPerson? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <summary>The team's projects, laid out row by row.</summary>
    [Content]
    public AvaloniaList<SemanticZoomProject> Projects { get; } = new();

    /// <inheritdoc />
    public override SemanticZoomLevel Kind => SemanticZoomLevel.Team;

    /// <inheritdoc />
    public override IReadOnlyList<SemanticZoomNode> Children => Projects;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeadProperty)
        {
            RecomputeUpwards();
        }
    }
}

/// <summary>A project: a card of task blocks with a lead, a health and a due date.</summary>
public sealed class SemanticZoomProject : SemanticZoomNode
{
    /// <summary>The project lead.</summary>
    public static readonly StyledProperty<SemanticZoomPerson?> LeadProperty =
        AvaloniaProperty.Register<SemanticZoomProject, SemanticZoomPerson?>(nameof(Lead));

    /// <summary>How the project is doing (the dot beside its status line).</summary>
    public static readonly StyledProperty<SemanticZoomHealth> HealthProperty =
        AvaloniaProperty.Register<SemanticZoomProject, SemanticZoomHealth>(nameof(Health));

    /// <summary>When the project is due (shown on its card and in its hover card).</summary>
    public static readonly StyledProperty<DateOnly?> DueProperty =
        AvaloniaProperty.Register<SemanticZoomProject, DateOnly?>(nameof(Due));

    public SemanticZoomProject()
    {
        Track(this, Tasks);
    }

    /// <inheritdoc cref="LeadProperty"/>
    public SemanticZoomPerson? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <inheritdoc cref="HealthProperty"/>
    public SemanticZoomHealth Health { get => GetValue(HealthProperty); set => SetValue(HealthProperty, value); }

    /// <inheritdoc cref="DueProperty"/>
    public DateOnly? Due { get => GetValue(DueProperty); set => SetValue(DueProperty, value); }

    /// <summary>The project's tasks, three to a row.</summary>
    [Content]
    public AvaloniaList<SemanticZoomTask> Tasks { get; } = new();

    /// <inheritdoc />
    public override SemanticZoomLevel Kind => SemanticZoomLevel.Project;

    /// <inheritdoc />
    public override IReadOnlyList<SemanticZoomNode> Children => Tasks;

    /// <summary>The team this project belongs to.</summary>
    public SemanticZoomTeam? Team => Parent as SemanticZoomTeam;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeadProperty || change.Property == HealthProperty)
        {
            RecomputeUpwards();
        }
        else if (change.Property == DueProperty)
        {
            NotifyValuesChanged();
        }
    }
}

/// <summary>
/// A task: a block on its project's card, and when zoomed in, a card with its status, summary, assignee and checklist.
/// Ticking the last open checklist item marks the task <see cref="SemanticZoomTaskStatus.Done"/>; unticking an item of a
/// done task puts it back <see cref="SemanticZoomTaskStatus.InReview"/>. Set <see cref="Status"/> yourself at any time.
/// </summary>
public sealed class SemanticZoomTask : SemanticZoomNode
{
    /// <summary>Where the task stands.</summary>
    public static readonly StyledProperty<SemanticZoomTaskStatus> StatusProperty =
        AvaloniaProperty.Register<SemanticZoomTask, SemanticZoomTaskStatus>(nameof(Status), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Who is doing it.</summary>
    public static readonly StyledProperty<SemanticZoomPerson?> AssigneeProperty =
        AvaloniaProperty.Register<SemanticZoomTask, SemanticZoomPerson?>(nameof(Assignee));

    /// <summary>Who reviews it (used in the generated summary of a task in review; null uses the project lead).</summary>
    public static readonly StyledProperty<SemanticZoomPerson?> ReviewerProperty =
        AvaloniaProperty.Register<SemanticZoomTask, SemanticZoomPerson?>(nameof(Reviewer));

    /// <summary>When it is due.</summary>
    public static readonly StyledProperty<DateOnly?> DueProperty =
        AvaloniaProperty.Register<SemanticZoomTask, DateOnly?>(nameof(Due));

    /// <summary>The estimate in points.</summary>
    public static readonly StyledProperty<int> PointsProperty =
        AvaloniaProperty.Register<SemanticZoomTask, int>(nameof(Points));

    /// <summary>How many comments it has.</summary>
    public static readonly StyledProperty<int> CommentCountProperty =
        AvaloniaProperty.Register<SemanticZoomTask, int>(nameof(CommentCount));

    /// <summary>The description in the task's details; null generates one from the status (<see cref="SemanticZoomTexts"/>).</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<SemanticZoomTask, string?>(nameof(Summary));

    public SemanticZoomTask()
    {
        Checklist.ResetBehavior = ResetBehavior.Remove;
        Checklist.CollectionChanged += OnChecklistCollectionChanged;
        Recompute();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public SemanticZoomTaskStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="AssigneeProperty"/>
    public SemanticZoomPerson? Assignee { get => GetValue(AssigneeProperty); set => SetValue(AssigneeProperty, value); }

    /// <inheritdoc cref="ReviewerProperty"/>
    public SemanticZoomPerson? Reviewer { get => GetValue(ReviewerProperty); set => SetValue(ReviewerProperty, value); }

    /// <inheritdoc cref="DueProperty"/>
    public DateOnly? Due { get => GetValue(DueProperty); set => SetValue(DueProperty, value); }

    /// <inheritdoc cref="PointsProperty"/>
    public int Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <inheritdoc cref="CommentCountProperty"/>
    public int CommentCount { get => GetValue(CommentCountProperty); set => SetValue(CommentCountProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>The checklist, top to bottom.</summary>
    [Content]
    public AvaloniaList<SemanticZoomChecklistItem> Checklist { get; } = new();

    /// <inheritdoc />
    public override SemanticZoomLevel Kind => SemanticZoomLevel.Task;

    /// <inheritdoc />
    public override IReadOnlyList<SemanticZoomNode> Children => Array.Empty<SemanticZoomNode>();

    /// <summary>The project this task belongs to.</summary>
    public SemanticZoomProject? Project => Parent as SemanticZoomProject;

    /// <summary>Ticked checklist items.</summary>
    public int ChecklistDone
    {
        get
        {
            var done = 0;
            foreach (var item in Checklist)
            {
                if (item.IsDone)
                {
                    done++;
                }
            }
            return done;
        }
    }

    /// <summary>The task's own completion: 1 when done, otherwise the share of ticked checklist items.</summary>
    internal double TaskProgress =>
        Status == SemanticZoomTaskStatus.Done ? 1 : Checklist.Count == 0 ? 0 : ChecklistDone / (double)Checklist.Count;

    private void OnChecklistCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SemanticZoomChecklistItem item in e.OldItems)
            {
                if (ReferenceEquals(item.Task, this))
                {
                    item.Task = null;
                }
            }
        }
        if (e.NewItems is not null)
        {
            foreach (SemanticZoomChecklistItem item in e.NewItems)
            {
                item.Task = this;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in Checklist)
            {
                item.Task = this;
            }
        }
        OnChecklistChanged(applyRule: false);
        Organization?.RaiseStructureChanged();
    }

    /// <summary>
    /// A checklist item was ticked or unticked. With <paramref name="applyRule"/>: every item ticked → done; an item of a
    /// done task unticked → back in review.
    /// </summary>
    internal void OnChecklistChanged(bool applyRule)
    {
        if (applyRule && Checklist.Count > 0)
        {
            var done = ChecklistDone;
            if (done == Checklist.Count)
            {
                Status = SemanticZoomTaskStatus.Done;
            }
            else if (Status == SemanticZoomTaskStatus.Done)
            {
                Status = SemanticZoomTaskStatus.InReview;
            }
        }
        RecomputeUpwards();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty || change.Property == AssigneeProperty || change.Property == DueProperty)
        {
            RecomputeUpwards();
        }
        else if (change.Property == PointsProperty || change.Property == CommentCountProperty ||
                 change.Property == SummaryProperty || change.Property == ReviewerProperty)
        {
            NotifyValuesChanged();
        }
    }
}
