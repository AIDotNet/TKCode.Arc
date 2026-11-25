// Ported from vscode (src/vs/editor/common/cursor/cursor.ts: CommandExecutor, IExecContext, ICommandData and
// ICommandsData, commit b57d5c9e), MIT license.
//
// Differences from upstream: the executor is a file of its own (upstream keeps it at the end of cursor.ts, next to the
// cursors controller); "Ignoring commands" is reported to UnexpectedErrors where upstream writes console.warn.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>
/// Runs one command per cursor as one edit: collects their edit operations, drops the cursors whose operations overlap
/// another cursor's, applies the rest as one undo-able edit and asks each command where its cursor goes.
/// </summary>
internal static class CommandExecutor
{
    /// <summary>
    /// Executes <paramref name="commands"/> (one per selection in <paramref name="selectionsBefore"/>, null for none) and
    /// returns the selections after them, without the cursors that lost; null when nothing was edited.
    /// </summary>
    public static List<TextSelection>? ExecuteCommands(
        TextModel model,
        IReadOnlyList<TextSelection> selectionsBefore,
        IReadOnlyList<ICommand?> commands,
        TextModelEditSource? editReason = null)
    {
        var ctx = new ExecContext(model, selectionsBefore);

        var result = InnerExecuteCommands(ctx, commands, editReason ?? EditSources.Unknown("executeCommands"));

        for (int i = 0, len = ctx.TrackedRanges.Count; i < len; i++)
        {
            ctx.Model.SetTrackedRange(ctx.TrackedRanges[i], null, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges);
        }

        return result;
    }

    private static List<TextSelection>? InnerExecuteCommands(ExecContext ctx, IReadOnlyList<ICommand?> commands, TextModelEditSource editReason)
    {
        if (ArrayIsEmpty(commands))
        {
            return null;
        }

        var commandsData = GetEditOperations(ctx, commands);
        if (commandsData.Operations.Count == 0)
        {
            return null;
        }

        var rawOperations = commandsData.Operations;

        var loserCursorsMap = GetLoserCursorMap(rawOperations);
        if (loserCursorsMap.Contains(0))
        {
            // These commands are very messed up
            UnexpectedErrors.Report(new InvalidOperationException("Ignoring commands"));
            return null;
        }

        // Remove operations belonging to losing cursors
        var filteredOperations = new List<SingleEditOperation>();
        for (int i = 0, len = rawOperations.Count; i < len; i++)
        {
            if (!loserCursorsMap.Contains(rawOperations[i].Identifier!.Value.Major))
            {
                filteredOperations.Add(rawOperations[i]);
            }
        }

        // TODO@Alex: find a better way to do this.
        // give the hint that edit operations are tracked to the model
        if (commandsData.HadTrackedEditOperation && filteredOperations.Count > 0)
        {
            filteredOperations[0] = filteredOperations[0] with { IsTracked = true };
        }

        var selectionsAfter = ctx.Model.PushEditOperations(ctx.SelectionsBefore, filteredOperations, inverseEditOperations =>
        {
            var groupedInverseEditOperations = new List<ValidEditOperation>[ctx.SelectionsBefore.Count];
            for (var i = 0; i < ctx.SelectionsBefore.Count; i++)
            {
                groupedInverseEditOperations[i] = new List<ValidEditOperation>();
            }

            foreach (var op in inverseEditOperations)
            {
                if (op.Identifier is null)
                {
                    // perhaps auto whitespace trim edits
                    continue;
                }

                groupedInverseEditOperations[op.Identifier.Value.Major].Add(op);
            }

            var cursorSelections = new TextSelection[ctx.SelectionsBefore.Count];
            for (var i = 0; i < ctx.SelectionsBefore.Count; i++)
            {
                if (groupedInverseEditOperations[i].Count > 0)
                {
                    // upstream's sort is stable; minors are unique per command, so any sort gives the same order
                    groupedInverseEditOperations[i].Sort(static (a, b) => a.Identifier!.Value.Minor - b.Identifier!.Value.Minor);
                    cursorSelections[i] = commands[i]!.ComputeCursorState(ctx.Model, new CursorStateComputerData(ctx, groupedInverseEditOperations[i]));
                }
                else
                {
                    cursorSelections[i] = ctx.SelectionsBefore[i];
                }
            }

            return cursorSelections;
        }, null, editReason);

        var result = new List<TextSelection>(selectionsAfter ?? ctx.SelectionsBefore);

        // Extract losing cursors, sorted descending, and remove them
        var losingCursors = loserCursorsMap.ToList();
        losingCursors.Sort(static (a, b) => b - a);
        foreach (var losingCursor in losingCursors)
        {
            result.RemoveAt(losingCursor);
        }

        return result;
    }

