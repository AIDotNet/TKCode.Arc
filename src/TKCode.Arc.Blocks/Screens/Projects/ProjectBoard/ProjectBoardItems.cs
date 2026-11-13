using Avalonia;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>Which cards a <see cref="ProjectBoard"/> shows.</summary>
public enum ProjectBoardFilter
{
    /// <summary>Every task.</summary>
    All,

    /// <summary>Only the tasks owned by <see cref="ProjectBoard.CurrentUser"/>.</summary>
    Mine,
}

/// <summary>The way a card's move buttons send a task.</summary>
public enum ProjectBoardMoveDirection
{
    /// <summary>To the stage on the left.</summary>
    Previous,

    /// <summary>To the stage on the right.</summary>
    Next,
}

/// <summary>
/// One column of a <see cref="ProjectBoard"/>: a stage of the work, such as Planned, In progress, Review or Done. Tasks
/// name their stage by <see cref="Key"/>.
/// </summary>
public sealed class ProjectBoardStage : AvaloniaObject
{
    /// <summary>A stable id that tasks refer to with <see cref="ProjectBoardTask.StageKey"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ProjectBoardStage, string>(nameof(Key), string.Empty);

    /// <summary>The column heading; empty uses <see cref="Key"/>.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<ProjectBoardStage, string>(nameof(Header), string.Empty);

    /// <summary>
    /// Marks the stage that counts as finished: its tasks fill the sprint line, their titles turn quiet and their due
    /// labels stop warning. When no stage is marked, the last stage counts as finished.
    /// </summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<ProjectBoardStage, bool>(nameof(IsDone));

    /// <summary>The text shown in the column while it has no cards; null uses the board's empty texts.</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<ProjectBoardStage, string?>(nameof(EmptyText));

    /// <summary>Creates an empty stage.</summary>
    public ProjectBoardStage()
    {
    }

    /// <summary>Creates a stage with a key and a heading.</summary>
    /// <param name="key">The stable id tasks refer to.</param>
    /// <param name="header">The column heading; null uses the key.</param>
    /// <param name="isDone">True for the finished stage.</param>
    public ProjectBoardStage(string key, string? header = null, bool isDone = false)
    {
        Key = key;
        Header = header ?? key;
        IsDone = isDone;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>The heading, or the key when no heading is set.</summary>
    public string DisplayHeader => string.IsNullOrEmpty(Header) ? Key : Header;

    /// <inheritdoc />
    public override string ToString() => DisplayHeader;
}

/// <summary>
/// One card of a <see cref="ProjectBoard"/>: a task with an area, a due label, a title, an owner and a comment count, in
/// the stage named by <see cref="StageKey"/>. Changing <see cref="StageKey"/> moves the card to that column.
/// </summary>
public sealed class ProjectBoardTask : AvaloniaObject
{
    /// <summary>A stable id for events and persistence (a new GUID by default).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ProjectBoardTask, string>(nameof(Key), string.Empty);

    /// <summary>The card title; long titles wrap.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProjectBoardTask, string>(nameof(Title), string.Empty);

    /// <summary>The small label at the top left (a product area or team); null shows none.</summary>
    public static readonly StyledProperty<string?> AreaProperty =
        AvaloniaProperty.Register<ProjectBoardTask, string?>(nameof(Area));

    /// <summary>The due label at the top right ("Today", "Fri", "Oct 3"); null shows none.</summary>
    public static readonly StyledProperty<string?> DueTextProperty =
        AvaloniaProperty.Register<ProjectBoardTask, string?>(nameof(DueText));

    /// <summary>Shows the due label in the warning color; ignored once the task is in the finished stage.</summary>
    public static readonly StyledProperty<bool> IsDueSoonProperty =
        AvaloniaProperty.Register<ProjectBoardTask, bool>(nameof(IsDueSoon));

    /// <summary>The person responsible (a 22px avatar; the "My work" filter compares it with the current user).</summary>
    public static readonly StyledProperty<ProjectPerson?> OwnerProperty =
        AvaloniaProperty.Register<ProjectBoardTask, ProjectPerson?>(nameof(Owner));

    /// <summary>The number of comments; zero hides the comment count.</summary>
    public static readonly StyledProperty<int> CommentCountProperty =
        AvaloniaProperty.Register<ProjectBoardTask, int>(nameof(CommentCount));

    /// <summary>The <see cref="ProjectBoardStage.Key"/> of the task's stage; an unknown key shows it in the first stage.</summary>
    public static readonly StyledProperty<string?> StageKeyProperty =
        AvaloniaProperty.Register<ProjectBoardTask, string?>(nameof(StageKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Any object the app wants to keep with the task.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ProjectBoardTask, object?>(nameof(Tag));

    /// <summary>Creates a task with a new key.</summary>
    public ProjectBoardTask()
    {
        SetCurrentValue(KeyProperty, Guid.NewGuid().ToString("N"));
    }

    /// <summary>Creates a task with a title in a stage.</summary>
    /// <param name="title">The card title.</param>
    /// <param name="stageKey">The key of the task's stage.</param>
    public ProjectBoardTask(string title, string? stageKey = null) : this()
    {
        Title = title;
        StageKey = stageKey;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="AreaProperty"/>
    public string? Area { get => GetValue(AreaProperty); set => SetValue(AreaProperty, value); }

    /// <inheritdoc cref="DueTextProperty"/>
    public string? DueText { get => GetValue(DueTextProperty); set => SetValue(DueTextProperty, value); }

    /// <inheritdoc cref="IsDueSoonProperty"/>
    public bool IsDueSoon { get => GetValue(IsDueSoonProperty); set => SetValue(IsDueSoonProperty, value); }

    /// <inheritdoc cref="OwnerProperty"/>
    public ProjectPerson? Owner { get => GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }

    /// <inheritdoc cref="CommentCountProperty"/>
    public int CommentCount { get => GetValue(CommentCountProperty); set => SetValue(CommentCountProperty, value); }

    /// <inheritdoc cref="StageKeyProperty"/>
    public string? StageKey { get => GetValue(StageKeyProperty); set => SetValue(StageKeyProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A move made with a card's arrow buttons (the parameter of <see cref="ProjectBoard.MoveCommand"/>).</summary>
public sealed class ProjectBoardMoveRequest
{
    /// <summary>Creates the move.</summary>
    /// <param name="task">The task that moved.</param>
    /// <param name="direction">The button that was pressed.</param>
    /// <param name="fromStage">The stage it left.</param>
    /// <param name="toStage">The stage it is in now.</param>
    public ProjectBoardMoveRequest(ProjectBoardTask task, ProjectBoardMoveDirection direction, ProjectBoardStage fromStage, ProjectBoardStage toStage)
    {
        Task = task;
        Direction = direction;
        FromStage = fromStage;
        ToStage = toStage;
    }

    /// <summary>The task that moved.</summary>
    public ProjectBoardTask Task { get; }

    /// <summary>The button that was pressed.</summary>
    public ProjectBoardMoveDirection Direction { get; }

    /// <summary>The stage the task left.</summary>
    public ProjectBoardStage FromStage { get; }

    /// <summary>The stage the task is in now.</summary>
    public ProjectBoardStage ToStage { get; }
}
