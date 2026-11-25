// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetSession.ts: SnippetSession.adjustWhitespace,
// adjustSelection, createEditsAndSnippetsFromSelections and createEditsAndSnippetsFromEdits, commit b57d5c9e), MIT
// license. Differences: see SnippetSession.cs.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Snippet;

internal sealed partial class SnippetSession
{
    /// <summary>
    /// Normalizes the indentation of the snippet's text for the line at <paramref name="position"/> (and indents its
    /// lines like that line when <paramref name="adjustIndentation"/>); returns the line's leading whitespace.
    /// </summary>
    public static string AdjustWhitespace(TextModel model, TextPosition position, bool adjustIndentation, TextmateSnippet snippet, ISet<Marker>? filter = null)
    {
        var line = model.GetLineContent(position.LineNumber);
        var lineLeadingWhitespace = SnippetStrings.GetLeadingWhitespace(line, 0, position.Column - 1);

        // the snippet as inserted
        string? snippetTextString = null;

        snippet.Walk(marker =>
        {
            // all text elements that are not inside choice
            if (marker is not Text text || marker.Parent is Choice)
            {
                return true;
            }

            // check with filter (iff provided)
            if (filter is not null && !filter.Contains(marker))
            {
                return true;
            }

            var lines = SnippetStrings.SplitLines(text.Value);

            if (adjustIndentation)
            {
                // adjust indentation of snippet test
                // -the snippet-start doesn't get extra-indented (lineLeadingWhitespace), only normalized
                // -all N+1 lines get extra-indented and normalized
                // -the text start get extra-indented and normalized when following a linebreak
                var offset = snippet.Offset(marker);
                if (offset == 0)
                {
                    // snippet start
                    lines[0] = model.NormalizeIndentation(lines[0]);
                }
                else
                {
                    // check if text start is after a linebreak
                    snippetTextString ??= snippet.ToString();
                    var prevChar = offset - 1 < snippetTextString.Length ? snippetTextString[offset - 1] : '\0';
                    if (prevChar is '\n' or '\r')
                    {
                        lines[0] = model.NormalizeIndentation(lineLeadingWhitespace + lines[0]);
                    }
                }

                for (var i = 1; i < lines.Length; i++)
                {
                    lines[i] = model.NormalizeIndentation(lineLeadingWhitespace + lines[i]);
                }
            }

            var newValue = string.Join(model.GetEOL(), lines);
            if (newValue != text.Value)
            {
                marker.Parent!.Replace(marker, [new Text(newValue)]);
                snippetTextString = null;
            }

            return true;
        });

        return lineLeadingWhitespace;
    }

    /// <summary>Grows the selection around its position by the characters to overwrite before and after it.</summary>
    public static TextSelection AdjustSelection(TextModel model, TextSelection selection, int overwriteBefore, int overwriteAfter)
    {
        if (overwriteBefore != 0 || overwriteAfter != 0)
        {
            // overwrite[Before|After] is compute using the position, not the whole
            // selection. therefore we adjust the selection around that position
            var positionLineNumber = selection.PositionLineNumber;
            var positionColumn = selection.PositionColumn;
            var positionColumnBefore = positionColumn - overwriteBefore;
            var positionColumnAfter = positionColumn + overwriteAfter;

            var range = model.ValidateRange(new TextRange(
                positionLineNumber,
                positionColumnBefore,
                positionLineNumber,
                positionColumnAfter));

            selection = TextSelection.FromRange(range, selection.Direction);
        }

        return selection;
    }

