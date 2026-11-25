// Ported from vscode (src/vs/editor/contrib/bracketMatching/browser/bracketMatching.ts, commit b57d5c9e), MIT license:
// BracketMatchingController's jumpToBracket, selectToBracket, removeBrackets, _recomputeBrackets and _updateBrackets,
// with BracketsData and the two decoration options.
// Differences from upstream:
//  - the commands are static methods on an IEditorActionHost; the brackets the editor shows (_lastBracketsData,
//    _lastVersionId) are the engine class BracketMatchingState, which the control's controller schedules (50 ms after
//    a cursor or content change) and whose decorations it sets;
//  - matchBracket / findEnclosingBrackets get 20 ms as upstream;
//  - the overview ruler color is the theme color editorOverviewRuler.bracketMatchForeground.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;

namespace TKCode.Arc.Editor.Contrib.BracketMatching;

/// <summary>The brackets at a cursor (upstream's BracketsData).</summary>
internal sealed record BracketsData(TextPosition Position, (TextRange Open, TextRange Close)? Brackets, ModelDecorationOptions Options);

/// <summary>Go to bracket, select to bracket and remove brackets, and the matching bracket highlight.</summary>
internal static class BracketMatching
{
    /// <summary>The class name of the matching bracket decoration (upstream's bracket-match).</summary>
    public const string BracketMatchClassName = "bracket-match";

    /// <summary>The inline class name of the matching bracket decoration (upstream's bracket-match-inline).</summary>
    public const string BracketMatchInlineClassName = "bracket-match-inline";

    /// <summary>The overview ruler color of a matching bracket (upstream's editorOverviewRuler.bracketMatchForeground).</summary>
    public const string OverviewRulerBracketMatchForeground = "editorOverviewRuler.bracketMatchForeground";

    internal static readonly TimeSpan s_maxDuration = TimeSpan.FromMilliseconds(20);

    /// <summary>A matching bracket at a cursor, with an overview ruler mark.</summary>
    public static ModelDecorationOptions DecorationOptionsWithOverviewRuler { get; } = new("bracket-match-overview")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = BracketMatchClassName,
        InlineClassName = BracketMatchInlineClassName,
        OverviewRuler = new ModelDecorationOverviewRulerOptions
        {
            Color = DecorationColor.FromTheme(OverviewRulerBracketMatchForeground),
            Position = OverviewRulerLane.Center,
        },
    };

    /// <summary>The enclosing brackets of a cursor (matchBrackets: always), without an overview ruler mark.</summary>
    public static ModelDecorationOptions DecorationOptionsWithoutOverviewRuler { get; } = new("bracket-match-no-overview")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = BracketMatchClassName,
        InlineClassName = BracketMatchInlineClassName,
    };

    private static BracketPairsTextModelPart BracketPairs(IEditorActionHost editor, TextModel model) =>
        model.GetBracketPairs(editor.LanguageConfigurationService);

    /// <summary>BracketMatchingController.jumpToBracket (Ctrl+Shift+\).</summary>
    public static void JumpToBracket(IEditorActionHost editor)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var bracketPairs = BracketPairs(editor, model);
        var newSelections = editor.GetSelections().Select(selection =>
        {
            var position = selection.Range.Start;

            // find matching brackets if position is on a bracket
            var brackets = bracketPairs.MatchBracket(position);
            TextPosition? newCursorPosition = null;
            if (brackets is { } b)
            {
                if (b.Open.ContainsPosition(position) && !b.Close.ContainsPosition(position))
                {
                    newCursorPosition = b.Close.Start;
                }
                else if (b.Close.ContainsPosition(position))
                {
                    newCursorPosition = b.Open.Start;
                }
            }
            else
            {
                // find the enclosing brackets if the position isn't on a matching bracket
                var enclosingBrackets = bracketPairs.FindEnclosingBrackets(position);
                if (enclosingBrackets is { } e)
                {
                    newCursorPosition = e.Close.Start;
                }
                else
                {
                    // no enclosing brackets, try the very first next bracket
                    var nextBracket = bracketPairs.FindNextBracket(position);
                    if (nextBracket is not null)
                    {
                        newCursorPosition = nextBracket.Range.Start;
                    }
                }
            }

            if (newCursorPosition is { } p)
            {
                return new TextSelection(p.LineNumber, p.Column, p.LineNumber, p.Column);
            }
            return new TextSelection(position.LineNumber, position.Column, position.LineNumber, position.Column);
        }).ToList();

        if (newSelections.Count == 0)
        {
            return;
        }

        editor.SetSelections(newSelections, "api");
        editor.RevealRange(newSelections[0]);
    }

    /// <summary>BracketMatchingController.selectToBracket.</summary>
    public static void SelectToBracket(IEditorActionHost editor, bool selectBrackets)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var bracketPairs = BracketPairs(editor, model);
        var newSelections = new List<TextSelection>();

        foreach (var selection in editor.GetSelections())
        {
            var position = selection.Range.Start;
            var brackets = bracketPairs.MatchBracket(position);

            if (brackets is null)
            {
                brackets = bracketPairs.FindEnclosingBrackets(position);
                if (brackets is null)
                {
                    var nextBracket = bracketPairs.FindNextBracket(position);
                    if (nextBracket is not null)
                    {
                        brackets = bracketPairs.MatchBracket(nextBracket.Range.Start);
                    }
                }
            }

            TextPosition? selectFrom = null;
            TextPosition? selectTo = null;

            if (brackets is { } b)
            {
                var (open, close) = TextRange.CompareRangesUsingStarts(b.Open, b.Close) <= 0 ? (b.Open, b.Close) : (b.Close, b.Open);
                selectFrom = selectBrackets ? open.Start : open.End;
                selectTo = selectBrackets ? close.End : close.Start;

                if (close.ContainsPosition(position))
                {
                    // select backwards if the cursor was on the closing bracket
                    (selectFrom, selectTo) = (selectTo, selectFrom);
                }
            }

            if (selectFrom is { } from && selectTo is { } to)
            {
                newSelections.Add(new TextSelection(from.LineNumber, from.Column, to.LineNumber, to.Column));
            }
        }

        if (newSelections.Count > 0)
        {
            editor.SetSelections(newSelections, "api");
            editor.RevealRange(newSelections[0]);
        }
    }

    /// <summary>BracketMatchingController.removeBrackets (Ctrl+Alt+Backspace).</summary>
    public static void RemoveBrackets(IEditorActionHost editor, string? editSource)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var bracketPairs = BracketPairs(editor, model);
        foreach (var selection in editor.GetSelections())
        {
            var position = selection.Position;

            var brackets = bracketPairs.MatchBracket(position) ?? bracketPairs.FindEnclosingBrackets(position);
            if (brackets is { } b)
            {
                editor.PushUndoStop();
                editor.ExecuteEdits(
                    editSource,
                    [
                        new SingleEditOperation(b.Open, ""),
                        new SingleEditOperation(b.Close, ""),
                    ],
                    null);
                editor.PushUndoStop();
            }
        }
    }
}

