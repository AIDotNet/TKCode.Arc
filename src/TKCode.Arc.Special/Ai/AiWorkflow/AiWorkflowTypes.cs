using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What a step of an <see cref="AiWorkflow"/> does.</summary>
public enum AiWorkflowStepKind
{
    /// <summary>Starts the flow (a new email, a schedule, a webhook). Always the first step; it cannot be removed.</summary>
    Trigger,

    /// <summary>A model step: classify, summarize, draft or extract.</summary>
    Ai,

    /// <summary>Does something in an app: posts a message, creates an issue, adds a row.</summary>
    Action,

    /// <summary>Picks one of its paths. One per flow, as the last top-level step.</summary>
    Branch
}

/// <summary>Where a step of an <see cref="AiWorkflow"/> is in the current test run.</summary>
public enum AiWorkflowStepStatus
{
    /// <summary>Not reached yet (or no run).</summary>
    Idle,

    /// <summary>Running now: the activity line shimmers.</summary>
    Running,

    /// <summary>Finished: its duration and output preview show.</summary>
    Done,

    /// <summary>Failed: its error and Retry show.</summary>
    Failed,

    /// <summary>On a branch path the run did not take.</summary>
    Skipped
}

/// <summary>How a test run of an <see cref="AiWorkflow"/> ended.</summary>
public enum AiWorkflowRunStatus
{
    /// <summary>Every step on the taken path finished.</summary>
    Passed,

    /// <summary>The run stopped on a failed step.</summary>
    Failed
}

/// <summary>What changed in the steps of an <see cref="AiWorkflow"/>.</summary>
public enum AiWorkflowChangeKind
{
    /// <summary>A step was added from the add step menu or the empty state.</summary>
    Added,

    /// <summary>A step was removed with its remove button.</summary>
    Removed
}

/// <summary>
/// The app a step of an <see cref="AiWorkflow"/> belongs to: a name read to screen readers and shown in the add step menu,
/// and a mark (a 24 × 24 stroked glyph) drawn in a small hairline square on the rail. The built-in apps are generic
/// (Mail, AI, Chat, Tracker, ...) with Lucide glyphs; pass your own name and mark for real products (for a model step,
/// the model's vendor with <c>isAccent: true</c>).
/// </summary>
public sealed class AiWorkflowApp
{
    /// <summary>Creates an empty app; set <see cref="Name"/> and <see cref="Mark"/>.</summary>
    public AiWorkflowApp()
    {
    }

    /// <summary>Creates an app with an id, a name and a mark.</summary>
    public AiWorkflowApp(string id, string name, Geometry? mark, bool isAccent = false)
    {
        Id = id;
        Name = name;
        Mark = mark;
        IsAccent = isAccent;
    }

    /// <summary>A short id ("mail", "ai", ...); the built-in apps are found by it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name read with the step ("Create issue, Tracker, Done in 700ms").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The mark: a 24 × 24 stroked glyph, drawn in the text color so it keeps contrast in both themes.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>Draws the mark in the accent color (the AI steps).</summary>
    public bool IsAccent { get; set; }

    /// <summary>A mail inbox (trigger: a new email).</summary>
    public static AiWorkflowApp Mail { get; } = new("mail", "Mail", ArcIcons.Mail);

    /// <summary>A generic model step (classify, summarize, draft), its mark in the accent. Name the model's vendor for real products.</summary>
    public static AiWorkflowApp Ai { get; } = new("ai", "AI", ArcIcons.Sparkles, isAccent: true);

    /// <summary>A team chat (post a message to a channel).</summary>
    public static AiWorkflowApp Chat { get; } = new("chat", "Chat", ArcIcons.MessageSquare);

    /// <summary>An issue tracker (create or update an issue).</summary>
    public static AiWorkflowApp Tracker { get; } = new("tracker", "Tracker", ArcIcons.CircleDot);