    /// <summary>The edits and snippets that insert <paramref name="template"/> at every selection.</summary>
    public static (List<SingleEditOperation> Edits, List<OneSnippet> Snippets) CreateEditsAndSnippetsFromSelections(
        ISnippetEditor editor,
        string template,
        int overwriteBefore,
        int overwriteAfter,
        bool enforceFinalTabstop,
        bool adjustWhitespace,
        string? clipboardText,
        IOvertypingCapturer? overtypingCapturer)
    {
        var model = editor.Model;
        var services = editor.VariableServices;

        var modelBasedVariableResolver = new ModelBasedVariableResolver(services.LabelService, model);
        string? ReadClipboardText() => clipboardText;

        // know what text the overwrite[Before|After] extensions
        // of the primary cursor have selected because only when
        // secondary selections extend to the same text we can grow them
        var primary = editor.GetSelection();
        var firstBeforeText = model.GetValueInRange(AdjustSelection(model, primary, overwriteBefore, 0));
        var firstAfterText = model.GetValueInRange(AdjustSelection(model, primary, 0, overwriteAfter));

        // remember the first non-whitespace column to decide if
        // `keepWhitespace` should be overruled for secondary selections
        var firstLineFirstNonWhitespace = model.GetLineFirstNonWhitespaceColumn(primary.PositionLineNumber);

        // sort selections by their start position but remeber
        // the original index. that allows you to create correct
        // offset-based selection logic without changing the
        // primary selection
        var indexedSelections = editor.GetSelections()
            .Select((selection, idx) => (Selection: selection, Idx: idx))
            .Order(Comparer<(TextSelection Selection, int Idx)>.Create((a, b) => TextRange.CompareRangesUsingStarts(a.Selection, b.Selection)))
            .ToList();

        var edits = new SingleEditOperation[indexedSelections.Count];
        var snippets = new OneSnippet[indexedSelections.Count];
        var timeBasedVariableResolver = new TimeBasedVariableResolver(services.TimeProvider);
        var workspaceBasedVariableResolver = new WorkspaceBasedVariableResolver(services.WorkspaceService);
        var randomBasedVariableResolver = new RandomBasedVariableResolver(services.Random);

        foreach (var (selection, idx) in indexedSelections)
        {
            // extend selection with the `overwriteBefore` and `overwriteAfter` and then
            // compare if this matches the extensions of the primary selection
            var extensionBefore = AdjustSelection(model, selection, overwriteBefore, 0);
            var extensionAfter = AdjustSelection(model, selection, 0, overwriteAfter);
            if (firstBeforeText != model.GetValueInRange(extensionBefore))
            {
                extensionBefore = selection;
            }

            if (firstAfterText != model.GetValueInRange(extensionAfter))
            {
                extensionAfter = selection;
            }

            // merge the before and after selection into one
            var snippetSelection = selection
                .SetStartPosition(extensionBefore.StartLineNumber, extensionBefore.StartColumn)
                .SetEndPosition(extensionAfter.EndLineNumber, extensionAfter.EndColumn);

            var snippet = new SnippetParser().Parse(template, true, enforceFinalTabstop);

            // adjust the template string to match the indentation and
            // whitespace rules of this insert location (can be different for each cursor)
            // happens when being asked for (default) or when this is a secondary
            // cursor and the leading whitespace is different
            var start = snippetSelection.Range.Start;
            var snippetLineLeadingWhitespace = AdjustWhitespace(
                model,
                start,
                adjustWhitespace || (idx > 0 && firstLineFirstNonWhitespace != model.GetLineFirstNonWhitespaceColumn(selection.PositionLineNumber)),
                snippet);

            snippet.ResolveVariables(new CompositeSnippetVariableResolver(
            [
                modelBasedVariableResolver,
                new ClipboardBasedVariableResolver(ReadClipboardText, idx, indexedSelections.Count, editor.MultiCursorPaste == MultiCursorPaste.Spread),
                new SelectionBasedVariableResolver(model, selection, idx, overtypingCapturer, editor.LanguageConfigurationService),
                new CommentBasedVariableResolver(model, selection, editor.LanguageConfigurationService),
                timeBasedVariableResolver,
                workspaceBasedVariableResolver,
                randomBasedVariableResolver,
            ]));

            // store snippets with the index of their originating selection.
            // that ensures the primary cursor stays primary despite not being
            // the one with lowest start position
            edits[idx] = EditOperation.Replace(snippetSelection, snippet.ToString()) with
            {
                // mark the edit so only our undo edits will be used to generate end cursors
                Identifier = new SingleEditOperationIdentifier(idx, 0),
                IsTracked = true,
            };
            snippets[idx] = new OneSnippet(editor, snippet, snippetLineLeadingWhitespace);
        }

        return (edits.ToList(), snippets.ToList());
    }

