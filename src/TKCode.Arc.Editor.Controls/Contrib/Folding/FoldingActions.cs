// Ported from vscode (src/vs/editor/contrib/folding/browser/folding.ts: FoldingAction and the folding actions, commit
// b57d5c9e), MIT license.
// Differences:
//  - the arguments are a FoldingArguments record (upstream: an object checked by foldingArgumentsConstraint);
//  - an action does not wait for an update that waits for its delay: it runs it at once;
//  - the regular expression of the block comment and region marker fallbacks are .NET expressions (the region marker
//    is the language configuration's start marker itself);
//  - no menus, telemetry or the _executeFoldingRangeProvider command.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;
using static TKCode.Arc.Editor.Contrib.Folding.FoldingModelFunctions;

namespace TKCode.Arc;

/// <summary>The arguments of the fold and unfold actions (upstream's FoldingArguments).</summary>
/// <param name="Levels">How many levels to fold or unfold.</param>
/// <param name="Direction"><c>up</c> or <c>down</c> (the default).</param>
/// <param name="SelectionLines">The 0-based start lines to act on instead of the selections.</param>
internal sealed record FoldingArguments(int? Levels = null, string? Direction = null, IReadOnlyList<int>? SelectionLines = null);

/// <summary>An action on the folding model (upstream's FoldingAction).</summary>
internal abstract class FoldingAction : EditorAction
{
    protected FoldingAction(string id, string label, params EditorKeybinding[] keybindings)
        : base(id, label, null, CodeEditorContextKeys.FoldingEnabled, keybindings)
    {
    }

    /// <summary>Does the action's work on the folding model.</summary>
    protected abstract void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args);

    public override async Task Run(CodeEditor editor, object? args)
    {
        if (FoldingController.Get(editor) is not { } foldingController)
        {
            return;
        }

        if (foldingController.GetFoldingModelNow() is { } foldingModelPromise)
        {
            if (await foldingModelPromise.ConfigureAwait(true) is { } foldingModel)
            {
                Invoke(foldingController, foldingModel, editor, args);
                if (editor.GetSelection() is { } selection)
                {
                    foldingController.Reveal(selection.Range.Start);
                }
            }
        }
    }

    protected static List<int> GetSelectedLines(CodeEditor editor) => [.. editor.GetSelections().Select(static s => s.StartLineNumber)];

    protected static List<int> GetLineNumbers(object? args, CodeEditor editor)
    {
        if (args is FoldingArguments { SelectionLines: { } selectionLines })
        {
            return [.. selectionLines.Select(static l => l + 1)]; // to 1-based line numbers
        }

        return GetSelectedLines(editor);
    }

    // the keybindings of the folding actions: when the text has focus (upstream's kbExpr)
    protected static EditorKeybinding Key(int primary, int? mac = null) => new(primary)
    {
        When = EditorContextKeys.EditorTextFocus,
        Mac = mac is { } m ? new PlatformKeybindings { Primary = m } : null,
    };

    protected static EditorKeybinding Chord(int second) => Key(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, second));

    // an action with no default keybinding (upstream: kbOpts with no primary)
    protected static EditorKeybinding[] NoKey => [];
}

/// <summary>The folding actions (upstream's registerEditorAction calls of folding.ts).</summary>
internal static class FoldingActions
{
    public static readonly EditorAction Unfold = new UnfoldAction();
    public static readonly EditorAction UnfoldRecursively = new UnFoldRecursivelyAction();
    public static readonly EditorAction Fold = new FoldAction();
    public static readonly EditorAction FoldRecursively = new FoldRecursivelyAction();
    public static readonly EditorAction ToggleFoldRecursively = new ToggleFoldRecursivelyAction();
    public static readonly EditorAction FoldAll = new FoldAllAction();
    public static readonly EditorAction UnfoldAll = new UnfoldAllAction();
    public static readonly EditorAction FoldAllBlockComments = new FoldAllBlockCommentsAction();
    public static readonly EditorAction FoldAllRegions = new FoldAllRegionsAction();
    public static readonly EditorAction UnfoldAllRegions = new UnfoldAllRegionsAction();
    public static readonly EditorAction FoldAllExcept = new FoldAllExceptAction();
    public static readonly EditorAction UnfoldAllExcept = new UnfoldAllExceptAction();
    public static readonly EditorAction ToggleFold = new ToggleFoldAction();
    public static readonly EditorAction GotoParentFold = new GotoParentFoldAction();
    public static readonly EditorAction GotoPreviousFold = new GotoPreviousFoldAction();
    public static readonly EditorAction GotoNextFold = new GotoNextFoldAction();
    public static readonly EditorAction FoldRangeFromSelection = new FoldRangeFromSelectionAction();
    public static readonly EditorAction RemoveFoldRangeFromSelection = new RemoveFoldRangeFromSelectionAction();
    public static readonly EditorAction ToggleImportFold = new ToggleImportFoldAction();

    /// <summary>Fold Level 1 to 7 (upstream's FoldLevelAction instances).</summary>
    public static readonly IReadOnlyList<EditorAction> FoldLevels = [.. Enumerable.Range(1, 7).Select(static i => (EditorAction)new FoldLevelAction(i))];

