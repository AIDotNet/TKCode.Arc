// Ported from vscode (src/vs/editor/contrib/smartSelect/browser/wordSelections.ts, commit b57d5c9e), MIT license; and
// TokenizationTextModelPart.getWordAtPosition with _findLanguageBoundaries (src/vs/editor/common/model/tokens/
// tokenizationTextModelPart.ts), which upstream's model.getWordAtPosition is.
// Differences from upstream:
//  - the word at a position is ModelWords.GetWordAtPosition with the language configuration service given to the
//    constructor (the model here has no language configuration service of its own);
//  - the provider answers synchronously (a completed Task).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.SmartSelect;

/// <summary>Selection ranges from the word at a position: the part of a camelCase / snake_case word, the word, the line, the document.</summary>
internal sealed class WordSelectionRangeProvider : ISelectionRangeProvider
{
    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly bool _selectSubwords;

    public WordSelectionRangeProvider(LanguageConfigurationService languageConfigurationService, bool selectSubwords = true)
    {
        _languageConfigurationService = languageConfigurationService;
        _selectSubwords = selectSubwords;
    }

    public Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token)
    {
        var result = new List<IReadOnlyList<SelectionRange>>();
        foreach (var position in positions)
        {
            var bucket = new List<SelectionRange>();
            result.Add(bucket);
            if (_selectSubwords)
            {
                AddInWordRanges(bucket, model, position);
            }
            AddWordRanges(bucket, model, position);
            AddWhitespaceLine(bucket, model, position);
            bucket.Add(new SelectionRange(model.GetFullModelRange()));
        }
        return Task.FromResult<IReadOnlyList<IReadOnlyList<SelectionRange>>?>(result);
    }

    // _addInWordRanges
    private void AddInWordRanges(List<SelectionRange> bucket, TextModel model, TextPosition pos)
    {
        if (ModelWords.GetWordAtPosition(model, _languageConfigurationService, pos) is not { } obj)
        {
            return;
        }

        var word = obj.Word;
        var startColumn = obj.StartColumn;
        var offset = pos.Column - startColumn;
        var start = offset;
        var end = offset;
        var lastCh = 0;

        // LEFT anchor (start)
        for (; start >= 0; start--)
        {
            // charCodeAt past the end is NaN upstream, which matches no test below
            var ch = start < word.Length ? word[start] : 0;
            if (start != offset && (ch == '_' || ch == '-'))
            {
                // foo-bar OR foo_bar
                break;
            }
            else if (Strings.IsLowerAsciiLetter(ch) && Strings.IsUpperAsciiLetter(lastCh))
            {
                // fooBar
                break;
            }
            lastCh = ch;
        }
        start += 1;

        // RIGHT anchor (end)
        for (; end < word.Length; end++)
        {
            var ch = word[end];
            if (Strings.IsUpperAsciiLetter(ch) && Strings.IsLowerAsciiLetter(lastCh))
            {
                // fooBar
                break;
            }
            else if (ch == '_' || ch == '-')
            {
                // foo-bar OR foo_bar
                break;
            }
            lastCh = ch;
        }

        if (start < end)
        {
            bucket.Add(new SelectionRange(new TextRange(pos.LineNumber, startColumn + start, pos.LineNumber, startColumn + end)));
        }
    }

    // _addWordRanges
    private void AddWordRanges(List<SelectionRange> bucket, TextModel model, TextPosition pos)
    {
        if (ModelWords.GetWordAtPosition(model, _languageConfigurationService, pos) is { } word)
        {
            bucket.Add(new SelectionRange(new TextRange(pos.LineNumber, word.StartColumn, pos.LineNumber, word.EndColumn)));
        }
    }

    // _addWhitespaceLine
    private static void AddWhitespaceLine(List<SelectionRange> bucket, TextModel model, TextPosition pos)
    {
        if (model.GetLineLength(pos.LineNumber) > 0
            && model.GetLineFirstNonWhitespaceColumn(pos.LineNumber) == 0
            && model.GetLineLastNonWhitespaceColumn(pos.LineNumber) == 0)
        {
            bucket.Add(new SelectionRange(new TextRange(pos.LineNumber, 1, pos.LineNumber, model.GetLineMaxColumn(pos.LineNumber))));
        }
    }
}

/// <summary>The word at a position by the language there (upstream's model.getWordAtPosition).</summary>
internal static class ModelWords
{
    /// <summary>TokenizationTextModelPart.getWordAtPosition.</summary>
    public static WordAtPosition? GetWordAtPosition(TextModel model, LanguageConfigurationService languageConfigurationService, TextPosition rawPosition)
    {
        var position = model.ValidatePosition(rawPosition);
        var lineContent = model.GetLineContent(position.LineNumber);
        var lineTokens = model.Tokenization.GetLineTokens(position.LineNumber);
        var tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);

        // (1). First try checking right biased word
        var (rbStartOffset, rbEndOffset) = FindLanguageBoundaries(lineTokens, tokenIndex);
        var rightBiasedWord = WordHelper.GetWordAtText(
            position.Column,
            languageConfigurationService.GetLanguageConfiguration(lineTokens.GetLanguageId(tokenIndex)).GetWordDefinition(),
            lineContent[rbStartOffset..rbEndOffset],
            rbStartOffset);
        // Make sure the result touches the original passed in position
        if (rightBiasedWord is not null && rightBiasedWord.StartColumn <= rawPosition.Column && rawPosition.Column <= rightBiasedWord.EndColumn)
        {
            return rightBiasedWord;
        }

        // (2). Else, if we were at a language boundary, check the left biased word
        if (tokenIndex > 0 && rbStartOffset == position.Column - 1)
        {
            // edge case, where `position` sits between two tokens belonging to two different languages
            var (lbStartOffset, lbEndOffset) = FindLanguageBoundaries(lineTokens, tokenIndex - 1);
            var leftBiasedWord = WordHelper.GetWordAtText(
                position.Column,
                languageConfigurationService.GetLanguageConfiguration(lineTokens.GetLanguageId(tokenIndex - 1)).GetWordDefinition(),
                lineContent[lbStartOffset..lbEndOffset],
                lbStartOffset);
            // Make sure the result touches the original passed in position
            if (leftBiasedWord is not null && leftBiasedWord.StartColumn <= rawPosition.Column && rawPosition.Column <= leftBiasedWord.EndColumn)
            {
                return leftBiasedWord;
            }
        }

        return null;
    }

    // _findLanguageBoundaries
    private static (int StartOffset, int EndOffset) FindLanguageBoundaries(Tokens.LineTokens lineTokens, int tokenIndex)
    {
        var languageId = lineTokens.GetLanguageId(tokenIndex);

        // go left until a different language is hit
        var startOffset = 0;
        for (var i = tokenIndex; i >= 0 && lineTokens.GetLanguageId(i) == languageId; i--)
        {
            startOffset = lineTokens.GetStartOffset(i);
        }

        // go right until a different language is hit
        var endOffset = lineTokens.GetLineContent().Length;
        for (int i = tokenIndex, tokenCount = lineTokens.GetCount(); i < tokenCount && lineTokens.GetLanguageId(i) == languageId; i++)
        {
            endOffset = lineTokens.GetEndOffset(i);
        }

        return (startOffset, endOffset);
    }
}
