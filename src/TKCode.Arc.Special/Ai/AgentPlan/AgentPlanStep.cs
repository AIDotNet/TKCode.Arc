using System.Threading;
using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// One step of an <see cref="AgentPlan"/>: an id, an instruction-style title, and while the plan runs its state, the
/// activity lines the agent reports, an optional note (a suggested or agent-inserted step), the reason of a failure and
/// the time a finished step took. Every property is observed, so changing a step updates the plan on screen.
/// </summary>
public sealed class AgentPlanStep : AvaloniaObject
{
    private static long s_nextId;

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AgentPlanStep, string>(nameof(Title), string.Empty);

    /// <summary>Defines the <see cref="State"/> property.</summary>
    public static readonly StyledProperty<AgentPlanStepState> StateProperty =
        AvaloniaProperty.Register<AgentPlanStep, AgentPlanStepState>(nameof(State));

    /// <summary>Defines the <see cref="Note"/> property.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<AgentPlanStep, string?>(nameof(Note));

    /// <summary>Defines the <see cref="NoteKind"/> property.</summary>
    public static readonly StyledProperty<AgentPlanNoteKind> NoteKindProperty =
        AvaloniaProperty.Register<AgentPlanStep, AgentPlanNoteKind>(nameof(NoteKind));

    /// <summary>Defines the <see cref="Error"/> property.</summary>
    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<AgentPlanStep, string?>(nameof(Error));

    /// <summary>Defines the <see cref="Duration"/> property.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<AgentPlanStep, TimeSpan?>(nameof(Duration));

    /// <summary>Creates a pending step with a generated id and an empty title.</summary>
    public AgentPlanStep()
        : this(null, string.Empty)
    {
    }

    /// <summary>Creates a pending step.</summary>
    /// <param name="id">A stable id (the simulation finds a step's script by it); null generates one.</param>
    /// <param name="title">The step, written as an instruction ("Add a search index for articles").</param>
    public AgentPlanStep(string? id, string title)
    {
        Id = string.IsNullOrEmpty(id) ? "step-" + Interlocked.Increment(ref s_nextId) : id;
        Title = title;
    }

    /// <summary>The step's stable id. It stays the same when the step is renamed or moved.</summary>
    public string Id { get; }

    /// <summary>The step, written as an instruction. Renaming a draft step in place writes it.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Where the step stands while the plan runs (Pending until then).</summary>
    public AgentPlanStepState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Short past-tense activity lines the agent reports while it works on the step ("Read 5 files", "Ran 9 tests"); the
    /// first word picks the icon (see <see cref="AgentPlanActivityIcons"/>). They show under the active and the failed
    /// step and fold away when the step is done or skipped.
    /// </summary>
    public AvaloniaList<string> Activities { get; } = new();

    /// <summary>
    /// The note under the title. Null shows the default text of <see cref="NoteKind"/> ("Suggested", "Added by agent")
    /// or no note for <see cref="AgentPlanNoteKind.None"/>.
    /// </summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>Where the step came from: the agent's notes are led by the model's mark.</summary>
    public AgentPlanNoteKind NoteKind
    {
        get => GetValue(NoteKindProperty);
        set => SetValue(NoteKindProperty, value);
    }

    /// <summary>Why the step failed, shown after the word Failed while <see cref="State"/> is Failed.</summary>
    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    /// <summary>How long the step took, shown on the right once it is done ("23s", "1m 07s") and added to the total.</summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>A pending copy with the same id, title and note (no activity, error or time).</summary>
    public AgentPlanStep CloneAsPending() => new(Id, Title) { Note = Note, NoteKind = NoteKind };

    /// <inheritdoc />
    public override string ToString() => Title;
}