    /// <summary>A docs workspace (create a page).</summary>
    public static AiWorkflowApp Docs { get; } = new("docs", "Docs", ArcIcons.FileText);

    /// <summary>A spreadsheet (add a row).</summary>
    public static AiWorkflowApp Sheets { get; } = new("sheets", "Sheets", ArcIcons.Table);

    /// <summary>A CRM (update a contact).</summary>
    public static AiWorkflowApp Crm { get; } = new("crm", "CRM", ArcIcons.Users);

    /// <summary>A help desk (open or answer a ticket).</summary>
    public static AiWorkflowApp Helpdesk { get; } = new("helpdesk", "Help desk", ArcIcons.Inbox);

    /// <summary>A code host (open an issue or a pull request).</summary>
    public static AiWorkflowApp Repo { get; } = new("repo", "Repo", ArcIcons.Code);

    /// <summary>A payments service (trigger: a failed payment).</summary>
    public static AiWorkflowApp Payments { get; } = new("payments", "Payments", ArcIcons.CreditCard);

    /// <summary>A schedule (trigger: every weekday at 9:00).</summary>
    public static AiWorkflowApp Schedule { get; } = new("schedule", "Schedule", ArcIcons.Clock);

    /// <summary>A webhook (trigger: an incoming request).</summary>
    public static AiWorkflowApp Webhook { get; } = new("webhook", "Webhook", ArcIcons.Zap);

    /// <summary>The branch step's own mark.</summary>
    public static AiWorkflowApp Branch { get; } = new("branch", "Branch", ArcIcons.GitBranch);

    /// <summary>Every built-in app.</summary>
    public static IReadOnlyList<AiWorkflowApp> BuiltIn { get; } =
        [Mail, Ai, Chat, Tracker, Docs, Sheets, Crm, Helpdesk, Repo, Payments, Schedule, Webhook, Branch];

    /// <summary>
    /// The built-in app with <paramref name="id"/> (case does not matter), or a new app named after the id with no mark.
    /// XAML uses it to read <c>App="mail"</c>.
    /// </summary>
    public static AiWorkflowApp Parse(string id)
    {
        foreach (var app in BuiltIn)
        {
            if (string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return app;
            }
        }
        return new AiWorkflowApp(id, id, null);
    }
}

/// <summary>
/// The output preview of a step of an <see cref="AiWorkflow"/>, rendered inline in small muted text under the step,
/// aligned with its title. Use one of <see cref="AiWorkflowEmailOutput"/>, <see cref="AiWorkflowLabelOutput"/>,
/// <see cref="AiWorkflowRouteOutput"/>, <see cref="AiWorkflowMessageOutput"/>, <see cref="AiWorkflowRecordOutput"/> or
/// <see cref="AiWorkflowTextOutput"/>.
/// </summary>
public abstract class AiWorkflowOutput
{
    /// <summary>The output in one line of plain text (read in the run's announcements).</summary>
    public abstract string Summary { get; }
}

/// <summary>An email: the sender and subject on one line and a two line preview.</summary>
public sealed class AiWorkflowEmailOutput : AiWorkflowOutput
{
    /// <summary>The sender's name.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>The sender's address, after the name.</summary>
    public string? Address { get; set; }

    /// <summary>The subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The start of the body (two lines at most).</summary>
    public string Preview { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string Summary => From + ": " + Subject;
}

/// <summary>A label a model chose: a hairline label with a status dot, the confidence in monospace and a streamed reason.</summary>
public sealed class AiWorkflowLabelOutput : AiWorkflowOutput
{
    /// <summary>The label ("Urgent").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The model's confidence from 0 to 1, or null to leave it out.</summary>
    public double? Confidence { get; set; }

    /// <summary>Why the model chose the label; it streams in fast chunks.</summary>
    public string? Reason { get; set; }

    /// <inheritdoc />
    public override string Summary => Label;
}

/// <summary>The path a branch took. Return it from a branch step to pick the path.</summary>
public sealed class AiWorkflowRouteOutput : AiWorkflowOutput
{
    /// <summary>The <see cref="AiWorkflowBranchPath.Id"/> of the path that runs.</summary>
    public string PathId { get; set; } = string.Empty;