    private static bool ArrayIsEmpty(IReadOnlyList<ICommand?> commands)
    {
        for (int i = 0, len = commands.Count; i < len; i++)
        {
            if (commands[i] is not null)
            {
                return false;
            }
        }

        return true;
    }

    private static CommandsData GetEditOperations(ExecContext ctx, IReadOnlyList<ICommand?> commands)
    {
        var operations = new List<SingleEditOperation>();
        var hadTrackedEditOperation = false;

        for (int i = 0, len = commands.Count; i < len; i++)
        {
            var command = commands[i];
            if (command is not null)
            {
                var r = GetEditOperationsFromCommand(ctx, i, command);
                operations.AddRange(r.Operations);
                hadTrackedEditOperation = hadTrackedEditOperation || r.HadTrackedEditOperation;
            }
        }

        return new CommandsData(operations, hadTrackedEditOperation);
    }

    private static CommandsData GetEditOperationsFromCommand(ExecContext ctx, int majorIdentifier, ICommand command)
    {
        // This method acts as a transaction, if the command fails
        // everything it has done is ignored
        var builder = new EditOperationBuilder(ctx, majorIdentifier, command.InsertsAutoWhitespace);

        try
        {
            command.GetEditOperations(ctx.Model, builder);
        }
        catch (Exception e)
        {
            // TODO@Alex use notification service if this should be user facing
            UnexpectedErrors.Report(e);
            return new CommandsData([], false);
        }

        return new CommandsData(builder.Operations, builder.HadTrackedEditOperation);
    }

    private static HashSet<int> GetLoserCursorMap(List<SingleEditOperation> source)
    {
        // This is destructive on the array
        var operations = new List<SingleEditOperation>(source);

        // Sort operations with last one first (upstream's sort is stable: the index breaks ties)
        var indexed = operations.Select((op, index) => (op, index)).ToList();
        indexed.Sort(static (a, b) =>
        {
            // Note the minus!
            var r = -TextRange.CompareRangesUsingEnds(a.op.Range, b.op.Range);
            return r != 0 ? r : a.index - b.index;
        });
        operations = indexed.Select(static x => x.op).ToList();

        // Operations can not overlap!
        var loserCursorsMap = new HashSet<int>();

        for (var i = 1; i < operations.Count; i++)
        {
            var previousOp = operations[i - 1];
            var currentOp = operations[i];

            if (previousOp.Range.Start.IsBefore(currentOp.Range.End))
            {
                int loserMajor;

                if (previousOp.Identifier!.Value.Major > currentOp.Identifier!.Value.Major)
                {
                    // previousOp loses the battle
                    loserMajor = previousOp.Identifier!.Value.Major;
                }
                else
                {
                    loserMajor = currentOp.Identifier!.Value.Major;
                }

                loserCursorsMap.Add(loserMajor);

                for (var j = 0; j < operations.Count; j++)
                {
                    if (operations[j].Identifier!.Value.Major == loserMajor)
                    {
                        operations.RemoveAt(j);
                        if (j < i)
                        {
                            i--;
                        }

                        j--;
                    }
                }

                if (i > 0)
                {
                    i--;
                }
            }
        }

        return loserCursorsMap;
    }

