// Ported from vscode (src/vs/editor/common/editorCommon.ts: IEditOperationBuilder, ICursorStateComputerData and ICommand,
// commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Commands;

/// <summary>A builder and helper for edit operations for a command.</summary>
internal interface IEditOperationBuilder
{
    /// <summary>Adds a replace operation; <paramref name="range"/> may be empty (insert), <paramref name="text"/> null (delete).</summary>
    void AddEditOperation(TextRange range, string? text, bool forceMoveMarkers = false);

    /// <summary>
    /// Adds a replace operation whose inverse <see cref="ICursorStateComputerData.GetInverseEditOperations"/> returns.
    /// </summary>
    void AddTrackedEditOperation(TextRange range, string? text, bool forceMoveMarkers = false);

    /// <summary>
    /// Tracks <paramref name="selection"/> while the edits are applied: it does not grow if that can be helped, and an
    /// empty one clamps to a nearby character (the previous one when <paramref name="trackPreviousOnEmpty"/> is true,
    /// the next one when false, the one the line end suggests when null). Returns an id for
    /// <see cref="ICursorStateComputerData.GetTrackedSelection"/>.
    /// </summary>
    string TrackSelection(TextSelection selection, bool? trackPreviousOnEmpty = null);
}

/// <summary>A helper for computing the cursor state after a command.</summary>
internal interface ICursorStateComputerData
{
    /// <summary>The inverse of the tracked edit operations the command added, in the order it added them.</summary>
    IReadOnlyList<ValidEditOperation> GetInverseEditOperations();

    /// <summary>A selection tracked with <see cref="IEditOperationBuilder.TrackSelection"/>.</summary>
    TextSelection GetTrackedSelection(string id);
}

/// <summary>A command that modifies text and cursor state on a model.</summary>
internal interface ICommand
{
    /// <summary>The command inserts automatic whitespace that should be trimmed if possible.</summary>
    bool InsertsAutoWhitespace => false;

    /// <summary>Collects the edit operations the command needs.</summary>
    void GetEditOperations(TextModel model, IEditOperationBuilder builder);

    /// <summary>Computes the cursor state after the edit operations were applied.</summary>
    TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper);
}
