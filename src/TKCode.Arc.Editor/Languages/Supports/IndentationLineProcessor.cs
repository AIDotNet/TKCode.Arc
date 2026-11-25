// Ported from vscode (src/vs/editor/common/languages/supports/indentationLineProcessor.ts, commit b57d5c9e), MIT
// license.
// Differences from upstream:
//  - the module function isLanguageDifferentFromLineStart is in the static class IndentationLineProcessors;
//  - ProcessedIndentRulesSupport and IndentationLineProcessor take an IVirtualModel (AutoIndent.cs); a TextModel is
//    wrapped in a TextModelVirtualModel, which ProcessedIndentRulesSupport has a constructor for;
//  - the optional newIndentation parameters are nullable strings (null for upstream's undefined);
//  - getProcessedTokenContextAroundRange returns the record ProcessedTokenContext;
//  - the brackets expression has no global flag in .NET; Regex.Replace replaces every match, as the global flag
//    makes String.prototype.replace do.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// This class is a wrapper class around <see cref="IndentRulesSupport"/>.
/// It processes the lines by removing the language configuration brackets from the regex, string and comment tokens.
/// It then calls into the <see cref="IndentRulesSupport"/> to validate the indentation conditions.
/// </summary>
internal sealed class ProcessedIndentRulesSupport
{
    private readonly IndentRulesSupport _indentRulesSupport;
    private readonly IndentationLineProcessor _indentationLineProcessor;

    public ProcessedIndentRulesSupport(
        IVirtualModel model,
        IndentRulesSupport indentRulesSupport,
        LanguageConfigurationService languageConfigurationService)
    {
        _indentRulesSupport = indentRulesSupport;
        _indentationLineProcessor = new IndentationLineProcessor(model, languageConfigurationService);
    }

    public ProcessedIndentRulesSupport(
        TextModel model,
        IndentRulesSupport indentRulesSupport,
        LanguageConfigurationService languageConfigurationService)
        : this(new TextModelVirtualModel(model), indentRulesSupport, languageConfigurationService)
    {
    }

    /// <summary>
    /// Apply the new indentation and return whether the indentation level should be increased after the given line number
    /// </summary>
    public bool ShouldIncrease(int lineNumber, string? newIndentation = null)
    {
        var processedLine = _indentationLineProcessor.GetProcessedLine(lineNumber, newIndentation);
        return _indentRulesSupport.ShouldIncrease(processedLine);
    }

    /// <summary>
    /// Apply the new indentation and return whether the indentation level should be decreased after the given line number
    /// </summary>
    public bool ShouldDecrease(int lineNumber, string? newIndentation = null)
    {
        var processedLine = _indentationLineProcessor.GetProcessedLine(lineNumber, newIndentation);
        return _indentRulesSupport.ShouldDecrease(processedLine);
    }

    /// <summary>
    /// Apply the new indentation and return whether the indentation level should remain unchanged at the given line number
    /// </summary>
    public bool ShouldIgnore(int lineNumber, string? newIndentation = null)
    {
        var processedLine = _indentationLineProcessor.GetProcessedLine(lineNumber, newIndentation);
        return _indentRulesSupport.ShouldIgnore(processedLine);
    }

    /// <summary>
    /// Apply the new indentation and return whether the indentation level should increase on the line after the given line number
    /// </summary>
    public bool ShouldIndentNextLine(int lineNumber, string? newIndentation = null)
    {
        var processedLine = _indentationLineProcessor.GetProcessedLine(lineNumber, newIndentation);
        return _indentRulesSupport.ShouldIndentNextLine(processedLine);
    }
}

/// <summary>The processed text around a range (the result of <see cref="IndentationContextProcessor.GetProcessedTokenContextAroundRange"/>).</summary>
internal sealed record ProcessedTokenContext(
    IViewLineTokens BeforeRangeProcessedTokens,
    IViewLineTokens AfterRangeProcessedTokens,
    IViewLineTokens PreviousLineProcessedTokens);

/// <summary>
/// This class fetches the processed text around a range which can be used for indentation evaluation.
/// It returns:
/// - The processed text before the given range and on the same start line
/// - The processed text after the given range and on the same end line
/// - The processed text on the previous line
/// </summary>
internal sealed class IndentationContextProcessor
{
    private readonly TextModel _model;
    private readonly IndentationLineProcessor _indentationLineProcessor;