    private sealed class ExecContext
    {
        public ExecContext(TextModel model, IReadOnlyList<TextSelection> selectionsBefore)
        {
            Model = model;
            SelectionsBefore = selectionsBefore;
        }

        public TextModel Model { get; }

        public IReadOnlyList<TextSelection> SelectionsBefore { get; }

        public List<string> TrackedRanges { get; } = new();

        public List<SelectionDirection> TrackedRangesDirection { get; } = new();
    }

    private readonly record struct CommandsData(List<SingleEditOperation> Operations, bool HadTrackedEditOperation);

    private sealed class EditOperationBuilder : IEditOperationBuilder
    {
        private readonly ExecContext _ctx;
        private readonly int _majorIdentifier;
        private readonly bool _insertsAutoWhitespace;
        private int _operationMinor;

        public EditOperationBuilder(ExecContext ctx, int majorIdentifier, bool insertsAutoWhitespace)
        {
            _ctx = ctx;
            _majorIdentifier = majorIdentifier;
            _insertsAutoWhitespace = insertsAutoWhitespace;
        }

        public List<SingleEditOperation> Operations { get; } = new();

        public bool HadTrackedEditOperation { get; private set; }

        public void AddEditOperation(TextRange range, string? text, bool forceMoveMarkers = false)
        {
            if (range.IsEmpty && text == string.Empty)
            {
                // This command wants to add a no-op => no thank you
                return;
            }

            Operations.Add(new SingleEditOperation(range, text, forceMoveMarkers)
            {
                Identifier = new SingleEditOperationIdentifier(_majorIdentifier, _operationMinor++),
                IsAutoWhitespaceEdit = _insertsAutoWhitespace,
            });
        }

        public void AddTrackedEditOperation(TextRange range, string? text, bool forceMoveMarkers = false)
        {
            HadTrackedEditOperation = true;
            AddEditOperation(range, text, forceMoveMarkers);
        }

        public string TrackSelection(TextSelection selection, bool? trackPreviousOnEmpty = null)
        {
            TrackedRangeStickiness stickiness;
            if (selection.IsEmpty)
            {
                if (trackPreviousOnEmpty is { } previous)
                {
                    stickiness = previous
                        ? TrackedRangeStickiness.GrowsOnlyWhenTypingBefore
                        : TrackedRangeStickiness.GrowsOnlyWhenTypingAfter;
                }
                else
                {
                    // Try to lock it with surrounding text
                    var maxLineColumn = _ctx.Model.GetLineMaxColumn(selection.StartLineNumber);
                    stickiness = selection.StartColumn == maxLineColumn
                        ? TrackedRangeStickiness.GrowsOnlyWhenTypingBefore
                        : TrackedRangeStickiness.GrowsOnlyWhenTypingAfter;
                }
            }
            else
            {
                stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges;
            }

            var l = _ctx.TrackedRanges.Count;
            var id = _ctx.Model.SetTrackedRange(null, selection.Range, stickiness)!;
            _ctx.TrackedRanges.Add(id);
            _ctx.TrackedRangesDirection.Add(selection.Direction);
            return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed class CursorStateComputerData : ICursorStateComputerData
    {
        private readonly ExecContext _ctx;
        private readonly List<ValidEditOperation> _inverseEditOperations;

        public CursorStateComputerData(ExecContext ctx, List<ValidEditOperation> inverseEditOperations)
        {
            _ctx = ctx;
            _inverseEditOperations = inverseEditOperations;
        }

        public IReadOnlyList<ValidEditOperation> GetInverseEditOperations() => _inverseEditOperations;

        public TextSelection GetTrackedSelection(string id)
        {
            var idx = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture);
            var range = _ctx.Model.GetTrackedRange(_ctx.TrackedRanges[idx])!.Value;
            if (_ctx.TrackedRangesDirection[idx] == SelectionDirection.LeftToRight)
            {
                return new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn);
            }

            return new TextSelection(range.EndLineNumber, range.EndColumn, range.StartLineNumber, range.StartColumn);
        }
    }
}
