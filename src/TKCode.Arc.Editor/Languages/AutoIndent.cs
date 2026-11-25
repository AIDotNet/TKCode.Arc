// Ported from vscode (src/vs/editor/common/languages/autoIndent.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the module functions are in the static class AutoIndent, with strings.getLeadingWhitespace as GetLeadingWhitespace;
//  - IVirtualModel's `tokenization` member is the interface IVirtualModelTokenization; its optional forceTokenization
//    is a member every implementation has (it may do nothing). TextModel does not implement IVirtualModel:
//    TextModelVirtualModel wraps one, and the functions that upstream calls with an ITextModel where an IVirtualModel
//    is expected wrap it;
//  - IIndentConverter leaves out the optional normalizeIndentation, which nothing here calls;
//  - the results of getInheritIndentForLine and getIndentForEnter are the records InheritIndentResult and
//    IndentForEnterResult; a missing action (null upstream) is a null IndentAction?, a missing line a null int?;
//  - LanguageConfigurationService.GetLanguageConfiguration never returns null, so upstream's `!richEditSupport`
//    checks always pass;
//  - getIndentMetadata returns IndentConsts? (the bits of IndentRulesSupport.GetIndentMetadata).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The tokenization part of an <see cref="IVirtualModel"/>.</summary>
internal interface IVirtualModelTokenization
{
    IViewLineTokens GetLineTokens(int lineNumber);

    string GetLanguageId();

    string GetLanguageIdAtPosition(int lineNumber, int column);

    /// <summary>Optional upstream; an implementation that has nothing to do does nothing.</summary>
    void ForceTokenization(int lineNumber);
}

/// <summary>The parts of a model that the indentation rules read, so that a line can be replaced virtually.</summary>
internal interface IVirtualModel
{
    IVirtualModelTokenization Tokenization { get; }

    string GetLineContent(int lineNumber);
}

/// <summary>Shifts and unshifts an indentation by one level.</summary>
internal interface IIndentConverter
{
    string ShiftIndent(string indentation);

    string UnshiftIndent(string indentation);
}

/// <summary>A <see cref="TextModel"/> seen as an <see cref="IVirtualModel"/>.</summary>
internal sealed class TextModelVirtualModel : IVirtualModel, IVirtualModelTokenization
{
    private readonly TextModel _model;

    public TextModelVirtualModel(TextModel model)
    {
        _model = model;
    }

    public IVirtualModelTokenization Tokenization => this;

    public string GetLineContent(int lineNumber) => _model.GetLineContent(lineNumber);

    public IViewLineTokens GetLineTokens(int lineNumber) => _model.Tokenization.GetLineTokens(lineNumber);

    public string GetLanguageId() => _model.Tokenization.GetLanguageId();

    public string GetLanguageIdAtPosition(int lineNumber, int column) =>
        _model.Tokenization.GetLanguageIdAtPosition(lineNumber, column);

    public void ForceTokenization(int lineNumber) => _model.Tokenization.ForceTokenization(lineNumber);
}

/// <summary>The indentation a line inherits from the lines above it.</summary>
/// <param name="Indentation">The indentation of <paramref name="Line"/>.</param>
/// <param name="Action">Indent when the line should be indented once more than <paramref name="Indentation"/>.</param>
/// <param name="Line">The line the indentation comes from, if any.</param>
internal sealed record InheritIndentResult(string Indentation, IndentAction? Action, int? Line = null);

/// <summary>The indentation of the line before and of the line after an Enter.</summary>
internal sealed record IndentForEnterResult(string BeforeEnter, string AfterEnter);

/// <summary>The module functions of autoIndent.ts.</summary>
internal static class AutoIndent
{
    // /^\s+$/ and /^\s*$/, with JavaScript's \s and $.
    private static readonly Regex s_whitespaceOnly = RegExps.CreateRegExp(@"^\s+$", isRegex: true, new RegExpOptions(MatchCase: true));
    private static readonly Regex s_whitespaceOrEmpty = RegExps.CreateRegExp(@"^\s*$", isRegex: true, new RegExpOptions(MatchCase: true));

