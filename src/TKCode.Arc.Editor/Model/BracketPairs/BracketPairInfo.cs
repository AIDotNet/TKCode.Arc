// Ported from vscode (src/vs/editor/common/textModelBracketPairs.ts: IFoundBracket, BracketInfo, BracketPairInfo,
// BracketPairWithMinIndentationInfo, commit b57d5c9e), MIT license.

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>A bracket found in the model, with its kind.</summary>
internal sealed record FoundBracket(TextRange Range, BracketKindBase BracketInfo);

/// <summary>A bracket and how deep it is nested.</summary>
/// <param name="Range">The bracket's range.</param>
/// <param name="NestingLevel">0-based level.</param>
/// <param name="NestingLevelOfEqualBracketType">0-based level among the brackets of the same kind.</param>
/// <param name="IsInvalid">Whether it is a closing bracket without an opening one.</param>
internal sealed record BracketInfo(TextRange Range, int NestingLevel, int NestingLevelOfEqualBracketType, bool IsInvalid);

/// <summary>A bracket pair: its whole range, the ranges of its brackets (no closing one if it is unclosed) and its nesting.</summary>
internal class BracketPairInfo(
    TextRange range,
    TextRange openingBracketRange,
    TextRange? closingBracketRange,
    int nestingLevel,
    int nestingLevelOfEqualBracketType,
    PairAstNode bracketPairNode)
{
    public TextRange Range { get; } = range;

    public TextRange OpeningBracketRange { get; } = openingBracketRange;

    public TextRange? ClosingBracketRange { get; } = closingBracketRange;

    /// <summary>0-based.</summary>
    public int NestingLevel { get; } = nestingLevel;

    public int NestingLevelOfEqualBracketType { get; } = nestingLevelOfEqualBracketType;

    public OpeningBracketKind OpeningBracketInfo => (OpeningBracketKind)bracketPairNode.OpeningBracket.BracketInfo;

    public ClosingBracketKind? ClosingBracketInfo => bracketPairNode.ClosingBracket?.BracketInfo as ClosingBracketKind;
}

internal sealed class BracketPairWithMinIndentationInfo(
    TextRange range,
    TextRange openingBracketRange,
    TextRange? closingBracketRange,
    int nestingLevel,
    int nestingLevelOfEqualBracketType,
    PairAstNode bracketPairNode,
    int minVisibleColumnIndentation)
    : BracketPairInfo(range, openingBracketRange, closingBracketRange, nestingLevel, nestingLevelOfEqualBracketType, bracketPairNode)
{
    /// <summary>-1 if not requested, otherwise the size of the minimum indentation in the bracket pair in terms of visible columns.</summary>
    public int MinVisibleColumnIndentation { get; } = minVisibleColumnIndentation;
}