    public IndentationContextProcessor(TextModel model, LanguageConfigurationService languageConfigurationService)
    {
        _model = model;
        _indentationLineProcessor = new IndentationLineProcessor(new TextModelVirtualModel(model), languageConfigurationService);
    }

    /// <summary>
    /// Returns the processed text, stripped from the language configuration brackets within the string, comment and
    /// regex tokens, around the given range
    /// </summary>
    public ProcessedTokenContext GetProcessedTokenContextAroundRange(TextRange range)
    {
        var beforeRangeProcessedTokens = GetProcessedTokensBeforeRange(range);
        var afterRangeProcessedTokens = GetProcessedTokensAfterRange(range);
        var previousLineProcessedTokens = GetProcessedPreviousLineTokens(range);
        return new ProcessedTokenContext(beforeRangeProcessedTokens, afterRangeProcessedTokens, previousLineProcessedTokens);
    }

    private IViewLineTokens GetProcessedTokensBeforeRange(TextRange range)
    {
        _model.Tokenization.ForceTokenization(range.StartLineNumber);
        var lineTokens = _model.Tokenization.GetLineTokens(range.StartLineNumber);
        var scopedLineTokens = Supports.CreateScopedLineTokens(lineTokens, range.StartColumn - 1);
        IViewLineTokens slicedTokens;
        if (IndentationLineProcessors.IsLanguageDifferentFromLineStart(_model, range.Start))
        {
            var columnIndexWithinScope = (range.StartColumn - 1) - scopedLineTokens.FirstCharOffset;
            var firstCharacterOffset = scopedLineTokens.FirstCharOffset;
            var lastCharacterOffset = firstCharacterOffset + columnIndexWithinScope;
            slicedTokens = lineTokens.SliceAndInflate(firstCharacterOffset, lastCharacterOffset, 0);
        }
        else
        {
            var columnWithinLine = range.StartColumn - 1;
            slicedTokens = lineTokens.SliceAndInflate(0, columnWithinLine, 0);
        }
        var processedTokens = _indentationLineProcessor.GetProcessedTokens(slicedTokens);
        return processedTokens;
    }

    private IViewLineTokens GetProcessedTokensAfterRange(TextRange range)
    {
        var position = range.IsEmpty ? range.Start : range.End;
        _model.Tokenization.ForceTokenization(position.LineNumber);
        var lineTokens = _model.Tokenization.GetLineTokens(position.LineNumber);
        var scopedLineTokens = Supports.CreateScopedLineTokens(lineTokens, position.Column - 1);
        var columnIndexWithinScope = position.Column - 1 - scopedLineTokens.FirstCharOffset;
        var firstCharacterOffset = scopedLineTokens.FirstCharOffset + columnIndexWithinScope;
        var lastCharacterOffset = scopedLineTokens.FirstCharOffset + scopedLineTokens.GetLineLength();
        var slicedTokens = lineTokens.SliceAndInflate(firstCharacterOffset, lastCharacterOffset, 0);
        var processedTokens = _indentationLineProcessor.GetProcessedTokens(slicedTokens);
        return processedTokens;
    }

    private IViewLineTokens GetProcessedPreviousLineTokens(TextRange range)
    {
        ScopedLineTokens GetScopedLineTokensAtEndColumnOfLine(int lineNumber)
        {
            _model.Tokenization.ForceTokenization(lineNumber);
            var lineTokens = _model.Tokenization.GetLineTokens(lineNumber);
            var endColumnOfLine = _model.GetLineMaxColumn(lineNumber) - 1;
            var scopedLineTokensAtEndColumn = Supports.CreateScopedLineTokens(lineTokens, endColumnOfLine);
            return scopedLineTokensAtEndColumn;
        }

        _model.Tokenization.ForceTokenization(range.StartLineNumber);
        var lineTokens = _model.Tokenization.GetLineTokens(range.StartLineNumber);
        var scopedLineTokens = Supports.CreateScopedLineTokens(lineTokens, range.StartColumn - 1);
        var emptyTokens = LineTokens.CreateEmpty("", scopedLineTokens.LanguageIdCodec);
        var previousLineNumber = range.StartLineNumber - 1;
        var isFirstLine = previousLineNumber == 0;
        if (isFirstLine)
        {
            return emptyTokens;
        }
        var canScopeExtendOnPreviousLine = scopedLineTokens.FirstCharOffset == 0;
        if (!canScopeExtendOnPreviousLine)
        {
            return emptyTokens;
        }
        var scopedLineTokensAtEndColumnOfPreviousLine = GetScopedLineTokensAtEndColumnOfLine(previousLineNumber);
        var doesLanguageContinueOnPreviousLine = scopedLineTokens.LanguageId == scopedLineTokensAtEndColumnOfPreviousLine.LanguageId;
        if (!doesLanguageContinueOnPreviousLine)
        {
            return emptyTokens;
        }
        var previousSlicedLineTokens = scopedLineTokensAtEndColumnOfPreviousLine.ToIViewLineTokens();
        var processedTokens = _indentationLineProcessor.GetProcessedTokens(previousSlicedLineTokens);
        return processedTokens;
    }
}

