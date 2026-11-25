// Ported from vscode (src/vs/editor/common/viewModel/viewModelDecoration.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - viewModel.ts has the same ViewModelDecoration class; this is the one both refer to;
//  - the module functions isModelDecorationVisible, isModelDecorationInComment and isModelDecorationInString are static
//    members of ViewModelDecoration and take the TextModel (upstream's ITextModel).

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>A model decoration with its range in view coordinates.</summary>
internal sealed class ViewModelDecoration
{
    public ViewModelDecoration(TextRange range, ModelDecorationOptions options)
    {
        Range = range;
        Options = options;
    }

    public TextRange Range { get; }

    public ModelDecorationOptions Options { get; }

    public static bool IsModelDecorationVisible(TextModel model, IModelDecoration decoration)
    {
        if (decoration.Options.HideInCommentTokens && IsModelDecorationInComment(model, decoration))
        {
            return false;
        }

        if (decoration.Options.HideInStringTokens && IsModelDecorationInString(model, decoration))
        {
            return false;
        }

        return true;
    }

    public static bool IsModelDecorationInComment(TextModel model, IModelDecoration decoration) =>
        TestTokensInRange(model, decoration.Range, tokenType => tokenType == StandardTokenType.Comment);

    public static bool IsModelDecorationInString(TextModel model, IModelDecoration decoration) =>
        TestTokensInRange(model, decoration.Range, tokenType => tokenType == StandardTokenType.String);

    /// <summary>
    /// Calls the callback for every token that intersects the range. If the callback returns <c>false</c>, iteration
    /// stops and <c>false</c> is returned. Otherwise, <c>true</c> is returned.
    /// </summary>
    private static bool TestTokensInRange(TextModel model, TextRange range, Func<StandardTokenType, bool> callback)
    {
        for (var lineNumber = range.StartLineNumber; lineNumber <= range.EndLineNumber; lineNumber++)
        {
            var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
            var isFirstLine = lineNumber == range.StartLineNumber;
            var isEndLine = lineNumber == range.EndLineNumber;

            var tokenIdx = isFirstLine ? lineTokens.FindTokenIndexAtOffset(range.StartColumn - 1) : 0;
            while (tokenIdx < lineTokens.GetCount())
            {
                if (isEndLine)
                {
                    var startOffset = lineTokens.GetStartOffset(tokenIdx);
                    if (startOffset > range.EndColumn - 1)
                    {
                        break;
                    }
                }

                var callbackResult = callback(lineTokens.GetStandardTokenType(tokenIdx));
                if (!callbackResult)
                {
                    return false;
                }

                tokenIdx++;
            }
        }

        return true;
    }
}
