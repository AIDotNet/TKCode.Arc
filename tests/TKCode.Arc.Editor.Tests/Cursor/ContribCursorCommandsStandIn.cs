// Stand-in for the cursor commands of vscode's editor contributions that are not ported yet, commit b57d5c9e, MIT
// license: InsertCursorAbove and InsertCursorBelow (src/vs/editor/contrib/multicursor/browser/multicursor.ts) and
// ExpandLineSelection (src/vs/editor/contrib/lineSelection/browser/lineSelection.ts). Each keeps the body of upstream's
// run method on an IViewModel, so that the tests can run CursorMoveCommands the way those commands do. The core
// commands (coreCommands.ts) are the engine's CoreNavigationCommands and CoreEditingCommands.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal static class ContribCursorCommandsStandIn
{
    /// <summary>ExpandLineSelection (contrib/lineSelection/browser/lineSelection.ts).</summary>
    public static void ExpandLineSelection(IViewModel viewModel)
    {
        viewModel.Model.PushStackElement();
        viewModel.SetCursorStates(null, CursorChangeReason.Explicit, CursorMoveCommands.ExpandLineSelection(viewModel, viewModel.GetCursorStates()));
    }

    /// <summary>InsertCursorAbove (contrib/multicursor/browser/multicursor.ts).</summary>
    public static void InsertCursorAbove(IViewModel viewModel, bool logicalLine = false)
    {
        viewModel.Model.PushStackElement();
        viewModel.SetCursorStates(null, CursorChangeReason.Explicit, CursorMoveCommands.AddCursorUp(viewModel, viewModel.GetCursorStates(), logicalLine));
        viewModel.RevealTopMostCursor(null);
    }

    /// <summary>InsertCursorBelow (contrib/multicursor/browser/multicursor.ts).</summary>
    public static void InsertCursorBelow(IViewModel viewModel, bool logicalLine = false)
    {
        viewModel.Model.PushStackElement();
        viewModel.SetCursorStates(null, CursorChangeReason.Explicit, CursorMoveCommands.AddCursorDown(viewModel, viewModel.GetCursorStates(), logicalLine));
        viewModel.RevealBottomMostCursor(null);
    }
}