    /// <summary>Why ("Label is Urgent").</summary>
    public string? Note { get; set; }

    /// <inheritdoc />
    public override string Summary => Note ?? PathId;
}

/// <summary>A posted chat message.</summary>
public sealed class AiWorkflowMessageOutput : AiWorkflowOutput
{
    /// <summary>The channel ("#support").</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Who posted it.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>The message.</summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string Summary => Channel + ": " + Text;
}

/// <summary>A created or updated record: an issue key, a row, a ticket or a page.</summary>
public sealed class AiWorkflowRecordOutput : AiWorkflowOutput
{
    /// <summary>The record's key ("SUP-1042"), in monospace.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The record's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One more fact ("Urgent, assigned to On call").</summary>
    public string? Meta { get; set; }

    /// <inheritdoc />
    public override string Summary => Id + " " + Title;
}

/// <summary>Free text, such as a drafted reply; it streams in fast chunks.</summary>
public sealed class AiWorkflowTextOutput : AiWorkflowOutput
{
    /// <summary>The text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string Summary => Text;
}

/// <summary>One path of a branch step: a label for its tab and the steps that run when the branch picks it.</summary>
public sealed class AiWorkflowBranchPath
{
    /// <summary>The id a <see cref="AiWorkflowRouteOutput"/> names.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The tab's label ("Urgent").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The steps on this path, in order.</summary>
    public List<AiWorkflowStep> Steps { get; set; } = [];

    internal AiWorkflowBranchPath Clone() => new() { Id = Id, Label = Label, Steps = [.. Steps.Select(s => s.Clone())] };
}

/// <summary>
/// One step of an <see cref="AiWorkflow"/>. The workflow treats steps as values: to change one, put a new list in
/// <see cref="AiWorkflow.Steps"/>. <see cref="Output"/>, <see cref="Duration"/> and <see cref="Error"/> are what a simulated
/// run shows; a run through <see cref="AiWorkflow.RunStep"/> shows what your code returns.
/// </summary>
public sealed class AiWorkflowStep
{
    /// <summary>A unique id (outputs are keyed by it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What the step does.</summary>
    public AiWorkflowStepKind Kind { get; set; } = AiWorkflowStepKind.Action;

    /// <summary>The app it belongs to (its mark and name).</summary>
    public AiWorkflowApp? App { get; set; }

    /// <summary>The title ("Create issue").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One line of detail after the title ("Support team").</summary>
    public string? Detail { get; set; }

    /// <summary>What the step says while it runs; the detail line crossfades to it and shimmers.</summary>
    public string? Activity { get; set; }

    /// <summary>The output a simulated run shows.</summary>
    public AiWorkflowOutput? Output { get; set; }

    /// <summary>How long a simulated run takes (600ms when null).</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>A simulated run fails with this message on its first attempt; Retry then passes.</summary>
    public string? Error { get; set; }

    /// <summary>A branch's paths (each with its own steps).</summary>
    public List<AiWorkflowBranchPath> Paths { get; set; } = [];

    internal AiWorkflowStep Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        App = App,
        Title = Title,
        Detail = Detail,
        Activity = Activity,
        Output = Output,
        Duration = Duration,
        Error = Error,
        Paths = [.. Paths.Select(p => p.Clone())]
    };
}

/// <summary>
/// An entry of the add step menu of an <see cref="AiWorkflow"/>: the same shape as a step without an id. Triggers are
/// offered in the empty state; everything else is in the menu.
/// </summary>
public sealed class AiWorkflowCatalogItem
{
    /// <summary>What the new step does.</summary>
    public AiWorkflowStepKind Kind { get; set; } = AiWorkflowStepKind.Action;

    /// <summary>The app (its mark and name).</summary>
    public AiWorkflowApp? App { get; set; }

