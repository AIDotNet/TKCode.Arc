using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="CodeSuggestion"/> is in its life.</summary>
public enum CodeSuggestionStatus
{
    /// <summary>The model is reading the file: a shimmering label over the plain file, no hunks yet.</summary>
    Thinking,

    /// <summary>The model is writing: hunks show as they grow, new lines fade in one by one.</summary>
    Streaming,

    /// <summary>The suggestion is complete: every hunk has Reject and Accept under it.</summary>
    Ready,

    /// <summary>The suggestion failed: the plain file, the error message and Retry.</summary>
    Error
}

/// <summary>What the person decided about one hunk of a <see cref="CodeSuggestion"/>.</summary>
public enum CodeSuggestionDecision
{
    /// <summary>Not decided yet.</summary>
    Pending,

    /// <summary>Kept: its removed lines leave the file and its new lines settle in.</summary>
    Accepted,

    /// <summary>Dropped: its new lines fold away and the original lines stay.</summary>
    Rejected
}

/// <summary>
/// One proposed change of a <see cref="CodeSuggestion"/>: it replaces <see cref="Remove"/> lines starting at the zero based
/// line <see cref="Start"/> with the <see cref="Insert"/> lines (remove 0 inserts before <see cref="Start"/>; an empty insert
/// deletes). To stream, replace <see cref="Insert"/> with a longer list (or call <see cref="AppendLine"/>) as tokens arrive.
/// </summary>
public sealed class CodeSuggestionHunk : AvaloniaObject
{
    /// <summary>The hunk's id, unique within the suggestion; decisions are kept by it.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<CodeSuggestionHunk, string>(nameof(Id), string.Empty);

    /// <summary>The zero based line of the first removed line, or the line the new lines go before.</summary>
    public static readonly StyledProperty<int> StartProperty =
        AvaloniaProperty.Register<CodeSuggestionHunk, int>(nameof(Start));

    /// <summary>How many original lines the hunk removes (0 for a pure insertion).</summary>
    public static readonly StyledProperty<int> RemoveProperty =
        AvaloniaProperty.Register<CodeSuggestionHunk, int>(nameof(Remove));

    /// <summary>The new lines (empty for a pure deletion). Replace the list to stream more lines in.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> InsertProperty =
        AvaloniaProperty.Register<CodeSuggestionHunk, IReadOnlyList<string>>(nameof(Insert), Array.Empty<string>());

    /// <summary>A two to six word summary shown in the review bar and in the names of its buttons.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<CodeSuggestionHunk, string?>(nameof(Summary));

    /// <summary>Creates an empty hunk; set <see cref="Id"/> and <see cref="Start"/>.</summary>
    public CodeSuggestionHunk()
    {
    }

    /// <summary>Creates a hunk.</summary>
    /// <param name="id">The id, unique within the suggestion.</param>
    /// <param name="start">The zero based line of the first removed line (or the insert position).</param>
    /// <param name="remove">How many original lines it removes.</param>
    /// <param name="insert">The new lines.</param>
    public CodeSuggestionHunk(string id, int start, int remove, params string[] insert)
    {
        Id = id;
        Start = start;
        Remove = remove;
        Insert = insert;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="StartProperty"/>
    public int Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="RemoveProperty"/>
    public int Remove { get => GetValue(RemoveProperty); set => SetValue(RemoveProperty, value); }

    /// <inheritdoc cref="InsertProperty"/>
    public IReadOnlyList<string> Insert { get => GetValue(InsertProperty); set => SetValue(InsertProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>The new lines as one text, lines split on <c>\n</c> (handy in XAML). An empty text inserts nothing.</summary>
    public string InsertText
    {
        get => string.Join('\n', Insert);
        set => Insert = string.IsNullOrEmpty(value) ? Array.Empty<string>() : value.Replace("\r", string.Empty).Split('\n');
    }

    /// <summary>Appends one new line (streaming).</summary>
    public void AppendLine(string line) => Insert = [.. Insert, line];

    /// <summary>A copy with the same values.</summary>
    public CodeSuggestionHunk Copy() => new(Id, Start, Remove, [.. Insert]) { Summary = Summary };

    /// <inheritdoc />
    public override string ToString() => Summary ?? Id;
}

/// <summary>Data of <see cref="CodeSuggestion.DecisionMade"/>.</summary>
public sealed class CodeSuggestionDecisionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public CodeSuggestionDecisionEventArgs(RoutedEvent routedEvent, CodeSuggestionHunk hunk, CodeSuggestionDecision decision, string code)
        : base(routedEvent)
    {
        Hunk = hunk;
        Decision = decision;
        Code = code;
    }

    /// <summary>The hunk that was decided.</summary>
    public CodeSuggestionHunk Hunk { get; }

    /// <summary>Accepted or rejected.</summary>
    public CodeSuggestionDecision Decision { get; }

    /// <summary>The file as it now stands: accepted hunks applied, the rest original.</summary>
    public string Code { get; }
}

/// <summary>Data of <see cref="CodeSuggestion.Resolved"/>.</summary>
public sealed class CodeSuggestionResolvedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public CodeSuggestionResolvedEventArgs(RoutedEvent routedEvent, string code, IReadOnlyList<string> accepted, IReadOnlyList<string> rejected)
        : base(routedEvent)
    {
        Code = code;
        Accepted = accepted;
        Rejected = rejected;
    }

    /// <summary>The final file: every accepted hunk applied. Write it here.</summary>
    public string Code { get; }

    /// <summary>The ids of the accepted hunks, in file order.</summary>
    public IReadOnlyList<string> Accepted { get; }

    /// <summary>The ids of the rejected hunks, in file order.</summary>
    public IReadOnlyList<string> Rejected { get; }
}
