using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A compact sprint board: a few stages as columns, each task a card that the arrow buttons send one stage left or right,
/// a sprint line that fills as tasks reach the finished stage, a team row, an All work / My work switch and an Add button.
/// A moved card flies in a straight line to the end of its new column while the old column closes up; filtered cards
/// fade out in place as the others close the gap. Everything works with Tab, Enter and Space, and screen readers hear
/// each move. Sized for tens of tasks; for hundreds, drag and drop and virtualization, use a full kanban board.
/// </summary>
[TemplatePart(PanelPart, typeof(ProjectBoardPanel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(FilterPart, typeof(ProjectBoardFilterBar))]
[TemplatePart(AddPart, typeof(ProjectBoardButton))]
[TemplatePart(TeamPart, typeof(ProjectsAvatarStack))]
[TemplatePart(ProgressPart, typeof(ProjectsProgressLine))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(HeaderPart, typeof(ProjectBoardHeaderPanel))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, ScrollingPseudoClass, FilteredPseudoClass, FramedPseudoClass)]
public partial class ProjectBoard : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string ScrollPart = "PART_Scroll";
    public const string FilterPart = "PART_Filter";
    public const string AddPart = "PART_Add";
    public const string TeamPart = "PART_Team";
    public const string ProgressPart = "PART_Progress";
    public const string SubtitlePart = "PART_Subtitle";
    public const string HeaderPart = "PART_Header";
    public const string AnnouncerPart = "PART_Announcer";
    public const string NarrowPseudoClass = ":narrow";
    public const string CompactPseudoClass = ":compact";
    public const string ScrollingPseudoClass = ":scrolling";
    public const string FilteredPseudoClass = ":filtered";
    public const string FramedPseudoClass = ":framed";

    /// <summary>The stages, left to right (2 to 6 read well).</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectBoardStage>> StagesProperty =
        AvaloniaProperty.Register<ProjectBoard, AvaloniaList<ProjectBoardStage>>(nameof(Stages));

    /// <summary>The tasks; each column shows its stage's tasks in this order. <see cref="TasksSource"/> takes precedence when set.</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectBoardTask>> TasksProperty =
        AvaloniaProperty.Register<ProjectBoard, AvaloniaList<ProjectBoardTask>>(nameof(Tasks));

    /// <summary>
    /// A collection of <see cref="ProjectBoardTask"/>s (observed when it raises collection changes) that replaces
    /// <see cref="Tasks"/>. A writable list is reordered by moves (a moved task goes to the end of its new column) and
    /// receives added tasks; a read-only one only has its tasks' stages changed.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> TasksSourceProperty =
        AvaloniaProperty.Register<ProjectBoard, IEnumerable?>(nameof(TasksSource));

    /// <summary>The people shown as overlapping avatars in the header (hidden when empty and on narrow boards).</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectPerson>> TeamProperty =
        AvaloniaProperty.Register<ProjectBoard, AvaloniaList<ProjectPerson>>(nameof(Team));

    /// <summary>The person using the board: "My work" shows the tasks they own, drafts are theirs. Null hides the switch.</summary>
    public static readonly StyledProperty<ProjectPerson?> CurrentUserProperty =
        AvaloniaProperty.Register<ProjectBoard, ProjectPerson?>(nameof(CurrentUser));

    /// <summary>Which cards are shown: every task or the current user's.</summary>
    public static readonly StyledProperty<ProjectBoardFilter> FilterProperty =
        AvaloniaProperty.Register<ProjectBoard, ProjectBoardFilter>(nameof(Filter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The heading in the header; null leaves the heading out (the progress sentence stays).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ProjectBoard, string?>(nameof(Title));

    /// <summary>Shows the progress sentence and the sprint line (the line also divides the header from the columns).</summary>
    public static readonly StyledProperty<bool> IsProgressVisibleProperty =
        AvaloniaProperty.Register<ProjectBoard, bool>(nameof(IsProgressVisible), true);

    /// <summary>The progress sentence under the heading: {0} finished tasks, {1} all tasks (the filter never changes them).</summary>
    public static readonly StyledProperty<string> ProgressFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(ProgressFormat), "{0} of {1} shipped this sprint");

    /// <summary>The label of the button that shows every task.</summary>
    public static readonly StyledProperty<string> AllWorkTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(AllWorkText), "All work");

    /// <summary>The label of the button that shows the current user's tasks.</summary>
    public static readonly StyledProperty<string> MyWorkTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(MyWorkText), "My work");

    /// <summary>The screen reader name of the filter switch.</summary>
    public static readonly StyledProperty<string> FilterLabelProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(FilterLabel), "Show");

    /// <summary>The screen reader name of the team avatars.</summary>
    public static readonly StyledProperty<string> TeamLabelProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(TeamLabel), "Team");

    /// <summary>The Add button's label (its name and tooltip when only the icon shows).</summary>
    public static readonly StyledProperty<string> AddTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(AddText), "Add task");

    /// <summary>Shows the Add button.</summary>
    public static readonly StyledProperty<bool> IsAddVisibleProperty =
        AvaloniaProperty.Register<ProjectBoard, bool>(nameof(IsAddVisible), true);

    /// <summary>The stage new tasks go to; null or unknown uses the first stage.</summary>
    public static readonly StyledProperty<string?> AddStageKeyProperty =
        AvaloniaProperty.Register<ProjectBoard, string?>(nameof(AddStageKey));

    /// <summary>Titles the Add button cycles through for drafts when no <see cref="AddRequested"/> handler supplies a task.</summary>
    public static readonly StyledProperty<AvaloniaList<string>> DraftTitlesProperty =
        AvaloniaProperty.Register<ProjectBoard, AvaloniaList<string>>(nameof(DraftTitles));

    /// <summary>The title of a draft when <see cref="DraftTitles"/> is empty.</summary>
    public static readonly StyledProperty<string> NewTaskTitleProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(NewTaskTitle), "Untitled task");

    /// <summary>The area label of drafts; null shows none.</summary>
    public static readonly StyledProperty<string?> DraftAreaProperty =
        AvaloniaProperty.Register<ProjectBoard, string?>(nameof(DraftArea));

    /// <summary>The due label of drafts; null shows none.</summary>
    public static readonly StyledProperty<string?> NoDateTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string?>(nameof(NoDateText), "No date");

    /// <summary>Shown in an empty column while every task is shown (a stage's own <see cref="ProjectBoardStage.EmptyText"/> wins).</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(EmptyText), "No tasks yet");

    /// <summary>Shown in an empty column while only the current user's tasks are shown.</summary>
    public static readonly StyledProperty<string> EmptyMineTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(EmptyMineText), "Nothing assigned to you");

    /// <summary>A column's screen reader name: {0} the stage, {1} the number of cards shown.</summary>
    public static readonly StyledProperty<string> ColumnLabelFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(ColumnLabelFormat), "{0}, {1} tasks");

    /// <summary>A column's screen reader name when it shows one card: {0} the stage.</summary>
    public static readonly StyledProperty<string> ColumnLabelSingularFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(ColumnLabelSingularFormat), "{0}, 1 task");

    /// <summary>An arrow button's name: {0} the task, {1} the stage it sends the task to.</summary>
    public static readonly StyledProperty<string> MoveToFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(MoveToFormat), "Move {0} to {1}");

    /// <summary>Put in <see cref="MoveToFormat"/> for the left arrow of a task in the first stage (the button is disabled).</summary>
    public static readonly StyledProperty<string> MovePreviousTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(MovePreviousText), "the previous stage");

    /// <summary>Put in <see cref="MoveToFormat"/> for the right arrow of a task in the last stage (the button is disabled).</summary>
    public static readonly StyledProperty<string> MoveNextTextProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(MoveNextText), "the next stage");

    /// <summary>Announced after a move: {0} the task, {1} its new stage.</summary>
    public static readonly StyledProperty<string> MovedFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(MovedFormat), "Moved {0} to {1}.");

    /// <summary>Announced after an add: {0} the task, {1} its stage.</summary>
    public static readonly StyledProperty<string> AddedFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(AddedFormat), "Added {0} to {1}.");

    /// <summary>The due part of a card's screen reader name: {0} the due label.</summary>
    public static readonly StyledProperty<string> DueLabelFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(DueLabelFormat), "due {0}");

    /// <summary>The owner part of a card's screen reader name: {0} the owner's name.</summary>
    public static readonly StyledProperty<string> OwnerLabelFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(OwnerLabelFormat), "assigned to {0}");

    /// <summary>The comments part of a card's screen reader name: {0} the count.</summary>
    public static readonly StyledProperty<string> CommentsFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(CommentsFormat), "{0} comments");

    /// <summary>The comments part of a card's screen reader name for one comment.</summary>
    public static readonly StyledProperty<string> CommentsSingularFormatProperty =
        AvaloniaProperty.Register<ProjectBoard, string>(nameof(CommentsSingularFormat), "1 comment");

    /// <summary>Draws the board as a bordered, rounded card (true); false fills its space flat (a page of its own).</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<ProjectBoard, bool>(nameof(IsFramed));

    /// <summary>The narrowest column of the wide layout (200); narrower boards scroll sideways.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ProjectBoard, double>(nameof(MinColumnWidth), 200);

    /// <summary>At this width and below the board turns narrow: touch-sized buttons, no team row, one column and a peek.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ProjectBoard, double>(nameof(NarrowWidth), 700);

    /// <summary>At this width and below the actions take a row of their own and Add shows only its icon.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ProjectBoard, double>(nameof(CompactWidth), 400);

    /// <summary>False makes moves, adds and filters change in place at once, as with reduced motion.</summary>
    public static readonly StyledProperty<bool> AnimatesMovesProperty =
        AvaloniaProperty.Register<ProjectBoard, bool>(nameof(AnimatesMoves), true);

    /// <summary>Runs after a card's arrow button moved its task (parameter: a <see cref="ProjectBoardMoveRequest"/>).</summary>
    public static readonly StyledProperty<ICommand?> MoveCommandProperty =
        AvaloniaProperty.Register<ProjectBoard, ICommand?>(nameof(MoveCommand));

    /// <summary>Runs after the board added a task (parameter: the new <see cref="ProjectBoardTask"/>).</summary>
    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<ProjectBoard, ICommand?>(nameof(AddCommand));

    /// <summary>The number of tasks in the finished stage (every task, whatever the filter).</summary>
    public static readonly DirectProperty<ProjectBoard, int> DoneCountProperty =
        AvaloniaProperty.RegisterDirect<ProjectBoard, int>(nameof(DoneCount), o => o.DoneCount);

    /// <summary>The number of tasks on the board (every task, whatever the filter).</summary>
    public static readonly DirectProperty<ProjectBoard, int> TaskCountProperty =
        AvaloniaProperty.RegisterDirect<ProjectBoard, int>(nameof(TaskCount), o => o.TaskCount);

    /// <summary>Raised before a task changes stage; set <see cref="ProjectBoardTaskMovingEventArgs.Cancel"/> to keep it.</summary>
    public static readonly RoutedEvent<ProjectBoardTaskMovingEventArgs> TaskMovingEvent =
        RoutedEvent.Register<ProjectBoard, ProjectBoardTaskMovingEventArgs>(nameof(TaskMoving), RoutingStrategies.Bubble);

    /// <summary>Raised when a task changed stage, as its card starts to fly (save the move here).</summary>
    public static readonly RoutedEvent<ProjectBoardTaskMovedEventArgs> TaskMovedEvent =
        RoutedEvent.Register<ProjectBoard, ProjectBoardTaskMovedEventArgs>(nameof(TaskMoved), RoutingStrategies.Bubble);

    /// <summary>Raised when the Add button is pressed: supply the task, add nothing, or let the board add a draft.</summary>
    public static readonly RoutedEvent<ProjectBoardAddRequestedEventArgs> AddRequestedEvent =
        RoutedEvent.Register<ProjectBoard, ProjectBoardAddRequestedEventArgs>(nameof(AddRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after the board added a task (the Add button or <see cref="AddTask(ProjectBoardTask, string?)"/>).</summary>
    public static readonly RoutedEvent<ProjectBoardTaskEventArgs> TaskAddedEvent =
        RoutedEvent.Register<ProjectBoard, ProjectBoardTaskEventArgs>(nameof(TaskAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Filter"/> changes.</summary>
    public static readonly RoutedEvent<ProjectBoardFilterChangedEventArgs> FilterChangedEvent =
        RoutedEvent.Register<ProjectBoard, ProjectBoardFilterChangedEventArgs>(nameof(FilterChanged), RoutingStrategies.Bubble);

    private int _doneCount;
    private int _taskCount;

    static ProjectBoard()
    {
        FocusableProperty.OverrideDefaultValue<ProjectBoard>(false);
    }

    public ProjectBoard()
    {
        SetCurrentValue(StagesProperty, new AvaloniaList<ProjectBoardStage>());
        SetCurrentValue(TasksProperty, new AvaloniaList<ProjectBoardTask>());
        SetCurrentValue(TeamProperty, new AvaloniaList<ProjectPerson>());
        SetCurrentValue(DraftTitlesProperty, new AvaloniaList<string>());
        SetCurrentValue(AutomationProperties.NameProperty, "Project board");
    }

    /// <inheritdoc cref="StagesProperty"/>
    public AvaloniaList<ProjectBoardStage> Stages { get => GetValue(StagesProperty); set => SetValue(StagesProperty, value); }

    /// <inheritdoc cref="TasksProperty"/>
    [Content]
    public AvaloniaList<ProjectBoardTask> Tasks { get => GetValue(TasksProperty); set => SetValue(TasksProperty, value); }

    /// <inheritdoc cref="TasksSourceProperty"/>
    public IEnumerable? TasksSource { get => GetValue(TasksSourceProperty); set => SetValue(TasksSourceProperty, value); }

    /// <inheritdoc cref="TeamProperty"/>
    public AvaloniaList<ProjectPerson> Team { get => GetValue(TeamProperty); set => SetValue(TeamProperty, value); }

    /// <inheritdoc cref="CurrentUserProperty"/>
    public ProjectPerson? CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <inheritdoc cref="FilterProperty"/>
    public ProjectBoardFilter Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IsProgressVisibleProperty"/>
    public bool IsProgressVisible { get => GetValue(IsProgressVisibleProperty); set => SetValue(IsProgressVisibleProperty, value); }

    /// <inheritdoc cref="ProgressFormatProperty"/>
    public string ProgressFormat { get => GetValue(ProgressFormatProperty); set => SetValue(ProgressFormatProperty, value); }

    /// <inheritdoc cref="AllWorkTextProperty"/>
    public string AllWorkText { get => GetValue(AllWorkTextProperty); set => SetValue(AllWorkTextProperty, value); }

    /// <inheritdoc cref="MyWorkTextProperty"/>
    public string MyWorkText { get => GetValue(MyWorkTextProperty); set => SetValue(MyWorkTextProperty, value); }

    /// <inheritdoc cref="FilterLabelProperty"/>
    public string FilterLabel { get => GetValue(FilterLabelProperty); set => SetValue(FilterLabelProperty, value); }

    /// <inheritdoc cref="TeamLabelProperty"/>
    public string TeamLabel { get => GetValue(TeamLabelProperty); set => SetValue(TeamLabelProperty, value); }

    /// <inheritdoc cref="AddTextProperty"/>
    public string AddText { get => GetValue(AddTextProperty); set => SetValue(AddTextProperty, value); }

    /// <inheritdoc cref="IsAddVisibleProperty"/>
    public bool IsAddVisible { get => GetValue(IsAddVisibleProperty); set => SetValue(IsAddVisibleProperty, value); }

    /// <inheritdoc cref="AddStageKeyProperty"/>
    public string? AddStageKey { get => GetValue(AddStageKeyProperty); set => SetValue(AddStageKeyProperty, value); }

    /// <inheritdoc cref="DraftTitlesProperty"/>
    public AvaloniaList<string> DraftTitles { get => GetValue(DraftTitlesProperty); set => SetValue(DraftTitlesProperty, value); }

    /// <inheritdoc cref="NewTaskTitleProperty"/>
    public string NewTaskTitle { get => GetValue(NewTaskTitleProperty); set => SetValue(NewTaskTitleProperty, value); }

    /// <inheritdoc cref="DraftAreaProperty"/>
    public string? DraftArea { get => GetValue(DraftAreaProperty); set => SetValue(DraftAreaProperty, value); }

    /// <inheritdoc cref="NoDateTextProperty"/>
    public string? NoDateText { get => GetValue(NoDateTextProperty); set => SetValue(NoDateTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="EmptyMineTextProperty"/>
    public string EmptyMineText { get => GetValue(EmptyMineTextProperty); set => SetValue(EmptyMineTextProperty, value); }

    /// <inheritdoc cref="ColumnLabelFormatProperty"/>
    public string ColumnLabelFormat { get => GetValue(ColumnLabelFormatProperty); set => SetValue(ColumnLabelFormatProperty, value); }

    /// <inheritdoc cref="ColumnLabelSingularFormatProperty"/>
    public string ColumnLabelSingularFormat { get => GetValue(ColumnLabelSingularFormatProperty); set => SetValue(ColumnLabelSingularFormatProperty, value); }

    /// <inheritdoc cref="MoveToFormatProperty"/>
    public string MoveToFormat { get => GetValue(MoveToFormatProperty); set => SetValue(MoveToFormatProperty, value); }

    /// <inheritdoc cref="MovePreviousTextProperty"/>
    public string MovePreviousText { get => GetValue(MovePreviousTextProperty); set => SetValue(MovePreviousTextProperty, value); }

    /// <inheritdoc cref="MoveNextTextProperty"/>
    public string MoveNextText { get => GetValue(MoveNextTextProperty); set => SetValue(MoveNextTextProperty, value); }

    /// <inheritdoc cref="MovedFormatProperty"/>
    public string MovedFormat { get => GetValue(MovedFormatProperty); set => SetValue(MovedFormatProperty, value); }

    /// <inheritdoc cref="AddedFormatProperty"/>
    public string AddedFormat { get => GetValue(AddedFormatProperty); set => SetValue(AddedFormatProperty, value); }

    /// <inheritdoc cref="DueLabelFormatProperty"/>
    public string DueLabelFormat { get => GetValue(DueLabelFormatProperty); set => SetValue(DueLabelFormatProperty, value); }

    /// <inheritdoc cref="OwnerLabelFormatProperty"/>
    public string OwnerLabelFormat { get => GetValue(OwnerLabelFormatProperty); set => SetValue(OwnerLabelFormatProperty, value); }

    /// <inheritdoc cref="CommentsFormatProperty"/>
    public string CommentsFormat { get => GetValue(CommentsFormatProperty); set => SetValue(CommentsFormatProperty, value); }

    /// <inheritdoc cref="CommentsSingularFormatProperty"/>
    public string CommentsSingularFormat { get => GetValue(CommentsSingularFormatProperty); set => SetValue(CommentsSingularFormatProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="AnimatesMovesProperty"/>
    public bool AnimatesMoves { get => GetValue(AnimatesMovesProperty); set => SetValue(AnimatesMovesProperty, value); }

    /// <inheritdoc cref="MoveCommandProperty"/>
    public ICommand? MoveCommand { get => GetValue(MoveCommandProperty); set => SetValue(MoveCommandProperty, value); }

    /// <inheritdoc cref="AddCommandProperty"/>
    public ICommand? AddCommand { get => GetValue(AddCommandProperty); set => SetValue(AddCommandProperty, value); }

    /// <inheritdoc cref="DoneCountProperty"/>
    public int DoneCount { get => _doneCount; private set => SetAndRaise(DoneCountProperty, ref _doneCount, value); }

    /// <inheritdoc cref="TaskCountProperty"/>
    public int TaskCount { get => _taskCount; private set => SetAndRaise(TaskCountProperty, ref _taskCount, value); }

    /// <inheritdoc cref="TaskMovingEvent"/>
    public event EventHandler<ProjectBoardTaskMovingEventArgs>? TaskMoving
    {
        add => AddHandler(TaskMovingEvent, value);
        remove => RemoveHandler(TaskMovingEvent, value);
    }

    /// <inheritdoc cref="TaskMovedEvent"/>
    public event EventHandler<ProjectBoardTaskMovedEventArgs>? TaskMoved
    {
        add => AddHandler(TaskMovedEvent, value);
        remove => RemoveHandler(TaskMovedEvent, value);
    }

    /// <inheritdoc cref="AddRequestedEvent"/>
    public event EventHandler<ProjectBoardAddRequestedEventArgs>? AddRequested
    {
        add => AddHandler(AddRequestedEvent, value);
        remove => RemoveHandler(AddRequestedEvent, value);
    }

    /// <inheritdoc cref="TaskAddedEvent"/>
    public event EventHandler<ProjectBoardTaskEventArgs>? TaskAdded
    {
        add => AddHandler(TaskAddedEvent, value);
        remove => RemoveHandler(TaskAddedEvent, value);
    }

    /// <inheritdoc cref="FilterChangedEvent"/>
    public event EventHandler<ProjectBoardFilterChangedEventArgs>? FilterChanged
    {
        add => AddHandler(FilterChangedEvent, value);
        remove => RemoveHandler(FilterChangedEvent, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectBoardPeer(this);

    private sealed class ProjectBoardPeer(ProjectBoard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