    /// <summary>
    /// Get nearest preceding line which doesn't match unIndentPattern or contains all whitespace.
    /// Result:
    /// -1: run into the boundary of embedded languages
    /// 0: every line above are invalid
    /// else: nearest preceding line of the same language
    /// </summary>
    private static int GetPrecedingValidLine(IVirtualModel model, int lineNumber, ProcessedIndentRulesSupport processedIndentRulesSupport)
    {
        var languageId = model.Tokenization.GetLanguageIdAtPosition(lineNumber, 0);
        if (lineNumber > 1)
        {
            int lastLineNumber;
            var resultLineNumber = -1;

            for (lastLineNumber = lineNumber - 1; lastLineNumber >= 1; lastLineNumber--)
            {
                if (model.Tokenization.GetLanguageIdAtPosition(lastLineNumber, 0) != languageId)
                {
                    return resultLineNumber;
                }
                var text = model.GetLineContent(lastLineNumber);
                if (processedIndentRulesSupport.ShouldIgnore(lastLineNumber) || IsWhitespaceOnly(text) || text.Length == 0)
                {
                    resultLineNumber = lastLineNumber;
                    continue;
                }

                return lastLineNumber;
            }
        }

        return -1;
    }

    /// <summary>
    /// Get inherited indentation from above lines.
    /// 1. Find the nearest preceding line which doesn't match unIndentedLinePattern.
    /// 2. If this line matches indentNextLinePattern or increaseIndentPattern, it means that the indent level of
    ///    `lineNumber` should be 1 greater than this line.
    /// 3. If this line doesn't match any indent rules
    ///   a. check whether the line above it matches indentNextLinePattern
    ///   b. If not, the indent level of this line is the result
    ///   c. If so, it means the indent of this line is *temporary*, go upward utill we find a line whose indent is not
    ///      temporary (the same workflow a -> b -> c).
    /// 4. Otherwise, we fail to get an inherited indent from aboves. Return null and we should not touch the indent of
    ///    `lineNumber`
    ///
    /// This function only return the inherited indent based on above lines, it doesn't check whether current line
    /// should decrease or not.
    /// </summary>
    public static InheritIndentResult? GetInheritIndentForLine(
        EditorAutoIndentStrategy autoIndent,
        IVirtualModel model,
        int lineNumber,
        bool honorIntentialIndent,
        LanguageConfigurationService languageConfigurationService)
    {
        if (autoIndent < EditorAutoIndentStrategy.Full)
        {
            return null;
        }

        var indentRulesSupport = languageConfigurationService.GetLanguageConfiguration(model.Tokenization.GetLanguageId()).IndentRulesSupport;
        if (indentRulesSupport is null)
        {
            return null;
        }
        var processedIndentRulesSupport = new ProcessedIndentRulesSupport(model, indentRulesSupport, languageConfigurationService);

        if (lineNumber <= 1)
        {
            return new InheritIndentResult("", null);
        }

        // Use no indent if this is the first non-blank line
        for (var priorLineNumber = lineNumber - 1; priorLineNumber > 0; priorLineNumber--)
        {
            if (model.GetLineContent(priorLineNumber).Length != 0)
            {
                break;
            }
            if (priorLineNumber == 1)
            {
                return new InheritIndentResult("", null);
            }
        }

        var precedingUnIgnoredLine = GetPrecedingValidLine(model, lineNumber, processedIndentRulesSupport);
        if (precedingUnIgnoredLine < 0)
        {
            return null;
        }
        else if (precedingUnIgnoredLine < 1)
        {
            return new InheritIndentResult("", null);
        }

        if (processedIndentRulesSupport.ShouldIncrease(precedingUnIgnoredLine) || processedIndentRulesSupport.ShouldIndentNextLine(precedingUnIgnoredLine))
        {
            var precedingUnIgnoredLineContent = model.GetLineContent(precedingUnIgnoredLine);
            return new InheritIndentResult(GetLeadingWhitespace(precedingUnIgnoredLineContent), IndentAction.Indent, precedingUnIgnoredLine);
        }
        else if (processedIndentRulesSupport.ShouldDecrease(precedingUnIgnoredLine))
        {
            var precedingUnIgnoredLineContent = model.GetLineContent(precedingUnIgnoredLine);
            return new InheritIndentResult(GetLeadingWhitespace(precedingUnIgnoredLineContent), null, precedingUnIgnoredLine);
        }
        else
        {
            // precedingUnIgnoredLine can not be ignored.
            // it doesn't increase indent of following lines
            // it doesn't increase just next line
            // so current line is not affect by precedingUnIgnoredLine
            // and then we should get a correct inheritted indentation from above lines
            if (precedingUnIgnoredLine == 1)
            {
                return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(precedingUnIgnoredLine)), null, precedingUnIgnoredLine);
            }

