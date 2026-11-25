// The seam other editor features use to insert snippets (upstream: SnippetController2.get(editor).insert(...) as the
// suggest controller, code actions and the insert snippet command call it, commit b57d5c9e), MIT license.
// The suggest feature asks SnippetInserter.Get(editor) for it and falls back to plain text (SnippetParser.AsInsertText)
// when the editor has no snippet controller.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc;

/// <summary>Inserts snippets into a code editor and runs snippet mode (what features need of the snippet controller).</summary>
internal interface ISnippetInserter
{
    /// <summary>Whether the editor is in snippet mode (tab stops to visit).</summary>
    bool IsInSnippet { get; }

    /// <summary>
    /// Inserts <paramref name="template"/> at every cursor, replacing <see cref="SnippetInsertOptions.OverwriteBefore"/>
    /// characters before and <see cref="SnippetInsertOptions.OverwriteAfter"/> after each, or into the current tab stop
    /// when the editor is in snippet mode (a nested snippet). A completion item's snippet goes through here.
    /// </summary>
    void Insert(string template, SnippetInsertOptions? options = null);

    /// <summary>Applies snippet edits (each replaces a range with a snippet) as one snippet session (upstream's apply).</summary>
    void Apply(IReadOnlyList<SnippetEdit> edits, SnippetInsertOptions? options = null);

    /// <summary>Selects the next tab stop (the jumpToNextSnippetPlaceholder command).</summary>
    void Next();

    /// <summary>Selects the previous tab stop.</summary>
    void Prev();

    /// <summary>Ends snippet mode; <paramref name="resetSelection"/> keeps only the primary cursor.</summary>
    void Cancel(bool resetSelection = false);

    /// <summary>
    /// Shows a choice placeholder's options (upstream's showSimpleSuggestions). The suggest feature sets it to show them
    /// in its widget and returns true; when it is null or returns false the snippet controller shows its own list.
    /// </summary>
    Func<ICompletionItemProvider, bool>? ShowSimpleSuggestions { get; set; }
}

/// <summary>Finds the snippet inserter of an editor.</summary>
internal static class SnippetInserter
{
    /// <summary>The editor's snippet controller, or null when it has none (a test editor, or before it is shown).</summary>
    public static ISnippetInserter? Get(CodeEditor editor) => CodeEditorSnippetController.Get(editor);

    /// <summary>
    /// Inserts <paramref name="template"/> as a snippet when the editor has a snippet controller, else its text alone
    /// (the placeholders' defaults, no tab stops) at every cursor.
    /// </summary>
    public static void InsertOrPlainText(CodeEditor editor, string template, SnippetInsertOptions? options = null)
    {
        if (Get(editor) is { } inserter)
        {
            inserter.Insert(template, options);
            return;
        }

        var text = SnippetParser.AsInsertText(template);
        var before = options?.OverwriteBefore ?? 0;
        var after = options?.OverwriteAfter ?? 0;
        var model = editor.Model;
        var edits = new List<SingleEditOperation>();
        foreach (var selection in editor.GetSelections())
        {
            var range = SnippetSession.AdjustSelection(model, selection, before, after);
            edits.Add(new SingleEditOperation(range, text));
        }

        editor.ExecuteEdits("snippet", edits);
    }
}