    /// <summary>The edits and the one snippet that apply snippet <paramref name="snippetEdits"/> to their ranges.</summary>
    public static (List<SingleEditOperation> Edits, List<OneSnippet> Snippets) CreateEditsAndSnippetsFromEdits(
        ISnippetEditor editor,
        IReadOnlyList<SnippetEdit> snippetEdits,
        bool enforceFinalTabstop,
        bool adjustWhitespace,
        string? clipboardText,
        IOvertypingCapturer? overtypingCapturer)
    {
        if (snippetEdits.Count == 0)
        {
            return ([], []);
        }

        var edits = new List<SingleEditOperation>();
        var model = editor.Model;
        var services = editor.VariableServices;

        var parser = new SnippetParser();
        var snippet = new TextmateSnippet();

        var modelBasedVariableResolver = new ModelBasedVariableResolver(services.LabelService, model);
        var timeBasedVariableResolver = new TimeBasedVariableResolver(services.TimeProvider);
        var workspaceBasedVariableResolver = new WorkspaceBasedVariableResolver(services.WorkspaceService);
        var randomBasedVariableResolver = new RandomBasedVariableResolver(services.Random);
        string? ReadClipboardText() => clipboardText;
        var clipboardSpread = editor.MultiCursorPaste == MultiCursorPaste.Spread;

        // keep caller's original index so $CURSOR_INDEX/$CURSOR_NUMBER reflect input order, not range-sorted order
        var indexedSnippetEdits = snippetEdits
            .Select((edit, idx) => (Edit: edit, Idx: idx))
            .Order(Comparer<(SnippetEdit Edit, int Idx)>.Create((a, b) => TextRange.CompareRangesUsingStarts(a.Edit.Range, b.Edit.Range)))
            .ToList();

        var offset = 0;
        for (var i = 0; i < indexedSnippetEdits.Count; i++)
        {
            var (edit, idx) = indexedSnippetEdits[i];
            var range = edit.Range;

            // gaps between snippet edits are appended as text nodes. this
            // ensures placeholder-offsets are later correct
            if (i > 0)
            {
                var lastRange = indexedSnippetEdits[i - 1].Edit.Range;
                var textRange = OneSnippet.FromPositions(lastRange.End, range.Start);
                var textNode = new Text(model.GetValueInRange(textRange));
                snippet.AppendChild(textNode);
                offset += textNode.Value.Length;
            }

            // snapshot already-resolved variables so this edit's resolver only touches
            // (a) variables in the newly parsed fragment and (b) clones backfilled by
            // parseFragment into earlier placeholders sharing the same index (#206121)
            var preExistingVariables = new HashSet<Variable>(ReferenceEqualityComparer.Instance);
            snippet.Walk(marker =>
            {
                if (marker is Variable variable)
                {
                    preExistingVariables.Add(variable);
                }

                return true;
            });

            var newNodes = parser.ParseFragment(edit.Template, snippet);
            AdjustWhitespace(
                model,
                range.Start,
                edit.KeepWhitespace is { } keepWhitespace ? !keepWhitespace : adjustWhitespace,
                snippet,
                new HashSet<Marker>(newNodes, ReferenceEqualityComparer.Instance));

            var editSelection = TextSelection.FromRange(range, SelectionDirection.LeftToRight);
            var editResolver = new CompositeSnippetVariableResolver(
            [
                modelBasedVariableResolver,
                new ClipboardBasedVariableResolver(ReadClipboardText, idx, indexedSnippetEdits.Count, clipboardSpread),
                new SelectionBasedVariableResolver(model, editSelection, idx, overtypingCapturer, editor.LanguageConfigurationService),
                new CommentBasedVariableResolver(model, editSelection, editor.LanguageConfigurationService),
                timeBasedVariableResolver,
                workspaceBasedVariableResolver,
                randomBasedVariableResolver,
            ]);

            snippet.Walk(marker =>
            {
                if (marker is Variable variable && !preExistingVariables.Contains(variable))
                {
                    variable.Resolve(editResolver);
                }

                return true;
            });

            var snippetText = snippet.ToString();
            var snippetFragmentText = snippetText[offset..];
            offset = snippetText.Length;

            // make edit
            edits.Add(EditOperation.Replace(range, snippetFragmentText) with
            {
                // mark the edit so only our undo edits will be used to generate end cursors
                Identifier = new SingleEditOperationIdentifier(i, 0),
                IsTracked = true,
            });
        }

        parser.EnsureFinalTabstop(snippet, enforceFinalTabstop, true);

        return (edits, [new OneSnippet(editor, snippet, "")]);
    }
}
