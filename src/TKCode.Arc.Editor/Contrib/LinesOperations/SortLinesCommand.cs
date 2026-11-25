// Ported from vscode (src/vs/editor/contrib/linesOperations/browser/sortLinesCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - Intl.Collator's compare (the default locale and options) is the current culture's string comparison;
//  - the sort is stable (OrderBy), as Array.prototype.sort is.

using System.Globalization;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.LinesOperations;

/// <summary>Sorts the lines of a selection.</summary>
internal sealed class SortLinesCommand : ICommand
{
    private static readonly StringComparer s_collator = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.None);

    private readonly TextSelection _selection;
    private readonly bool _descending;
    private string? _selectionId;

    public SortLinesCommand(TextSelection selection, bool descending)
    {
        _selection = selection;
        _descending = descending;
        _selectionId = null;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var op = SortLines(model, _selection, _descending);
        if (op is { } edit)
        {
            builder.AddEditOperation(edit.Range, edit.Text);
        }

        _selectionId = builder.TrackSelection(_selection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) =>
        helper.GetTrackedSelection(_selectionId!);

    /// <summary>Whether sorting the lines of <paramref name="selection"/> changes them.</summary>
    public static bool CanRun(TextModel? model, TextSelection selection, bool descending)
    {
        if (model is null)
        {
            return false;
        }

        var data = GetSortData(model, selection, descending);

        if (data is null)
        {
            return false;
        }

        for (int i = 0, len = data.Value.Before.Count; i < len; i++)
        {
            if (data.Value.Before[i] != data.Value.After[i])
            {
                return true;
            }
        }

        return false;
    }

    private static (int StartLineNumber, int EndLineNumber, List<string> Before, List<string> After)? GetSortData(TextModel model, TextSelection selection, bool descending)
    {
        var startLineNumber = selection.StartLineNumber;
        var endLineNumber = selection.EndLineNumber;

        if (selection.EndColumn == 1)
        {
            endLineNumber--;
        }

        // Nothing to sort if user didn't select anything.
        if (startLineNumber >= endLineNumber)
        {
            return null;
        }

        var linesToSort = new List<string>();

        // Get the contents of the selection to be sorted.
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            linesToSort.Add(model.GetLineContent(lineNumber));
        }

        var sorted = linesToSort.OrderBy(static line => line, s_collator).ToList();

        // If descending, reverse the order.
        if (descending)
        {
            sorted.Reverse();
        }

        return (startLineNumber, endLineNumber, linesToSort, sorted);
    }

    /// <summary>Generate commands for sorting lines on a model.</summary>
    private static SingleEditOperation? SortLines(TextModel model, TextSelection selection, bool descending)
    {
        var data = GetSortData(model, selection, descending);

        if (data is not { } d)
        {
            return null;
        }

        return EditOperation.Replace(
            new TextRange(d.StartLineNumber, 1, d.EndLineNumber, model.GetLineMaxColumn(d.EndLineNumber)),
            string.Join('\n', d.After));
    }
}
