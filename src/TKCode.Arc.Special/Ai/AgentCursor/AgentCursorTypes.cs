using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AgentCursor"/> run is.</summary>
public enum AgentCursorPhase
{
    /// <summary>Nothing has run yet; with <see cref="AgentCursor.AutoPlay"/> off the header offers Ask.</summary>
    Idle,

    /// <summary>The agent's cursor has left its avatar and reads the document (and fetches the edits).</summary>
    Thinking,

    /// <summary>The agent is making its edits one by one.</summary>
    Editing,

    /// <summary>The edits are done and stay tinted until someone accepts or undoes them from the review bar.</summary>
    Review,

    /// <summary>Someone accepted the changes.</summary>
    Accepted,

    /// <summary>Someone undid the changes; the document is back to where it was.</summary>
    Reverted,

    /// <summary>Someone pressed Stop; finished edits stay for review.</summary>
    Stopped,

    /// <summary>The agent had no edits to make.</summary>
    Empty,

    /// <summary>Fetching the edits failed; the header offers Retry.</summary>
    Error
}

/// <summary>What a block of an <see cref="AgentCursor"/> document is.</summary>
public enum AgentCursorBlockKind
{
    /// <summary>The document's title.</summary>
    Title,

    /// <summary>A section heading.</summary>
    Heading,

    /// <summary>A paragraph of body text.</summary>
    Paragraph
}

/// <summary>What an <see cref="AgentCursorEdit"/> does.</summary>
public enum AgentCursorEditKind
{
    /// <summary>Selects a block and streams in new text for it.</summary>
    Rewrite,

    /// <summary>Adds a new block after another one.</summary>
    Insert,

    /// <summary>Replaces one word inside a block, flagged with a wavy underline first.</summary>
    Fix
}

/// <summary>One block of the document an <see cref="AgentCursor"/> shows: a title, heading or paragraph with a stable id.</summary>
public sealed class AgentCursorBlock
{
    /// <summary>Creates an empty paragraph; set <see cref="Id"/> and <see cref="Text"/>.</summary>
    public AgentCursorBlock()
    {
    }

    /// <summary>Creates a block.</summary>
    /// <param name="id">A stable id that edits address the block by.</param>
    /// <param name="kind">Title, heading or paragraph.</param>
    /// <param name="text">The block's plain text.</param>
    public AgentCursorBlock(string id, AgentCursorBlockKind kind, string text)
    {
        Id = id;
        Kind = kind;
        Text = text;
    }

    /// <summary>A stable id that edits address the block by.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Title, heading or paragraph.</summary>
    public AgentCursorBlockKind Kind { get; set; } = AgentCursorBlockKind.Paragraph;

    /// <summary>The block's plain text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// One edit the agent makes in an <see cref="AgentCursor"/> document. Edits address blocks by id: a rewrite replaces a
/// block's text, an insert adds a block after one, a fix replaces an exact word inside a block.
/// </summary>
public sealed class AgentCursorEdit
{
    /// <summary>Rewrite, insert or fix.</summary>
    public AgentCursorEditKind Kind { get; set; }

    /// <summary>The block the edit works on; for an insert, the block the new one goes after (empty: the end).</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>The new text of a rewrite, or the text of an inserted block.</summary>
    public string? Text { get; set; }

    /// <summary>What an inserted block is (a paragraph by default).</summary>
    public AgentCursorBlockKind InsertKind { get; set; } = AgentCursorBlockKind.Paragraph;

    /// <summary>The id of an inserted block; one is made up when empty.</summary>
    public string? NewBlockId { get; set; }

    /// <summary>The exact word a fix replaces (the first whole-word match in the block).</summary>
    public string? Word { get; set; }

    /// <summary>The word a fix puts in its place.</summary>
    public string? Replacement { get; set; }

    /// <summary>What the status line says while the edit runs ("Tightening the summary"); a default per kind when empty.</summary>
    public string? Description { get; set; }

    /// <summary>A rewrite: selects block <paramref name="blockId"/> and streams in <paramref name="text"/>.</summary>
    public static AgentCursorEdit Rewrite(string blockId, string text, string? description = null) =>
        new() { Kind = AgentCursorEditKind.Rewrite, BlockId = blockId, Text = text, Description = description };

    /// <summary>An insert: adds a <paramref name="kind"/> block with <paramref name="text"/> after block <paramref name="afterBlockId"/>.</summary>
    public static AgentCursorEdit Insert(string afterBlockId, AgentCursorBlockKind kind, string text, string? newBlockId = null,
        string? description = null) =>
        new()
        {
            Kind = AgentCursorEditKind.Insert, BlockId = afterBlockId, InsertKind = kind, Text = text, NewBlockId = newBlockId,
            Description = description
        };

    /// <summary>A fix: replaces <paramref name="word"/> in block <paramref name="blockId"/> with <paramref name="replacement"/>.</summary>
    public static AgentCursorEdit Fix(string blockId, string word, string replacement, string? description = null) =>
        new() { Kind = AgentCursorEditKind.Fix, BlockId = blockId, Word = word, Replacement = replacement, Description = description };
}

/// <summary>A person in an <see cref="AgentCursor"/> document, drawn as a second cursor with their initials and first name.</summary>
public sealed class AgentCursorCollaborator
{
    /// <summary>Creates an empty collaborator; set <see cref="Name"/>.</summary>
    public AgentCursorCollaborator()
    {
    }

    /// <summary>Creates a collaborator with a name and, optionally, a cursor color.</summary>
    public AgentCursorCollaborator(string name, Color? color = null)
    {
        Name = name;
        Color = color;
    }

    /// <summary>The person's full name; the cursor shows the first name, the avatars the initials.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A photo for the header avatar; initials when null or when it fails to load.</summary>
    public ImageSource? Source { get; set; }

    /// <summary>The cursor's color; the theme's second series color when null.</summary>
    public Color? Color { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Arguments of <see cref="AgentCursor.Accepted"/> and <see cref="AgentCursor.Undone"/>: the document after the change.</summary>
public sealed class AgentCursorDocumentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentCursorDocumentEventArgs(RoutedEvent routedEvent, IReadOnlyList<AgentCursorBlock> blocks)
        : base(routedEvent)
    {
        Blocks = blocks;
    }

    /// <summary>The whole document after the change (new block objects). Store it and pass it back as <see cref="AgentCursor.Blocks"/>.</summary>
    public IReadOnlyList<AgentCursorBlock> Blocks { get; }
}

/// <summary>Arguments of <see cref="AgentCursor.PhaseChanged"/>.</summary>
public sealed class AgentCursorPhaseChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentCursorPhaseChangedEventArgs(RoutedEvent routedEvent, AgentCursorPhase oldPhase, AgentCursorPhase phase)
        : base(routedEvent)
    {
        OldPhase = oldPhase;
        Phase = phase;
    }

    /// <summary>The phase before.</summary>
    public AgentCursorPhase OldPhase { get; }

    /// <summary>The phase now.</summary>
    public AgentCursorPhase Phase { get; }
}
