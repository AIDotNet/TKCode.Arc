using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AiInlineEdit"/> stands.</summary>
public enum AiInlineEditPhase
{
    /// <summary>Reading and selecting text.</summary>
    Idle,

    /// <summary>The instruction field is open for the selected passage.</summary>
    Instructing,

    /// <summary>Waiting for the rewrite.</summary>
    Thinking,

    /// <summary>The rewrite shows as changes to accept or reject.</summary>
    Reviewing
}

/// <summary>What an <see cref="AiInlineEdit"/> asks its <see cref="AiInlineEdit.RewriteHandler"/> to rewrite.</summary>
/// <param name="Text">The whole text, for context.</param>
/// <param name="Start">Where the selected passage starts in <paramref name="Text"/>.</param>
/// <param name="Length">The length of the selected passage.</param>
/// <param name="Selection">The selected passage itself.</param>
/// <param name="Instruction">What the person asked for ("Make it shorter").</param>
public sealed record AiInlineEditRequest(string Text, int Start, int Length, string Selection, string Instruction);

/// <summary>Event data of <see cref="AiInlineEdit.EditApplied"/>: the text before and after, and what was kept.</summary>
public sealed class AiInlineEditAppliedEventArgs(RoutedEvent routedEvent, string oldText, string newText, string instruction,
    int accepted, int rejected) : RoutedEventArgs(routedEvent)
{
    /// <summary>The whole text before the edit.</summary>
    public string OldText { get; } = oldText;

    /// <summary>The whole text after the edit (equal to <see cref="OldText"/> when every change was rejected).</summary>
    public string NewText { get; } = newText;

    /// <summary>The instruction the rewrite followed.</summary>
    public string Instruction { get; } = instruction;

    /// <summary>How many changes were accepted.</summary>
    public int Accepted { get; } = accepted;

    /// <summary>How many changes were rejected.</summary>
    public int Rejected { get; } = rejected;
}

/// <summary>One change of a rewrite under review: the words taken out, the words put in, and the decision so far.</summary>
internal sealed class AiInlineEditHunk(string deleted, string inserted)
{
    public string Deleted { get; } = deleted;

    public string Inserted { get; } = inserted;

    /// <summary>True accepted, false rejected, null not decided yet.</summary>
    public bool? Decision { get; set; }
}

/// <summary>A piece of a reviewed passage: unchanged text, or a change.</summary>
internal readonly record struct AiInlineEditPiece(string? Text, AiInlineEditHunk? Hunk);
