// Adaptation of vscode (src/vs/editor/common/model/textModel.ts: getWordAtPosition and getWordUntilPosition, commit
// b57d5c9e), MIT license.
// Differences from upstream: helpers outside TextModel (so that the model file stays as it is); the word definition is
// the model language's (through a LanguageConfigurationService when one is given, else the default word definition);
// upstream also looks at the language of embedded tokens left of the position, which is not ported.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib;

/// <summary>The word at or before a position of a model (upstream's ITextModel.getWordAtPosition / getWordUntilPosition).</summary>
internal static class ModelWords
{
    /// <summary>The word that contains or touches <paramref name="position"/>, or null.</summary>
    public static WordAtPosition? GetWordAtPosition(TextModel model, TextPosition position, LanguageConfigurationService? languageConfigurationService = null)
    {
        var validated = model.ValidatePosition(position);
        var wordDefinition = languageConfigurationService?.GetLanguageConfiguration(model.GetLanguageId()).GetWordDefinition()
            ?? WordHelper.DefaultWordRegExp;
        return WordHelper.GetWordAtText(validated.Column, wordDefinition, model.GetLineContent(validated.LineNumber), 0);
    }

    /// <summary>The part of the word at <paramref name="position"/> left of it; an empty word at the position when there is none.</summary>
    public static WordAtPosition GetWordUntilPosition(TextModel model, TextPosition position, LanguageConfigurationService? languageConfigurationService = null)
    {
        var validated = model.ValidatePosition(position);
        var word = GetWordAtPosition(model, validated, languageConfigurationService);
        if (word is null)
        {
            return new WordAtPosition(string.Empty, validated.Column, validated.Column);
        }

        return new WordAtPosition(word.Word[..(validated.Column - word.StartColumn)], word.StartColumn, validated.Column);
    }
}
