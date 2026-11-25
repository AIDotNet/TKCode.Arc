// CodeEditor.InsertSnippet: the public entry to the snippet feature (upstream: the editor.action.insertSnippet command
// of src/vs/workbench/contrib/snippets/browser/commands/insertSnippet.ts, which reads the clipboard and calls
// SnippetController2.insert; commit b57d5c9e), MIT license. The snippet controller is
// Contrib/Snippet/CodeEditorSnippetController.cs.

using Avalonia.Controls;
using Avalonia.Input.Platform;
using TKCode.Arc.Editor.Contrib.Snippet;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    private List<(string Template, string? ClipboardText)>? _pendingSnippets;

    /// <summary>
    /// Inserts a snippet at every cursor and starts snippet mode: the snippet's tab stops (<c>$1</c>, <c>${2:default}</c>,
    /// <c>${3|one,two|}</c>) are visited with Tab and Shift+Tab, mirrors of a tab stop change with it, Escape or moving
    /// the cursor away ends it. The snippet uses the TextMate syntax of Visual Studio Code's snippets, variables
    /// (<c>$TM_SELECTED_TEXT</c>, <c>$CLIPBOARD</c>, <c>$CURRENT_YEAR</c>, ...) and transforms included; its lines are
    /// indented like the line it is inserted on. Inserting while in snippet mode nests the snippet in the current tab
    /// stop. A snippet that uses <c>$CLIPBOARD</c> is inserted once the clipboard's text is read; one inserted before the
    /// editor is shown is inserted when it is.
    /// </summary>
    /// <param name="snippet">The snippet, for example <c>for (let ${1:i} = 0; $1 &lt; ${2:n}; $1++) {\n\t$0\n}</c>.</param>
    public void InsertSnippet(string snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        if (SnippetParser.GuessNeedsClipboard(snippet) && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = InsertSnippetWithClipboardAsync(snippet, clipboard);
            return;
        }

        InsertSnippetCore(snippet, null);
    }

    private async Task InsertSnippetWithClipboardAsync(string snippet, IClipboard clipboard)
    {
        string? text = null;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            // an unreadable clipboard leaves $CLIPBOARD empty, as upstream's clipboard service does
        }

        InsertSnippetCore(snippet, text);
    }

    private void InsertSnippetCore(string snippet, string? clipboardText)
    {
        if (CodeEditorSnippetController.Get(this) is { } controller)
        {
            controller.Insert(snippet, new SnippetInsertOptions { ClipboardText = clipboardText });
        }
        else if (HasContributions)
        {
            // an editor built without the snippet feature (tests): the snippet's text alone
            SnippetInserter.InsertOrPlainText(this, snippet);
        }
        else
        {
            (_pendingSnippets ??= []).Add((snippet, clipboardText));
        }
    }

    /// <summary>The snippets inserted before the snippet controller existed, in order; the controller inserts them.</summary>
    internal IReadOnlyList<(string Template, string? ClipboardText)> TakePendingSnippets()
    {
        var pending = _pendingSnippets;
        _pendingSnippets = null;
        return pending ?? [];
    }
}
