namespace TKCode.Arc;

/// <summary>
/// Every string the map draws, built once per data or wording change so frames only look them up (no formatting and no
/// allocation while drawing). Indexed by layout box.
/// </summary>
internal sealed class SemanticZoomLabels
{
    public static readonly SemanticZoomLabels Empty = new(0);

    private static readonly SemanticZoomPerson[] s_nobody = Array.Empty<SemanticZoomPerson>();

    private readonly string?[] _percent;
    private readonly string?[] _line;
    private readonly string?[] _people;
    private readonly SemanticZoomPerson[]?[] _faces;
    private readonly string?[] _status;
    private readonly string?[] _detail;
    private readonly string?[] _comments;
    private readonly string?[] _summary;
    private readonly string?[] _place;
    private readonly string?[] _checklist;
    private readonly Dictionary<SemanticZoomPerson, string> _initials = new();

    private SemanticZoomLabels(int count)
    {
        _percent = new string?[count];
        _line = new string?[count];
        _people = new string?[count];
        _faces = new SemanticZoomPerson[]?[count];
        _status = new string?[count];
        _detail = new string?[count];
        _comments = new string?[count];
        _summary = new string?[count];
        _place = new string?[count];
        _checklist = new string?[count];
    }

    /// <summary>Most people circles on a project card.</summary>
    public const int MaxFaces = 5;

    /// <summary>The checklist heading.</summary>
    public string ChecklistLabel { get; private set; } = "Checklist";

    /// <summary>Percentage of a team or project ("60%").</summary>
    public string? Percent(int box) => Get(_percent, box);

    /// <summary>Team: "Lead · N projects · N open tasks"; project: "On track · Due Oct 22"; task: "2/3 · Sep 9" or "Blocked".</summary>
    public string? Line(int box) => Get(_line, box);

    /// <summary>Project people row text ("Name and N others").</summary>
    public string? People(int box) => Get(_people, box);

    /// <summary>People circles of a project (lead first, at most <see cref="MaxFaces"/>).</summary>
    public SemanticZoomPerson[] Faces(int box) => box >= 0 && box < _faces.Length ? _faces[box] ?? s_nobody : s_nobody;

    /// <summary>The lead of a team (its first face).</summary>
    public SemanticZoomPerson? Lead(int box) => Faces(box) is { Length: > 0 } faces ? faces[0] : null;

    /// <summary>Task status name.</summary>
    public string? StatusName(int box) => Get(_status, box);

    /// <summary>Task row after the status ("· Due Sep 12 · 2 points").</summary>
    public string? Detail(int box) => Get(_detail, box);

    /// <summary>Task comment count ("3 comments").</summary>
    public string? Comments(int box) => Get(_comments, box);

    /// <summary>Task summary (given or generated).</summary>
    public string? Summary(int box) => Get(_summary, box);

    /// <summary>Task place ("Project · Team").</summary>
    public string? Place(int box) => Get(_place, box);

    /// <summary>Task checklist count ("2 of 3").</summary>
    public string? ChecklistCount(int box) => Get(_checklist, box);

    /// <summary>A person's initials (cached).</summary>
    public string InitialsOf(SemanticZoomPerson person)
    {
        if (!_initials.TryGetValue(person, out var initials))
        {
            initials = person.DisplayInitials;
            _initials[person] = initials;
        }
        return initials;
    }

    private static string? Get(string?[] array, int box) => box >= 0 && box < array.Length ? array[box] : null;

    /// <summary>Builds every label of <paramref name="layout"/>.</summary>
    public static SemanticZoomLabels Build(SemanticZoomLayout layout, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        var labels = new SemanticZoomLabels(layout.Boxes.Length) { ChecklistLabel = texts.ChecklistLabel };
        for (var i = 0; i < layout.Boxes.Length; i++)
        {
            labels.Update(layout, i, texts, formatter);
        }
        return labels;
    }