    /// <summary>The title ("Create issue").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One line of detail.</summary>
    public string? Detail { get; set; }

    /// <summary>What the step says while it runs.</summary>
    public string? Activity { get; set; }

    /// <summary>The output a simulated run shows.</summary>
    public AiWorkflowOutput? Output { get; set; }

    /// <summary>How long a simulated run takes.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>A simulated first attempt fails with this message.</summary>
    public string? Error { get; set; }

    /// <summary>A branch's paths (their steps are copied into the new step).</summary>
    public List<AiWorkflowBranchPath> Paths { get; set; } = [];

    /// <summary>Makes a step from the entry with the given id.</summary>
    public AiWorkflowStep ToStep(string id) => new()
    {
        Id = id,
        Kind = Kind,
        App = App,
        Title = Title,
        Detail = Detail,
        Activity = Activity,
        Output = Output,
        Duration = Duration,
        Error = Error,
        Paths = [.. Paths.Select(p => p.Clone())]
    };
}

/// <summary>What <see cref="AiWorkflow.RunStep"/> gets with each step.</summary>
public sealed class AiWorkflowRunStepContext
{
    internal AiWorkflowRunStepContext(CancellationToken cancellation, IReadOnlyDictionary<string, AiWorkflowOutput> previous, int attempt)
    {
        Cancellation = cancellation;
        Previous = previous;
        Attempt = attempt;
    }

    /// <summary>Cancelled on Stop, a new run, or when the workflow leaves the window.</summary>
    public CancellationToken Cancellation { get; }

    /// <summary>The outputs of the steps that ran before, by step id.</summary>
    public IReadOnlyDictionary<string, AiWorkflowOutput> Previous { get; }

    /// <summary>1 on the first try, 2 after the first Retry, and so on.</summary>
    public int Attempt { get; }
}

/// <summary>
/// Runs one step of an <see cref="AiWorkflow"/> for real and returns its output (or null). Throw to fail the step with
/// the exception's message. A branch returns an <see cref="AiWorkflowRouteOutput"/> to pick its path.
/// </summary>
public delegate Task<AiWorkflowOutput?> AiWorkflowStepRunner(AiWorkflowStep step, AiWorkflowRunStepContext context);

/// <summary>Arguments of <see cref="AiWorkflow.StepsChanged"/>.</summary>
public sealed class AiWorkflowStepsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiWorkflowStepsChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<AiWorkflowStep> steps, AiWorkflowChangeKind kind,
                                           AiWorkflowStep step) : base(routedEvent)
    {
        Steps = steps;
        Kind = kind;
        Step = step;
    }

    /// <summary>The new list of steps (also in <see cref="AiWorkflow.Steps"/>).</summary>
    public IReadOnlyList<AiWorkflowStep> Steps { get; }

    /// <summary>Whether a step was added or removed.</summary>
    public AiWorkflowChangeKind Kind { get; }

    /// <summary>The step that was added or removed.</summary>
    public AiWorkflowStep Step { get; }
}

/// <summary>Arguments of <see cref="AiWorkflow.RunCompleted"/>.</summary>
public sealed class AiWorkflowRunCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiWorkflowRunCompletedEventArgs(RoutedEvent routedEvent, AiWorkflowRunStatus status, TimeSpan duration,
                                           IReadOnlyDictionary<string, AiWorkflowOutput> outputs, string? failedStepId)
        : base(routedEvent)
    {
        Status = status;
        Duration = duration;
        Outputs = outputs;
        FailedStepId = failedStepId;
    }

    /// <summary>Passed, or failed on a step.</summary>
    public AiWorkflowRunStatus Status { get; }

    /// <summary>How long the run took.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Every step's output, by step id.</summary>
    public IReadOnlyDictionary<string, AiWorkflowOutput> Outputs { get; }

    /// <summary>The step the run stopped on, when it failed.</summary>
    public string? FailedStepId { get; }
}
