// Ported from vscode (src/vs/editor/common/tokens/contiguousTokensStore.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a line's tokens are a uint[] or null (see ContiguousTokensEditing.cs); the lines are a List;
//  - setMultilineTokens takes the TextModel (upstream's ITextModel) and returns the changed ranges as
//    ModelTokensChangedRange values;
//  - getDefaultMetadata is a static member (TokenizationTextModelPart used it while it was a stand-in).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Represents contiguous tokens in a text model.</summary>
internal sealed class ContiguousTokensStore
{
    private readonly ILanguageIdCodec _languageIdCodec;
    private List<uint[]?> _lineTokens;

    public ContiguousTokensStore(ILanguageIdCodec languageIdCodec)
    {
        _lineTokens = new List<uint[]?>();
        _languageIdCodec = languageIdCodec;
    }

    public bool HasTokens => _lineTokens.Count > 0;

    private int Len => _lineTokens.Count;

    public void Flush() => _lineTokens = new List<uint[]?>();

    public LineTokens GetTokens(string topLevelLanguageId, int lineIndex, string lineText)
    {
        uint[]? rawLineTokens = null;
        if (lineIndex < Len)
        {
            rawLineTokens = _lineTokens[lineIndex];
        }

        if (rawLineTokens is not null && !ReferenceEquals(rawLineTokens, ContiguousTokensEditing.EmptyLineTokens))
        {
            return new LineTokens(rawLineTokens, lineText, _languageIdCodec);
        }

        var lineTokens = new uint[2];
        lineTokens[0] = (uint)lineText.Length;
        lineTokens[1] = GetDefaultMetadata(_languageIdCodec.EncodeLanguageId(topLevelLanguageId));
        return new LineTokens(lineTokens, lineText, _languageIdCodec);
    }

    private static uint[] MassageTokens(LanguageId topLevelLanguageId, int lineTextLength, uint[]? tokens)
    {
        if (lineTextLength == 0)
        {
            var hasDifferentLanguageId = false;
            if (tokens is { Length: > 1 })
            {
                hasDifferentLanguageId = TokenMetadata.GetLanguageId(tokens[1]) != topLevelLanguageId;
            }

            if (!hasDifferentLanguageId)
            {
                return ContiguousTokensEditing.EmptyLineTokens;
            }
        }

        if (tokens is null || tokens.Length == 0)
        {
            var defaultTokens = new uint[2];
            defaultTokens[0] = (uint)lineTextLength;
            defaultTokens[1] = GetDefaultMetadata(topLevelLanguageId);
            return defaultTokens;
        }

        // Ensure the last token covers the end of the text
        tokens[^2] = (uint)lineTextLength;
        return tokens;
    }

    private void EnsureLine(int lineIndex)
    {
        while (lineIndex >= Len)
        {
            _lineTokens.Add(null);
        }
    }

    private void DeleteLines(int start, int deleteCount)
    {
        if (deleteCount == 0)
        {
            return;
        }

        if (start + deleteCount > Len)
        {
            deleteCount = Len - start;
        }

        // Upstream's splice: a start past the end deletes nothing.
        if (deleteCount <= 0 || start >= Len)
        {
            return;
        }

        _lineTokens.RemoveRange(start, deleteCount);
    }

    private void InsertLines(int insertIndex, int insertCount)
    {
        if (insertCount == 0)
        {
            return;
        }

        // Upstream's arrayInsert: past the end it appends.
        _lineTokens.InsertRange(Math.Min(insertIndex, Len), new uint[]?[insertCount]);
    }

    public bool SetTokens(string topLevelLanguageId, int lineIndex, int lineTextLength, uint[]? tokens, bool checkEquality)
    {
        var massaged = MassageTokens(_languageIdCodec.EncodeLanguageId(topLevelLanguageId), lineTextLength, tokens);
        EnsureLine(lineIndex);
        var oldTokens = _lineTokens[lineIndex];
        _lineTokens[lineIndex] = massaged;

        if (checkEquality)
        {
            return !TokensEqual(oldTokens, massaged);
        }

        return false;
    }

    private static bool TokensEqual(uint[]? a, uint[]? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.AsSpan().SequenceEqual(b);
    }

    #region Editing

    public void AcceptEdit(TextRange range, int eolCount, int firstLineLength)
    {
        AcceptDeleteRange(range);
        AcceptInsertText(new TextPosition(range.StartLineNumber, range.StartColumn), eolCount, firstLineLength);
    }

