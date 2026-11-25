// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/colorizedBracketPairsDecorationProvider.ts,
// commit b57d5c9e), MIT license.
//
// Differences: the provider is created with the model's bracket pairs part (which is created when first asked for, see
// TextModel.GetBracketPairs) and reads the colorization options from the model on each query; the decoration options
// of each class name are created once and shared. The theming participant (the CSS color of each class name) is the
// view's: it maps the class names to the theme's editorBracketHighlight colors (ColorProvider.GetBracketColor).

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Decorates each bracket with a class name by its nesting level (bracket pair colorization).</summary>
internal sealed class ColorizedBracketPairsDecorationProvider : IDisposable
{
    private readonly TextModel _textModel;
    private readonly BracketPairsTextModelPart _bracketPairs;
    private readonly Action _onBracketPairsChanged;

    public ColorizedBracketPairsDecorationProvider(TextModel textModel, BracketPairsTextModelPart bracketPairs, Action onDidChange)
    {
        _textModel = textModel;
        _bracketPairs = bracketPairs;
        _onBracketPairsChanged = onDidChange;
        _bracketPairs.DidChange += _onBracketPairsChanged;
    }

    private BracketPairColorizationOptions ColorizationOptions => _textModel.GetOptions().BracketPairColorizationOptions;

    public List<ModelDecoration> GetDecorationsInRange(TextRange range, bool onlyMinimapDecorations = false)
    {
        if (onlyMinimapDecorations)
        {
            // Bracket pair colorization decorations are not rendered in the minimap
            return [];
        }

        var options = ColorizationOptions;
        if (!options.Enabled)
        {
            return [];
        }

        var result = new List<ModelDecoration>();
        _bracketPairs.GetBracketsInRange(range, true).ForEach(bracket =>
        {
            result.Add(new ModelDecoration(
                $"bracket{bracket.Range}-{bracket.NestingLevel}",
                0,
                bracket.Range,
                ColorProvider.GetDecorationOptions(bracket, options.IndependentColorPoolPerBracketType)));
        });
        return result;
    }

    public List<ModelDecoration> GetAllDecorations()
    {
        if (!ColorizationOptions.Enabled)
        {
            return [];
        }

        return GetDecorationsInRange(new TextRange(1, 1, _textModel.GetLineCount(), 1));
    }

    public void Dispose() => _bracketPairs.DidChange -= _onBracketPairsChanged;
}

/// <summary>The class names of colorized brackets.</summary>
internal static class ColorProvider
{
    public const string UnexpectedClosingBracketClassName = "unexpected-closing-bracket";

    /// <summary>The prefix of a nesting level's class name; the level (0-29) follows it.</summary>
    public const string LevelClassNamePrefix = "bracket-highlighting-";

    // To support a dynamic amount of colors up to 6 colors, we use a number that is a lcm of all numbers from 1 to 6.
    public const int LevelCount = 30;

    private static readonly ModelDecorationOptions s_unexpected = Create(UnexpectedClosingBracketClassName);
    private static readonly ModelDecorationOptions[] s_levels =
        Enumerable.Range(0, LevelCount).Select(level => Create(GetInlineClassNameOfLevel(level))).ToArray();

    public static string GetInlineClassName(BracketInfo bracket, bool independentColorPoolPerBracketType)
    {
        if (bracket.IsInvalid)
        {
            return UnexpectedClosingBracketClassName;
        }

        return GetInlineClassNameOfLevel(independentColorPoolPerBracketType ? bracket.NestingLevelOfEqualBracketType : bracket.NestingLevel);
    }

    public static string GetInlineClassNameOfLevel(int level) => LevelClassNamePrefix + (level % LevelCount);

    internal static ModelDecorationOptions GetDecorationOptions(BracketInfo bracket, bool independentColorPoolPerBracketType)
    {
        if (bracket.IsInvalid)
        {
            return s_unexpected;
        }

        var level = independentColorPoolPerBracketType ? bracket.NestingLevelOfEqualBracketType : bracket.NestingLevel;
        return s_levels[level % LevelCount];
    }

    private static ModelDecorationOptions Create(string inlineClassName) =>
        new("BracketPairColorization") { InlineClassName = inlineClassName };
}
