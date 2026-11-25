// Ported from vscode (src/vs/editor/common/languages/enterAction.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the module function getEnterAction is in the static class EnterActions (EnterAction is the record of
//    LanguageConfiguration.cs);
//  - LanguageConfigurationService.GetLanguageConfiguration never returns null, so upstream's `!richEditSupport` check
//    always passes;
//  - substrings clamp their indices as JavaScript's String.prototype.substring does.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The module functions of enterAction.ts.</summary>
internal static class EnterActions
{
    /// <summary>
    /// What pressing Enter in <paramref name="range"/> does by the language's on-enter rules and brackets, with the
    /// indentation of the line resolved, or null when no rule applies.
    /// </summary>
    public static CompleteEnterAction? GetEnterAction(
        EditorAutoIndentStrategy autoIndent,
        TextModel model,
        TextRange range,
        LanguageConfigurationService languageConfigurationService)
    {
        model.Tokenization.ForceTokenization(range.StartLineNumber);
        var languageId = model.GetLanguageIdAtPosition(range.StartLineNumber, range.StartColumn);
        var richEditSupport = languageConfigurationService.GetLanguageConfiguration(languageId);
        var indentationContextProcessor = new IndentationContextProcessor(model, languageConfigurationService);
        var processedContextTokens = indentationContextProcessor.GetProcessedTokenContextAroundRange(range);
        var previousLineText = processedContextTokens.PreviousLineProcessedTokens.GetLineContent();
        var beforeEnterText = processedContextTokens.BeforeRangeProcessedTokens.GetLineContent();
        var afterEnterText = processedContextTokens.AfterRangeProcessedTokens.GetLineContent();

        var enterResult = richEditSupport.OnEnter(autoIndent, previousLineText, beforeEnterText, afterEnterText);
        if (enterResult is null)
        {
            return null;
        }

        var indentAction = enterResult.IndentAction;
        var appendText = enterResult.AppendText;
        var removeText = enterResult.RemoveText ?? 0;

        // Here we add `\t` to appendText first because enterAction is leveraging appendText and removeText to change indentation.
        if (string.IsNullOrEmpty(appendText))
        {
            if (indentAction == IndentAction.Indent || indentAction == IndentAction.IndentOutdent)
            {
                appendText = "\t";
            }
            else
            {
                appendText = "";
            }
        }
        else if (indentAction == IndentAction.Indent)
        {
            appendText = "\t" + appendText;
        }

        var indentation = LanguageConfigurations.GetIndentationAtPosition(model, range.StartLineNumber, range.StartColumn);
        if (removeText != 0)
        {
            var end = Math.Clamp(indentation.Length - removeText, 0, indentation.Length);
            indentation = indentation.Substring(0, end);
        }

        return new CompleteEnterAction(indentAction, appendText, removeText, indentation);
    }
}
