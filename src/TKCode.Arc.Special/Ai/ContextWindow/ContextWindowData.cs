using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What a <see cref="ContextWindowItem"/> holds. The grid fills in this order: the system prompt, tool definitions, files
/// and memory, the summary of folded messages, then the conversation.
/// </summary>
public enum ContextWindowCategory
{
    /// <summary>The system prompt and other fixed instructions.</summary>
    System,

    /// <summary>Tool definitions (function schemas, connected servers).</summary>
    Tools,

    /// <summary>Attached files and memory. Compaction never folds them.</summary>
    Files,

    /// <summary>The summary that compaction writes in place of folded messages. It is never folded again.</summary>
    Summary,

    /// <summary>Conversation turns: a message with the tool results and the reply it produced. Compaction folds the older ones.</summary>
    Messages
}

/// <summary>Whether a <see cref="ContextWindow"/> is waiting for input or folding messages into a summary.</summary>
public enum ContextWindowPhase
{
    /// <summary>Nothing is running; the actions are available.</summary>
    Idle,

    /// <summary>Older messages are being folded into a summary; every action and the model menu are off until it lands.</summary>
    Summarizing
}

/// <summary>Why a <see cref="ContextWindow"/> compacted.</summary>
public enum ContextWindowCompactReason
{
    /// <summary>Someone pressed Compact now (or code called <see cref="ContextWindow.Compact"/>).</summary>
    Manual,

    /// <summary>A message, a file or an added item took the window past its threshold.</summary>
    Threshold,

    /// <summary>A switch to a smaller model left the window past its threshold.</summary>
    ModelSwitch
}

/// <summary>What changed the items of a <see cref="ContextWindow"/>.</summary>
public enum ContextWindowChange
{
    /// <summary>Send message added the next turn.</summary>
    Sent,

    /// <summary>Attach file added the next file.</summary>
    Attached,

    /// <summary>Code added an item with <see cref="ContextWindow.AddItem"/>.</summary>
    Added,

    /// <summary>Compaction folded older messages into the summary.</summary>
    Compacted
}

/// <summary>
/// A model the <see cref="ContextWindow"/> can switch to: its window and the part of it held back for the reply, in
/// tokens, with a name and a mark for the model menu. Data only; replace the object to change a model on screen.
/// </summary>
public sealed class ContextWindowModel
{
    /// <summary>Creates an empty model; set at least <see cref="Name"/> and <see cref="Window"/>.</summary>
    public ContextWindowModel()
    {
    }

    /// <summary>Creates a model.</summary>
    /// <param name="id">A stable identifier for your code.</param>
    /// <param name="name">The display name.</param>
    /// <param name="window">The context window in tokens.</param>
    /// <param name="reserve">Tokens held back for the reply (the outlined cells at the end of the grid).</param>
    /// <param name="mark">A 24 × 24 stroked glyph shown before the name (for example a Lucide icon), or null for none.</param>
    /// <param name="markBrush">The mark's color, or null for the text color.</param>
    public ContextWindowModel(string id, string name, long window, long reserve, Geometry? mark = null, IBrush? markBrush = null)
    {
        Id = id;
        Name = name;
        Window = window;
        Reserve = reserve;
        Mark = mark;
        MarkBrush = markBrush;
    }

    /// <summary>A stable identifier for your code; the control does not show it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name in the pill and the menu.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The context window in tokens. Each of the 200 cells is a two-hundredth of it.</summary>
    public long Window { get; set; }

    /// <summary>Tokens held back for the reply, drawn as outlined cells at the end of the grid. Keep it equal to the largest output you request.</summary>
    public long Reserve { get; set; }

    /// <summary>A 24 × 24 stroked glyph drawn at 16px before the name (for example an <c>ArcIcons</c> member); null shows no mark.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the text color, which follows the theme.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// One thing in the context window: the system prompt, a tool definition, a file, the summary or a conversation turn,
/// with its size in tokens. Items are grouped by <see cref="Category"/> in the grid, so their order matters only within a
/// category. Changing a property updates the card at once.
/// </summary>
public sealed class ContextWindowItem : AvaloniaObject
{
    /// <summary>Defines the <see cref="Id"/> property.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ContextWindowItem, string>(nameof(Id), string.Empty);

    /// <summary>Defines the <see cref="Category"/> property.</summary>
    public static readonly StyledProperty<ContextWindowCategory> CategoryProperty =
        AvaloniaProperty.Register<ContextWindowItem, ContextWindowCategory>(nameof(Category), ContextWindowCategory.Messages);

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ContextWindowItem, string>(nameof(Label), string.Empty);

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly StyledProperty<long> TokensProperty =
        AvaloniaProperty.Register<ContextWindowItem, long>(nameof(Tokens), coerce: (_, value) => Math.Max(0, value));

    /// <summary>Creates an empty conversation item.</summary>
    public ContextWindowItem()
    {
    }

    /// <summary>Creates an item.</summary>
    /// <param name="category">What it holds.</param>
    /// <param name="label">A short label (a file path, a tool name, the first words of a message).</param>
    /// <param name="tokens">Its size in tokens.</param>
    /// <param name="id">A stable identifier for your code, or null for none.</param>
    public ContextWindowItem(ContextWindowCategory category, string label, long tokens, string? id = null)
    {
        Category = category;
        Label = label;
        Tokens = tokens;
        Id = id ?? string.Empty;
    }

    /// <summary>A stable identifier for your code (the card names the items it creates "turn-1", "file-1" and "summary").</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>What the item holds; it decides the item's color and its place in the grid.</summary>
    public ContextWindowCategory Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

    /// <summary>A short label: a file path, a tool name or the first words of a message. The card itself shows labels only in its status line.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The size in tokens (never negative).</summary>
    public long Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <inheritdoc />
    public override string ToString() => $"{Category}: {Label} ({Tokens})";
}

/// <summary>
/// A turn that Send message adds (a message with the tool results and the reply it produced), or a file that Attach file
/// adds. Send and Attach take them in order and start over after the last.
/// </summary>
public sealed class ContextWindowTurn
{
    /// <summary>Creates an empty turn.</summary>
    public ContextWindowTurn()
    {
    }

    /// <summary>Creates a turn.</summary>
    /// <param name="label">The message (shown quoted in the status line) or the file path.</param>
    /// <param name="tokens">Its size in tokens.</param>
    public ContextWindowTurn(string label, long tokens)
    {
        Label = label;
        Tokens = tokens;
    }

    /// <summary>The message, shown quoted in the status line, or the file path.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The size in tokens.</summary>
    public long Tokens { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Label} ({Tokens})";
}
