using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What an <see cref="AiSuggestionPop"/> lists.</summary>
internal enum AiSuggestionKind
{
    /// <summary>Commands typed with "/" at the start of a line; titles show with the slash, no group headings.</summary>
    Command,

    /// <summary>Context typed with "@"; rows are grouped under headings.</summary>
    Mention
}

/// <summary>One row of an <see cref="AiSuggestionPop"/>.</summary>
/// <param name="Id">The id of the command or context item (what the owner inserts as a token).</param>
/// <param name="Title">The label without "/" or "@" (the list shows the slash itself for commands).</param>
/// <param name="Hint">The second line (a description, a path, a source); null for a one-line row.</param>
/// <param name="Icon">A 24 × 24 Lucide-style stroke geometry, drawn at 16px; null leaves the icon slot empty.</param>
/// <param name="Group">The heading the row is listed under (mentions only); null for no heading.</param>
internal sealed record AiSuggestionRow(string Id, string Title, string? Hint, Geometry? Icon, string? Group);

/// <summary>Arguments of <see cref="AiSuggestionPop.Accepted"/>: the row the person chose.</summary>
internal sealed class AiSuggestionAcceptedEventArgs : RoutedEventArgs
{
    public AiSuggestionAcceptedEventArgs(RoutedEvent routedEvent, AiSuggestionKind kind, AiSuggestionRow row, int index)
        : base(routedEvent)
    {
        Kind = kind;
        Row = row;
        Index = index;
    }

    /// <summary>Whether the list showed commands or mentions.</summary>
    public AiSuggestionKind Kind { get; }

    /// <summary>The chosen row.</summary>
    public AiSuggestionRow Row { get; }

    /// <summary>Its index in the rows given to <see cref="AiSuggestionPop.Show"/>.</summary>
    public int Index { get; }
}
