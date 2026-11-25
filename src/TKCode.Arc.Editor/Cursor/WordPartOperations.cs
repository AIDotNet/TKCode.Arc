// Ported from vscode (src/vs/editor/common/cursor/cursorWordOperations.ts: WordPartOperations and enforceDefined,
// commit b57d5c9e), MIT license. See CursorWordOperations.cs for the differences from upstream.

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Word part moves and deletes: the nearest of a word boundary and a camelCase, snake_case or kebab-case one.</summary>
internal static class WordPartOperations
{
    public static TextRange? DeleteWordPartLeft(DeleteWordContext ctx)
    {
        var candidates = EnforceDefined([
            WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordStart),
            WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordEnd),
            WordOperations.DeleteWordPartLeft(ctx.Model, ctx.Selection),
        ]);
        candidates.Sort(TextRange.CompareRangesUsingEnds);
        return candidates.Count > 2 ? candidates[2] : null;
    }

    public static TextRange? DeleteWordPartRight(DeleteWordContext ctx)
    {
        var candidates = EnforceDefined([
            WordOperations.DeleteWordRight(ctx, WordNavigationType.WordStart),
            WordOperations.DeleteWordRight(ctx, WordNavigationType.WordEnd),
            WordOperations.DeleteWordPartRight(ctx.Model, ctx.Selection),
        ]);
        candidates.Sort(TextRange.CompareRangesUsingStarts);
        return candidates.Count > 0 ? candidates[0] : null;
    }

    public static TextPosition MoveWordPartLeft(
        WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position, bool hasMulticursor)
    {
        var candidates = new List<TextPosition>
        {
            WordOperations.MoveWordLeft(wordSeparators, model, position, WordNavigationType.WordStart, hasMulticursor),
            WordOperations.MoveWordLeft(wordSeparators, model, position, WordNavigationType.WordEnd, hasMulticursor),
            WordOperations.MoveWordPartLeft(model, position),
        };
        candidates.Sort((a, b) => a.CompareTo(b));
        return candidates[2];
    }

    public static TextPosition MoveWordPartRight(WordCharacterClassifier wordSeparators, ICursorSimpleModel model, TextPosition position)
    {
        var candidates = new List<TextPosition>
        {
            WordOperations.MoveWordRight(wordSeparators, model, position, WordNavigationType.WordStart),
            WordOperations.MoveWordRight(wordSeparators, model, position, WordNavigationType.WordEnd),
            WordOperations.MoveWordPartRight(model, position),
        };
        candidates.Sort((a, b) => a.CompareTo(b));
        return candidates[0];
    }

    private static List<TextRange> EnforceDefined(TextRange?[] arr)
    {
        var result = new List<TextRange>(arr.Length);
        foreach (var el in arr)
        {
            if (el is { } value)
            {
                result.Add(value);
            }
        }
        return result;
    }
}
