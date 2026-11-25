// Ported from vscode (src/vs/editor/common/commands/surroundSelectionCommand.ts, commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Commands;

/// <summary>Wraps a selection with a character before and one after it (typing a quote or bracket over a selection).</summary>
internal sealed class SurroundSelectionCommand : ICommand
{
    private readonly TextSelection _range;
    private readonly string _charBeforeSelection;
    private readonly string _charAfterSelection;

    public SurroundSelectionCommand(TextSelection range, string charBeforeSelection, string charAfterSelection)
    {
        _range = range;
        _charBeforeSelection = charBeforeSelection;
        _charAfterSelection = charAfterSelection;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        builder.AddTrackedEditOperation(
            new TextRange(_range.StartLineNumber, _range.StartColumn, _range.StartLineNumber, _range.StartColumn),
            _charBeforeSelection);

        // AddTrackedEditOperation() ignores us if the text == ''. Causing a chain of errors in ComputeCursorState()
        builder.AddTrackedEditOperation(
            new TextRange(_range.EndLineNumber, _range.EndColumn, _range.EndLineNumber, _range.EndColumn),
            string.IsNullOrEmpty(_charAfterSelection) ? null : _charAfterSelection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var firstOperationRange = inverseEditOperations[0].Range;
        var secondOperationRange = inverseEditOperations[1].Range;
        return new TextSelection(
            firstOperationRange.EndLineNumber,
            firstOperationRange.EndColumn,
            secondOperationRange.EndLineNumber,
            secondOperationRange.EndColumn - _charAfterSelection.Length);
    }
}

/// <summary>A surround selection command that runs after composition finished.</summary>
internal sealed class CompositionSurroundSelectionCommand : ICommand
{
    private readonly TextPosition _position;
    private readonly string _text;
    private readonly string _charAfter;

    public CompositionSurroundSelectionCommand(TextPosition position, string text, string charAfter)
    {
        _position = position;
        _text = text;
        _charAfter = charAfter;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder) =>
        builder.AddTrackedEditOperation(
            new TextRange(_position.LineNumber, _position.Column, _position.LineNumber, _position.Column),
            _text + _charAfter);

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var inverseEditOperations = helper.GetInverseEditOperations();
        var opRange = inverseEditOperations[0].Range;
        return new TextSelection(
            opRange.EndLineNumber,
            opRange.StartColumn,
            opRange.EndLineNumber,
            opRange.EndColumn - _charAfter.Length);
    }
}