    /// <summary>Rebuilds the labels of one box (a value changed).</summary>
    public void Update(SemanticZoomLayout layout, int box, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        var node = layout.Boxes[box].Node;
        switch (node)
        {
            case SemanticZoomTeam team:
                _percent[box] = formatter.FormatPercent(team.Progress);
                _line[box] = SemanticZoomTexts.Format(texts.TeamLineFormat, team.Lead?.Name ?? texts.NobodyText,
                    team.ProjectCount, team.OpenTasks);
                _faces[box] = team.Lead is { } lead ? [lead] : s_nobody;
                break;
            case SemanticZoomProject project:
                _percent[box] = formatter.FormatPercent(project.Progress);
                _line[box] = ProjectStatus(project, texts, formatter);
                var people = project.People;
                var faces = new SemanticZoomPerson[Math.Min(MaxFaces, people.Count)];
                for (var i = 0; i < faces.Length; i++)
                {
                    faces[i] = people[i];
                }
                _faces[box] = faces;
                _people[box] = PeopleText(people, texts);
                break;
            case SemanticZoomTask task:
                _line[box] = TaskMeta(task, texts, formatter);
                _status[box] = texts.StatusName(task.Status);
                _detail[box] = TaskDetail(task, texts, formatter);
                _comments[box] = SemanticZoomTexts.Format(task.CommentCount == 1 ? texts.CommentFormat : texts.CommentsFormat, task.CommentCount);
                _summary[box] = Summary(task, texts, formatter);
                _place[box] = SemanticZoomTexts.Format(texts.TaskPlaceFormat, task.Project?.Title ?? string.Empty,
                    task.Project?.Team?.Title ?? string.Empty);
                _checklist[box] = SemanticZoomTexts.Format(texts.CountOfFormat, task.ChecklistDone, task.Checklist.Count);
                break;
        }
    }

    /// <summary>"On track · Due Oct 22" (health and due date).</summary>
    public static string ProjectStatus(SemanticZoomProject project, SemanticZoomTexts texts, ISemanticZoomFormatter formatter) =>
        project.Due is { } due
            ? SemanticZoomTexts.Format(texts.ProjectStatusFormat, texts.HealthName(project.Health), formatter.FormatDate(due))
            : SemanticZoomTexts.Format(texts.ProjectStatusNoDueFormat, texts.HealthName(project.Health));

    /// <summary>"Name and N others".</summary>
    public static string PeopleText(IReadOnlyList<SemanticZoomPerson> people, SemanticZoomTexts texts) => people.Count switch
    {
        0 => string.Empty,
        1 => SemanticZoomTexts.Format(texts.PeopleOneFormat, people[0].Name),
        2 => SemanticZoomTexts.Format(texts.PeopleTwoFormat, people[0].Name, 1),
        _ => SemanticZoomTexts.Format(texts.PeopleManyFormat, people[0].Name, people.Count - 1)
    };

    /// <summary>Block corner: "2/3 · Sep 9", or the blocked status.</summary>
    public static string TaskMeta(SemanticZoomTask task, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        if (task.Status == SemanticZoomTaskStatus.Blocked)
        {
            return texts.StatusBlocked;
        }
        return task.Due is { } due
            ? SemanticZoomTexts.Format(texts.TaskMetaFormat, task.ChecklistDone, task.Checklist.Count, formatter.FormatDate(due))
            : SemanticZoomTexts.Format(texts.TaskMetaNoDueFormat, task.ChecklistDone, task.Checklist.Count);
    }

    /// <summary>"· Due Sep 12 · 2 points".</summary>
    public static string TaskDetail(SemanticZoomTask task, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        var points = SemanticZoomTexts.Format(task.Points == 1 ? texts.PointFormat : texts.PointsFormat, task.Points);
        return task.Due is { } due
            ? SemanticZoomTexts.Format(texts.TaskDetailFormat, formatter.FormatDate(due), points)
            : SemanticZoomTexts.Format(texts.TaskDetailNoDueFormat, points);
    }

    /// <summary>The task's summary, or one generated from its status (assignee, date, project and its description).</summary>
    public static string Summary(SemanticZoomTask task, SemanticZoomTexts texts, ISemanticZoomFormatter formatter)
    {
        if (!string.IsNullOrEmpty(task.Summary))
        {
            return task.Summary!;
        }
        var project = task.Project;
        var first = task.Assignee?.FirstName ?? texts.NobodyText;
        var reviewer = (task.Reviewer ?? project?.Lead)?.FirstName ?? texts.NobodyText;
        var date = task.Due is { } due ? formatter.FormatDate(due) : texts.NoneText;
        var format = task.Status switch
        {
            SemanticZoomTaskStatus.Done => texts.SummaryDoneFormat,
            SemanticZoomTaskStatus.InProgress => texts.SummaryInProgressFormat,
            SemanticZoomTaskStatus.InReview => texts.SummaryInReviewFormat,
            SemanticZoomTaskStatus.Blocked => texts.SummaryBlockedFormat,
            _ => texts.SummaryTodoFormat
        };
        var text = SemanticZoomTexts.Format(format, first, date, project?.Title ?? string.Empty,
            project?.Description ?? string.Empty, reviewer);
        return string.IsNullOrEmpty(project?.Description) ? text.TrimEnd(' ', ':', '.') + "." : text;
    }
}