            var previousLine = precedingUnIgnoredLine - 1;

            var previousLineIndentMetadata = indentRulesSupport.GetIndentMetadata(model.GetLineContent(previousLine));
            if ((previousLineIndentMetadata & (IndentConsts.IncreaseMask | IndentConsts.DecreaseMask)) == 0
                && (previousLineIndentMetadata & IndentConsts.IndentNextlineMask) != 0)
            {
                var stopLine = 0;
                for (var i = previousLine - 1; i > 0; i--)
                {
                    if (processedIndentRulesSupport.ShouldIndentNextLine(i))
                    {
                        continue;
                    }
                    stopLine = i;
                    break;
                }

                return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(stopLine + 1)), null, stopLine + 1);
            }

            if (honorIntentialIndent)
            {
                return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(precedingUnIgnoredLine)), null, precedingUnIgnoredLine);
            }
            else
            {
                // search from precedingUnIgnoredLine until we find one whose indent is not temporary
                for (var i = precedingUnIgnoredLine; i > 0; i--)
                {
                    if (processedIndentRulesSupport.ShouldIncrease(i))
                    {
                        return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(i)), IndentAction.Indent, i);
                    }
                    else if (processedIndentRulesSupport.ShouldIndentNextLine(i))
                    {
                        var stopLine = 0;
                        for (var j = i - 1; j > 0; j--)
                        {
                            // Upstream tests line i here, not j (so the loop stops at once or never); kept as it is.
                            if (processedIndentRulesSupport.ShouldIndentNextLine(i))
                            {
                                continue;
                            }
                            stopLine = j;
                            break;
                        }

                        return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(stopLine + 1)), null, stopLine + 1);
                    }
                    else if (processedIndentRulesSupport.ShouldDecrease(i))
                    {
                        return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(i)), null, i);
                    }
                }

                return new InheritIndentResult(GetLeadingWhitespace(model.GetLineContent(1)), null, 1);
            }
        }
    }

    /// <summary><see cref="GetInheritIndentForLine(EditorAutoIndentStrategy, IVirtualModel, int, bool, LanguageConfigurationService)"/> on a text model.</summary>
    public static InheritIndentResult? GetInheritIndentForLine(
        EditorAutoIndentStrategy autoIndent,
        TextModel model,
        int lineNumber,
        bool honorIntentialIndent,
        LanguageConfigurationService languageConfigurationService)
    {
        return GetInheritIndentForLine(autoIndent, new TextModelVirtualModel(model), lineNumber, honorIntentialIndent, languageConfigurationService);
    }

    /// <summary>The indentation the indentation rules and on-enter rules give a line, or null to leave it alone.</summary>
    public static string? GetGoodIndentForLine(
        EditorAutoIndentStrategy autoIndent,
        IVirtualModel virtualModel,
        string languageId,
        int lineNumber,
        IIndentConverter indentConverter,
        LanguageConfigurationService languageConfigurationService)
    {
        if (autoIndent < EditorAutoIndentStrategy.Full)
        {
            return null;
        }

        var richEditSupport = languageConfigurationService.GetLanguageConfiguration(languageId);

        var indentRulesSupport = languageConfigurationService.GetLanguageConfiguration(languageId).IndentRulesSupport;
        if (indentRulesSupport is null)
        {
            return null;
        }

        var processedIndentRulesSupport = new ProcessedIndentRulesSupport(virtualModel, indentRulesSupport, languageConfigurationService);
        var indent = GetInheritIndentForLine(autoIndent, virtualModel, lineNumber, true, languageConfigurationService);

        if (indent is not null)
        {
            if (indent.Line is { } inheritLine)
            {
                // Apply enter action as long as there are only whitespace lines between inherited line and this line.
                var shouldApplyEnterRules = true;
                for (var inBetweenLine = inheritLine; inBetweenLine < lineNumber - 1; inBetweenLine++)
                {
                    if (!s_whitespaceOrEmpty.IsMatch(virtualModel.GetLineContent(inBetweenLine)))
                    {
                        shouldApplyEnterRules = false;
                        break;
                    }
                }
                if (shouldApplyEnterRules)
                {
                    var enterResult = richEditSupport.OnEnter(autoIndent, "", virtualModel.GetLineContent(inheritLine), "");

                    if (enterResult is not null)
                    {
                        var indentation = GetLeadingWhitespace(virtualModel.GetLineContent(inheritLine));

                        if (enterResult.RemoveText is { } removeText and not 0)
                        {
                            indentation = JsSubstring(indentation, 0, indentation.Length - removeText);
                        }

                        if (enterResult.IndentAction == IndentAction.Indent || enterResult.IndentAction == IndentAction.IndentOutdent)
                        {
                            indentation = indentConverter.ShiftIndent(indentation);
                        }
                        else if (enterResult.IndentAction == IndentAction.Outdent)
                        {
                            indentation = indentConverter.UnshiftIndent(indentation);
                        }

                        if (processedIndentRulesSupport.ShouldDecrease(lineNumber))
                        {
                            indentation = indentConverter.UnshiftIndent(indentation);
                        }

                        if (!string.IsNullOrEmpty(enterResult.AppendText))
                        {
                            indentation += enterResult.AppendText;
                        }

                        return GetLeadingWhitespace(indentation);
                    }
                }
            }

            if (processedIndentRulesSupport.ShouldDecrease(lineNumber))
            {
                if (indent.Action == IndentAction.Indent)
                {
                    return indent.Indentation;
                }
                else
                {
                    return indentConverter.UnshiftIndent(indent.Indentation);
                }
            }
            else
            {
                if (indent.Action == IndentAction.Indent)
                {
                    return indentConverter.ShiftIndent(indent.Indentation);
                }
                else
                {
                    return indent.Indentation;
                }
            }
        }
        return null;
    }

    /// <summary>The indentation of the line before and the line after pressing Enter in <paramref name="range"/>.</summary>
    public static IndentForEnterResult? GetIndentForEnter(
        EditorAutoIndentStrategy autoIndent,
        TextModel model,
        TextRange range,
        IIndentConverter indentConverter,
        LanguageConfigurationService languageConfigurationService)
    {
        if (autoIndent < EditorAutoIndentStrategy.Full)
        {
            return null;
        }
        var languageId = model.GetLanguageIdAtPosition(range.StartLineNumber, range.StartColumn);
        var indentRulesSupport = languageConfigurationService.GetLanguageConfiguration(languageId).IndentRulesSupport;
        if (indentRulesSupport is null)
        {
            return null;
        }

        model.Tokenization.ForceTokenization(range.StartLineNumber);
        var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
        var processedContextTokens = indentationContextProcessor.GetProcessedTokenContextAroundRange(range);
        var afterEnterProcessedTokens = processedContextTokens.AfterRangeProcessedTokens;
        var beforeEnterProcessedTokens = processedContextTokens.BeforeRangeProcessedTokens;
        var beforeEnterIndent = GetLeadingWhitespace(beforeEnterProcessedTokens.GetLineContent());

        var virtualModel = CreateVirtualModelWithModifiedTokensAtLine(model, range.StartLineNumber, beforeEnterProcessedTokens);
        var languageIsDifferentFromLineStart = IndentationLineProcessors.IsLanguageDifferentFromLineStart(model, range.Start);
        var currentLine = model.GetLineContent(range.StartLineNumber);
        var currentLineIndent = GetLeadingWhitespace(currentLine);
        var afterEnterAction = GetInheritIndentForLine(autoIndent, virtualModel, range.StartLineNumber + 1, true, languageConfigurationService);
        if (afterEnterAction is null)
        {
            var beforeEnter = languageIsDifferentFromLineStart ? currentLineIndent : beforeEnterIndent;
            return new IndentForEnterResult(beforeEnter, beforeEnter);
        }

        var afterEnterIndent = languageIsDifferentFromLineStart ? currentLineIndent : afterEnterAction.Indentation;

        if (afterEnterAction.Action == IndentAction.Indent)
        {
            afterEnterIndent = indentConverter.ShiftIndent(afterEnterIndent);
        }

        if (indentRulesSupport.ShouldDecrease(afterEnterProcessedTokens.GetLineContent()))
        {
            afterEnterIndent = indentConverter.UnshiftIndent(afterEnterIndent);
        }

        return new IndentForEnterResult(
            languageIsDifferentFromLineStart ? currentLineIndent : beforeEnterIndent,
            afterEnterIndent);
    }

    /// <summary>
    /// We should always allow intentional indentation. It means, if users change the indentation of `lineNumber` and
    /// the content of this line doesn't match decreaseIndentPattern, we should not adjust the indentation.
    /// </summary>
    public static string? GetIndentActionForType(
        CursorConfiguration cursorConfig,
        TextModel model,
        TextRange range,
        string ch,
        IIndentConverter indentConverter,
        LanguageConfigurationService languageConfigurationService)
    {
        var autoIndent = cursorConfig.AutoIndent;
        if (autoIndent < EditorAutoIndentStrategy.Full)
        {
            return null;
        }
        var languageIsDifferentFromLineStart = IndentationLineProcessors.IsLanguageDifferentFromLineStart(model, range.Start);
        if (languageIsDifferentFromLineStart)
        {
            // this line has mixed languages and indentation rules will not work
            return null;
        }

        var languageId = model.GetLanguageIdAtPosition(range.StartLineNumber, range.StartColumn);
        var indentRulesSupport = languageConfigurationService.GetLanguageConfiguration(languageId).IndentRulesSupport;
        if (indentRulesSupport is null)
        {
            return null;
        }

        var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
        var processedContextTokens = indentationContextProcessor.GetProcessedTokenContextAroundRange(range);
        var beforeRangeText = processedContextTokens.BeforeRangeProcessedTokens.GetLineContent();
        var afterRangeText = processedContextTokens.AfterRangeProcessedTokens.GetLineContent();
        var textAroundRange = beforeRangeText + afterRangeText;
        var textAroundRangeWithCharacter = beforeRangeText + ch + afterRangeText;

        // If previous content already matches decreaseIndentPattern, it means indentation of this line should already be adjusted
        // Users might change the indentation by purpose and we should honor that instead of readjusting.
        if (!indentRulesSupport.ShouldDecrease(textAroundRange) && indentRulesSupport.ShouldDecrease(textAroundRangeWithCharacter))
        {
            // after typing `ch`, the content matches decreaseIndentPattern, we should adjust the indent to a good manner.
            // 1. Get inherited indent action
            var r = GetInheritIndentForLine(autoIndent, model, range.StartLineNumber, false, languageConfigurationService);
            if (r is null)
            {
                return null;
            }

            var indentation = r.Indentation;
            if (r.Action != IndentAction.Indent)
            {
                indentation = indentConverter.UnshiftIndent(indentation);
            }

            return indentation;
        }

        var previousLineNumber = range.StartLineNumber - 1;
        if (range.IsEmpty && previousLineNumber > 0)
        {
            var previousLine = model.GetLineContent(previousLineNumber);
            if (indentRulesSupport.ShouldIndentNextLine(previousLine) && indentRulesSupport.ShouldIncrease(textAroundRangeWithCharacter))
            {
                var inheritedIndentationData = GetInheritIndentForLine(autoIndent, model, range.StartLineNumber, false, languageConfigurationService);
                var inheritedIndentation = inheritedIndentationData?.Indentation;
                if (inheritedIndentation is not null)
                {
                    var currentLine = model.GetLineContent(range.StartLineNumber);
                    var actualCurrentIndentation = GetLeadingWhitespace(currentLine);
                    var inferredCurrentIndentation = indentConverter.ShiftIndent(inheritedIndentation);
                    // If the inferred current indentation is not equal to the actual current indentation, then the indentation has been intentionally changed, in that case keep it
                    var inferredIndentationEqualsActual = inferredCurrentIndentation == actualCurrentIndentation;
                    var textAroundRangeContainsOnlyWhitespace = s_whitespaceOrEmpty.IsMatch(textAroundRange);
                    var autoClosingPairExists =
                        cursorConfig.AutoClosingPairs.AutoClosingPairsOpenByEnd.TryGetValue(ch, out var autoClosingPairs)
                        && autoClosingPairs.Count > 0;
                    var isChFirstNonWhitespaceCharacterAndInAutoClosingPair = autoClosingPairExists && textAroundRangeContainsOnlyWhitespace;
                    if (inferredIndentationEqualsActual && isChFirstNonWhitespaceCharacterAndInAutoClosingPair)
                    {
                        return inheritedIndentation;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>The indentation rule bits of a line, or null when the language has no indentation rules.</summary>
    public static IndentConsts? GetIndentMetadata(
        TextModel model,
        int lineNumber,
        LanguageConfigurationService languageConfigurationService)
    {
        var indentRulesSupport = languageConfigurationService.GetLanguageConfiguration(model.GetLanguageId()).IndentRulesSupport;
        if (indentRulesSupport is null)
        {
            return null;
        }
        if (lineNumber < 1 || lineNumber > model.GetLineCount())
        {
            return null;
        }
        return indentRulesSupport.GetIndentMetadata(model.GetLineContent(lineNumber));
    }

    private static IVirtualModel CreateVirtualModelWithModifiedTokensAtLine(TextModel model, int modifiedLineNumber, IViewLineTokens modifiedTokens)
    {
        return new ModifiedLineVirtualModel(model, modifiedLineNumber, modifiedTokens);
    }

    /// <summary>strings.getLeadingWhitespace: the leading spaces and tabs of <paramref name="str"/>.</summary>
    internal static string GetLeadingWhitespace(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            var chCode = str[i];
            if (chCode != ' ' && chCode != '\t')
            {
                return str.Substring(0, i);
            }
        }
        return str;
    }

    // JavaScript's /^\s+$/.test(text).
    private static bool IsWhitespaceOnly(string text)
    {
        return s_whitespaceOnly.IsMatch(text);
    }

    // String.prototype.substring: clamps both indices to [0, length] and swaps them when start > end.
    private static string JsSubstring(string str, int start, int end)
    {
        start = Math.Clamp(start, 0, str.Length);
        end = Math.Clamp(end, 0, str.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }
        return str.Substring(start, end - start);
    }

    /// <summary>A model whose line <c>modifiedLineNumber</c> has other tokens (and so other content).</summary>
    private sealed class ModifiedLineVirtualModel : IVirtualModel, IVirtualModelTokenization
    {
        private readonly TextModel _model;
        private readonly int _modifiedLineNumber;
        private readonly IViewLineTokens _modifiedTokens;

        public ModifiedLineVirtualModel(TextModel model, int modifiedLineNumber, IViewLineTokens modifiedTokens)
        {
            _model = model;
            _modifiedLineNumber = modifiedLineNumber;
            _modifiedTokens = modifiedTokens;
        }

        public IVirtualModelTokenization Tokenization => this;

        public IViewLineTokens GetLineTokens(int lineNumber)
        {
            if (lineNumber == _modifiedLineNumber)
            {
                return _modifiedTokens;
            }
            else
            {
                return _model.Tokenization.GetLineTokens(lineNumber);
            }
        }

        public string GetLanguageId()
        {
            return _model.GetLanguageId();
        }

        public string GetLanguageIdAtPosition(int lineNumber, int column)
        {
            return _model.GetLanguageIdAtPosition(lineNumber, column);
        }

        // Upstream's virtual model has no forceTokenization.
        public void ForceTokenization(int lineNumber)
        {
        }

        public string GetLineContent(int lineNumber)
        {
            if (lineNumber == _modifiedLineNumber)
            {
                return _modifiedTokens.GetLineContent();
            }
            else
            {
                return _model.GetLineContent(lineNumber);
            }
        }
    }
}
