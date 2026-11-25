// Ported from vscode (src/vs/editor/contrib/wordHighlighter/browser/highlightDecorations.ts, commit b57d5c9e), MIT
// license.
// Differences: the overview ruler and minimap colors are theme color ids only (the overview ruler and minimap do not
// draw decorations yet); the CSS rules (.wordHighlight, .selectionHighlight, ...) are decoration styles the control
// registers.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.WordHighlighter;

/// <summary>The decorations of occurrence and selection highlights (upstream's highlightDecorations.ts).</summary>
internal static class HighlightDecorations
{
    /// <summary>The class name of read occurrences.</summary>
    public const string WordHighlightClassName = "wordHighlight";

    /// <summary>The class name of write occurrences.</summary>
    public const string WordHighlightStrongClassName = "wordHighlightStrong";

    /// <summary>The class name of textual occurrences.</summary>
    public const string WordHighlightTextClassName = "wordHighlightText";

    /// <summary>The class name of other occurrences of the selected text.</summary>
    public const string SelectionHighlightClassName = "selectionHighlight";

    private static ModelDecorationOverviewRulerOptions Ruler(string themeColorId) =>
        new() { Color = DecorationColor.FromTheme(themeColorId), Position = OverviewRulerLane.Center };

    private static ModelDecorationMinimapOptions Minimap() =>
        new() { Color = DecorationColor.FromTheme("minimap.selectionOccurrenceHighlight"), Position = MinimapPosition.Inline };

    private static readonly ModelDecorationOptions s_writeOptions = new("word-highlight-strong")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = WordHighlightStrongClassName,
        OverviewRuler = Ruler("editorOverviewRuler.wordHighlightStrongForeground"),
        Minimap = Minimap(),
    };

    private static readonly ModelDecorationOptions s_textOptions = new("word-highlight-text")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = WordHighlightTextClassName,
        OverviewRuler = Ruler("editorOverviewRuler.wordHighlightTextForeground"),
        Minimap = Minimap(),
    };

    private static readonly ModelDecorationOptions s_selectionHighlightOptions = new("selection-highlight-overview")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = SelectionHighlightClassName,
        OverviewRuler = Ruler("editorOverviewRuler.selectionHighlightForeground"),
        Minimap = Minimap(),
    };

    private static readonly ModelDecorationOptions s_selectionHighlightOptionsNoOverview = new("selection-highlight")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = SelectionHighlightClassName,
    };

    private static readonly ModelDecorationOptions s_regularOptions = new("word-highlight")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = WordHighlightClassName,
        OverviewRuler = Ruler("editorOverviewRuler.wordHighlightForeground"),
        Minimap = Minimap(),
    };

    /// <summary>The options of a document highlight of <paramref name="kind"/> (upstream's getHighlightDecorationOptions).</summary>
    public static ModelDecorationOptions GetHighlightDecorationOptions(DocumentHighlightKind? kind) => kind switch
    {
        DocumentHighlightKind.Write => s_writeOptions,
        DocumentHighlightKind.Text => s_textOptions,
        _ => s_regularOptions,
    };

    /// <summary>
    /// The options of a selection highlight; in the overview ruler only when the model has no semantic highlighting
    /// (upstream's getSelectionHighlightDecorationOptions).
    /// </summary>
    public static ModelDecorationOptions GetSelectionHighlightDecorationOptions(bool hasSemanticHighlights) =>
        hasSemanticHighlights ? s_selectionHighlightOptionsNoOverview : s_selectionHighlightOptions;
}