/// <summary>
/// This class performs the actual processing of the indentation lines.
/// The brackets of the language configuration are removed from the regex, string and comment tokens.
/// </summary>
internal sealed class IndentationLineProcessor
{
    private readonly IVirtualModel _model;
    private readonly LanguageConfigurationService _languageConfigurationService;

    public IndentationLineProcessor(IVirtualModel model, LanguageConfigurationService languageConfigurationService)
    {
        _model = model;
        _languageConfigurationService = languageConfigurationService;
    }

    /// <summary>
    /// Get the processed line for the given line number and potentially adjust the indentation level.
    /// Remove the language configuration brackets from the regex, string and comment tokens.
    /// </summary>
    public string GetProcessedLine(int lineNumber, string? newIndentation = null)
    {
        static string ReplaceIndentation(string line, string newIndentation)
        {
            var currentIndentation = AutoIndent.GetLeadingWhitespace(line);
            var adjustedLine = newIndentation + line.Substring(currentIndentation.Length);
            return adjustedLine;
        }

        _model.Tokenization.ForceTokenization(lineNumber);
        var tokens = _model.Tokenization.GetLineTokens(lineNumber);
        var processedLine = GetProcessedTokens(tokens).GetLineContent();
        if (newIndentation is not null)
        {
            processedLine = ReplaceIndentation(processedLine, newIndentation);
        }
        return processedLine;
    }

    /// <summary>
    /// Process the line with the given tokens, remove the language configuration brackets from the regex, string and
    /// comment tokens.
    /// </summary>
    public IViewLineTokens GetProcessedTokens(IViewLineTokens tokens)
    {
        static bool ShouldRemoveBracketsFromTokenType(StandardTokenType tokenType)
        {
            return tokenType == StandardTokenType.String
                || tokenType == StandardTokenType.RegEx
                || tokenType == StandardTokenType.Comment;
        }

        var languageId = tokens.GetLanguageId(0);
        var bracketsConfiguration = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew;
        var bracketsRegExp = bracketsConfiguration.GetBracketRegExp(new RegExpOptions(Global: true));
        var textAndMetadata = new List<(string Text, uint Metadata)>();
        tokens.ForEach(tokenIndex =>
        {
            var tokenType = tokens.GetStandardTokenType(tokenIndex);
            var text = tokens.GetTokenText(tokenIndex);
            if (ShouldRemoveBracketsFromTokenType(tokenType))
            {
                text = bracketsRegExp.Replace(text, "");
            }
            var metadata = tokens.GetMetadata(tokenIndex);
            textAndMetadata.Add((text, metadata));
        });
        var processedLineTokens = LineTokens.CreateFromTextAndMetadata(textAndMetadata, tokens.LanguageIdCodec);
        return processedLineTokens;
    }
}

/// <summary>The module functions of indentationLineProcessor.ts.</summary>
internal static class IndentationLineProcessors
{
    /// <summary>Whether the language at <paramref name="position"/> is an embedded one that starts after the line start.</summary>
    public static bool IsLanguageDifferentFromLineStart(TextModel model, TextPosition position)
    {
        model.Tokenization.ForceTokenization(position.LineNumber);
        var lineTokens = model.Tokenization.GetLineTokens(position.LineNumber);
        var scopedLineTokens = Supports.CreateScopedLineTokens(lineTokens, position.Column - 1);
        var doesScopeStartAtOffsetZero = scopedLineTokens.FirstCharOffset == 0;
        var isScopedLanguageEqualToFirstLanguageOnLine = lineTokens.GetLanguageId(0) == scopedLineTokens.LanguageId;
        var languageIsDifferentFromLineStart = !doesScopeStartAtOffsetZero && !isScopedLanguageEqualToFirstLanguageOnLine;
        return languageIsDifferentFromLineStart;
    }
}
