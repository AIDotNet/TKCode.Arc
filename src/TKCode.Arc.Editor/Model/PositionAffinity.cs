// Ported from vscode (src/vs/editor/common/model.ts: PositionAffinity, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>Which side a position sticks to where the model and the view disagree (injected text, wrapping).</summary>
internal enum PositionAffinity
{
    /// <summary>Prefers the left most position.</summary>
    Left = 0,

    /// <summary>Prefers the right most position.</summary>
    Right = 1,

    /// <summary>No preference.</summary>
    None = 2,

    /// <summary>If the given position is on injected text, prefers the position left of it.</summary>
    LeftOfInjectedText = 3,

    /// <summary>If the given position is on injected text, prefers the position right of it.</summary>
    RightOfInjectedText = 4,
}