/// <summary>
/// The brackets the editor highlights at its cursors (upstream's _lastBracketsData and _lastVersionId with
/// _recomputeBrackets and _updateBrackets). The controller calls <see cref="Update"/> and sets the decorations it returns.
/// </summary>
internal sealed class BracketMatchingState
{
    private const int MaxSelections = 100;

    private IReadOnlyList<BracketsData> _lastBracketsData = [];
    private int _lastVersionId;

    /// <summary>The brackets found by the last update, in cursor order.</summary>
    public IReadOnlyList<BracketsData> LastBracketsData => _lastBracketsData;

    /// <summary>Forget the cached brackets (the model, its language configuration or the option changed).</summary>
    public void Reset()
    {
        _lastBracketsData = [];
        _lastVersionId = 0;
    }

    /// <summary>
    /// Recomputes the brackets at the empty selections and returns the decorations to show (upstream's _updateBrackets);
    /// null when <paramref name="matchBrackets"/> is never (the decorations stay as they are).
    /// </summary>
    public List<ModelDeltaDecoration>? Update(IEditorActionHost editor, bool hasWidgetFocus, MatchBracketsMode matchBrackets)
    {
        if (matchBrackets == MatchBracketsMode.Never)
        {
            return null;
        }
        RecomputeBrackets(editor, hasWidgetFocus, matchBrackets);

        var newDecorations = new List<ModelDeltaDecoration>();
        foreach (var bracketData in _lastBracketsData)
        {
            if (bracketData.Brackets is { } brackets)
            {
                newDecorations.Add(new ModelDeltaDecoration(brackets.Open, bracketData.Options));
                newDecorations.Add(new ModelDeltaDecoration(brackets.Close, bracketData.Options));
            }
        }

        return newDecorations;
    }

    private void RecomputeBrackets(IEditorActionHost editor, bool hasWidgetFocus, MatchBracketsMode matchBrackets)
    {
        if (editor.GetModel() is not { } model || !hasWidgetFocus)
        {
            // no model or no focus => no brackets!
            Reset();
            return;
        }

        var selections = editor.GetSelections();
        if (selections.Count > MaxSelections)
        {
            // no bracket matching for high numbers of selections
            Reset();
            return;
        }

        var versionId = model.GetVersionId();
        IReadOnlyList<BracketsData> previousData = [];
        if (_lastVersionId == versionId)
        {
            // use the previous data only if the model is at the same version id
            previousData = _lastBracketsData;
        }

        var positions = new List<TextPosition>();
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];

            if (selection.IsEmpty)
            {
                // will bracket match a cursor only if the selection is collapsed
                positions.Add(selection.Range.Start);
            }
        }

        // sort positions for `previousData` cache hits
        if (positions.Count > 1)
        {
            positions.Sort();
        }

        var bracketPairs = model.GetBracketPairs(editor.LanguageConfigurationService);
        var newData = new List<BracketsData>();
        var previousIndex = 0;
        var previousLen = previousData.Count;
        for (int i = 0, len = positions.Count; i < len; i++)
        {
            var position = positions[i];

            while (previousIndex < previousLen && previousData[previousIndex].Position.IsBefore(position))
            {
                previousIndex++;
            }

            if (previousIndex < previousLen && previousData[previousIndex].Position == position)
            {
                newData.Add(previousData[previousIndex]);
            }
            else
            {
                var brackets = bracketPairs.MatchBracket(position, BracketMatching.s_maxDuration);
                var options = BracketMatching.DecorationOptionsWithOverviewRuler;
                if (brackets is null && matchBrackets == MatchBracketsMode.Always)
                {
                    brackets = bracketPairs.FindEnclosingBrackets(position, BracketMatching.s_maxDuration);
                    options = BracketMatching.DecorationOptionsWithoutOverviewRuler;
                }
                newData.Add(new BracketsData(position, brackets, options));
            }
        }

        _lastBracketsData = newData;
        _lastVersionId = versionId;
    }
}
