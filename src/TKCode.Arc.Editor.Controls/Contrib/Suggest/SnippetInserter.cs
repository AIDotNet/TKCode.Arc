// The seam through which the suggest controller inserts a completion (upstream: SnippetController2.get(editor).insert
// in suggestController.ts _insertSuggestion, commit b57d5c9e, MIT license). The snippets feature implements
// ISnippetInserter over its SnippetController2 (tab stops, placeholders, choices, variables, a snippet session); until
// then PlainTextSnippetInserter inserts the snippet's text without the syntax and selects its first tab stop.
//
// SuggestController.SnippetInserter defaults to SnippetControllerSuggestInserter, which goes through the editor's
// snippet controller (Contrib/Snippet, ISnippetInserter) and falls back to PlainTextSnippetInserter when the editor has
// none.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Snippet;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>How a snippet goes in (upstream's ISnippetInsertOptions as the suggest controller fills it).</summary>
/// <param name="OverwriteBefore">Characters left of each cursor the snippet replaces.</param>
/// <param name="OverwriteAfter">Characters right of each cursor the snippet replaces.</param>
/// <param name="UndoStopBefore">Whether to push an undo stop before the edit.</param>
/// <param name="UndoStopAfter">Whether to push an undo stop after the edit.</param>
/// <param name="AdjustWhitespace">Whether lines after the first get the indentation of the line they go into.</param>
/// <param name="ClipboardText">The text of the CLIPBOARD variable.</param>
internal sealed record SnippetInsertParams(
    int OverwriteBefore,
    int OverwriteAfter,
    bool UndoStopBefore,
    bool UndoStopAfter,
    bool AdjustWhitespace,
    string? ClipboardText);

/// <summary>Inserts a completion's snippet at every cursor of an editor (the snippets feature's SnippetController2.insert).</summary>
internal interface ISuggestSnippetInserter
{
    void Insert(CodeEditor editor, string template, SnippetInsertParams options);
}

/// <summary>Inserts through the editor's snippet controller (tab stops, placeholders, choices), or as plain text without one.</summary>
internal sealed class SnippetControllerSuggestInserter : ISuggestSnippetInserter
{
    public static readonly SnippetControllerSuggestInserter Instance = new();

    public void Insert(CodeEditor editor, string template, SnippetInsertParams options)
    {
        if (SnippetInserter.Get(editor) is not { } snippets)
        {
            PlainTextSnippetInserter.Instance.Insert(editor, template, options);
            return;
        }

        snippets.Insert(template, new SnippetInsertOptions
        {
            OverwriteBefore = options.OverwriteBefore,
            OverwriteAfter = options.OverwriteAfter,
            UndoStopBefore = options.UndoStopBefore,
            UndoStopAfter = options.UndoStopAfter,
            AdjustWhitespace = options.AdjustWhitespace,
            ClipboardText = options.ClipboardText,
        });
    }
}

/// <summary>Inserts a snippet's text without its syntax and selects its first tab stop: the fallback without a snippets feature.</summary>
internal sealed class PlainTextSnippetInserter : ISuggestSnippetInserter
{
    public static readonly PlainTextSnippetInserter Instance = new();

    public void Insert(CodeEditor editor, string template, SnippetInsertParams options)
    {
        var model = editor.Model;
        var selections = editor.GetSelections();
        if (selections.Count == 0)
        {
            return;
        }

        var plain = PlainSnippet.Parse(template, options.ClipboardText);
        var edits = new List<SingleEditOperation>(selections.Count);
        var texts = new List<string>(selections.Count);
        var starts = new List<int>(selections.Count);
        foreach (var selection in selections)
        {
            // upstream's SnippetSession: the selection grown by overwriteBefore / overwriteAfter on its line
            var start = selection.Range.Start;
            var end = selection.Range.End;
            var startColumn = Math.Max(1, start.Column - options.OverwriteBefore);
            var endColumn = Math.Min(model.GetLineMaxColumn(end.LineNumber), end.Column + options.OverwriteAfter);
            var range = new TextRange(start.LineNumber, startColumn, end.LineNumber, endColumn);

            var (text, selectionStart) = options.AdjustWhitespace
                ? AdjustWhitespace(model, new TextPosition(start.LineNumber, startColumn), plain)
                : (plain.Text, plain.SelectionStart);
            edits.Add(EditOperation.Replace(range, text));
            texts.Add(text);
            starts.Add(selectionStart);
        }

        if (options.UndoStopBefore)
        {
            editor.PushUndoStop();
        }

        editor.ExecuteEdits("snippet", edits, inverse =>
        {
            var result = new List<TextSelection>(inverse.Count);
            for (var i = 0; i < inverse.Count && i < texts.Count; i++)
            {
                var from = inverse[i].Range.Start;
                var a = PositionAt(from, texts[i], starts[i]);
                var b = PositionAt(from, texts[i], starts[i] + plain.SelectionLength);
                result.Add(new TextSelection(a.LineNumber, a.Column, b.LineNumber, b.Column));
            }
            return result;
        });

        if (options.UndoStopAfter)
        {
            editor.PushUndoStop();
        }
    }

    // lines after the first get the leading whitespace of the line the snippet goes into (upstream's adjustWhitespace,
    // without normalizing the snippet's own indentation)
    private static (string Text, int SelectionStart) AdjustWhitespace(TextModel model, TextPosition position, PlainSnippet plain)
    {
        var line = model.GetLineContent(position.LineNumber);
        var end = Math.Min(line.Length, position.Column - 1);
        var i = 0;
        while (i < end && line[i] is ' ' or '\t')
        {
            i++;
        }
        var indent = line[..i];
        if (indent.Length == 0 || !plain.Text.Contains('\n'))
        {
            return (plain.Text, plain.SelectionStart);
        }

        var sb = new System.Text.StringBuilder(plain.Text.Length + 16);
        var selectionStart = plain.SelectionStart;
        for (var k = 0; k < plain.Text.Length; k++)
        {
            var ch = plain.Text[k];
            sb.Append(ch);
            if (ch == '\n')
            {
                sb.Append(indent);
                if (k < plain.SelectionStart)
                {
                    selectionStart += indent.Length;
                }
            }
        }
        return (sb.ToString(), selectionStart);
    }

    private static TextPosition PositionAt(TextPosition start, string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var line = start.LineNumber;
        var column = start.Column;
        for (var k = 0; k < offset; k++)
        {
            if (text[k] == '\n')
            {
                line++;
                column = 1;
            }
            else if (text[k] != '\r')
            {
                column++;
            }
        }
        return new TextPosition(line, column);
    }
}
