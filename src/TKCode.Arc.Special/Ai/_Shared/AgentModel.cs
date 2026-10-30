using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The model or agent behind a run in the AI components (<c>AgentStatus</c>, <c>AgentPlan</c>, <c>AgentTimeline</c>):
/// a display name and an optional mark. Data only; set it once per run.
/// </summary>
public sealed class AgentModel
{
    /// <summary>Creates an empty model; set <see cref="Name"/> before showing it.</summary>
    public AgentModel()
    {
    }

    /// <summary>Creates a model with a name and, optionally, a mark and its color.</summary>
    /// <param name="name">The display name, such as the model's name.</param>
    /// <param name="mark">A 24 × 24 stroked glyph (for example a Lucide icon), or null for the default mark.</param>
    /// <param name="markBrush">The mark's color, or null for the text color.</param>
    public AgentModel(string name, Geometry? mark = null, IBrush? markBrush = null)
    {
        Name = name;
        Mark = mark;
        MarkBrush = markBrush;
    }

    /// <summary>The display name, such as the model's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A 24 × 24 stroked glyph drawn as the model's mark (for example a Lucide icon); null draws the default mark.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the text color, which follows the theme.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