    /// <summary>Every folding action, in upstream's registration order.</summary>
    public static IReadOnlyList<EditorCommand> All { get; } =
    [
        Unfold, UnfoldRecursively, Fold, FoldRecursively, ToggleFoldRecursively, FoldAll, UnfoldAll, FoldAllBlockComments,
        FoldAllRegions, UnfoldAllRegions, FoldAllExcept, UnfoldAllExcept, ToggleFold, GotoParentFold, GotoPreviousFold,
        GotoNextFold, FoldRangeFromSelection, RemoveFoldRangeFromSelection, ToggleImportFold, .. FoldLevels,
    ];

    private sealed class UnfoldAction() : FoldingAction(
        "editor.unfold", "Unfold",
        Key(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.BracketRight, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.BracketRight))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var arguments = args as FoldingArguments;
            var levels = arguments?.Levels is { } l && l != 0 ? l : 1;
            var lineNumbers = GetLineNumbers(args, editor);
            if (arguments?.Direction == "up")
            {
                SetCollapseStateLevelsUp(foldingModel, false, levels, lineNumbers);
            }
            else
            {
                SetCollapseStateLevelsDown(foldingModel, false, levels, lineNumbers);
            }
        }
    }

    private sealed class UnFoldRecursivelyAction() : FoldingAction(
        "editor.unfoldRecursively", "Unfold Recursively",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.BracketRight))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateLevelsDown(foldingModel, false, int.MaxValue, GetSelectedLines(editor));
    }

    private sealed class FoldAction() : FoldingAction(
        "editor.fold", "Fold",
        Key(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.BracketLeft, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.BracketLeft))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var arguments = args as FoldingArguments;
            var lineNumbers = GetLineNumbers(args, editor);
            var levels = arguments?.Levels;
            var direction = arguments?.Direction;
            if (levels is null && direction is null)
            {
                // fold the region at the location or if already collapsed, the first uncollapsed parent instead.
                SetCollapseStateUp(foldingModel, true, lineNumbers);
            }
            else if (direction == "up")
            {
                SetCollapseStateLevelsUp(foldingModel, true, levels is { } up && up != 0 ? up : 1, lineNumbers);
            }
            else
            {
                SetCollapseStateLevelsDown(foldingModel, true, levels is { } down && down != 0 ? down : 1, lineNumbers);
            }
        }
    }

    private sealed class ToggleFoldAction() : FoldingAction(
        "editor.toggleFold", "Toggle Fold",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.KeyL))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            ToggleCollapseState(foldingModel, 1, GetSelectedLines(editor));
    }

    private sealed class FoldRecursivelyAction() : FoldingAction(
        "editor.foldRecursively", "Fold Recursively",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.BracketLeft))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateLevelsDown(foldingModel, true, int.MaxValue, GetSelectedLines(editor));
    }

    private sealed class ToggleFoldRecursivelyAction() : FoldingAction(
        "editor.toggleFoldRecursively", "Toggle Fold Recursively",
        Chord(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyL))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            ToggleCollapseState(foldingModel, int.MaxValue, GetSelectedLines(editor));
    }

    private sealed class FoldAllBlockCommentsAction() : FoldingAction(
        "editor.foldAllBlockComments", "Fold All Block Comments",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Slash))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            if (foldingModel.Regions.HasTypes())
            {
                SetCollapseStateForType(foldingModel, FoldingRangeKind.Comment.Value, true);
            }
            else
            {
                var comments = editor.LanguageConfigurationService.GetLanguageConfiguration(editor.Model.GetLanguageId()).Comments;
                if (comments?.BlockCommentStartToken is { Length: > 0 } blockCommentStartToken)
                {
                    var regExp = new Regex("^\\s*" + Regex.Escape(blockCommentStartToken));
                    SetCollapseStateForMatchingLines(foldingModel, regExp, true);
                }
            }
        }
    }

    private sealed class FoldAllRegionsAction() : FoldingAction(
        "editor.foldAllMarkerRegions", "Fold All Regions",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Digit8))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetRegionsState(foldingModel, editor, true);
    }

    private sealed class UnfoldAllRegionsAction() : FoldingAction(
        "editor.unfoldAllMarkerRegions", "Unfold All Regions",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Digit9))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetRegionsState(foldingModel, editor, false);
    }

    // Fold All Regions and Unfold All Regions: by type, else by the language's start marker
    private static void SetRegionsState(FoldingModel foldingModel, CodeEditor editor, bool doCollapse)
    {
        if (foldingModel.Regions.HasTypes())
        {
            SetCollapseStateForType(foldingModel, FoldingRangeKind.Region.Value, doCollapse);
        }
        else
        {
            var foldingRules = editor.LanguageConfigurationService.GetLanguageConfiguration(editor.Model.GetLanguageId()).FoldingRules;
            if (foldingRules?.Markers?.Start is { } start)
            {
                SetCollapseStateForMatchingLines(foldingModel, start, doCollapse);
            }
        }
    }

    private sealed class FoldAllExceptAction() : FoldingAction(
        "editor.foldAllExcept", "Fold All Except Selected",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Minus))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateForRest(foldingModel, true, GetSelectedLines(editor));
    }

    private sealed class UnfoldAllExceptAction() : FoldingAction(
        "editor.unfoldAllExcept", "Unfold All Except Selected",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Equal))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateForRest(foldingModel, false, GetSelectedLines(editor));
    }

    private sealed class FoldAllAction() : FoldingAction(
        "editor.foldAll", "Fold All",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Digit0))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateLevelsDown(foldingModel, true);
    }

    private sealed class UnfoldAllAction() : FoldingAction(
        "editor.unfoldAll", "Unfold All",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.KeyJ))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateLevelsDown(foldingModel, false);
    }

    private sealed class FoldLevelAction(int level) : FoldingAction(
        IdPrefix + level, "Fold Level " + level,
        Chord(KeyMod.CtrlCmd | ((int)KeyCode.Digit0 + level)))
    {
        private const string IdPrefix = "editor.foldLevel";

        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args) =>
            SetCollapseStateAtLevel(foldingModel, level, true, GetSelectedLines(editor));
    }

    /// <summary>Puts the cursor at the start of a line (the go-to actions).</summary>
    private static void GoToLine(CodeEditor editor, int? startLineNumber)
    {
        if (startLineNumber is { } line)
        {
            editor.SetSelection(new TextSelection(line, 1, line, 1));
        }
    }

    private sealed class GotoParentFoldAction() : FoldingAction("editor.gotoParentFold", "Go to Parent Fold", NoKey)
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var selectedLines = GetSelectedLines(editor);
            if (selectedLines.Count > 0)
            {
                GoToLine(editor, GetParentFoldLine(selectedLines[0], foldingModel));
            }
        }
    }

    private sealed class GotoPreviousFoldAction() : FoldingAction("editor.gotoPreviousFold", "Go to Previous Folding Range", NoKey)
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var selectedLines = GetSelectedLines(editor);
            if (selectedLines.Count > 0)
            {
                GoToLine(editor, GetPreviousFoldLine(selectedLines[0], foldingModel));
            }
        }
    }

    private sealed class GotoNextFoldAction() : FoldingAction("editor.gotoNextFold", "Go to Next Folding Range", NoKey)
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var selectedLines = GetSelectedLines(editor);
            if (selectedLines.Count > 0)
            {
                GoToLine(editor, GetNextFoldLine(selectedLines[0], foldingModel));
            }
        }
    }

    private sealed class FoldRangeFromSelectionAction() : FoldingAction(
        "editor.createFoldingRangeFromSelection", "Create Folding Range from Selection",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Comma))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var collapseRanges = new List<FoldRange>();
            var selections = editor.GetSelections();
            if (selections.Count == 0)
            {
                return;
            }

            foreach (var selection in selections)
            {
                var endLineNumber = selection.EndLineNumber;
                if (selection.EndColumn == 1)
                {
                    --endLineNumber;
                }

                if (endLineNumber > selection.StartLineNumber)
                {
                    collapseRanges.Add(new FoldRange(selection.StartLineNumber, endLineNumber, null, true, FoldSource.UserDefined));
                    editor.SetSelection(new TextSelection(selection.StartLineNumber, 1, selection.StartLineNumber, 1));
                }
            }

            if (collapseRanges.Count > 0)
            {
                collapseRanges.Sort(static (a, b) => a.StartLineNumber - b.StartLineNumber);
                var newRanges = FoldingRegions.SanitizeAndMerge(foldingModel.Regions, collapseRanges, editor.Model.GetLineCount());
                foldingModel.UpdatePost(FoldingRegions.FromFoldRanges(newRanges));
            }
        }
    }

    private sealed class RemoveFoldRangeFromSelectionAction() : FoldingAction(
        "editor.removeManualFoldingRanges", "Remove Manual Folding Ranges",
        Chord(KeyMod.CtrlCmd | (int)KeyCode.Period))
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var selections = editor.GetSelections();
            if (selections.Count > 0)
            {
                foldingModel.RemoveManualRanges([.. selections.Select(static s => s.Range)]);
                foldingController.TriggerFoldingModelChanged();
            }
        }
    }

    private sealed class ToggleImportFoldAction() : FoldingAction("editor.toggleImportFold", "Toggle Import Fold", NoKey)
    {
        protected override void Invoke(FoldingController foldingController, FoldingModel foldingModel, CodeEditor editor, object? args)
        {
            var regionsToToggle = new List<FoldingRegion>();
            var regions = foldingModel.Regions;
            for (var i = regions.Length - 1; i >= 0; i--)
            {
                if (regions.GetType(i) == FoldingRangeKind.Imports.Value)
                {
                    regionsToToggle.Add(regions.ToRegion(i));
                }
            }

            foldingModel.ToggleCollapseState(regionsToToggle);
            foldingController.TriggerFoldingModelChanged();
        }
    }
}