    private void AcceptDeleteRange(TextRange range)
    {
        var firstLineIndex = range.StartLineNumber - 1;
        if (firstLineIndex >= Len)
        {
            return;
        }

        if (range.StartLineNumber == range.EndLineNumber)
        {
            if (range.StartColumn == range.EndColumn)
            {
                // Nothing to delete
                return;
            }

            _lineTokens[firstLineIndex] = ContiguousTokensEditing.Delete(_lineTokens[firstLineIndex], range.StartColumn - 1, range.EndColumn - 1);
            return;
        }

        _lineTokens[firstLineIndex] = ContiguousTokensEditing.DeleteEnding(_lineTokens[firstLineIndex], range.StartColumn - 1);

        var lastLineIndex = range.EndLineNumber - 1;
        uint[]? lastLineTokens = null;
        if (lastLineIndex < Len)
        {
            lastLineTokens = ContiguousTokensEditing.DeleteBeginning(_lineTokens[lastLineIndex], range.EndColumn - 1);
        }

        // Take remaining text on last line and append it to remaining text on first line
        _lineTokens[firstLineIndex] = ContiguousTokensEditing.Append(_lineTokens[firstLineIndex], lastLineTokens);

        // Delete middle lines
        DeleteLines(range.StartLineNumber, range.EndLineNumber - range.StartLineNumber);
    }

    private void AcceptInsertText(TextPosition position, int eolCount, int firstLineLength)
    {
        if (eolCount == 0 && firstLineLength == 0)
        {
            // Nothing to insert
            return;
        }

        var lineIndex = position.LineNumber - 1;
        if (lineIndex >= Len)
        {
            return;
        }

        if (eolCount == 0)
        {
            // Inserting text on one line
            _lineTokens[lineIndex] = ContiguousTokensEditing.Insert(_lineTokens[lineIndex], position.Column - 1, firstLineLength);
            return;
        }

        _lineTokens[lineIndex] = ContiguousTokensEditing.DeleteEnding(_lineTokens[lineIndex], position.Column - 1);
        _lineTokens[lineIndex] = ContiguousTokensEditing.Insert(_lineTokens[lineIndex], position.Column - 1, firstLineLength);

        InsertLines(position.LineNumber, eolCount);
    }

    #endregion

    /// <summary>Stores the tokens of several blocks of lines; returns the ranges of lines whose tokens changed.</summary>
    public List<ModelTokensChangedRange> SetMultilineTokens(IReadOnlyList<ContiguousMultilineTokens> tokens, TextModel textModel)
    {
        var ranges = new List<ModelTokensChangedRange>();
        if (tokens.Count == 0)
        {
            return ranges;
        }

        for (int i = 0, len = tokens.Count; i < len; i++)
        {
            var element = tokens[i];
            var minChangedLineNumber = 0;
            var maxChangedLineNumber = 0;
            var hasChange = false;
            for (var lineNumber = element.StartLineNumber; lineNumber <= element.EndLineNumber; lineNumber++)
            {
                if (hasChange)
                {
                    SetTokens(textModel.GetLanguageId(), lineNumber - 1, textModel.GetLineLength(lineNumber), element.GetLineTokens(lineNumber), false);
                    maxChangedLineNumber = lineNumber;
                }
                else
                {
                    var lineHasChange = SetTokens(textModel.GetLanguageId(), lineNumber - 1, textModel.GetLineLength(lineNumber), element.GetLineTokens(lineNumber), true);
                    if (lineHasChange)
                    {
                        hasChange = true;
                        minChangedLineNumber = lineNumber;
                        maxChangedLineNumber = lineNumber;
                    }
                }
            }

            if (hasChange)
            {
                ranges.Add(new ModelTokensChangedRange(minChangedLineNumber, maxChangedLineNumber));
            }
        }

        return ranges;
    }

    /// <summary>The metadata of a line's token when the language has no tokenizer.</summary>
    public static uint GetDefaultMetadata(LanguageId topLevelLanguageId) =>
        ((uint)topLevelLanguageId << MetadataConsts.LanguageIdOffset)
        | ((uint)StandardTokenType.Other << MetadataConsts.TokenTypeOffset)
        | ((uint)FontStyle.None << MetadataConsts.FontStyleOffset)
        | ((uint)ColorId.DefaultForeground << MetadataConsts.ForegroundOffset)
        | ((uint)ColorId.DefaultBackground << MetadataConsts.BackgroundOffset)
        // If there is no grammar, we just take a guess and try to match brackets.
        | MetadataConsts.BalancedBracketsMask;
}
